using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class FoodButtonAgent : Agent {
    [Header("Referências da cena")]
    public Transform buttonTransform;
    public Transform foodTransform;
    public MeshRenderer floorRenderer;
    public Transform button2Transform;

    [Header("Feedback visual")]
    public Material winMaterial;
    public Material loseMaterial;

    [Header("Parâmetros")]
    private float moveSpeed = 5f;

    private bool button1Pressed = false;
    private bool button2Pressed = false;
    private int cooldownSteps = 250;
    private int cooldownRemaining = 0;

    private Vector3 foodVelocity;
    private float foodSpeed = 1f;

    public override void OnEpisodeBegin() {
        cooldownRemaining = 0;

        transform.localPosition = new Vector3(Random.Range(-3.5f, 3.5f), 0.5f, Random.Range(-3.5f, 3.5f));
        foodVelocity = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized * foodSpeed;

        //buttonTransform.localPosition = new Vector3(3f, 0.5f, 2f);

        buttonTransform.localPosition = new Vector3(Random.Range(-3.5f, 3.5f), 0.5f, Random.Range(-3.5f, 3.5f));
        button2Transform.localPosition = new Vector3(Random.Range(-3.5f, 3.5f), 0.5f, Random.Range(-3.5f, 3.5f));

        foodTransform.gameObject.SetActive(false);
        foodTransform.localPosition = RandomFoodPosition();

        button1Pressed = false;
        button2Pressed = false;
    }

    private void Update() {
        if (foodTransform.gameObject.activeSelf) {
            foodTransform.localPosition += foodVelocity * Time.deltaTime;

            // Bounce nas bordas da arena
            if (Mathf.Abs(foodTransform.localPosition.x) > 3.5f) {
                foodVelocity.x = -foodVelocity.x;
            }
            if (Mathf.Abs(foodTransform.localPosition.z) > 3.5f) {
                foodVelocity.z = -foodVelocity.z;
            }
        }
    }

    private Vector3 RandomFoodPosition() {
        return new Vector3(
            Random.Range(-3.5f, 3.5f), 0.5f, Random.Range(-3.5f, 3.5f));
    }

    public override void CollectObservations(VectorSensor sensor) {
        /*Vector3 dirBtn = (buttonTransform.localPosition - transform.localPosition).normalized;
        float distBtn = Vector3.Distance(buttonTransform.localPosition, transform.localPosition) / 10f;
        sensor.AddObservation(dirBtn);   // 3 floats
        sensor.AddObservation(distBtn);  // 1 float

        if (foodTransform.gameObject.activeSelf) {
            Vector3 dirFood = (foodTransform.localPosition - transform.localPosition).normalized;
            float distFood = Vector3.Distance(foodTransform.localPosition, transform.localPosition) / 10f;
            sensor.AddObservation(dirFood);  // 3 floats
            sensor.AddObservation(distFood); // 1 float
        } else {
            sensor.AddObservation(Vector3.zero); // 3 floats placeholder
            sensor.AddObservation(0f);           // 1 float placeholder
        }*/

        /*Vector3 dirBtn = (buttonTransform.localPosition - transform.localPosition).normalized;
        float distBtn = Vector3.Distance(buttonTransform.localPosition,
        transform.localPosition) / 10f;
        sensor.AddObservation(dirBtn); // 3
        sensor.AddObservation(distBtn); // 1
                                        // Sem observação da comida!*/

        /*sensor.AddObservation(transform.localPosition);         // 3
        sensor.AddObservation(buttonTransform.localPosition);   // 3
        if (foodTransform.gameObject.activeSelf)
            sensor.AddObservation(foodTransform.localPosition); // 3
        else
            sensor.AddObservation(Vector3.zero);                // 3*/

        Vector3 dirBtn = (buttonTransform.localPosition - transform.localPosition).normalized;
        float distBtn = Vector3.Distance(buttonTransform.localPosition, transform.localPosition) / 10f;
        sensor.AddObservation(dirBtn);   // 3 floats
        sensor.AddObservation(distBtn);  // 1 float

        Vector3 dirBtn2 = (button2Transform.localPosition - transform.localPosition).normalized;
        float distBtn2 = Vector3.Distance(button2Transform.localPosition, transform.localPosition) / 10f;
        sensor.AddObservation(dirBtn2);  // 3 floats
        sensor.AddObservation(distBtn2); // 1 float

        if (foodTransform.gameObject.activeSelf) {
            Vector3 dirFood = (foodTransform.localPosition - transform.localPosition).normalized;
            float distFood = Vector3.Distance(foodTransform.localPosition, transform.localPosition) / 10f;
            sensor.AddObservation(dirFood);  // 3 floats
            sensor.AddObservation(distFood); // 1 float
            sensor.AddObservation(foodVelocity.normalized); // 3 floats - direção do movimento

        } else {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0f);
            sensor.AddObservation(Vector3.zero);
        }

        sensor.AddObservation(button1Pressed ? 1f : 0f); // 1 float
        sensor.AddObservation(button2Pressed ? 1f : 0f); // 1 float
        sensor.AddObservation(cooldownRemaining / (float)cooldownSteps); // 1 float
    }

    public override void OnActionReceived(ActionBuffers actions) {
        AddReward(-1f / MaxStep);

        if (StepCount >= MaxStep) {
            AddReward(-1f);
            EndEpisode();
            return;
        }

        /*if (!buttonPressed) {
            float dist = Vector3.Distance(transform.localPosition, buttonTransform.localPosition);
            AddReward((10f - dist) * 0.01f);
        }*/

        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        transform.localPosition += new Vector3(moveX, 0, moveZ) * Time.deltaTime * moveSpeed;
        int press = actions.DiscreteActions[0];
        /*if (press == 1 && !button1Pressed) {
            float distToButton = Vector3.Distance(transform.localPosition, buttonTransform.localPosition);
            if (distToButton < 1f) {
                AddReward(+0.5f);
                button1Pressed = true;
                if (button2Pressed) 
                    foodTransform.gameObject.SetActive(true);
            }
        }

        if (press == 1 && !button2Pressed) {
            float distToButton2 = Vector3.Distance(transform.localPosition, button2Transform.localPosition);
            if (distToButton2 < 1f) {
                AddReward(+0.5f);
                button2Pressed = true;
                if (button1Pressed) 
                    foodTransform.gameObject.SetActive(true);
            }
        }*/

        if (press == 1 && !button1Pressed) {
            float distToButton = Vector3.Distance(transform.localPosition, buttonTransform.localPosition);
            if (distToButton < 1f) {
                AddReward(+0.5f);
                button1Pressed = true;
            }
        }

        if (press == 1 && !button2Pressed) {
            float distToButton2 = Vector3.Distance(transform.localPosition, button2Transform.localPosition);
            if (distToButton2 < 1f) {
                AddReward(+0.5f);
                button2Pressed = true;
            }
        }

        // Cooldown só começa quando ambos estiverem premidos
        if (button1Pressed && button2Pressed && cooldownRemaining == 0 && !foodTransform.gameObject.activeSelf) {
            cooldownRemaining = cooldownSteps;
        }

        if (cooldownRemaining > 0) {
            cooldownRemaining--;
            if (cooldownRemaining == 0)
                foodTransform.gameObject.SetActive(true);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut) {
        var ca = actionsOut.ContinuousActions;
        ca[0] = Input.GetAxis("Horizontal");
        ca[1] = Input.GetAxis("Vertical");

        var da = actionsOut.DiscreteActions;
        da[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Food")) {
            AddReward(+1f);
            floorRenderer.material = winMaterial;
            EndEpisode();
        }
        if (other.CompareTag("Wall")) {
            AddReward(-1f);
            floorRenderer.material = loseMaterial;
            EndEpisode();
        }
    }
}