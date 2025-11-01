using UnityEngine;
using UnityEngine.UI;
using Events; // <-- EventBus события

public class AudioSettingsManager : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider globalSlider;   // общий (SFX/UI)
    [SerializeField] private Slider musicSlider;    // музыка

    public static float GlobalVolume { get; private set; } = 1f;
    public static float MusicVolume { get; private set; } = 1f;

    private const string GlobalKey = "GlobalVolume";
    private const string MusicKey = "MusicVolume";

    private void Awake()
    {
        GlobalVolume = PlayerPrefs.GetFloat(GlobalKey, 1f);
        MusicVolume = PlayerPrefs.GetFloat(MusicKey, 1f);

        if (globalSlider)
        {
            globalSlider.value = GlobalVolume;
            globalSlider.onValueChanged.AddListener(SetGlobalVolume);
        }

        if (musicSlider)
        {
            musicSlider.value = MusicVolume;
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
        }
    }

    public void SetGlobalVolume(float value)
    {
        if (Mathf.Approximately(GlobalVolume, value)) return;

        GlobalVolume = value;
        PlayerPrefs.SetFloat(GlobalKey, GlobalVolume);
        Events.EventBus.Publish(new GlobalVolumeChanged(value));
    }

    public void SetMusicVolume(float value)
    {
        if (Mathf.Approximately(MusicVolume, value)) return;

        MusicVolume = value;
        PlayerPrefs.SetFloat(MusicKey, MusicVolume);
        Events.EventBus.Publish(new MusicVolumeChanged(value));
    }
}