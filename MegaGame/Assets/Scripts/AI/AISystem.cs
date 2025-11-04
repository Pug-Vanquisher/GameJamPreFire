using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Events;

public class AISystem : MonoBehaviour
{
    [Header("Tick")]
    [SerializeField] private float tickHz = 5f;

    [Header("Engage / Pursue")]
    [SerializeField] private float pursueTimeCoward = 3f;
    [SerializeField] private float pursueTimeNeutral = 7f;
    [SerializeField] private float pursueTimeAggro = 12f;
    [SerializeField] private float engageStandOff = 250f;      // дистанция «держим круг»
    [SerializeField] private float strafeLateralSpeed = 0.45f; // поперечная скорость (доля Speed)
    [SerializeField] private float strafeJitterAmplitude = 8f; // дрожание

    [Header("Separation")]
    [SerializeField] private float separationRadius = 100f;
    [SerializeField] private float separationPushPerSecond = 250f;

    [Header("Shooting")]
    [SerializeField] private Vector2 shotInterval = new Vector2(1.2f, 2.2f); // интервал для отряда
    [SerializeField, Range(0f, 1f)] private float enemyHitChance = 0.35f;
    [SerializeField] private Vector2Int baseDamage = new Vector2Int(4, 9);
    [SerializeField] private float damageMultiplier = 1.0f;
    [SerializeField] private int maxShotsPerTick = 2;
    [SerializeField] private float initialStaggerMax = 0.7f;

    [Header("Limits")]
    [SerializeField] private int maxMobilePerRegion = 5; // без гарнизонов
    [SerializeField] private int maxChasers = 5;         // кто одновременно «пасёт» игрока

    [Header("Help / Reinforcement")]
    [SerializeField] private float helpBroadcastRadius = 2500f;
    [SerializeField] private float helpResponseRadius = 2500f;
    [SerializeField] private float helpPursueTime = 12f;        // сколько «ищем» по вызову
    [SerializeField, Range(0, 1)] private float cowardRetreatProb = 0.70f;

    [Header("React to shots")]
    [SerializeField] private float hearShotRadius = 2200f;
    [SerializeField, Range(0, 1)] private float hearShotRespondProb = 0.50f; // базовый шанс
    [SerializeField] private float investigateTime = 8f;                     // сколько «проверяем точку»

    [Header("Resupply on arrival")]
    [SerializeField, Range(0, 1)] private float resupplyProb = 0.25f; // шанс пополнить
    [SerializeField] private Vector2Int resupplyAmmoRange = new Vector2Int(6, 14);
    [SerializeField] private Vector2Int resupplyMedsRange = new Vector2Int(1, 3);

    [Header("Debug Movement Broadcast")]
    [SerializeField] private bool debugBroadcastMovement = true;
    [SerializeField] private float movementBroadcastHz = 3f;

    // таймеры стрельбы (пер-отряд)
    private readonly Dictionary<string, float> nextFireAt = new();
    private readonly Dictionary<string, float> lastBroadcastAt = new();

    class GraphEdge
    {
        public string to;
        public float w;
        public List<Vector2> path;
    }

    Dictionary<string, List<GraphEdge>> G;
    float tickAccum;

    void OnEnable()
    {
        EventBus.Subscribe<MapGenerated>(OnMapGenerated);
        EventBus.Subscribe<ReinforcementRequested>(OnReinforcementRequested);
        EventBus.Subscribe<SquadDied>(OnSquadDied);
        EventBus.Subscribe<PlayerFired>(OnPlayerFired);               // << новое
        BuildGraph();
    }
    void OnDisable()
    {
        EventBus.Unsubscribe<MapGenerated>(OnMapGenerated);
        EventBus.Unsubscribe<ReinforcementRequested>(OnReinforcementRequested);
        EventBus.Unsubscribe<SquadDied>(OnSquadDied);
        EventBus.Unsubscribe<PlayerFired>(OnPlayerFired);
    }

