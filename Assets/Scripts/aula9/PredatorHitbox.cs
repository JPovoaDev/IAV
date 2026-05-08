using UnityEngine;

public class PredatorHitbox : MonoBehaviour {
    private PreyPredatorArena arena;

    private void Start() {
        arena = transform.parent.parent.gameObject.GetComponent<PreyPredatorArena>();
        Debug.Log("Arena encontrada: " + arena);
    }

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Prey")) {
            Debug.Log("CAPTUROU");
            arena.OnPreyCaptured();
        }
    }
}