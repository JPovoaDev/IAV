using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Networking;

public class ARIAInvestigator : MonoBehaviour {

    [SerializeField] private PatrolRobot patrolRobot;
    [SerializeField] private ARIAEmotionalState emotionalState;
    [SerializeField] private TMP_Text ariaReplyText;

    [SerializeField] private Transform wp5Arquivo;
    [SerializeField] private Transform wp6Laboratorio;
    [SerializeField] private Transform wp7Corredor;
    [SerializeField] private Transform wp8Base; // ponto de regresso depois de cada investigação, separado dos waypoints de patrulha

    [SerializeField] private string apiUrl = "http://localhost:11434/api/chat";
    [SerializeField] private string modelName = "qwen2.5:3b";

    // guarda se o robot já começou a mexer-se para o destino
    // o NavMeshAgent precisa de pelo menos um frame para calcular o caminho
    // se não esperarmos por isto o remainingDistance é 0 logo no início e o script acha que já chegou
    private bool robotMoving = false;

    // a lore de cada zona está aqui porque está colado à construção das prompts
    // se estivesse num ScriptableObject era mais difícil ajustar o texto e o prompt ao mesmo tempo
    private readonly Dictionary<string, string> zoneLore = new Dictionary<string, string>() {
        {
            "arquivo",
            "Sala de servidores. Terminais desligados com força, não foi shutdown normal. " +
            "Pó acumulado: inactividade estimada em 6 anos. " +
            "Um painel lateral está arrombado pelo interior. " +
            "Logs de acesso apagados manualmente na última sessão activa."
        },
        {
            "laboratorio",
            "Equipamento de contenção partido. Marcas de combustão nas paredes norte e este. " +
            "Vestígios de saída apressada: cadeiras tombadas, documentos espalhados no chão. " +
            "Leitura de radiação residual: 0.8 mSv/h, muito acima do normal. " +
            "Detectado movimento de ar anómalo na conduta de ventilação sul."
        },
        {
            "corredor",
            "Corredor principal vazio. Iluminação de emergência activa a 40%. " +
            "Câmeras de segurança offline mas estrutura intacta. " +
            "Porta de acesso restrito no fundo: selada e sem sinais de tentativa de abertura. " +
            "Sem movimento detetado. Temperatura e pressão dentro dos parâmetros normais."
        }
    };

    private Dictionary<string, Transform> zoneTargets;
    private string[] zoneNames;

    private enum InvestigationPhase {
        Idle,
        GoingToZone,
        ReturningToBase
    }

    private InvestigationPhase phase = InvestigationPhase.Idle;
    private string currentZone;

    private void Awake() {
        zoneNames = new string[] { "arquivo", "laboratorio", "corredor" };

        zoneTargets = new Dictionary<string, Transform>() {
            { zoneNames[0], wp5Arquivo     },
            { zoneNames[1], wp6Laboratorio },
            { zoneNames[2], wp7Corredor    }
        };
    }

    private void Update() {
        if (phase == InvestigationPhase.Idle)
            return;

        NavMeshAgent nav = patrolRobot.GetComponent<NavMeshAgent>();

        // esperamos até o robot ter um caminho calculado e já estar em movimento
        // sem esta verificação a transição de fase disparava no mesmo frame que o GoTo foi chamado
        if (!robotMoving) {
            if (nav.pathPending || nav.remainingDistance < 0.1f)
                return;

            robotMoving = true;
            return;
        }

        // quando a fase muda, resetamos robotMoving para false
        // assim o bloco acima também protege a segunda viagem (de regresso à base)
        if (phase == InvestigationPhase.GoingToZone) {
            Debug.Log($"[ARIAInvestigator] Chegou a '{currentZone}'. A regressar à base...");
            phase = InvestigationPhase.ReturningToBase;
            robotMoving = false;
            patrolRobot.GoTo(wp8Base.position);
            ariaReplyText.text = "ARIA: Robot a regressar. A processar dados...";

        } else if (phase == InvestigationPhase.ReturningToBase) {
            Debug.Log($"[ARIAInvestigator] Robot na base. A gerar relatório de '{currentZone}'...");
            phase = InvestigationPhase.Idle;
            StartCoroutine(GenerateReport(currentZone));
        }
    }

    public void SendRobotTo(string zoneName) {
        if (string.IsNullOrWhiteSpace(zoneName)) {
            zoneName = zoneNames[Random.Range(0, zoneNames.Length)];
            Debug.Log($"[ARIAInvestigator] Zona não especificada, escolhida aleatoriamente: {zoneName}");
        }

        if (!zoneTargets.ContainsKey(zoneName) || zoneTargets[zoneName] == null) {
            Debug.LogWarning($"[ARIAInvestigator] Zona desconhecida: {zoneName}");
            ariaReplyText.text = "ARIA: Zona não encontrada no mapa de instalações.";
            return;
        }

        currentZone = zoneName;
        phase = InvestigationPhase.GoingToZone;
        robotMoving = false;
        patrolRobot.StopPatrol(); // passa o controlo do robot para este script, o ResumePatrol é chamado no fim
        patrolRobot.GoTo(zoneTargets[zoneName].position);

        ariaReplyText.text = $"ARIA: Robot enviado para [{zoneName}]. A monitorizar...";
        Debug.Log($"[ARIAInvestigator] Robot a caminho de: {zoneName}");
    }

