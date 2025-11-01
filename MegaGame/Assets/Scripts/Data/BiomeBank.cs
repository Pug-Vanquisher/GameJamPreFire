using UnityEngine;

public enum Biome { Plains, Forest, Swamp, Hills } 

[CreateAssetMenu(menuName = "World/Biome Bank")]
public class BiomeBank : ScriptableObject
{
    [System.Serializable]
    public class Rule
    {
        public Biome biome;
        [Range(0f, 1f)] public float min = 0f;
        [Range(0f, 1f)] public float max = 1f;
        public Color color = Color.white;
    }

    public Rule[] rules;

    public Biome Evaluate(float v)
    {
        if (rules == null || rules.Length == 0) return Biome.Plains;
        for (int i = 0; i < rules.Length; i++)
            if (v >= rules[i].min && v < rules[i].max) return rules[i].biome;
        return rules[rules.Length - 1].biome;
    }

    public bool CloseToBoundary(float v, float eps)
    {
        if (rules == null) return false;
        foreach (var r in rules)
        {
            if (Mathf.Abs(v - r.min) < eps) return true;
            if (Mathf.Abs(v - r.max) < eps) return true;
        }
        return false;
    }

    public Color ColorOf(Biome b)
    {
        foreach (var r in rules) if (r.biome == b) return r.color;
        return Color.magenta;
    }
}
