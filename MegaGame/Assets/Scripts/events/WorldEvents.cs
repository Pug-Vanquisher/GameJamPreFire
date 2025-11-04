using System.Collections.Generic;
using UnityEngine;

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

    public readonly struct SquadDied
    {
        public readonly string SquadId;
        public readonly string Callsign;
        public readonly bool IsGarrison;
        public readonly string AnchorNodeId; // для гарнизонов – Id города
        public readonly Vector2 Pos;

        public SquadDied(string squadId, string callsign, bool isGarrison, string anchorNodeId, Vector2 pos)
        {
            SquadId = squadId; Callsign = callsign; IsGarrison = isGarrison; AnchorNodeId = anchorNodeId; Pos = pos;
        }
    }

    public readonly struct GarrisonCountChanged
    {
        public readonly string NodeId;
        public readonly int Remaining;
        public GarrisonCountChanged(string nodeId, int remaining)
        { NodeId = nodeId; Remaining = remaining; }
    }

    public readonly struct SquadMoved
    {
        public readonly string SquadId;
        public readonly Vector2 Pos;
        public readonly bool IsGarrison;

        public SquadMoved(string squadId, Vector2 pos, bool isGarrison)
        { SquadId = squadId; Pos = pos; IsGarrison = isGarrison; }
    }
        public readonly struct VisibleTargetsChanged { }

    public enum NodeKind { City, Camp }

    public readonly struct EnemyPlannedMove
    {
        public readonly string SquadId;
        public readonly string Callsign;
        public readonly NodeKind DestKind;
        public readonly string DestId;
        public readonly UnityEngine.Vector2 DestPos;
        public readonly string DestName;
        public EnemyPlannedMove(string squadId, string callsign, NodeKind kind, string destId, string destName, UnityEngine.Vector2 destPos)
        { SquadId = squadId; Callsign = callsign; DestKind = kind; DestId = destId; DestName = destName; DestPos = destPos; }
    }

    public readonly struct EnemyRetreatDeclared
    {
        public readonly string SquadId;
        public readonly string Callsign;
        public readonly UnityEngine.Vector2 FromPos;
        public readonly string DestId;
        public readonly UnityEngine.Vector2 DestPos;
        public EnemyRetreatDeclared(string squadId, string callsign, UnityEngine.Vector2 fromPos, string destId, UnityEngine.Vector2 destPos)
        { SquadId = squadId; Callsign = callsign; FromPos = fromPos; DestId = destId; DestPos = destPos; }
    }

    public readonly struct PlayerFired
    {
        public readonly Vector2 Pos;
        public PlayerFired(Vector2 pos) { Pos = pos; }
    }

    public readonly struct EnemyHeardShots
    {
        public readonly string SquadId, Callsign;
        public readonly int DirIndex; // 0..7 (N,NE,E,SE,S,SW,W,NW)
        public EnemyHeardShots(string id, string call, int dir) { SquadId = id; Callsign = call; DirIndex = dir; }
    }

    public readonly struct EnemyHelpAccepted
    {
        public readonly string ResponderId, ResponderCallsign, RequesterCallsign;
        public EnemyHelpAccepted(string id, string responder, string requester)
        { ResponderId = id; ResponderCallsign = responder; RequesterCallsign = requester; }
    }

    public enum SupplyKind { Ammo, Meds }

    public readonly struct EnemyResupplied
    {
        public readonly string SquadId, Callsign;
        public readonly NodeKind DestKind;      // City/Camp
        public readonly string DestId, DestName;
        public readonly SupplyKind Kind;
        public readonly int Amount;
        public readonly Vector2 DestPos;

        public EnemyResupplied(string squadId, string callsign, NodeKind destKind, string destId, string destName,
                               SupplyKind kind, int amount, Vector2 destPos)
        {
            SquadId = squadId; Callsign = callsign; DestKind = destKind; DestId = destId; DestName = destName;
            Kind = kind; Amount = amount; DestPos = destPos;
        }
    }

    public readonly struct EnemyEngaged
    {
        public readonly string SquadId, Callsign;
        public EnemyEngaged(string id, string call) { SquadId = id; Callsign = call; }
    }

    // обрати внимание: ReinforcementRequested уже есть; добавь поле CallerCallsign если ещё не было
    public readonly struct ReinforcementRequested
    {
        public readonly string CallerId, CallerCallsign;
        public readonly Vector2 Pos;
        public readonly float Radius;
        public ReinforcementRequested(string id, string call, Vector2 pos, float radius)
        { CallerId = id; CallerCallsign = call; Pos = pos; Radius = radius; }
    }

}
