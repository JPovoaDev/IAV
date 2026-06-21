using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

[RequireComponent(typeof(Rigidbody))]
public class SaboteurAgentPF : Agent {
    [Header("Arena")]
    public ParkourArenaPF arena;

    [Header("Alvos a sabotar")]
    public ParkourAgentPF[] targets;

    [Header("Movimento")]
    public float moveSpeed = 5f;
    public float jumpForce = 5f;

    [Header("Recompensas")]
    public float timePenaltyPerStep = -0.0002f;
    public float stillPenaltyThreshold = 0.5f;
    public float stayStillPenalty = -0.001f;
    public float targetFellReward = 0.3f; // recompensa principal: conseguir derrubar um alvo
    public float checkpointReward = 0.3f; // mas também é avaliado a percorrer o percurso ele próprio
    public float goalReward = 1f;

    [HideInInspector] public int nextCheckpointIdx = 0;
    [HideInInspector] public Transform nextPlatformTarget;
    [HideInInspector] public Vector3 pendingSpawn;
    [HideInInspector] public bool hasPendingSpawn = false;

    private Rigidbody rb;
    private bool canJump = false;
    private Transform currentTarget; // o ParkourAgentPF mais próximo neste momento


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

        if (hasPendingSpawn) {
            transform.position = pendingSpawn;
            rb.position = pendingSpawn;
            hasPendingSpawn = false;
        }

        UpdateNearestTarget();
    }

    public void ResetState() => UpdateNearestTarget();
    public void OnTargetFell() => AddReward(targetFellReward);

    // tem mais observações do que o ParkourAgentPF normal porque, para além de saber para
    // onde ir no percurso, também precisa de saber onde está o alvo mais próximo para o conseguir perseguir e empurrar
    public override void CollectObservations(VectorSensor sensor) {
        UpdateNearestTarget();

        Vector3 vel = rb.linearVelocity;
        sensor.AddObservation(vel.x / moveSpeed);                         // 1
        sensor.AddObservation(vel.y / jumpForce);                         // 2
        sensor.AddObservation(vel.z / moveSpeed);                         // 3

        if (nextPlatformTarget != null) {
            Vector3 toGoal = nextPlatformTarget.position - transform.position;
            sensor.AddObservation(toGoal.normalized);                     // 4,5,6
            sensor.AddObservation(toGoal.magnitude / 20f);                // 7
        } else {
            sensor.AddObservation(Vector3.zero);                          // 4,5,6
            sensor.AddObservation(0f);                                    // 7
        }

        if (currentTarget != null) {
            Vector3 toTarget = currentTarget.position - transform.position;
            sensor.AddObservation(toTarget.normalized);                   // 8,9,10
            sensor.AddObservation(toTarget.magnitude / 20f);              // 11
        } else {
            sensor.AddObservation(Vector3.zero);                          // 8,9,10
            sensor.AddObservation(0f);                                    // 11
        }

        sensor.AddObservation(transform.position.y / 10f);               // 12
        sensor.AddObservation(Mathf.Clamp(vel.y / jumpForce, -1f, 1f)); // 13
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

        if (transform.position.y < -3f)
            arena.OnSaboteurFell();
    }

    public override void Heuristic(in ActionBuffers actionsOut) {
        var ca = actionsOut.ContinuousActions;
        var da = actionsOut.DiscreteActions;
        ca[0] = Input.GetAxis("Horizontal");
        ca[1] = Input.GetAxis("Vertical");
        da[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }


    // ao colidir com um ParkourAgentPF, aplica um impulso na direção contrária e marca o alvo como "empurrado recentemente" (RegisterPush),
    // para que se ele cair logo a seguir, o ParkourArenaPF saiba dar a recompensa ao saboteur em vez de tratar isso como uma queda normal do agente
    private void OnCollisionEnter(Collision collision) {
        if (collision.gameObject.CompareTag("Platform"))
            canJump = true;

        if (collision.gameObject.CompareTag("ParkourAgent")) {
            Rigidbody targetRb = collision.gameObject.GetComponent<Rigidbody>();
            Vector3 pushDir = collision.transform.position - transform.position;
            pushDir.y = 0.3f;
            pushDir.Normalize();
            targetRb.AddForce(pushDir * 8f, ForceMode.Impulse);

            collision.gameObject.GetComponent<ParkourAgentPF>().RegisterPush();
        }
    }

    private void OnCollisionExit(Collision collision) {
        if (collision.gameObject.CompareTag("Platform"))
            canJump = false;
    }

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Checkpoint") || other.CompareTag("Goal")) {
            var cp = other.GetComponent<CheckpointTriggerPF>();

            if (cp != null) 
                arena.OnCheckpointHitSaboteur(cp);
        }
        if (other.CompareTag("DeathZone"))
            arena.OnSaboteurFell();
    }


    private void UpdateNearestTarget() {
        float minDist = float.MaxValue;
        currentTarget = null;

        foreach (var t in targets) {
            if (t == null) 
                continue;

            float dist = Vector3.Distance(transform.position, t.transform.position);

            if (dist < minDist) { 
                minDist = dist; 
                currentTarget = t.transform; 
            }
        }
    }
}