    void OnMapGenerated(MapGenerated _)
    {
        BuildGraph();
        var ws = WorldState.Instance; if (!ws) return;
        foreach (var s in ws.EnemySquads)
        {
            if (s.IsGarrison) { s.State = AIState.Idle; s.TargetNodeId = s.AnchorNodeId; continue; }
            PickNewTarget(s);
        }
    }

    void BuildGraph()
    {
        G = new Dictionary<string, List<GraphEdge>>();
        var ws = WorldState.Instance; if (!ws) return;

        void AddNode(string id) { if (!G.ContainsKey(id)) G[id] = new List<GraphEdge>(); }
        if (ws.Capital) AddNode(ws.Capital.Id);
        foreach (var c in ws.Cities) AddNode(c.Id);

        foreach (var r in ws.Roads)
        {
            if (!G.ContainsKey(r.A)) G[r.A] = new List<GraphEdge>();
            if (!G.ContainsKey(r.B)) G[r.B] = new List<GraphEdge>();
            float w = 0f;
            for (int i = 1; i < r.Path.Count; i++) w += Vector2.Distance(r.Path[i - 1], r.Path[i]);

            G[r.A].Add(new GraphEdge { to = r.B, w = w, path = r.Path });
            var rev = new List<Vector2>(r.Path); rev.Reverse();
            G[r.B].Add(new GraphEdge { to = r.A, w = w, path = rev });
        }
    }

    void Update()
    {
        tickAccum += Time.deltaTime * tickHz;
        while (tickAccum >= 1f)
        {
            tickAccum -= 1f;
            StepAI();
        }
    }

