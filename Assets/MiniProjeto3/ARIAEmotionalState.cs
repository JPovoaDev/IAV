using UnityEngine;

public enum ARIAMood { CALM, WARNING, ALERT }

public class ARIAEmotionalState : MonoBehaviour {

    [SerializeField] private Light moodLight;

    private float calmMultiplier = 1.0f;
    private float warningMultiplier = 0.4f;
    private float alertMultiplier = 0.0f; // zero para que o trust congele completamente quando há perigo ativo

    [SerializeField] private Color calmColor = new Color(0.2f, 0.6f, 1f);
    [SerializeField] private Color warningColor = new Color(1f, 0.6f, 0f);
    [SerializeField] private Color alertColor = Color.red;

    // público para outros scripts lerem, mas só SetMood o pode alterar
    public ARIAMood CurrentState = ARIAMood.CALM;

    // o ARIAActions chama este método para saber quanto trust deve adicionar no próximo tick
    // o valor muda consoante o que o robot encontrou na última investigação
    public float GetTrustMultiplier() {
        if (CurrentState == ARIAMood.CALM)
            return calmMultiplier;
        if (CurrentState == ARIAMood.WARNING)
            return warningMultiplier;
        if (CurrentState == ARIAMood.ALERT)
            return alertMultiplier;

        return 1f;
    }

    private void Start() {
        ApplyVisuals();
    }

    public void SetMood(ARIAMood newMood) {
        if (newMood == CurrentState)
            return;

        CurrentState = newMood;
        ApplyVisuals();
        Debug.Log($"[ARIA Mood] -> {CurrentState}");
    }

    // Contains em vez de comparação exata porque o modelo pequeno às vezes devolve
    // a classificação com texto extra à volta, por exemplo "A situação é: ALERT" em vez de só "ALERT"
    // verificamos ALERT antes de WARNING para não haver conflito entre as duas palavras
    // o fallback é WARNING e não CALM porque preferimos errar pelo lado do cuidado
    public static ARIAMood ParseFromLLM(string raw) {
        string cleaned = raw.Trim().ToUpperInvariant();

        if (cleaned.Contains("ALERT"))
            return ARIAMood.ALERT;
        if (cleaned.Contains("WARNING"))
            return ARIAMood.WARNING;
        if (cleaned.Contains("CALM"))
            return ARIAMood.CALM;

        return ARIAMood.WARNING;
    }

    private void ApplyVisuals() {
        if (moodLight == null)
            return;

        if (CurrentState == ARIAMood.CALM)
            moodLight.color = calmColor;
        else if (CurrentState == ARIAMood.WARNING)
            moodLight.color = warningColor;
        else
            moodLight.color = alertColor;

        moodLight.enabled = true;
    }
}