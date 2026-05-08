using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

[RequireComponent(typeof(CharacterController))]
public class PredatorAgent : Agent {
    [Header("Coordenação")]
    /*public PreyPredatorArena arena;*/
    public PreyAgent prey;

    [Header("Movimento")]
    public float moveSpeed = 4f;
    public float gravity = 20f;

    private CharacterController controller;
    private Vector3 velocity;

    public override void Initialize() {
        controller = GetComponent<CharacterController>();
        controller.stepOffset = 0.3f;
        prey = transform.parent.Find("Prey").GetComponent<PreyAgent>();
    }

    public override void OnEpisodeBegin() {
        // NÃO chama arena.StartEpisode — a presa trata disso
        velocity = Vector3.zero;
    }

    public void Place(Vector3 localPosition) {
        controller.enabled = false;
        transform.localPosition = localPosition;
        transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        controller.enabled = true;
        velocity = Vector3.zero;
    }

    public override void CollectObservations(VectorSensor sensor) {
        sensor.AddObservation(velocity.x / moveSpeed); // 1
        sensor.AddObservation(velocity.z / moveSpeed); // 1
        // Total: 2 floats. Jogo simétrico à presa.

        Vector3 toPrey = prey.transform.localPosition - transform.localPosition;
        sensor.AddObservation(toPrey.normalized); // +3 floats
        sensor.AddObservation(toPrey.magnitude / 10f); // +1 float
    }

    public override void OnActionReceived(ActionBuffers actions) {
        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveZ = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        Vector3 horizontal = new Vector3(moveX, 0f, moveZ) * moveSpeed;

        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        velocity.y -= gravity * Time.deltaTime;
        velocity.x = horizontal.x;
        velocity.z = horizontal.z;

        float dist = Vector3.Distance(transform.localPosition, prey.transform.localPosition);
        AddReward(0.001f * (1f - dist / 10f));

        controller.Move(velocity * Time.deltaTime);
    }

    public override void Heuristic(in ActionBuffers actionsOut) {
        var ca = actionsOut.ContinuousActions;
        ca[0] = Input.GetAxis("Horizontal");
        ca[1] = Input.GetAxis("Vertical");
    }

    /*private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Prey")) {
            arena.OnPreyCaptured();
        }
    }*/
}