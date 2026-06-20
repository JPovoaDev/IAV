using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GamblerNPCPF : MonoBehaviour {

    [HideInInspector] public Transform playerTransform;
    [HideInInspector] public GameObject voxelArenaObject;

    public GameObject canvasAgentBetPrefab; // o teu CanvasAgentBet (BettingPanel + InteractHint)
    public GameObject parkourArenaPrefab;
    public GameObject tvPrefab;
    public RenderTexture raceRenderTexture;

    [HideInInspector] public float interactionDistance = 3f;
    [HideInInspector] public KeyCode interactKey = KeyCode.E;
    [HideInInspector] public float arenaSpawnDistance = 2000;

    private string[] robotNames = { "Robot A", "Robot B", "Robot C" };

    // apanhadas por código depois de instanciar o canvasAgentBetPrefab
    private GameObject bettingPanel;
    private TMP_Text dialogueText;
    private TMP_Text resultText;
    private Button buttonRobotA;
    private Button buttonRobotB;
    private Button buttonRobotC;
    private TMP_Text interactHint;

    private bool waitingBet = false;
    private bool racing = false;
    private bool finished = false;
    private int playerBet = -1;
    private int npcBet = -1;

    private GameObject spawnedTV;
    private GameObject spawnedArena;
    private ParkourArenaPF arenaScript;

    void Start() {
        SetupCanvas();

        bettingPanel.SetActive(false);
        resultText.gameObject.SetActive(false);
        interactHint.gameObject.SetActive(false);

        buttonRobotA.onClick.AddListener(() => PlaceBet(0));
        buttonRobotB.onClick.AddListener(() => PlaceBet(1));
        buttonRobotC.onClick.AddListener(() => PlaceBet(2));
    }

    void SetupCanvas() {
        GameObject canvasInstance = Instantiate(canvasAgentBetPrefab);
        canvasInstance.transform.SetParent(transform, false); // mantém a posição local já configurada no prefab

        Transform bettingPanelT = canvasInstance.transform.Find("BettingPanel");
        bettingPanel = bettingPanelT.gameObject;
        dialogueText = bettingPanelT.Find("DialogueText").GetComponent<TMP_Text>();
        resultText = bettingPanelT.Find("ResultText").GetComponent<TMP_Text>();

        Transform buttonsT = bettingPanelT.Find("Buttons");
        buttonRobotA = buttonsT.Find("ButtonRobotA").GetComponent<Button>();
        buttonRobotB = buttonsT.Find("ButtonRobotB").GetComponent<Button>();
        buttonRobotC = buttonsT.Find("ButtonRobotC").GetComponent<Button>();

        interactHint = canvasInstance.transform.Find("InteractHint").GetComponent<TMP_Text>();
    }

    void Update() {
        if (finished) {
            interactHint.gameObject.SetActive(false);
            return;
        }

        bool close = Vector3.Distance(transform.position, playerTransform.position) <= interactionDistance;

        interactHint.gameObject.SetActive(close && !waitingBet && !racing);

        if (close && !waitingBet && !racing && Input.GetKeyDown(interactKey))
            OpenBetting();
    }

    void OpenBetting() {
        waitingBet = true;
        bettingPanel.SetActive(true);
        resultText.gameObject.SetActive(false);
        dialogueText.text = "Aposta num robot. Eu aposto noutro!";

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void PlaceBet(int idx) {
        playerBet = idx;

        npcBet = Random.Range(0, 3);
        while (npcBet == playerBet)
            npcBet = Random.Range(0, 3);

        waitingBet = false;
        bettingPanel.SetActive(false);
        dialogueText.text = "Eu aposto no " + robotNames[npcBet] + "! Boa sorte...";

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        StartRace();
    }

    void StartRace() {
        racing = true;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        float dist = arenaSpawnDistance + Random.Range(-30f, 30f);
        Vector3 arenaPos = transform.position + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

        spawnedArena = Instantiate(parkourArenaPrefab, arenaPos, Quaternion.identity);
        arenaScript = spawnedArena.GetComponentInChildren<ParkourArenaPF>();
        arenaScript.onRaceWinner += OnRaceFinished;

        PlatformSpawnerPF spawner = spawnedArena.GetComponentInChildren<PlatformSpawnerPF>();
        if (spawner != null) spawner.forcedLesson = 1;

        Vector3 tvPos = transform.position + transform.forward * 2.5f + Vector3.up * 3f;
        spawnedTV = Instantiate(tvPrefab, tvPos, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));

        StartCoroutine(SetupCamera(arenaPos));
    }

    IEnumerator SetupCamera(Vector3 arenaPos) {
        yield return null;

        GameObject camObj = new GameObject("RaceCamera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.targetTexture = raceRenderTexture;
        cam.fieldOfView = 75f;

        camObj.transform.position = arenaPos + new Vector3(-25f, 14f, 16f);
        camObj.transform.LookAt(arenaPos + new Vector3(0f, 2f, 16f));

        camObj.transform.SetParent(spawnedArena.transform);
    }

    void OnRaceFinished(int winnerIdx) {
        racing = false;
        bool playerWon = winnerIdx == playerBet;
        bool npcWon = winnerIdx == npcBet;

        bettingPanel.SetActive(true);
        resultText.gameObject.SetActive(true);

        if (playerWon) {
            finished = true;
            dialogueText.text = "O teu " + robotNames[winnerIdx] + " ganhou! Já podes ir buscar a obsidiana à arena.";
            resultText.text = robotNames[winnerIdx] + " venceu!\nGanhaste a aposta.";
            buttonRobotA.gameObject.SetActive(false);
            buttonRobotB.gameObject.SetActive(false);
            buttonRobotC.gameObject.SetActive(false);
            BlockInteractionPF.obsidianUnlocked = true;
            Invoke(nameof(CleanupRace), 5f);

        } else if (npcWon) {
            finished = true;
            dialogueText.text = "O meu " + robotNames[npcBet] + " ganhou! Perdeste, a arena vai desaparecer...";
            resultText.text = robotNames[winnerIdx] + " venceu.\nPerdeste a aposta.";
            Invoke(nameof(DespawnEverything), 5f);
            Invoke(nameof(CleanupRace), 5f);

        } else {
            dialogueText.text = "O " + robotNames[winnerIdx] + " ganhou, mas ninguém apostou nele! Empate — podes tentar outra vez.";
            resultText.text = robotNames[winnerIdx] + " venceu.\nNinguém ganhou — nova aposta disponível!";
            Invoke(nameof(ResetForReBet), 5f);
        }
    }

    void ResetForReBet() {
        arenaScript.onRaceWinner -= OnRaceFinished; arenaScript = null;
        Destroy(spawnedArena); spawnedArena = null;
        Destroy(spawnedTV); spawnedTV = null;

        playerBet = -1;
        npcBet = -1;
        finished = false;

        resultText.gameObject.SetActive(false);
        bettingPanel.SetActive(false);
        buttonRobotA.gameObject.SetActive(true);
        buttonRobotB.gameObject.SetActive(true);
        buttonRobotC.gameObject.SetActive(true);
    }

    void CleanupRace() {
        spawnedArena.SetActive(false);
        Destroy(spawnedTV);
    }

    void DespawnEverything() {
        ChunkPF chunk = voxelArenaObject.GetComponent<ChunkPF>();
        chunk.RemoveObsidian();
        Destroy(gameObject);
    }

    void OnDestroy() {
        arenaScript.onRaceWinner -= OnRaceFinished;
        Destroy(spawnedArena);
        Destroy(spawnedTV);
    }
}