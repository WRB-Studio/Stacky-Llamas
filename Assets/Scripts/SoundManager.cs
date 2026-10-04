using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }
    public bool soundIsOn = true;
    public AudioSource audioSourceMusic;
    public AudioClip mainMusic;
    public AudioClip gameOverMusic;
    [SerializeField] private AudioSource audioSourceSounds;
    public AudioClip[] bounceSounds;
    public AudioClip spawnSound;
    private bool applicationSuspended;

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Debug.LogError("Only one SoundManager is allowed in a scene.", this);
            enabled = false;
            return;
        }
        Instance = this;
        if (!audioSourceSounds) audioSourceSounds = GetComponent<AudioSource>();
        ApplySoundState();
    }

    public bool ValidateConfiguration()
    {
        bool valid = audioSourceMusic && audioSourceSounds && audioSourceMusic != audioSourceSounds
            && mainMusic && gameOverMusic && spawnSound && bounceSounds != null && bounceSounds.Length > 0;
        if (bounceSounds != null)
            foreach (var clip in bounceSounds) valid &= clip;
        if (!valid) Debug.LogError("Assign separate music/SFX AudioSources and all sound clips.", this);
        return valid;
    }

    public void ToggleSound() => SetSound(!soundIsOn);

    public void SetSound(bool isOn)
    {
        soundIsOn = isOn;
        ApplySoundState();
    }

    public void PlaySpawnSound()
    {
        if (soundIsOn && !applicationSuspended && audioSourceSounds && spawnSound)
            audioSourceSounds.PlayOneShot(spawnSound);
    }

    public void PlayBounceSound()
    {
        if (!soundIsOn || applicationSuspended || !audioSourceSounds || bounceSounds == null || bounceSounds.Length == 0) return;
        var clip = bounceSounds[Random.Range(0, bounceSounds.Length)];
        if (clip) audioSourceSounds.PlayOneShot(clip, 0.5f);
    }

    public void PlayMainMusic() => PlayMusic(mainMusic);
    public void PlayGameOverMusic() => PlayMusic(gameOverMusic);

    private void PlayMusic(AudioClip clip)
    {
        if (!audioSourceMusic || !clip) return;
        audioSourceMusic.clip = clip;
        audioSourceMusic.Play();
        if (applicationSuspended) audioSourceMusic.Pause();
    }

    public void SetApplicationSuspended(bool suspended)
    {
        if (applicationSuspended == suspended) return;
        applicationSuspended = suspended;
        if (suspended)
        {
            if (audioSourceMusic) audioSourceMusic.Pause();
            if (audioSourceSounds) audioSourceSounds.Pause();
        }
        else
        {
            if (audioSourceMusic) audioSourceMusic.UnPause();
            if (audioSourceSounds) audioSourceSounds.UnPause();
        }
    }

    private void ApplySoundState()
    {
        float volume = soundIsOn ? 1 : 0;
        if (audioSourceMusic) audioSourceMusic.volume = volume;
        if (audioSourceSounds) audioSourceSounds.volume = volume;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
