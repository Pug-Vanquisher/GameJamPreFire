using UnityEngine;
using Events;

public class MenuScript : MonoBehaviour
{
    public enum Menu { None = -1, Pause, Tutorial, Settings, Win, Defeat }

    [SerializeField] private GameObject[] window;
    [SerializeField] private GameObject image;
    [SerializeField] private GameRunManager gamemanager;

    private Menu currentMenu;

    void OnEnable() => EventBus.Subscribe<PlayerDied>(Defeat);
    void OnDisable() => EventBus.Unsubscribe<PlayerDied>(Defeat);

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
    }

    public void Pause(bool isPaused)
    {
        Time.timeScale = isPaused ? 0 : 1;
        currentMenu = isPaused ? Menu.Pause : Menu.None;
    }

    public void OpenMenu(int menu) => currentMenu = (Menu)menu;

    public void Restart(string variant)
    {
        if (variant == "new") gamemanager.RestartNewMap();
        if (variant == "same") gamemanager.RestartSameMap();
        Pause(false);
    }

    public void Win() => currentMenu = Menu.Win;

    public void Defeat(PlayerDied died)
    {
        Pause(true);
        currentMenu = Menu.Defeat;
    }

    public void Exit()
    {
        Debug.Log("Quit");
        Application.Quit();
    }
}
