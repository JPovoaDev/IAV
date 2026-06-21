using UnityEngine;
using System.Collections.Generic;

public class BlockInteractionPF : MonoBehaviour {

    public WorldManagerPF worldManager;
    public float maxDistance = 6f; // distância máxima do raycast —> quanto maior, mais longe o jogador consegue alcançar os blocos
    public Transform highlightCube; // cubo wireframe que aparece à volta do bloco que estamos a apontar

    // registo de quantas vezes cada posição de bloco foi atingida (3 para partir)
    private Dictionary<Vector3Int, int> blockDamage = new Dictionary<Vector3Int, int>();
    private int hitsToBreak = 3;

    // flag estática para que o GamblerNPCLLMPF a possa ligar de qualquer sítio assim que o jogador vence a corrida
    public static bool obsidianUnlocked = false;

    // paleta de blocos que o jogador pode colocar
    private BlockPF.BlockType[] palette = {
        BlockPF.BlockType.DIRT,
        BlockPF.BlockType.STONE,
        BlockPF.BlockType.GRASS,
        BlockPF.BlockType.SAND,
        BlockPF.BlockType.SNOW,
        BlockPF.BlockType.WOOD,
        BlockPF.BlockType.LEAVES,
        BlockPF.BlockType.OBSIDIAN
    };
    private int currentIndex = 0; // índice do bloco atualmente selecionado na paleta
    private BlockPF.BlockType placeType => palette[currentIndex]; // propriedade que expõe o tipo atual sem revelar o índice

    void Update() {
        HandleHighlight(); // atualiza o contorno wireframe do bloco que estamos a mirar
        HandleScroll();    // muda o bloco selecionado com a roda do rato
        if (Input.GetMouseButtonDown(0)) BreakBlock();
        if (Input.GetMouseButtonDown(1)) PlaceBlock();
    }

