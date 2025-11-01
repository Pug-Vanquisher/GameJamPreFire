using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Events;

public class ConsoleLogUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private RectTransform content;   
    [SerializeField] private TMP_Text rowPrefab;
    [SerializeField] private Button btnUp;
    [SerializeField] private Button btnDown;

    [Header("Цвета отправителей")]
    [SerializeField] private Color robotColor = new Color(0.55f, 0.95f, 0.55f, 1f);
    [SerializeField] private Color enemyColor = new Color(0.95f, 0.45f, 0.45f, 1f);
    [SerializeField] private Color worldColor = Color.white;

    [Header("Поведение")]
    [SerializeField] private int maxRows = 200;
    [SerializeField] private float scrollStep = 0.15f; 

    void OnEnable()
    {
        EventBus.Subscribe<ConsoleMessage>(OnConsoleMessage);
        if (btnUp) btnUp.onClick.AddListener(() => Nudge(+scrollStep));
        if (btnDown) btnDown.onClick.AddListener(() => Nudge(-scrollStep));
    }
    void OnDisable()
    {
        EventBus.Unsubscribe<ConsoleMessage>(OnConsoleMessage);
        if (btnUp) btnUp.onClick.RemoveAllListeners();
        if (btnDown) btnDown.onClick.RemoveAllListeners();
    }

    void OnConsoleMessage(ConsoleMessage e)
    {
        if (!rowPrefab || !content) return;

        var row = Instantiate(rowPrefab, content);
        row.enableWordWrapping = true;
        row.alignment = TextAlignmentOptions.Left;

        // Префикс времени
        string ts = e.Ts.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        row.text = $"[{ts}] {e.Text}";
        row.color = ColorFor(e.Sender);

        // ограничиваем длину истории
        while (content.childCount > maxRows)
            Destroy(content.GetChild(0).gameObject);

        // автоскролл вниз
        if (scroll) { Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 0f; }
    }

    Color ColorFor(ConsoleSender s) => s switch
    {
        ConsoleSender.Robot => robotColor,
        ConsoleSender.Enemy => enemyColor,
        _ => worldColor
    };

    void Nudge(float delta)
    {
        if (!scroll) return;
        float v = Mathf.Clamp01(scroll.verticalNormalizedPosition + delta);
        scroll.verticalNormalizedPosition = v;
    }
}
