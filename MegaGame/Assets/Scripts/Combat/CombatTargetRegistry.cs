using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CombatTargetRegistry
{
    // id -> индекс буквы 0..25  (0=A,1=B,...)
    static readonly Dictionary<string, int> idToIndex = new();
    static readonly SortedSet<int> usedIndices = new(); // какие индексы сейчас заняты

    static readonly string[] NATO =
    {
        "ALPHA","BRAVO","CHARLIE","DELTA","ECHO","FOXTROT","GOLF","HOTEL",
        "INDIA","JULIETT","KILO","LIMA","MIKE","NOVEMBER","OSCAR","PAPA",
        "QUEBEC","ROMEO","SIERRA","TANGO","UNIFORM","VICTOR","WHISKEY",
        "XRAY","YANKEE","ZULU"
    };

    public static bool UpdateVisibility(IReadOnlyList<string> visibleIds)
    {
        bool changed = false;
        var visSet = new HashSet<string>(visibleIds);

        var toRemove = idToIndex.Keys.Where(id => !visSet.Contains(id)).ToList();
        foreach (var id in toRemove)
        {
            int idx = idToIndex[id];
            idToIndex.Remove(id);
            usedIndices.Remove(idx);
            changed = true;
        }

        foreach (var id in visibleIds)
        {
            if (idToIndex.ContainsKey(id)) continue;
            int free = NextFreeIndex();
            if (free >= 0)
            {
                idToIndex[id] = free;
                usedIndices.Add(free);
                changed = true;
            }
        }

        return changed;
    }

    static int NextFreeIndex()
    {
        for (int i = 0; i < 26; i++)
            if (!usedIndices.Contains(i)) return i;
        return -1;
    }

    public static bool TryGetIndex(string id, out int idx) => idToIndex.TryGetValue(id, out idx);

    public static string Letter(int idx) => (idx >= 0 && idx < 26) ? ((char)('A' + idx)).ToString() : "?";
    public static string Nato(int idx) => (idx >= 0 && idx < 26) ? NATO[idx] : "TARGET";

    public static List<(string id, int idx)> GetVisibleOrdered()
        => idToIndex.Select(kv => (kv.Key, kv.Value)).OrderBy(p => p.Value).ToList();

    public static void Remove(string id)
    {
        if (idToIndex.TryGetValue(id, out int idx))
        {
            idToIndex.Remove(id);
            usedIndices.Remove(idx);
        }
    }

    public static void Clear()
    {
        idToIndex.Clear();
        usedIndices.Clear();
    }
}
