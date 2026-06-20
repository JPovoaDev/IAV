using UnityEngine;
using Unity.MLAgents;

public class PlatformSpawnerPF : MonoBehaviour {
    [Header("Prefabs")]
    public GameObject platformPrefab;
    public GameObject checkpointPrefab;
    public GameObject goalPrefab;

    [Header("Parâmetros base")]
    public float platformHeight = 0.5f;
    public float platformWidth = 8f;
    public float platformDepth = 8f;

    [HideInInspector] public int forcedLesson = -1;
    private GameObject[] spawnedCheckpoints;

    public CheckpointTriggerPF[] GetCheckpoints() =>
        System.Array.ConvertAll(spawnedCheckpoints,
            cp => cp.GetComponent<CheckpointTriggerPF>());


    public void BuildCourse() {
        /*int lesson = Mathf.RoundToInt(
            Academy.Instance.EnvironmentParameters.GetWithDefault("lesson", 0f));*/ // usar isto para treinar

        int lesson = forcedLesson >= 0 ? forcedLesson : 2;

        float gap;
        int numPlatforms;
        float heightVariation;

        switch (lesson) {
            case 0: gap = 5f; numPlatforms = 3; heightVariation = 0f; break;
            case 1: gap = 7f; numPlatforms = 4; heightVariation = 0f; break;
            case 2: gap = 8f; numPlatforms = 5; heightVariation = 0.8f; break;
            default: gap = 10f; numPlatforms = 6; heightVariation = 1.5f; break;
        }

        ClearAll();
        spawnedCheckpoints = new GameObject[numPlatforms];

        SpawnPlatform(
            new Vector3(0f, platformHeight / 2f, 0f),
            new Vector3(5f, platformHeight, 5f)
        );

        float currentZ = 0f;
        float currentY = 0f;

        for (int i = 0; i < numPlatforms; i++) {
            currentZ += gap;
            float x = Random.Range(-2f, 2f);

            if (heightVariation > 0f)
                currentY = Mathf.Clamp(
                    currentY + Random.Range(-1f, 1f) * heightVariation,
                    -1f, 4f);

            float platY = platformHeight / 2f + currentY;

            SpawnPlatform(
                new Vector3(x, platY, currentZ),
                new Vector3(platformWidth, platformHeight, platformDepth)
            );

            bool isGoal = (i == numPlatforms - 1);
            Vector3 cpPos = new Vector3(x, platY + platformHeight + 0.5f, currentZ);
            spawnedCheckpoints[i] = SpawnCheckpoint(cpPos, i, isGoal);
        }
    }

    public Vector3 AgentSpawnPosition =>
        transform.TransformPoint(new Vector3(0f, platformHeight + 0.5f, 0f));


    private GameObject SpawnPlatform(Vector3 localPosition, Vector3 scale) {
        Vector3 worldPos = transform.TransformPoint(localPosition);
        var p = Instantiate(platformPrefab, worldPos, Quaternion.identity);
        p.transform.SetParent(transform);
        p.transform.localScale = scale;
        p.tag = "Platform";
        return p;
    }

    private GameObject SpawnCheckpoint(Vector3 localPosition, int index, bool isGoal) {
        Vector3 worldPos = transform.TransformPoint(localPosition);
        var prefab = isGoal ? goalPrefab : checkpointPrefab;
        var cp = Instantiate(prefab, worldPos, Quaternion.identity);
        cp.transform.SetParent(transform);
        var trigger = cp.GetComponent<CheckpointTriggerPF>();
        trigger.checkpointIndex = index;
        trigger.isGoal = isGoal;
        cp.tag = isGoal ? "Goal" : "Checkpoint";
        return cp;
    }

    private void ClearAll() {
        foreach (Transform child in transform)
            Destroy(child.gameObject);
    }
}