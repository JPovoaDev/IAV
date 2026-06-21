using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public enum EstadoNPC {
    Parado,
    AEsperarApostar,
    AEsperarEscolherRobot,
}

public class GamblerNPCLLMPF : MonoBehaviour {
    [HideInInspector] public Transform playerTransform;
    [HideInInspector] public GameObject voxelArenaObject; // o chunk que tem a obsidiana desta arena, usado no fim para a remover

    private float distanciaInteracao = 3f;
    private KeyCode teclaInteragir = KeyCode.F;
    [SerializeField] private GameObject dicaInteracao; // texto "Pressiona F"

    [SerializeField] private OllamaClientPF ollama; // cada NPC tem o seu próprio cliente, não é partilhado entre arenas

    // os prompts é que definem a personalidade do bartender e obrigam o modelo a responder sempre no mesmo formato JSON para conseguirmos fazer parse com
    // JsonUtility depois
    // as regras extra (1 a 4) existem porque sem elas o modelo de 3B às vezes dizia uma coisa na "frase" e pedia outra no campo "item"
    private string systemPromptAvaliacao =
       "Tu és um bartender ganancioso de uma casa de apostas de corridas de robôs. Só deixas apostar se o jogador te der algo valioso. " +
    "Responde SEMPRE em JSON: {\"interessado\": true/false, \"item\": \"NOME_DO_TIPO\", \"quantidade\": numero, \"frase\": \"frase curta em português de Portugal\"}. " +
    "REGRAS OBRIGATÓRIAS: " +
    "1. Se interessado for true, o campo \"item\" e \"quantidade\" têm de corresponder exatamente ao que pedes na \"frase\" — nunca digas uma coisa no JSON e outra na frase. " +
    "2. A \"frase\" tem de mencionar explicitamente o nome do item e a quantidade pedida (ex: \"Dá-me 3 Diamond e deixo-te apostar\"). " +
    "3. Nunca uses frases vagas como \"ajuda-nos a garantir que ganhes\" sem dizeres o item e a quantidade. " +
    "4. Escolhe sempre um item da lista de inventário fornecida, nunca inventes um item que não esteja lá.";

    private string systemPromptEscolha =
        "O jogador vai dizer em que robot aposta: Robot A, Robot B ou Robot C (índices 0, 1, 2). " +
        "Responde SEMPRE em JSON: {\"indiceRobot\": numero_ou_-1, \"frase\": \"frase curta em português de Portugal\"}.";

    private string[] nomesRobots = { "Robot A", "Robot B", "Robot C" };

    // a arena de corrida e a TV são criadas só quando se chega a apostar, não
    // existem enquanto o jogador só está a negociar com o bartender
    [Header("Arena / TV")]
    [SerializeField] private GameObject prefabArena; // contém o PlatformSpawnerPF + ParkourArenaPF + os agentes
    [SerializeField] private GameObject prefabTV;
    [SerializeField] private RenderTexture renderTextureCorrida; // a câmara da corrida escreve para aqui e a TV lê daqui
    private float distanciaSpawnArena = 2000f; // longe do mundo para não aparecer no meio de chunks

    private GameObject arenaCriada;
    private GameObject tvCriada;

    [Header("UI")]
    [SerializeField] private GameObject painelDialogo;
    [SerializeField] private TMP_Text textoDialogo;
    [SerializeField] private TMP_InputField campoInput;
    [SerializeField] private GameObject botaoEnviar;
    [SerializeField] private GameObject painelEscolha; // botões Aceitar / Recusar

    private EstadoNPC estado = EstadoNPC.Parado;

    // para o script do jogador não meter o cursor locked enquanto está a falar com o agente
    public static bool DialogoAberto = false;

    void Update() {
        bool dialogoFechado = !painelDialogo.activeSelf;
        bool perto = Vector3.Distance(transform.position, playerTransform.position) <= distanciaInteracao;

        dicaInteracao.SetActive(perto && dialogoFechado && estado == EstadoNPC.Parado);

        if (perto && dialogoFechado && estado == EstadoNPC.Parado && Input.GetKeyDown(teclaInteragir)) {
            AbrirDialogo();
        }

        if (painelDialogo.activeSelf && Input.GetKeyDown(KeyCode.Escape)) {
            FecharDialogoCompleto();
        }
    }

    void Start() {
        painelDialogo.SetActive(false);

        dicaInteracao.SetActive(false);
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        playerTransform = player.transform;
    }

