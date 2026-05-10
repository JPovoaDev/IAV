using UnityEngine;
using Unity.MLAgents;

public class PlatformSpawner : MonoBehaviour
{
    [Header("Prefab das plataformas")]
    public GameObject platformPrefab;
    public GameObject checkpointPrefab;
    public GameObject goalPrefab;

    [Header("Parâmetros base")]
    public int numberOfPlatforms = 4;
    public float platformHeight = 0.5f;
    public float platformWidth = 3f;

    private GameObject[] spawnedPlatforms;
    private GameObject[] spawnedCheckpoints;

    public CheckpointTrigger[] GetCheckpoints() =>
        System.Array.ConvertAll(spawnedCheckpoints,
            cp => cp.GetComponent<CheckpointTrigger>());

    public void BuildCourse()
    {
        float gap = Academy.Instance.EnvironmentParameters
            .GetWithDefault("platform_gap", 5f);

        int numPlatforms = Mathf.RoundToInt(Academy.Instance.EnvironmentParameters
            .GetWithDefault("num_platforms", 4f));

        float heightVariation = Academy.Instance.EnvironmentParameters
            .GetWithDefault("height_variation", 0f); // 0 = plano, 1 = com alturas

        Debug.Log("Gap: " + gap + " | Plataformas: " + numPlatforms + " | Altura: " + heightVariation);

        ClearPlatforms();
        numberOfPlatforms = numPlatforms;

        spawnedPlatforms = new GameObject[numberOfPlatforms];
        spawnedCheckpoints = new GameObject[numberOfPlatforms];

        float halfH = platformHeight / 2f;
        SpawnPlatform(new Vector3(0, halfH, 0), new Vector3(3, platformHeight, 3));

        float currentZ = 0f;
        float currentY = 0f; // altura atual acumulada

        for (int i = 0; i < numberOfPlatforms; i++)
        {
            currentZ += gap;
            float x = Random.Range(-1f, 1f);

            // Variação de altura — só ativa nos níveis difíceis
            if (heightVariation > 0f)
            {
                // Sobe ou desce aleatoriamente entre -1 e +1
                float deltaY = Random.Range(-1f, 1f) * heightVariation;
                currentY = Mathf.Clamp(currentY + deltaY, -1f, 3f); // limitar altura
            }

            float platY = halfH + currentY;
            Vector3 platPos = new Vector3(x, platY, currentZ);
            Vector3 platScale = new Vector3(platformWidth, platformHeight, platformWidth);
            spawnedPlatforms[i] = SpawnPlatform(platPos, platScale);

            bool isGoal = (i == numberOfPlatforms - 1);
            Vector3 cpPos = new Vector3(x, platY + platformHeight + 0.5f, currentZ);
            spawnedCheckpoints[i] = SpawnCheckpoint(cpPos, i, isGoal);
        }
    }
    private GameObject SpawnPlatform(Vector3 position, Vector3 scale)
    {
        var p = Instantiate(platformPrefab, position, Quaternion.identity);
        p.transform.SetParent(transform);
        p.transform.localScale = scale;
        p.tag = "Platform";
        return p;
    }

    private GameObject SpawnCheckpoint(Vector3 position, int index, bool isGoal)
    {
        var prefab = isGoal ? goalPrefab : checkpointPrefab;

        if (prefab == null)
        {
            Debug.LogError("Prefab null! isGoal: " + isGoal);
            return null;
        }

        var cp = Instantiate(prefab, position, Quaternion.identity);
        cp.transform.SetParent(transform);

        var trigger = cp.GetComponent<CheckpointTrigger>();
        if (trigger == null)
        {
            Debug.LogError("CheckpointTrigger não encontrado no prefab: " + prefab.name);
            return cp;
        }

        trigger.checkpointIndex = index;
        trigger.isGoal = isGoal;
        cp.tag = isGoal ? "Goal" : "Checkpoint";
        return cp;
    }

    private void ClearPlatforms()
    {
        foreach (Transform child in transform)
            Destroy(child.gameObject);
    }
}