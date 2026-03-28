using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour {

    [Header("Movimento")]
    public float walkSpeed = 5f;         // Velocidade horizontal em m/s
    public float jumpHeight = 1.2f;      // Altura máxima do salto em metros
    public float gravity = -20f;         // Aceleração gravitacional (negativo = para baixo)

    [Header("Câmara / Look")]
    public Transform cameraTransform;    // Arrastar o filho Camera aqui
    public float mouseSensitivity = 2f;  // Sensibilidade do rato

    private CharacterController cc;
    private Vector3 velocity;            // Velocidade acumulada (usada para gravidade + salto)
    private float xRotation = 0f;        // Rotação vertical da câmara (clamped)

    [Header("Água")]
    public WorldManager worldManager;
    public float waterGravity = -3f;    // gravidade tipo lua dentro de água
    public float waterSpeed = 3f;     // movimento mais lento na água


    void Start() {
        cc = GetComponent<CharacterController>();

        // Bloquear e esconder o cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update() {
        HandleLook();
        HandleMovement();
    }

    void HandleLook() {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Rotação vertical — aplicada à câmara, não ao corpo
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);  // impede virar ao contrário
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Rotação horizontal — roda o corpo inteiro
        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleMovement() {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        bool grounded = cc.isGrounded;
        bool inWater = IsInWater();

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

    bool IsInWater() {
        if (worldManager == null) return false;

        // pés do jogador (centro - metade da altura)
        Vector3 feetPos = transform.position + Vector3.up * 0.5f;

        int bx = Mathf.FloorToInt(feetPos.x);
        int by = Mathf.FloorToInt(feetPos.y);
        int bz = Mathf.FloorToInt(feetPos.z);

        Vector2Int chunkCoord = new Vector2Int(
            Mathf.FloorToInt((float)bx / Chunk.chunkSize),
            Mathf.FloorToInt((float)bz / Chunk.chunkSize));

        Chunk chunk = worldManager.GetChunk(chunkCoord);
        if (chunk == null) return false;

        int lx = bx - chunkCoord.x * Chunk.chunkSize;
        int ly = by;
        int lz = bz - chunkCoord.y * Chunk.chunkSize;

        if (lx < 0 || lx >= Chunk.chunkSize ||
            ly < 0 || ly >= Chunk.chunkSize ||
            lz < 0 || lz >= Chunk.chunkSize) return false;

        return chunk.chunkData[lx, ly, lz].type == Block.BlockType.WATER;
    }
}