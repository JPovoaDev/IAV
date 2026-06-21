using System.Collections.Generic;
using UnityEngine;

public class BlockPF {

    public enum BlockType { GRASS, DIRT, STONE, AIR, WATER, HERB, SAND, SNOW, ICE, CACTI, WOOD, LEAVES, OBSIDIAN }
    public BlockType type;

    public enum CubeFace { Front, Back, Top, Bottom, Left, Right }
    public Vector3 position;
    public bool isSolid;

    // os 8 vértices de um cubo unitário centrado na origem, partilhados por todos os blocos
    // são static readonly porque são sempre iguais, não faz sentido alocar 8 Vector3 por
    // instância quando podem ser partilhados sem custo entre todos os blocos do mundo
    static readonly Vector3 v0 = new Vector3(-0.5f, -0.5f, 0.5f);
    static readonly Vector3 v1 = new Vector3(0.5f, -0.5f, 0.5f);
    static readonly Vector3 v2 = new Vector3(0.5f, -0.5f, -0.5f);
    static readonly Vector3 v3 = new Vector3(-0.5f, -0.5f, -0.5f);
    static readonly Vector3 v4 = new Vector3(-0.5f, 0.5f, 0.5f);
    static readonly Vector3 v5 = new Vector3(0.5f, 0.5f, 0.5f);
    static readonly Vector3 v6 = new Vector3(0.5f, 0.5f, -0.5f);
    static readonly Vector3 v7 = new Vector3(-0.5f, 0.5f, -0.5f);

    public BlockPF(BlockType type, Vector3 position) {
        this.type = type;
        this.position = position;
        // HERB e LEAVES são tratados como não-sólidos para que os blocos adjacentes
        // desenhem as suas faces ao lado deles, caso contrário o chunk "escondia" essas
        // faces e as ervas e folhas ficariam invisíveis por dentro de outros blocos
        isSolid = (type != BlockType.AIR && type != BlockType.WATER && type != BlockType.HERB && type != BlockType.LEAVES);
    }

    // chamado pelo ChunkPF para cada face visível de cada bloco, adiciona os dados
    // desta face às listas globais da malha do chunk —> toda a geometria do chunk fica
    // numa única Mesh para minimizar draw calls (um chunk = um mesh = um draw call)
    public void AddFaceToMeshData(CubeFace face,
        List<Vector3> vertices, List<int> triangles, List<Vector2> uvs) {
        // guarda o número atual de vértices na lista, serve de offset para os índices
        // dos triângulos deste bloco, que referenciam os seus 4 vértices na lista global
        int vertexIndex = vertices.Count;

        // seleciona os 4 vértices desta face pela ordem correta para que as normais
        // fiquem a apontar para fora do cubo (winding order no sentido dos ponteiros do relógio)
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

        // soma a posição do bloco a cada vértice local para os colocar no sítio certo
        // dentro da malha do chunk (que usa coordenadas locais relativas ao chunk)
        foreach (Vector3 v in faceVertices)
            vertices.Add(v + position);

        // UVs a partir do atlas de texturas onde cada tipo de bloco (e às vezes cada face) tem a sua própria região no atlas, calculada em GetUVs
        Vector2[] faceUVs = GetUVs(face, type);
        uvs.Add(faceUVs[0]);
        uvs.Add(faceUVs[1]);
        uvs.Add(faceUVs[2]);
        uvs.Add(faceUVs[3]);

        // dois triângulos por face, com o offset adicionado para referenciar os vértices certos dentro da lista global
        // sem o offset, todos os blocos referenciariam os índices 0-3 em vez dos seus próprios vértices
        triangles.Add(vertexIndex + 3);
        triangles.Add(vertexIndex + 1);
        triangles.Add(vertexIndex + 0);
        triangles.Add(vertexIndex + 3);
        triangles.Add(vertexIndex + 2);
        triangles.Add(vertexIndex + 1);
    }

    // calcula as coordenadas UV de uma face no atlas de texturas 16x16
    // cada textura ocupa exatamente 1/16 do atlas em cada eixo
    // é o canto inferior-esquerdo da textura deste tipo de bloco no atlas, em coordenadas UV
    public static Vector2[] GetUVs(CubeFace face, BlockType type) {
        Vector2 lbc;

        // GRASS é o único tipo com texturas diferentes por face:
        // topo verde, fundo terra pura, lados com a transição entre os dois
        if (type == BlockType.GRASS) {
            if (face == CubeFace.Top)
                lbc = new Vector2(2f, 6f) / 16;
            else if (face == CubeFace.Bottom)
                lbc = new Vector2(2f, 15f) / 16;
            else
                lbc = new Vector2(3f, 15f) / 16;

        } else if (type == BlockType.DIRT) lbc = new Vector2(2f, 15f) / 16;
        else if (type == BlockType.WATER) lbc = new Vector2(14f, 2f) / 16;
        else if (type == BlockType.HERB) lbc = new Vector2(10f, 10f) / 16f;
        else if (type == BlockType.SAND) lbc = new Vector2(0f, 4f) / 16f;
        else if (type == BlockType.SNOW) lbc = new Vector2(2f, 11f) / 16f;
        else if (type == BlockType.ICE) lbc = new Vector2(3f, 11f) / 16f;
        else if (type == BlockType.CACTI) lbc = new Vector2(6f, 11f) / 16f;
        else if (type == BlockType.WOOD) lbc = new Vector2(4f, 14f) / 16;
        else if (type == BlockType.LEAVES) lbc = new Vector2(2f, 6f) / 16;
        else if (type == BlockType.OBSIDIAN) lbc = new Vector2(5f, 13f) / 16;
        else lbc = new Vector2(0f, 14f) / 16; // STONE e fallback

        // calcula os 4 cantos da região do atlas a partir do canto inferior-esquerdo
        // cada textura ocupa 1/16 do espaço total em cada eixo
        Vector2 uv00 = lbc;                               // inferior-esquerdo
        Vector2 uv10 = lbc + new Vector2(1f, 0f) / 16;   // inferior-direito
        Vector2 uv01 = lbc + new Vector2(0f, 1f) / 16;   // superior-esquerdo
        Vector2 uv11 = lbc + new Vector2(1f, 1f) / 16;   // superior-direito

        // a ordem tem de coincidir com a ordem dos vértices definida em AddFaceToMeshData
        // (v4, v5, v1, v0 para a face Front, etc.) ou as texturas ficam rodadas ou espelhadas
        return new[] { uv11, uv01, uv00, uv10 };
    }
}