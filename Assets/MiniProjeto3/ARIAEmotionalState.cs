using UnityEngine;

public enum ARIAMood { CALM, WARNING, ALERT }

// Estado emocional da ARIA. Muda consoante o que o robot encontra nas zonas.
// CALM    → tudo normal, trust sobe normalmente, luz azul
// WARNING → algo suspeito, trust sobe mais devagar, luz laranja
// ALERT   → perigo, trust para completamente, luz vermelha
public class ARIAEmotionalState : MonoBehaviour
{
    [Header("Luz de mood (separada da trust light)")]
    [SerializeField] private Light moodLight;

    [Header("Multiplicadores de trust por estado")]
    [SerializeField] private float calmMultiplier = 1.0f;
    [SerializeField] private float warningMultiplier = 0.4f;
    [SerializeField] private float alertMultiplier = 0.0f;

    [Header("Cores")]
    [SerializeField] private Color calmColor = new Color(0.2f, 0.6f, 1f);  // azul
    [SerializeField] private Color warningColor = new Color(1f, 0.6f, 0f);    // laranja
    [SerializeField] private Color alertColor = Color.red;

    public ARIAMood CurrentState { get; private set; } = ARIAMood.CALM;

    public float TrustMultiplier => CurrentState switch
    {
        ARIAMood.CALM => calmMultiplier,
        ARIAMood.WARNING => warningMultiplier,
        ARIAMood.ALERT => alertMultiplier,
        _ => 1f
    };

    private void Start()
    {
        ApplyVisuals();
    }

    public void SetMood(ARIAMood newMood)
    {
        if (newMood == CurrentState) return;
        CurrentState = newMood;
        ApplyVisuals();
        Debug.Log($"[ARIA Mood] → {CurrentState}");
    }

    public static ARIAMood ParseFromLLM(string raw)
    {
        string cleaned = raw.Trim().ToUpperInvariant();
        // Aceita mesmo que o LLM ponha texto extra — procura a palavra
        if (cleaned.Contains("ALERT")) return ARIAMood.ALERT;
        if (cleaned.Contains("WARNING")) return ARIAMood.WARNING;
        if (cleaned.Contains("CALM")) return ARIAMood.CALM;
        return ARIAMood.WARNING; // fallback conservador
    }

    private void ApplyVisuals()
    {
        if (moodLight == null) return;
        moodLight.color = CurrentState switch
        {
            ARIAMood.CALM => calmColor,
            ARIAMood.WARNING => warningColor,
            ARIAMood.ALERT => alertColor,
            _ => calmColor
        };
        moodLight.enabled = true;
    }
}