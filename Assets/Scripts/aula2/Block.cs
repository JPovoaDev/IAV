using System.Collections.Generic;
using UnityEngine;
public class Block {

    public enum BlockType { GRASS, DIRT, STONE, AIR, WATER }
    public BlockType type;


    public enum CubeFace { Front, Back, Top, Bottom, Left, Right }
    public Vector3 position;
    public bool isSolid;
    // Os 8 vertices (mesmos da aula01)
    static readonly Vector3 v0 = new Vector3(-0.5f, -0.5f, 0.5f);
    static readonly Vector3 v1 = new Vector3(0.5f, -0.5f, 0.5f);
    static readonly Vector3 v2 = new Vector3(0.5f, -0.5f, -0.5f);
    static readonly Vector3 v3 = new Vector3(-0.5f, -0.5f, -0.5f);
    static readonly Vector3 v4 = new Vector3(-0.5f, 0.5f, 0.5f);
    static readonly Vector3 v5 = new Vector3(0.5f, 0.5f, 0.5f);
    static readonly Vector3 v6 = new Vector3(0.5f, 0.5f, -0.5f);
    static readonly Vector3 v7 = new Vector3(-0.5f, 0.5f, -0.5f);
    public Block(BlockType type, Vector3 position) {
        this.type = type;
        this.position = position;
        isSolid = (type != BlockType.AIR && type != BlockType.WATER);
    }
    // TODO: implementar
    public void AddFaceToMeshData(CubeFace face,
        List<Vector3> vertices, List<int> triangles, List<Vector2> uvs) {
        // 1. Guardar o índice actual — serve de offset para os triângulos deste bloco
        int vertexIndex = vertices.Count;

        // 2. Obter os 4 vértices da face (tabela do guião)
        Vector3[] faceVertices;
        switch (face) {
            case CubeFace.Front: faceVertices = new[] { v4, v5, v1, v0 }; break;
            case CubeFace.Back: faceVertices = new[] { v6, v7, v3, v2 }; break;
            case CubeFace.Top: faceVertices = new[] { v7, v6, v5, v4 }; break;
            case CubeFace.Bottom: faceVertices = new[] { v0, v1, v2, v3 }; break;
            case CubeFace.Left: faceVertices = new[] { v7, v4, v0, v3 }; break;
            case CubeFace.Right: faceVertices = new[] { v5, v6, v2, v1 }; break;
            default: return;
        }

        // 3. Somar a posição do bloco a cada vértice e adicionar à lista
        foreach (Vector3 v in faceVertices)
            vertices.Add(v + position);

        // 4. UVs — textura inteira em cada face
        /*uvs.Add(new Vector2(0, 1)); // superior-esquerdo
        uvs.Add(new Vector2(1, 1)); // superior-direito
        uvs.Add(new Vector2(1, 0)); // inferior-direito
        uvs.Add(new Vector2(0, 0)); // inferior-esquerdo*/
        Vector2[] faceUVs = GetUVs(face, type);
        uvs.Add(faceUVs[0]);
        uvs.Add(faceUVs[1]);
        uvs.Add(faceUVs[2]);
        uvs.Add(faceUVs[3]);

        // 5. Triângulos COM OFFSET — os índices referenciam os vértices certos na lista global
        triangles.Add(vertexIndex + 3);
        triangles.Add(vertexIndex + 1);
        triangles.Add(vertexIndex + 0);
        triangles.Add(vertexIndex + 3);
        triangles.Add(vertexIndex + 2);
        triangles.Add(vertexIndex + 1);
    }

    public static Vector2[] GetUVs(CubeFace face, BlockType type) {
        // Canto inferior-esquerdo de cada textura no atlas (coluna, linha) / 16
        Vector2 lbc;
        if (type == BlockType.GRASS) {
            if (face == CubeFace.Top) 
                lbc = new Vector2(2f, 6f) / 16;
            else if 
                (face == CubeFace.Bottom) lbc = new Vector2(2f, 15f) / 16;
            else 
                lbc = new Vector2(3f, 15f) / 16;

        } else if 
            (type == BlockType.DIRT) lbc = new Vector2(2f, 15f) / 16;
        else if (type == BlockType.WATER)
            lbc = new Vector2(14f, 2f) / 16;
        else 
            lbc = new Vector2(0f, 14f) / 16;
        Vector2 uv00 = lbc; // inferior-esquerdo
        Vector2 uv10 = lbc + new Vector2(1f, 0f) / 16; // inferior-direito
        Vector2 uv01 = lbc + new Vector2(0f, 1f) / 16; // superior-esquerdo
        Vector2 uv11 = lbc + new Vector2(1f, 1f) / 16; // superior-direito
        return new[] { uv11, uv01, uv00, uv10 };
    }
}