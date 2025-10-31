using UnityEngine;

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
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        EventManager.Instance.Subscribe("VolumeChanged", MusicVolume);
    }

    public void PlaySound(int index)
    {
        if (!IsValidIndex(index)) return;

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

        MusicVolume();
    }

    private void MusicVolume()
    {
        if (currentMusicIndex >= 0 && currentMusicIndex < volumes.Length)
        {
            musicSource.volume = volumes[currentMusicIndex] * AudioSettingsManager.GlobalVolume;
            Debug.Log("звук должен измениться");
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
        EventManager.Instance.Unsubscribe("VolumeChanged", MusicVolume);
    }
}