    void StepAI()
    {
        int shotsThisTick = 0;
        var ws = WorldState.Instance; if (!ws) return;
        Vector2 player = PlayerState.Pos;
        float dt = 1f / Mathf.Max(1e-4f, tickHz);

        // кого допускаем «в хвост»
        var allowedChasers = new HashSet<string>();
        {
            var list = new List<(SquadData s, float d)>();
            foreach (var q in ws.EnemySquads)
            {
                if (q.IsGarrison) continue;
                float d = Vector2.Distance(q.Pos, player);
                if (d <= q.DetectionRadius) list.Add((q, d));
            }
            list.Sort((a, b) => a.d.CompareTo(b.d));
            for (int i = 0; i < list.Count && i < maxChasers; i++) allowedChasers.Add(list[i].s.Id);
        }

        EnforceRegionCap(ws);

        foreach (var s in ws.EnemySquads.ToList())
        {
            Vector2 oldPos = s.Pos;
            bool wasInCombat = s.InCombat;

            // === ГАРНИЗОН ===
            if (s.IsGarrison)
            {
                float d = Vector2.Distance(s.Pos, player);
                s.InCombat = (d <= s.DetectionRadius);
                s.State = s.InCombat ? AIState.Engage : AIState.Idle;

                if (s.InCombat && shotsThisTick < maxShotsPerTick)
                    if (TryEnemyFire(s)) shotsThisTick++;

                // событие «вступили в бой» — фикс
                if (!wasInCombat && s.InCombat)
                    EventBus.Publish(new EnemyEngaged(s.Id, s.Callsign));

                MaybeBroadcastMoved(s, oldPos);
                continue;
            }

            // === МОБИЛЬНЫЕ ===
            float distToPlayer = Vector2.Distance(s.Pos, player);

            if (distToPlayer <= s.DetectionRadius && allowedChasers.Contains(s.Id))
            {
                s.State = AIState.Engage; s.InCombat = true;

                // трус может отступить
                if (s.Persona == EnemyPersonality.Cowardly && distToPlayer < engageStandOff * 0.8f && Random.value < cowardRetreatProb)
                {
                    var far = FarthestNodeFrom(player);
                    if (far != null)
                    {
                        EventBus.Publish(new EnemyRetreatDeclared(s.Id, s.Callsign, s.Pos, far.Id, far.Pos));
                        s.TargetNodeId = far.Id;
                        s.State = AIState.Return; s.InCombat = false;
                        s.Path = null; s.PathIndex = 0;
                    }
                }
                else
                {
                    if (distToPlayer > engageStandOff)
                    {
                        Vector2 dir = (player - s.Pos).normalized;
                        s.Pos += dir * (s.Speed * 0.6f * dt);
                        if (shotsThisTick < maxShotsPerTick && TryEnemyFire(s)) shotsThisTick++;
                    }
                    else
                    {
                        Vector2 toPl = (player - s.Pos);
                        Vector2 perp = new Vector2(-toPl.y, toPl.x).normalized;
                        s.Pos += perp * (s.Speed * strafeLateralSpeed * dt)
                              + (Vector2)Random.insideUnitCircle * strafeJitterAmplitude * dt;
                        if (shotsThisTick < maxShotsPerTick && TryEnemyFire(s)) shotsThisTick++;
                    }
                }
            }
            else if (distToPlayer <= s.DetectionRadius && !allowedChasers.Contains(s.Id))
            {
                // слишком много преследователей
                s.InCombat = false;
                if (string.IsNullOrEmpty(s.TargetNodeId)) PickNewTarget(s);
                s.State = AIState.Return;
            }
            else if (s.InCombat)
            {
                s.InCombat = false;
                float chaseTime =
                    s.Persona == EnemyPersonality.Cowardly ? pursueTimeCoward :
                    s.Persona == EnemyPersonality.Aggressive ? pursueTimeAggro : pursueTimeNeutral;
                s.PursueUntil = Time.time + chaseTime;
                s.State = AIState.Pursue;
            }

            // движение вне боя
            if (!s.InCombat)
            {
                if (s.State == AIState.Pursue)
                {
                    if (Time.time <= s.PursueUntil)
                    {
                        Vector2 dir = (player - s.Pos).normalized;
                        s.Pos += dir * (s.Speed * dt);
                    }
                    else
                    {
                        s.State = AIState.Return;
                        s.Path = null; s.PathIndex = 0;
                    }
                }

                if (s.State == AIState.Return || s.State == AIState.Patrol || s.State == AIState.Idle)
                {
                    // построение пути при необходимости
                    if (s.Path == null || s.PathIndex >= s.Path.Count)
                    {
                        if (string.IsNullOrEmpty(s.TargetNodeId)) PickNewTarget(s);
                        BuildPathToTarget(s);
                    }

                    // движение по пути
                    if (s.Path != null && s.PathIndex < s.Path.Count)
                    {
                        Vector2 tgt = s.Path[s.PathIndex];
                        float step = s.Speed * dt;
                        Vector2 to = tgt - s.Pos;
                        if (to.magnitude <= step)
                        {
                            s.Pos = tgt;
                            s.PathIndex++;

                            // цель достигнута
                            if (s.PathIndex >= s.Path.Count)
                            {
                                // пробуем пополнить узел
                                TryResupplyOnArrival(s);

                                s.State = AIState.Patrol;
                                s.TargetNodeId = null;
                            }
                        }
                        else
                        {
                            s.Pos += to.normalized * step;
                        }
                    }
                }
            }

            ApplySeparation(ws, s, dt);

            // событие «вступили в бой»
            if (!wasInCombat && s.InCombat)
                EventBus.Publish(new EnemyEngaged(s.Id, s.Callsign));

            MaybeBroadcastMoved(s, oldPos);
        }
    }

