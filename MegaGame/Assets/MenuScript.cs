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
    [SerializeField] private GameRunManager gamemanager;
    private Menu currentMenu;


    void Start()
    {
        EventBus.Subscribe<PlayerDied>(Defeat);
    }

    // Update is called once per frame
    void Update()
    {
        for(int i = 0; i < window.Length; i++)
        {
            window[i].SetActive(i == (int)currentMenu && currentMenu != Menu.None);
        }
    }

    public void Pause(bool isPaused)
    {
        Time.timeScale = (isPaused) ? 0 : 1;
        if (isPaused) { currentMenu = Menu.Pause; }
        else { currentMenu = Menu.None; }
    }

    public void OpenMenu(Menu menu)
    {
        currentMenu = menu;
    }

    public void Restart(string variant)
    {
        if (variant == "new") { gamemanager.RestartNewMap(); }
        if (variant == "same") { gamemanager.RestartSameMap(); }
        Pause(false);
    }

    public void Win()
    {
        currentMenu = Menu.Win;
    }

    public void Defeat(PlayerDied died)
    {
        currentMenu = Menu.Defeat;
    }
    public void Exit()
    {
        Debug.Log("Quit");
        Application.Quit();
    }

}
