using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using Events;

public class ConsoleLogUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text screen;     
    [SerializeField] private TMP_Text meter;        
    [SerializeField] private bool wordWrap = true;

    [Header("Линии")]
    [SerializeField, Min(1)] private int visibleLines = 12;
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
    private readonly List<string> entries = new(); 
    private readonly List<int> entryLines = new(); 
    private int tailSkipLines = 0;                
    private bool stickToBottom = true;           

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

        // базовые настройки
        screen.richText = true;
        screen.enableWordWrapping = wordWrap;
        screen.overflowMode = TextOverflowModes.Overflow;
        screen.alignment = TextAlignmentOptions.TopLeft;

        // измеритель
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

        float w = Mathf.Max(1f, screen.rectTransform.rect.width);
        if (!Mathf.Approximately(w, lastWidth))
        {
            lastWidth = w;
            SyncMeterWidth();
            RecalculateAllLineCounts();
            ClampScroll();
            RenderWindow();
        }

        // NumPad
        if (!progActive)
        {
            if (Input.GetKeyDown(KeyCode.Keypad8)) ScrollUp(scrollStepLines);
            if (Input.GetKeyDown(KeyCode.Keypad2)) ScrollDown(scrollStepLines);
        }

        if (progActive) RenderWindow(); //индикатор прогресса
    }

    void OnRunStarted(RunStarted _)
    {
        entries.Clear();
        entryLines.Clear();
        tailSkipLines = 0;
        stickToBottom = true;
        progActive = false; progDur = 0f; progTitle = "";

        RecalculateAllLineCounts();
        RenderWindow(forceBottom: true);
    }

    void OnConsoleMessage(ConsoleMessage e)
    {
        string ts = e.Ts.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        SoundManager.Instance.PlaySound(1);
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
            ClampScroll();
        }

        int linesAdded = MeasureLines(msg);
        entryLines.Add(linesAdded);

        if (progActive)
        {
            tailSkipLines = 0;
            stickToBottom = true;
        }
        else
        {
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
        if (progActive) return;

        if (rq.Delta > 0) ScrollUp(scrollStepLines * 2); 
        else ScrollDown(scrollStepLines * 2);
    }

    void OnCmdStart(CommandExecutionStarted e)
    {
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
        progActive = false; progDur = 0f; progTitle = "";
        tailSkipLines = 0;
        stickToBottom = true;
        ClampScroll();
        RenderWindow(forceBottom: true);
    }

    void RenderWindow(bool forceBottom = false)
    {
        if (forceBottom) { tailSkipLines = 0; stickToBottom = true; }

        int totalVisual = TotalLines() + (progActive ? 1 : 0);
        int windowLines = Mathf.Max(1, visibleLines);
        int payloadLines = Mathf.Max(0, windowLines - (progActive ? 1 : 0));

        // скролл
        int maxSkip = Mathf.Max(0, totalVisual - windowLines);
        tailSkipLines = Mathf.Clamp(tailSkipLines, 0, maxSkip);

        // старт
        int startIdx = entries.Count - 1;
        int toSkip = tailSkipLines;
        for (int i = entries.Count - 1; i >= 0 && toSkip > 0; i--)
        {
            toSkip -= entryLines[i];
            startIdx = i - 1;
        }
        if (startIdx < -1) startIdx = -1;

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


    void ScrollUp(int lines)   // Вверх
    {
        if (lines <= 0) return;
        int maxSkip = Mathf.Max(0, TotalLines() + (progActive ? 1 : 0) - visibleLines);
        tailSkipLines = Mathf.Clamp(tailSkipLines + lines, 0, maxSkip);
        stickToBottom = (tailSkipLines == 0);
        RenderWindow();
    }
    void ScrollDown(int lines) // Вниз
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

    void SetupMeter()
    {
        if (meter) { CopyVisualSettings(meter); lastWidth = Mathf.Max(1f, screen.rectTransform.rect.width); SyncMeterWidth(); return; }

        var go = new GameObject("ConsoleLog_Meter", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(screen.rectTransform.parent, false);
        meter = go.GetComponent<TextMeshProUGUI>();
        CopyVisualSettings(meter);
        meter.alpha = 0f;                      
        meter.gameObject.SetActive(true);       

        lastWidth = Mathf.Max(1f, screen.rectTransform.rect.width);
        SyncMeterWidth();
    }

    void CopyVisualSettings(TMP_Text dst)
    {
        var src = screen;
        var rtd = dst.rectTransform;
        rtd.anchorMin = rtd.anchorMax = new Vector2(0, 1);
        rtd.pivot = new Vector2(0, 1);
        rtd.anchoredPosition = new Vector2(-99999, 99999); 
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

    float ProgressT() => Mathf.Clamp01((Time.time - progStart) / progDur);

    string ProgressLine(float t)
    {
        int filled = Mathf.Clamp(Mathf.RoundToInt(t * progressWidth), 0, progressWidth);
        string bar = "[" + new string('|', filled) + new string(' ', progressWidth - filled) + $"] {Mathf.RoundToInt(t * 100)}% {progTitle}";
        return $"<color=#{robotHex}>{Escape(bar)}</color>";
    }

    static string Escape(string s) => s?.Replace("<", "&lt;").Replace(">", "&gt;") ?? "";
}