    void ApplySeparation(WorldState ws, SquadData s, float dt)
    {
        if (s.IsGarrison) return;
        Vector2 push = Vector2.zero;
        foreach (var o in ws.EnemySquads)
        {
            if (o == s || o.IsGarrison) continue;
            Vector2 d = s.Pos - o.Pos;
            float dist = d.magnitude;
            if (dist < 1e-3f) { push += (Vector2)Random.insideUnitCircle; continue; }
            if (dist < separationRadius)
            {
                float k = (separationRadius - dist) / separationRadius;
                push += d.normalized * k;
            }
        }
        if (push.sqrMagnitude > 1e-6f)
            s.Pos += push.normalized * (separationPushPerSecond * dt);
    }

    void MaybeBroadcastMoved(SquadData s, Vector2 oldPos)
    {
        if (!debugBroadcastMovement || s.IsGarrison) return;
        if ((s.Pos - oldPos).sqrMagnitude < 0.25f) return;

        float now = Time.time;
        float minDt = 1f / Mathf.Max(1e-3f, movementBroadcastHz);
        if (!lastBroadcastAt.TryGetValue(s.Id, out float last) || now - last >= minDt)
        {
            lastBroadcastAt[s.Id] = now;
            EventBus.Publish(new SquadMoved(s.Id, s.Pos, false));
        }
    }

    // ---------- поведение «услышали выстрелы» ----------
    void OnPlayerFired(PlayerFired e)
    {
        var ws = WorldState.Instance; if (!ws) return;

        foreach (var s in ws.EnemySquads)
        {
            if (s.IsGarrison) continue; // гарнизон стоит
            if (s.InCombat) continue;

            float d = Vector2.Distance(s.Pos, e.Pos);
            if (d > hearShotRadius) continue;

            float p = hearShotRespondProb * PersonaHearModifier(s.Persona);
            if (Random.value > p) continue;

            // сообщим направление (от отряда к месту выстрела)
            int dir = Dir8Index(s.Pos, e.Pos);
            EventBus.Publish(new EnemyHeardShots(s.Id, s.Callsign, dir));

            // идём «проверить» — используем PursueUntil как таймер расследования
            s.State = AIState.Pursue;
            s.PursueUntil = Time.time + investigateTime;

            // двигаемся через дорожную сеть к ближайшей к точке выстрела ноде
            string nearNode = NearestRoadNodeId(e.Pos);
            s.TargetNodeId = nearNode;
            s.Path = null; s.PathIndex = 0;
            BuildPathToTarget(s);
        }
    }

    float PersonaHearModifier(EnemyPersonality p)
    {
        switch (p)
        {
            case EnemyPersonality.Cowardly: return 0.6f;
            case EnemyPersonality.Aggressive: return 1.2f;
            default: return 1.0f;
        }
    }

    // ---------- дозаправка / пополнение при прибытии ----------
    void TryResupplyOnArrival(SquadData s)
    {
        if (Random.value > resupplyProb) return;

        var ws = WorldState.Instance; if (!ws) return;
        NodeData node = null;
        if (!string.IsNullOrEmpty(s.TargetNodeId))
            node = ws.FindCityById(s.TargetNodeId) ?? ws.FindCampById(s.TargetNodeId);

        if (node == null) return;

        // что пополняем?
        bool giveAmmo = Random.value < 0.6f;
        int amount = giveAmmo
            ? Random.Range(Mathf.Min(resupplyAmmoRange.x, resupplyAmmoRange.y), Mathf.Max(resupplyAmmoRange.x, resupplyAmmoRange.y) + 1)
            : Random.Range(Mathf.Min(resupplyMedsRange.x, resupplyMedsRange.y), Mathf.Max(resupplyMedsRange.x, resupplyMedsRange.y) + 1);

        if (giveAmmo) node.Ammo += amount; else node.Meds += amount;

        // сообщение
        if (node.Type == NodeType.City || node.Type == NodeType.Capital)
        {
            EventBus.Publish(new EnemyResupplied(
                s.Id, s.Callsign, NodeKind.City, node.Id, node.Name,
                giveAmmo ? SupplyKind.Ammo : SupplyKind.Meds, amount, node.Pos
            ));
        }
        else // лагерь — с привязкой к ближайшему городу
        {
            EventBus.Publish(new EnemyResupplied(
                s.Id, s.Callsign, NodeKind.Camp, node.Id, node.Name,
                giveAmmo ? SupplyKind.Ammo : SupplyKind.Meds, amount, node.Pos
            ));
        }
    }

