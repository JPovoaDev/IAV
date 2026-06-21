using UnityEngine;
using TMPro;

public class PortalManagerPF : MonoBehaviour
{
    public TMP_Text portalPromptText;
    [SerializeField] private GameObject prefabPortal;
    [SerializeField] private float distanciaSpawnPortal = 4f;

    private bool portalAvailable = false;
    private GameObject portalCriado;
    private Transform playerTransform;

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;
    }

    void Update()
    {
        int obsidiana = InventoryManagerPF.Instance.Count(BlockPF.BlockType.OBSIDIAN);

        if (obsidiana >= 9 && !portalAvailable)
        {
            portalAvailable = true;
            portalPromptText.gameObject.SetActive(true);
            portalPromptText.text = "Tens 9 obsidianas. Prime P para abrir o portal.";
        }

        if (portalAvailable && Input.GetKeyDown(KeyCode.P))
        {
            CriarPortal();
            portalPromptText.text = "O portal apareceu!";
        }

        // DEBUG: dá 9 obsidianas instantaneamente
        if (Input.GetKeyDown(KeyCode.O))
            InventoryManagerPF.Instance.AddBlock(BlockPF.BlockType.OBSIDIAN, 9);
    }

    private void CriarPortal()
    {
        if (portalCriado != null || prefabPortal == null || playerTransform == null) return;

        Vector3 posicao = playerTransform.position + playerTransform.forward * distanciaSpawnPortal;
        portalCriado = Instantiate(prefabPortal, posicao, Quaternion.LookRotation(-playerTransform.forward));
        // Quaternion.LookRotation(-playerTransform.forward) faz o portal ficar de frente para o jogador
    }
}