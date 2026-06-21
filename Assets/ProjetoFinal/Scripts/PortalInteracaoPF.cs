using UnityEngine;

public class PortalInteracaoPF : MonoBehaviour {
    [HideInInspector] private float distanciaInteracao = 5f;
    [HideInInspector] private KeyCode teclaInteragir = KeyCode.F;
    [SerializeField] private GameObject endGame;

    private Transform playerTransform;

    public void SetReferences(Transform player, GameObject endGameObj) {
        playerTransform = player;
        endGame = endGameObj;
    }

    void Start() {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        playerTransform = player.transform;

    }

    void Update() {

        bool perto = Vector3.Distance(transform.position, playerTransform.position) <= distanciaInteracao;

        // Log só quando premes F, não a cada frame
        if (Input.GetKeyDown(teclaInteragir)) {

            if (perto) {
                TerminarJogo();
            }
        }
    }
    private void TerminarJogo() {
        endGame.SetActive(true);
    }
}