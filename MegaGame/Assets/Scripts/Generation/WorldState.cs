using System.Collections.Generic;
using UnityEngine;

public class WorldState : MonoBehaviour
{
    public static WorldState Instance { get; private set; }

    public float MapHalfSize { get; private set; }

    public class RegionSeed { public int Id; public Vector2 Pos; }
    public List<RegionSeed> Regions = new(); 
    public int GetRegionId(Vector2 p, out float d1, out float d2)
    {
        int best = -1, second = -1; d1 = float.MaxValue; d2 = float.MaxValue;
        for (int i = 0; i < Regions.Count; i++)
        {
            float dsq = (p - Regions[i].Pos).sqrMagnitude;
            if (dsq < d1) { d2 = d1; second = best; d1 = dsq; best = i; }
            else if (dsq < d2) { d2 = dsq; second = i; }
        }
        d1 = Mathf.Sqrt(d1); d2 = Mathf.Sqrt(d2);
        return best;
    }
    public int GetRegionId(Vector2 p) { float a, b; return GetRegionId(p, out a, out b); }

    public BiomeBank Biomes { get; private set; }

    float f1, f2, blend;
    Vector2 off1, off2;
    public float SampleBiomeRaw(Vector2 p)
    {
        float n1 = Mathf.PerlinNoise((p.x + MapHalfSize + off1.x) * f1,
                                     (p.y + MapHalfSize + off1.y) * f1);
        float n2 = Mathf.PerlinNoise((p.x + MapHalfSize + off2.x) * f2,
                                     (p.y + MapHalfSize + off2.y) * f2);
        return Mathf.Clamp01(Mathf.Lerp(n1, n2, blend));
    }

    public Biome SampleBiome(Vector2 p)
    {
        if (!Biomes) return Biome.Plains;
        return Biomes.Evaluate(SampleBiomeRaw(p));
    }

    public NodeData Capital { get; private set; }
    public NodeData PlayerBase { get; private set; }
    public Vector2 PlayerSpawn { get; private set; }
    public readonly List<NodeData> Cities = new();
    public readonly List<NodeData> Camps = new();
    public readonly List<SquadData> EnemySquads = new();

    public class RoadEdge
    {
        public int Id;
        public string A, B;            
        public List<Vector2> Path;     
    }
    public readonly List<RoadEdge> Roads = new();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void InitGlobals(float half, BiomeBank biomes, float freq1, float freq2, float mix, int seed)
    {
        MapHalfSize = half;
        Biomes = biomes;
        f1 = Mathf.Max(1e-6f, freq1);
        f2 = Mathf.Max(1e-6f, freq2);
        blend = Mathf.Clamp01(mix);

        // оффсеты
        var rng = new System.Random(seed);
        off1 = new Vector2((float)rng.NextDouble() * 1000f, (float)rng.NextDouble() * 1000f);
        off2 = new Vector2((float)rng.NextDouble() * 1000f, (float)rng.NextDouble() * 1000f);
    }

    public void SetCapital(NodeData node) => Capital = node;
    public void SetPlayerBase(NodeData node, Vector2 spawn) { PlayerBase = node; PlayerSpawn = spawn; }

    public string DirectionFromCapital(Vector2 p)
    {
        if (Capital == null) return "Ч";
        var v = (p - Capital.Pos).normalized;
        float deg = (Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg + 360f) % 360f;
        string[] dirs = { "восток", "северо-восток", "север", "северо-запад", "запад", "юго-запад", "юг", "юго-восток" };
        return dirs[Mathf.RoundToInt(deg / 45f) % 8];
    }

    public NodeData FindCityById(string id) => Cities.Find(n => n.Id == id);
    public NodeData FindCampById(string id) => Camps.Find(n => n.Id == id);
    public NodeData FindNearestNode(Vector2 pos, float maxDist, out bool isCamp)
    {
        isCamp = false;
        NodeData best = null; float bestD2 = maxDist * maxDist;
        foreach (var n in Cities)
        {
            float d2 = (n.Pos - pos).sqrMagnitude;
            if (d2 < bestD2) { bestD2 = d2; best = n; isCamp = false; }
        }
        foreach (var n in Camps)
        {
            float d2 = (n.Pos - pos).sqrMagnitude;
            if (d2 < bestD2) { bestD2 = d2; best = n; isCamp = true; }
        }
        return best;
    }
}

[System.Serializable]
public class NodeData
{
    public string Id;
    public string Name;
    public NodeType Type;
    public Faction Faction;
    public Vector2 Pos;
    public int RegionId;
    public string Terrain;
    public bool IsCapital;

    public bool IsCaptured;  
    public bool IsDestroyed;  
    public int Garrison;     

    public int Fuel;
    public int Meds;
    public int Ammo;

    public NodeData(string id, string name, NodeType type, Faction faction,
                    Vector2 pos, int regionId, string terrain, bool isCapital = false)
    {
        Id = id; Name = name; Type = type; Faction = faction;
        Pos = pos; RegionId = regionId; Terrain = terrain; IsCapital = isCapital;
    }
}

[System.Serializable]
public class SquadData
{
    public string Id;
    public Vector2 Pos;
    public int RegionId;
    public int Strength;

    public SquadData(string id, Vector2 pos, int regionId, int strength)
    {
        Id = id;
        Pos = pos;
        RegionId = regionId;
        Strength = strength;
    }
}
