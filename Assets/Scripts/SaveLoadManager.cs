using UnityEngine;

public static class SaveLoadManager
{
    private const string KeyBestScore = "BestScore";
    private const string KeySoundOnOff = "SoundOnOff";

    public static void SaveBestScore(float currentScore)
    {
        if (float.IsNaN(currentScore) || float.IsInfinity(currentScore) || currentScore <= LoadBestScore()) return;
        PlayerPrefs.SetFloat(KeyBestScore, currentScore);
        PlayerPrefs.Save();
    }

    public static float LoadBestScore()
    {
        float best = PlayerPrefs.GetFloat(KeyBestScore, 0);
        return float.IsNaN(best) || float.IsInfinity(best) ? 0 : Mathf.Max(0, best);
    }

    public static void SaveSoundSetting(bool isOn)
    {
        PlayerPrefs.SetInt(KeySoundOnOff, isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static bool LoadSoundSetting() => PlayerPrefs.GetInt(KeySoundOnOff, 1) == 1;
}
