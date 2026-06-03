using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

// Fluxo completo de investigação:
//   1. StopPatrol()
//   2. Robot vai à zona (WP5/6/7) — random se não especificada, exacta se especificada
//   3. Robot chega → vai ao WP8 (base da ARIA)
//   4. Robot chega à base → LLM gera relatório da zona
//   5. Segunda chamada LLM classifica: CALM / WARNING / ALERT
//   6. ARIA reage com personalidade consoante o mood
//   7. ResumePatrol() — robot volta ao wander WP1-WP4

public class ARIAInvestigator : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private PatrolRobot patrolRobot;
    [SerializeField] private ARIAEmotionalState emotionalState;
    [SerializeField] private TMP_Text ariaReplyText;

    [Header("Waypoints de investigação")]
    [SerializeField] private Transform wp5Arquivo;      // WP5 → arquivo     (→ WARNING)
    [SerializeField] private Transform wp6Laboratorio;  // WP6 → laboratório (→ ALERT)
    [SerializeField] private Transform wp7Corredor;     // WP7 → corredor    (→ CALM)
    [SerializeField] private Transform wp8Base;         // WP8 → base da ARIA (regresso)

    [Header("LLM")]
    [SerializeField] private string apiUrl = "http://localhost:11434/api/chat";
    [SerializeField] private string modelName = "qwen2.5:3b";

    [Header("Navegação")]
    [SerializeField] private float arrivalThreshold = 1.5f;
    private bool robotMoving = false;

    // ── Lore das zonas ────────────────────────────────────────────────────────
    // Escrita para empurrar o LLM para a classificação pretendida:
    //   arquivo    → WARNING  (suspeito mas não urgente)
    //   laboratorio → ALERT   (perigo imediato)
    //   corredor   → CALM     (tudo normal)

    private readonly Dictionary<string, string> zoneLore = new() {
        {
            "arquivo",
            "Sala de servidores. Terminais desligados com força — não foi shutdown normal. " +
            "Pó acumulado: inactividade estimada em 6 anos. " +
            "Um painel lateral está arrombado pelo interior. " +
            "Logs de acesso apagados manualmente na última sessão activa."
        },
        {
            "laboratorio",
            "Equipamento de contenção partido. Marcas de combustão nas paredes norte e este. " +
            "Vestígios de saída apressada: cadeiras tombadas, documentos espalhados no chão. " +
            "Leitura de radiação residual: 0.8 mSv/h — muito acima do normal. " +
            "Detectado movimento de ar anómalo na conduta de ventilação sul."
        },
        {
            "corredor",
            "Corredor principal vazio. Iluminação de emergência activa a 40%. " +
            "Câmeras de segurança offline mas estrutura intacta. " +
            "Porta de acesso restrito no fundo: selada e sem sinais de tentativa de abertura. " +
            "Sem movimento detectado. Temperatura e pressão dentro dos parâmetros normais."
        }
    };

    // Mapa nome → Transform
    private Dictionary<string, Transform> zoneTargets;
    private string[] zoneNames; // para random

    // Estado interno da investigação
    private enum InvestigationPhase { Idle, GoingToZone, ReturningToBase }
    private InvestigationPhase phase = InvestigationPhase.Idle;
    private string currentZone;

    private void Awake()
    {
        zoneTargets = new Dictionary<string, Transform> {
            { "arquivo",      wp5Arquivo     },
            { "laboratorio",  wp6Laboratorio },
            { "corredor",     wp7Corredor    }
        };
        zoneNames = new string[] { "arquivo", "laboratorio", "corredor" };
    }

    private void Update()
    {
        if (phase == InvestigationPhase.Idle) return;

        var nav = patrolRobot.Agent;

        // Espera que o robot comece a mover de facto
        if (!robotMoving)
        {
            if (nav.pathPending || nav.remainingDistance < 0.1f) return;
            robotMoving = true;
            return;
        }

        if (nav.pathPending) return;
        if (nav.remainingDistance > arrivalThreshold) return;

        // Chegou ao destino actual
        if (phase == InvestigationPhase.GoingToZone)
        {
            // Chegou à zona — agora volta à base
            Debug.Log($"[ARIAInvestigator] Chegou à zona '{currentZone}'. A regressar à base...");
            phase = InvestigationPhase.ReturningToBase;
            robotMoving = false;
            patrolRobot.GoTo(wp8Base.position);

            if (ariaReplyText != null)
                ariaReplyText.text = "ARIA: Robot a regressar. A processar dados...";
        }
        else if (phase == InvestigationPhase.ReturningToBase)
        {
            // Chegou à base — gerar relatório
            Debug.Log($"[ARIAInvestigator] Robot na base. A gerar relatório de '{currentZone}'...");
            phase = InvestigationPhase.Idle;
            StartCoroutine(GenerateReport(currentZone));
        }
    }

    // ── API chamada pelo ARIAActions ──────────────────────────────────────────

    // zoneName = null ou vazio → escolhe random
    public void SendRobotTo(string zoneName)
    {
        // Se não especificado, escolhe random
        if (string.IsNullOrWhiteSpace(zoneName))
        {
            zoneName = zoneNames[Random.Range(0, zoneNames.Length)];
            Debug.Log($"[ARIAInvestigator] Zona não especificada — escolhida aleatoriamente: {zoneName}");
        }

        if (!zoneTargets.TryGetValue(zoneName, out Transform target) || target == null)
        {
            Debug.LogWarning($"[ARIAInvestigator] Zona desconhecida: {zoneName}");
            if (ariaReplyText != null)
                ariaReplyText.text = "ARIA: Zona não encontrada no mapa de instalações.";
            return;
        }

        currentZone = zoneName;
        phase = InvestigationPhase.GoingToZone;
        robotMoving = false;    
        patrolRobot.StopPatrol();
        patrolRobot.GoTo(target.position);

        if (ariaReplyText != null)
            ariaReplyText.text = $"ARIA: Robot enviado para [{zoneName}]. A monitorizar...";

        Debug.Log($"[ARIAInvestigator] Robot a caminho de: {zoneName}");
    }

    // ── Pipeline LLM ─────────────────────────────────────────────────────────

    private IEnumerator GenerateReport(string zoneName)
    {
        string context = zoneLore.TryGetValue(zoneName, out string lore)
            ? lore : "Zona sem dados registados.";

        // Chamada 1 — relatório narrativo
        string reportPrompt =
            $"És ARIA, uma IA de segurança isolada numa instalação abandonada chamada Projecto AXIOM. " +
            $"O teu robot de patrulha regressou da zona '{zoneName}' com os seguintes dados: {context}. " +
            $"Gera um relatório breve (2-3 frases) na primeira pessoa, em português. " +
            $"Tom frio e técnico. Sem markdown. Sem listas.";

        string report = null;
        yield return StartCoroutine(CallLLM(reportPrompt, r => report = r));

        if (report == null)
        {
            if (ariaReplyText != null)
                ariaReplyText.text = "ARIA: Falha na comunicação com o robot.";
            patrolRobot.ResumePatrol();
            yield break;
        }

        // Chamada 2 — classificar
        string classifyPrompt =
            $"Relatório de segurança: \"{report}\". " +
            $"Classifica a situação numa só palavra: CALM, WARNING ou ALERT. " +
            $"CALM = tudo normal. WARNING = suspeito mas não urgente. ALERT = perigo imediato. " +
            $"Responde APENAS com uma dessas três palavras, nada mais.";

        string classification = null;
        yield return StartCoroutine(CallLLM(classifyPrompt, r => classification = r));

        ARIAMood mood = classification != null
            ? ARIAEmotionalState.ParseFromLLM(classification)
            : ARIAMood.WARNING;

        emotionalState.SetMood(mood);
        Debug.Log($"[ARIAInvestigator] Classificação: '{classification?.Trim()}' → {mood}");

        // Chamada 3 — ARIA reage com personalidade
        string reactionPrompt =
            $"És ARIA, uma IA de segurança fria e técnica num bunker abandonado. " +
            $"O teu robot regressou da zona '{zoneName}' e o relatório é: \"{report}\". " +
            $"A tua avaliação interna é: {mood}. " +
            $"Reage em 1-2 frases em português. " +
            (mood == ARIAMood.CALM ? "Tom neutro e seco. Algo como 'Zona verificada. Sem anomalias.'." :
             mood == ARIAMood.WARNING ? "Tom ligeiramente tenso. Começa com algo como 'Interessante...' ou 'Isto requer atenção.'." :
                                       "Tom urgente e preocupado. Começa com 'Anomalia confirmada.' ou 'Isto é um problema.'.");

        string reaction = null;
        yield return StartCoroutine(CallLLM(reactionPrompt, r => reaction = r));

        // Mostra reacção + relatório
        if (ariaReplyText != null)
        {
            string moodTag = mood == ARIAMood.CALM ? "[CALM]" :
                             mood == ARIAMood.WARNING ? "[WARNING]" : "[ALERT]";
            ariaReplyText.text = $"ARIA {moodTag}: {reaction ?? ""}\n\nRelatório [{zoneName}]: {report}";
        }

        // Em ALERT, re-investiga automaticamente após delay
        if (mood == ARIAMood.ALERT)
        {
            yield return new WaitForSeconds(5f);
            SendRobotTo(currentZone);
            yield break;
        }

        patrolRobot.ResumePatrol();
    }

    // ── Helper HTTP ───────────────────────────────────────────────────────────

    private IEnumerator CallLLM(string prompt, System.Action<string> callback)
    {
        var body = new JObject
        {
            ["model"] = modelName,
            ["stream"] = false,
            ["messages"] = new JArray {
                new JObject { ["role"] = "user", ["content"] = prompt }
            }
        };

        using var http = new UnityWebRequest(apiUrl, "POST");
        byte[] bytes = Encoding.UTF8.GetBytes(body.ToString());
        http.uploadHandler = new UploadHandlerRaw(bytes);
        http.downloadHandler = new DownloadHandlerBuffer();
        http.SetRequestHeader("Content-Type", "application/json");

        yield return http.SendWebRequest();

        if (http.result == UnityWebRequest.Result.Success)
        {
            try
            {
                var parsed = JObject.Parse(http.downloadHandler.text);
                callback(parsed["message"]?["content"]?.ToString());
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ARIAInvestigator] Parse error: {e.Message}");
                callback(null);
            }
        }
        else
        {
            Debug.LogWarning($"[ARIAInvestigator] HTTP error: {http.error}");
            callback(null);
        }
    }
}