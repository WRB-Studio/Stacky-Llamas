using System.Collections;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public bool soundIsOn = true;

    public AudioSource audioSourceMusic;
    public AudioClip mainMusic;
    public AudioClip gameOverMusic;

    private AudioSource audioSourceSounds;
    public AudioClip[] bounceSounds;
    public AudioClip spawnSound;


    public static SoundManager Instance;



    private void Awake()
    {
        Instance = this;

        audioSourceSounds = GetComponent<AudioSource>();
    }

    public void PlaySpawnSound()
    {
        if (!soundIsOn)
            return;

        audioSourceSounds.PlayOneShot(spawnSound);
    }

    public void PlayBounceSound()
    {
        if (!soundIsOn)
            return;

        audioSourceSounds.PlayOneShot(bounceSounds[Random.Range(0, bounceSounds.Length)], 0.5f);
    }

    public void PlayMainMusic()
    {
        audioSourceMusic.clip = mainMusic;
        audioSourceMusic.Play();
    }

    public void PlayGameOverMusic()
    {
        audioSourceMusic.clip = gameOverMusic;
        audioSourceMusic.Play();
    }

    public void setSoundOnOff()
    {
        soundIsOn = !soundIsOn;

        if (soundIsOn)
            audioSourceMusic.volume = 1;
        else
            audioSourceMusic.volume = 0;
    }

    public void setSoundOnOff(bool isOn)
    {
        soundIsOn = isOn;

        if(soundIsOn)
            audioSourceMusic.volume = 1;
        else
            audioSourceMusic.volume = 0;
    }

}
