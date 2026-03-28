using System.Collections.Generic;
using UnityEngine;
public class BlockDebugger : MonoBehaviour {
    [SerializeField] private Material material;
    void Start() {
        Block block1 = new Block(Block.BlockType.STONE, Vector3.zero);
        Block block2 = new Block(Block.BlockType.GRASS, new Vector3(1, 0, 0));
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        // Adicionar as 6 faces
        block1.AddFaceToMeshData(Block.CubeFace.Front, vertices, triangles, uvs);
        block1.AddFaceToMeshData(Block.CubeFace.Back, vertices, triangles, uvs);
        block1.AddFaceToMeshData(Block.CubeFace.Top, vertices, triangles, uvs);
        block1.AddFaceToMeshData(Block.CubeFace.Bottom, vertices, triangles, uvs);
        block1.AddFaceToMeshData(Block.CubeFace.Left, vertices, triangles, uvs);
        block1.AddFaceToMeshData(Block.CubeFace.Right, vertices, triangles, uvs);
        // Adicionar as 6 faces
        block2.AddFaceToMeshData(Block.CubeFace.Front, vertices, triangles, uvs);
        block2.AddFaceToMeshData(Block.CubeFace.Back, vertices, triangles, uvs);
        block2.AddFaceToMeshData(Block.CubeFace.Top, vertices, triangles, uvs);
        block2.AddFaceToMeshData(Block.CubeFace.Bottom, vertices, triangles, uvs);
        block2.AddFaceToMeshData(Block.CubeFace.Left, vertices, triangles, uvs);
        block2.AddFaceToMeshData(Block.CubeFace.Right, vertices, triangles, uvs);
        // Construir a mesh
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        gameObject.AddComponent<MeshFilter>().mesh = mesh;
        gameObject.AddComponent<MeshRenderer>().material = material;
    }
}