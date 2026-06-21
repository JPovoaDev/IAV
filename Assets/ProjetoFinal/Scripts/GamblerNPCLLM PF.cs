using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Bartender de uma casa de apostas. Estado simples: cada "estado" diz que tipo de
// mensagem estamos à espera de receber do jogador (texto livre).
public enum EstadoNPC
{
    Parado,
    AEsperarApostar,
    AEsperarEscolherRobot,
}

public class GamblerNPCLLMPF : MonoBehaviour
{
    [Header("Ligados pelo ArenaRegistryPF (não precisas de arrastar no Inspector)")]
    public Transform playerTransform;
    public GameObject voxelArenaObject;

    [Header("Interação")]
    [SerializeField] private float distanciaInteracao = 3f;
    [SerializeField] private KeyCode teclaInteragir = KeyCode.F;
    [SerializeField] private GameObject dicaInteracao; // texto "Pressiona F"

    [Header("Ollama")]
    [SerializeField] private OllamaClientPF ollama;

    [Header("Personalidade")]
    [TextArea(4, 12)]
    [SerializeField]
    private string systemPromptAvaliacao =
       "Tu és um bartender ganancioso de uma casa de apostas de corridas de robôs. Só deixas apostar se o jogador te der algo valioso. " +
    "Responde SEMPRE em JSON: {\"interessado\": true/false, \"item\": \"NOME_DO_TIPO\", \"quantidade\": numero, \"frase\": \"frase curta em português de Portugal\"}. " +
    "REGRAS OBRIGATÓRIAS: " +
    "1. Se interessado for true, o campo \"item\" e \"quantidade\" têm de corresponder exatamente ao que pedes na \"frase\" — nunca digas uma coisa no JSON e outra na frase. " +
    "2. A \"frase\" tem de mencionar explicitamente o nome do item e a quantidade pedida (ex: \"Dá-me 3 Diamond e deixo-te apostar\"). " +
    "3. Nunca uses frases vagas como \"ajuda-nos a garantir que ganhes\" sem dizeres o item e a quantidade. " +
    "4. Escolhe sempre um item da lista de inventário fornecida, nunca inventes um item que não esteja lá.";

    [TextArea(4, 12)]
    [SerializeField]
    private string systemPromptEscolha =
        "O jogador vai dizer em que robot aposta: Robot A, Robot B ou Robot C (índices 0, 1, 2). " +
        "Responde SEMPRE em JSON: {\"indiceRobot\": numero_ou_-1, \"frase\": \"frase curta em português de Portugal\"}.";

    [Header("Robots")]
    [SerializeField] private string[] nomesRobots = { "Robot A", "Robot B", "Robot C" };

    [Header("Arena / TV")]
    [SerializeField] private GameObject prefabArena;
    [SerializeField] private GameObject prefabTV;
    [SerializeField] private RenderTexture renderTextureCorrida;
    [SerializeField] private float distanciaSpawnArena = 2000f;

    private GameObject arenaCriada;
    private GameObject tvCriada;

    [Header("UI (arrasta os objetos da Canvas)")]
    [SerializeField] private GameObject painelDialogo;
  
    [SerializeField] private TMP_Text textoDialogo;
    [SerializeField] private TMP_InputField campoInput;
    [SerializeField] private GameObject botaoEnviar; // opcional: só preenche se o botão Enviar NÃO for filho do campoInput
    [SerializeField] private GameObject painelEscolha; // botões Aceitar / Recusar

    private EstadoNPC estado = EstadoNPC.Parado;

    // Outros scripts (ex: câmara/movimento em 1ª pessoa) podem verificar isto
    // para saber se devem ignorar input do jogador enquanto se fala com o NPC.
    public static bool DialogoAberto = false;