    // ---------- усиление/ограничения ----------
    void EnforceRegionCap(WorldState ws)
    {
        var byRegion = new Dictionary<int, List<SquadData>>();
        foreach (var s in ws.EnemySquads)
            if (!s.IsGarrison)
            {
                int rid = CurrentRegionId(s.Pos);
                if (!byRegion.TryGetValue(rid, out var lst)) { lst = new List<SquadData>(); byRegion[rid] = lst; }
                lst.Add(s);
            }

        foreach (var kv in byRegion)
        {
            var lst = kv.Value;
            if (lst.Count <= maxMobilePerRegion) continue;

            // избыточных уводим в другие регионы
            for (int i = maxMobilePerRegion; i < lst.Count; i++)
            {
                var s = lst[i];
                var dest = PickDestOutsideRegion(ws, kv.Key);
                if (dest != null)
                {
                    s.TargetNodeId = dest.Id;
                    s.State = AIState.Return; s.InCombat = false;
                    s.Path = null; s.PathIndex = 0;
                    PublishMoveIntent(s, dest);
                }
            }
        }
    }

    int CurrentRegionId(Vector2 p)
    {
        var ws = WorldState.Instance; if (!ws) return -1;
        int bestRegion = ws.Capital ? ws.Capital.RegionId : -1;
        float bestD = ws.Capital ? Vector2.Distance(p, ws.Capital.Pos) : float.MaxValue;
        foreach (var c in ws.Cities)
        {
            float d = Vector2.Distance(p, c.Pos);
            if (d < bestD) { bestD = d; bestRegion = c.RegionId; }
        }
        return bestRegion;
    }

    NodeData PickDestOutsideRegion(WorldState ws, int regionId)
    {
        var candidates = new List<NodeData>();
        foreach (var c in ws.Cities) if (c.RegionId != regionId) candidates.Add(c);
        foreach (var k in ws.Camps) if (k.RegionId != regionId) candidates.Add(k);
        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    void PickNewTarget(SquadData s)
    {
        var ws = WorldState.Instance; if (!ws) return;

        NodeData dest = null;
        bool wantCity = Random.Range(0, 100) < 70 || ws.Cities.Count == 0;
        if (wantCity && ws.Cities.Count > 0)
            dest = ws.Cities[Random.Range(0, ws.Cities.Count)];
        else if (ws.Camps.Count > 0)
            dest = ws.Camps[Random.Range(0, ws.Camps.Count)];

        if (dest != null)
        {
            s.TargetNodeId = dest.Id;
            PublishMoveIntent(s, dest); // композер сам решит по вероятности
        }
    }

    void BuildPathToTarget(SquadData s)
    {
        var ws = WorldState.Instance; if (!ws) return;
        NodeData target = ws.FindCityById(s.TargetNodeId) ?? ws.FindCampById(s.TargetNodeId);
        if (target == null) { s.Path = null; return; }

        string startNodeId = NearestRoadNodeId(s.Pos);
        string endNodeId = (target.Type == NodeType.City || target.Type == NodeType.Capital) ? target.Id : NearestRoadNodeId(target.Pos);

        var path = new List<Vector2>();

        if (!string.IsNullOrEmpty(startNodeId))
        {
            var startNode = (startNodeId == ws.Capital?.Id) ? ws.Capital : ws.FindCityById(startNodeId);
            if (startNode != null) path.AddRange(StraightLeg(s.Pos, startNode.Pos));
        }

        var polyline = ShortestPolyline(startNodeId, endNodeId);
        if (polyline != null && polyline.Count > 0)
        {
            if (path.Count == 0 || (path[path.Count - 1] - polyline[0]).sqrMagnitude >= 0.01f)
                path.Add(polyline[0]);
            for (int i = 1; i < polyline.Count; i++) path.Add(polyline[i]);
        }

        if (target.Type == NodeType.Camp)
            path.AddRange(StraightLeg(path.Count > 0 ? path[^1] : s.Pos, target.Pos));

        s.Path = path;
        s.PathIndex = 0;
    }

    string NearestRoadNodeId(Vector2 p)
    {
        var ws = WorldState.Instance; if (!ws) return ws?.Capital?.Id;
        string bestId = ws.Capital?.Id; float best = (bestId != null) ? Vector2.Distance(p, ws.Capital.Pos) : float.MaxValue;
        foreach (var c in ws.Cities)
        {
            float d = Vector2.Distance(p, c.Pos);
            if (d < best) { best = d; bestId = c.Id; }
        }
        return bestId;
    }

    List<Vector2> ShortestPolyline(string fromId, string toId)
    {
        if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId) || fromId == toId || G == null)
            return new List<Vector2>();

