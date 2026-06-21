using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class ChatMessage
{
    public string role;
    public string content;

public class ChatRequest
{
    public string model;
    public List<ChatMessage> messages;
    public bool stream;
}


public class ChatResponse
{
    public ChatMessage message;
    public bool done;
}

public class LLMAgent : MonoBehaviour
{
    [SerializeField] private string apiUrl = "http://localhost:11434/api/chat";
    [SerializeField] private string modelName = "qwen2.5:3b";

    private TMP_Text agentReplyText;

    private void Awake()
    {
        agentReplyText = GameObject.FindWithTag("AgentAnswer").GetComponent<TMP_Text>();
    }

    public void Talk(string message)
    {
        agentReplyText.text = "<a pensar...>";
        StartCoroutine(SendToLLM(message));
    }

    private IEnumerator SendToLLM(string userMessage)
    {
        var request = new ChatRequest
        {
            model = modelName,
            stream = false,
            messages = new List<ChatMessage>
            {
                new ChatMessage { role = "user", content = userMessage },
            },
        };

        string json = JsonUtility.ToJson(request);

        using var http = new UnityWebRequest(apiUrl, "POST");
        byte[] body = Encoding.UTF8.GetBytes(json);
        http.uploadHandler = new UploadHandlerRaw(body);
        http.downloadHandler = new DownloadHandlerBuffer();
        http.SetRequestHeader("Content-Type", "application/json");

        yield return http.SendWebRequest();

        if (http.result == UnityWebRequest.Result.Success)
        {
            var response = JsonUtility.FromJson<ChatResponse>(http.downloadHandler.text);
            agentReplyText.text = response.message.content;
        }
        else
        {
            agentReplyText.text = "Erro a contactar o LLM.";
            Debug.LogWarning(http.error);
        }
    }
}
