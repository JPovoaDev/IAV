using UnityEngine;

// Componentes de acção para o NPC. Os métodos públicos sem parâmetros são
// invocados via SendMessage a partir de LLMAgentWithActions, quando o LLM
// emite { "action": "OpenDoor" } ou { "action": "Shoot" }.
public class GuardActions : MonoBehaviour
{
    [SerializeField] private Transform door;
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 2f;

    private bool doorOpen;

    public void OpenDoor()
    {
        if (door == null)
        {
            Debug.Log("OpenDoor: porta não atribuída no Inspector");
            return;
        }
        doorOpen = !doorOpen;
        StopAllCoroutines();
        StartCoroutine(RotateDoor(doorOpen ? openAngle : 0f));
    }

    public void Shoot()
    {
        Debug.Log("Shoot — bang!");
        // Aqui podes instanciar uma flecha, tocar som, etc.
    }

    private System.Collections.IEnumerator RotateDoor(float targetY)
    {
        Quaternion start = door.localRotation;
        Quaternion end = Quaternion.Euler(0, targetY, 0);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * openSpeed;
            door.localRotation = Quaternion.Slerp(start, end, t);
            yield return null;
        }
        door.localRotation = end;
    }
}