        var dist = new Dictionary<string, float>();
        var prev = new Dictionary<string, (string node, List<Vector2> path)>();
        var pq = new SortedList<float, string>();

        foreach (var k in G.Keys) dist[k] = float.PositiveInfinity;
        dist[fromId] = 0f; pq.Add(0f, fromId);

        while (pq.Count > 0)
        {
            var cur = pq.Values[0]; pq.RemoveAt(0);
            if (cur == toId) break;

            foreach (var e in G[cur])
            {
                float nd = dist[cur] + e.w;
                if (nd + 1e-3f < dist[e.to])
                {
                    if (dist[e.to] < float.PositiveInfinity)
                    {
                        int idx = pq.IndexOfKey(dist[e.to]);
                        if (idx >= 0) pq.RemoveAt(idx);
                    }
                    dist[e.to] = nd;
                    prev[e.to] = (cur, e.path);
                    pq.Add(nd + Random.value * 1e-4f, e.to); // шум для уникальности ключа
                }
            }
        }

        if (!prev.ContainsKey(toId)) return new List<Vector2>();
        var stack = new Stack<List<Vector2>>();
        string at = toId;
        while (at != fromId && prev.ContainsKey(at))
        {
            var pr = prev[at];
            stack.Push(pr.path);
            at = pr.node;
        }

