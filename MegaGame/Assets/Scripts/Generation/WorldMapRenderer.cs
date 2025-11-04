using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Events;

public class WorldMapRenderer : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RawImage mapImage;      
    [SerializeField] private RectTransform overlay; 
    [SerializeField] private Image playerMarker;
    [SerializeField] private Image cityIconPrefab;
    [SerializeField] private Image capitalIconPrefab;
    [SerializeField] private Image enemyIconPrefab;
    [SerializeField] private Image campIconPrefab;

    [Header("Texture")]
    [SerializeField] private int texWidth = 512;
    [SerializeField] private int texHeight = 512;
    [SerializeField] private FilterMode filter = FilterMode.Point; 

    [Header("Colors / Layers")]
    [SerializeField] private Color biomeFallback = new Color(0.10f, 0.12f, 0.10f, 1f); 
    [SerializeField] private Color regionHatchColor = new Color(0f, 0f, 0f, 0.16f);
    [SerializeField] private int regionHatchSpacing = 8;
    [SerializeField] private int regionHatchThickness = 1;
    [SerializeField] private Color regionBorderColor = new Color(0f, 0f, 0f, 0.65f);

    [Header("Roads/Nodes")]
    [SerializeField] private Color roadColor = new Color(0.75f, 0.70f, 0.60f, 1f);
    [SerializeField] private Color cityColor = new Color(0.95f, 0.85f, 0.40f, 1f);
    [SerializeField] private Color capitalColor = new Color(0.95f, 0.55f, 0.25f, 1f);
    [SerializeField] private Color baseColor = new Color(0.40f, 0.85f, 0.95f, 1f);
    [SerializeField] private Color campColor = new Color(0.80f, 0.30f, 0.30f, 1f);
    [SerializeField] private int roadWidthPx = 2;
    [SerializeField] private int nodeRadiusPx = 3;

    [Header("Biome details toggles")]
    [SerializeField] private bool drawPlainsStrokes = true;
    [SerializeField] private bool drawSwampHatch = true;
    [SerializeField] private bool drawForestEdgeDots = true;
    [SerializeField] private bool drawHillsRings = true;

    [Header("Plains (штрихи-борозды)")]
    [SerializeField] private Color plainsStrokeColor = new Color(0.10f, 0.14f, 0.10f, 0.35f);
    [SerializeField] private int plainsCell = 14;     // шаг сетки штрихов
    [SerializeField] private int plainsLen = 7;      // длина штриха 
    [SerializeField] private int plainsThick = 1;      // толщина диска
    [SerializeField, Range(0, 1)] private float plainsDensity = 0.28f; // вероятность штриха в ячейке

    [Header("Swamp (болотная штриховка)")]
    [SerializeField] private Color swampHatchColor = new Color(0.18f, 0.45f, 0.70f, 0.40f);
    [SerializeField] private int swampSpacing = 7;    // вертикальный шаг
    [SerializeField] private int swampDash = 8;    // длина штриха
    [SerializeField] private int swampGap = 5;    // разрыв между штрихами
    [SerializeField] private int swampThick = 1;

    [Header("Forest (кромка кронами)")]
    [SerializeField] private Color forestDotColor = new Color(0.06f, 0.18f, 0.08f, 0.55f);
    [SerializeField] private int forestDotR = 1;
    [SerializeField] private int forestEdgeEvery = 4; // шаг 

    [Header("Hills (контурные кольца)")]
    [SerializeField] private Color hillsRingColor = new Color(0.35f, 0.25f, 0.12f, 0.55f);
    [SerializeField] private float hillsIsoA = 0.82f;
    [SerializeField] private float hillsIsoB = 0.90f;
    [SerializeField] private float hillsBand = 0.012f;


    [Header("Dev toggles")]
    [SerializeField] private bool debugRender = false; 
    [SerializeField] private bool showBoundaries = true;
    [SerializeField] private bool showHatch = true;

    Texture2D tex;
    Color[] row;

    readonly List<Image> overlayStatic = new();  // столица/города/лагеря
    readonly List<Image> overlayDynamic = new(); // враги 
    int overlayStaticIdx, overlayDynamicIdx;


    void OnEnable()
    {
        EventBus.Subscribe<MapGenerated>(OnMapGenerated);
        EventBus.Subscribe<RoadBuilt>(OnRoadBuilt);
        EventBus.Subscribe<NodeSpawned>(OnNodeSpawned);
        EventBus.Subscribe<PlayerMoved>(OnPlayerMoved);
        EventBus.Subscribe<SquadSpawned>(OnSquadSpawned);
        EventBus.Subscribe<SquadDied>(OnSquadDied);
        EventBus.Subscribe<SquadMoved>(OnSquadMoved);
    }
    void OnDisable()
    {
        EventBus.Unsubscribe<MapGenerated>(OnMapGenerated);
        EventBus.Unsubscribe<RoadBuilt>(OnRoadBuilt);
        EventBus.Unsubscribe<NodeSpawned>(OnNodeSpawned);
        EventBus.Unsubscribe<PlayerMoved>(OnPlayerMoved);
        EventBus.Unsubscribe<SquadSpawned>(OnSquadSpawned);
        EventBus.Unsubscribe<SquadDied>(OnSquadDied);
        EventBus.Unsubscribe<SquadMoved>(OnSquadMoved);
    }

    void Start()
    {
        EnsureTexture();
        RebuildAll();
        UpdatePlayerMarker();
    }

    void EnsureTexture()
    {
        if (tex != null) return;
        tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = filter;
        mapImage.texture = tex;
        row = new Color[texWidth];
    }

    void OnMapGenerated(MapGenerated _) => RebuildAll();
    void OnRoadBuilt(RoadBuilt _) => RebuildAll();
    void OnNodeSpawned(NodeSpawned _) => RebuildAll();
    void OnPlayerMoved(PlayerMoved _) => UpdatePlayerMarker();
    void OnSquadSpawned(SquadSpawned e) => UpdateEnemiesOverlay();
    void OnSquadDied(SquadDied e) => UpdateEnemiesOverlay();
    void OnSquadMoved(SquadMoved e)
    {
        if (e.IsGarrison) return;
        UpdateEnemiesOverlay();
    }

    void RebuildAll()
    {
        var ws = WorldState.Instance; if (!ws) return;
        EnsureTexture();

        BakeBiomes();
        AddBiomeDetails();

        if (showHatch) DrawRegionHatch();
        if (showBoundaries) DrawRegionBoundaries();

        foreach (var r in ws.Roads)
            DrawPolyline(r.Path, roadColor, roadWidthPx);

        overlayStaticIdx = 0;
        EnsureOverlayCapacity(overlayStatic, 64);

        bool useCapitalIcon = capitalIconPrefab;
        bool useCityIcon = cityIconPrefab;

        if (ws.Capital != null)
        {
            if (useCapitalIcon) PlaceMarkerStatic(capitalIconPrefab, ws.Capital.Pos);
            else DrawNode(ws.Capital.Pos, capitalColor, nodeRadiusPx + 1);
        }

        if (ws.PlayerBase != null)
        {
            // для базы пока оставим точку (можно тоже префабом при желании)
            DrawNode(ws.PlayerBase.Pos, baseColor, nodeRadiusPx + 1);
        }

        foreach (var c in ws.Cities)
        {
            if (useCityIcon) PlaceMarkerStatic(cityIconPrefab, c.Pos);
            else DrawNode(c.Pos, cityColor, nodeRadiusPx);
        }

        // Лагеря — ТОЛЬКО при debugRender
        if (debugRender)
        {
            foreach (var k in ws.Camps)
            {
                if (campIconPrefab) PlaceMarkerStatic(campIconPrefab, k.Pos);
                else DrawNode(k.Pos, campColor, nodeRadiusPx);
            }
        }

        HideRest(overlayStatic, overlayStaticIdx);

        tex.Apply();

        // ДИНАМИКА: враги (мобильные) — обновляем отдельно
        UpdateEnemiesOverlay();
    }

    void UpdatePlayerMarker()
    {
        var ws = WorldState.Instance;
        if (!ws || !playerMarker || !overlay) return;
        playerMarker.rectTransform.anchoredPosition =
            MapRenderUtils.WorldToOverlayAnchored(PlayerState.Pos, ws.MapHalfSize, overlay);
    }

    void UpdateEnemiesOverlay()
    {
        var ws = WorldState.Instance; if (!ws || !overlay) return;

        overlayDynamicIdx = 0;
        EnsureOverlayCapacity(overlayDynamic, 64);
        HideRest(overlayDynamic, overlayDynamicIdx); 

        if (!debugRender || !enemyIconPrefab)
        {
            HideAll(overlayDynamic);
            return;
        }

        foreach (var s in ws.EnemySquads)
        {
            if (s.IsGarrison) continue; 
            var dot = GetFromPool(overlayDynamic, overlayDynamicIdx++, enemyIconPrefab);
            dot.rectTransform.SetParent(overlay, false);
            dot.rectTransform.anchoredPosition = MapRenderUtils.WorldToOverlayAnchored(s.Pos, ws.MapHalfSize, overlay);
            dot.gameObject.SetActive(true);
        }

        HideRest(overlayDynamic, overlayDynamicIdx);
    }

    void BakeBiomes()
    {
        var ws = WorldState.Instance; if (!ws) return;
        bool hasBank = ws.Biomes != null;

        for (int y = 0; y < texHeight; y++)
        {
            for (int x = 0; x < texWidth; x++)
            {
                float u = x / (float)(texWidth - 1);
                float v = y / (float)(texHeight - 1);
                Vector2 world = new Vector2(
                    Mathf.Lerp(-ws.MapHalfSize, ws.MapHalfSize, u),
                    Mathf.Lerp(-ws.MapHalfSize, ws.MapHalfSize, v));

                Color c;
                if (hasBank)
                {
                    float raw = ws.SampleBiomeRaw(world);     
                    var biome = ws.Biomes.Evaluate(raw);      
                    c = ws.Biomes.ColorOf(biome);           
                }
                else
                {
                    c = new Color(0.1f, 0.12f, 0.1f, 1f);       
                }

                tex.SetPixel(x, y, c);
            }
        }
    }

    void DrawRegionHatch()
    {
        var ws = WorldState.Instance; if (!ws) return;
        if (ws.Regions == null || ws.Regions.Count == 0) return;

        for (int y = 0; y < texHeight; y++)
        {
            for (int x = 0; x < texWidth; x++)
            {
                float u = x / (float)(texWidth - 1);
                float v = y / (float)(texHeight - 1);
                Vector2 world = new Vector2(
                    Mathf.Lerp(-ws.MapHalfSize, ws.MapHalfSize, u),
                    Mathf.Lerp(-ws.MapHalfSize, ws.MapHalfSize, v));

                int id = ws.GetRegionId(world);
                int phase = 3 * id + 7;
                int s = regionHatchSpacing;
                if (((x + y + phase) % s) < regionHatchThickness)
                {
                    Color baseC = tex.GetPixel(x, y);
                    Color mix = Color.Lerp(baseC, regionHatchColor, regionHatchColor.a);
                    mix.a = 1f;
                    tex.SetPixel(x, y, mix);
                }
            }
        }
    }

    void DrawRegionBoundaries()
    {
        var ws = WorldState.Instance; if (!ws) return;
        if (ws.Regions == null || ws.Regions.Count == 0) return;

        for (int y = 1; y < texHeight; y++)
        {
            for (int x = 1; x < texWidth; x++)
            {
                Vector2 world = PixelToWorld(ws, x, y);
                Vector2 worldL = PixelToWorld(ws, x - 1, y);
                Vector2 worldB = PixelToWorld(ws, x, y - 1);

                int id = ws.GetRegionId(world);
                int idL = ws.GetRegionId(worldL);
                int idB = ws.GetRegionId(worldB);

                if (id != idL || id != idB)
                    tex.SetPixel(x, y, regionBorderColor);
            }
        }
    }

    Vector2 PixelToWorld(WorldState ws, int x, int y)
    {
        float u = x / (float)(texWidth - 1);
        float v = y / (float)(texHeight - 1);
        return new Vector2(
            Mathf.Lerp(-ws.MapHalfSize, ws.MapHalfSize, u),
            Mathf.Lerp(-ws.MapHalfSize, ws.MapHalfSize, v));
    }

    void DrawNode(Vector2 worldPos, Color col, int radius)
    {
        var ws = WorldState.Instance; if (!ws) return;
        var p = MapRenderUtils.WorldToPixel(worldPos, ws.MapHalfSize, texWidth, texHeight);
        for (int y = -radius; y <= radius; y++)
        {
            int yy = p.y + y; if (yy < 0 || yy >= texHeight) continue;
            for (int x = -radius; x <= radius; x++)
            {
                int xx = p.x + x; if (xx < 0 || xx >= texWidth) continue;
                if (x * x + y * y <= radius * radius) tex.SetPixel(xx, yy, col);
            }
        }
    }

    void DrawPolyline(List<Vector2> path, Color col, int width)
    {
        if (path == null || path.Count < 2) return;
        var ws = WorldState.Instance; if (!ws) return;

        for (int i = 0; i < path.Count - 1; i++)
        {
            var a = MapRenderUtils.WorldToPixel(path[i], ws.MapHalfSize, texWidth, texHeight);
            var b = MapRenderUtils.WorldToPixel(path[i + 1], ws.MapHalfSize, texWidth, texHeight);
            DrawThickLine(a, b, col, width);
        }
    }

    void DrawThickLine(Vector2Int a, Vector2Int b, Color col, int width)
    {
        int dx = Mathf.Abs(b.x - a.x), sx = a.x < b.x ? 1 : -1;
        int dy = -Mathf.Abs(b.y - a.y), sy = a.y < b.y ? 1 : -1;
        int err = dx + dy, e2, x = a.x, y = a.y;

        for (; ; )
        {
            DrawDisc(x, y, width, col);
            if (x == b.x && y == b.y) break;
            e2 = 2 * err;
            if (e2 >= dy) { err += dy; x += sx; }
            if (e2 <= dx) { err += dx; y += sy; }
        }
    }

    void DrawDisc(int cx, int cy, int r, Color col)
    {
        for (int y = -r; y <= r; y++)
        {
            int yy = cy + y; if (yy < 0 || yy >= texHeight) continue;
            for (int x = -r; x <= r; x++)
            {
                int xx = cx + x; if (xx < 0 || xx >= texWidth) continue;
                if (x * x + y * y <= r * r) tex.SetPixel(xx, yy, col);
            }
        }
    }

    void AddBiomeDetails()
    {
        var ws = WorldState.Instance; if (!ws || !ws.Biomes) return;

        if (drawSwampHatch)
        {
            for (int y = 0; y < texHeight; y += swampSpacing)
            {
                int x0 = (Hash1D(y, 911) % (swampDash + swampGap));
                for (int x = x0; x < texWidth; x += swampDash + swampGap)
                {
                    int x1 = Mathf.Min(x + swampDash, texWidth - 1);
                    for (int xx = x; xx <= x1; xx++)
                    {
                        if (BiomeAtPixel(ws, xx, y) == Biome.Swamp)
                            DrawDisc(xx, y, swampThick, swampHatchColor);
                    }
                }
            }
        }

        if (drawForestEdgeDots)
        {
            for (int y = 1; y < texHeight - 1; y++)
            {
                for (int x = 1; x < texWidth - 1; x++)
                {
                    if (BiomeAtPixel(ws, x, y) != Biome.Forest) continue;
                    if (!IsEdge(ws, x, y)) continue;                
                    if (((x + y) % forestEdgeEvery) != 0) continue; 

                    Vector2 n = RawGrad(ws, x, y).normalized;
                    Vector2 p = new Vector2(x, y) - n * 2.0f;
                    int px = Mathf.Clamp(Mathf.RoundToInt(p.x + Jitter(x, y, 0.6f)), 0, texWidth - 1);
                    int py = Mathf.Clamp(Mathf.RoundToInt(p.y + Jitter(y, x, 0.6f)), 0, texHeight - 1);

                    if (BiomeAtPixel(ws, px, py) == Biome.Forest)
                        DrawDisc(px, py, forestDotR, forestDotColor);
                }
            }
        }

        if (drawPlainsStrokes)
        {
            int step = Mathf.Max(6, plainsCell);
            for (int cy = step / 2; cy < texHeight; cy += step)
                for (int cx = step / 2; cx < texWidth; cx += step)
                {
                    if (BiomeAtPixel(ws, cx, cy) != Biome.Plains) continue;
                    float r = (Hash2D(cx, cy, 7283) & 1023) / 1023f;
                    if (r > plainsDensity) continue;

                    Vector2 g = RawGrad(ws, cx, cy);
                    if (g.sqrMagnitude < 1e-6f) g = new Vector2(1, 0);
                    Vector2 t = new Vector2(-g.y, g.x).normalized;

                    int len = Mathf.Clamp(plainsLen, 3, 32);
                    Vector2 a = new Vector2(cx, cy) - t * (len * 0.5f);
                    Vector2 b = new Vector2(cx, cy) + t * (len * 0.5f);

                    DrawLineMaskedByBiome(
                        RoundToInt(a), RoundToInt(b), plainsStrokeColor, plainsThick, Biome.Plains, ws
                    );
                }
        }

        if (drawHillsRings)
        {
            for (int y = 1; y < texHeight - 1; y++)
                for (int x = 1; x < texWidth - 1; x++)
                {
                    if (BiomeAtPixel(ws, x, y) != Biome.Hills) continue;
                    float raw = Raw(ws, x, y);
                    if (IsIso(raw, hillsIsoA, hillsBand) || IsIso(raw, hillsIsoB, hillsBand))
                    {
                        float slope = RawGrad(ws, x, y).magnitude;
                        int thick = Mathf.Clamp((int)Mathf.Round(1f + slope * 6f), 1, 3);
                        DrawDisc(x, y, thick, hillsRingColor);
                    }
                }
        }
    }

    float Raw(WorldState ws, int x, int y)
    {
        float u = x / (float)(texWidth - 1);
        float v = y / (float)(texHeight - 1);
        Vector2 w = new Vector2(
            Mathf.Lerp(-ws.MapHalfSize, ws.MapHalfSize, u),
            Mathf.Lerp(-ws.MapHalfSize, ws.MapHalfSize, v));
        return ws.SampleBiomeRaw(w);
    }

    Vector2 RawGrad(WorldState ws, int x, int y)
    {
        const int d = 1;
        float c = Raw(ws, x, y);
        float rx = Raw(ws, Mathf.Clamp(x + d, 0, texWidth - 1), y) - Raw(ws, Mathf.Clamp(x - d, 0, texWidth - 1), y);
        float ry = Raw(ws, x, Mathf.Clamp(y + d, 0, texHeight - 1)) - Raw(ws, x, Mathf.Clamp(y - d, 0, texHeight - 1));
        return new Vector2(rx, ry);
    }

    bool IsIso(float v, float iso, float band) => Mathf.Abs(v - iso) <= band;

    bool IsEdge(WorldState ws, int x, int y)
    {
        var b = BiomeAtPixel(ws, x, y);
        if (BiomeAtPixel(ws, x - 1, y) != b) return true;
        if (BiomeAtPixel(ws, x + 1, y) != b) return true;
        if (BiomeAtPixel(ws, x, y - 1) != b) return true;
        if (BiomeAtPixel(ws, x, y + 1) != b) return true;
        return false;
    }

    Biome BiomeAtPixel(WorldState ws, int x, int y)
    {
        x = Mathf.Clamp(x, 0, texWidth - 1);
        y = Mathf.Clamp(y, 0, texHeight - 1);
        float v = Raw(ws, x, y);
        return ws.Biomes ? ws.Biomes.Evaluate(v) : Biome.Plains;
    }

    Vector2Int RoundToInt(Vector2 v) => new Vector2Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y));

    int Hash1D(int a, int seed) { unchecked { int h = seed; h = (h * 16777619) ^ a; h ^= (h >> 13); return h & 0x7fffffff; } }
    int Hash2D(int x, int y, int seed)
    {
        unchecked
        {
            int h = seed;
            h = (h * 73856093) ^ x;
            h = (h * 19349663) ^ y;
            h ^= (h >> 13);
            return h & 0x7fffffff;
        }
    }

    void DrawLineMaskedByBiome(Vector2Int a, Vector2Int b, Color col, int width, Biome mask, WorldState ws)
    {
        int dx = Mathf.Abs(b.x - a.x), sx = a.x < b.x ? 1 : -1;
        int dy = -Mathf.Abs(b.y - a.y), sy = a.y < b.y ? 1 : -1;
        int err = dx + dy, e2, x = a.x, y = a.y;

        for (; ; )
        {
            if (x >= 0 && x < texWidth && y >= 0 && y < texHeight && BiomeAtPixel(ws, x, y) == mask)
                DrawDisc(x, y, width, col);

            if (x == b.x && y == b.y) break;
            e2 = 2 * err;
            if (e2 >= dy) { err += dy; x += sx; }
            if (e2 <= dx) { err += dx; y += sy; }
        }
    }

    float Jitter(int x, int y, float amplitude)
    {
        int h = Hash2D(x, y, 0x2F6E2B1);               
        float r = (h & 0x7fffffff) / 2147483647f;      
        return (r * 2f - 1f) * amplitude;           
    }

    void PlaceMarkerStatic(Image prefab, Vector2 worldPos)
    {
        var ws = WorldState.Instance; if (!ws || !overlay || !prefab) return;
        var img = GetFromPool(overlayStatic, overlayStaticIdx++, prefab);
        img.rectTransform.SetParent(overlay, false);
        img.rectTransform.anchoredPosition = MapRenderUtils.WorldToOverlayAnchored(worldPos, ws.MapHalfSize, overlay);
        img.gameObject.SetActive(true);
    }

    Image GetFromPool(List<Image> pool, int index, Image prefab)
    {
        if (index < pool.Count && pool[index] != null)
        {
            var it = pool[index];
            it.sprite = prefab.sprite;
            it.color = prefab.color;
            it.rectTransform.sizeDelta = prefab.rectTransform.sizeDelta;
            return it;
        }
        else
        {
            var created = Instantiate(prefab, overlay);
            if (index >= pool.Count) pool.Add(created);
            else pool[index] = created;
            created.gameObject.SetActive(false);
            return created;
        }
    }

    void EnsureOverlayCapacity(List<Image> pool, int min)
    {
        while (pool.Count < min)
        {
            var go = new GameObject("marker", typeof(RectTransform), typeof(Image));
            var img = go.GetComponent<Image>();
            img.gameObject.SetActive(false);
            pool.Add(img);
        }
    }

    void HideAll(List<Image> pool)
    {
        foreach (var it in pool) if (it) it.gameObject.SetActive(false);
    }

    void HideRest(List<Image> pool, int used)
    {
        for (int i = used; i < pool.Count; i++)
            if (pool[i]) pool[i].gameObject.SetActive(false);
    }
}
