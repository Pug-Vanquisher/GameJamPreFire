using UnityEngine;
using UnityEngine.UI;

public class AudioSettingsManager : MonoBehaviour
{
    [SerializeField] private Slider volumeSlider;
    public static float GlobalVolume { get; private set; } = 1f;

    private const string VolumePrefKey = "GlobalVolume";

    private void Awake()
    {
        GlobalVolume = PlayerPrefs.GetFloat(VolumePrefKey, 1f);
        volumeSlider.value = GlobalVolume;
        volumeSlider.onValueChanged.AddListener(SetVolume);
    }

    public void SetVolume(float value)
    {
        if (Mathf.Abs(GlobalVolume - value) < 0.01f) return;

        GlobalVolume = value;
        PlayerPrefs.SetFloat(VolumePrefKey, GlobalVolume);
        EventManager.Instance.TriggerEvent("VolumeChanged");
    }
}
