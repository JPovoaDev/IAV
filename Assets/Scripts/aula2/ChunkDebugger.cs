using UnityEngine;
public class ChunkDebugger : MonoBehaviour {
    [SerializeField] private Material chunkMaterial;
    /*void Start() {
        GameObject go = new GameObject("Chunk_0_0_0");
        go.transform.position = Vector3.zero;
        Chunk chunk = go.AddComponent<Chunk>();
        chunk.chunkMaterial = chunkMaterial;
    }*/

    void Start() {
        CreateChunk(new Vector2Int(0, 0));
        CreateChunk(new Vector2Int(1, 0));
    }

    void CreateChunk(Vector2Int offset) {
        GameObject go = new GameObject($"Chunk_{offset.x}_{offset.y}");
        go.transform.position = new Vector3(offset.x * Chunk.chunkSize, 0, offset.y * Chunk.chunkSize);
        Chunk chunk = go.AddComponent<Chunk>();
        //chunk.Initialize(offset, chunkMaterial);
    }
}
