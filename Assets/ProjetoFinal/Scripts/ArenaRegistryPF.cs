using System.Collections.Generic;
using UnityEngine;

public class ArenaRegistryPF : MonoBehaviour {

    public static ArenaRegistryPF Instance;

    // guarda só as posições das arenas já registadas (usado para não duplicar e para o GetNearestArena)
    private List<Vector3> arenas = new List<Vector3>();

    public GameObject capsulePrefab; // o prefab do NPC apostador (tem o GamblerNPCLLMPF lá dentro)
    public Transform player;

    void Awake() {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // chamado pelo ChunkPF assim que ele acaba de construir uma arena no terreno
    // "position" é a posição do bloco de obsidiana (o "centro" da arena)
    // "capsuleSpawnPos" é onde o NPC vai aparecer (calculado no chunk)
    // e "chunkObject" é o GameObject do próprio chunk, para o NPC saber a quem pedir para remover a obsidiana
    public void RegisterArena(Vector3 position, Vector3 capsuleSpawnPos, GameObject chunkObject) {
        if (!arenas.Contains(position)) {
            arenas.Add(position);
            SpawnCapsule(capsuleSpawnPos, chunkObject);
        }
    }

    // cria o NPC e liga-lhe as duas coisas de que ele precisa para funcionar:
    // a transform do jogador (para saber quando está perto e abrir o diálogo)
    // e o gameobject do chunk (para no fim poder desbloquear/remover a obsidiana dessa arena específica)
    void SpawnCapsule(Vector3 spawnPos, GameObject chunkObject) {
        GameObject capsule = Instantiate(capsulePrefab, spawnPos, Quaternion.identity);

        GamblerNPCLLMPF gambler = capsule.GetComponent<GamblerNPCLLMPF>();
        gambler.playerTransform = player;
        gambler.voxelArenaObject = chunkObject;
    }


}