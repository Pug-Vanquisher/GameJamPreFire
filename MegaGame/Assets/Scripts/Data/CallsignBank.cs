using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "World/Callsign Bank")]
public class CallsignBank : ScriptableObject
{
    [TextArea] public string rawList; 
    List<string> pool;

    void OnEnable()
    {
        pool = new List<string>();
        if (string.IsNullOrWhiteSpace(rawList)) return;
        var lines = rawList.Split('\n');
        var set = new HashSet<string>();
        foreach (var ln in lines)
        {
            var s = ln.Trim();
            if (s.Length == 0) continue;
            if (set.Add(s)) pool.Add(s);
        }
    }

    public string TakeUnique(System.Random rng, HashSet<string> used)
    {
        if (pool == null || pool.Count == 0) return $"Враг-{Random.Range(100, 999)}";
        for (int i = 0; i < 500; i++)
        {
            string pick = pool[rng.Next(pool.Count)];
            if (used.Add(pick)) return pick;
        }
        return $"Враг-{Random.Range(100, 999)}";
    }
}
