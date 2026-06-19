using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Linq;
using Unity.VisualScripting;
using UnityEngine;
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ChunkPF : MonoBehaviour {

    // ================= TERRENO / MESH =================
    public const int chunkSize = 16;               // Tamanho do chunk. Maior = mais detalhe por chunk, mais custo de geração.
    public BlockPF[,,] chunkData;                    // Grelha de blocos do chunk.
    private Material chunkMaterial;                 // Material aplicado ao mesh final.
    private Vector2Int worldOffset;                 // Posição do chunk no mundo, em coordenadas de chunk.
    private WorldManagerPF worldManager;              // Usado para consultar chunks vizinhos nas bordas.

    // ================= TERRENO BASE =================
    private float scale = 0.09f;                    // Escala do relevo principal. Menor = formas maiores e mais suaves; maior = relevo mais apertado.
    private int octaves = 6;                        // Número de camadas de ruído. Mais = relevo mais rico e irregular, mas mais caro.

    // ================= PLANÍCIES + MONTANHAS =================
    private float continentalnessScale = 0.05f;    // Tamanho das massas de terra. Menor = continentes enormes; maior = ilhas/regiões mais pequenas.
    private float continentalnessStrength = 1.35f;  // Quanto este layer puxa o terreno para cima. Maior = terreno globalmente mais alto.
    private int seaLevel = 5;                       // Altura média base do terreno. Menor = mapa mais baixo.
    private int maxHeight = 15;                     // Altura máxima alvo. Maior = picos mais altos.
    private float detailScale = 0.1f;               // Ruído fino para rugosidade da superfície. Maior = mais nervoso.
    private float detailAmplitude = 0.6f;           // Intensidade do ruído fino. Maior = superfície mais ondulada/irregular.
    private float heightPeakExponent = 1.35f;       // Expoente da curva de altura. Maior = picos mais raros e marcados, planícies mais largas.

    // ================= GRUTAS =================
    private float caveScale = 0.07f;                // Escala do ruído das grutas. Menor = grutas maiores e mais abertas.
    private float caveThreshold = 0.56f;            // Limiar para escavar. Menor = mais grutas; maior = menos grutas.
    private int margin = 4;                         // Protecção perto da superfície. Maior = grutas mais afastadas da camada exterior.

    // ================= TÚNEIS "WORM" =================
    private int wormSteps = 250;                     // Comprimento do túnel. Mais = túneis mais longos.
    private float wormRadius = 3.5f;                // Raio do túnel. Maior = túneis mais largos.
    private float wormStepSize = 3.0f;              // Distância por passo. Maior = túneis mais agressivos/menos suaves.
    private float wormDirectionScale = 0.45f;       // Velocidade com que a direcção muda. Maior = mais curvas.
    private float wormSpawnChance = 0.36f;          // Probabilidade de spawnar um túnel neste chunk. 0 = nunca, 1 = sempre.

    void Start() {
        //InitializeChunk();
        //DrawChunk();
    }

    public void Initialize(Vector2Int offset, Material mat, WorldManagerPF manager) {
        worldOffset = offset;
        chunkMaterial = mat;
        worldManager = manager;
        InitializeChunk();
        //DrawChunk();
    }

    void InitializeChunk() {
        chunkData = new BlockPF[chunkSize, chunkSize, chunkSize];
        int[,] surfaceHeight = new int[chunkSize, chunkSize]; // guardar altura da superfície
        int[,] biome = new int[chunkSize, chunkSize];// 0= normal 1= deserto 2= neve

        // determinar solid/ar com carving
        for (int x = 0; x < chunkSize; x++)
            for (int z = 0; z < chunkSize; z++) {
                float nx = worldOffset.x * chunkSize + x;
                float nz = worldOffset.y * chunkSize + z;

                // Layer 1: CONTINENTALNESS (escala enorme - define regiões)
                // Este layer decide "isto é oceano ou continente?"
                float continentalness = FBm(nx, nz, 2, continentalnessScale);

                float biomeNoise = FBm(nx, nz, 1, 0.005f);// a escala é muito pequena para os biomas grandes
                if (biomeNoise < 0.30)
                    biome[x, z] = 1;//deserto
                else if (biomeNoise > 0.70)
                    biome[x, z] = 2;//neve
                else
                    biome[x, z] = 0; //normal


                    // Layer 2: BASE HEIGHT (escala média - forma do terreno)
                    // Este layer define montanhas, vales, colinas dentro de cada região
                    float baseHeight = FBm(nx, nz, octaves, scale);
                baseHeight = Mathf.Pow(baseHeight, heightPeakExponent); // valor maior = cria montanhas mais raras e mais marcadas

                // Layer 3: DETAIL (escala pequena - rugosidade)
                // Este layer adiciona pedras, irregularidades, textura fina
                float detail = FBm(nx, nz, 6, detailScale);

                // COMBINAR OS 3 LAYERS:
                // continentalness controla a altura base (oceano vs montanha)
                // baseHeight modula dentro dessa faixa
                // detail adiciona +- alguns blocos de textura
                float finalHeight = Mathf.Lerp(seaLevel, maxHeight,
                    continentalness * continentalnessStrength * baseHeight)
                    + detail * detailAmplitude;

                for (int y = 0; y < chunkSize; y++) {
                    // Blend com density field para overhangs
                    float densityNoise = Perlin3D(nx * 0.1f, y * 0.1f, nz * 0.1f) * 2f - 1f;
                    float finalDensity = (finalHeight - y) + densityNoise;
                    bool solid = finalDensity > 0f;

                    BlockPF.BlockType type = solid ? BlockPF.BlockType.DIRT : BlockPF.BlockType.AIR;
                    chunkData[x, y, z] = new BlockPF(type, new Vector3(x, y, z));
                }
            }

        // guardar altura de superfície desta coluna
        for (int x = 0; x < chunkSize; x++)
            for (int z = 0; z < chunkSize; z++) {
                surfaceHeight[x, z] = 0;
                for (int y = chunkSize - 1; y >= 0; y--) {
                    if (chunkData[x, y, z].isSolid) {
                        surfaceHeight[x, z] = y;
                        break;
                    }
                }
            }

        // proteger superfície do carving
        for (int x = 0; x < chunkSize; x++)
            for (int z = 0; z < chunkSize; z++)
                for (int y = 0; y < chunkSize; y++) {
                    if (!chunkData[x, y, z].isSolid) 
                        continue;
                    if (y <= 1 || y >= surfaceHeight[x, z] - margin) 
                        continue;

                    float cx = (worldOffset.x * chunkSize + x) * caveScale;
                    float cy = y * caveScale;
                    float cz = (worldOffset.y * chunkSize + z) * caveScale;
                    float caveNoise = Perlin3D(cx, cy, cz);
                    if (caveNoise > caveThreshold)
                        chunkData[x, y, z] = new BlockPF(BlockPF.BlockType.AIR, new Vector3(x, y, z));
                }

        Vector3 wormStart = new Vector3(
            worldOffset.x * chunkSize + chunkSize / 2,
            chunkSize / 2,
            worldOffset.y * chunkSize + chunkSize / 2
        );

        if (Random.value < wormSpawnChance)
            CarveWorm(wormStart, wormSteps, wormRadius, wormStepSize, wormDirectionScale);

        // atribuir tipos de bloco
        for (int x = 0; x < chunkSize; x++)
            for (int y = 0; y < chunkSize; y++)
                for (int z = 0; z < chunkSize; z++) {
                    if (!chunkData[x, y, z].isSolid)
                        continue;

                    bool aboveIsAir = (y + 1 >= chunkSize) || !chunkData[x, y + 1, z].isSolid;
                    bool isSurface = aboveIsAir && y >= surfaceHeight[x, z] - margin;
                    bool isCaveWall = aboveIsAir && y < surfaceHeight[x, z] - margin;

                    BlockPF.BlockType type;

                    if (y <= 2)
                    {
                        type = BlockPF.BlockType.STONE;
                    }
                    else if (isCaveWall)
                    {
                        type = BlockPF.BlockType.STONE;
                    }
                    else if (isSurface)
                    {
                        // aqui aplicamos os biomas
                        switch (biome[x, z])
                        {
                            case 1: type = BlockPF.BlockType.SAND; break; // deserto
                            case 2: type = BlockPF.BlockType.SNOW; break; // neve
                            default: type = BlockPF.BlockType.GRASS; break; // normal
                        }
                    }
                    else
                    {
                        // subsuperfície também muda com o bioma
                        type = biome[x, z] == 1 ? BlockPF.BlockType.SAND : BlockPF.BlockType.DIRT;
                    }

                    chunkData[x, y, z] = new BlockPF(type, new Vector3(x, y, z));
                }

        // meter água e neve no bioma de neve
        for (int x = 0; x < chunkSize; x++)
            for (int y = 0; y < chunkSize; y++)
                for (int z = 0; z < chunkSize; z++)
                {
                    if (chunkData[x, y, z].type != BlockPF.BlockType.AIR) continue;
                    if (y >= seaLevel) continue;

                    // gelo no bioma neve, água nos outros
                    BlockPF.BlockType waterType = biome[x, z] == 2
                        ? BlockPF.BlockType.ICE
                        : BlockPF.BlockType.WATER;

                    chunkData[x, y, z] = new BlockPF(waterType, new Vector3(x, y, z));
                }

        //meter a ervinha com porbablidade de 30 porcento em cima do bloco erva, e o cacto no bioma de deserto
        for (int x = 0; x < chunkSize; x++)
            for (int z = 0; z < chunkSize; z++)
            {
                int y = surfaceHeight[x, z];
                if (y + 1 >= chunkSize) continue;

                switch (biome[x, z])
                {
                    case 0: // normal — erva
                        if (chunkData[x, y, z].type == BlockPF.BlockType.GRASS && Random.value < 0.01f)
                            chunkData[x, y + 1, z] = new BlockPF(BlockPF.BlockType.HERB, new Vector3(x, y + 1, z));
                        break;
                    case 1: // deserto — cactos (menos frequentes)
                        if (chunkData[x, y, z].type == BlockPF.BlockType.SAND && Random.value < 0.005f)
                            chunkData[x, y + 1, z] = new BlockPF(BlockPF.BlockType.CACTI, new Vector3(x, y + 1, z));
                        break;
                    case 2: // neve — sem vegetação
                        break;
                }
            }

        // --- Spawnar árvores na superfície (bioma normal) ---
        for (int x = 2; x < chunkSize - 2; x++)
            for (int z = 2; z < chunkSize - 2; z++) {
                if (biome[x, z] != 0) continue;
                if (Random.value > 0.05f) continue;

                int surfY = surfaceHeight[x, z];
                if (surfY <= 0) continue;
                if (chunkData[x, surfY, z].type != BlockPF.BlockType.GRASS) continue;

                PlaceTree(x, surfY, z);
            }

        // --- Tentar colocar arena de gambling ---
        TryPlaceArena();
    }

    void PlaceTree(int x, int baseY, int z) {
        int trunkHeight = 4;
        for (int i = 1; i <= trunkHeight; i++) {
            int y = baseY + i;
            if (y < chunkSize)
                chunkData[x, y, z] = new BlockPF(BlockPF.BlockType.WOOD,
                    new Vector3(worldOffset.x * chunkSize + x, y, worldOffset.y * chunkSize + z));
        }

        int treeTop = baseY + trunkHeight;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = 0; dy <= 2; dy++)
                for (int dz = -1; dz <= 1; dz++) {
                    int lx = x + dx, ly = treeTop + dy, lz = z + dz;
                    if (lx < 0 || lx >= chunkSize || ly < 0 || ly >= chunkSize || lz < 0 || lz >= chunkSize) continue;
                    if (chunkData[lx, ly, lz].type != BlockPF.BlockType.AIR) continue;
                    chunkData[lx, ly, lz] = new BlockPF(BlockPF.BlockType.LEAVES,
                        new Vector3(worldOffset.x * chunkSize + lx, ly, worldOffset.y * chunkSize + lz));
                }
    }

    void TryPlaceArena() {
        System.Random rng = new System.Random(worldOffset.x * 1000 + worldOffset.y);
        if (rng.NextDouble() > 0.15) return;

        for (int x = 2; x < chunkSize - 3; x++)
            for (int z = 2; z < chunkSize - 3; z++)
                for (int y = 2; y < 8; y++) {
                    if (IsSpaceClear(x, y, z, 5, 3, 5)) {
                        BuildArena(x, y, z);
                        return;
                    }
                }
    }

    bool IsSpaceClear(int startX, int startY, int startZ, int w, int h, int d) {
        for (int x = startX; x < startX + w; x++)
            for (int y = startY; y < startY + h; y++)
                for (int z = startZ; z < startZ + d; z++) {
                    if (x >= chunkSize || y >= chunkSize || z >= chunkSize) return false;
                    if (chunkData[x, y, z].type != BlockPF.BlockType.AIR) return false;
                }
        return true;
    }

    void BuildArena(int cx, int cy, int cz) {
        // chão
        for (int x = cx; x < cx + 5; x++)
            for (int z = cz; z < cz + 5; z++)
                SetIfInBounds(x, cy - 1, z, BlockPF.BlockType.STONE);

        // paredes
        for (int h = 0; h < 3; h++) {
            for (int x = cx; x < cx + 5; x++) {
                SetIfInBounds(x, cy + h, cz, BlockPF.BlockType.STONE);
                SetIfInBounds(x, cy + h, cz + 4, BlockPF.BlockType.STONE);
            }
            for (int z = cz; z < cz + 5; z++) {
                SetIfInBounds(cx, cy + h, z, BlockPF.BlockType.STONE);
                SetIfInBounds(cx + 4, cy + h, z, BlockPF.BlockType.STONE);
            }
        }

        // bloco central da arena (obsidiana = marca o NPC)
        int npcX = cx + 2, npcZ = cz + 2;
        SetIfInBounds(npcX, cy, npcZ, BlockPF.BlockType.OBSIDIAN);

        Vector3 worldPos = new Vector3(
            worldOffset.x * chunkSize + npcX,
            cy,
            worldOffset.y * chunkSize + npcZ);
        ArenaRegistryPF.Instance?.RegisterArena(worldPos);
    }

    void SetIfInBounds(int x, int y, int z, BlockPF.BlockType type) {
        if (x >= 0 && x < chunkSize && y >= 0 && y < chunkSize && z >= 0 && z < chunkSize)
            chunkData[x, y, z] = new BlockPF(type, new Vector3(x, y, z));
    }

    void CarveWorm(Vector3 start, int steps, float radius, float stepSize, float directionScale) {
        Vector3 pos = start;
        for (int i = 0; i < steps; i++) {
            float nx = Perlin3D(pos.x * directionScale,
                                pos.y * directionScale,
                                pos.z * directionScale) * 2f - 1f;
            float ny = Perlin3D(pos.y * directionScale + 100f,
                                pos.z * directionScale + 100f,
                                pos.x * directionScale + 100f) * 2f - 1f;
            float nz = Perlin3D(pos.z * directionScale + 200f,
                                pos.x * directionScale + 200f,
                                pos.y * directionScale + 200f) * 2f - 1f;

            Vector3 dir = new Vector3(nx, ny * 0.5f, nz).normalized;
            pos += dir * stepSize;

            CarveAt(pos, radius);
        }
    }

    void CarveAt(Vector3 center, float radius) {
        int localX = Mathf.RoundToInt(center.x) - worldOffset.x * chunkSize;
        int localY = Mathf.RoundToInt(center.y);
        int localZ = Mathf.RoundToInt(center.z) - worldOffset.y * chunkSize;
        int r = Mathf.CeilToInt(radius);

        for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
                for (int dz = -r; dz <= r; dz++) {
                    if (dx * dx + dy * dy + dz * dz > radius * radius) continue;

                    int bx = localX + dx;
                    int by = localY + dy;
                    int bz = localZ + dz;

                    if (bx >= 0 && bx < chunkSize &&
                        by > 1 && by < chunkSize - margin &&//protege a superfice para o wormm n sair para a superficie, para n cavar a superficie e n fazer buracos la 
                        bz >= 0 && bz < chunkSize)
                        chunkData[bx, by, bz] = new BlockPF(BlockPF.BlockType.AIR, new Vector3(bx, by, bz));
                }
    }

    public static float Perlin3D(float x, float y, float z) {
        float xy = Mathf.PerlinNoise(x, y);
        float yz = Mathf.PerlinNoise(y, z);
        float xz = Mathf.PerlinNoise(x, z);
        float yx = Mathf.PerlinNoise(y, x);
        float zy = Mathf.PerlinNoise(z, y);
        float zx = Mathf.PerlinNoise(z, x);
        return (xy + yz + xz + yx + zy + zx) / 6f;
    }

    public static float FBm(float x, float z, int octaves, float scale, float persistence = 0.5f, float lacunarity = 2.0f) {
        float value = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float totalAmplitude = 0f;

        for (int i = 0; i < octaves; i++) {
            value += Mathf.PerlinNoise(x * scale * frequency, z * scale * frequency) * amplitude;
            totalAmplitude += amplitude;
            amplitude *= persistence; // decresce a cada oitava
            frequency *= lacunarity; // cresce a cada oitava
        }

        return value / totalAmplitude; // normalizar para [0, 1]
    }


    public void DrawChunk() {
        // listas partilhadas por todos os blocos do chunk
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // listas só para colisão ou seja só blocos sólidos
        List<Vector3> colVerts = new List<Vector3>();
        List<int> colTris = new List<int>();
        List<Vector2> colUVs = new List<Vector2>();

        // percorremos todos os blocos e adicionamos as 6 faces
        for (int x = 0; x < chunkSize; x++)
            for (int y = 0; y < chunkSize; y++)
                for (int z = 0; z < chunkSize; z++) {
                    BlockPF block = chunkData[x, y, z];

                    /*if (!block.isSolid) continue;
                    *block.AddFaceToMeshData(Block.CubeFace.Front, vertices, triangles, uvs);
                    block.AddFaceToMeshData(Block.CubeFace.Back, vertices, triangles, uvs);
                    block.AddFaceToMeshData(Block.CubeFace.Top, vertices, triangles, uvs);
                    block.AddFaceToMeshData(Block.CubeFace.Bottom, vertices, triangles, uvs);
                    block.AddFaceToMeshData(Block.CubeFace.Left, vertices, triangles, uvs);
                    block.AddFaceToMeshData(Block.CubeFace.Right, vertices, triangles, uvs);*/

                    // Dentro do loop de blocos:
                    if (!block.isSolid) 
                        continue;
                    if (!HasSolidNeighbour(x, y, z + 1))
                        block.AddFaceToMeshData(BlockPF.CubeFace.Front, vertices, triangles, uvs);
                    if (!HasSolidNeighbour(x, y, z - 1))
                        block.AddFaceToMeshData(BlockPF.CubeFace.Back, vertices, triangles, uvs);
                    if (!HasSolidNeighbour(x, y + 1, z))
                        block.AddFaceToMeshData(BlockPF.CubeFace.Top, vertices, triangles, uvs);
                    if (!HasSolidNeighbour(x, y - 1, z))
                        block.AddFaceToMeshData(BlockPF.CubeFace.Bottom, vertices, triangles, uvs);
                    if (!HasSolidNeighbour(x - 1, y, z))
                        block.AddFaceToMeshData(BlockPF.CubeFace.Left, vertices, triangles, uvs);
                    if (!HasSolidNeighbour(x + 1, y, z))
                        block.AddFaceToMeshData(BlockPF.CubeFace.Right, vertices, triangles, uvs);

                    // colisão (cópia separada)
                    if (!HasSolidNeighbour(x, y, z + 1)) 
                        block.AddFaceToMeshData(BlockPF.CubeFace.Front, colVerts, colTris, colUVs);
                    if (!HasSolidNeighbour(x, y, z - 1)) 
                        block.AddFaceToMeshData(BlockPF.CubeFace.Back, colVerts, colTris, colUVs);
                    if (!HasSolidNeighbour(x, y + 1, z)) 
                        block.AddFaceToMeshData(BlockPF.CubeFace.Top, colVerts, colTris, colUVs);
                    if (!HasSolidNeighbour(x, y - 1, z)) 
                        block.AddFaceToMeshData(BlockPF.CubeFace.Bottom, colVerts, colTris, colUVs);
                    if (!HasSolidNeighbour(x - 1, y, z)) 
                        block.AddFaceToMeshData(BlockPF.CubeFace.Left, colVerts, colTris, colUVs);
                    if (!HasSolidNeighbour(x + 1, y, z)) 
                        block.AddFaceToMeshData(BlockPF.CubeFace.Right, colVerts, colTris, colUVs);
                }

        // loop água, só desenha faces contra o tipo AR
        for (int x = 0; x < chunkSize; x++)
            for (int y = 0; y < chunkSize; y++)
                for (int z = 0; z < chunkSize; z++) {
                    BlockPF block = chunkData[x, y, z];
                    if (block.type != BlockPF.BlockType.WATER) continue;

                    if (IsAir(x, y, z + 1)) block.AddFaceToMeshData(BlockPF.CubeFace.Front, vertices, triangles, uvs);
                    if (IsAir(x, y, z - 1)) block.AddFaceToMeshData(BlockPF.CubeFace.Back, vertices, triangles, uvs);
                    if (IsAir(x, y + 1, z)) block.AddFaceToMeshData(BlockPF.CubeFace.Top, vertices, triangles, uvs);
                    if (IsAir(x, y - 1, z)) block.AddFaceToMeshData(BlockPF.CubeFace.Bottom, vertices, triangles, uvs);
                    if (IsAir(x - 1, y, z)) block.AddFaceToMeshData(BlockPF.CubeFace.Left, vertices, triangles, uvs);
                    if (IsAir(x + 1, y, z)) block.AddFaceToMeshData(BlockPF.CubeFace.Right, vertices, triangles, uvs);
                }
        // loop folhas (LEAVES, são semi-transparentes e renderiza todas as faces)
        for (int x = 0; x < chunkSize; x++)
            for (int y = 0; y < chunkSize; y++)
                for (int z = 0; z < chunkSize; z++) {
                    if (chunkData[x, y, z].type != BlockPF.BlockType.LEAVES) continue;
                    BlockPF block = chunkData[x, y, z];
                    block.AddFaceToMeshData(BlockPF.CubeFace.Front, vertices, triangles, uvs);
                    block.AddFaceToMeshData(BlockPF.CubeFace.Back, vertices, triangles, uvs);
                    block.AddFaceToMeshData(BlockPF.CubeFace.Top, vertices, triangles, uvs);
                    block.AddFaceToMeshData(BlockPF.CubeFace.Bottom, vertices, triangles, uvs);
                    block.AddFaceToMeshData(BlockPF.CubeFace.Left, vertices, triangles, uvs);
                    block.AddFaceToMeshData(BlockPF.CubeFace.Right, vertices, triangles, uvs);
                }

        // Loop erva 
        for (int x = 0; x < chunkSize; x++)
            for (int y = 0; y < chunkSize; y++)
                for (int z = 0; z < chunkSize; z++) {
                    if (chunkData[x, y, z].type != BlockPF.BlockType.HERB) continue;
                    BlockPF block = chunkData[x, y, z];
                    block.AddFaceToMeshData(BlockPF.CubeFace.Front, vertices, triangles, uvs);
                    block.AddFaceToMeshData(BlockPF.CubeFace.Back, vertices, triangles, uvs);
                }

        // criamos a mesh de render e atribuímos os arrays
        Mesh mesh = new Mesh();
        // o problema é o limite do index buffer, por defeito, o Unity usa índices de 16 bits, que suporta no máximo 65.535 vértices
        // um chunk 16*16*16 sem face culling gera 4.096 blocos × 6 faces × 4 vértices = 98.304 vértices
        // ou seja ultrapassa o limite e os blocos a mais ficam cortados
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();

        // calcular normais e bounds automaticamente
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // mesh de colisão (só sólidos)
        Mesh colMesh = new Mesh();
        colMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        colMesh.vertices = colVerts.ToArray();
        colMesh.triangles = colTris.ToArray();
        colMesh.RecalculateBounds();
        MeshCollider col = GetComponent<MeshCollider>();
        if (col == null) col = gameObject.AddComponent<MeshCollider>();
        col.sharedMesh = colMesh;

        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshRenderer>().material = chunkMaterial;
    }

    bool IsAir(int x, int y, int z) {
        if (y < 0 || y >= chunkSize) return true;

        if (x < 0) {
            ChunkPF n = worldManager?.GetChunk(worldOffset + Vector2Int.left);
            return n == null || n.chunkData[chunkSize - 1, y, z].type == BlockPF.BlockType.AIR;
        }
        if (x >= chunkSize) {
            ChunkPF n = worldManager?.GetChunk(worldOffset + Vector2Int.right);
            return n == null || n.chunkData[0, y, z].type == BlockPF.BlockType.AIR;
        }
        if (z < 0) {
            ChunkPF n = worldManager?.GetChunk(worldOffset + Vector2Int.down);
            return n == null || n.chunkData[x, y, chunkSize - 1].type == BlockPF.BlockType.AIR;
        }
        if (z >= chunkSize) {
            ChunkPF n = worldManager?.GetChunk(worldOffset + Vector2Int.up);
            return n == null || n.chunkData[x, y, 0].type == BlockPF.BlockType.AIR;
        }

        return chunkData[x, y, z].type == BlockPF.BlockType.AIR;
    }

    /*bool HasSolidNeighbour(int x, int y, int z) {
        // TODO:
        // Se (x,y,z) está fora dos limites do chunk → return ???
        // Senão → return chunkData[x, y, z].???


        // Se está fora dos limites do chunk → não há vizinho sólido, desenha a face
        if (x < 0 || x >= chunkSize ||
            y < 0 || y >= chunkSize ||
            z < 0 || z >= chunkSize)
            return false;

        return chunkData[x, y, z].isSolid;
    }*/

    bool HasSolidNeighbour(int x, int y, int z) {
        if (y < 0 || y >= chunkSize) return false;

        if (x < 0) {
            ChunkPF neighbour = worldManager?.GetChunk(worldOffset + Vector2Int.left);
            return neighbour != null && neighbour.chunkData[chunkSize - 1, y, z].isSolid;
        }
        if (x >= chunkSize) {
            ChunkPF neighbour = worldManager?.GetChunk(worldOffset + Vector2Int.right);
            return neighbour != null && neighbour.chunkData[0, y, z].isSolid;
        }
        if (z < 0) {
            ChunkPF neighbour = worldManager?.GetChunk(worldOffset + Vector2Int.down);
            return neighbour != null && neighbour.chunkData[x, y, chunkSize - 1].isSolid;
        }
        if (z >= chunkSize) {
            ChunkPF neighbour = worldManager?.GetChunk(worldOffset + Vector2Int.up);
            return neighbour != null && neighbour.chunkData[x, y, 0].isSolid;
        }

        return chunkData[x, y, z].isSolid;
    }
}