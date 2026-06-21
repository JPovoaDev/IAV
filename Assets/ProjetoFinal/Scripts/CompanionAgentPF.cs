using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

// Este script vai no MESMO GameObject onde tens o Behavior Parameters
// com o teu modelo .onnx já treinado (o mesmo prefab que usaste no treino).
public class CompanionAgentPF : Agent {
    [Header("Referências")]
    [HideInInspector] public Transform player;
    public string targetTag = "Target"; // tag que as obsidianas têm

    [Header("Movimento")]
    public float moveSpeed = 5f;
    public float catchDistance = 1f;    // a partir desta distância considera que chegou à arena

    [Header("Seguir (voa fixo perto de ti)")]
    public Vector3 followOffset = new Vector3(1.5f, 2f, -1f); // offset no espaço do mundo, não relativo à rotação do jogador
    public float arriveThreshold = 0.1f; // evita tremer quando já está no sítio certo
    public float rotationSpeed = 8f;   // quão rápido roda para a direção certa (só quando se move)

    [Header("Controlo")]
    public KeyCode seekKey = KeyCode.E; // tecla para mandar ir atrás da arena mais perto
    public float seekTimeout = 8f;      // tempo máximo (segundos) à procura antes de desistir e voltar para nós

    [Header("Debug (só para veres no Inspector durante o Play)")]
    [SerializeField] private bool isSeeking = false;
    [SerializeField] private Transform currentTarget;
    [SerializeField] private float seekTimer = 0f;

    private DecisionRequester decisionRequester;

    public override void Initialize() {
        // o DecisionRequester é quem pede decisões ao modelo treinado ao longo do tempo,
        // começa desligado para que o companion apenas siga o jogador por defeito e só
        // ative o "cérebro" do ML-Agents quando está a perseguir um target
        decisionRequester = GetComponent<DecisionRequester>();
        if (decisionRequester != null) decisionRequester.enabled = false;
    }

    public override void OnEpisodeBegin() {
        // propositadamente vazio: no treino isto reposicionava o agente de forma aleatória,
        // mas no jogo não queremos isso, o agente começa sempre ao pé do jogador
    }

    private void Update() {
        // ao pressionar E procura a obsidiana (arena) mais próxima e ativa o ML para ir
        // até lá, se não existir nenhum target com a tag correta não faz nada
        if (Input.GetKeyDown(seekKey) && !isSeeking) {
            currentTarget = FindNearestTarget();
            if (currentTarget != null) {
                isSeeking = true;
                seekTimer = 0f;
                if (decisionRequester != null) decisionRequester.enabled = true; // liga o "cérebro"
            }
        }

        if (!isSeeking) {
            FollowPlayer();
        } else {
            seekTimer += Time.deltaTime;

            bool reachedTarget = currentTarget != null &&
                Vector3.Distance(transform.position, currentTarget.position) <= catchDistance;
            bool gaveUp = seekTimer >= seekTimeout;

            // se o target desapareceu (foi destruído), já chegou perto o suficiente ou
            // esgotou o timeout, volta ao modo de seguir o jogador e desliga o ML
            if (currentTarget == null || reachedTarget || gaveUp) {
                StopSeeking();
            }
        }
    }

    private void StopSeeking() {
        isSeeking = false;
        currentTarget = null;
        // desliga o DecisionRequester para que o modelo pare de receber pedidos de decisão,
        // o movimento volta a ser controlado pelo FollowPlayer até ao próximo E
        if (decisionRequester != null) decisionRequester.enabled = false;
    }

    private Transform FindNearestTarget() {
        // percorre todos os GameObjects com a tag "Target" e devolve o mais próximo,
        // se não existir nenhum devolve null e o seek não chega a ser ativado
        GameObject[] targets = GameObject.FindGameObjectsWithTag(targetTag);
        Transform nearest = null;
        float minDist = Mathf.Infinity;

        foreach (var t in targets) {
            float d = Vector3.Distance(transform.position, t.transform.position);
            if (d < minDist) {
                minDist = d;
                nearest = t.transform;
            }
        }
        return nearest;
    }

    private void FollowPlayer() {
        // usa um offset fixo no espaço do mundo para que o companion "voe" sempre à
        // mesma distância e altura sem tentar acompanhar a rotação do jogador
        Vector3 desiredPos = player.position + followOffset;

        float dist = Vector3.Distance(transform.position, desiredPos);
        if (dist > arriveThreshold) {
            // MoveTowards em vez de lerp para ter velocidade constante, mais fácil de
            // ajustar com o moveSpeed no Inspector e sem overshoot
            Vector3 moveDir = (desiredPos - transform.position).normalized;
            transform.position = Vector3.MoveTowards(transform.position, desiredPos, moveSpeed * Time.deltaTime);
            RotateTowards(moveDir);
        }
        // dentro do arriveThreshold não mexe nem posição nem rotação para não tremer
    }

    private void RotateTowards(Vector3 direction) {
        // ignora a componente y para o companion não inclinar para cima ou para baixo
        // ao subir/descer na direção do jogador
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
    }

    // daqui para baixo é basicamente igual ao treino

    public override void CollectObservations(VectorSensor sensor) {
        // observações mínimas: só direção e distância ao target, o modelo não precisa
        // de saber mais porque a única tarefa é chegar a um ponto no espaço
        if (currentTarget == null) {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0f);
            return;
        }

        Vector3 direction = (currentTarget.position - transform.position).normalized;
        sensor.AddObservation(direction);

        float distance = Vector3.Distance(transform.position, currentTarget.position);
        sensor.AddObservation(distance / 10f); // normalizado para ajudar a convergência durante o treino
    }

    public override void OnActionReceived(ActionBuffers actions) {
        // sai imediatamente se não estiver em seek: quando está a seguir o jogador o
        // FollowPlayer já trata do movimento e não queremos que o ML interfira
        if (!isSeeking) return;

        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        Vector3 moveVec = new Vector3(moveX, 0, moveZ);

        transform.position += moveVec * Time.deltaTime * moveSpeed;
        RotateTowards(moveVec);
    }

    public override void Heuristic(in ActionBuffers actionsOut) {
        // não é usado no jogo, serve apenas para testar o comportamento manualmente
        // no editor sem precisar do modelo treinado
        var ca = actionsOut.ContinuousActions;
        ca[0] = 0f;
        ca[1] = 0f;
    }
}