using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

// Versão V2 do GuardActions: regista handlers no LLMAgentWithParameterizedActions
// no Start, em vez de depender de SendMessage por reflexão.
//
// Cada handler recebe os arguments da tool call como JObject e extrai as
// chaves que conhece. Aqui é o sítio para validar (enum fora de gama,
// strings vazias) e fazer fallback gracioso.

[RequireComponent(typeof(LLMAgentWithParameterizedActions))]
public class GuardActionsV2 : MonoBehaviour
{
    [Header("Referências cruzadas")]
    [SerializeField] private RLPatrolAgent patroller;
    [SerializeField] private WaypointRegistry waypoints;
    [SerializeField] private Transform door;

    [Header("Porta")]
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 2f;

    private LLMAgentWithParameterizedActions agent;
    private TMP_Text agentReplyText;
    private bool doorOpen;

    private void Awake()
    {
        agent = GetComponent<LLMAgentWithParameterizedActions>();
        var go = GameObject.FindWithTag("AgentAnswer");
        if (go != null) agentReplyText = go.GetComponent<TMP_Text>();
    }

    private void Start()
    {
        agent.RegisterTool("GoTo",     HandleGoTo);
        agent.RegisterTool("OpenDoor", HandleOpenDoor);
        agent.RegisterTool("Shoot",    HandleShoot);

        if (patroller != null)
            patroller.OnGoalReached += HandleGoalReached;
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private void HandleGoTo(JObject args)
    {
        if (patroller == null || waypoints == null)
        {
            Debug.LogWarning("GoTo: patroller ou waypoints não atribuídos.");
            return;
        }

        string target = args["target"]?.ToString();
        if (string.IsNullOrWhiteSpace(target))
        {
            Debug.LogWarning("GoTo: argumento 'target' ausente.");
            return;
        }

        if (!waypoints.TryResolve(target, out var t))
        {
            Debug.LogWarning($"GoTo: waypoint desconhecido '{target}'.");
            // Falha graciosa — não rebenta a conversa.
            return;
        }

        patroller.SetGoal(t);
    }

    private void HandleOpenDoor(JObject args)
    {
        if (door == null) { Debug.Log("OpenDoor: porta não atribuída"); return; }
        doorOpen = !doorOpen;
        StopAllCoroutines();
        StartCoroutine(RotateDoor(doorOpen ? openAngle : 0f));
    }

    private void HandleShoot(JObject args)
    {
        string dir = args["direction"]?.ToString() ?? "frente";
        Debug.Log($"Shoot — bang! ({dir})");
        // Aqui podes instanciar uma flecha consoante a direcção,
        // tocar som diferente, animar, etc.
    }

    // ── Feedback do corpo → cérebro (T3) ─────────────────────────────────────

    private void HandleGoalReached(Transform reached)
    {
        // O NPC anuncia que chegou. Em alternativa, podíamos voltar a chamar
        // o LLM com uma mensagem de "system event: arrived at X" para
        // continuar a conversa, mas isso é território da aula 12+.
        if (agentReplyText != null)
            agentReplyText.text = $"(o guarda chegou a {reached.name})";
    }

    // ── Animação da porta (igual à aula 10) ─────────────────────────────────

    private System.Collections.IEnumerator RotateDoor(float targetY)
    {
        Quaternion start = door.localRotation;
        Quaternion end = Quaternion.Euler(0, targetY, 0);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * openSpeed;
            door.localRotation = Quaternion.Slerp(start, end, t);
            yield return null;
        }
        door.localRotation = end;
    }
}