    void HandleHighlight() {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance)) {
            // hit.point está exatamente na superfície do bloco, por isso recuamos 0.5 unidades
            // para dentro ao longo da normal —> sem este ajuste, o RoundToInt pode arredondar
            // para o bloco vizinho em vez do bloco que estamos realmente a apontar
            Vector3 center = hit.point - hit.normal * 0.5f;
            highlightCube.position = new Vector3(
                Mathf.RoundToInt(center.x),
                Mathf.RoundToInt(center.y),
                Mathf.RoundToInt(center.z));
            highlightCube.gameObject.SetActive(true);

        } else {
            highlightCube.gameObject.SetActive(false); // sem bloco à frente, esconde o contorno
        }
    }

    void Start() {
        // garante que o inventário já destaca o bloco correto desde o primeiro frame antes de qualquer scroll
        InventoryManagerPF.Instance.SetHighlight(placeType);
    }

    void HandleScroll() {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll == 0f) return;
        int dir = scroll > 0f ? 1 : -1;
        int start = currentIndex;
        // avança na paleta mas salta blocos com stock zero —> assim o jogador nunca
        // fica com um tipo selecionado que não pode colocar
        // o do-while para quando dá a volta completa à paleta sem encontrar nada disponível
        do {
            currentIndex = (currentIndex + dir + palette.Length) % palette.Length;
        } while (InventoryManagerPF.Instance.Count(palette[currentIndex]) <= 0
                 && currentIndex != start);
        InventoryManagerPF.Instance.SetHighlight(placeType);
    }

    void BreakBlock() {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance)) return;

        // mesmo ajuste de -normal * 0.5f do highlight para garantir que identificamos o bloco correto e não o adjacente na superfície
        Vector3 center = hit.point - hit.normal * 0.5f;
        int bx = Mathf.RoundToInt(center.x);
        int by = Mathf.RoundToInt(center.y);
        int bz = Mathf.RoundToInt(center.z);

        // função local para consultar o tipo de um bloco a partir das suas coordenadas globais
        // fica aqui e não no WorldManagerPF porque é a única função que precisa disto
        // a lógica de conversão global?chunk?local é a mesma usada em todo o projeto
        BlockPF.BlockType GetBlockType(int bx, int by, int bz) {
            int cs = ChunkPF.chunkSize;

            Vector2Int chunkCoord = new Vector2Int(
                Mathf.FloorToInt((float)bx / cs),
                Mathf.FloorToInt((float)bz / cs));

            ChunkPF chunk = worldManager.GetChunk(chunkCoord);

            if (chunk == null) 
                return BlockPF.BlockType.AIR;

            int lx = bx - chunkCoord.x * cs;
            int ly = by;
            int lz = bz - chunkCoord.y * cs;

            if (lx < 0 || lx >= cs || ly < 0 || ly >= cs || lz < 0 || lz >= cs)
                return BlockPF.BlockType.AIR;

            return chunk.chunkData[lx, ly, lz].type;
        }

        BlockPF.BlockType hitType = GetBlockType(bx, by, bz);
        // ar e água não têm colisão sólida então o raycast nunca deveria apanhar estes tipos,
        // mas verificamos na mesma por segurança caso o collider esteja mal configurado
        if (hitType == BlockPF.BlockType.AIR || hitType == BlockPF.BlockType.WATER) 
            return;

        // obsidiana está protegida até o jogador vencer a corrida e o GamblerNPCLLMPF ligar a flag
        if (!obsidianUnlocked && hitType == BlockPF.BlockType.OBSIDIAN) 
            return;

        // acumula dano para esta posição específica
        Vector3Int blockKey = new Vector3Int(bx, by, bz);
        if (!blockDamage.ContainsKey(blockKey)) 
            blockDamage[blockKey] = 0;
        blockDamage[blockKey]++;

        if (blockDamage[blockKey] < hitsToBreak) 
            return; // ainda não chegou ao limite de golpes

        // o bloco parte e limpa o dano acumulado, devolve o bloco ao inventário e substitui por ar
        blockDamage.Remove(blockKey);
        InventoryManagerPF.Instance.AddBlock(hitType);
        ModifyBlock(new Vector3(bx, by, bz), BlockPF.BlockType.AIR);
    }

    void PlaceBlock() {
        if (InventoryManagerPF.Instance.Count(placeType) <= 0)
            return; // sem stock não coloca nada

        // aqui somamos a normal em vez de a subtrair, para colocar o bloco novo na face de fora do bloco atingido e não dentro dele
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance)) {
            InventoryManagerPF.Instance.RemoveBlock(placeType);
            ModifyBlock(hit.point + hit.normal * 0.5f, placeType);
        }
    }

    // função central que altera um bloco no mundo: converte coordenadas globais para
    // locais do chunk, atualiza os dados, reconstrói a malha e trata dos vizinhos nas bordas
    void ModifyBlock(Vector3 worldPos, BlockPF.BlockType type) {
        int cs = ChunkPF.chunkSize;

        // arredonda para a grelha inteira de 1 unidade por bloco
        int bx = Mathf.RoundToInt(worldPos.x);
        int by = Mathf.RoundToInt(worldPos.y);
        int bz = Mathf.RoundToInt(worldPos.z);

        if (by < 0 || by >= cs) 
            return; // y fora dos limites verticais do chunk, ignora

        // FloorToInt em vez de RoundToInt porque coordenadas negativas têm de ser tratadas corretamente
        Vector2Int chunkCoord = new Vector2Int(
            Mathf.FloorToInt((float)bx / cs),
            Mathf.FloorToInt((float)bz / cs));

        ChunkPF chunk = worldManager.GetChunk(chunkCoord);
        if (chunk == null) 
            return;

        // converte coordenadas globais para locais dentro do chunk (0 a chunkSize-1)
        int localX = bx - chunkCoord.x * cs;
        int localY = by; // y não precisa de conversão, os chunks não têm offset vertical
        int localZ = bz - chunkCoord.y * cs;

        if (localX < 0 || localX >= cs ||
            localY < 0 || localY >= cs ||
            localZ < 0 || localZ >= cs) 
            return;

        BlockPF block = chunk.chunkData[localX, localY, localZ];
        block.type = type;
        // ar e água não são sólidos, isto controla se o DrawChunk vai ou não gerar faces
        // nos blocos adjacentes a este (um bloco sólido "esconde" as faces dos vizinhos)
        block.isSolid = (type != BlockPF.BlockType.AIR && type != BlockPF.BlockType.WATER);

        chunk.DrawChunk(); // reconstrói a malha deste chunk com o bloco alterado

        // se o bloco modificado fica na borda do chunk, o vizinho também precisa de ser
        // redesenhado porque a sua face partilhada pode ter aparecido ou desaparecido porque
        // cada chunk só sabe desenhar as suas próprias faces e pergunta ao vizinho se o bloco
        // do lado é sólido e sem este redraw o vizinho ficaria com uma face a mais ou a menos
        if (localX == 0) RedrawNeighbour(chunkCoord + Vector2Int.left);
        if (localX == cs - 1) RedrawNeighbour(chunkCoord + Vector2Int.right);
        if (localZ == 0) RedrawNeighbour(chunkCoord + Vector2Int.down);
        if (localZ == cs - 1) RedrawNeighbour(chunkCoord + Vector2Int.up);
    }

    // redesenha o chunk vizinho se ele existir —> chamado só quando um bloco numa borda é alterado
    void RedrawNeighbour(Vector2Int coord) {
        ChunkPF c = worldManager.GetChunk(coord);
        if (c != null) c.DrawChunk();
    }
}