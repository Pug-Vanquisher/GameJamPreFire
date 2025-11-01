using System.Collections.Generic;
using UnityEngine;

public class WorldState : MonoBehaviour
{
    public static WorldState Instance { get; private set; }

    public float MapHalfSize { get; private set; }

    public class RegionSeed { public int Id; public Vector2 Pos; }
    public List<RegionSeed> Regions = new(); // seeds
    public int GetRegionId(Vector2 p, out float d1, out float d2)
    {
        // возвращает id ближайшего региона
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
    public float BiomeFrequency { get; private set; }
    public Biome SampleBiome(Vector2 p)
    {
        if (!Biomes) return Biome.Plains;
        return Biomes.Evaluate(SampleBiomeRaw(p));
    }
    public float SampleBiomeRaw(Vector2 p)
    {
        float nx = (p.x + MapHalfSize) * BiomeFrequency;
        float ny = (p.y + MapHalfSize) * BiomeFrequency;
        return Mathf.PerlinNoise(nx, ny);
    }

    public NodeData Capital { get; private set; }
    public NodeData PlayerBase { get; private set; }
    public Vector2 PlayerSpawn { get; private set; }
    public readonly List<NodeData> Cities = new();
    public readonly List<NodeData> Camps = new();
    public readonly List<SquadData> EnemySquads = new();

    public class RoadEdge
    {
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

    public void InitGlobals(float half, BiomeBank biomes, float biomeFreq)
    { MapHalfSize = half; Biomes = biomes; BiomeFrequency = biomeFreq; }

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

    public NodeData(string id, string name, NodeType type, Faction faction,
                    Vector2 pos, int regionId, string terrain, bool isCapital = false)
    {
        Id = id;
        Name = name;
        Type = type;
        Faction = faction;
        Pos = pos;
        RegionId = regionId;
        Terrain = terrain;
        IsCapital = isCapital;
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
