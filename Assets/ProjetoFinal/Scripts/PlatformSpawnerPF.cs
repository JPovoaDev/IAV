using UnityEngine;
using Unity.MLAgents;

// Gera o percurso de plataformas em si. Este script é reaproveitado de dois sítios
// diferentes: durante o treino dos agentes (ParkourAgentPF / SaboteurAgentPF via
// ML-Agents) e também no minijogo real que o jogador vê depois de apostar com o
// GamblerNPCLLMPF. É o mesmo código nos dois casos, só muda o "forcedLesson" para
// fixar a dificuldade quando não estamos a treinar.
public class PlatformSpawnerPF : MonoBehaviour {
    [Header("Prefabs")]
    public GameObject platformPrefab;
    public GameObject checkpointPrefab;
    public GameObject goalPrefab;

    [Header("Parâmetros base")]
    public float platformHeight = 0.5f;
    public float platformWidth = 8f;
    public float platformDepth = 8f;

    // quando -1, usava-se o currículo do ML-Agents (lesson das environment parameters)
    // durante o treino; no minijogo real isto fica fixo no Inspector para garantir
    // sempre o mesmo nível de dificuldade que o jogador vai ver
    [HideInInspector] public int forcedLesson = -1;
    private GameObject[] spawnedCheckpoints;

    // o ParkourArenaPF chama isto depois de BuildCourse() para saber os checkpoints
    // gerados e poder ligar cada agente ao primeiro checkpoint do percurso
    public CheckpointTriggerPF[] GetCheckpoints() => System.Array.ConvertAll(spawnedCheckpoints,
            cp => cp.GetComponent<CheckpointTriggerPF>());


    public void BuildCourse() {
        /*int lesson = Mathf.RoundToInt(
            Academy.Instance.EnvironmentParameters.GetWithDefault("lesson", 0f));*/ // usar isto para treinar

        int lesson = forcedLesson >= 0 ? forcedLesson : 2;

        // curriculum learning manual: lições mais baixas têm gaps mais pequenos e
        // percursos mais curtos, para o agente aprender o básico antes de ir para
        // percursos com mais variação de altura
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

        // plataforma inicial, onde os agentes (e o jogador, ao ver pela TV) começam
        SpawnPlatform(
            new Vector3(0f, platformHeight / 2f, 0f),
            new Vector3(5f, platformHeight, 5f)
        );

        float currentZ = 0f;
        float currentY = 0f;

        for (int i = 0; i < numPlatforms; i++) {
            currentZ += gap;
            float x = Random.Range(-2f, 2f);

            // random walk na altura, limitado para não ficar impossível de saltar
            if (heightVariation > 0f)
                currentY = Mathf.Clamp(currentY + Random.Range(-1f, 1f) * heightVariation, -1f, 4f);

            float platY = platformHeight / 2f + currentY;

            SpawnPlatform(
                new Vector3(x, platY, currentZ),
                new Vector3(platformWidth, platformHeight, platformDepth)
            );

            // a última plataforma do percurso é sempre o goal, o resto são checkpoints normais
            bool isGoal = (i == numPlatforms - 1);
            Vector3 cpPos = new Vector3(x, platY + platformHeight + 0.5f, currentZ);
            spawnedCheckpoints[i] = SpawnCheckpoint(cpPos, i, isGoal);
        }
    }

    // ponto de spawn dos agentes, usado pelo ParkourArenaPF sempre que um agente cai ou termina o percurso e precisa de voltar ao início
    public Vector3 AgentSpawnPosition => transform.TransformPoint(new Vector3(0f, platformHeight + 0.5f, 0f));


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

    // destrói o percurso anterior antes de gerar um novo, usado tanto no reset de
    // episódio durante o treino como sempre que o jogador inicia uma nova aposta
    private void ClearAll() {
        foreach (Transform child in transform)
            Destroy(child.gameObject);
    }
}