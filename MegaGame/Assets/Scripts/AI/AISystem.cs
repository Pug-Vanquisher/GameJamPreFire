using System.Collections.Generic;
using UnityEngine;
using Events;

public class AISystem : MonoBehaviour
{
    [Header("Tick")]
    [SerializeField] private float tickHz = 5f;

    [Header("Balance")]
    [SerializeField] private float pursueTimeCoward = 3f;
    [SerializeField] private float pursueTimeNeutral = 7f;
    [SerializeField] private float pursueTimeAggro = 12f;

    [Header("Combat behavior")]
    [SerializeField] private float engageStandOff = 250f;      // на этой дистанции «держим круг»
    [SerializeField] private float strafeLateralSpeed = 0.45f; // боковая скорость при страйфе (в долях Speed)
    [SerializeField] private float strafeJitterAmplitude = 8f; // случайное дрожание при страйфе

    [Header("Separation")]
    [SerializeField] private float separationRadius = 100f;
    [SerializeField] private float separationPushPerSecond = 250f;

    [Header("Shooting")]
    [SerializeField] private Vector2 shotInterval = new Vector2(1.2f, 2.2f); // интервал между выстрелами одного отряда
    [SerializeField, Range(0f, 1f)] private float enemyHitChance = 0.35f;     // шанс попадания
    [SerializeField] private Vector2Int baseDamage = new Vector2Int(4, 9);   // базовый урон (до Firepower)
    [SerializeField] private float damageMultiplier = 1.0f;                   // общий множитель сложности
    [SerializeField] private int maxShotsPerTick = 2;                         // ограничитель "одновременности"
    [SerializeField] private float initialStaggerMax = 0.7f;                  // рассинхрон стартовых фаз, сек

    // таймеры пер-отряд
    readonly Dictionary<string, float> nextFireAt = new Dictionary<string, float>();

    [Header("Limits")]
    [SerializeField] private int maxMobilePerRegion = 5; // без учёта гарнизона
    [SerializeField] private int maxChasers = 5; // не более N одновременно «пасут» игрока

    [Header("Reinforcement / Help")]
    [SerializeField] private float helpBroadcastRadius = 2500f; // в каком радиусе слышно «запрос помощи»
    [SerializeField] private float helpResponseRadius = 2500f; // радиус, в котором отряды рассматривают отклик
    [SerializeField] private float helpPursueTime = 12f;   // сколько секунд «ищем» игрока на точке
    [SerializeField, Range(0, 1)] private float cowardRetreatProb = 0.70f; // трус отступит с таким шансом

