using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class LLMAgentWithPersonality : MonoBehaviour
{
    [SerializeField] private string apiUrl = "http://localhost:11434/api/chat";
    [SerializeField] private string modelName = "llama3.2:1b";

    [TextArea(4, 12)]
    [SerializeField] private string systemPrompt =
        "Tu és um guarda no portão de um castelo medieval. Falas em português, de forma seca e desconfiada. Não confias em estranhos. Nunca quebras a personagem. Respondes em 1-2 frases, nunca mais.";

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
                new ChatMessage { role = "system", content = systemPrompt },
                new ChatMessage { role = "user",   content = userMessage },
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
