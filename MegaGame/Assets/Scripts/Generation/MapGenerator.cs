using System.Collections.Generic;
using UnityEngine;
using Events;
using System.Collections;

public class MapGenerator : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private MapGenConfig config;
    [SerializeField] private NameBank names;
    [SerializeField] private BiomeBank biomes;
    [SerializeField] private CallsignBank callsigns;

    [Header("Boot/Async")]
    [SerializeField] private bool autoGenerateOnStart = true;  // можно выключать и дергать GenerateAsync вручную из Boot

    private System.Random rng;
    private WorldState world;

    [ContextMenu("Generate Now")]
    public void GenerateNow() => StartCoroutine(GenerateAsync(null));

    private void Start()
    {
        if (autoGenerateOnStart)
            StartCoroutine(GenerateAsync(null));
    }

    /// <summary>
    /// Полная асинхронная генерация мира с прогрессом (0..1).
    /// </summary>
    public IEnumerator GenerateAsync(System.Action<float> onProgress)
    {
        if (!config)
        {
            Debug.LogError("MapGenerator: no config");
            yield break;
        }

        float Report(float a) { onProgress?.Invoke(a); return a; }

        int seed = config.useSystemTimeSeed ? System.Environment.TickCount : config.seed;
        rng = new System.Random(seed);

        // Чистим/готовим мир
        world = WorldState.Instance ?? new GameObject("[WorldState]").AddComponent<WorldState>();
        world.Cities.Clear(); world.Camps.Clear(); world.EnemySquads.Clear(); world.Roads.Clear(); world.Regions.Clear();
        world.InitGlobals(
            config.mapHalfSize,
            biomes,
            config.biomeFrequency, config.biomeFrequency2,
            config.biomeBlend,
            config.biomeDetailSeed
        );
        Report(0.03f);
        yield return null;

        // Регионы (вороной-семена)
        var used = new List<Vector2>();
        for (int i = 0; i < config.regionSeeds; i++)
        {
            Vector2 p = SamplePosUnique(used, config.regionSeedMinSpacing, config.mapHalfSize);
            world.Regions.Add(new WorldState.RegionSeed { Id = i, Pos = p });
            used.Add(p);

            if ((i % 4) == 0)
            {
                Report(0.06f + 0.08f * (i / (float)Mathf.Max(1, config.regionSeeds)));
                yield return null;
            }
        }

        // Быстрый оповещатель регионов (на Debug-рендер)
        for (int i = 0; i < world.Regions.Count; i++)
            EventBus.Publish(new RegionCreated(i, RegionRectApprox(world.Regions[i].Pos)));

        Report(0.16f);
        yield return null;

        // Столица
        var capital = new NodeData("capital", "Столица", NodeType.Capital, Faction.Player,
                                   Vector2.zero, world.GetRegionId(Vector2.zero), "plain", true);
        world.SetCapital(capital);
        EventBus.Publish(new NodeSpawned(capital.Id, capital.Name, capital.Pos, capital.RegionId, capital.Type, true));
        Report(0.18f);
        yield return null;

        // Города
        used.Clear(); used.Add(capital.Pos);
        var usedCityNames = new HashSet<string>(); usedCityNames.Add("Столица");
        for (int i = 0; i < config.citiesCount; i++)
        {
            if (!TryPlaceNode(out var pos, out var regionId, out var biomeKey, true)) { i--; continue; }
            string name = names ? names.PickCityNameUnique(rng, usedCityNames) : $"Город-{rng.Next(1000, 9999)}";

            var node = new NodeData($"city_{i}", name, NodeType.City, Faction.Neutral, pos, regionId, biomeKey);
            node.IsCaptured = false;
            node.IsDestroyed = false;
            node.Garrison = Mathf.Clamp(rng.Next(config.cityGarrisonRange.x, config.cityGarrisonRange.y + 1), 0, 999);
            node.Fuel = Mathf.Clamp(rng.Next(config.fuelRange.x, config.fuelRange.y + 1), 1, int.MaxValue);
            node.Meds = Mathf.Clamp(rng.Next(config.medsRange.x, config.medsRange.y + 1), 0, int.MaxValue);
            node.Ammo = Mathf.Clamp(rng.Next(config.ammoRange.x, config.ammoRange.y + 1), 0, int.MaxValue);

            node.StartCaptured = node.IsCaptured; node.StartDestroyed = node.IsDestroyed; node.StartGarrison = node.Garrison;
            node.StartFuel = node.Fuel; node.StartMeds = node.Meds; node.StartAmmo = node.Ammo;

            world.Cities.Add(node); used.Add(pos);
            EventBus.Publish(new NodeSpawned(node.Id, node.Name, node.Pos, node.RegionId, node.Type));

            if ((i & 3) == 0)
            {
                Report(0.45f * (i / (float)Mathf.Max(1, config.citiesCount)) + 0.18f);
                yield return null;
            }
        }
        Report(0.60f);
        yield return null;

        // Лагеря
        for (int i = 0; i < config.campsCount; i++)
        {
            if (!TryPlaceNode(out var pos, out var regionId, out var biomeKey, false)) { i--; continue; }
            string name = names ? names.PickCampName(rng) : $"Застава {i + 1}";

            var node = new NodeData($"camp_{i}", name, NodeType.Camp, Faction.Enemy, pos, regionId, biomeKey);
            node.IsCaptured = false;
            node.IsDestroyed = false;
            node.Garrison = Mathf.Clamp(rng.Next(config.campGarrisonRange.x, config.campGarrisonRange.y + 1), 0, 999);
            node.Fuel = Mathf.Clamp(rng.Next(config.fuelRange.x, config.fuelRange.y + 1), 1, int.MaxValue);
            node.Meds = Mathf.Clamp(rng.Next(config.medsRange.x, config.medsRange.y + 1), 0, int.MaxValue);
            node.Ammo = Mathf.Clamp(rng.Next(config.ammoRange.x, config.ammoRange.y + 1), 0, int.MaxValue);

            node.StartCaptured = node.IsCaptured; node.StartDestroyed = node.IsDestroyed; node.StartGarrison = node.Garrison;
            node.StartFuel = node.Fuel; node.StartMeds = node.Meds; node.StartAmmo = node.Ammo;

            world.Camps.Add(node);
            EventBus.Publish(new NodeSpawned(node.Id, node.Name, node.Pos, node.RegionId, node.Type));

            if ((i & 3) == 0)
            {
                Report(0.70f * (i / (float)Mathf.Max(1, config.campsCount)) + 0.60f);
                yield return null;
            }
        }
        Report(0.75f);
        yield return null;

        // База игрока
        {
            float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            Vector2 basePos = ClampToMap(capital.Pos + dir * config.playerBaseDistanceFromCapital, config.mapHalfSize);

            for (int tries = 0; tries < 80; tries++)
            {
                if (AcceptsPlacement(basePos, true)) break;
                ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                basePos = ClampToMap(capital.Pos + dir * config.playerBaseDistanceFromCapital, config.mapHalfSize);
            }
            var baseNode = new NodeData("player_base", "База", NodeType.Base, Faction.Player,
                                        basePos, world.GetRegionId(basePos), world.SampleBiome(basePos).ToString());
            world.SetPlayerBase(baseNode, basePos);
            EventBus.Publish(new NodeSpawned(baseNode.Id, baseNode.Name, baseNode.Pos, baseNode.RegionId, baseNode.Type));
        }
        Report(0.78f);
        yield return null;

        // Враги — асинхронно (гарнизоны + мобильные)
        yield return StartCoroutine(GenerateEnemiesAsync(0.78f, 0.92f, onProgress));

        // Дороги — асинхронно (MST + доп.ребра + извилистость)
        yield return StartCoroutine(BuildRoadsAsync(0.92f, 0.99f, onProgress));

        // Финал
        EventBus.Publish(new MapGenerated(seed));
        Debug.Log($"[MapGen] seed {seed} | cities:{world.Cities.Count} camps:{world.Camps.Count} roads:{world.Roads.Count}");
        Report(1f);
    }

    /// <summary>
    /// Асинхронный спавн всех врагов: гарнизоны городов/лагерей + мобильные отряды.
    /// </summary>
    private IEnumerator GenerateEnemiesAsync(float from, float to, System.Action<float> onProgress)
    {
        var ws = WorldState.Instance; if (!ws) yield break;

        float Lerp01(float t) => Mathf.Lerp(from, to, Mathf.Clamp01(t));
        void Report01(float t) => onProgress?.Invoke(Lerp01(t));

        var usedCallsigns = new HashSet<string>();

        EnemyPersonality PickPersona()
        {
            int r = rng.Next(100);
            if (r < 30) return EnemyPersonality.Cowardly;
            if (r < 70) return EnemyPersonality.Neutral;
            return EnemyPersonality.Aggressive;
        }

        // Оценим общий объём работы для плавного прогресса
        int total = 0;
        foreach (var c in ws.Cities) total += Mathf.Max(0, c.Garrison);
        foreach (var k in ws.Camps) total += Mathf.Max(0, k.Garrison);
        total += Mathf.Max(0, config.enemySquads);
        total = Mathf.Max(1, total);

        int done = 0;
        int yieldStep = 16;

        // --- Гарнизоны в городах ---
        foreach (var city in ws.Cities)
        {
            if (city.Garrison <= 0) continue;

            int count = city.Garrison;
            float R = config.garrisonSpawnRadius;
            float sep = Mathf.Max(10f, config.garrisonMinSeparation);
            float angleStep = 360f / Mathf.Max(1, count);

            var usedNumbers = new HashSet<int>(); // 1..30 — уникальные номера для позывного "<CityName>-N"

            for (int k = 0; k < count; k++)
            {
                int num = rng.Next(1, 31);
                int guard = 0;
                while (!usedNumbers.Add(num) && guard++ < 64) num = rng.Next(1, 31);
                string call = $"{city.Name}-{num}";
                usedCallsigns.Add(call);

                float ang = (angleStep * k + rng.Next(-10, 11)) * Mathf.Deg2Rad;
                Vector2 basePos = city.Pos + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * R;

                // раздвижка, чтобы не прилипали
                int repel = 0;
                while (repel++ < 30)
                {
                    bool clash = false;
                    foreach (var s in ws.EnemySquads)
                    {
                        if (!s.IsGarrison || s.AnchorNodeId != city.Id) continue;
                        if ((s.Pos - basePos).sqrMagnitude < sep * sep) { clash = true; break; }
                    }
                    if (!clash) break;
                    basePos += Random.insideUnitCircle * 6f;
                }

                var sq = new SquadData($"g_{city.Id}_{k}", basePos, city.RegionId, 1);
                sq.IsGarrison = true;
                sq.AnchorNodeId = city.Id;
                sq.Persona = EnemyPersonality.Neutral;
                sq.Callsign = call;
                sq.Firepower = 1.0f;
                sq.Speed = Mathf.Lerp(config.enemySpeedRange.x, config.enemySpeedRange.y, (float)rng.NextDouble());
                sq.DetectionRadius = config.enemyDetectionRadius;

                ws.EnemySquads.Add(sq);
                EventBus.Publish(new SquadSpawned(sq.Id, sq.Pos, sq.RegionId, sq.Strength));

                done++;
                if ((done % yieldStep) == 0) { Report01(done / (float)total); yield return null; }
            }
        }

        // --- Гарнизоны в лагерях ---
        foreach (var camp in ws.Camps)
        {
            if (camp.Garrison <= 0) continue;

            int count = camp.Garrison;
            float R = config.garrisonSpawnRadius * 0.8f;
            float sep = Mathf.Max(10f, config.garrisonMinSeparation);
            float angleStep = 360f / Mathf.Max(1, count);

            var usedNumbers = new HashSet<int>(); // 1..30

            for (int k = 0; k < count; k++)
            {
                int num = rng.Next(1, 31);
                int guard = 0;
                while (!usedNumbers.Add(num) && guard++ < 64) num = rng.Next(1, 31);
                string call = $"{camp.Name}-{num}";
                usedCallsigns.Add(call);

                float ang = (angleStep * k + rng.Next(-10, 11)) * Mathf.Deg2Rad;
                Vector2 basePos = camp.Pos + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * R;

                int repel = 0;
                while (repel++ < 30)
                {
                    bool clash = false;
                    foreach (var s in ws.EnemySquads)
                    {
                        if (!s.IsGarrison || s.AnchorNodeId != camp.Id) continue;
                        if ((s.Pos - basePos).sqrMagnitude < sep * sep) { clash = true; break; }
                    }
                    if (!clash) break;
                    basePos += Random.insideUnitCircle * 6f;
                }

                var sq = new SquadData($"g_{camp.Id}_{k}", basePos, camp.RegionId, 1);
                sq.IsGarrison = true;
                sq.AnchorNodeId = camp.Id;
                sq.Persona = EnemyPersonality.Neutral;
                sq.Callsign = call;
                sq.Firepower = 1.0f;
                sq.Speed = Mathf.Lerp(config.enemySpeedRange.x, config.enemySpeedRange.y, (float)rng.NextDouble());
                sq.DetectionRadius = config.enemyDetectionRadius;

                ws.EnemySquads.Add(sq);
                EventBus.Publish(new SquadSpawned(sq.Id, sq.Pos, sq.RegionId, sq.Strength));

                done++;
                if ((done % yieldStep) == 0) { Report01(done / (float)total); yield return null; }
            }
        }

        // --- Мобильные отряды ---
        for (int m = 0; m < config.enemySquads; m++)
        {
            bool fromCity = rng.Next(100) < 70 || ws.Camps.Count == 0 || ws.Cities.Count == 0;
            NodeData src = fromCity
                ? ws.Cities[rng.Next(ws.Cities.Count)]
                : ws.Camps[rng.Next(ws.Camps.Count)];

            Vector2 pos = src.Pos + Random.insideUnitCircle * 50f;

            var sq = new SquadData($"squad_{m}", pos, src.RegionId, 1);
            sq.Persona = PickPersona();

            if (callsigns != null) sq.Callsign = callsigns.TakeUnique(rng, usedCallsigns);
            else
            {
                string tmp = $"Враг-{rng.Next(100, 999)}";
                usedCallsigns.Add(tmp);
                sq.Callsign = tmp;
            }

            sq.Firepower = (sq.Persona == EnemyPersonality.Cowardly) ? 0.8f :
                           (sq.Persona == EnemyPersonality.Aggressive) ? 1.2f : 1.0f;

            sq.Speed = Mathf.Lerp(config.enemySpeedRange.x, config.enemySpeedRange.y, (float)rng.NextDouble());
            sq.DetectionRadius = config.enemyDetectionRadius;

            ws.EnemySquads.Add(sq);
            EventBus.Publish(new SquadSpawned(sq.Id, sq.Pos, sq.RegionId, sq.Strength));

            done++;
            if ((done % yieldStep) == 0) { Report01(done / (float)total); yield return null; }
        }

        Report01(1f);
    }

    /// <summary>
    /// Асинхронная постройка дорожной сети: MST + дополнительные рёбра, с прогрессом.
    /// </summary>
    private IEnumerator BuildRoadsAsync(float from, float to, System.Action<float> onProgress)
    {
        var ws = WorldState.Instance; if (!ws) yield break;

        float Lerp01(float t) => Mathf.Lerp(from, to, Mathf.Clamp01(t));
        void Report01(float t) => onProgress?.Invoke(Lerp01(t));

        var nodes = new List<NodeData>();
        if (ws.Capital != null) nodes.Add(ws.Capital);
        nodes.AddRange(ws.Cities);

        // 1) K-NN кандидаты (синхронно — их немного при наших размерах)
        int k = Mathf.Max(2, config.roadKNearest);
        var candidateEdges = KNearestEdges(nodes, k);

        // 2) MST по кандидатам
        var mst = BuildMST(nodes.Count, candidateEdges);

        int total = mst.Count + Mathf.Max(0, config.extraConnections);
        total = Mathf.Max(1, total);
        int done = 0;

        // 3) Добавляем рёбра MST
        foreach (var e in mst)
        {
            AddRoadEdge(nodes[e.a], nodes[e.b]);
            done++;
            if ((done & 3) == 0) { Report01(done / (float)total); yield return null; }
        }

        // 4) Доп. соединения
        for (int i = 0; i < config.extraConnections; i++)
        {
            var extra = PickExtraEdge(nodes, mst, candidateEdges);
            if (extra.a >= 0) AddRoadEdge(nodes[extra.a], nodes[extra.b]);

            done++;
            if ((done & 1) == 0) { Report01(done / (float)total); yield return null; }
        }

        Report01(1f);
    }

    // -------------------- Вспомогательные методы размещения --------------------

    private bool TryPlaceNode(out Vector2 pos, out int regionId, out string biomeKey, bool isCity)
    {
        float half = config.mapHalfSize;
        for (int tries = 0; tries < 200; tries++)
        {
            pos = new Vector2(
                Mathf.Lerp(-half, half, (float)rng.NextDouble()),
                Mathf.Lerp(-half, half, (float)rng.NextDouble())
            );
            if (pos.magnitude < config.capitalSafeRadius) continue; // держим столицы зону чистой

            if (!AcceptsPlacement(pos, isCity)) continue;

            regionId = world.GetRegionId(pos);
            biomeKey = world.SampleBiome(pos).ToString();
            return true;
        }
        pos = default; regionId = -1; biomeKey = "plain";
        return false;
    }

    private bool AcceptsPlacement(Vector2 pos, bool isCity)
    {
        float d1, d2; int reg = world.GetRegionId(pos, out d1, out d2);
        if (reg < 0) return false;
        if ((d2 - d1) < config.regionSafeMargin) return false; // не на границе регионов

        float raw = world.SampleBiomeRaw(pos);
        if (biomes && biomes.CloseToBoundary(raw, config.biomeNoiseEpsilon)) return false; // не на границе биомов
        if (!StableBiomeNeighborhood(pos, world.SampleBiome(pos))) return false;            // устойчивость окружения

        if (!FarFromExisting(pos, config.minNodeSpacing)) return false;                    // не рядом с другими узлами

        return true;
    }

    private bool StableBiomeNeighborhood(Vector2 pos, Biome center)
    {
        if (!biomes) return true;
        float r = config.biomeProbeRadius;
        const int N = 8;
        for (int i = 0; i < N; i++)
        {
            float ang = (Mathf.PI * 2f) * (i / (float)N);
            Vector2 p = pos + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
            if (world.SampleBiome(p) != center) return false;
        }
        return true;
    }

    private bool FarFromExisting(Vector2 pos, float minDist)
    {
        float d2 = minDist * minDist;
        if (world.Capital != null && (pos - world.Capital.Pos).sqrMagnitude < d2) return false;
        if (world.PlayerBase != null && (pos - world.PlayerBase.Pos).sqrMagnitude < d2) return false;
        foreach (var n in world.Cities) if ((pos - n.Pos).sqrMagnitude < d2) return false;
        foreach (var n in world.Camps) if ((pos - n.Pos).sqrMagnitude < d2) return false;
        return true;
    }

    private static Vector2 ClampToMap(Vector2 p, float half)
        => new(Mathf.Clamp(p.x, -half, half), Mathf.Clamp(p.y, -half, half));

    private Vector2 SamplePosUnique(List<Vector2> used, float minDist, float half)
    {
        for (int tries = 0; tries < 200; tries++)
        {
            Vector2 p = new(
                Mathf.Lerp(-half, half, (float)rng.NextDouble()),
                Mathf.Lerp(-half, half, (float)rng.NextDouble())
            );
            bool ok = true;
            foreach (var u in used) if ((u - p).sqrMagnitude < minDist * minDist) { ok = false; break; }
            if (ok) return p;
        }
        return new(
            Mathf.Lerp(-half, half, (float)rng.NextDouble()),
            Mathf.Lerp(-half, half, (float)rng.NextDouble())
        );
    }

    private Rect RegionRectApprox(Vector2 seedPos)
    {
        float s = 200f;
        return new Rect(seedPos.x - s * 0.5f, seedPos.y - s * 0.5f, s, s);
    }

    // -------------------- Построение дорог: синхронные и общие утилиты --------------------

    private struct Edge { public int a, b; public float w; }

    private List<Edge> KNearestEdges(List<NodeData> nodes, int k)
    {
        var edges = new List<Edge>();
        for (int i = 0; i < nodes.Count; i++)
        {
            var tmp = new List<Edge>();
            for (int j = 0; j < nodes.Count; j++)
            {
                if (i == j) continue;
                float w = (nodes[i].Pos - nodes[j].Pos).sqrMagnitude;
                tmp.Add(new Edge { a = i, b = j, w = w });
            }
            tmp.Sort((x, y) => x.w.CompareTo(y.w));
            for (int t = 0; t < Mathf.Min(k, tmp.Count); t++)
            {
                var e = tmp[t];
                int a = Mathf.Min(e.a, e.b), b = Mathf.Max(e.a, e.b);
                if (!edges.Exists(z => z.a == a && z.b == b))
                    edges.Add(new Edge { a = a, b = b, w = Mathf.Sqrt(e.w) });
            }
        }
        edges.Sort((x, y) => x.w.CompareTo(y.w));
        return edges;
    }

    private List<Edge> BuildMST(int n, List<Edge> edges)
    {
        var parent = new int[n]; for (int i = 0; i < n; i++) parent[i] = i;
        int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        void Union(int x, int y) { x = Find(x); y = Find(y); if (x != y) parent[y] = x; }

        var result = new List<Edge>();
        foreach (var e in edges)
        {
            int fa = Find(e.a), fb = Find(e.b);
            if (fa == fb) continue;
            Union(fa, fb);
            result.Add(e);
            if (result.Count == n - 1) break;
        }
        return result;
    }

    private MapGenerator.Edge PickExtraEdge(List<NodeData> nodes, List<Edge> mst, List<Edge> pool)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            var e = pool[i];
            bool already = mst.Exists(x => (x.a == e.a && x.b == e.b) || (x.a == e.b && x.b == e.a));
            bool built = world.Roads.Exists(r =>
                (NodeIndex(nodes, r.A) == e.a && NodeIndex(nodes, r.B) == e.b) ||
                (NodeIndex(nodes, r.A) == e.b && NodeIndex(nodes, r.B) == e.a));

            if (!already && !built) return e;
        }
        return new Edge { a = -1, b = -1, w = 0 };
    }

    private int NodeIndex(List<NodeData> nodes, string id)
    {
        for (int i = 0; i < nodes.Count; i++) if (nodes[i].Id == id) return i;
        return -1;
    }

    private void AddRoadEdge(NodeData A, NodeData B)
    {
        var path = MakeCurvyPolyline(A.Pos, B.Pos, config.roadSegments, config.roadCurviness, config.roadFrequency);
        var edge = new WorldState.RoadEdge { Id = world.Roads.Count, A = A.Id, B = B.Id, Path = path };
        world.Roads.Add(edge);
        EventBus.Publish(new RoadBuilt(A.Id, B.Id, path));
    }

    private List<Vector2> MakeCurvyPolyline(Vector2 a, Vector2 b, int segments, float curviness, float freq)
    {
        var pts = new List<Vector2>(segments + 1);
        Vector2 ab = b - a;
        float len = ab.magnitude;
        if (len < 1e-3f) { pts.Add(a); return pts; }

        Vector2 dir = ab / len;
        Vector2 nrm = new Vector2(-dir.y, dir.x);
        float amp = len * Mathf.Clamp01(curviness) * 0.2f;

        float phase = (float)rng.NextDouble() * 1000f;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector2 p = Vector2.Lerp(a, b, t);
            float wave = Mathf.PerlinNoise(phase, t * freq) - 0.5f;
            p += nrm * (wave * 2f * amp * Mathf.Sin(t * Mathf.PI));
            pts.Add(p);
        }
        return pts;
    }

    /// <summary>
    /// Синхронная сборка дорожной сети (оставлена для обратной совместимости).
    /// </summary>
    private void BuildRoads()
    {
        var nodes = new List<NodeData>(); nodes.Add(world.Capital); nodes.AddRange(world.Cities);

        int k = Mathf.Max(2, config.roadKNearest);
        var candidateEdges = KNearestEdges(nodes, k);

        var mst = BuildMST(nodes.Count, candidateEdges);
        foreach (var e in mst) AddRoadEdge(nodes[e.a], nodes[e.b]);

        // Доп соединения
        for (int i = 0; i < config.extraConnections; i++)
        {
            var extra = PickExtraEdge(nodes, mst, candidateEdges);
            if (extra.a >= 0) AddRoadEdge(nodes[extra.a], nodes[extra.b]);
        }
    }

    // -------------------- Быстрая перезагрузка только врагов (как было) --------------------

    public void RespawnEnemiesOnly()
    {
        var ws = WorldState.Instance; if (!ws) return;
        ws.ClearEnemies();

        var usedCallsigns = new HashSet<string>();
        System.Func<EnemyPersonality> PickPersona = () =>
        {
            int r = rng.Next(100);
            if (r < 30) return EnemyPersonality.Cowardly;
            if (r < 70) return EnemyPersonality.Neutral;
            return EnemyPersonality.Aggressive;
        };

        // --- Гарнизоны городов ---
        foreach (var city in ws.Cities)
        {
            if (city.Garrison <= 0) continue;
            int count = city.Garrison;
            float R = config.garrisonSpawnRadius;
            float sep = Mathf.Max(10f, config.garrisonMinSeparation);
            float angleStep = 360f / Mathf.Max(1, count);
            var usedNumbers = new HashSet<int>();

            for (int k = 0; k < count; k++)
            {
                int num = rng.Next(1, 31);
                int guard = 0;
                while (!usedNumbers.Add(num) && guard++ < 64) num = rng.Next(1, 31);
                string call = $"{city.Name}-{num}";
                usedCallsigns.Add(call);

                float ang = (angleStep * k + rng.Next(-10, 11)) * Mathf.Deg2Rad;
                Vector2 basePos = city.Pos + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * R;

                int repel = 0;
                while (repel++ < 30)
                {
                    bool clash = false;
                    foreach (var s in ws.EnemySquads)
                    {
                        if (!s.IsGarrison || s.AnchorNodeId != city.Id) continue;
                        if ((s.Pos - basePos).sqrMagnitude < sep * sep) { clash = true; break; }
                    }
                    if (!clash) break;
                    basePos += Random.insideUnitCircle * 6f;
                }

                var sq = new SquadData($"g_{city.Id}_{k}", basePos, city.RegionId, 1);
                sq.IsGarrison = true;
                sq.AnchorNodeId = city.Id;
                sq.Persona = EnemyPersonality.Neutral;
                sq.Callsign = call;
                sq.Firepower = 1.0f;
                sq.Speed = Mathf.Lerp(config.enemySpeedRange.x, config.enemySpeedRange.y, (float)rng.NextDouble());
                sq.DetectionRadius = config.enemyDetectionRadius;

                ws.EnemySquads.Add(sq);
                EventBus.Publish(new SquadSpawned(sq.Id, sq.Pos, sq.RegionId, sq.Strength));
            }
        }

        // --- Гарнизоны лагерей ---
        foreach (var camp in ws.Camps)
        {
            if (camp.Garrison <= 0) continue;
            int count = camp.Garrison;
            float R = config.garrisonSpawnRadius * 0.8f;
            float sep = Mathf.Max(10f, config.garrisonMinSeparation);
            float angleStep = 360f / Mathf.Max(1, count);
            var usedNumbers = new HashSet<int>();

            for (int k = 0; k < count; k++)
            {
                int num = rng.Next(1, 31);
                int guard = 0;
                while (!usedNumbers.Add(num) && guard++ < 64) num = rng.Next(1, 31);
                string call = $"{camp.Name}-{num}";
                usedCallsigns.Add(call);

                float ang = (angleStep * k + rng.Next(-10, 11)) * Mathf.Deg2Rad;
                Vector2 basePos = camp.Pos + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * R;

                int repel = 0;
                while (repel++ < 30)
                {
                    bool clash = false;
                    foreach (var s in ws.EnemySquads)
                    {
                        if (!s.IsGarrison || s.AnchorNodeId != camp.Id) continue;
                        if ((s.Pos - basePos).sqrMagnitude < sep * sep) { clash = true; break; }
                    }
                    if (!clash) break;
                    basePos += Random.insideUnitCircle * 6f;
                }

                var sq = new SquadData($"g_{camp.Id}_{k}", basePos, camp.RegionId, 1);
                sq.IsGarrison = true;
                sq.AnchorNodeId = camp.Id;
                sq.Persona = EnemyPersonality.Neutral;
                sq.Callsign = call;
                sq.Firepower = 1.0f;
                sq.Speed = Mathf.Lerp(config.enemySpeedRange.x, config.enemySpeedRange.y, (float)rng.NextDouble());
                sq.DetectionRadius = config.enemyDetectionRadius;

                ws.EnemySquads.Add(sq);
                EventBus.Publish(new SquadSpawned(sq.Id, sq.Pos, sq.RegionId, sq.Strength));
            }
        }

        // --- Мобильные ---
        for (int m = 0; m < config.enemySquads; m++)
        {
            bool fromCity = rng.Next(100) < 70 || ws.Camps.Count == 0 || ws.Cities.Count == 0;
            NodeData src = fromCity
                ? ws.Cities[rng.Next(ws.Cities.Count)]
                : ws.Camps[rng.Next(ws.Camps.Count)];

            Vector2 pos = src.Pos + Random.insideUnitCircle * 50f;

            var sq = new SquadData($"squad_{m}", pos, src.RegionId, 1);
            sq.Persona = PickPersona();

            if (callsigns != null) sq.Callsign = callsigns.TakeUnique(rng, usedCallsigns);
            else { string tmp = $"Враг-{rng.Next(100, 999)}"; usedCallsigns.Add(tmp); sq.Callsign = tmp; }

            sq.Firepower = (sq.Persona == EnemyPersonality.Cowardly) ? 0.8f :
                           (sq.Persona == EnemyPersonality.Aggressive) ? 1.2f : 1.0f;

            sq.Speed = Mathf.Lerp(config.enemySpeedRange.x, config.enemySpeedRange.y, (float)rng.NextDouble());
            sq.DetectionRadius = config.enemyDetectionRadius;

            ws.EnemySquads.Add(sq);
            EventBus.Publish(new SquadSpawned(sq.Id, sq.Pos, sq.RegionId, sq.Strength));
        }
    }
}
