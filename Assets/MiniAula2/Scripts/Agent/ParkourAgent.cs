using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

[RequireComponent(typeof(Rigidbody))]
public class ParkourAgent : Agent
{
    [Header("Arena")]
    public ParkourArena arena;

    [Header("Spawn")]
    public Transform spawnPoint;

    [Header("Plataformas (para observações)")]
    public Transform nextPlatformTarget;

    [Header("Movimento")]
    public float moveSpeed = 4f;
    public float jumpForce = 5f;
    public float groundCheckDistance = 0.15f;
    public LayerMask groundLayer;

    [Header("Recompensas")]
    public float timePenaltyPerStep = -0.001f;
    public float stillPenaltyThreshold = 0.5f;
    public float stayStillPenalty = -0.002f;
    public float forwardProgressReward = 0.001f;

    private Rigidbody rb;
    private bool isGrounded;
    private bool hasJumped = false;
    private float previousZ;
    private int nextCheckpointIdx = 0;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    public override void OnEpisodeBegin()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        isGrounded = false;
        hasJumped = false;

        Vector3 spawn = spawnPoint != null
            ? spawnPoint.position
            : new Vector3(0, 1f, 0);
        spawn.x += Random.Range(-0.5f, 0.5f);
        transform.position = spawn;
        transform.rotation = Quaternion.identity;

        nextCheckpointIdx = 0;
        previousZ = transform.position.z;

        if (arena == null) { Debug.LogError("ARENA É NULL"); return; }
        arena.ResetEpisode();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (rb == null)
        {
            sensor.AddObservation(new float[10]);
            return;
        }

        Vector3 vel = rb.linearVelocity;
        sensor.AddObservation(vel.x / moveSpeed);
        sensor.AddObservation(vel.y / jumpForce);
        sensor.AddObservation(vel.z / moveSpeed);

        sensor.AddObservation(isGrounded ? 1f : 0f);

        if (nextPlatformTarget != null)
        {
            Vector3 toTarget = nextPlatformTarget.position - transform.position;
            sensor.AddObservation(toTarget.normalized);
            sensor.AddObservation(toTarget.magnitude / 20f);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0f);
        }

        sensor.AddObservation(transform.position.y / 10f);
        sensor.AddObservation(Mathf.Clamp(vel.y / jumpForce, -1f, 1f));
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        CheckGrounded();

        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveZ = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        Vector3 targetVelocity = new Vector3(moveX * moveSpeed, rb.linearVelocity.y, moveZ * moveSpeed);
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, 0.3f);

        int jump = actions.DiscreteActions[0];
        if (jump == 1 && isGrounded && !hasJumped)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
            hasJumped = true;
        }
        if (isGrounded) hasJumped = false;

        AddReward(timePenaltyPerStep);

        float speed2D = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude;
        if (speed2D < stillPenaltyThreshold)
            AddReward(stayStillPenalty);

        float deltaZ = transform.position.z - previousZ;
        if (deltaZ > 0f)
            AddReward(deltaZ * forwardProgressReward);
        previousZ = transform.position.z;

        if (transform.position.y < -3f)
        {
            AddReward(-1f);
            arena.OnAgentDied();
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions;
        var da = actionsOut.DiscreteActions;

        ca[0] = Input.GetAxis("Horizontal");
        ca[1] = Input.GetAxis("Vertical");
        da[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Checkpoint") || other.CompareTag("Goal"))
        {
            var cp = other.GetComponent<CheckpointTrigger>();
            if (cp == null) return;

            float reward = arena.OnCheckpointHit(cp.checkpointIndex, cp.isGoal);
            AddReward(reward);

            if (cp.isGoal)
                EndEpisode();
        }
    }

    private void CheckGrounded()
    {
        isGrounded = Physics.Raycast(
            transform.position,
            Vector3.down,
            groundCheckDistance + 0.5f,
            groundLayer
        );
    }
}