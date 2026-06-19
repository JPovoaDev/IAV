using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class PortalManagerPF : MonoBehaviour {
    public TMP_Text portalPromptText;
    private bool portalAvailable = false;

    void Update() {
        int obsidiana = InventoryManagerPF.Instance.Count(BlockPF.BlockType.OBSIDIAN);

        if (obsidiana >= 9 && !portalAvailable) {
            portalAvailable = true;
            portalPromptText.gameObject.SetActive(true);
            portalPromptText.text = "Tens 9 obsidianas. Prime P para abrir o portal.";
        }

        if (portalAvailable && Input.GetKeyDown(KeyCode.P))
            SceneManager.LoadScene("EndScene");
    }
}