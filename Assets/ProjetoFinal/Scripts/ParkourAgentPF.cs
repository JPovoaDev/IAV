using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

[RequireComponent(typeof(Rigidbody))]
public class ParkourAgentPF : Agent {
    [Header("Arena")]
    public ParkourArenaPF arena;

    [Header("Observações")]
    public Transform nextPlatformTarget;

    [Header("Movimento")]
    public float moveSpeed = 4f;
    public float jumpForce = 5f;

    [Header("Recompensas")]
    public float timePenaltyPerStep = -0.001f;
    public float stillPenaltyThreshold = 0.5f;
    public float stayStillPenalty = -0.002f;
    public float fallPenalty = 1f;
    public float checkpointReward = 0.3f;
    public float goalReward = 1f;

    [HideInInspector] public int nextCheckpointIdx = 0;
    [HideInInspector] public Vector3 pendingSpawn;
    [HideInInspector] public bool hasPendingSpawn = false;

    [HideInInspector] public bool wasRecentlyPushed = false;
    private float pushTimer = 0f;
    private const float pushWindow = 2f;

    private Rigidbody rb;
    private bool canJump = false;


    public override void Initialize() {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    public void SetSpawnPosition(Vector3 pos) {
        rb.position = pos;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = pos;
        transform.rotation = Quaternion.identity;
    }


    public override void OnEpisodeBegin() {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        canJump = false;
        nextCheckpointIdx = 0;
        wasRecentlyPushed = false;
        pushTimer = 0f;

        if (hasPendingSpawn) {
            transform.position = pendingSpawn;
            rb.position = pendingSpawn;
            hasPendingSpawn = false;
        }
    }

    private void Update() {
        if (wasRecentlyPushed) {
            pushTimer -= Time.deltaTime;
            if (pushTimer <= 0f) wasRecentlyPushed = false;
        }
    }

    public void RegisterPush() {
        wasRecentlyPushed = true;
        pushTimer = pushWindow;
    }

    public override void CollectObservations(VectorSensor sensor) {
        Vector3 vel = rb.linearVelocity;
        sensor.AddObservation(vel.x / moveSpeed);                         // 1
        sensor.AddObservation(vel.y / jumpForce);                         // 2
        sensor.AddObservation(vel.z / moveSpeed);                         // 3

        if (nextPlatformTarget != null) {
            Vector3 toTarget = nextPlatformTarget.position - transform.position;
            sensor.AddObservation(toTarget.normalized);                   // 4,5,6
            sensor.AddObservation(toTarget.magnitude / 20f);              // 7
        } else {
            sensor.AddObservation(Vector3.zero);                          // 4,5,6
            sensor.AddObservation(0f);                                    // 7
        }

        sensor.AddObservation(transform.position.y / 10f);               // 8
        sensor.AddObservation(Mathf.Clamp(vel.y / jumpForce, -1f, 1f)); // 9
    }


    public override void OnActionReceived(ActionBuffers actions) {
        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveZ = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        Vector3 targetVel = new Vector3(moveX * moveSpeed, rb.linearVelocity.y, moveZ * moveSpeed);
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVel, 0.3f);

        int jump = actions.DiscreteActions[0];
        if (jump == 1 && canJump) {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
            canJump = false;
        }

        AddReward(timePenaltyPerStep);

        float speed2D = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude;
        if (speed2D < stillPenaltyThreshold)
            AddReward(stayStillPenalty);

        if (transform.position.y < -3f && !hasPendingSpawn)
            arena.OnAgentFell(this);
    }

    public override void Heuristic(in ActionBuffers actionsOut) {
        var ca = actionsOut.ContinuousActions;
        var da = actionsOut.DiscreteActions;
        ca[0] = Input.GetAxis("Horizontal");
        ca[1] = Input.GetAxis("Vertical");
        da[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }

    private void OnCollisionEnter(Collision collision) {
        if (collision.gameObject.CompareTag("Platform"))
            canJump = true;
    }

    private void OnCollisionExit(Collision collision) {
        if (collision.gameObject.CompareTag("Platform"))
            canJump = false;
    }

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Checkpoint") || other.CompareTag("Goal")) {
            var cp = other.GetComponent<CheckpointTriggerPF>();
            if (cp != null) arena.OnCheckpointHit(this, cp);
        }
        if (other.CompareTag("DeathZone"))
            arena.OnAgentFell(this);
    }
}