using UnityEngine;
using Events;

public class MenuScript : MonoBehaviour
{
    public enum Menu
    {
        None = -1,
        Pause,
        Tutorial,
        Settings,
        Win,
        Defeat
    }

    [SerializeField] private GameObject[] window;
    [SerializeField] private GameObject image;
    [SerializeField] private GameObject image2;
    [SerializeField] private GameRunManager gamemanager;
    private Menu currentMenu;


    void Start()
    {
        EventBus.Subscribe<PlayerDied>(Defeat);
        EventBus.Subscribe<WinCondition>(Win);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Pause(currentMenu == Menu.None);
        }

        bool showImage = false;
        for(int i = 0; i < window.Length; i++)
        {
            window[i].SetActive(i == (int)currentMenu && currentMenu != Menu.None);
            if (i == (int)currentMenu && currentMenu != Menu.None) { showImage = true; }
        }
        if(currentMenu == Menu.Tutorial)
        {
            image.SetActive(false);
            image2.SetActive(true);
        }
        else
        {
            image.SetActive(showImage);
            image2.SetActive(false);
        }
    }

    public void Pause(bool isPaused)
    {
        Time.timeScale = (isPaused) ? 0 : 1;
        currentMenu = (isPaused)? Menu.Pause : Menu.None;
    }

    public void OpenMenu(int menu)
    {
        currentMenu = (Menu)menu;
    }

    public void Restart(string variant)
    {
        if (variant == "new") { gamemanager.RestartNewMap(); }
        if (variant == "same") { gamemanager.RestartSameMap(); }
        Pause(false);
    }

    public void Win(WinCondition win)
    {
        currentMenu = Menu.Win;
    }

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
