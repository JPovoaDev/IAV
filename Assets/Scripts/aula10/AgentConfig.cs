using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ToolEntry
{
    [Tooltip("Nome da função. Tem de coincidir com um método público sem parâmetros num componente do mesmo GameObject.")]
    public string name = "OpenDoor";

    [TextArea(2, 5)]
    [Tooltip("Descrição clara para o LLM saber QUANDO usar esta tool. É lida pelo LLM via API, não pelo system prompt.")]
    public string description = "Abre o portão. Usar só com visitantes identificados.";
}

[CreateAssetMenu(fileName = "AgentConfig", menuName = "LLM/AgentConfig")]
public class AgentConfig : ScriptableObject
{
    [Header("Personalidade (system prompt)")]
    [TextArea(3, 10)] [SerializeField] private string contextPrompt =
        "Tu és um guarda no portão de um castelo medieval. Falas em português, de forma seca e desconfiada. Não confias em estranhos. Nunca quebras a personagem.";

    [Header("Como responder (tom, comprimento)")]
    [TextArea(3, 10)] [SerializeField] private string answerGuideline =
        "Responde em 1-2 frases, nunca mais. Em português europeu.";

    [Header("Tools — acções declaradas via API")]
    [Tooltip("Cada entry vira uma function declarada no campo tools[] do request ao Ollama. O LLM escolhe se chama, qual chama, e quando.")]
    [SerializeField] private List<ToolEntry> tools = new()
    {
        new ToolEntry { name = "OpenDoor", description = "Abre o portão. Usar só com visitantes identificados ou com nome conhecido." },
        new ToolEntry { name = "Shoot",    description = "Dispara uma flecha de aviso. Usar se o visitante for ameaçador ou recusar identificar-se 3 vezes." },
    };

    [Header("Fallback")]
    [SerializeField] private string defaultErrorAnswer = "Desculpa, não consegui responder.";

    public string ContextPrompt => contextPrompt;
    public string AnswerGuideline => answerGuideline;
    public IReadOnlyList<ToolEntry> Tools => tools;
    public string DefaultErrorAnswer => defaultErrorAnswer;
}
