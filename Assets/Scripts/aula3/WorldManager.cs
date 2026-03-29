using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldManager : MonoBehaviour {

    [Header("Referências")]
    public Transform player;
    public GameObject chunkPrefab;
    public Material chunkMaterial;

    [Header("Configuração")]
    private int renderDistance = 6;

    private Dictionary<Vector2Int, GameObject> activeChunks = new();
    private Vector2Int lastPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);

    private int chunksPerFrame = 2;
    private Coroutine buildRoutine;

    void Start() {
        UpdateChunks();
    }

    void Update() {
        Vector2Int current = GetPlayerChunk();
        /*if (current != lastPlayerChunk) {
            lastPlayerChunk = current;
            UpdateChunks();
        }*/

        if (current != lastPlayerChunk) {
            lastPlayerChunk = current;

            if (buildRoutine != null)
                StopCoroutine(buildRoutine);

            RemoveDistantChunks(current);
            buildRoutine = StartCoroutine(BuildChunks(GetNeededChunks(current)));
        }
    }

    public Chunk GetChunk(Vector2Int coord) {
        if (activeChunks.TryGetValue(coord, out GameObject go))
            return go.GetComponent<Chunk>();
        return null;
    }

    IEnumerator BuildChunks(HashSet<Vector2Int> needed) {
        List<Chunk> newChunks = new List<Chunk>();
        int count = 0;

        foreach (var coord in needed) {
            if (!activeChunks.ContainsKey(coord)) {
                Chunk c = SpawnChunk(coord);
                
                newChunks.Add(c);
                //SpawnChunk(coord);
                count++;
                if (count % chunksPerFrame == 0)
                    yield return null; // pausa até ao próximo frame
                // o yield pausa a corrotina e da o controlo ao unity ate o proximo frame, o problema aqui é que enqunato a corrotina esta pausada o jogador pode se mover e o 
                //update cancela o update ent os dados que foram spawnados mas parados e nunca chegam a ser desenhados. A solucoa era meter o yeild depois do drawChunks pk mesmo qeu 
                // a corotina pare pelo menos os chunks sao desenhados.
            }
        }
        
        foreach (var chunk in newChunks)
            chunk.DrawChunk();
    }

    HashSet<Vector2Int> GetNeededChunks(Vector2Int center) {
        HashSet<Vector2Int> needed = new HashSet<Vector2Int>();
        for (int cx = -renderDistance; cx <= renderDistance; cx++)
            for (int cz = -renderDistance; cz <= renderDistance; cz++)
                needed.Add(new Vector2Int(center.x + cx, center.y + cz));
        return needed;
    }

    void RemoveDistantChunks(Vector2Int center) {
        HashSet<Vector2Int> needed = GetNeededChunks(center);
        List<Vector2Int> toRemove = new List<Vector2Int>();
        foreach (var key in activeChunks.Keys)
            if (!needed.Contains(key))
                toRemove.Add(key);
        foreach (var key in toRemove) {
            Destroy(activeChunks[key]);
            activeChunks.Remove(key);
        }
    }

    Vector2Int GetPlayerChunk() {
        Vector3 pos = player.position;
        return new Vector2Int(
            Mathf.FloorToInt(pos.x / Chunk.chunkSize),
            Mathf.FloorToInt(pos.z / Chunk.chunkSize));
    }

    void UpdateChunks() {
        Vector2Int center = GetPlayerChunk();

        HashSet<Vector2Int> needed = new HashSet<Vector2Int>();
        for (int cx = -renderDistance; cx <= renderDistance; cx++)
            for (int cz = -renderDistance; cz <= renderDistance; cz++)
                needed.Add(new Vector2Int(center.x + cx, center.y + cz));

        List<Vector2Int> toRemove = new List<Vector2Int>();
        foreach (var key in activeChunks.Keys)
            if (!needed.Contains(key))
                toRemove.Add(key);

        foreach (var key in toRemove) {
            Destroy(activeChunks[key]);
            activeChunks.Remove(key);
        }

        /*foreach (var coord in needed)
            if (!activeChunks.ContainsKey(coord))
                SpawnChunk(coord);*/

        List<Chunk> newChunks = new List<Chunk>();
        foreach (var coord in needed) {
            if (!activeChunks.ContainsKey(coord)) {
                newChunks.Add(SpawnChunk(coord));
            }
        }

        foreach (var chunk in newChunks)
            chunk.DrawChunk();
    }

    Chunk SpawnChunk(Vector2Int coord) {
        Vector3 worldPos = new Vector3(coord.x * Chunk.chunkSize, 0, coord.y * Chunk.chunkSize);
        GameObject go = Instantiate(chunkPrefab, worldPos, Quaternion.identity);
        Chunk chunk = go.GetComponent<Chunk>();
        //chunk.Initialize(coord, chunkMaterial);
        chunk.Initialize(coord, chunkMaterial, this);
        activeChunks[coord] = go;
        return chunk;
    }
}