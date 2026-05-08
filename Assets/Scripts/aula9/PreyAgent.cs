using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEditorInternal.VR;

[RequireComponent(typeof(CharacterController))]
public class PreyAgent : Agent {
    [Header("Coordenação")]
    public PreyPredatorArena arena;
    public PredatorAgent predator;

    [Header("Movimento")]
    public float moveSpeed = 4f;
    public float gravity = 20f;

    private CharacterController controller;
    private Vector3 velocity;

    public override void Initialize() {
        controller = GetComponent<CharacterController>();
        controller.stepOffset = 0.3f;
        arena = transform.parent.GetComponent<PreyPredatorArena>();
        predator = transform.parent.Find("Predator").GetComponent<PredatorAgent>();
    }

    public override void OnEpisodeBegin() {
        velocity = Vector3.zero;
        arena.StartEpisode();
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
        // Total: 2 floats. Ray Perception Sensors tratam do resto.

        Vector3 toPredator = predator.transform.localPosition - transform.localPosition;
        sensor.AddObservation(toPredator.normalized); // +3 floats
        sensor.AddObservation(toPredator.magnitude / 10f); // +1 float
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

        controller.Move(velocity * Time.deltaTime);
    }

    public override void Heuristic(in ActionBuffers actionsOut) {
        var ca = actionsOut.ContinuousActions;
        ca[0] = Input.GetAxis("Horizontal");
        ca[1] = Input.GetAxis("Vertical");
    }
}