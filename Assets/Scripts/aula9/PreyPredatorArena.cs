using System.Collections;
using UnityEngine;

public class PreyPredatorArena : MonoBehaviour {
    [Header("Agentes")]
    public PreyAgent prey;
    public PredatorAgent predator;

    [Header("Limites")]
    public int maxEpisodeSteps = 2000;
    public float arenaHalfSize = 4.5f;

    [Header("Feedback visual (opcional)")]
    public MeshRenderer floorRenderer;
    public Material defaultMaterial;
    public Material predatorWinMaterial;
    public Material preyWinMaterial;

    private int stepCount;

    public void StartEpisode() {
        stepCount = 0;

        float spawnX = Random.Range(-arenaHalfSize, arenaHalfSize);

        prey.Place(new Vector3(spawnX, 0f, arenaHalfSize * 0.6f));
        predator.Place(new Vector3(spawnX, 0f, -arenaHalfSize * 0.6f));

        floorRenderer.material = defaultMaterial;
    }

    private void FixedUpdate() {
        stepCount++;

        if (stepCount >= maxEpisodeSteps) {
            // Timeout — presa sobreviveu, presa ganha
            prey.AddReward(1f);
            predator.AddReward(-1f);
            floorRenderer.material = preyWinMaterial;

            StartCoroutine(DelayedReset());
        }
    }

    public void OnPreyCaptured() {
        // Predador tocou na presa
        Debug.Log("OnPreyCaptured chamado");
        predator.AddReward(1f);
        prey.AddReward(-1f);

        floorRenderer.material = predatorWinMaterial;

        StartCoroutine(DelayedReset());
    }

    private IEnumerator DelayedReset() {
        yield return new WaitForSeconds(0.5f);
        EndAndReset();
    }

    private void EndAndReset() {
        prey.EndEpisode();
        predator.EndEpisode();
    }
}