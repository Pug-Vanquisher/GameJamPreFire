using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MusicStarter : MonoBehaviour
{
    public int musicIndex;

    void Start()
    {
        SoundManager.Instance.PlayMusic(musicIndex);
    }

}
