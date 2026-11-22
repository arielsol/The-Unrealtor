using System;
using System.Collections;
using System.Collections.Generic;
using UnityCommunity.UnitySingleton;
using UnityEngine;

public class AudioManager : MonoSingleton<AudioManager>
{
    [SerializeField] public AudioSource Audio1;
    [SerializeField] public AudioSource Audio2;
    [SerializeField] private AudioClip startAudio1;
    [SerializeField] private AudioClip startAudio2;
    [Range(0f, 1f)] [SerializeField] private float volume;

    [SerializeField] private AudioClip matchSFX;
    [SerializeField] private AudioClip levelCompleteSFX;
    [SerializeField] private AudioClip errorSFX;
    [SerializeField] private AudioClip bgm;

    private void Start()
    {
        Audio1.volume = volume;
        /*Audio1.volume = 0;

        Audio1.PlayOneShot(startAudio1);
        Audio1.PlayOneShot(startAudio2);

	    Audio1.PlayScheduled(AudioSettings.dspTime + startAudio1.length);
        Audio1.PlayScheduled(AudioSettings.dspTime + startAudio2.length);*/
    }

    public void PlayMatchAudio()
    {
        Audio1.clip = matchSFX;
        Audio1.Play();
    }
    public void PlayLevelCompleteAudio()
    {
        Audio1.clip = levelCompleteSFX;
        Audio1.Play();
    }

    public void PlayMatchErrorAudio()
    {
        Audio1.clip = errorSFX;
        Audio1.Play();
    }

}