        var result = new List<Vector2>();
        while (stack.Count > 0)
        {
            var seg = stack.Pop();
            if (result.Count == 0) result.AddRange(seg);
            else
            {
                if ((result[^1] - seg[0]).sqrMagnitude >= 0.01f) result.Add(seg[0]);
                for (int i = 1; i < seg.Count; i++) result.Add(seg[i]);
            }
        }
        return result;
    }

    List<Vector2> StraightLeg(Vector2 a, Vector2 b) => new() { a, b };

    public static bool KillSquadById(string squadId, Vector2? posOverride = null)
    {
        var ws = WorldState.Instance; if (!ws) return false;
        int idx = ws.EnemySquads.FindIndex(s => s.Id == squadId);
        if (idx < 0) return false;

        var s = ws.EnemySquads[idx];
        var p = posOverride ?? s.Pos;

        ws.EnemySquads.RemoveAt(idx);

        EventBus.Publish(new SquadDied(s.Id, s.Callsign, s.IsGarrison, s.AnchorNodeId, p));

        if (s.IsGarrison && !string.IsNullOrEmpty(s.AnchorNodeId))
        {
            int left = ws.EnemySquads.FindAll(x => x.IsGarrison && x.AnchorNodeId == s.AnchorNodeId).Count;
            EventBus.Publish(new GarrisonCountChanged(s.AnchorNodeId, left));
        }
        return true;
    }

    NodeData FarthestNodeFrom(Vector2 p)
    {
        var ws = WorldState.Instance; if (!ws) return null;
        NodeData best = ws.Capital; float bestD = (best != null) ? Vector2.Distance(p, best.Pos) : -1f;
        foreach (var c in ws.Cities)
        {
            float d = Vector2.Distance(p, c.Pos);
            if (d > bestD) { bestD = d; best = c; }
        }
        foreach (var k in ws.Camps)
        {
            float d = Vector2.Distance(p, k.Pos);
            if (d > bestD) { bestD = d; best = k; }
        }
        return best;
    }

    void PublishMoveIntent(SquadData s, NodeData dest)
    {
        if (dest == null) return;
        var kind = (dest.Type == NodeType.Camp) ? NodeKind.Camp : NodeKind.City;
        EventBus.Publish(new EnemyPlannedMove(s.Id, s.Callsign, kind, dest.Id, dest.Name, dest.Pos));
    }

    int Dir8Index(Vector2 from, Vector2 to)
    {
        Vector2 d = to - from; if (d.sqrMagnitude < 1e-6f) return 0;
        float ang = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg; if (ang < 0f) ang += 360f;
        return Mathf.RoundToInt(ang / 45f) & 7;
    }

    // помощь: запрос и отклик
    void OnSquadDied(SquadDied e)
    {
        var ws = WorldState.Instance; if (!ws) return;

        SquadData requester = null; float best = float.MaxValue;
        foreach (var s in ws.EnemySquads)
        {
            float d = Vector2.Distance(s.Pos, e.Pos);
            if (d < best) { best = d; requester = s; }
        }
        if (requester == null || best > helpBroadcastRadius) return;

        if (Random.value < 0.70f)
        {
            EventBus.Publish(new ReinforcementRequested(requester.Id, requester.Callsign, e.Pos, helpResponseRadius));
        }
    }

    void OnReinforcementRequested(ReinforcementRequested e)
    {
        var ws = WorldState.Instance; if (!ws) return;
        foreach (var s in ws.EnemySquads)
        {
            if (s.IsGarrison) continue;
            float d = Vector2.Distance(s.Pos, e.Pos);
            if (d > e.Radius) continue;

            float resp = PersonaRespondChance(s.Persona);
            if (Random.value <= resp)
            {
                // сообщение «принял, иду на помощь»
                EventBus.Publish(new EnemyHelpAccepted(s.Id, s.Callsign, e.CallerCallsign));

                s.State = AIState.Pursue;
                s.PursueUntil = Time.time + helpPursueTime;
                string nearest = NearestRoadNodeId(e.Pos);
                s.TargetNodeId = nearest;
                s.Path = null; s.PathIndex = 0;
                BuildPathToTarget(s);
            }
        }
    }

    float PersonaRespondChance(EnemyPersonality p)
        => p switch
        {
            EnemyPersonality.Cowardly => 0.30f,
            EnemyPersonality.Aggressive => 0.85f,
            _ => 0.55f
        };

    bool TryEnemyFire(SquadData s)
    {
        float now = Time.time;

        if (!nextFireAt.ContainsKey(s.Id))
            nextFireAt[s.Id] = now + Random.Range(0f, initialStaggerMax);

        if (now < nextFireAt[s.Id]) return false;

        float nextDt = Mathf.Clamp(Random.Range(shotInterval.x, shotInterval.y), 0.05f, 99f);
        nextFireAt[s.Id] = now + nextDt;

        bool hit = Random.value < enemyHitChance;
        if (!hit) return true;

        int baseDmg = Random.Range(Mathf.Min(baseDamage.x, baseDamage.y), Mathf.Max(baseDamage.x, baseDamage.y) + 1);
        float scaled = baseDmg * Mathf.Max(0.1f, s.Firepower) * Mathf.Max(0.1f, damageMultiplier);
        int finalDmg = Mathf.Max(1, Mathf.RoundToInt(scaled));

        PlayerInventory.Damage(finalDmg);
        return true;
    }
}
