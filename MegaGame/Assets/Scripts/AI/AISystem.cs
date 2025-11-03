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

    class GraphEdge
    {
        public string to;
        public float w;
        public List<Vector2> path; // та же polyline из WorldState.RoadEdge.Path
    }

    Dictionary<string, List<GraphEdge>> G; // граф по Id узлов (capital + cities)
    float tickAccum;

    void OnEnable()
    {
        EventBus.Subscribe<MapGenerated>(OnMapGenerated);
        BuildGraph(); // на случай если карта уже есть
    }
    void OnDisable()
    {
        EventBus.Unsubscribe<MapGenerated>(OnMapGenerated);
    }

    void OnMapGenerated(MapGenerated _)
    {
        BuildGraph();
        // начальные цели мобильным отрядам
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
        var ws = WorldState.Instance; if (!ws) return;
        Vector2 player = PlayerState.Pos;

        foreach (var s in ws.EnemySquads)
        {
            // Гарнизон: только реагирует на близость игрока
            if (s.IsGarrison)
            {
                float d = Vector2.Distance(s.Pos, player);
                if (d <= s.DetectionRadius)
                {
                    s.State = AIState.Engage; s.InCombat = true;
                    // легкое сближение + покачивание
                    Vector2 dir = (player - s.Pos).normalized;
                    s.Pos = s.Pos + dir * (s.Speed * 0.2f * (1f / tickHz)) + (Vector2)Random.insideUnitCircle * 10f;
                }
                else
                {
                    s.InCombat = false;
                    s.State = AIState.Idle;
                    // держимся рядом с опорой
                    var anchor = ws.FindCityById(s.AnchorNodeId);
                    if (anchor != null && Vector2.Distance(s.Pos, anchor.Pos) > 60f)
                    {
                        Vector2 dir = (anchor.Pos - s.Pos).normalized;
                        s.Pos += dir * (s.Speed * 0.3f * (1f / tickHz));
                    }
                }
                continue;
            }

            // Мобильные
            float distToPlayer = Vector2.Distance(s.Pos, player);
            if (distToPlayer <= s.DetectionRadius)
            {
                s.State = AIState.Engage; s.InCombat = true;
                // чуть ближе к игроку + микродрифт, имитация боя
                Vector2 dir = (player - s.Pos).normalized;
                s.Pos += dir * (s.Speed * 0.4f * (1f / tickHz)) + (Vector2)Random.insideUnitCircle * 12f;
                // при выходе игрока из радиуса — решение о преследовании будет в следующем тике
            }
            else if (s.InCombat) // только что ушёл из радиуса
            {
                s.InCombat = false;
                // шанс преследовать зависит от характера
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
                        // двигаемся напрямую к последней позиции игрока
                        Vector2 dir = (player - s.Pos).normalized;
                        s.Pos += dir * (s.Speed * (1f / tickHz));
                    }
                    else
                    {
                        // закончить погоню — вернуться к патрулю
                        s.State = AIState.Return;
                        s.Path = null; s.PathIndex = 0;
                    }
                }

                if (s.State == AIState.Return || s.State == AIState.Patrol || s.State == AIState.Idle)
                {
                    if (s.Path == null || s.PathIndex >= s.Path.Count)
                    {
                        // нет пути → спланировать к целевому узлу
                        if (string.IsNullOrEmpty(s.TargetNodeId)) PickNewTarget(s);
                        BuildPathToTarget(s);
                    }

                    // шаг по пути
                    if (s.Path != null && s.PathIndex < s.Path.Count)
                    {
                        Vector2 tgt = s.Path[s.PathIndex];
                        float step = s.Speed * (1f / tickHz);
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
        }
    }

    void PickNewTarget(SquadData s)
    {
        var ws = WorldState.Instance; if (!ws) return;
        // цель: случайный город (чаще) или лагерь
        bool wantCity = Random.Range(0, 100) < 70 || ws.Cities.Count == 0;
        if (wantCity && ws.Cities.Count > 0)
            s.TargetNodeId = ws.Cities[Random.Range(0, ws.Cities.Count)].Id;
        else if (ws.Camps.Count > 0)
            s.TargetNodeId = ws.Camps[Random.Range(0, ws.Camps.Count)].Id;
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
}
