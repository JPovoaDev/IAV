using UnityEngine;

using UnityEngine;

public class CheckpointTrigger : MonoBehaviour
{
    [Header("Identificação")]
    public int checkpointIndex = 0;   // ordem crescente: 0, 1, 2...
    public bool isGoal = false;       // última plataforma?

    [Header("Feedback visual")]
    public MeshRenderer indicatorRenderer;
    public Material activatedMaterial;

    private bool activated = false;

    public bool IsActivated => activated;

    public void Activate()
    {
        activated = true;
        if (indicatorRenderer != null && activatedMaterial != null)
            indicatorRenderer.material = activatedMaterial;
    }

    public void Reset()
    {
        activated = false;
        
    }
}