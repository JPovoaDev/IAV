using UnityEngine;

public class BlockInteraction : MonoBehaviour {

    public WorldManager worldManager;
    public float maxDistance = 6f;
    public Transform highlightCube;

    private Block.BlockType[] palette = {
        Block.BlockType.DIRT,
        Block.BlockType.STONE,
        Block.BlockType.GRASS
    };
    private int currentIndex = 0;
    private Block.BlockType placeType => palette[currentIndex];

    void Update() {
        HandleHighlight();
        HandleScroll();
        if (Input.GetMouseButtonDown(0)) BreakBlock();
        if (Input.GetMouseButtonDown(1)) PlaceBlock();
    }

    void HandleHighlight() {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance)) {
            Vector3 center = hit.point - hit.normal * 0.5f;
            highlightCube.position = new Vector3(
                Mathf.RoundToInt(center.x),
                Mathf.RoundToInt(center.y),
                Mathf.RoundToInt(center.z));
            highlightCube.gameObject.SetActive(true);
        } else {
            highlightCube.gameObject.SetActive(false);
        }
    }

    void HandleScroll() {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f) currentIndex = (currentIndex + 1) % palette.Length;
        if (scroll < 0f) currentIndex = (currentIndex - 1 + palette.Length) % palette.Length;
    }

    void BreakBlock() {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
            ModifyBlock(hit.point - hit.normal * 0.5f, Block.BlockType.AIR);
    }

    void PlaceBlock() {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
            ModifyBlock(hit.point + hit.normal * 0.5f, placeType);
    }

    void ModifyBlock(Vector3 worldPos, Block.BlockType type) {
        int cs = Chunk.chunkSize;

        int bx = Mathf.RoundToInt(worldPos.x);
        int by = Mathf.RoundToInt(worldPos.y);
        int bz = Mathf.RoundToInt(worldPos.z);

        if (by < 0 || by >= cs) return;

        Vector2Int chunkCoord = new Vector2Int(
            Mathf.FloorToInt((float)bx / cs),
            Mathf.FloorToInt((float)bz / cs));

        Chunk chunk = worldManager.GetChunk(chunkCoord);
        if (chunk == null) return;

        int localX = bx - chunkCoord.x * cs;
        int localY = by;
        int localZ = bz - chunkCoord.y * cs;

        if (localX < 0 || localX >= cs ||
            localY < 0 || localY >= cs ||
            localZ < 0 || localZ >= cs) return;

        Block block = chunk.chunkData[localX, localY, localZ];
        block.type = type;
        block.isSolid = (type != Block.BlockType.AIR && type != Block.BlockType.WATER);

        chunk.DrawChunk();

        if (localX == 0) RedrawNeighbour(chunkCoord + Vector2Int.left);
        if (localX == cs - 1) RedrawNeighbour(chunkCoord + Vector2Int.right);
        if (localZ == 0) RedrawNeighbour(chunkCoord + Vector2Int.down);
        if (localZ == cs - 1) RedrawNeighbour(chunkCoord + Vector2Int.up);
    }

    void RedrawNeighbour(Vector2Int coord) {
        Chunk c = worldManager.GetChunk(coord);
        if (c != null) c.DrawChunk();
    }
}