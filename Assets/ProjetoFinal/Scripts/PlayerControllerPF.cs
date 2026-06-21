using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControllerPF : MonoBehaviour {

    [Header("Movimento")]
    public float walkSpeed = 5f;
    public float jumpHeight = 1.2f;
    public float gravity = -20f;

    [Header("Câmara / Look")]
    public Transform cameraTransform;
    public float mouseSensitivity = 2f;

    private CharacterController cc;
    private Vector3 velocity;
    private float xRotation = 0f; // rotação vertical da câmara (clamped)

    [Header("Água")]
    public WorldManagerPF worldManager;
    public float waterGravity = -3f; // gravidade mais leve dentro de água
    public float waterSpeed = 3f; // movimento mais lento na água


    void Start() {
        cc = GetComponent<CharacterController>();

        // bloquear e esconder o cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update() {
        HandleLook();
        HandleMovement();

        // enquanto o jogador está a falar com o agente de apostas o cursor
        // tem de ficar livre para clicar nos botões, por isso saímos daqui sem voltar a trancar o cursor
        if (GamblerNPCLLMPF.DialogoAberto) 
            return; // não tocar no cursor enquanto o diálogo está aberto

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void HandleLook() {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // rotação vertical aplicada à câmara, não ao corpo
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); // impede virar ao contrário
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // rotação horizontal, roda o corpo inteiro
        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleMovement() {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        bool grounded = cc.isGrounded;
        bool inWater = IsInWater();

        // troca os valores de gravidade/velocidade consoante o jogador está dentro de
        // água ou não para a sensação de "flutuar"
        float currentGravity = inWater ? waterGravity : gravity;
        float currentSpeed = inWater ? waterSpeed : walkSpeed;

        // reset vertical ao tocar no chão
        if (grounded && velocity.y < 0f)
            velocity.y = inWater ? -1f : -2f;

        // movimento horizontal
        float h = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
        float v = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);

        Vector3 move = transform.right * h + transform.forward * v;
        if (move.magnitude > 1f) move.Normalize();
        cc.Move(move * currentSpeed * Time.deltaTime);

        // salto normal ou nadar para cima
        if (keyboard.spaceKey.wasPressedThisFrame) {
            if (grounded)
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            else if (inWater)
                velocity.y = 2f;
        }

        velocity.y += currentGravity * Time.deltaTime;
        cc.Move(velocity * Time.deltaTime);
    }

    // pega na posição dos pés do jogador, converte para coordenadas de chunk + coordenadas
    // locais dentro do chunk e vai ver diretamente ao chunkData do ChunkPF se o
    // bloco é água, é o mesmo sistema de coordenadas que o ChunkPF usa para gerar o terreno
    bool IsInWater() {
        // pés do jogador (centro é a metade da altura)
        Vector3 feetPos = transform.position + Vector3.up * 0.5f;

        int bx = Mathf.FloorToInt(feetPos.x);
        int by = Mathf.FloorToInt(feetPos.y);
        int bz = Mathf.FloorToInt(feetPos.z);

        Vector2Int chunkCoord = new Vector2Int(
            Mathf.FloorToInt((float)bx / ChunkPF.chunkSize),
            Mathf.FloorToInt((float)bz / ChunkPF.chunkSize));

        ChunkPF chunk = worldManager.GetChunk(chunkCoord);

        if (chunk == null) 
            return false; // chunk ainda não gerado ou fora de alcance, assume que não há água

        int lx = bx - chunkCoord.x * ChunkPF.chunkSize;
        int ly = by;
        int lz = bz - chunkCoord.y * ChunkPF.chunkSize;

        if (lx < 0 || lx >= ChunkPF.chunkSize || ly < 0 || ly >= ChunkPF.chunkSize || lz < 0 || lz >= ChunkPF.chunkSize) 
            return false;

        return chunk.chunkData[lx, ly, lz].type == BlockPF.BlockType.WATER;
    }

}