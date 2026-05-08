using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

[RequireComponent(typeof(CharacterController))]
public class StairsAgent : Agent
{
    public Transform targetTransform;
    public MeshRenderer floorRenderer;
    public Material defaultMaterial, winMaterial, loseMaterial;
    public float moveSpeed = 4f;
    public float gravity = 20f;

    // Geometria dos degraus (construídos em runtime)
    public float stepHeight = 0.5f;
    public float stepDepth = 0.7f;
    public float stepWidth = 3f;

    private CharacterController controller;
    private Vector3 velocity;
    private Transform stepsRoot;

    public override void Initialize()
    {
        controller = GetComponent<CharacterController>();
        controller.stepOffset = 1.0f;

        if (floorRenderer != null)
            floorRenderer.material = defaultMaterial;
    }

    public override void OnEpisodeBegin()
    {
        // 1) Lê o número de degraus do currículo (default 0 no Editor)
        int stepCount = Mathf.RoundToInt(
    Academy.Instance.EnvironmentParameters
        .GetWithDefault("step_count", 8f));

        // 2) Constrói a escada para esta lição
        BuildStaircase(stepCount);

        // 3) Reset do agente
        controller.enabled = false;
        transform.localPosition = new Vector3(
            Random.Range(-3.5f, 3.5f),
            0.5f,
            Random.Range(-3.5f, -1.5f));
        controller.enabled = true;

        velocity = Vector3.zero;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 toTarget = targetTransform.localPosition - transform.localPosition;
        sensor.AddObservation(toTarget.normalized); // 3 floats
        sensor.AddObservation(toTarget.magnitude / 10f); // 1 float
        sensor.AddObservation(controller.isGrounded ? 1f : 0f); // 1 float
        // Total: 5 floats
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        AddReward(-1f / MaxStep);
        AddReward(transform.localPosition.y * 0.001f);

        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveZ = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        Vector3 horizontal = new Vector3(moveX, 0f, moveZ) * moveSpeed;

        // Gravidade manual
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        velocity.y -= gravity * Time.deltaTime;

        Vector3 total = horizontal + Vector3.up * velocity.y;
        controller.Move(total * Time.deltaTime);

        // Caiu fora da arena?
        if (transform.localPosition.y < -2f)
        {
            AddReward(-1f);
            if (floorRenderer != null)
                floorRenderer.material = loseMaterial;
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions;
        ca[0] = Input.GetAxis("Horizontal");
        ca[1] = Input.GetAxis("Vertical");

        Debug.Log($"Heuristic chamado: X={ca[0]} Z={ca[1]}"); 
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Target"))
        {
            AddReward(+1f);
            if (floorRenderer != null)
                floorRenderer.material = winMaterial;
            EndEpisode();
        }
        else if (other.CompareTag("Wall"))
        {
            AddReward(-1f);
            if (floorRenderer != null)
                floorRenderer.material = loseMaterial;
            EndEpisode();
        }
    }

    private void BuildStaircase(int stepCount)
    {
        // Limpar escada anterior
        if (stepsRoot != null)
        {
            Destroy(stepsRoot.gameObject);
            stepsRoot = null;
        }

        if (stepCount <= 0)
        {
            // Lição 0: terreno plano; alvo no chão
            targetTransform.localPosition = new Vector3(
                Random.Range(-3f, 3f),
                0.5f,
                Random.Range(1.5f, 3.5f));
            return;
        }

        stepsRoot = new GameObject("StepsRoot").transform;
        stepsRoot.SetParent(transform.parent, worldPositionStays: false);

        // Curvas conforme o número de degraus
        int[] turns =
            stepCount == 5 ? new[] { 2 } :
            stepCount == 8 ? new[] { 2, 5 } : new int[0];

        Vector3 pos = new Vector3(0f, 0f, -1.5f);
        Vector3 dir = new Vector3(0f, 0f, 1f);
        int turnSign = +1;

        for (int i = 0; i < stepCount; i++)
        {
            if (System.Array.IndexOf(turns, i) >= 0)
            {
                dir = Quaternion.Euler(0f, 90f * turnSign, 0f) * dir;
                turnSign = -turnSign;
            }

            pos += dir * stepDepth;
            float h = (i + 1) * stepHeight; // altura cumulativa

            var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
            step.tag = "Platform";
            step.transform.SetParent(stepsRoot, false);
            step.transform.localPosition = new Vector3(pos.x, h / 2f, pos.z);
            step.transform.localRotation = Quaternion.LookRotation(dir);
            step.transform.localScale = new Vector3(stepWidth, h, stepDepth);
        }

        // Alvo no topo do último degrau
        targetTransform.localPosition = new Vector3(
            pos.x,
            stepCount * stepHeight + 0.5f,
            pos.z);
    }
}