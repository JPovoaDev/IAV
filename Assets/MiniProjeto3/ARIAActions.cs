using Newtonsoft.Json.Linq;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(LLMAgentWithParameterizedActions))]
public class ARIAActions : MonoBehaviour {

    [Header("Door")]
    [SerializeField] private GameObject door;

    [Header("Lights")]
    [SerializeField] private Light alarmLight;
    [SerializeField] private Light trustLight;

    [Header("Secret Panel")]
    [SerializeField] private GameObject secretPanel;

    [Header("Patrol Robot")]
    [SerializeField] private PatrolRobot patrolRobot;

    [Header("Trust")]
    [SerializeField] private float trustThreshold = 100f;
    [SerializeField] private float trustPerMessage = 8f;
    private float trustLevel = 0f;
    private bool doorUnlocked = false;

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

        secretPanel.SetActive(false);
        alarmLight.enabled = false;
        trustLight.color = Color.red;

        InvokeRepeating(nameof(AutoTrust), 5f, 5f);
    }

    private void AutoTrust() {
        OnMessageReceived();
    }

    public void OnMessageReceived() {
        if (doorUnlocked) return;
        trustLevel = Mathf.Clamp(trustLevel + trustPerMessage, 0f, trustThreshold);
        float t = trustLevel / trustThreshold;
        trustLight.color = Color.Lerp(Color.red, Color.green, t);
        Debug.Log($"ARIA Trust: {trustLevel}/{trustThreshold}");
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
        InvokeRepeating(nameof(AutoTrust), 10f, 5f);
    }

    private void HandleActivateAlarm(JObject args) {
        trustLevel = Mathf.Clamp(trustLevel - 30f, 0f, trustThreshold);
        float t = trustLevel / trustThreshold;
        trustLight.color = Color.Lerp(Color.red, Color.green, t);
        CancelInvoke(nameof(AutoTrust));
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