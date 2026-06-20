using UnityEngine;
using System.Collections.Generic;

public class BlockInteractionPF : MonoBehaviour {

    public WorldManagerPF worldManager;
    public float maxDistance = 6f;// distancia que o raycast alcanca, quanto maior conseguimos alcancar os blocos mais longe
    public Transform highlightCube;// é o cubo wireframe que aparece a volta do cubo que estamos a apontar

    private Dictionary<Vector3Int, int> blockDamage = new Dictionary<Vector3Int, int>();
    private int hitsToBreak = 3;

    public static bool obsidianUnlocked = false;

    private BlockPF.BlockType[] palette = { // lista de blocos que podemos colocar 
        BlockPF.BlockType.DIRT,
        BlockPF.BlockType.STONE,
        BlockPF.BlockType.GRASS,
        BlockPF.BlockType.SAND,
        BlockPF.BlockType.SNOW,
        BlockPF.BlockType.WOOD,
        BlockPF.BlockType.LEAVES,
        BlockPF.BlockType.OBSIDIAN
    };
    private int currentIndex = 0; // o indice indica qual bloco esta selecionado, ou seja, no indice 0 colocamos o bloco DIRT
    private BlockPF.BlockType placeType => palette[currentIndex]; // playce typ eé uma propriedade que devolve sempre o bloco com o index que vamos colocar

    void Update() {
        HandleHighlight();// atulaiza o bloco que fica highlight
        HandleScroll();// muda o bloco selecionado
        if (Input.GetMouseButtonDown(0)) BreakBlock();// comm o botao direito partimos com o esquerdo colocamos o bloco 
        if (Input.GetMouseButtonDown(1)) PlaceBlock();
    }

