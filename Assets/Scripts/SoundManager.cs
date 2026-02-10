using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    public bool soundIsOn = true;

    public AudioSource audioSourceMusic;
    public AudioClip mainMusic;
    public AudioClip gameOverMusic;

    [SerializeField] private AudioSource audioSourceSounds;
    public AudioClip[] bounceSounds;
    public AudioClip spawnSound;

    private void Awake()
    {
        Instance = this;
        if (!audioSourceSounds)
            audioSourceSounds = GetComponent<AudioSource>();

        ApplySoundState();
    }

    public void ToggleSound() => SetSound(!soundIsOn);

    public void SetSound(bool isOn)
    {
        soundIsOn = isOn;
        ApplySoundState();
    }

    public void PlaySpawnSound()
    {
        if (!soundIsOn || !audioSourceSounds || !spawnSound) return;
        audioSourceSounds.PlayOneShot(spawnSound);
    }

    public void PlayBounceSound()
    {
        if (!soundIsOn || !audioSourceSounds || bounceSounds == null || bounceSounds.Length == 0) return;
        audioSourceSounds.PlayOneShot(bounceSounds[Random.Range(0, bounceSounds.Length)], 0.5f);
    }

    public void PlayMainMusic() => PlayMusic(mainMusic);
    public void PlayGameOverMusic() => PlayMusic(gameOverMusic);

    // alte Calls bleiben gültig
    public void setSoundOnOff() => ToggleSound();
    public void setSoundOnOff(bool isOn) => SetSound(isOn);

    private void PlayMusic(AudioClip clip)
    {
        if (!audioSourceMusic || !clip) return;
        audioSourceMusic.clip = clip;
        audioSourceMusic.Play();
    }

    private void ApplySoundState()
    {
        float v = soundIsOn ? 1f : 0f;
        if (audioSourceMusic) audioSourceMusic.volume = v;
        if (audioSourceSounds) audioSourceSounds.volume = v;
    }
}