    [Header("Debug Movement Broadcast")]
    [SerializeField] private bool debugBroadcastMovement = true;
    [SerializeField] private float movementBroadcastHz = 3f;
    Dictionary<string, float> lastBroadcastAt = new Dictionary<string, float>();

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
        BuildGraph();
        EventBus.Subscribe<ReinforcementRequested>(OnReinforcementRequested);
        EventBus.Subscribe<SquadDied>(OnSquadDied);
    }
    void OnDisable()
    {
        EventBus.Unsubscribe<MapGenerated>(OnMapGenerated);
        EventBus.Unsubscribe<ReinforcementRequested>(OnReinforcementRequested);
        EventBus.Unsubscribe<SquadDied>(OnSquadDied);
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
        AddNode(ws.Capital?.Id ?? "capital");
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

        var allowedChasers = new HashSet<string>();
        {
            var list = new List<(SquadData s, float d)>();
            foreach (var q in ws.EnemySquads)
                if (!q.IsGarrison)
                {
                    float d = Vector2.Distance(q.Pos, player);
                    if (d <= q.DetectionRadius) list.Add((q, d));
                }
            list.Sort((a, b) => a.d.CompareTo(b.d));
            for (int i = 0; i < list.Count && i < maxChasers; i++) allowedChasers.Add(list[i].s.Id);
        }

        EnforceRegionCap(ws);

        foreach (var s in ws.EnemySquads)
        {
            Vector2 oldPos = s.Pos;

            // === ГАРНИЗОН ===
            if (s.IsGarrison)
            {
                float d = Vector2.Distance(s.Pos, player);
                s.InCombat = (d <= s.DetectionRadius);
                s.State = s.InCombat ? AIState.Engage : AIState.Idle;

                if (s.InCombat && shotsThisTick < maxShotsPerTick)
                {
                    if (TryEnemyFire(s))
                        shotsThisTick++;
                }

                MaybeBroadcastMoved(s, oldPos);
                continue;
            }

            // === МОБИЛЬНЫЕ ===
            float distToPlayer = Vector2.Distance(s.Pos, player);

            if (distToPlayer <= s.DetectionRadius && allowedChasers.Contains(s.Id))
            {
                s.State = AIState.Engage; s.InCombat = true;

                // ТРУС — может отступить, если подошёл близко
                if (s.Persona == EnemyPersonality.Cowardly && distToPlayer < engageStandOff * 0.8f && Random.value < cowardRetreatProb)
                {
                    var far = FarthestNodeFrom(player);
                    if (far != null)
                    {
                        EventBus.Publish(new EnemyRetreatDeclared(s.Id, s.Callsign, s.Pos, far.Id, far.Pos));
                        s.TargetNodeId = far.Id;
                        s.State = AIState.Return; s.InCombat = false;
                        s.Path = null; s.PathIndex = 0;
                        // дальше не сближаемся
                    }
                }
                else
                {
                    if (distToPlayer > engageStandOff)
                    {
                        Vector2 dir = (player - s.Pos).normalized;
                        s.Pos += dir * (s.Speed * 0.6f * dt);
                        if (shotsThisTick < maxShotsPerTick)
                        {
                            if (TryEnemyFire(s))
                                shotsThisTick++;
                        }
                    }
                    else
                    {
                        Vector2 toPl = (player - s.Pos);
                        Vector2 perp = new Vector2(-toPl.y, toPl.x).normalized;
                        s.Pos += perp * (s.Speed * strafeLateralSpeed * dt)
                              + (Vector2)Random.insideUnitCircle * strafeJitterAmplitude * dt;
                        if (shotsThisTick < maxShotsPerTick)
                        {
                            if (TryEnemyFire(s))
                                shotsThisTick++;
                        }
                    }
                }
            }
            else if (distToPlayer <= s.DetectionRadius && !allowedChasers.Contains(s.Id))
            {
                // слишком много преследователей — этот уходит к своим делам
                s.InCombat = false;
                if (string.IsNullOrEmpty(s.TargetNodeId)) PickNewTarget(s);
                s.State = AIState.Return; // уводим с «хвоста»
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
                    if (s.Path == null || s.PathIndex >= s.Path.Count)
                    {
                        if (string.IsNullOrEmpty(s.TargetNodeId)) PickNewTarget(s);
                        BuildPathToTarget(s);
                    }

                    if (s.Path != null && s.PathIndex < s.Path.Count)
                    {
                        Vector2 tgt = s.Path[s.PathIndex];
                        float step = s.Speed * dt;
                        Vector2 to = tgt - s.Pos;
                        if (to.magnitude <= step)
                        {
                            s.Pos = tgt;
                            s.PathIndex++;
                            if (s.PathIndex >= s.Path.Count) { s.State = AIState.Patrol; s.TargetNodeId = null; }
                        }
                        else
                        {
                            s.Pos += to.normalized * step;
                        }
                    }
                }
            }

            ApplySeparation(ws, s, dt);

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
                float k = (separationRadius - dist) / separationRadius; // 0..1
                push += d.normalized * k;
            }
        }
        if (push.sqrMagnitude > 1e-6f)
        {
            s.Pos += push.normalized * (separationPushPerSecond * dt);
        }
    }

    void MaybeBroadcastMoved(SquadData s, Vector2 oldPos)
    {
        if (!debugBroadcastMovement || s.IsGarrison) return;
        if ((s.Pos - oldPos).sqrMagnitude < 0.25f) return; // мало сдвинулись

        float now = Time.time;
        float minDt = 1f / Mathf.Max(1e-3f, movementBroadcastHz);
        if (!lastBroadcastAt.TryGetValue(s.Id, out float last) || now - last >= minDt)
        {
            lastBroadcastAt[s.Id] = now;
            EventBus.Publish(new SquadMoved(s.Id, s.Pos, false));
        }
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
            PublishMoveIntent(s, dest); // 10% шанс вывода решит композер
        }
    }

    // строим путь: по дорогам между узлами; если цель лагерь — последняя нога off-road
    void BuildPathToTarget(SquadData s)
    {
        var ws = WorldState.Instance; if (!ws) return;
        NodeData target = ws.FindCityById(s.TargetNodeId) ?? ws.FindCampById(s.TargetNodeId);
        if (target == null) { s.Path = null; return; }

        // стартовый/конечный узлы дорожной сети: capital+cities
        string startNodeId = NearestRoadNodeId(s.Pos);
        string endNodeId = (target.Type == NodeType.City || target.Type == NodeType.Capital)
            ? target.Id
            : NearestRoadNodeId(target.Pos);

        var path = new List<Vector2>();

        // если мы не у узла — добавим «подход» к ближайшему узлу сети
        if (!string.IsNullOrEmpty(startNodeId))
        {
            var startNode = (startNodeId == ws.Capital.Id) ? ws.Capital : ws.FindCityById(startNodeId);
            if (startNode != null) path.AddRange(StraightLeg(s.Pos, startNode.Pos));
        }

        // по дорожному графу
        var polyline = ShortestPolyline(startNodeId, endNodeId);
        if (polyline != null && polyline.Count > 0)
        {
            if (path.Count > 0 && (path[path.Count - 1] - polyline[0]).sqrMagnitude < 0.1f) { }
            else if (polyline.Count > 0) path.Add(polyline[0]);
            for (int i = 1; i < polyline.Count; i++) path.Add(polyline[i]);
        }

        // если цель лагерь — off-road от узла к лагерю
        if (target.Type == NodeType.Camp)
        {
            path.AddRange(StraightLeg(path.Count > 0 ? path[path.Count - 1] : s.Pos, target.Pos));
        }

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

    // простая Dijkstra по G, возвращает склеенную polyline
    List<Vector2> ShortestPolyline(string fromId, string toId)
    {
        if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId) || fromId == toId || G == null) return new List<Vector2>();
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
                    pq.Add(nd + Random.value * 1e-4f, e.to); // небольшой шум для уникальности ключа
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
            if (result.Count == 0) { result.AddRange(seg); }
            else
            {
                // склеиваем, избегая дубликата стыка
                if ((result[result.Count - 1] - seg[0]).sqrMagnitude < 0.01f) { }
                else result.Add(seg[0]);
                for (int i = 1; i < seg.Count; i++) result.Add(seg[i]);
            }
        }
        return result;
    }

    List<Vector2> StraightLeg(Vector2 a, Vector2 b)
    {
        // короткая прямая (2 точки). Можно нарезать чаще, если нужно.
        return new List<Vector2> { a, b };
    }

    public static bool KillSquadById(string squadId, Vector2? posOverride = null)
    {
        var ws = WorldState.Instance; if (!ws) return false;
        int idx = ws.EnemySquads.FindIndex(s => s.Id == squadId);
        if (idx < 0) return false;

        var s = ws.EnemySquads[idx];
        var p = posOverride ?? s.Pos;

        ws.EnemySquads.RemoveAt(idx);

        // уведомления
        EventBus.Publish(new SquadDied(s.Id, s.Callsign, s.IsGarrison, s.AnchorNodeId, p));

        if (s.IsGarrison && !string.IsNullOrEmpty(s.AnchorNodeId))
        {
            int left = ws.EnemySquads.FindAll(x => x.IsGarrison && x.AnchorNodeId == s.AnchorNodeId).Count;
            EventBus.Publish(new GarrisonCountChanged(s.AnchorNodeId, left));
        }
        return true;
    }

    int CurrentRegionId(Vector2 p)
    {
        // При отсутствии быстрых семплеров региона — берём регион ближайшего города/столицы
        var ws = WorldState.Instance; if (!ws) return -1;
        int bestRegion = ws.Capital != null ? ws.Capital.RegionId : -1;
        float bestD = ws.Capital != null ? Vector2.Distance(p, ws.Capital.Pos) : float.MaxValue;
        foreach (var c in ws.Cities)
        {
            float d = Vector2.Distance(p, c.Pos);
            if (d < bestD) { bestD = d; bestRegion = c.RegionId; }
        }
        return bestRegion;
    }

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

            // избыточных отправим в другие регионы
            for (int i = maxMobilePerRegion; i < lst.Count; i++)
            {
                var s = lst[i];
                // выберем любую цель в другом регионе
                var dest = PickDestOutsideRegion(ws, kv.Key);
                if (dest != null)
                {
                    s.TargetNodeId = dest.Id;
                    s.State = AIState.Return;
                    s.InCombat = false;
                    s.Path = null; s.PathIndex = 0;
                    PublishMoveIntent(s, dest);
                }
            }
        }
    }

    NodeData PickDestOutsideRegion(WorldState ws, int regionId)
    {
        var candidates = new List<NodeData>();
        foreach (var c in ws.Cities) if (c.RegionId != regionId) candidates.Add(c);
        foreach (var k in ws.Camps) if (k.RegionId != regionId) candidates.Add(k);
        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    void PublishMoveIntent(SquadData s, NodeData dest)
    {
        if (dest == null) return;
        var kind = (dest.Type == NodeType.Camp) ? NodeKind.Camp : NodeKind.City;
        EventBus.Publish(new EnemyPlannedMove(s.Id, s.Callsign, kind, dest.Id, dest.Name, dest.Pos));
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

    void OnSquadDied(SquadDied e)
    {
        var ws = WorldState.Instance; if (!ws) return;

        // выбираем ближайшего ЖИВОГО союзника как "запрашивающего" (может быть гарнизоном)
        SquadData requester = null; float best = float.MaxValue;
        foreach (var s in ws.EnemySquads)
        {
            float d = Vector2.Distance(s.Pos, e.Pos);
            if (d < best) { best = d; requester = s; }
        }
        if (requester == null || best > helpBroadcastRadius) return;

        // 70% шанс запросить помощь
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
            if (s.IsGarrison) continue; // гарнизон стоит, но просить помощь может — уже обработано выше
            float d = Vector2.Distance(s.Pos, e.Pos);
            if (d > e.Radius) continue;

            float resp = PersonaRespondChance(s.Persona);
            if (Random.value <= resp)
            {
                // идём к точке запроса (через ближайший узел), потом преследуем N секунд
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
    {
        switch (p)
        {
            case EnemyPersonality.Cowardly: return 0.30f;
            case EnemyPersonality.Aggressive: return 0.85f;
            default: return 0.55f; // Neutral
        }
    }

    bool TryEnemyFire(SquadData s)
    {
        float now = Time.time;

        // инициализация стартовой фазы (рассинхрон)
        if (!nextFireAt.ContainsKey(s.Id))
            nextFireAt[s.Id] = now + UnityEngine.Random.Range(0f, initialStaggerMax);

        // ещё не время стрелять?
        if (now < nextFireAt[s.Id]) return false;

        // назначим следующий выстрел заранее (чтобы даже при пропуске кадра рассинхрон сохранялся)
        float nextDt = Mathf.Clamp(UnityEngine.Random.Range(shotInterval.x, shotInterval.y), 0.05f, 99f);
        nextFireAt[s.Id] = now + nextDt;

        // шанс попадания
        bool hit = UnityEngine.Random.value < enemyHitChance;
        if (!hit) return true; // выстрел был, просто мимо

        // урон = базовый * Firepower * глобальный множитель + округление
        int baseDmg = UnityEngine.Random.Range(Mathf.Min(baseDamage.x, baseDamage.y), Mathf.Max(baseDamage.x, baseDamage.y) + 1);
        float scaled = baseDmg * Mathf.Max(0.1f, s.Firepower) * Mathf.Max(0.1f, damageMultiplier);
        int finalDmg = Mathf.Max(1, Mathf.RoundToInt(scaled));

        PlayerInventory.Damage(finalDmg); // вызовет PlayerDamaged/PlayerDied события

        return true;
    }
}
