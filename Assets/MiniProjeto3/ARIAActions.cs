using Newtonsoft.Json.Linq;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(LLMAgentWithParameterizedActions))]
public class ARIAActions : MonoBehaviour {

    [SerializeField] private GameObject door;

    [SerializeField] private Light alarmLight;
    [SerializeField] private Light trustLight;

    [SerializeField] private GameObject secretPanel;

    [SerializeField] private PatrolRobot patrolRobot;

    [SerializeField] private ARIAInvestigator investigator;
    [SerializeField] private ARIAEmotionalState emotionalState;

    [SerializeField] private float trustThreshold = 100f;
    [SerializeField] private float trustPerMessage = 8f; // valor base antes de ser multiplicado pelo estado emocional da ARIA

    private float trustLevel = 0f;
    private bool doorUnlocked = false; // para de acumular trust depois da porta abrir

    private LLMAgentWithParameterizedActions agent;

    private void Awake() {
        agent = GetComponent<LLMAgentWithParameterizedActions>();
    }

    private void Start() {
        agent.RegisterTool("LockDown", HandleLockDown);
        agent.RegisterTool("ActivateAlarm", HandleActivateAlarm);
        agent.RegisterTool("RevealSecret", HandleRevealSecret);
        agent.RegisterTool("PatrolStop", HandlePatrolStop);
        agent.RegisterTool("PatrolResume", HandlePatrolResume);
        agent.RegisterTool("Investigate", HandleInvestigate);

        secretPanel.SetActive(false);
        alarmLight.enabled = false;
        trustLight.color = Color.red;

        // o trust cresce automaticamente com o tempo, não só quando o jogador fala
        // assim o puzzle tem sempre progresso, mas a velocidade depende do humor da ARIA
        InvokeRepeating(nameof(AutoTrust), 5f, 5f);
    }

    private void AutoTrust() {
        OnMessageReceived();
    }

    public void OnMessageReceived() {
        if (doorUnlocked) return;

        // o multiplicador vem do ARIAEmotionalState e reflete o resultado da última investigação
        // se o robot encontrou perigo (ALERT) o multiplicador é 0 e o trust para completamente
        float multiplier = 1f;
        if (emotionalState != null) {
            multiplier = emotionalState.GetTrustMultiplier();
        }

        trustLevel = Mathf.Clamp(trustLevel + trustPerMessage * multiplier, 0f, trustThreshold);

        float t = trustLevel / trustThreshold;
        trustLight.color = Color.Lerp(Color.red, Color.green, t);

        Debug.Log($"ARIA Trust: {trustLevel}/{trustThreshold} (multiplicador: {multiplier})");

        if (trustLevel >= trustThreshold) {
            doorUnlocked = true;
            door.transform.rotation = Quaternion.Euler(0, -90, 0);
            Debug.Log("ARIA: Acesso concedido.");
        }
    }

    private void HandleLockDown(JObject args) {
        doorUnlocked = false;
        trustLevel = 0f;
        door.transform.rotation = Quaternion.Euler(0, 0, 0);
        trustLight.color = Color.red;
        CancelInvoke(nameof(AutoTrust));
        // o delay de arranque é maior que o normal porque o lockdown é uma punição severa
        InvokeRepeating(nameof(AutoTrust), 10f, 5f);
    }

    private void HandleActivateAlarm(JObject args) {
        // o alarme não reseta tudo como o lockdown, só perde algum trust, quisemos que fosse uma punição mais leve
        trustLevel = Mathf.Clamp(trustLevel - 30f, 0f, trustThreshold);
        float t = trustLevel / trustThreshold;
        trustLight.color = Color.Lerp(Color.red, Color.green, t);
        CancelInvoke(nameof(AutoTrust));

        // delay ainda maior que no lockdown porque o alarme implica que a ARIA está ativamente desconfiada
        InvokeRepeating(nameof(AutoTrust), 15f, 5f);
        StartCoroutine(FlashAlarm());
    }

    private void HandleRevealSecret(JObject args) {
        secretPanel.SetActive(true);
    }

    private void HandlePatrolStop(JObject args) {
        patrolRobot.StopPatrol();
    }

    private void HandlePatrolResume(JObject args) {
        patrolRobot.ResumePatrol();
    }

    private void HandleInvestigate(JObject args) {
        string zone = "";
        if (args != null && args["zone"] != null)
            zone = args["zone"].ToString();
        
        // zona vazia é válida, o ARIAInvestigator sabe escolher uma zona aleatória se não tiver zona
        if (investigator != null)
            investigator.SendRobotTo(zone);
    }

    private IEnumerator FlashAlarm() {
        for (int i = 0; i < 6; i++) {
            alarmLight.color = Color.red;

            alarmLight.enabled = true;
            yield return new WaitForSeconds(0.3f);

            alarmLight.enabled = false;
            yield return new WaitForSeconds(0.3f);
        }

        alarmLight.enabled = false;
    }
}