    void Update()
    {
        bool dialogoFechado = !painelDialogo.activeSelf;
        bool perto = Vector3.Distance(transform.position, playerTransform.position) <= distanciaInteracao;

            dicaInteracao.SetActive(perto && dialogoFechado && estado == EstadoNPC.Parado);

        if (perto && dialogoFechado && estado == EstadoNPC.Parado && Input.GetKeyDown(teclaInteragir))
        {
            AbrirDialogo();
        }

        // Escape fecha sempre o diálogo, para nunca ficares "preso" no painel
        // (ex.: depois de uma recusa ou de uma resposta não reconhecida).
        if (painelDialogo.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            FecharDialogoCompleto();
        }
    }

    void Start() {
        painelDialogo.SetActive(false);

        dicaInteracao.SetActive(false);
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        playerTransform = player.transform;
    }

    private bool temItemPendente = false;
    private BlockPF.BlockType itemPendente;
    private int quantidadePendente = 0;

    private int apostaJogador = -1;
    private int apostaNPC = -1;

    [Serializable]
    private class RespostaAvaliacao
    {
        public bool interessado;
        public string item;
        public int quantidade;
        public string frase;
    }

    [Serializable]
    private class RespostaEscolha
    {
        public int indiceRobot;
        public string frase;
    }

    // ---------- Cursor: aparece só enquanto o diálogo está aberto ----------

    private void AbrirCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void FecharCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ---------- Estado da UI: input field XOR botões Aceitar/Recusar, nunca os dois ----------

    private void MostrarInput(bool mostrar)
    {
        campoInput.gameObject.SetActive(mostrar);
        botaoEnviar.SetActive(mostrar);

        if (mostrar)
        {
            campoInput.text = "";
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(campoInput.gameObject);
            campoInput.ActivateInputField();
        }
    }

    private void MostrarEscolha(bool mostrar)
    {
        painelEscolha.SetActive(mostrar);
    }

    private void DefinirEstadoUI(bool mostrarInput, bool mostrarEscolha)
    {
        MostrarInput(mostrarInput);
        MostrarEscolha(mostrarEscolha);
    }

    // Fecha tudo e devolve o controlo ao jogo
    private void FecharDialogoCompleto()
    {
        painelDialogo.SetActive(false);
        DialogoAberto = false;
        FecharCursor();
        estado = EstadoNPC.Parado;
    }

    private IEnumerator FecharDepoisDe(float segundos)
    {
        DefinirEstadoUI(mostrarInput: false, mostrarEscolha: false);
        yield return new WaitForSeconds(segundos);
        FecharDialogoCompleto();
    }

    // Liga isto ao botão de interagir / abrir o diálogo
    public void AbrirDialogo()
    {
        painelDialogo.SetActive(true);
        DialogoAberto = true;
        AbrirCursor();

        estado = EstadoNPC.AEsperarApostar;
        textoDialogo.text = "Então, vens apostar? Escreve aí em baixo.";

        DefinirEstadoUI(mostrarInput: true, mostrarEscolha: false);
    }

    // Liga isto ao botão "Enviar" / OnSubmit do input field
    public void OnEnviarClicado()
    {
        string texto = campoInput.text;
        if (string.IsNullOrWhiteSpace(texto)) return; // ignora envios vazios

        campoInput.text = "";

        if (estado == EstadoNPC.AEsperarApostar)
        {
            DefinirEstadoUI(mostrarInput: false, mostrarEscolha: false);
            StartCoroutine(PedirAvaliacao());
        }
        else if (estado == EstadoNPC.AEsperarEscolherRobot)
        {
            DefinirEstadoUI(mostrarInput: false, mostrarEscolha: false);
            StartCoroutine(PedirEscolha(texto));
        }
    }

    // ---------- Passo 1: o LLM diz o que quer em troca ----------

