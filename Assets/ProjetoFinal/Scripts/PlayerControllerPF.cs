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

    bool IsInWater() {
        // pés do jogador (centro é a metade da altura)
        Vector3 feetPos = transform.position + Vector3.up * 0.5f;

        // apanhamos as posições do bloco em coordenadas globais e como o jogador pode ter coordenadas float convertemos para baixo em int
        int bx = Mathf.FloorToInt(feetPos.x);
        int by = Mathf.FloorToInt(feetPos.y);
        int bz = Mathf.FloorToInt(feetPos.z);

        // agora apanhamos o chunk a que esse bloco pertence usando novamente o floor porque também podem haver coordenadas negativas
        // por ex bloco -3 / 16 daria 0 se fosse para converter para int em vez de -1
        Vector2Int chunkCoord = new Vector2Int(
            Mathf.FloorToInt((float)bx / ChunkPF.chunkSize),
            Mathf.FloorToInt((float)bz / ChunkPF.chunkSize));

        ChunkPF chunk = worldManager.GetChunk(chunkCoord);

        if (chunk == null) 
            return false; // chunk ainda não gerado ou fora de alcance, assume que não há água

        // agora apanhamos as coordenadas locais da posição do bloco dentro do chunk entre 0 e 15
        // calculamos a partir da subtração da posição do canto do chunk em coords globais à posição global do bloco

        // exemplo: bloco global 23, chunk 1. canto do chunk em 1*16 = 16 então lx = 23 -16 = 7
        // isto é importante porque o array chunkData de cada chunk é sempre indexado de 0 a chunkSize-1 e então
        // não sabe nada de coords globais, apenas que o bloco dele é o número 7
        int lx = bx - chunkCoord.x * ChunkPF.chunkSize;
        int ly = by;
        int lz = bz - chunkCoord.y * ChunkPF.chunkSize;

        // se por algum motivo ly (a altura) sair fora de [0, chunkSize] evitamos um erro ao aceder ao array
        if (lx < 0 || lx >= ChunkPF.chunkSize || ly < 0 || ly >= ChunkPF.chunkSize || lz < 0 || lz >= ChunkPF.chunkSize) 
            return false;

        return chunk.chunkData[lx, ly, lz].type == BlockPF.BlockType.WATER;
    }

}