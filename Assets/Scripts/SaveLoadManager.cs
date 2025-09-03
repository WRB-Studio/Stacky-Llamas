using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class SaveLoadManager : MonoBehaviour
{
    private const int maxScores = 3;


    public static void SaveHighScore(float currentScore)
    {
        for (int i = 0; i < maxScores; i++)
        {
            float savedScore = PlayerPrefs.GetFloat("HighScore" + i, 0);

            if (currentScore > savedScore)
            {
                // Schiebe die niedrigeren Scores nach unten
                for (int j = maxScores - 1; j > i; j--)
                {
                    PlayerPrefs.SetFloat("HighScore" + j, PlayerPrefs.GetFloat("HighScore" + (j - 1), 0));
                }

                PlayerPrefs.SetFloat("HighScore" + i, currentScore);
                break;
            }
        }

        PlayerPrefs.Save();
    }

    public static float[] LoadHighscores()
    {
        float[] highscores = new float[maxScores];

        for (int i = 0; i < maxScores; i++)
        {
            highscores[i] = PlayerPrefs.GetFloat("HighScore" + i, 0);
        }

        return highscores;
    }


    public static void SaveSoundSetting()
    {
        PlayerPrefs.SetInt("SoundOnOff", SoundManager.Instance.soundIsOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    // Laden des Sound-Zustands (ein oder aus)
    public static void LoadSoundSetting()
    {
        if(PlayerPrefs.GetInt("SoundOnOff", 1) == 0)
        {
            SoundManager.Instance.setSoundOnOff(false);
        }
        else
        {
            SoundManager.Instance.setSoundOnOff(true);
        }
    }
}

