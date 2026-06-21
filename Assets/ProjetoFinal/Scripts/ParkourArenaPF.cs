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
    public Material winMaterial; // verde  —> parkour chegou ao goal
    public Material loseMaterial; // vermelho —> agente caiu
    public Material saboteurWinMaterial; // laranja —> saboteur chegou ao goal

    [Header("Episódio")]
    public int maxEpisodeSteps = 5000; // timeout de segurança, para o episódio não ficar preso para sempre

    private CheckpointTriggerPF[] checkpoints;
    private int stepCount;
    private bool episodeReady = false;
    private int agentsFinished = 0;
    private Coroutine flashCoroutine;

    // evento público que o GamblerNPCLLMPF subscreve para saber o índice do agente
    // vencedor assim que a corrida acaba
    [HideInInspector] public System.Action<int> onRaceWinner;
    private bool raceWinnerFired = false; // garante que só dispara uma vez por corrida, mesmo que vários agentes cheguem perto

    private bool SaboteurActive => saboteur != null && saboteur.gameObject.activeInHierarchy;

    private void FlashFloor(Material mat, float duration = 1f) {
        if (flashCoroutine != null) 
            StopCoroutine(flashCoroutine);

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
        if (!episodeReady) 
            return;

        stepCount++;

        if (stepCount >= maxEpisodeSteps)
            FullReset("timeout");
    }


    public void OnSaboteurFell() {
        if (!episodeReady || !SaboteurActive) 
            return;

        FlashFloor(loseMaterial);

        saboteur.pendingSpawn = spawner.AgentSpawnPosition + new Vector3(Random.Range(-0.5f, 0.5f), 0f, 0f);
        saboteur.hasPendingSpawn = true;
        saboteur.EndEpisode();
    }

    public void OnSaboteurGoal() {
        if (!episodeReady || !SaboteurActive) 
            return;

        FlashFloor(saboteurWinMaterial);
        FullReset("saboteur_won");
    }

    // chamado pelo ParkourAgentPF quando toca num checkpoint, valida que é mesmo o
    // próximo checkpoint na ordem (evita que um agente "salte" checkpoints por engano
    // de física, embora nunca aconteça) e avança o índice
    // se for o goal, é aqui que o vencedor é decidido
    public void OnCheckpointHit(ParkourAgentPF agent, CheckpointTriggerPF cp) {
        if (!episodeReady) 
            return;

        if (cp.checkpointIndex != agent.nextCheckpointIdx) 
            return;

        cp.Activate();
        agent.nextCheckpointIdx++;

        if (!cp.isGoal) {
            agent.AddReward(agent.checkpointReward);
            agent.nextPlatformTarget = checkpoints[agent.nextCheckpointIdx].transform;
            return;
        }

        // chegou ao goal
        agent.AddReward(agent.goalReward);
        FlashFloor(winMaterial);
        agentsFinished++;

        // só o primeiro agente a chegar conta como vencedor da corrida, é isto que
        // decide se o jogador ganhou ou perdeu a aposta no GamblerNPCLLMPF
        if (!raceWinnerFired) {
            raceWinnerFired = true;
            onRaceWinner?.Invoke(System.Array.IndexOf(agents, agent));
        }

        agent.pendingSpawn = spawner.AgentSpawnPosition + new Vector3(Random.Range(-0.5f, 0.5f), 0f, 0f);
        agent.hasPendingSpawn = true;
        agent.nextPlatformTarget = checkpoints[0].transform;
        agent.EndEpisode();

        if (agentsFinished >= agents.Length)
            FullReset("all_finished");
    }

    public void OnAgentFell(ParkourAgentPF agent) {
        if (!episodeReady) return;
        agent.AddReward(-agent.fallPenalty);

        // se o agente caiu logo depois de ter sido empurrado pelo saboteur, a culpa
        // (e a recompensa) é atribuída ao saboteur, não fica só como erro do agente
        if (agent.wasRecentlyPushed && SaboteurActive)
            saboteur.OnTargetFell();

        FlashFloor(loseMaterial);

        for (int i = 0; i < agent.nextCheckpointIdx; i++)
            checkpoints[i].Reset();

        agent.pendingSpawn = spawner.AgentSpawnPosition + new Vector3(Random.Range(-0.5f, 0.5f), 0f, 0f);
        agent.hasPendingSpawn = true;
        agent.nextCheckpointIdx = 0;
        agent.nextPlatformTarget = checkpoints[0].transform;
        agent.EndEpisode();
    }

    public void OnCheckpointHitSaboteur(CheckpointTriggerPF cp) {
        if (!episodeReady || !SaboteurActive) 
            return;

        if (cp.checkpointIndex != saboteur.nextCheckpointIdx) 
            return;

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


    // reconstrói o percurso inteiro do zero, usado tanto entre episódios de treino
    // como entre corridas reais no jogo (cada aposta gera um percurso novo, já que
    // o PlatformSpawnerPF tem aleatoriedade na altura e na posição das plataformas)
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