    // estado da negociação atual, guardado aqui enquanto se espera o jogador clicar em Aceitar ou Recusar (ver OnAceitarClicado / OnRecusarClicado)
    private bool temItemPendente = false;
    private BlockPF.BlockType itemPendente;
    private int quantidadePendente = 0;

    private int apostaJogador = -1;
    private int apostaNPC = -1; // o "rival" do jogador, escolhido ao calhas mas nunca igual à dele

    // estas duas classes só existem para dar match à estrutura exata do JSON que pedimos ao LLM nos prompts lá em cima
    [Serializable]
    private class RespostaAvaliacao {
        public bool interessado;
        public string item;
        public int quantidade;
        public string frase;
    }

    [Serializable]
    private class RespostaEscolha {
        public int indiceRobot;
        public string frase;
    }


    private void AbrirCursor() {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void FecharCursor() {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    //input field XOR botões Aceitar/Recusar, nunca os dois

    private void MostrarInput(bool mostrar) {
        campoInput.gameObject.SetActive(mostrar);
        botaoEnviar.SetActive(mostrar);

        if (mostrar) {
            campoInput.text = "";
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(campoInput.gameObject);
            campoInput.ActivateInputField();
        }
    }

    private void MostrarEscolha(bool mostrar) {
        painelEscolha.SetActive(mostrar);
    }

    private void DefinirEstadoUI(bool mostrarInput, bool mostrarEscolha) {
        MostrarInput(mostrarInput);
        MostrarEscolha(mostrarEscolha);
    }

    // fecha tudo e devolve o controlo ao jogo
    private void FecharDialogoCompleto() {
        painelDialogo.SetActive(false);
        DialogoAberto = false;
        FecharCursor();
        estado = EstadoNPC.Parado;
    }

    private IEnumerator FecharDepoisDe(float segundos) {
        DefinirEstadoUI(mostrarInput: false, mostrarEscolha: false);
        yield return new WaitForSeconds(segundos);
        FecharDialogoCompleto();
    }

    public void AbrirDialogo() {
        painelDialogo.SetActive(true);
        DialogoAberto = true;
        AbrirCursor();

        estado = EstadoNPC.AEsperarApostar;
        textoDialogo.text = "Então, vens apostar? Tens a certeza?";

        DefinirEstadoUI(mostrarInput: true, mostrarEscolha: false);
    }

    public void OnEnviarClicado() {
        string texto = campoInput.text;
        if (string.IsNullOrWhiteSpace(texto)) return; // ignora envios vazios

        campoInput.text = "";

        // o mesmo botão "Enviar" serve para os dois passos da conversa, o que muda
        // é qual coroutine se chama, consoante o estado em que estamos
        if (estado == EstadoNPC.AEsperarApostar) {
            DefinirEstadoUI(mostrarInput: false, mostrarEscolha: false);
            StartCoroutine(PedirAvaliacao());
        } else if (estado == EstadoNPC.AEsperarEscolherRobot) {
            DefinirEstadoUI(mostrarInput: false, mostrarEscolha: false);
            StartCoroutine(PedirEscolha(texto));
        }
    }

    // passo 1: o LLM diz o que quer em troca

    private IEnumerator PedirAvaliacao() {
        textoDialogo.text = "(o bartender está a pensar...)";

        // monta a lista do inventário do jogador para mandar ao LLM, é a partir
        // disto que ele tem de escolher um item (regra 4 do prompt)
        // sem isto o bartender ia pedir coisas aleatórias que o jogador podia nem ter
        string inventario = "Inventário do jogador:\n";
        foreach (BlockPF.BlockType tipo in BlockRarityPF.ordemDeRaridade) {
            int quantidade = InventoryManagerPF.Instance.Count(tipo);
            inventario += "- " + tipo + ": " + quantidade + "\n";
        }

        ollama.EnviarMensagem(systemPromptAvaliacao, inventario);

        while (ollama.aPedir) 
            yield return null; // espera pela resposta sem travar o jogo

        RespostaAvaliacao resposta = JsonUtility.FromJson<RespostaAvaliacao>(ollama.resposta);

        // é aqui que se não houver resposta por exemplo se o modelo estiver desligado o agente diz a fallback response
        if (resposta == null || !resposta.interessado) {
            textoDialogo.text = resposta != null ? resposta.frase : "Não tens nada que me interesse.";
            yield return new WaitForSeconds(2.5f);
            FecharDialogoCompleto();
            yield break;
        }

        // valida o item pedido contra o inventário do jogador
        bool itemValido = Enum.TryParse(resposta.item, true, out BlockPF.BlockType item);
        int temos = itemValido ? InventoryManagerPF.Instance.Count(item) : 0;

        if (!itemValido || temos <= 0) {
            // o jogador não tem nada do que o LLM pediu -> pede ao próprio LLM para reagir a isso e dizer para o jogador voltar mais tarde
            // isto é um segundo pedido ao Ollama, não uma continuação da conversa anterior porque o OllamaClientPF não guarda histórico nenhum,
            // por isso temos de lhe explicar a situação toda de novo no contexto
            textoDialogo.text = "(o bartender está a pensar...)";

            string contexto = "O jogador não tem nenhum " + resposta.item +
                " no inventário. Recusa o negócio e diz-lhe educadamente que volte " +
                "mais tarde com algo que tenhas pedido. Responde no mesmo formato JSON " +
                "(interessado deve ser false).";

            ollama.EnviarMensagem(systemPromptAvaliacao, contexto);

            while (ollama.aPedir)
                yield return null;

            RespostaAvaliacao respostaRecusa = JsonUtility.FromJson<RespostaAvaliacao>(ollama.resposta);

            textoDialogo.text = respostaRecusa != null ? respostaRecusa.frase : "Não tens o que preciso. Volta mais tarde.";

            yield return new WaitForSeconds(2.5f);
            FecharDialogoCompleto();
            yield break;
        }

        // se o jogador tiver menos do que o pedido mas se tiver alguma coisa ajustamos a quantidade para o máximo que ele tem
        if (resposta.quantidade > temos) {
            resposta.quantidade = temos;
        }

        itemPendente = item;
        quantidadePendente = resposta.quantidade;
        temItemPendente = true;

        textoDialogo.text = resposta.frase;
        DefinirEstadoUI(mostrarInput: false, mostrarEscolha: true);
    }

    public void OnAceitarClicado() {
        // só aqui é que o item sai do inventário
        InventoryManagerPF.Instance.RemoveBlock(itemPendente, quantidadePendente);

        textoDialogo.text = "Negócio fechado! Em qual robot apostas: Robot A, B ou C?";
        estado = EstadoNPC.AEsperarEscolherRobot;

        DefinirEstadoUI(mostrarInput: true, mostrarEscolha: false);
    }

    public void OnRecusarClicado() {
        temItemPendente = false;
        textoDialogo.text = "Sem material, sem aposta.";
        StartCoroutine(FecharDepoisDe(2f));
    }

    // passo 2: o LLM interpreta a escolha do robot
    // o jogador escreve em texto livre tipo "aposto no B" ou "robot 2" e é o LLM que
    // interpreta isso e devolve o índice certo, em vez de obrigarmos a um formato
    // rígido tipo só aceitar "A", "B" ou "C" exatamente
    private IEnumerator PedirEscolha(string textoJogador) {
        textoDialogo.text = "(a pensar...)";

        ollama.EnviarMensagem(systemPromptEscolha, "O jogador escreveu: \"" + textoJogador + "\"");
        while (ollama.aPedir) 
            yield return null;

        RespostaEscolha resposta = JsonUtility.FromJson<RespostaEscolha>(ollama.resposta);

        if (resposta == null || resposta.indiceRobot < 0 || resposta.indiceRobot >= nomesRobots.Length) {
            textoDialogo.text = "Não percebi, repete em qual robot apostas.";
            DefinirEstadoUI(mostrarInput: true, mostrarEscolha: false);
            yield break;
        }

        apostaJogador = resposta.indiceRobot;

        // o NPC também aposta num robot diferente do jogador (senão nunca havia hipótese de o jogador perder a aposta para ele)
        apostaNPC = UnityEngine.Random.Range(0, nomesRobots.Length);

        while (apostaNPC == apostaJogador)
            apostaNPC = UnityEngine.Random.Range(0, nomesRobots.Length);

        textoDialogo.text = resposta.frase;

        yield return new WaitForSeconds(1.5f);
        FecharDialogoCompleto();

        ComecarCorrida();
    }

    // cria a arena e a TV e liga-se ao evento de fim de corrida
    // é aqui que entramos no território do ML-Agents onde spawnamos o prefab que contém o
    // PlatformSpawnerPF + ParkourArenaPF + os agentes já treinados
    // spawnamos longe do NPC (distanciaSpawnArena) para a corrida não interferir visualmente com o resto do
    // mundo e ligamos uma câmara a renderizar para a TV ao lado do bartender
    private void ComecarCorrida() {
        float angulo = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float distancia = distanciaSpawnArena + UnityEngine.Random.Range(-30f, 30f);
        Vector3 posicaoArena = transform.position + new Vector3(Mathf.Cos(angulo) * distancia, 0f, Mathf.Sin(angulo) * distancia);

        arenaCriada = Instantiate(prefabArena, posicaoArena, Quaternion.identity);

        // é aqui que nos ligamos ao evento do ParkourArenaPF, CompararResultado só corre quando algum agente chegar mesmo ao goal
        ParkourArenaPF scriptArena = arenaCriada.GetComponentInChildren<ParkourArenaPF>();
        scriptArena.onRaceWinner += CompararResultado;

        Vector3 posicaoTV = transform.position + transform.forward * 2.5f + Vector3.up * 3f;
        tvCriada = Instantiate(prefabTV, posicaoTV, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));

        StartCoroutine(PrepararCamara(posicaoArena));
    }

    private IEnumerator PrepararCamara(Vector3 posicaoArena) {
        yield return null; // espera um frame para a arena estar pronta

        // câmara criada só em código (não é um prefab) porque a posição depende de
        // onde a arena calhou de ser instanciada e escreve para a RenderTexture que
        // a TV está a usar como material, dando o efeito de "ecrã ao vivo"
        GameObject objetoCamara = new GameObject("CamaraDaCorrida");
        Camera camara = objetoCamara.AddComponent<Camera>();
        camara.targetTexture = renderTextureCorrida;
        camara.fieldOfView = 75f;

        objetoCamara.transform.position = posicaoArena + new Vector3(-25f, 14f, 16f);
        objetoCamara.transform.LookAt(posicaoArena + new Vector3(0f, 2f, 16f));
        objetoCamara.transform.SetParent(arenaCriada.transform);
    }

    // passo 3: comparar com o vencedor real da corrida
    // chamado pelo onRaceWinner do ParkourArenaPF com o índice do agente que chegou primeiro
    // compara contra a aposta do jogador e a do NPC e decide o desfecho que acaba por mexer no estado do mundo voxel no ChunkPF
    public void CompararResultado(int indiceVencedor) {
        painelDialogo.SetActive(true);
        DialogoAberto = true;
        AbrirCursor();
        DefinirEstadoUI(mostrarInput: false, mostrarEscolha: false);

        if (indiceVencedor == apostaJogador) {
            // ganhaste: a arena fica visível e a obsidiana fica acessível
            textoDialogo.text = "O teu " + nomesRobots[indiceVencedor] + " ganhou! Tens acesso à arena.";
            BlockInteractionPF.obsidianUnlocked = true; // flag global lida pelo BlockInteractionPF na hora de minerar
        } else if (indiceVencedor == apostaNPC) {
            // perdeste: a arena desaparece, junto com a obsidiana
            textoDialogo.text = "O meu " + nomesRobots[apostaNPC] + " ganhou! Perdeste a aposta.";
            Invoke("DespawnarArena", 5f);
        } else {
            // ninguém apostou no vencedor: devolve o material e a arena desaparece, sem desbloquear nada
            textoDialogo.text = "Ninguém apostou no " + nomesRobots[indiceVencedor] + ". Toma o teu material de volta.";

            if (temItemPendente)
                InventoryManagerPF.Instance.AddBlock(itemPendente, quantidadePendente);

            Invoke("DespawnarArena", 5f);
        }

        estado = EstadoNPC.Parado;
        StartCoroutine(FecharDepoisDe(5f)); // fecha o painel junto com a arena
    }

    // tira a TV e a arena do mapa (a obsidiana só fica acessível se chamarmos isto a perder)
    private void DespawnarArena() {
        if (tvCriada != null) Destroy(tvCriada);
        if (arenaCriada != null) Destroy(arenaCriada);

        // é aqui que o ciclo fecha de volta no ChunkPF: vamos apanhar o componente
        // ChunkPF do gameobject que o ArenaRegistryPF nos deu no início e pedimos
        // para remover a obsidiana e reconstruir a malha do chunk sem ela
        if (voxelArenaObject != null) {
            ChunkPF chunk = voxelArenaObject.GetComponent<ChunkPF>();
            if (chunk != null) chunk.RemoveObsidian();
        }
    }
}