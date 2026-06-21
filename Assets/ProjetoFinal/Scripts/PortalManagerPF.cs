using UnityEngine;
using TMPro;

public class PortalManagerPF : MonoBehaviour {
    public TMP_Text portalPromptText;
    [SerializeField] private GameObject prefabPortal;
    [SerializeField] private float distanciaSpawnPortal = 4f;

    public GameObject endGame;
    private bool portalAvailable = false;
    private GameObject portalCriado; // guardado para evitar criar o portal mais do que uma vez
    private Transform playerTransform;

    void Start() {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        playerTransform = player.transform;
    }

    void Update() {
        int obsidiana = InventoryManagerPF.Instance.Count(BlockPF.BlockType.OBSIDIAN);

        // verifica a condição a cada frame mas só atualiza o estado e o UI uma vez,
        // a flag portalAvailable evita repetir o SetActive e o texto em frames seguintes
        // depois de a condição já ter sido cumprida
        if (obsidiana >= 9 && !portalAvailable) {
            portalAvailable = true;
            portalPromptText.gameObject.SetActive(true);
            portalPromptText.text = "Tens 9 obsidianas. Prime P para abrir o portal.";
        }

        if (portalAvailable && Input.GetKeyDown(KeyCode.P)) {
            CriarPortal();
            portalPromptText.text = "O portal apareceu!";
        }

        // DEBUG: dá 9 obsidianas instantaneamente para testar o portal sem apanhar blocos
        if (Input.GetKeyDown(KeyCode.Z))
            InventoryManagerPF.Instance.AddBlock(BlockPF.BlockType.OBSIDIAN, 9);
    }

    private void CriarPortal() {
        // guarda a instância para que pressionar P outra vez não crie um segundo portal
        if (portalCriado != null) return;

        // coloca o portal à frente do jogador e roda-o para que fique virado para ele,
        // usa -forward porque LookRotation aponta a face Z+ e queremos a face "da frente"
        // do portal a olhar para quem o criou
        Vector3 posicao = playerTransform.position + playerTransform.forward * distanciaSpawnPortal;
        portalCriado = Instantiate(prefabPortal, posicao, Quaternion.LookRotation(-playerTransform.forward));

        // passa as referências ao PortalInteracaoPF em vez de usar Find ou singletons,
        // cada portal fica assim isolado e só conhece o jogador e o ecrã que lhe foram dados
        PortalInteracaoPF interacao = portalCriado.GetComponent<PortalInteracaoPF>();
        if (interacao != null)
            interacao.SetReferences(playerTransform, endGame);
    }
}