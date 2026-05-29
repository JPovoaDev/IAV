using UnityEngine;

// Permite seleccionar um NPC com clique. NPCs têm de ter a tag "SmartAgent".
// Anexar à Camera principal.
public class NPCSelector : MonoBehaviour
{
    private Camera cam;
    private GameObject selectedNPC;

    private void Awake() { cam = Camera.main; }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit) && hit.collider.CompareTag("SmartAgent"))
        {
            selectedNPC = hit.collider.gameObject;
            Debug.Log($"NPC seleccionado: {selectedNPC.name}");
        }
    }

    public GameObject GetSelectedNPC() => selectedNPC;
}
