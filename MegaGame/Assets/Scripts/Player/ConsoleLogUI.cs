// ConsoleLogUI.cs — стабильная виртуализация: суффиксные суммы + бинпоиск, без трюков с RectTransform
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using Events;

public class ConsoleLogUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text screen;           // один TextMeshProUGUI в рамке консоли
    [SerializeField] private bool wordWrap = true;

    [Header("Colors")]
    [SerializeField] private Color robotColor = new(0.55f, 0.95f, 0.55f, 1f);
    [SerializeField] private Color enemyColor = new(0.95f, 0.45f, 0.45f, 1f);
    [SerializeField] private Color worldColor = Color.white;

    [Header("Behavior")]
    [SerializeField] private int maxHistory = 800;
    [SerializeField] private float scrollStepKeypadPx = 48f;   // NumPad 8/2
    [SerializeField] private float scrollStepMenuFactor = 0.5f;// [9]/[6] — доля экрана
    [SerializeField] private float overscanPx = 4f;            // маленький запас, чтобы точно «добить» окно

    [Header("Progress bar")]
    [SerializeField] private int progressWidth = 22;

    // данные
    private readonly List<string> lines = new(); // rich text
    private readonly List<float> heights = new(); // высота каждой строки при текущей ширине
    private readonly List<float> suf = new(); // суффиксные суммы: sum h[i..N-1]
    private float viewW = -1f, viewH = -1f;        // размеры поля вывода
    private float scrollFromBottom = 0f;           // 0 — у самого низа; >0 — прокручено вверх (px)
    private bool stickToBottom = true;

    // прогресс длительной команды
    private bool progActive;
    private float progStart, progDur;
    private string progTitle = "";
    private float cachedProgressH = -1f;

    string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
    string robotHex, enemyHex, worldHex;

    void Awake()
    {
        if (!screen) screen = GetComponentInChildren<TMP_Text>();
        robotHex = Hex(robotColor); enemyHex = Hex(enemyColor); worldHex = Hex(worldColor);

        if (screen)
        {
            screen.richText = true;
            screen.enableWordWrapping = wordWrap;
            screen.overflowMode = TextOverflowModes.Overflow;
            screen.alignment = TextAlignmentOptions.TopLeft;
        }

        CacheViewSize();
        RebuildAllMetrics();
        RenderVisible(forceBottom: true);
    }

    void OnEnable()
    {
        EventBus.Subscribe<ConsoleMessage>(OnConsoleMessage);
        EventBus.Subscribe<RunStarted>(OnRunStarted);
        EventBus.Subscribe<ConsoleScrollRequest>(OnScrollRequest);
        EventBus.Subscribe<CommandExecutionStarted>(OnCmdStart);
        EventBus.Subscribe<CommandExecutionFinished>(OnCmdFinish);
    }
    void OnDisable()
    {
        EventBus.Unsubscribe<ConsoleMessage>(OnConsoleMessage);
        EventBus.Unsubscribe<RunStarted>(OnRunStarted);
        EventBus.Unsubscribe<ConsoleScrollRequest>(OnScrollRequest);
        EventBus.Unsubscribe<CommandExecutionStarted>(OnCmdStart);
        EventBus.Unsubscribe<CommandExecutionFinished>(OnCmdFinish);
    }

    void Update()
    {
        if (!screen) return;

        // динамика размеров world-canvas → пересчёт метрик
        if (CacheViewSize())
        {
            RebuildAllMetrics();
            RenderVisible();
        }

        // NumPad: 8 — вверх; 2 — вниз
        if (Input.GetKeyDown(KeyCode.Keypad8)) ScrollPixels(+scrollStepKeypadPx);
        if (Input.GetKeyDown(KeyCode.Keypad2)) ScrollPixels(-scrollStepKeypadPx);

        if (progActive) RenderVisible(); // анимация прогресса
    }

    // ===== события =====

    void OnRunStarted(RunStarted _)
    {
        lines.Clear();
        heights.Clear();
        suf.Clear();
        scrollFromBottom = 0f;
        stickToBottom = true;
        progActive = false; progDur = 0f; progTitle = "";
        cachedProgressH = -1f;

        RebuildAllMetrics();
        RenderVisible(forceBottom: true);

        EventBus.Publish(new ConsoleMessage(ConsoleSender.World, "Новый забег начат."));
    }

    void OnConsoleMessage(ConsoleMessage e)
    {
        string ts = e.Ts.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        string hex = e.Sender switch
        {
            ConsoleSender.Robot => robotHex,
            ConsoleSender.Enemy => enemyHex,
            _ => worldHex
        };
        string line = $"<color=#{hex}>[{ts}] {Escape(e.Text)}</color>";

        lines.Add(line);
        if (lines.Count > maxHistory)
        {
            lines.RemoveAt(0);
            if (heights.Count > 0) heights.RemoveAt(0);
        }

        heights.Add(MeasureHeight(line));
        RebuildSuffix(); // обновили suf под новую высоту

        RenderVisible(forceBottom: stickToBottom); // если у низа — прилипнуть к низу
    }

    void OnScrollRequest(ConsoleScrollRequest rq)
    {
        float step = Mathf.Max(8f, viewH * Mathf.Abs(scrollStepMenuFactor));
        ScrollPixels(rq.Delta > 0 ? +step : -step);
    }

    void OnCmdStart(CommandExecutionStarted e)
    {
        progActive = true;
        progStart = Time.time;
        progDur = Mathf.Max(0.01f, e.Duration);
        progTitle = e.Title ?? "";
        cachedProgressH = -1f;
        RenderVisible();
    }
    void OnCmdFinish(CommandExecutionFinished _)
    {
        progActive = false; progDur = 0f; progTitle = "";
        cachedProgressH = -1f;
        RenderVisible(forceBottom: true);
    }

    // ===== виртуализация / скролл =====

    void ScrollPixels(float deltaUp)
    {
        // deltaUp > 0 — прокрутить ВВЕРХ (увеличиваем offset от низа)
        stickToBottom = false;
        float maxOffset = Mathf.Max(0f, TotalContentHeight() - viewH);
        scrollFromBottom = Mathf.Clamp(scrollFromBottom + deltaUp, 0f, maxOffset);
        if (Mathf.Approximately(scrollFromBottom, 0f)) stickToBottom = true;
        RenderVisible();
    }

    void RenderVisible(bool forceBottom = false)
    {
        if (!screen) return;

        float totalH = TotalContentHeight();
        float maxOffset = Mathf.Max(0f, totalH - viewH);
        if (forceBottom) { scrollFromBottom = 0f; stickToBottom = true; }
        else { scrollFromBottom = Mathf.Clamp(scrollFromBottom, 0f, maxOffset); }

        float progH = progActive ? ProgressHeight() : 0f;
        float target = Mathf.Max(0f, viewH - progH) + scrollFromBottom + overscanPx;

        // если нет строк — рисуем только прогресс при необходимости
        if (lines.Count == 0)
        {
            if (progActive)
            {
                screen.text = ProgressLine();
            }
            else screen.text = "";
            return;
        }

        // суффиксные суммы: suf[i] = sum(h[i..N-1]); монотонно убывает при i++
        // нам нужен минимальный i, такой что suf[i] >= target (т.е. блок i..N-1 заполняет окно с учётом скролла)
        int i = FindStartIndexBySuffix(target);
        if (i < 0) i = 0;

        var sb = new System.Text.StringBuilder(512);
        for (int k = i; k < lines.Count; k++)
            sb.AppendLine(lines[k]);

        if (progActive) sb.AppendLine(ProgressLine());

        screen.text = sb.ToString();
    }

    int FindStartIndexBySuffix(float need)
    {
        // бинпоиск по убывающему suf: ищем минимальный i с suf[i] >= need
        int n = suf.Count; if (n == 0) return -1;
        int lo = 0, hi = n - 1, ans = n - 1;

        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (suf[mid] >= need) { ans = mid; hi = mid - 1; }
            else lo = mid + 1;
        }
        return ans;
    }

    // ===== измерения / метрики =====

    bool CacheViewSize()
    {
        if (!screen) return false;
        var rt = screen.rectTransform;
        float w = Mathf.Max(1f, rt.rect.width);
        float h = Mathf.Max(1f, rt.rect.height);
        if (!Mathf.Approximately(viewW, w) || !Mathf.Approximately(viewH, h))
        {
            viewW = w; viewH = h;
            return true;
        }
        return false;
    }

    void RebuildAllMetrics()
    {
        heights.Clear();
        for (int i = 0; i < lines.Count; i++)
            heights.Add(MeasureHeight(lines[i]));
        RebuildSuffix();
        cachedProgressH = -1f;
    }

    void RebuildSuffix()
    {
        suf.Clear();
        float acc = 0f;
        // идём снизу вверх, накапливая сумму; затем развернём
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            acc += (i < heights.Count ? heights[i] : 0f);
            suf.Add(acc);
        }
        suf.Reverse(); // теперь suf[i] = sum(h[i..N-1]) — как нужно
    }

    float MeasureHeight(string rich)
    {
        if (!screen) return 0f;
        var v = screen.GetPreferredValues(rich, viewW, 0f);
        return Mathf.Ceil(v.y + 1f);
    }

    float TotalContentHeight()
    {
        float sum = (suf.Count > 0) ? suf[0] : 0f;
        if (progActive) sum += ProgressHeight();
        return sum;
    }

    float ProgressHeight()
    {
        if (cachedProgressH > 0f) return cachedProgressH;
        string sample = ProgressLineRaw(1f);
        var v = screen.GetPreferredValues(sample, viewW, 0f);
        cachedProgressH = Mathf.Ceil(v.y);
        return cachedProgressH;
    }

    string ProgressLine()
    {
        float t = Mathf.Clamp01((Time.time - progStart) / progDur);
        return ProgressLineRaw(t);
    }

    string ProgressLineRaw(float t)
    {
        int filled = Mathf.Clamp(Mathf.RoundToInt(t * progressWidth), 0, progressWidth);
        string bar = "[" + new string('|', filled) + new string(' ', progressWidth - filled) + $"] {Mathf.RoundToInt(t * 100)}% {progTitle}";
        return $"<color=#{robotHex}>{Escape(bar)}</color>";
    }

    static string Escape(string s) => s?.Replace("<", "&lt;").Replace(">", "&gt;") ?? "";
}
