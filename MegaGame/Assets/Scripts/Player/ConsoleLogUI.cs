using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using Events;

public class ConsoleLogUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text screen;        // видимый TextMeshProUGUI
    [Tooltip("Необязательный скрытый текст для измерения. Если пусто — создадим автоматом.")]
    [SerializeField] private TMP_Text meter;         // скрытый измеритель lineCount
    [SerializeField] private bool wordWrap = true;

    [Header("Manual window (lines)")]
    [Tooltip("Сколько визуальных строк помещаем в окно (с учётом переноса). Прогресс занимает 1 строку.")]
    [SerializeField, Min(1)] private int visibleLines = 12;
    [Tooltip("Шаг прокрутки по строкам (NumPad 8/2, [9]/[6]).")]
    [SerializeField, Min(1)] private int scrollStepLines = 4;

    [Header("Colors")]
    [SerializeField] private Color robotColor = new(0.55f, 0.95f, 0.55f, 1f);
    [SerializeField] private Color enemyColor = new(0.95f, 0.45f, 0.45f, 1f);
    [SerializeField] private Color worldColor = Color.white;

    [Header("History")]
    [SerializeField] private int maxHistory = 800;

    [Header("Progress bar")]
    [SerializeField] private int progressWidth = 22;

    // данные
    private readonly List<string> entries = new(); // rich-тексты
    private readonly List<int> entryLines = new(); // визуальные строки каждого entry
    private int tailSkipLines = 0;                 // сколько строк «от низа» скрываем (скролл)
    private bool stickToBottom = true;             // прилипание к низу

    // прогресс команды
    private bool progActive;
    private float progStart, progDur;
    private string progTitle = "";

    // кеши
    private float lastWidth = -1f;
    private string robotHex, enemyHex, worldHex;

    void Awake()
    {
        if (!screen) screen = GetComponentInChildren<TMP_Text>();
        robotHex = ColorUtility.ToHtmlStringRGB(robotColor);
        enemyHex = ColorUtility.ToHtmlStringRGB(enemyColor);
        worldHex = ColorUtility.ToHtmlStringRGB(worldColor);

        // базовые настройки видимого текста
        screen.richText = true;
        screen.enableWordWrapping = wordWrap;
        screen.overflowMode = TextOverflowModes.Overflow;
        screen.alignment = TextAlignmentOptions.TopLeft;

        // подготовим измеритель
        SetupMeter();

        RecalculateAllLineCounts();
        RenderWindow(forceBottom: true);
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

        // реагируем на изменение ширины (world canvas масштабы и т.п.)
        float w = Mathf.Max(1f, screen.rectTransform.rect.width);
        if (!Mathf.Approximately(w, lastWidth))
        {
            lastWidth = w;
            SyncMeterWidth();
            RecalculateAllLineCounts();
            ClampScroll();
            RenderWindow();
        }

        // NumPad: 8 — вверх, 2 — вниз (но блокируем при выполнении команды)
        if (!progActive)
        {
            if (Input.GetKeyDown(KeyCode.Keypad8)) ScrollUp(scrollStepLines);
            if (Input.GetKeyDown(KeyCode.Keypad2)) ScrollDown(scrollStepLines);
        }

        if (progActive) RenderWindow(); // двигаем индикатор прогресса
    }

    // ---------- EVENTS ----------

    void OnRunStarted(RunStarted _)
    {
        entries.Clear();
        entryLines.Clear();
        tailSkipLines = 0;
        stickToBottom = true;
        progActive = false; progDur = 0f; progTitle = "";

        RecalculateAllLineCounts();
        RenderWindow(forceBottom: true);

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
        string msg = $"<color=#{hex}>[{ts}] {Escape(e.Text)}</color>";

        entries.Add(msg);
        if (entries.Count > maxHistory)
        {
            int cut = entries.Count - maxHistory;
            entries.RemoveRange(0, cut);
            if (entryLines.Count >= cut) entryLines.RemoveRange(0, cut);
            // подрезаем и скролл
            ClampScroll();
        }

        int linesAdded = MeasureLines(msg);
        entryLines.Add(linesAdded);

        // Если выполняется команда — всегда держимся у низа и не даём скроллить
        if (progActive)
        {
            tailSkipLines = 0;
            stickToBottom = true;
        }
        else
        {
            // Если НЕ у низа — сохраняем визуальную позицию (добавляя новые линии к смещению)
            if (!stickToBottom && tailSkipLines > 0)
                tailSkipLines += linesAdded;
            else
                tailSkipLines = 0; // прилипание к низу
        }

        ClampScroll();
        RenderWindow();
    }

    void OnScrollRequest(ConsoleScrollRequest rq)
    {
        // ВАЖНО: [9] — вверх, [6] — вниз. Игнорируем во время прогресса.
        if (progActive) return;

        if (rq.Delta > 0) ScrollUp(scrollStepLines * 2);   // вверх (к старым логам)
        else ScrollDown(scrollStepLines * 2); // вниз (к новым)
    }

    void OnCmdStart(CommandExecutionStarted e)
    {
        // При запуске команды: уходим вниз, блокируем прокрутку до завершения
        progActive = true;
        progStart = Time.time;
        progDur = Mathf.Max(0.01f, e.Duration);
        progTitle = e.Title ?? "";
        tailSkipLines = 0;
        stickToBottom = true;
        RenderWindow(forceBottom: true);
    }

    void OnCmdFinish(CommandExecutionFinished _)
    {
        // После завершения: снова вниз и показываем результат в самом низу
        progActive = false; progDur = 0f; progTitle = "";
        tailSkipLines = 0;
        stickToBottom = true;
        ClampScroll();
        RenderWindow(forceBottom: true);
    }

    // ---------- CORE: manual window by lines ----------

    void RenderWindow(bool forceBottom = false)
    {
        if (forceBottom) { tailSkipLines = 0; stickToBottom = true; }

        int totalVisual = TotalLines() + (progActive ? 1 : 0);
        int windowLines = Mathf.Max(1, visibleLines);
        int payloadLines = Mathf.Max(0, windowLines - (progActive ? 1 : 0));

        // ограничим скролл
        int maxSkip = Mathf.Max(0, totalVisual - windowLines);
        tailSkipLines = Mathf.Clamp(tailSkipLines, 0, maxSkip);

        // вычисляем старт по «линиям от низа»
        int startIdx = entries.Count - 1;
        int toSkip = tailSkipLines;
        for (int i = entries.Count - 1; i >= 0 && toSkip > 0; i--)
        {
            toSkip -= entryLines[i];
            startIdx = i - 1;
        }
        if (startIdx < -1) startIdx = -1;

        // набираем окно на payloadLines линий
        var stack = new Stack<string>();
        int taken = 0;
        for (int i = startIdx; i >= 0 && taken < payloadLines; i--)
        {
            stack.Push(entries[i]);
            taken += entryLines[i];
        }

        var sb = new StringBuilder(512);
        foreach (var line in stack) sb.AppendLine(line);
        if (progActive) sb.AppendLine(ProgressLine(ProgressT()));

        screen.text = sb.ToString();

        stickToBottom = (tailSkipLines == 0);
    }

    // ---------- scroll helpers (чётное направление) ----------

    void ScrollUp(int lines)   // к старым логам
    {
        if (lines <= 0) return;
        int maxSkip = Mathf.Max(0, TotalLines() + (progActive ? 1 : 0) - visibleLines);
        tailSkipLines = Mathf.Clamp(tailSkipLines + lines, 0, maxSkip);
        stickToBottom = (tailSkipLines == 0);
        RenderWindow();
    }
    void ScrollDown(int lines) // к новым логам
    {
        if (lines <= 0) return;
        tailSkipLines = Mathf.Max(0, tailSkipLines - lines);
        stickToBottom = (tailSkipLines == 0);
        RenderWindow();
    }

    int TotalLines()
    {
        int sum = 0;
        for (int i = 0; i < entryLines.Count; i++) sum += entryLines[i];
        return sum;
    }

    void ClampScroll()
    {
        int maxSkip = Mathf.Max(0, TotalLines() + (progActive ? 1 : 0) - visibleLines);
        tailSkipLines = Mathf.Clamp(tailSkipLines, 0, maxSkip);
        if (tailSkipLines == 0) stickToBottom = true;
    }

    // ---------- measuring ----------

    void SetupMeter()
    {
        if (meter) { CopyVisualSettings(meter); lastWidth = Mathf.Max(1f, screen.rectTransform.rect.width); SyncMeterWidth(); return; }

        var go = new GameObject("ConsoleLog_Meter", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(screen.rectTransform.parent, false);
        meter = go.GetComponent<TextMeshProUGUI>();
        CopyVisualSettings(meter);
        meter.alpha = 0f;                       // невидим
        meter.gameObject.SetActive(true);       // но активен, чтобы работал ForceMeshUpdate

        lastWidth = Mathf.Max(1f, screen.rectTransform.rect.width);
        SyncMeterWidth();
    }

    void CopyVisualSettings(TMP_Text dst)
    {
        var src = screen;
        var rtd = dst.rectTransform;
        rtd.anchorMin = rtd.anchorMax = new Vector2(0, 1);
        rtd.pivot = new Vector2(0, 1);
        rtd.anchoredPosition = new Vector2(-99999, 99999); // далеко за экран
        dst.richText = true;
        dst.enableWordWrapping = wordWrap;
        dst.overflowMode = TextOverflowModes.Overflow;
        dst.alignment = TextAlignmentOptions.TopLeft;

        if (dst is TextMeshProUGUI u && src is TextMeshProUGUI v)
        {
            u.font = v.font;
            u.fontSize = v.fontSize;
            u.lineSpacing = v.lineSpacing;
            u.enableAutoSizing = false;
        }
    }

    void SyncMeterWidth()
    {
        if (!meter) return;
        var rt = meter.rectTransform;
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(1f, screen.rectTransform.rect.width));
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 2000f);
    }

    int MeasureLines(string rich)
    {
        meter.text = rich;
        meter.ForceMeshUpdate();
        return Mathf.Max(1, meter.textInfo.lineCount);
    }

    void RecalculateAllLineCounts()
    {
        entryLines.Clear();
        for (int i = 0; i < entries.Count; i++)
            entryLines.Add(MeasureLines(entries[i]));
    }

    // ---------- progress ----------

    float ProgressT() => Mathf.Clamp01((Time.time - progStart) / progDur);

    string ProgressLine(float t)
    {
        int filled = Mathf.Clamp(Mathf.RoundToInt(t * progressWidth), 0, progressWidth);
        string bar = "[" + new string('|', filled) + new string(' ', progressWidth - filled) + $"] {Mathf.RoundToInt(t * 100)}% {progTitle}";
        return $"<color=#{robotHex}>{Escape(bar)}</color>";
    }

    // ---------- utils ----------

    static string Escape(string s) => s?.Replace("<", "&lt;").Replace(">", "&gt;") ?? "";
}