    private IEnumerator PedirAvaliacao()
    {
        textoDialogo.text = "(o bartender está a pensar...)";

        string inventario = "Inventário do jogador:\n";
        foreach (BlockPF.BlockType tipo in BlockRarityPF.ordemDeRaridade)
        {
            int quantidade = InventoryManagerPF.Instance.Count(tipo);
            inventario += "- " + tipo + ": " + quantidade + "\n";
        }

        ollama.EnviarMensagem(systemPromptAvaliacao, inventario);
        while (ollama.aPedir) yield return null;

        RespostaAvaliacao resposta = JsonUtility.FromJson<RespostaAvaliacao>(ollama.resposta);

        if (resposta == null || !resposta.interessado)
        {
            textoDialogo.text = resposta != null ? resposta.frase : "Não tens nada que me interesse.";
            yield return new WaitForSeconds(2.5f);
            FecharDialogoCompleto();
            yield break;
        }

        // Valida o item pedido contra o inventário REAL do jogador
        bool itemValido = Enum.TryParse(resposta.item, true, out BlockPF.BlockType item);
        int temos = itemValido ? InventoryManagerPF.Instance.Count(item) : 0;

        if (!itemValido || temos <= 0)
        {
            // O jogador não tem nada do que o LLM pediu — pede ao próprio LLM
            // para reagir a isso e dizer para o jogador voltar mais tarde.
            textoDialogo.text = "(o bartender está a pensar...)";

            string contexto = "O jogador não tem nenhum " + resposta.item +
                " no inventário. Recusa o negócio e diz-lhe educadamente que volte " +
                "mais tarde com algo que tenhas pedido. Responde no mesmo formato JSON " +
                "(interessado deve ser false).";

            ollama.EnviarMensagem(systemPromptAvaliacao, contexto);
            while (ollama.aPedir) yield return null;

            RespostaAvaliacao respostaRecusa = JsonUtility.FromJson<RespostaAvaliacao>(ollama.resposta);

            textoDialogo.text = respostaRecusa != null
                ? respostaRecusa.frase
                : "Não tens o que preciso. Volta mais tarde.";

            yield return new WaitForSeconds(2.5f);
            FecharDialogoCompleto();
            yield break;
        }

        // Se o jogador tem MENOS do que o pedido (mas tem alguma coisa), 
        // ajusta a quantidade para o máximo que ele tem
        if (resposta.quantidade > temos)
        {
            resposta.quantidade = temos;
        }

        itemPendente = item;
        quantidadePendente = resposta.quantidade;
        temItemPendente = true;

        textoDialogo.text = resposta.frase;
        DefinirEstadoUI(mostrarInput: false, mostrarEscolha: true);
    }
    // Liga isto ao botão "Aceitar"
    public void OnAceitarClicado()
    {
        InventoryManagerPF.Instance.RemoveBlock(itemPendente, quantidadePendente);

        textoDialogo.text = "Negócio fechado! Em qual robot apostas: Robot A, B ou C?";
        estado = EstadoNPC.AEsperarEscolherRobot;

        DefinirEstadoUI(mostrarInput: true, mostrarEscolha: false);
    }

    // Liga isto ao botão "Recusar"
    public void OnRecusarClicado()
    {
        temItemPendente = false;
        textoDialogo.text = "Sem material, sem aposta.";
        StartCoroutine(FecharDepoisDe(2f));
    }

    // ---------- Passo 2: o LLM interpreta a escolha do robot ----------

    private IEnumerator PedirEscolha(string textoJogador)
    {
        textoDialogo.text = "(a pensar...)";

        ollama.EnviarMensagem(systemPromptEscolha, "O jogador escreveu: \"" + textoJogador + "\"");
        while (ollama.aPedir) yield return null;

        RespostaEscolha resposta = JsonUtility.FromJson<RespostaEscolha>(ollama.resposta);

        if (resposta == null || resposta.indiceRobot < 0 || resposta.indiceRobot >= nomesRobots.Length)
        {
            textoDialogo.text = "Não percebi, repete em qual robot apostas.";
            DefinirEstadoUI(mostrarInput: true, mostrarEscolha: false);
            yield break;
        }

        apostaJogador = resposta.indiceRobot;
        apostaNPC = UnityEngine.Random.Range(0, nomesRobots.Length);
        while (apostaNPC == apostaJogador)
            apostaNPC = UnityEngine.Random.Range(0, nomesRobots.Length);

        textoDialogo.text = resposta.frase;

        yield return new WaitForSeconds(1.5f);
        FecharDialogoCompleto();

        ComecarCorrida();
    }

