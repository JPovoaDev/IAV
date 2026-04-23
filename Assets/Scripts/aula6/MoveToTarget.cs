using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class MoveToTarget : Agent
{
    public Transform targetTransform;
    public MeshRenderer floorRenderer;
    public Material winMaterial;
    public Material loseMaterial;
    public float moveSpeed = 5f;

    public override void OnEpisodeBegin()
    {
        transform.localPosition = new Vector3(
            Random.Range(-4f, 4f),
            0.5f,
            Random.Range(-3.5f, -0.5f)
        );

        targetTransform.localPosition = new Vector3(
            Random.Range(-4f, 4f),
            0.5f,
            Random.Range(0.5f, 3.5f)
        );
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 direction = (targetTransform.position - transform.position).normalized;
        sensor.AddObservation(direction); 

        float distance = Vector3.Distance(transform.position, targetTransform.position);
        sensor.AddObservation(distance / 10f); 
    }
    public override void OnActionReceived(ActionBuffers actions)
    {
        int action = actions.DiscreteActions[0];
        Vector3 move = Vector3.zero;

        switch (action)
        {
            case 1: move = Vector3.forward; break;
            case 2: move = Vector3.back; break;
            case 3: move = Vector3.left; break;
            case 4: move = Vector3.right; break;
        }

        transform.position += move * Time.deltaTime * moveSpeed;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var da = actionsOut.DiscreteActions;
        da[0] = 0;
        if (Input.GetKey(KeyCode.W)) da[0] = 1;
        if (Input.GetKey(KeyCode.S)) da[0] = 2;
        if (Input.GetKey(KeyCode.A)) da[0] = 3;
        if (Input.GetKey(KeyCode.D)) da[0] = 4;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Target"))
        {
            SetReward(+1f);
            floorRenderer.material = winMaterial;
        }
        if (other.CompareTag("Wall"))
        {
            SetReward(-1f);
            floorRenderer.material = loseMaterial;
        }
        EndEpisode();
    }
}