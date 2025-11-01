using UnityEngine;
using Events; // <-- EventBus

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip[] sounds;
    [SerializeField] private float[] volumes;

    private int currentMusicIndex = -1;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        EventBus.Subscribe<GlobalVolumeChanged>(OnAnyVolumeChanged);
        EventBus.Subscribe<MusicVolumeChanged>(OnAnyVolumeChanged);
    }

    public void PlaySound(int index)
    {
        if (!IsValidIndex(index)) return;

        // SFX/UI используют глобальную громкость
        float finalVolume = volumes[index] * AudioSettingsManager.GlobalVolume;
        audioSource.PlayOneShot(sounds[index], finalVolume);
    }

    public void PlayMusic(int index)
    {
        if (!IsValidIndex(index)) return;

        if (currentMusicIndex != index)
        {
            musicSource.clip = sounds[index];
            musicSource.loop = true;
            musicSource.Play();
            currentMusicIndex = index;
        }

        ApplyMusicVolume();
    }

    private void OnAnyVolumeChanged(GlobalVolumeChanged _) => ApplyMusicVolume();
    private void OnAnyVolumeChanged(MusicVolumeChanged _) => ApplyMusicVolume();

    private void ApplyMusicVolume()
    {
        if (currentMusicIndex >= 0 && currentMusicIndex < volumes.Length)
        {
            // Музыка использует ТОЛЬКО MusicVolume (по твоему ТЗ)
            musicSource.volume = volumes[currentMusicIndex] * AudioSettingsManager.MusicVolume;
        }
    }

    private bool IsValidIndex(int index)
    {
        if (index < 0 || index >= sounds.Length || index >= volumes.Length)
        {
            Debug.LogWarning("SoundManager: Неверный индекс звука или громкости");
            return false;
        }
        return true;
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<GlobalVolumeChanged>(OnAnyVolumeChanged);
        EventBus.Unsubscribe<MusicVolumeChanged>(OnAnyVolumeChanged);
    }
}
