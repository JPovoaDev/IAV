using UnityEngine;

public class PortalInteracaoPF : MonoBehaviour
{
    [SerializeField] private float distanciaInteracao = 5f;
    [SerializeField] private KeyCode teclaInteragir = KeyCode.F;
    [SerializeField] private GameObject dicaInteracao;
    [SerializeField] private GameObject endGame;

    private Transform playerTransform;

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;

        if (dicaInteracao != null) dicaInteracao.SetActive(false);
    }

    void Update()
    {
        if (playerTransform == null) return;

        bool perto = Vector3.Distance(transform.position, playerTransform.position) <= distanciaInteracao;

        if (dicaInteracao != null) dicaInteracao.SetActive(perto);

        // Log só quando premes F, não a cada frame
        if (Input.GetKeyDown(teclaInteragir))
        {
            Debug.Log("F foi premido. Perto do portal? " + perto);

            if (perto)
            {
                TerminarJogo();
            }
        }
    }

    private void TerminarJogo()
    {
        Debug.Log("TerminarJogo() foi chamado.");
        endGame.SetActive(true);
    }
}