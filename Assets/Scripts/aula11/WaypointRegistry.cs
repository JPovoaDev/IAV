using System;
using System.Collections.Generic;
using UnityEngine;

// Registry simples nome → Transform. A peça que resolve a tradução semântica
// (vocabulário do jogador → vocabulário interno) é o LLM com o `enum`. Aqui
// só fazemos o último passo: o nome enum → o GameObject concreto.
//
// Os nomes registados aqui têm de bater **exactamente** com os `allowedValues`
// declarados em ToolParam ("torre_norte", etc.). Se houver divergência, o
// resolve falha e a Patrol() vai logar warning sem crashar.

public class WaypointRegistry : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Nome exposto ao LLM (tem de coincidir com o enum da tool).")]
        public string name;
        public Transform transform;
    }

    [SerializeField] private List<Entry> waypoints = new();

    private Dictionary<string, Transform> map;

    private void Awake()
    {
        map = new Dictionary<string, Transform>(waypoints.Count);
        foreach (var e in waypoints)
        {
            if (string.IsNullOrWhiteSpace(e.name) || e.transform == null) continue;
            map[e.name] = e.transform;
        }
    }

    public bool TryResolve(string name, out Transform target)
    {
        if (map == null) Awake();
        return map.TryGetValue(name, out target);
    }

    public IEnumerable<string> KnownNames => map?.Keys ?? new Dictionary<string, Transform>().Keys;
}
