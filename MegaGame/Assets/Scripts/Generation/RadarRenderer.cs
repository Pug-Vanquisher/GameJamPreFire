using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Events;

public class RadarRenderer : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RawImage radarImage;      
    [SerializeField] private RectTransform radarRect;  
    [SerializeField] private Image cityDotPrefab;
    [SerializeField] private Image campDotPrefab;
    [SerializeField] private Image baseDotPrefab;
    [SerializeField] private Image enemyDotPrefab;

    [Header("Radar settings")]
    [SerializeField] private float radarRange = 1200f; 
    [SerializeField] private int texW = 128;
    [SerializeField] private int texH = 128;
    [SerializeField] private float refreshHz = 5f; // периодич. автообновление
    float _accum;

    [Header("Colors")]
    [SerializeField] private Color bgColor = new Color(0.05f, 0.06f, 0.07f, 1f);
    [SerializeField] private Color roadColor = new Color(0.8f, 0.75f, 0.7f, 1f);
    [SerializeField] private int roadWidthPx = 1;

    readonly List<Image> pool = new();
    int poolIdx = 0;

    Texture2D tex;

    void OnEnable()
    {
        EventBus.Subscribe<PlayerMoved>(OnPlayerMoved);
        EventBus.Subscribe<MapGenerated>(OnMapGenerated);
        EventBus.Subscribe<RoadBuilt>(OnRoadBuilt);
        EventBus.Subscribe<NodeSpawned>(OnNodeSpawned);
        EventBus.Subscribe<SquadSpawned>(OnSquadSpawned);
        EventBus.Subscribe<SquadDied>(OnSquadDied);
    }
    void OnDisable()
    {
        EventBus.Unsubscribe<PlayerMoved>(OnPlayerMoved);
        EventBus.Unsubscribe<MapGenerated>(OnMapGenerated);
        EventBus.Unsubscribe<RoadBuilt>(OnRoadBuilt);
        EventBus.Unsubscribe<NodeSpawned>(OnNodeSpawned);
        EventBus.Unsubscribe<SquadSpawned>(OnSquadSpawned);
        EventBus.Unsubscribe<SquadDied>(OnSquadDied);
    }

    void Start()
    {
        EnsureTexture();
        RefreshAll();
    }

    void Update()
    {
        _accum += Time.deltaTime * refreshHz;
        if (_accum >= 1f)
        {
            _accum -= 1f;
            RefreshAll();
        }
    }


    void EnsureTexture()
    {
        if (tex != null) return;
        tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Point;
        radarImage.texture = tex;
    }

    void OnMapGenerated(MapGenerated _) => RefreshAll();
    void OnRoadBuilt(RoadBuilt _) => RefreshAll();
    void OnNodeSpawned(NodeSpawned _) => RefreshAll();
    void OnPlayerMoved(PlayerMoved _) => RefreshAll();
    void OnSquadSpawned(SquadSpawned e) => RefreshAll();
    void OnSquadDied(SquadDied e) => RefreshAll();

    void RefreshAll()
    {
        var ws = WorldState.Instance; if (!ws || radarRect == null) return;

        ClearTex(bgColor);
        DrawRoadsAround(PlayerState.Pos, ws);
        tex.Apply();

        poolIdx = 0;
        EnsurePool(32);
        HideAll();

        Vector2 center = PlayerState.Pos;

        TryDot(ws.PlayerBase?.Pos ?? Vector2.zero, baseDotPrefab, center);
        TryDot(ws.Capital?.Pos ?? Vector2.zero, baseDotPrefab, center);
        foreach (var c in ws.Cities) TryDot(c.Pos, cityDotPrefab, center);
        foreach (var k in ws.Camps) TryDot(k.Pos, campDotPrefab, center);
        foreach (var s in ws.EnemySquads)
            TryDot(s.Pos, enemyDotPrefab, center);
    }

    void ClearTex(Color c)
    {
        for (int y = 0; y < texH; y++)
            for (int x = 0; x < texW; x++)
                tex.SetPixel(x, y, c);
    }

    void DrawRoadsAround(Vector2 center, WorldState ws)
    {
        foreach (var r in ws.Roads)
        {
            for (int i = 0; i < r.Path.Count - 1; i++)
            {
                Vector2 a = r.Path[i];
                Vector2 b = r.Path[i + 1];

                // в локальные координаты [-1..1]
                Vector2 la = (a - center) / radarRange;
                Vector2 lb = (b - center) / radarRange;

                if (la.sqrMagnitude > 4f && lb.sqrMagnitude > 4f) continue;

                // в пиксели
                Vector2Int pa = LocalToPixel(la);
                Vector2Int pb = LocalToPixel(lb);
                DrawThickLine(pa, pb, roadColor, roadWidthPx);
            }
        }
    }

    Vector2Int LocalToPixel(Vector2 local) // [-1..1] -> [0..texW/H)
    {
        float u = 0.5f * (local.x + 1f);
        float v = 0.5f * (local.y + 1f);
        int x = Mathf.Clamp(Mathf.RoundToInt(u * (texW - 1)), 0, texW - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(v * (texH - 1)), 0, texH - 1);
        return new Vector2Int(x, y);
    }

    void DrawThickLine(Vector2Int a, Vector2Int b, Color col, int w)
    {
        int dx = Mathf.Abs(b.x - a.x), sx = a.x < b.x ? 1 : -1;
        int dy = -Mathf.Abs(b.y - a.y), sy = a.y < b.y ? 1 : -1;
        int err = dx + dy, e2, x = a.x, y = a.y;
        for (; ; )
        {
            DrawDisc(x, y, w, col);
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
            int yy = cy + y; if (yy < 0 || yy >= texH) continue;
            for (int x = -r; x <= r; x++)
            {
                int xx = cx + x; if (xx < 0 || xx >= texW) continue;
                if (x * x + y * y <= r * r) tex.SetPixel(xx, yy, col);
            }
        }
    }

    void TryDot(Vector2 worldPos, Image prefab, Vector2 center)
    {
        if (!prefab) return;
        float dist = Vector2.Distance(worldPos, center);
        if (dist > radarRange) return;

        Vector2 dir = (worldPos - center) / radarRange; // -1..1
        Vector2 half = radarRect.rect.size * 0.5f;
        Vector2 uiPos = new Vector2(dir.x * half.x, dir.y * half.y);

        var dot = GetDot(prefab);
        dot.rectTransform.SetParent(radarRect, false);
        dot.rectTransform.anchoredPosition = uiPos;
        dot.gameObject.SetActive(true);
    }

    Image GetDot(Image prefab)
    {
        if (poolIdx < pool.Count)
        {
            var it = pool[poolIdx++];
            it.sprite = prefab.sprite;
            it.color = prefab.color;
            it.rectTransform.sizeDelta = prefab.rectTransform.sizeDelta;
            return it;
        }
        var created = Instantiate(prefab, radarRect);
        created.gameObject.SetActive(false);
        pool.Add(created);
        poolIdx++;
        return created;
    }

    void EnsurePool(int min) { while (pool.Count < min) pool.Add(new GameObject("dot", typeof(RectTransform), typeof(Image)).GetComponent<Image>()); }
    void HideAll() { foreach (var it in pool) if (it) it.gameObject.SetActive(false); }
}
