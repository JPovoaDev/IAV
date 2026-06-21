using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Classes simples que representam o JSON que enviamos e recebemos do Ollama.
[Serializable]
public class ChatMessage
{
    public string role;
    public string content;
}

[Serializable]
public class ChatRequest
{
    public string model;
    public bool stream;
    public string format;
    public List<ChatMessage> messages;
}

[Serializable]
public class ChatResponseMessage
{
    public string role;
    public string content;
}

[Serializable]
public class ChatResponse
{
    public ChatResponseMessage message;
}

// Fala com o Ollama. Chama EnviarMensagem(...) e ele guarda a resposta em "resposta".
public class OllamaClientPF : MonoBehaviour
{
    [SerializeField] private string apiUrl = "http://localhost:11434/api/chat";
    [SerializeField] private string modelName = "qwen2.5:3b";

    public bool aPedir = false;
    public string resposta = "";

    public void EnviarMensagem(string systemPrompt, string userMessage)
    {
        StartCoroutine(SendToLLM(systemPrompt, userMessage));
    }

    private IEnumerator SendToLLM(string systemPrompt, string userMessage)
    {
        aPedir = true;
        resposta = "";

        ChatRequest request = new ChatRequest
        {
            model = modelName,
            stream = false,
            format = "json",
            messages = new List<ChatMessage>
            {
                new ChatMessage { role = "system", content = systemPrompt },
                new ChatMessage { role = "user", content = userMessage },
            },
        };

        string json = JsonUtility.ToJson(request);
        byte[] body = Encoding.UTF8.GetBytes(json);

        UnityWebRequest http = new UnityWebRequest(apiUrl, "POST");
        http.uploadHandler = new UploadHandlerRaw(body);
        http.downloadHandler = new DownloadHandlerBuffer();
        http.SetRequestHeader("Content-Type", "application/json");

        yield return http.SendWebRequest();

        if (http.result == UnityWebRequest.Result.Success)
        {
            ChatResponse response = JsonUtility.FromJson<ChatResponse>(http.downloadHandler.text);
            resposta = response.message.content;
        }
        else
        {
            Debug.LogWarning("Erro a contactar o LLM: " + http.error);
            resposta = "";
        }

        aPedir = false;
    }
}