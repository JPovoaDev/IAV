using UnityEngine;

public class PortalInteracaoPF : MonoBehaviour {
    // distância e tecla escondidas no Inspector porque são injetadas pelo PortalManagerPF
    // no momento do Instantiate, não faz sentido expô-las para edição manual quando
    // nunca são configuradas diretamente aqui
    [HideInInspector] private float distanciaInteracao = 5f;
    [HideInInspector] private KeyCode teclaInteragir = KeyCode.F;
    [SerializeField] private GameObject endGame;

    private Transform playerTransform;

    // chamado pelo PortalManagerPF logo a seguir ao Instantiate para ligar o jogador
    // e o ecrã de fim de jogo a este portal concreto, sem depender de Find nem de singletons
    public void SetReferences(Transform player, GameObject endGameObj) {
        playerTransform = player;
        endGame = endGameObj;
    }

    void Start() {
        // fallback para o caso de o portal ser colocado diretamente em cena em vez de
        // ser instanciado pelo PortalManagerPF (onde SetReferences nunca teria sido chamado)
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        playerTransform = player.transform;
    }

    void Update() {
        // avalia a distância todos os frames mas só reage ao input, evita processamento
        // desnecessário quando o jogador ainda está longe
        bool perto = Vector3.Distance(transform.position, playerTransform.position) <= distanciaInteracao;

        if (Input.GetKeyDown(teclaInteragir)) {
            if (perto) {
                TerminarJogo();
            }
        }
    }

    private void TerminarJogo() {
        // ativa o ecrã de fim de jogo, que trata do resto (menus, créditos, etc.)
        endGame.SetActive(true);
    }
}