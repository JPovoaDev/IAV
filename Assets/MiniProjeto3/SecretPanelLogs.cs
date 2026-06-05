using UnityEngine;
using TMPro;

public class SecretPanelLogs : MonoBehaviour {

    [SerializeField] private TextMeshProUGUI display;

    // estes logs são a única lore explícita do jogo
    // a ARIA nunca conta o que aconteceu no Projecto AXIOM, o jogador só descobre se explorar este painel
    // as datas de 14 a 17 de março mostram que o colapso foi muito rápido, sem precisar de explicação
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

    // OnMouseDown funciona em colliders 3D sem precisar de EventSystem nem raycasts manuais
    // é mais simples para um objeto físico no mundo do que usar UI
    private void OnMouseDown() {
        display.text = logEntries[currentEntry];
        // o mod faz o índice voltar ao início automaticamente quando passa o último log
        currentEntry = (currentEntry + 1) % logEntries.Length;
    }
}