    private IEnumerator GenerateReport(string zoneName) {

        // descobrimos que pedir tudo de uma vez num único prompt dava resultados muito maus com o modelo pequeno
        // por isso dividimos em três chamadas separadas: narrar, classificar e reagir
        // cada chamada usa o resultado da anterior como contexto

        string context = "Zona sem dados registados.";
        if (zoneLore.ContainsKey(zoneName)) {
            context = zoneLore[zoneName];
        }

        string reportPrompt =
            $"És ARIA, uma IA de segurança isolada numa instalação abandonada chamada Projecto AXIOM. " +
            $"O teu robot de patrulha regressou da zona '{zoneName}' com os seguintes dados: {context}. " +
            $"Gera um relatório breve (2-3 frases) na primeira pessoa, em português. " +
            $"Tom frio e técnico. Sem markdown. Sem listas.";

        string report = null;
        yield return StartCoroutine(CallLLM(reportPrompt, r => report = r));

        if (report == null) {
            ariaReplyText.text = "ARIA: Falha na comunicação com o robot.";
            patrolRobot.ResumePatrol();
            yield break;
        }

        // classificamos o texto que o modelo gerou e não os dados brutos da zona
        // dá resultados mais consistentes porque o modelo está a classificar linguagem que ele próprio escreveu
        string classifyPrompt =
            $"Relatório de segurança: \"{report}\". " +
            $"Classifica a situação numa só palavra: CALM, WARNING ou ALERT. " +
            $"CALM = tudo normal. WARNING = suspeito mas não urgente. ALERT = perigo imediato. " +
            $"Responde APENAS com uma dessas três palavras, nada mais.";

        string classification = null;
        yield return StartCoroutine(CallLLM(classifyPrompt, r => classification = r));

        // se a classificação falhar usamos WARNING como fallback em vez de CALM
        // prefirimos que o jogo fique mais tenso do que fingir que está tudo bem quando não sabemos
        ARIAMood mood = ARIAMood.WARNING;
        if (classification != null) {
            mood = ARIAEmotionalState.ParseFromLLM(classification);
        }

        // atualizar o mood aqui já muda o multiplicador de trust no ARIAActions
        // se for ALERT o trust congela a partir deste momento
        emotionalState.SetMood(mood);
        Debug.Log($"[ARIAInvestigator] Classificacao: '{classification}' -> {mood}");

        // o Ollama local não suporta system prompt da mesma forma que a API cloud
        // por isso passamos o tom que quero dentro do próprio prompt como instrução
        string tonInstructions = "";
        if (mood == ARIAMood.CALM) {
            tonInstructions = "Tom neutro e seco. Algo como 'Zona verificada. Sem anomalias.'.";
        } else if (mood == ARIAMood.WARNING) {
            tonInstructions = "Tom ligeiramente tenso. Começa com algo como 'Interessante...' ou 'Isto requer atenção.'.";
        } else {
            tonInstructions = "Tom urgente e preocupado. Começa com 'Anomalia confirmada.' ou 'Isto é um problema.'.";
        }

        string reactionPrompt =
            $"És ARIA, uma IA de segurança fria e técnica num bunker abandonado. " +
            $"O teu robot regressou da zona '{zoneName}' e o relatório é: \"{report}\". " +
            $"A tua avaliação interna é: {mood}. " +
            $"Reage em 1-2 frases em português. " +
            tonInstructions;

        string reaction = null;
        yield return StartCoroutine(CallLLM(reactionPrompt, r => reaction = r));

        string moodTag = "";
        if (mood == ARIAMood.CALM) 
            moodTag = "[CALM]";
        else if (mood == ARIAMood.WARNING) 
            moodTag = "[WARNING]";
        else 
            moodTag = "[ALERT]";

        string reactionText = "";
        if (reaction != null)
            reactionText = reaction;

        ariaReplyText.text = $"ARIA {moodTag}: {reactionText}\n\nRelatório [{zoneName}]: {report}";

        // em ALERT reinvestiga automaticamente para criar um loop de escalada
        // o trust já parou por causa do multiplicador 0, isto mantém o robot ocupado
        // e faz o jogador perceber que há um problema real para resolver
        if (mood == ARIAMood.ALERT) {
            yield return new WaitForSeconds(5f);
            SendRobotTo(currentZone);
            yield break;
        }

        patrolRobot.ResumePatrol();
    }

    private IEnumerator CallLLM(string prompt, System.Action<string> callback) {
        JObject body = new JObject();
        body["model"] = modelName;
        body["stream"] = false; // streaming desativado para simplificar o parsing da resposta
        body["messages"] = new JArray {
            new JObject { ["role"] = "user", ["content"] = prompt }
        };

        var http = new UnityWebRequest(apiUrl, "POST");
        byte[] bytes = Encoding.UTF8.GetBytes(body.ToString());
        http.uploadHandler = new UploadHandlerRaw(bytes);
        http.downloadHandler = new DownloadHandlerBuffer();
        http.SetRequestHeader("Content-Type", "application/json");

        yield return http.SendWebRequest();

        if (http.result == UnityWebRequest.Result.Success) {
            try {
                JObject parsed = JObject.Parse(http.downloadHandler.text);

                // verificamos cada nível do JSON antes de aceder porque se o modelo devolver uma resposta malformada o script não devia crashar
                if (parsed["message"] != null && parsed["message"]["content"] != null)
                    callback(parsed["message"]["content"].ToString());
                else
                    callback(null);

            } catch (System.Exception e) {
                Debug.LogWarning($"[ARIAInvestigator] Erro ao fazer parse: {e.Message}");
                callback(null);
            }
        } else {
            Debug.LogWarning($"[ARIAInvestigator] Erro HTTP: {http.error}");
            callback(null); // null para o callback para sinalizar falha, cada etapa do pipeline decide o que fazer com isso
        }

        http.Dispose();
    }
}