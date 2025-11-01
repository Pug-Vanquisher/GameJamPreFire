using UnityEngine;

public class UIAudioPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] uiSounds;
    [SerializeField] private float[] volumes;

    public void PlaySound(int index)
    {
        if (index < 0 || index >= uiSounds.Length || index >= volumes.Length)
        {
            Debug.LogWarning("UIAudioPlayer: Неверный индекс звука или громкости");
            return;
        }

        float finalVolume = volumes[index] * AudioSettingsManager.GlobalVolume;
        audioSource.PlayOneShot(uiSounds[index], finalVolume);
    }
}
