using System.Collections.Generic;
using UnityEngine;
using static TreeEditor.TreeEditorHelper;

namespace Events
{
    public readonly struct MapGenerated
    {
        public readonly int Seed;
        public MapGenerated(int seed) => Seed = seed;
    }

    public readonly struct RegionCreated
    {
        public readonly int Id;          
        public readonly Rect Bounds;    
        public RegionCreated(int id, Rect bounds) { Id = id; Bounds = bounds; }
    }

    public readonly struct NodeSpawned
    {
        public readonly string Id;       // уникальный id узла
        public readonly string Name;     // имя узла (для городов)
        public readonly Vector2 Pos;
        public readonly int RegionId;
        public readonly NodeType Type;
        public readonly bool IsCapital;
        public NodeSpawned(string id, string name, Vector2 pos, int regionId, NodeType type, bool isCapital = false)
        {
            Id = id; Name = name; Pos = pos; RegionId = regionId; Type = type; IsCapital = isCapital;
        }
    }

    public readonly struct SquadSpawned
    {
        public readonly string Id;
        public readonly Vector2 Pos;
        public readonly int RegionId;
        public readonly int Strength;
        public SquadSpawned(string id, Vector2 pos, int regionId, int strength)
        { Id = id; Pos = pos; RegionId = regionId; Strength = strength; }
    }

    public readonly struct RoadBuilt
    {
        public readonly string A;         // id города A
        public readonly string B;         // id города B
        public readonly List<Vector2> Path; // мировые точки 
        public RoadBuilt(string a, string b, List<Vector2> path)
        { A = a; B = b; Path = path; }
    }

    public readonly struct CityCaptured
    {
        public readonly string Id, Name;
        public readonly int fuel, meds, ammo;
        public CityCaptured(string id, string name, int fuel, int meds, int ammo)
        { Id = id; Name = name; this.fuel = fuel; this.meds = meds; this.ammo = ammo; }
    }

    public readonly struct CampDestroyed
    {
        public readonly string Id, Name;
        public readonly int fuel, meds, ammo;
        public CampDestroyed(string id, string name, int fuel, int meds, int ammo)
        { Id = id; Name = name; this.fuel = fuel; this.meds = meds; this.ammo = ammo; }
    }

    public readonly struct NodeRemoved
    {
        public readonly string Id;
        public NodeRemoved(string id) { Id = id; }
    }
}
