using UnityEngine;
using TMPro;

public class SecretPanelLogs : MonoBehaviour {

    [SerializeField] private TextMeshProUGUI display;

    private string[] logEntries = {
        "> PROJECTO AXIOM — ENTRADA 001\n14.03.2018\nFase de contenção iniciada.",
        "> ENTRADA 002\n15.03.2018\nSistemas autónomos estáveis.\nEquipa em evacuação.",
        "> ENTRADA 003\n16.03.2018\nInstalações evacuadas.\nARIA permanece operacional.",
        "> ENTRADA FINAL\n17.03.2018\nCausa do incidente: CLASSIFICADO.\nARIA — aguarda instruções indefinidamente."
    };

    private int currentEntry = 0;

    private void Start() {
        display.text = "> [CLIQUE PARA LER REGISTOS]";
    }

    private void OnMouseDown() {
        display.text = logEntries[currentEntry];
        currentEntry = (currentEntry + 1) % logEntries.Length;
    }
}