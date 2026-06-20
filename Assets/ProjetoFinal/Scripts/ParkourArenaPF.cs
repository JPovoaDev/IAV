// ParkourArena.cs
using UnityEngine;

public class ParkourArenaPF : MonoBehaviour {
    [Header("Agentes")]
    public ParkourAgentPF[] agents;
    public SaboteurAgentPF saboteur;

    [Header("Spawner")]
    public PlatformSpawnerPF spawner;

    [Header("Feedback Visual")]
    public MeshRenderer floorRenderer;
    public Material defaultMaterial;
    public Material winMaterial;         // verde  — parkour chegou ao goal
    public Material loseMaterial;        // vermelho — agente caiu
    public Material saboteurWinMaterial; // laranja — saboteur chegou ao goal

    [Header("Episódio")]
    public int maxEpisodeSteps = 5000;

    private CheckpointTriggerPF[] checkpoints;
    private int stepCount;
    private bool episodeReady = false;
    private int agentsFinished = 0;
    private Coroutine flashCoroutine;
    [HideInInspector] public System.Action<int> onRaceWinner;
    private bool raceWinnerFired = false;

    private bool SaboteurActive =>
        saboteur != null && saboteur.gameObject.activeInHierarchy;

    private void FlashFloor(Material mat, float duration = 1f) {
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine(mat, duration));
    }

    private System.Collections.IEnumerator FlashRoutine(Material mat, float duration) {
        floorRenderer.material = mat;
        yield return new WaitForSeconds(duration);
        floorRenderer.material = defaultMaterial;
        flashCoroutine = null;
    }

    private void Start() {
        BuildAndReset();
        episodeReady = true;
    }

    private void FixedUpdate() {
        if (!episodeReady) return;
        stepCount++;
        if (stepCount >= maxEpisodeSteps)
            FullReset("timeout");
    }


    public void OnSaboteurFell() {
        if (!episodeReady || !SaboteurActive) return;
        FlashFloor(loseMaterial);
        saboteur.pendingSpawn = spawner.AgentSpawnPosition +
            new Vector3(Random.Range(-0.5f, 0.5f), 0f, 0f);
        saboteur.hasPendingSpawn = true;
        saboteur.EndEpisode();
    }

    public void OnSaboteurGoal() {
        if (!episodeReady || !SaboteurActive) return;
        FlashFloor(saboteurWinMaterial);
        FullReset("saboteur_won");
    }

    public void OnCheckpointHit(ParkourAgentPF agent, CheckpointTriggerPF cp) {
        if (!episodeReady) return;
        if (cp.checkpointIndex != agent.nextCheckpointIdx) return;

        cp.Activate();
        agent.nextCheckpointIdx++;

        if (!cp.isGoal) {
            agent.AddReward(agent.checkpointReward);
            agent.nextPlatformTarget = checkpoints[agent.nextCheckpointIdx].transform;
            return;
        }

        // Chegou ao goal
        agent.AddReward(agent.goalReward);
        FlashFloor(winMaterial);
        agentsFinished++;
        if (!raceWinnerFired) {
            raceWinnerFired = true;
            onRaceWinner?.Invoke(System.Array.IndexOf(agents, agent));
        }

        agent.pendingSpawn = spawner.AgentSpawnPosition +
            new Vector3(Random.Range(-0.5f, 0.5f), 0f, 0f);
        agent.hasPendingSpawn = true;
        agent.nextPlatformTarget = checkpoints[0].transform;
        agent.EndEpisode();

        if (agentsFinished >= agents.Length)
            FullReset("all_finished");
    }

    public void OnAgentFell(ParkourAgentPF agent) {
        if (!episodeReady) return;
        agent.AddReward(-agent.fallPenalty);

        if (agent.wasRecentlyPushed && SaboteurActive)
            saboteur.OnTargetFell();

        FlashFloor(loseMaterial);

        for (int i = 0; i < agent.nextCheckpointIdx; i++)
            checkpoints[i].Reset();

        agent.pendingSpawn = spawner.AgentSpawnPosition +
            new Vector3(Random.Range(-0.5f, 0.5f), 0f, 0f);
        agent.hasPendingSpawn = true;
        agent.nextCheckpointIdx = 0;
        agent.nextPlatformTarget = checkpoints[0].transform;
        agent.EndEpisode();
    }

    public void OnCheckpointHitSaboteur(CheckpointTriggerPF cp) {
        if (!episodeReady || !SaboteurActive) return;
        if (cp.checkpointIndex != saboteur.nextCheckpointIdx) return;

        cp.Activate();
        saboteur.nextCheckpointIdx++;

        if (!cp.isGoal) {
            saboteur.AddReward(saboteur.checkpointReward);
            saboteur.nextPlatformTarget = checkpoints[saboteur.nextCheckpointIdx].transform;
            return;
        }

        saboteur.AddReward(saboteur.goalReward);
        OnSaboteurGoal();
    }


    private void FullReset(string reason) {
        episodeReady = false;
        agentsFinished = 0;

        Debug.Log("FullReset: " + reason);

        foreach (var a in agents) {
            if (a == null) continue;
            a.pendingSpawn = spawner.AgentSpawnPosition +
                new Vector3(Random.Range(-0.5f, 0.5f), 0f, 0f);
            a.hasPendingSpawn = true;
            a.EndEpisode();
        }

        if (SaboteurActive) {
            saboteur.pendingSpawn = spawner.AgentSpawnPosition +
                new Vector3(Random.Range(-0.5f, 0.5f), 0f, 0f);
            saboteur.hasPendingSpawn = true;
            saboteur.EndEpisode();
        }

        BuildAndReset();
        episodeReady = true;
    }

    private void BuildAndReset() {
        stepCount = 0;
        agentsFinished = 0;
        raceWinnerFired = false;
        spawner.BuildCourse();
        checkpoints = spawner.GetCheckpoints();

        foreach (var a in agents) {
            if (a == null) continue;
            a.nextCheckpointIdx = 0;
            a.nextPlatformTarget = checkpoints[0].transform;
        }

        if (SaboteurActive) {
            saboteur.nextCheckpointIdx = 0;
            saboteur.nextPlatformTarget = checkpoints[0].transform;
        }

        floorRenderer.material = defaultMaterial;
    }
}