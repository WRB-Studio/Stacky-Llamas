using UnityEngine;

public class SaveLoadManager
{
    private const string KeyBestScore = "BestScore";
    private const string KeySoundOnOff = "SoundOnOff";

    // Speichert nur Best-Score
    public static void SaveBestScore(float currentScore)
    {
        float best = LoadBestScore();
        if (currentScore > best)
        {
            PlayerPrefs.SetFloat(KeyBestScore, currentScore);
            PlayerPrefs.Save();
        }
    }

    public static float LoadBestScore()
    {
        return PlayerPrefs.GetFloat(KeyBestScore, 0f);
    }

    public static void SaveSoundSetting()
    {
        PlayerPrefs.SetInt(KeySoundOnOff, SoundManager.Instance != null && SoundManager.Instance.soundIsOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void LoadSoundSetting()
    {
        bool isOn = PlayerPrefs.GetInt(KeySoundOnOff, 1) == 1;
        if (SoundManager.Instance != null)
            SoundManager.Instance.SetSound(isOn);
    }
}
