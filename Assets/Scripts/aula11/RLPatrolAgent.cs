using System;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

// Agente de inferência que reutiliza a política treinada na aula 08
// (StairsAgent.onnx). Diferenças cruciais para o agente de treino:
//
//   • Não há OnEpisodeBegin a construir escadas nem a reposicionar o agente.
//     Em inferência, queremos o agente vivo permanentemente.
//   • OnTriggerEnter NÃO chama EndEpisode. Em vez disso, dispara o evento
//     OnGoalReached que o NPC ouve para falar feedback (T3).
//   • O alvo (targetTransform) é definido **dinamicamente** via SetGoal,
//     em vez de gerado na construção da escada.
//
// IMPORTANTE: as observações têm de bater EXACTAMENTE com as do treino. Se
// mudares CollectObservations sem retreinar o .onnx, a política devolve
// acções absurdas. O formato (5 floats + Ray Perception Sensor) está clonado
// do StairsAgent da aula 08.

[RequireComponent(typeof(CharacterController))]
public class RLPatrolAgent : Agent
{
    [Header("Parâmetros de movimento (iguais ao treino)")]
    public float moveSpeed = 4f;
    public float gravity = 20f;

    [Header("Estado")]
    [Tooltip("Alvo actual. Atribuído por SetGoal().")]
    public Transform targetTransform;

    [Tooltip("Se true, ignora pedidos de acção do BehaviorParameters (agente parado).")]
    public bool idleWhenNoGoal = true;

    // Espessura do estado interno
    public bool IsExecuting { get; private set; }
    public event Action<Transform> OnGoalReached;

    private CharacterController controller;
    private Vector3 velocity;

    // ── API exposta ao LLM (via GuardActions) ────────────────────────────────

    public void SetGoal(Transform goal)
    {
        if (goal == null) { Debug.LogWarning("SetGoal: goal nulo"); return; }
        targetTransform = goal;
        IsExecuting = true;
    }

    public void CancelGoal()
    {
        targetTransform = null;
        IsExecuting = false;
    }

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    public override void Initialize()
    {
        controller = GetComponent<CharacterController>();
        controller.stepOffset = 1.0f;   // permite subir degraus pequenos sem RL
    }

    // OnEpisodeBegin não faz nada — em inferência não há episódios "frescos".
    public override void OnEpisodeBegin() { }

    // ── Observações (têm de bater com o treino) ──────────────────────────────

    public override void CollectObservations(VectorSensor sensor)
    {
        if (targetTransform == null)
        {
            // Sem alvo: dar zeros estáveis. A política recebe "estou no alvo".
            sensor.AddObservation(Vector3.zero);   // 3
            sensor.AddObservation(0f);             // 1
            sensor.AddObservation(controller != null && controller.isGrounded ? 1f : 0f); // 1
            return;
        }

        Vector3 toTarget = targetTransform.localPosition - transform.localPosition;
        sensor.AddObservation(toTarget.normalized);              // 3
        sensor.AddObservation(toTarget.magnitude / 10f);         // 1
        sensor.AddObservation(controller.isGrounded ? 1f : 0f);  // 1

        // Ray Perception Sensor 3D é adicionado como componente separado no
        // GameObject — o ML-Agents trata-o automaticamente. Confirmar no
        // Inspector: 9 raios, tags { Target, Wall, Platform }.
    }

    // ── Acções ───────────────────────────────────────────────────────────────

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (idleWhenNoGoal && !IsExecuting) return;

        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveZ = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        Vector3 horizontal = new Vector3(moveX, 0f, moveZ) * moveSpeed;

        if (controller.isGrounded && velocity.y < 0f) velocity.y = -2f;
        velocity.y -= gravity * Time.deltaTime;

        Vector3 total = horizontal + Vector3.up * velocity.y;
        controller.Move(total * Time.deltaTime);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions;
        ca[0] = Input.GetAxis("Horizontal");
        ca[1] = Input.GetAxis("Vertical");
    }

    // ── Colisões — substitui o EndEpisode do treino ──────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Target") && other.transform == targetTransform)
        {
            var reached = targetTransform;
            CancelGoal();
            OnGoalReached?.Invoke(reached);
        }
        // Paredes — NÃO terminamos episódio. Em inferência, o agente fica preso
        // e o utilizador percebe que o corpo falhou (slide 25 — "fora da
        // distribuição de treino"). Honesto pedagogicamente.
    }
}