    void HandleHighlight() {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);// criamos um raycast para onde a camara esta a olhar 
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance)) {
            Vector3 center = hit.point - hit.normal * 0.5f;//hit.point é o ponto exato que o raycast colidiu contra um box colider. O hit.Normal é o vetor que aponta para cima(fora do bloco)
            // do ponto que apanhou, e o 0.5. ent fazemos -normal para apontar para dentro do bloco e mutliplicamos por 0.5 para ficar dentro do bloco. sem isto se so ussassemos o hit.point e 
            //depois o round poderia arredondar para o bloco errado.o - hit.normal * 0.5f garante que fica no bloco certo.
            highlightCube.position = new Vector3(
                Mathf.RoundToInt(center.x),
                Mathf.RoundToInt(center.y),
                Mathf.RoundToInt(center.z));// estmaos a alinhar o ponto na grelha, arredondando
            highlightCube.gameObject.SetActive(true);//e ativa o highlight
        } else {
            highlightCube.gameObject.SetActive(false);// se o raio n acertou em nada fica desativo
        }
    }


    void Start() {
        InventoryManagerPF.Instance.SetHighlight(placeType);
    }

    void HandleScroll() {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll == 0f) return;
        int dir = scroll > 0f ? 1 : -1;
        int start = currentIndex;
        do {
            currentIndex = (currentIndex + dir + palette.Length) % palette.Length;
        } while (InventoryManagerPF.Instance.Count(palette[currentIndex]) <= 0
                 && currentIndex != start);
        InventoryManagerPF.Instance.SetHighlight(placeType);
    }


    void BreakBlock() {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance)) return;

        Vector3 center = hit.point - hit.normal * 0.5f;
        int bx = Mathf.RoundToInt(center.x);
        int by = Mathf.RoundToInt(center.y);
        int bz = Mathf.RoundToInt(center.z);

        BlockPF.BlockType GetBlockType(int bx, int by, int bz) {
            int cs = ChunkPF.chunkSize;
            Vector2Int chunkCoord = new Vector2Int(
                Mathf.FloorToInt((float)bx / cs),
                Mathf.FloorToInt((float)bz / cs));
            ChunkPF chunk = worldManager.GetChunk(chunkCoord);
            if (chunk == null) return BlockPF.BlockType.AIR;
            int lx = bx - chunkCoord.x * cs;
            int ly = by;
            int lz = bz - chunkCoord.y * cs;
            if (lx < 0 || lx >= cs || ly < 0 || ly >= cs || lz < 0 || lz >= cs)
                return BlockPF.BlockType.AIR;
            return chunk.chunkData[lx, ly, lz].type;
        }

        BlockPF.BlockType hitType = GetBlockType(bx, by, bz);
        if (hitType == BlockPF.BlockType.AIR || hitType == BlockPF.BlockType.WATER) return;
        if (!obsidianUnlocked && hitType == BlockPF.BlockType.OBSIDIAN) return;

        Vector3Int blockKey = new Vector3Int(bx, by, bz);
        if (!blockDamage.ContainsKey(blockKey)) blockDamage[blockKey] = 0;
        blockDamage[blockKey]++;

        if (blockDamage[blockKey] < hitsToBreak) return;

        blockDamage.Remove(blockKey);
        InventoryManagerPF.Instance.AddBlock(hitType);
        ModifyBlock(new Vector3(bx, by, bz), BlockPF.BlockType.AIR);
    }

    void PlaceBlock() {
        if (InventoryManagerPF.Instance.Count(placeType) <= 0) 
            return;

        //mandamos um raycast e modificamos para o tipo de bloco que esta com o current inidce
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance)) {
            InventoryManagerPF.Instance.RemoveBlock(placeType);
            ModifyBlock(hit.point + hit.normal * 0.5f, placeType);
        }
    }

    void ModifyBlock(Vector3 worldPos, BlockPF.BlockType type) {
        int cs = ChunkPF.chunkSize;

        int bx = Mathf.RoundToInt(worldPos.x);// arredondamos para cada bloco ocupar exatamente 1 unidade
        int by = Mathf.RoundToInt(worldPos.y);
        int bz = Mathf.RoundToInt(worldPos.z);

        if (by < 0 || by >= cs) return;// verificamos se o y esta dentro do chunk

        Vector2Int chunkCoord = new Vector2Int(// verifica em que chunk esta o bloco(usamos o mesmo raciocinio que o GetPlayerChunk)
            Mathf.FloorToInt((float)bx / cs),
            Mathf.FloorToInt((float)bz / cs));

        ChunkPF chunk = worldManager.GetChunk(chunkCoord);//vamos pedir o chunk ao manager
        if (chunk == null) return;

        //transforma em coordenadas do mundo 
        int localX = bx - chunkCoord.x * cs;
        int localY = by;//o y n é preciso pois os chunks n tem offsets verticais 
        int localZ = bz - chunkCoord.y * cs;

        //so para verificar se a conversao ficou dentro dos limites 
        if (localX < 0 || localX >= cs ||
            localY < 0 || localY >= cs ||
            localZ < 0 || localZ >= cs) return;

        BlockPF block = chunk.chunkData[localX, localY, localZ];
        block.type = type;
        block.isSolid = (type != BlockPF.BlockType.AIR && type != BlockPF.BlockType.WATER);// atribui ao bloco novo se é solido ou nao, ou seja, é solido se n for nem air ou agua

        chunk.DrawChunk();//constroi o chunk

        //aqui se o bloco estiver em qq um dos cantos do chunk reconstroi o chunk vizinho 
        if (localX == 0) RedrawNeighbour(chunkCoord + Vector2Int.left);
        if (localX == cs - 1) RedrawNeighbour(chunkCoord + Vector2Int.right);
        if (localZ == 0) RedrawNeighbour(chunkCoord + Vector2Int.down);
        if (localZ == cs - 1) RedrawNeighbour(chunkCoord + Vector2Int.up);

        // cada chunk e responsavel por desenhar as suas proprioas fases mas para saber se deve ou nao de desenhar uma face pergunta ao bloco se o bloco do lado ou de baixo é solido.
        //Senao tem que desenhar, por isso e que precisamos de desenhar o vizinho ou pedir para desenhar, para o sistema saber se tem ou n que desenhar todas as faces do cubo.
    }

    //desenha o vizinho do chunk
    void RedrawNeighbour(Vector2Int coord) {
        ChunkPF c = worldManager.GetChunk(coord);
        if (c != null) c.DrawChunk();
    }
}