using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Events;

/// <summary>
/// Асинхронный рендер мировой карты в Texture2D:
/// - заливает биомы сплошными цветами (без декоративных деталей);
/// - по желанию дорисовывает штриховку/границы регионов;
/// - поверх выкладывает дороги + статические маркеры (столица/города/база/лагеря[debug]);
/// - динамически обновляет маркеры врагов (только мобильных, по debug-флагу).
/// </summary>
public class WorldMapRenderer : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RawImage mapImage;
    [SerializeField] private RectTransform overlay;     // слой для UI-иконок поверх карты
    [SerializeField] private Image playerMarker;        // маркер игрока (UI-иконка)

    [Header("Prefabs")]
    [SerializeField] private Image baseIconPrefab;      // у префаба можно добавить TMP подпись в детях
    [SerializeField] private Image cityIconPrefab;      // у префаба сверху — TMP_Text (название)
    [SerializeField] private Image capitalIconPrefab;   // у префаба сверху — TMP_Text (название)
    [SerializeField] private Image campIconPrefab;      // для отладки лагерей на мировой карте
    [SerializeField] private Image enemyIconPrefab;     // маленькая точка для врагов (debug)

    [Header("Icon variants (random pick)")]
    [SerializeField] private List<Sprite> citySpriteVariants = new();
    [SerializeField] private List<Sprite> capitalSpriteVariants = new();
    [SerializeField] private bool showCityNames = true;
    [SerializeField] private bool showCapitalName = true;

    [Header("Texture")]
    [SerializeField] private int texWidth = 1024;
    [SerializeField] private int texHeight = 1024;
    [SerializeField] private FilterMode filter = FilterMode.Point;

    [Header("Async bake")]
    [Tooltip("Сколько строк текстуры закрашивать за кадр во время выпечки")]
    [SerializeField] private int bakeRowsPerFrame = 32;
    [Tooltip("Сколько дорог рисовать за кадр (для Apply раз в несколько шагов)")]
    [SerializeField] private int roadsPerFrame = 16;
    [Tooltip("Автозапекать карту при старте объекта (можно выключить для Boot-сцены).")]
    [SerializeField] private bool autoBakeOnStart = false;

    [Header("Regions overlay (optional)")]
    [SerializeField] private bool drawRegionHatch = true;
    [SerializeField] private bool drawRegionBorders = true;
    [SerializeField] private Color regionHatchColor = new Color(0f, 0f, 0f, 0.14f);
    [SerializeField] private int regionHatchSpacing = 8;
    [SerializeField] private int regionHatchThickness = 1;
    [SerializeField] private Color regionBorderColor = new Color(0f, 0f, 0f, 0.65f);

    [Header("Roads/Nodes colors")]
    [SerializeField] private Color roadColor = new Color(0.75f, 0.70f, 0.60f, 1f);
    [SerializeField] private int roadWidthPx = 2;
    [SerializeField] private Color cityFallbackDot = new Color(0.95f, 0.85f, 0.40f, 1f);
    [SerializeField] private Color capitalFallbackDot = new Color(0.95f, 0.55f, 0.25f, 1f);
    [SerializeField] private Color baseFallbackDot = new Color(0.40f, 0.85f, 0.95f, 1f);
    [SerializeField] private Color campFallbackDot = new Color(0.80f, 0.30f, 0.30f, 1f);
    [SerializeField] private int nodeRadiusPx = 3;

    [Header("Dev toggles")]
    [SerializeField] private bool debugRender = false; // показывать лагеря и мобильных врагов на мировой карте

    // runtime
    private Texture2D tex;
    private Coroutine bakeRoutine;

    // пулы UI-иконок
    readonly List<Image> overlayStatic = new();   // столица/города/база/лагеря (префабы с TMP)
    readonly List<Image> overlayDynamic = new();  // мобильные враги (простые Image)
    int overlayStaticIdx, overlayDynamicIdx;

    // ---------- Lifecycle / Events ----------
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
        if (autoBakeOnStart)
            RestartBake(null);
        else
            UpdatePlayerMarker(); // чтобы маркер не зависал в (0,0), если карту печёт Boot
    }

    // ---------- Public API for Boot scene ----------
    /// <summary> Запустить асинхронную выпечку карты (для загрузочного экрана). </summary>
    public IEnumerator BakeAsync(System.Action<float> onProgress)
    {
        if (bakeRoutine != null) StopCoroutine(bakeRoutine);
        yield return StartCoroutine(BakeAllCoroutine(onProgress));
    }

    // ---------- Internal triggers ----------
    void OnMapGenerated(MapGenerated _)
    {
        RestartBake(null);
    }
    void OnRoadBuilt(RoadBuilt _)
    {
        // дороги рисуются внутри BakeAll, но если пришли новые после — можно дорисовать быстро:
        RedrawRoadsAndNodesOnly();
    }
    void OnNodeSpawned(NodeSpawned _)
    {
        // свежие узлы появятся в статическом оверлее
        RedrawStaticOverlayOnly();
    }
    void OnPlayerMoved(PlayerMoved _) => UpdatePlayerMarker();
    void OnSquadSpawned(SquadSpawned _) => UpdateEnemiesOverlay();
    void OnSquadDied(SquadDied _) => UpdateEnemiesOverlay();
    void OnSquadMoved(SquadMoved e) { if (!e.IsGarrison) UpdateEnemiesOverlay(); }

    // ---------- Bake orchestration ----------
    void RestartBake(System.Action<float> onProgress)
    {
        if (!gameObject.activeInHierarchy) return;
        if (bakeRoutine != null) StopCoroutine(bakeRoutine);
        bakeRoutine = StartCoroutine(BakeAllCoroutine(onProgress));
    }

    IEnumerator BakeAllCoroutine(System.Action<float> onProgress)
    {
        var ws = WorldState.Instance;
        if (!ws) yield break;

        EnsureTexture();

        // фаза 1 — заливаем биомы
        yield return StartCoroutine(BakeBiomesCoroutine(onProgress, 0.00f, 0.80f));

        // фаза 2 — региональные оверлеи (опционально)
        if (drawRegionHatch)
        {
            yield return StartCoroutine(DrawRegionHatchCoroutine(onProgress, 0.80f, 0.88f));
        }
        if (drawRegionBorders)
        {
            yield return StartCoroutine(DrawRegionBordersCoroutine(onProgress, 0.88f, 0.92f));
        }

        // фаза 3 — дороги (поштучно, чтобы не фризить кадр)
        yield return StartCoroutine(DrawRoadsCoroutine(onProgress, 0.92f, 0.98f));

        // финал — статический оверлей (иконки) + враги
        RebuildStaticOverlay();
        UpdateEnemiesOverlay();
        tex.Apply(false, false);
        onProgress?.Invoke(1f);
        bakeRoutine = null;
    }

    // ---------- Phase 1: Biomes (solid fill) ----------
    IEnumerator BakeBiomesCoroutine(System.Action<float> onProgress, float a, float b)
    {
        var ws = WorldState.Instance; if (!ws) yield break;

        int rows = Mathf.Max(1, bakeRowsPerFrame);
        for (int y = 0; y < texHeight; y++)
        {
            for (int x = 0; x < texWidth; x++)
            {
                float u = x / (float)(texWidth - 1);
                float v = y / (float)(texHeight - 1);
                Vector2 world = new(
                    Mathf.Lerp(-ws.MapHalfSize, ws.MapHalfSize, u),
                    Mathf.Lerp(-ws.MapHalfSize, ws.MapHalfSize, v));

                if (ws.Biomes)
                {
                    float raw = ws.SampleBiomeRaw(world);
                    var biome = ws.Biomes.Evaluate(raw);
                    tex.SetPixel(x, y, ws.Biomes.ColorOf(biome));
                }
                else
                {
                    tex.SetPixel(x, y, new Color(0.12f, 0.14f, 0.12f, 1f));
                }
            }

            if ((y % rows) == 0)
            {
                tex.Apply(false, false);
                onProgress?.Invoke(Mathf.Lerp(a, b, y / (float)(texHeight - 1)));
                yield return null;
            }
        }
    }

    // ---------- Phase 2a: Region hatch ----------
    IEnumerator DrawRegionHatchCoroutine(System.Action<float> onProgress, float a, float b)
    {
        var ws = WorldState.Instance; if (!ws) yield break;
        if (ws.Regions == null || ws.Regions.Count == 0) yield break;

        int rows = Mathf.Max(1, bakeRowsPerFrame);
        for (int y = 0; y < texHeight; y++)
        {
            for (int x = 0; x < texWidth; x++)
            {
                float u = x / (float)(texWidth - 1);
                float v = y / (float)(texHeight - 1);
                Vector2 world = new(
                    Mathf.Lerp(-ws.MapHalfSize, ws.MapHalfSize, u),
                    Mathf.Lerp(-ws.MapHalfSize, ws.MapHalfSize, v));

                int id = ws.GetRegionId(world);
                int phase = 3 * id + 7;
                int s = regionHatchSpacing;
                if (((x + y + phase) % s) < regionHatchThickness)
                {
                    var baseC = tex.GetPixel(x, y);
                    var mix = Color.Lerp(baseC, regionHatchColor, regionHatchColor.a);
                    mix.a = 1f;
                    tex.SetPixel(x, y, mix);
                }
            }

            if ((y % rows) == 0)
            {
                tex.Apply(false, false);
                onProgress?.Invoke(Mathf.Lerp(a, b, y / (float)(texHeight - 1)));
                yield return null;
            }
        }
    }

    // ---------- Phase 2b: Region borders ----------
    IEnumerator DrawRegionBordersCoroutine(System.Action<float> onProgress, float a, float b)
    {
        var ws = WorldState.Instance; if (!ws) yield break;
        if (ws.Regions == null || ws.Regions.Count == 0) yield break;

        int rows = Mathf.Max(1, bakeRowsPerFrame);
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

            if ((y % rows) == 0)
            {
                tex.Apply(false, false);
                onProgress?.Invoke(Mathf.Lerp(a, b, y / (float)(texHeight - 1)));
                yield return null;
            }
        }
    }

    // ---------- Phase 3: Roads ----------
    IEnumerator DrawRoadsCoroutine(System.Action<float> onProgress, float a, float b)
    {
        var ws = WorldState.Instance; if (!ws) yield break;
        int count = ws.Roads.Count;
        if (count == 0) yield break;

        int per = Mathf.Max(1, roadsPerFrame);
        for (int i = 0; i < count; i++)
        {
            DrawPolyline(ws.Roads[i].Path, roadColor, roadWidthPx);

            if ((i % per) == 0)
            {
                tex.Apply(false, false);
                onProgress?.Invoke(Mathf.Lerp(a, b, i / (float)Mathf.Max(1, count - 1)));
                yield return null;
            }
        }
    }

    // ---------- Static & dynamic overlays ----------
    void RebuildStaticOverlay()
    {
        overlayStaticIdx = 0;

        var ws = WorldState.Instance; if (!ws) return;

        // столица
        if (ws.Capital != null)
        {
            if (capitalIconPrefab)
                PlaceMarkerStatic(capitalIconPrefab, ws.Capital.Pos, showCapitalName ? ws.Capital.Name : null, capitalSpriteVariants);
            else
                DrawNode(ws.Capital.Pos, capitalFallbackDot, nodeRadiusPx + 1);
        }

        // база игрока
        if (ws.PlayerBase != null)
        {
            if (baseIconPrefab)
                PlaceMarkerStatic(baseIconPrefab, ws.PlayerBase.Pos, null, null);
            else
                DrawNode(ws.PlayerBase.Pos, baseFallbackDot, nodeRadiusPx + 1);
        }

        // города
        foreach (var c in ws.Cities)
        {
            if (cityIconPrefab)
                PlaceMarkerStatic(cityIconPrefab, c.Pos, showCityNames ? c.Name : null, citySpriteVariants);
            else
                DrawNode(c.Pos, cityFallbackDot, nodeRadiusPx);
        }

        // лагеря — только при debug
        if (debugRender)
        {
            foreach (var k in ws.Camps)
            {
                if (campIconPrefab)
                    PlaceMarkerStatic(campIconPrefab, k.Pos, null, null);
                else
                    DrawNode(k.Pos, campFallbackDot, nodeRadiusPx);
            }
        }

        HideRest(overlayStatic, overlayStaticIdx);
    }

    void RedrawStaticOverlayOnly()
    {
        // перерисовываем только статические иконки (без повторной выпечки текстуры)
        RebuildStaticOverlay();
        UpdatePlayerMarker();
    }

    void RedrawRoadsAndNodesOnly()
    {
        // быстрый догон: перерисовать только дороги поверх уже выпеченных биомов
        var ws = WorldState.Instance; if (!ws) return;
        foreach (var r in ws.Roads) DrawPolyline(r.Path, roadColor, roadWidthPx);
        tex.Apply(false, false);
        RedrawStaticOverlayOnly();
    }

    void UpdateEnemiesOverlay()
    {
        var ws = WorldState.Instance; if (!ws || !overlay) return;

        overlayDynamicIdx = 0;
        if (!debugRender || !enemyIconPrefab)
        {
            HideAll(overlayDynamic);
            return;
        }

        EnsureOverlayCapacitySimple(overlayDynamic, Mathf.Max(64, ws.EnemySquads.Count));

        foreach (var s in ws.EnemySquads)
        {
            if (s.IsGarrison) continue;
            var dot = GetFromPoolSimple(overlayDynamic, overlayDynamicIdx++, enemyIconPrefab);
            dot.rectTransform.SetParent(overlay, false);
            dot.rectTransform.anchoredPosition = MapRenderUtils.WorldToOverlayAnchored(s.Pos, ws.MapHalfSize, overlay);
            dot.gameObject.SetActive(true);
        }
        HideRest(overlayDynamic, overlayDynamicIdx);
    }

    void UpdatePlayerMarker()
    {
        var ws = WorldState.Instance;
        if (!ws || !playerMarker || !overlay) return;
        playerMarker.rectTransform.anchoredPosition =
            MapRenderUtils.WorldToOverlayAnchored(PlayerState.Pos, ws.MapHalfSize, overlay);
    }

    // ---------- Low-level drawing ----------
    void EnsureTexture()
    {
        if (tex != null) return;
        tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = filter
        };
        if (mapImage) mapImage.texture = tex;
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

    // ---------- Markers pooling ----------
    void PlaceMarkerStatic(Image prefab, Vector2 worldPos, string labelText, List<Sprite> variants)
    {
        if (!overlay || !prefab) return;
        var ws = WorldState.Instance; if (!ws) return;

        var img = GetFromPoolPrefab(overlayStatic, overlayStaticIdx++, prefab);
        img.rectTransform.SetParent(overlay, false);
        img.rectTransform.anchoredPosition = MapRenderUtils.WorldToOverlayAnchored(worldPos, ws.MapHalfSize, overlay);

        // Рандомный вариант спрайта
        if (variants != null && variants.Count > 0)
        {
            var pick = variants[Random.Range(0, variants.Count)];
            if (pick) img.sprite = pick;
        }
        else
        {
            img.sprite = prefab.sprite; // дефолт
        }

        // Подпись
        var label = img.GetComponentInChildren<TMP_Text>(true);
        if (label)
        {
            if (!string.IsNullOrEmpty(labelText))
            {
                label.text = labelText;
                label.gameObject.SetActive(true);
            }
            else
            {
                label.text = string.Empty;
                label.gameObject.SetActive(false);
            }
        }

        img.gameObject.SetActive(true);
    }

    Image GetFromPoolPrefab(List<Image> pool, int index, Image prefab)
    {
        if (index < pool.Count && pool[index] != null)
        {
            var it = pool[index];
            // сбросим базовые параметры
            it.color = prefab.color;
            it.rectTransform.sizeDelta = prefab.rectTransform.sizeDelta;
            it.sprite = prefab.sprite;
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

    Image GetFromPoolSimple(List<Image> pool, int index, Image prefabShape)
    {
        if (index < pool.Count && pool[index] != null)
        {
            var it = pool[index];
            it.sprite = prefabShape.sprite;
            it.color = prefabShape.color;
            it.rectTransform.sizeDelta = prefabShape.rectTransform.sizeDelta;
            return it;
        }
        else
        {
            var created = Instantiate(prefabShape, overlay);
            if (index >= pool.Count) pool.Add(created);
            else pool[index] = created;
            created.gameObject.SetActive(false);
            return created;
        }
    }

    void EnsureOverlayCapacitySimple(List<Image> pool, int min)
    {
        while (pool.Count < min)
        {
            var img = new GameObject("marker", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
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
