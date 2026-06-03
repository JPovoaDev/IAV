using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

// Evolução natural do LLMAgentWithActions da aula 10:
//
//   • Tools agora declaram um schema de `parameters` (com `type`, `enum`, `required`)
//   • Respostas do Ollama com `arguments` desserializadas via Newtonsoft.Json
//     (JsonUtility da Unity não trata JSON dinâmico — objectos com chaves variáveis)
//   • Despacho via Dictionary<string, Action<JObject>> registado, em vez de
//     gameObject.SendMessage(name). Cada handler recebe os arguments como JObject
//     e extrai as chaves que conhece.
//
// Requer: Window → Package Manager → Add package by name → com.unity.nuget.newtonsoft-json

public class LLMAgentWithParameterizedActions : MonoBehaviour
{
    [SerializeField] private string apiUrl = "http://localhost:11434/api/chat";
    //[SerializeField] private string modelName = "qwen2.5:3b";
    [SerializeField] private string modelName = "mistral:7b";
    //[SerializeField] private string modelName = "llama3.2:3b";


    [SerializeField] private AgentConfigV2 agentConfig;

    [Tooltip("Mensagens user+assistant a manter (não conta o system prompt).")]
    [SerializeField] private int windowSize = 10;

    [Tooltip("Mesmo com schema validation, alguns modelos chamam tools fora da lista. Rede de segurança.")]
    [SerializeField] private bool validateAgainstDeclaredTools = true;

    private TMP_Text agentReplyText;
    private readonly List<ChatMessage> history = new();
    private readonly Dictionary<string, Action<JObject>> handlers = new();

    // ── Registo de tools (chamado pelos componentes que sabem agir) ──────────

    public void RegisterTool(string name, Action<JObject> handler)
    {
        handlers[name] = handler;
    }

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    private void Awake()
    {
        var go = GameObject.FindWithTag("AgentAnswer");
        if (go != null) agentReplyText = go.GetComponent<TMP_Text>();
    }

    public void Talk(string message)
    {
        if (agentReplyText != null) agentReplyText.text = "<a pensar...>";
        StartCoroutine(SendToLLM(message));
    }

    // ── Pipeline principal ───────────────────────────────────────────────────

    private IEnumerator SendToLLM(string userMessage)
    {
        if (agentConfig == null)
        {
            if (agentReplyText != null) agentReplyText.text = "AgentConfig não atribuído.";
            yield break;
        }

        history.Add(new ChatMessage { role = "user", content = userMessage });
        TrimHistory();

        // Constrói payload manualmente com JObject — JsonUtility não cobre o
        // schema de tools (objectos com chaves variáveis no `parameters`).
        var requestJson = BuildRequestJson(userMessage);

        using var http = new UnityWebRequest(apiUrl, "POST");
        http.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestJson));
        http.downloadHandler = new DownloadHandlerBuffer();
        http.SetRequestHeader("Content-Type", "application/json");

        yield return http.SendWebRequest();
        Debug.Log($"RAW RESPONSE: {http.downloadHandler.text}");

        if (http.result != UnityWebRequest.Result.Success)
        {
            if (agentReplyText != null) agentReplyText.text = agentConfig.DefaultErrorAnswer;
            Debug.LogWarning($"LLM error: {http.error}\n{http.downloadHandler?.text}");
            history.RemoveAt(history.Count - 1);
            yield break;
        }

        JObject resp;
        try { resp = JObject.Parse(http.downloadHandler.text); }
        catch (Exception e) { Debug.LogError($"JSON parse: {e.Message}"); yield break; }

        var message = resp["message"] as JObject;
        if (message == null) yield break;

        // 1) Texto falado pelo NPC
        string spoken = message["content"]?.ToString();
        var toolCalls = message["tool_calls"] as JArray;

        // Se há tool call mas sem texto, não mostrar "..."
        if (string.IsNullOrWhiteSpace(spoken))
        {
            spoken = (toolCalls != null && toolCalls.Count > 0) ? null : "...";
        }

        if (spoken != null)
        {
            if (agentReplyText != null) agentReplyText.text = spoken;
            history.Add(new ChatMessage { role = "assistant", content = spoken });
            TrimHistory();
        }

        // 2) Acções a executar (T3 do feedback fica nos handlers)
        // Fallback: se não há tool_calls, tenta parsear texto
        if ((toolCalls == null || toolCalls.Count == 0) && !string.IsNullOrWhiteSpace(spoken))
        {
            TryParseTextAsToolCall(spoken);
        }
        if (toolCalls == null) yield break;

        foreach (var call in toolCalls)
        {
            var fn = call["function"] as JObject;
            if (fn == null) continue;

            string toolName = fn["name"]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(toolName)) continue;

            if (validateAgainstDeclaredTools && !agentConfig.IsDeclared(toolName))
            {
                Debug.LogWarning($"Tool rejeitada (não declarada no AgentConfig): '{toolName}'");
                continue;
            }

            if (!handlers.TryGetValue(toolName, out var handler))
            {
                Debug.LogWarning($"Tool sem handler registado: '{toolName}'");
                continue;
            }

            var arguments = (fn["arguments"] as JObject) ?? new JObject();
            Debug.Log($"Tool call: {toolName} args={arguments.ToString(Formatting.None)}");

            try { handler(arguments); }
            catch (Exception e) { Debug.LogError($"Handler '{toolName}' falhou: {e.Message}"); }
        }
    }

    // ── Construção do payload ────────────────────────────────────────────────

    private string BuildRequestJson(string userMessage)
    {
        var messages = new JArray
        {
            new JObject { ["role"] = "system",  ["content"] = BuildSystemPrompt() },
        };
        foreach (var m in history)
            messages.Add(new JObject { ["role"] = m.role, ["content"] = m.content });

        var tools = new JArray();
        foreach (var t in agentConfig.Tools)
            tools.Add(t.ToToolJson());

        var req = new JObject
        {
            ["model"]    = modelName,
            ["stream"]   = false,
            ["messages"] = messages,
            ["tools"]    = tools,
        };
        return req.ToString(Formatting.None);
    }

    private string BuildSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine(agentConfig.ContextPrompt);
        sb.AppendLine();
        sb.AppendLine(agentConfig.AnswerGuideline);
        return sb.ToString();
    }

    private void TrimHistory()
    {
        while (history.Count > windowSize)
            history.RemoveAt(0);
    }
    private void TryParseTextAsToolCall(string text)
    {
        string t = text.ToLower();

        // Detect Investigate + zona
        string zone = null;
        if (t.Contains("corredor")) zone = "corredor";
        else if (t.Contains("arquivo")) zone = "arquivo";
        else if (t.Contains("laboratorio") || t.Contains("laboratório")) zone = "laboratorio";

        if (zone != null && handlers.TryGetValue("Investigate", out var handler))
        {
            var args = new JObject { ["zone"] = zone };
            Debug.Log($"[Fallback] Tool call: Investigate zone={zone}");
            handler(args);
            return;
        }

        // Outros fallbacks se necessário
        if (t.Contains("alarm") && handlers.TryGetValue("ActivateAlarm", out var alarm))
            alarm(new JObject());
        if (t.Contains("lockdown") && handlers.TryGetValue("LockDown", out var ld))
            ld(new JObject());
    }
}


// Reutiliza o tipo ChatMessage que já vinha da aula 10 (role + content).
// Se a tua versão de aula 10 não tornou o tipo público no namespace global,
// descomenta o bloco abaixo.
//
// [Serializable]
// public class ChatMessage
// {
//     public string role;
//     public string content;
// }