    // ---------- Cria a arena e a TV, e liga-se ao evento de fim de corrida ----------

    private void ComecarCorrida()
    {
        float angulo = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float distancia = distanciaSpawnArena + UnityEngine.Random.Range(-30f, 30f);
        Vector3 posicaoArena = transform.position + new Vector3(Mathf.Cos(angulo) * distancia, 0f, Mathf.Sin(angulo) * distancia);

        arenaCriada = Instantiate(prefabArena, posicaoArena, Quaternion.identity);

        ParkourArenaPF scriptArena = arenaCriada.GetComponentInChildren<ParkourArenaPF>();
        scriptArena.onRaceWinner += CompararResultado;

        Vector3 posicaoTV = transform.position + transform.forward * 2.5f + Vector3.up * 3f;
        tvCriada = Instantiate(prefabTV, posicaoTV, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));

        StartCoroutine(PrepararCamara(posicaoArena));
    }

    private IEnumerator PrepararCamara(Vector3 posicaoArena)
    {
        yield return null; // espera um frame para a arena estar pronta

        GameObject objetoCamara = new GameObject("CamaraDaCorrida");
        Camera camara = objetoCamara.AddComponent<Camera>();
        camara.targetTexture = renderTextureCorrida;
        camara.fieldOfView = 75f;

        objetoCamara.transform.position = posicaoArena + new Vector3(-25f, 14f, 16f);
        objetoCamara.transform.LookAt(posicaoArena + new Vector3(0f, 2f, 16f));
        objetoCamara.transform.SetParent(arenaCriada.transform);
    }

    // ---------- Passo 3: comparar com o vencedor real da corrida ----------

    public void CompararResultado(int indiceVencedor)
    {
        painelDialogo.SetActive(true);
        DialogoAberto = true;
        AbrirCursor();
        DefinirEstadoUI(mostrarInput: false, mostrarEscolha: false);

        if (indiceVencedor == apostaJogador)
        {
            // Ganhaste: a arena fica visível e a obsidiana fica acessível
            textoDialogo.text = "O teu " + nomesRobots[indiceVencedor] + " ganhou! Tens acesso à arena.";
            BlockInteractionPF.obsidianUnlocked = true;
        }
        else if (indiceVencedor == apostaNPC)
        {
            // Perdeste: a arena desaparece, junto com a obsidiana
            textoDialogo.text = "O meu " + nomesRobots[apostaNPC] + " ganhou! Perdeste a aposta.";
            Invoke("DespawnarArena", 5f);
        }
        else
        {
            // Ninguém apostou no vencedor: devolve o material e a arena some, sem desbloquear nada
            textoDialogo.text = "Ninguém apostou no " + nomesRobots[indiceVencedor] + ". Toma o teu material de volta.";
            if (temItemPendente)
                InventoryManagerPF.Instance.AddBlock(itemPendente, quantidadePendente);
            Invoke("DespawnarArena", 5f);
        }

        estado = EstadoNPC.Parado;
        StartCoroutine(FecharDepoisDe(5f)); // fecha o painel junto com a arena
    }

    // Tira a TV e a arena do mapa (a obsidiana só fica acessível se chamarmos isto a perder)
    private void DespawnarArena()
    {
        if (tvCriada != null) Destroy(tvCriada);
        if (arenaCriada != null) Destroy(arenaCriada);

        if (voxelArenaObject != null)
        {
            ChunkPF chunk = voxelArenaObject.GetComponent<ChunkPF>();
            if (chunk != null) chunk.RemoveObsidian();
        }
    }
}