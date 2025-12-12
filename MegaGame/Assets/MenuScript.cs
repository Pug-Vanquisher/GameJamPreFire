using UnityEngine;
using Events;

public class MenuScript : MonoBehaviour
{
    public enum Menu { None = -1, Pause, Tutorial, Settings, Win, Defeat }

    [SerializeField] private GameObject[] window;
    [SerializeField] private GameObject image;

    [Header("Win Black Screen")]
    [SerializeField] private GameObject winBlackScreen;

    [SerializeField] private GameRunManager gamemanager;

    [Header("Boot/Restart Behaviour")]
    [Tooltip("Всегда показывать меню паузы при старте игры")]
    [SerializeField] private bool pauseOnBoot = true;

    [Tooltip("Всегда показывать меню паузы при каждом RunStarted (включая рестарты)")]
    [SerializeField] private bool pauseOnRunStarted = true;

    private Menu currentMenu;
    private bool blackoutActive;

    void Awake()
    {
        // ВАЖНО: игра должна быть на паузе при запуске (ещё до первых событий)
        if (pauseOnBoot)
            EnterPauseMenu(clearBlackout: true);
    }

    void OnEnable()
    {
        EventBus.Subscribe<PlayerDied>(Defeat);
        EventBus.Subscribe<PlayerWon>(OnWin);
        EventBus.Subscribe<VictoryBlackoutChanged>(OnBlackout);
        EventBus.Subscribe<RunStarted>(OnRunStarted);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<PlayerDied>(Defeat);
        EventBus.Unsubscribe<PlayerWon>(OnWin);
        EventBus.Unsubscribe<VictoryBlackoutChanged>(OnBlackout);
        EventBus.Unsubscribe<RunStarted>(OnRunStarted);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Pause(currentMenu == Menu.None);

        bool showImage = false;
        for (int i = 0; i < window.Length; i++)
        {
            bool active = (i == (int)currentMenu && currentMenu != Menu.None);
            window[i].SetActive(active);
            if (active) showImage = true;
        }

        if (image) image.SetActive(showImage);
        if (winBlackScreen) winBlackScreen.SetActive(blackoutActive);
    }

    // === Главное: вход в меню паузы (старт/рестарт) ===
    void EnterPauseMenu(bool clearBlackout)
    {
        if (clearBlackout)
        {
            blackoutActive = false;
            if (winBlackScreen) winBlackScreen.SetActive(false);
        }

        Time.timeScale = 0;
        currentMenu = Menu.Pause;
    }

    public void Pause(bool isPaused)
    {
        Time.timeScale = isPaused ? 0 : 1;
        currentMenu = isPaused ? Menu.Pause : Menu.None;
    }

    public void OpenMenu(int menu) => currentMenu = (Menu)menu;

    public void Restart(string variant)
    {
        // При рестарте: сразу показываем меню и держим паузу
        EnterPauseMenu(clearBlackout: true);

        if (variant == "new") gamemanager.RestartNewMap();
        if (variant == "same") gamemanager.RestartSameMap();

        // ВАЖНО: НЕ снимаем паузу здесь
        // Pause(false);  <-- убрать
    }

    public void Win()
    {
        // Победа: открываем Win меню и ставим паузу
        Time.timeScale = 0;
        currentMenu = Menu.Win;
    }

    public void Defeat(PlayerDied _)
    {
        blackoutActive = false;
        if (winBlackScreen) winBlackScreen.SetActive(false);

        Time.timeScale = 0;
        currentMenu = Menu.Defeat;
    }

    void OnWin(PlayerWon _)
    {
        Win();
    }

    void OnBlackout(VictoryBlackoutChanged e)
    {
        blackoutActive = e.Active;
        if (winBlackScreen) winBlackScreen.SetActive(blackoutActive);
    }

    void OnRunStarted(RunStarted _)
    {
        // Раньше тут был Pause(false) — из-за этого игра «отжималась».
        // Теперь по требованию: старт/перезапуск => всегда пауза + меню.
        if (pauseOnRunStarted)
            EnterPauseMenu(clearBlackout: true);
    }

    public void Exit()
    {
        Debug.Log("Quit");
        Application.Quit();
    }
}
