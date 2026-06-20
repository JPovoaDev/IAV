using UnityEngine;

public class CheckpointTriggerPF : MonoBehaviour {
    [Header("Identificação")]
    public int checkpointIndex = 0;
    public bool isGoal = false;

    [Header("Feedback visual")]
    public MeshRenderer indicatorRenderer;
    public Material activatedMaterial;
    public Material defaultMaterial;

    private bool activated = false;
    public bool IsActivated => activated;

    public void Activate() {
        if (activated) return;
        activated = true;
        if (indicatorRenderer != null && activatedMaterial != null)
            indicatorRenderer.material = activatedMaterial;
    }

    public void Reset() {
        activated = false;
        if (indicatorRenderer != null && defaultMaterial != null)
            indicatorRenderer.material = defaultMaterial;
    }
}