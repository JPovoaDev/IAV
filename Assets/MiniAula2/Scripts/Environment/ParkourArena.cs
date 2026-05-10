using UnityEngine;

public class ParkourArena : MonoBehaviour
{
    public ParkourAgent agent;
    public PlatformSpawner spawner;  // ← adicionar esta referência

    public MeshRenderer floorRenderer;
    public Material defaultMaterial, winMaterial, loseMaterial;

    private CheckpointTrigger[] checkpoints;
    private int nextCheckpointIndex = 0;

    public void ResetEpisode()
    {
        // Reconstruir percurso com novo gap do currículo
        spawner.BuildCourse();

        // Buscar os checkpoints recém criados
        checkpoints = spawner.GetCheckpoints();

        nextCheckpointIndex = 0;

    

        // Atualizar primeiro alvo do agente
        if (checkpoints.Length > 0)
            agent.nextPlatformTarget = checkpoints[0].transform;
    }

    public float OnCheckpointHit(int index, bool isGoal)
    {
        if (index != nextCheckpointIndex) return 0f;

        checkpoints[index].Activate();
        nextCheckpointIndex++;

        // Atualizar próximo alvo
        if (nextCheckpointIndex < checkpoints.Length)
            agent.nextPlatformTarget = checkpoints[nextCheckpointIndex].transform;

        if (isGoal)
        {
            if (floorRenderer != null) floorRenderer.material = winMaterial;
            return 1f;
        }

        return 0.3f;
    }

    public void OnAgentDied()
    {
        Debug.Log("OnAgentDied chamado!");
        if (floorRenderer != null)
            floorRenderer.material = loseMaterial;
        else
            Debug.LogError("floorRenderer é null!");
    }
}