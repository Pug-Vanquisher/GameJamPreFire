using UnityEngine;
using TMPro;
using Events;
public class panel : MonoBehaviour
{
    public float maximum;
    public float minimum;
    public float current;
    public float step;
    public int poinCount;
    [SerializeField] private string key;

    public TMP_Text text;


    private void Start()
    {
        current = PlayerPrefs.GetFloat(key, 1f);
    }

    public void Change(int sign)
    {
        current = Mathf.Clamp(current + sign * step, minimum, maximum);

        float realMaximum = maximum - minimum;
        float realCurrent = (current - minimum) / realMaximum;

        text.text = "";
        for (int i = 0; i < realCurrent * poinCount; i++)
        {
            text.text += "|";
        }

        for (int i = (int)(realCurrent * poinCount); i < poinCount; i++)
        {
            text.text += ".";
        }

        PlayerPrefs.SetFloat(key, current);
        if (key == "GlobalVolume")
        {
            EventBus.Publish(new GlobalVolumeChanged(current));
        }
        else if(key == "MusicVolume")
        {
            Events.EventBus.Publish(new MusicVolumeChanged(current));
        }
    }
}
