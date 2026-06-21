using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Classes simples que representam o JSON que enviamos e recebemos do Ollama.
[Serializable]
public class ChatMessage {
    public string role;
    public string content;
}

[Serializable]
public class ChatRequest {
    public string model;
    public bool stream;
    public string format;
    public List<ChatMessage> messages;
}

[Serializable]
public class ChatResponseMessage {
    public string role;
    public string content;
}

[Serializable]
public class ChatResponse {
    public ChatResponseMessage message;
}

// fala com o Ollama, chama EnviarMensagem(...) e guarda a resposta em "resposta"
public class OllamaClientPF : MonoBehaviour {
    [SerializeField] private string apiUrl = "http://localhost:11434/api/chat";
    [SerializeField] private string modelName = "qwen2.5:3b"; // modelo pequeno para correr sem problemas

    // o GamblerNPCLLMPF faz polling a isto (while (ollama.aPedir) yield return null)
    // em vez de usar um evento ou callback, é mais simples mas só funciona bem porque só há um pedido de cada vez por NPC
    public bool aPedir = false;
    public string resposta = "";

    public void EnviarMensagem(string systemPrompt, string userMessage) {
        StartCoroutine(SendToLLM(systemPrompt, userMessage));
    }

    // corre numa coroutine porque um pedido de rede demora (mesmo sendo local, o modelo
    // tem de gerar a resposta) e isto não pode travar o frame do jogo enquanto espera
    private IEnumerator SendToLLM(string systemPrompt, string userMessage) {
        aPedir = true;
        resposta = "";

        // monta o pedido tal como o Ollama espera: modelo, stream desligado (queremos
        // a resposta toda de uma vez, não aos bocadinhos), e formato "json" para o
        // obrigar a devolver sempre JSON válido
        ChatRequest request = new ChatRequest {
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

        // aqui é onde a coroutine fica à espera da resposta do Ollama enquanto o resto do jogo continua a
        // correr normalmente
        yield return http.SendWebRequest();

        if (http.result == UnityWebRequest.Result.Success) {
            // desempacota o JSON de resposta e fica só com o texto que o modelo escreveu
            ChatResponse response = JsonUtility.FromJson<ChatResponse>(http.downloadHandler.text);
            resposta = response.message.content;
        } else {
            // se o Ollama não estiver a correr, ou a rede falhar, fica registado na consola
            // e devolve string vazia (o agente de apostas depois diz que não tem interesse no jogador)
            Debug.LogWarning("Erro a contactar o LLM: " + http.error);
            resposta = "";
        }

        aPedir = false;
    }
}