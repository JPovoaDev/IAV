using System.Collections.Generic;
using UnityEngine;

public class ArenaRegistryPF : MonoBehaviour {
    public static ArenaRegistryPF Instance;
    private List<Vector3> arenas = new List<Vector3>();
    public GameObject capsulePrefab;
    public Transform player;

    void Awake() {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void RegisterArena(Vector3 position, Vector3 capsuleSpawnPos, GameObject chunkObject) {
        if (!arenas.Contains(position)) {
            arenas.Add(position);
            SpawnCapsule(capsuleSpawnPos, chunkObject);
        }
    }

    void SpawnCapsule(Vector3 spawnPos, GameObject chunkObject) {
        GameObject capsule = Instantiate(capsulePrefab, spawnPos, Quaternion.identity);

        GamblerNPCLLMPF gambler = capsule.GetComponent< GamblerNPCLLMPF > ();
        gambler.playerTransform = player;
        gambler.voxelArenaObject = chunkObject;
    }

    public Vector3? GetNearestArena(Vector3 from) {
        if (arenas.Count == 0) return null;
        Vector3 nearest = arenas[0];
        float minDist = Vector3.Distance(from, nearest);
        foreach (var a in arenas) {
            float d = Vector3.Distance(from, a);
            if (d < minDist) { minDist = d; nearest = a; }
        }
        return nearest;
    }
}