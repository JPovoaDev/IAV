using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

// AgentConfig estendido: cada tool pode declarar parâmetros (com enum) que o
// LLM vai instanciar com valores concretos no `arguments` da resposta.
//
// Diferença para o AgentConfig da aula 10: o método ToToolJson() gera o
// schema JSON dinamicamente (não usamos serialização C# para isto porque
// Newtonsoft trata JObject directamente).

[Serializable]
public class ToolParam
{
    [Tooltip("Nome do parâmetro como vai aparecer em `arguments` na resposta do LLM.")]
    public string name = "target";

    [Tooltip("Tipo JSON Schema. Para esta aula usamos sempre 'string'.")]
    public string type = "string";

    [TextArea(1, 3)]
    [Tooltip("Descrição do parâmetro (lida pelo LLM via API).")]
    public string description = "Destino a alcançar.";

    [Tooltip("Valores permitidos. Vazio = sem restrição (não recomendado para nomes de coisas no mundo).")]
    public List<string> allowedValues = new() { "torre_norte", "torre_sul" };

    [Tooltip("Se true, o LLM tem de fornecer este parâmetro.")]
    public bool required = true;

    public JObject ToSchemaJson()
    {
        var prop = new JObject { ["type"] = type, ["description"] = description };
        if (allowedValues != null && allowedValues.Count > 0)
            prop["enum"] = new JArray(allowedValues);
        return prop;
    }
}

[Serializable]
public class ToolEntryV2
{
    [Tooltip("Nome da função. O handler registado em LLMAgentWithParameterizedActions tem de usar o mesmo nome.")]
    public string name = "GoTo";

    [TextArea(2, 5)]
    [Tooltip("Descrição clara para o LLM saber QUANDO usar esta tool. É lida pelo LLM via API, não pelo system prompt.")]
    public string description = "Anda até um ponto conhecido do mapa. Usar quando o jogador pedir explicitamente para ir, patrulhar, visitar ou inspeccionar um local.";

    [Tooltip("Parâmetros aceites pela tool. Vazio = tool sem argumentos (compatível com aula 10).")]
    public List<ToolParam> parameters = new()
    {
        new ToolParam {
            name = "target",
            allowedValues = new List<string> { "torre_norte", "torre_sul", "porta", "fogueira" },
            description = "Local a alcançar."
        },
    };

    public JObject ToToolJson()
    {
        var properties = new JObject();
        var required   = new JArray();

        foreach (var p in parameters)
        {
            properties[p.name] = p.ToSchemaJson();
            if (p.required) required.Add(p.name);
        }

        var paramSchema = new JObject
        {
            ["type"] = "object",
            ["properties"] = properties,
        };
        if (required.Count > 0) paramSchema["required"] = required;

        return new JObject
        {
            ["type"] = "function",
            ["function"] = new JObject
            {
                ["name"] = name,
                ["description"] = description,
                ["parameters"] = paramSchema,
            },
        };
    }
}

[CreateAssetMenu(fileName = "AgentConfigV2", menuName = "LLM/AgentConfigV2")]
public class AgentConfigV2 : ScriptableObject
{
    [Header("Personalidade (system prompt)")]
    [TextArea(3, 10)] [SerializeField] private string contextPrompt =
        "Tu és um guarda velho num portão de pedra. Falas em português, de forma seca. " +
        "Estás cansado de estar parado — se o jogador parecer perdido, ofereces-te para " +
        "patrulhar ou para ir ver algo, mas sem enumerar a tua lista de tarefas como um menu.";

    [Header("Como responder (tom, comprimento)")]
    [TextArea(3, 10)] [SerializeField] private string answerGuideline =
        "Responde em 1-2 frases, em português europeu, sempre em personagem.";

    [Header("Tools — declaradas via API com schema")]
    [SerializeField] private List<ToolEntryV2> tools = new()
    {
        new ToolEntryV2 {
            name = "GoTo",
            description = "Anda até um ponto conhecido do mapa.",
            parameters = new List<ToolParam> {
                new ToolParam {
                    name = "target",
                    allowedValues = new List<string> { "torre_norte", "torre_sul", "porta", "fogueira" },
                    description = "Local de destino.",
                },
            },
        },
        new ToolEntryV2 {
            name = "OpenDoor",
            description = "Abre o portão. Usar só com visitantes identificados.",
            parameters = new List<ToolParam>(),
        },
        new ToolEntryV2 {
            name = "Shoot",
            description = "Dispara uma flecha de aviso numa direcção. Usar se a situação for ameaçadora.",
            parameters = new List<ToolParam> {
                new ToolParam {
                    name = "direction",
                    allowedValues = new List<string> { "frente", "ar", "chao" },
                    description = "Direcção do disparo.",
                },
            },
        },
    };

    [Header("Fallback")]
    [SerializeField] private string defaultErrorAnswer = "Desculpa, não consegui responder.";

    public string ContextPrompt    => contextPrompt;
    public string AnswerGuideline  => answerGuideline;
    public IReadOnlyList<ToolEntryV2> Tools => tools;
    public string DefaultErrorAnswer => defaultErrorAnswer;

    public bool IsDeclared(string toolName)
    {
        foreach (var t in tools)
            if (t.name == toolName) return true;
        return false;
    }
}
