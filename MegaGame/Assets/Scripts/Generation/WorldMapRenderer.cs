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
    [SerializeField] private Color baseColor = new Color(0.40f, 0.85f, 0.95f, 1f);
    [SerializeField] private Color campColor = new Color(0.80f, 0.30f, 0.30f, 1f);
    [SerializeField] private int roadWidthPx = 2;
    [SerializeField] private int nodeRadiusPx = 3;

    [Header("Dev toggles")]
    [SerializeField] private bool showCamps = false; 
    [SerializeField] private bool showBoundaries = true;
    [SerializeField] private bool showHatch = true;

    Texture2D tex;
    Color[] row;

    void OnEnable()
    {
        EventBus.Subscribe<MapGenerated>(OnMapGenerated);
        EventBus.Subscribe<RoadBuilt>(OnRoadBuilt);
        EventBus.Subscribe<NodeSpawned>(OnNodeSpawned);
        EventBus.Subscribe<PlayerMoved>(OnPlayerMoved);
    }
    void OnDisable()
    {
        EventBus.Unsubscribe<MapGenerated>(OnMapGenerated);
        EventBus.Unsubscribe<RoadBuilt>(OnRoadBuilt);
        EventBus.Unsubscribe<NodeSpawned>(OnNodeSpawned);
        EventBus.Unsubscribe<PlayerMoved>(OnPlayerMoved);
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

    void RebuildAll()
    {
        var ws = WorldState.Instance; if (!ws) return;
        EnsureTexture();

        BakeBiomes();

        if (showHatch) DrawRegionHatch();
        if (showBoundaries) DrawRegionBoundaries();

        foreach (var r in ws.Roads)
            DrawPolyline(r.Path, roadColor, roadWidthPx);

        if (ws.Capital != null) DrawNode(ws.Capital.Pos, baseColor, nodeRadiusPx + 1);
        if (ws.PlayerBase != null) DrawNode(ws.PlayerBase.Pos, baseColor, nodeRadiusPx + 1);
        foreach (var c in ws.Cities) DrawNode(c.Pos, cityColor, nodeRadiusPx);
        if (showCamps) foreach (var k in ws.Camps) DrawNode(k.Pos, campColor, nodeRadiusPx);

        tex.Apply();
    }

    void UpdatePlayerMarker()
    {
        var ws = WorldState.Instance;
        if (!ws || !playerMarker || !overlay) return;
        playerMarker.rectTransform.anchoredPosition =
            MapRenderUtils.WorldToOverlayAnchored(PlayerState.Pos, ws.MapHalfSize, overlay);
    }

    void BakeBiomes()
    {
        var ws = WorldState.Instance; if (!ws) return;
        bool hasBank = ws.Biomes != null;

        for (int y = 0; y < texHeight; y++)
        {
            for (int x = 0; x < texWidth; x++)
            {
                // пиксель -> мировые координаты (север сверху)
                float u = x / (float)(texWidth - 1);
                float v = y / (float)(texHeight - 1);
                Vector2 world = new Vector2(
                    Mathf.Lerp(-ws.MapHalfSize, ws.MapHalfSize, u),
                    Mathf.Lerp(-ws.MapHalfSize, ws.MapHalfSize, v));

                Color c;
                if (hasBank)
                {
                    float raw = ws.SampleBiomeRaw(world);     // 0..1 по Перлину
                    var biome = ws.Biomes.Evaluate(raw);      // правило из SO
                    c = ws.Biomes.ColorOf(biome);             // ЦВЕТ ИЗ SO
                }
                else
                {
                    c = new Color(0.1f, 0.12f, 0.1f, 1f);        // запасной цвет
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
}
