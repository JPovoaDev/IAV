using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

// Padrão de tool calling (function calling) nativo do Ollama — o caminho que
// OpenAI, Anthropic, Google, MCP e Ollama todos usam. As acções são declaradas
// no campo `tools:[]` do request; o LLM escolhe se chama, qual, e quando, e
// devolve `message.tool_calls` validado contra o schema.
//
// Requer Ollama ≥ 0.3 e modelo com tool support (llama3.2:3b, qwen2.5, llama3.1).

[Serializable]
public class ToolParameterProperties { /* vazio → emite "properties":{} */ }

[Serializable]
public class ToolFunctionSchema
{
    public string type = "object";
    public ToolParameterProperties properties = new();
}

[Serializable]
public class ToolFunction
{
    public string name;
    public string description;
    public ToolFunctionSchema parameters = new();
}

[Serializable]
public class Tool
{
    public string type = "function";
    public ToolFunction function;
}

[Serializable]
public class ChatRequestWithTools
{
    public string model;
    public List<ChatMessage> messages;
    public bool stream;
    public List<Tool> tools;
}

[Serializable]
public class ToolCallFunction
{
    public string name;
    // arguments fica fora — JsonUtility não suporta JSON dinâmico. Para tools
    // com parâmetros (B5 do guião) usar System.Text.Json ou Newtonsoft.
}

[Serializable]
public class ToolCallEntry
{
    public string type;
    public ToolCallFunction function;
}

[Serializable]
public class AssistantMessage
{
    public string role;
    public string content;
    public List<ToolCallEntry> tool_calls;
}

[Serializable]
public class ChatToolResponse
{
    public AssistantMessage message;
}

public class LLMAgentWithActions : MonoBehaviour
{
    [SerializeField] private string apiUrl = "http://localhost:11434/api/chat";
    [SerializeField] private string modelName = "qwen2.5:3b";

    [SerializeField] private AgentConfig agentConfig;

    [Tooltip("Mensagens user+assistant a manter (não conta o system prompt).")]
    [SerializeField] private int windowSize = 10;

    [Tooltip("Mesmo com schema validation, alguns modelos chamam tools fora da lista. Manter como rede de segurança.")]
    [SerializeField] private bool validateAgainstDeclaredTools = true;

    private TMP_Text agentReplyText;
    private readonly List<ChatMessage> history = new();

    private void Awake()
    {
        agentReplyText = GameObject.FindWithTag("AgentAnswer").GetComponent<TMP_Text>();
    }

    public void Talk(string message)
    {
        agentReplyText.text = "<a pensar...>";
        StartCoroutine(SendToLLM(message));
    }

    private IEnumerator SendToLLM(string userMessage)
    {
        if (agentConfig == null)
        {
            agentReplyText.text = "AgentConfig não atribuído.";
            yield break;
        }

        history.Add(new ChatMessage { role = "user", content = userMessage });
        TrimHistory();

        var messages = new List<ChatMessage>(history.Count + 1)
        {
            new ChatMessage { role = "system", content = BuildSystemPrompt() },
        };
        messages.AddRange(history);

        var request = new ChatRequestWithTools
        {
            model = modelName,
            stream = false,
            messages = messages,
            tools = BuildTools(),
        };

        string json = JsonUtility.ToJson(request);

        using var http = new UnityWebRequest(apiUrl, "POST");
        http.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        http.downloadHandler = new DownloadHandlerBuffer();
        http.SetRequestHeader("Content-Type", "application/json");

        yield return http.SendWebRequest();

        if (http.result != UnityWebRequest.Result.Success)
        {
            agentReplyText.text = agentConfig.DefaultErrorAnswer;
            Debug.LogWarning($"LLM error: {http.error}\n{http.downloadHandler?.text}");
            history.RemoveAt(history.Count - 1);
            yield break;
        }

        var resp = JsonUtility.FromJson<ChatToolResponse>(http.downloadHandler.text);
        if (resp?.message == null)
        {
            agentReplyText.text = agentConfig.DefaultErrorAnswer;
            yield break;
        }

        string spoken = string.IsNullOrWhiteSpace(resp.message.content) ? "..." : resp.message.content;
        agentReplyText.text = spoken;
        history.Add(new ChatMessage { role = "assistant", content = spoken });
        TrimHistory();

        if (resp.message.tool_calls == null) yield break;

        foreach (var call in resp.message.tool_calls)
        {
            string toolName = call?.function?.name?.Trim();
            if (string.IsNullOrEmpty(toolName)) continue;

            if (validateAgainstDeclaredTools && !IsDeclared(toolName))
            {
                Debug.LogWarning($"Tool rejeitada (não declarada): '{toolName}'");
                continue;
            }

            Debug.Log($"Tool call: {toolName}");
            gameObject.SendMessage(toolName, SendMessageOptions.DontRequireReceiver);
        }
    }

    private string BuildSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine(agentConfig.ContextPrompt);
        sb.AppendLine();
        sb.AppendLine(agentConfig.AnswerGuideline);
        return sb.ToString();
        // NB: já não descrevemos as acções aqui — vão via tools[] na API.
        // O LLM lê as descrições directamente do schema declarado.
    }

    private List<Tool> BuildTools()
    {
        var list = new List<Tool>();
        foreach (var t in agentConfig.Tools)
        {
            list.Add(new Tool
            {
                type = "function",
                function = new ToolFunction
                {
                    name = t.name,
                    description = t.description,
                    parameters = new ToolFunctionSchema(),
                },
            });
        }
        return list;
    }

    private bool IsDeclared(string toolName)
    {
        foreach (var t in agentConfig.Tools)
            if (t.name == toolName) return true;
        return false;
    }

    private void TrimHistory()
    {
        while (history.Count > windowSize)
            history.RemoveAt(0);
    }
}
