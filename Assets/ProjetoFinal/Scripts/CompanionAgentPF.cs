using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

// Este script vai no MESMO GameObject onde tens o Behavior Parameters
// com o teu modelo .onnx já treinado (o mesmo prefab que usaste no treino).
public class CompanionAgentPF : Agent
{
    [Header("Referências")]
    public Transform player;         
    public string targetTag = "Target"; // tag que as obseidianas têm

    [Header("Movimento")]
    public float moveSpeed = 5f;
    public float catchDistance = 1f;    // a partir desta distância considera que cehgou a arena

    [Header("Seguir (voa fixo perto de ti)")]
    public Vector3 followOffset = new Vector3(1.5f, 2f, -1f); // offset fixo, distancia fixa que o agente fica de nos 
    public float arriveThreshold = 0.1f; // evita tremer quando já está no sítio certo
    public float rotationSpeed = 8f;   // quão rápido roda para a direção certa (só quando se move)

    [Header("Controlo")]
    public KeyCode seekKey = KeyCode.E; // tecla para mandar ir atrás da arena mais perto
    public float seekTimeout = 8f;      // tempo máximo (segundos) à procura antes de desistir e voltar para nos

    [Header("Debug (só para veres no Inspector durante o Play)")]
    [SerializeField] private bool isSeeking = false;
    [SerializeField] private Transform currentTarget;
    [SerializeField] private float seekTimer = 0f;

    private DecisionRequester decisionRequester;

    public override void Initialize()
    {
        // O DecisionRequester é quem pede decisões ao modelo treinado.
        // Começamos desligados agente não se mexe sozinho, ele so nos segue.
        decisionRequester = GetComponent<DecisionRequester>();
        if (decisionRequester != null) decisionRequester.enabled = false;
    }

    public override void OnEpisodeBegin()
    {
        // Propositadamente vazio.
        // No treino isto reposicionava o agente random; no jogo NÃO queremos isso.
    }

    private void Update()
    {
        // Disparar a procura da arena mais próximo
        if (Input.GetKeyDown(seekKey) && !isSeeking)
        {
            currentTarget = FindNearestTarget();
            if (currentTarget != null)
            {
                isSeeking = true;
                seekTimer = 0f;
                if (decisionRequester != null) decisionRequester.enabled = true; // liga o "cérebro"
            }
        }

        if (!isSeeking)
        {
            FollowPlayer();
        }
        else
        {
            seekTimer += Time.deltaTime;

            bool reachedTarget = currentTarget != null &&
                Vector3.Distance(transform.position, currentTarget.position) <= catchDistance;
            bool gaveUp = seekTimer >= seekTimeout;

            // se o target desapareceu, já chegámos perto, ou demorámos demasiado, volta a seguir
            if (currentTarget == null || reachedTarget || gaveUp)
            {
                StopSeeking();
            }
        }
    }

    private void StopSeeking()
    {
        isSeeking = false;
        currentTarget = null;
        if (decisionRequester != null) decisionRequester.enabled = false; // desliga o "cérebro"
    }

    private Transform FindNearestTarget()
    {
        GameObject[] targets = GameObject.FindGameObjectsWithTag(targetTag);
        Transform nearest = null;
        float minDist = Mathf.Infinity;

        foreach (var t in targets)
        {
            float d = Vector3.Distance(transform.position, t.transform.position);
            if (d < minDist)
            {
                minDist = d;
                nearest = t.transform;
            }
        }
        return nearest;
    }

    private void FollowPlayer()
    {
        if (player == null) return;

        // ponto alvo: offset FIXO no mundo a partir da nossa posição -> ele "voa" sempre à mesma distância
        Vector3 desiredPos = player.position + followOffset;

        float dist = Vector3.Distance(transform.position, desiredPos);
        if (dist > arriveThreshold)
        {
            Vector3 moveDir = (desiredPos - transform.position).normalized;
            transform.position = Vector3.MoveTowards(transform.position, desiredPos, moveSpeed * Time.deltaTime);
            RotateTowards(moveDir);
        }
        // se já chegou, fica mesmo parado (nem posição nem rotação mudam)
    }

    private void RotateTowards(Vector3 direction)
    {
        direction.y = 0f; // não queremos inclinar para cima/baixo
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
    }

    // ---- Daqui para baixo é basicamente igual ao treino ----

    public override void CollectObservations(VectorSensor sensor)
    {
        if (currentTarget == null)
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0f);
            return;
        }

        Vector3 direction = (currentTarget.position - transform.position).normalized;
        sensor.AddObservation(direction);

        float distance = Vector3.Distance(transform.position, currentTarget.position);
        sensor.AddObservation(distance / 10f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!isSeeking) return; // só se mexe via ML quando está a perseguir um target

        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        Vector3 moveVec = new Vector3(moveX, 0, moveZ);

        transform.position += moveVec * Time.deltaTime * moveSpeed;
        RotateTowards(moveVec);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // não é usado no jogo (só serve para testar manualmente no editor)
        var ca = actionsOut.ContinuousActions;
        ca[0] = 0f;
        ca[1] = 0f;
    }
}