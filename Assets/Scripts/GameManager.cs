using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
        
    [Header("Audio")]
    public AudioSource mainMusic;
    public AudioSource gameOverMusic;

    [Header("Score")]
    public Text txtScore;
    public float score = 0;

    [Header("Combo")]
    public Text txtComboCounter;
    public float maxComboTime;
    private int comboCounter;
    private float comboTimer;
    private float comboPoints;

    [Header("Pause Menu")]
    public Button btnPause;
    public GameObject panelPause;
    public GameObject pauseTitle;
    public Button btnResume;
    public Button btnReplay;
    public Button btnSound;
    public Button btnExit;
    public Image imgSoundOff;
    [HideInInspector] public bool isPause = false;

    [Header("GameOver")]
    public GameObject gameOverGrp;
    [HideInInspector] public bool isGameOver = false;
    public Text txtHighscores;
    public Button btnRestart;

    [Header("Others")]
    public GameObject tutorial;



    private void Awake()
    {
        Instance = this;
        gameOverGrp.SetActive(false);

        //Pause
        btnPause.onClick.AddListener(onBtnPause);
        btnResume.onClick.AddListener(onBtnResume);
        btnReplay.onClick.AddListener(onBtnReplay);
        btnExit.onClick.AddListener(onBtnExit);
        btnSound.onClick.AddListener(onBtnSound);

        //GameOver
        btnRestart.onClick.AddListener(RestartGame);

        txtHighscores.text = "";
        txtComboCounter.gameObject.SetActive(false);
    }

    void Start()
    {
        addScore(0);
        SaveLoadManager.LoadSoundSetting();
        SoundManager.Instance.PlayMainMusic();
        imgSoundOff.gameObject.SetActive(!SoundManager.Instance.soundIsOn);
        SoundManager.Instance.setSoundOnOff(SoundManager.Instance.soundIsOn);

        panelPause.SetActive(false);
        tutorial.SetActive(true);
        gameOverGrp.SetActive(false);
        btnPause.gameObject.SetActive(true);
    }

    private void Init()
    {
        if (SystemInfo.supportsGyroscope)
            Input.gyro.enabled = true;

        MergeObjectsController.Instance.Init();
    }

    private void FixedUpdate()
    {
        if (isGameOver || isPause)
            return;

        if (MergeObjectsController.Instance.firstTouch && Input.GetMouseButton(0))
        {
            MergeObjectsController.Instance.firstTouch = false;
            Init();
            tutorial.SetActive(false);
        }

        gravitationByDeviceRotation();
        comboHandler();
    }

    private void gravitationByDeviceRotation()
    {
        if (SystemInfo.supportsGyroscope)
        {
            Vector3 gravityDirection = Input.gyro.gravity;
            Physics2D.gravity = new Vector2(gravityDirection.x * 9.81f, gravityDirection.y * 9.81f);
        }
    }


    private void comboHandler()
    {
        if (comboCounter > 0)
        {
            comboTimer -= Time.deltaTime;

            if (comboTimer <= 0f)
            {
                score += comboPoints * comboCounter;
                txtScore.text = Mathf.RoundToInt(score).ToString();
                comboCounter = 0;
                comboPoints = 0;
                txtComboCounter.gameObject.SetActive(false);
            }
        }
    }

    public void addScore(float scorePoints)
    {
        if (comboCounter > 0 && comboTimer > 0)
        {
            comboCounter++;
            comboPoints += scorePoints;
            txtComboCounter.gameObject.SetActive(true);
            txtComboCounter.text = "x" + comboCounter;
        }

        comboTimer = maxComboTime;
        comboCounter++;

        score += scorePoints;
        txtScore.text = Mathf.RoundToInt(score).ToString();
    }

    public void setGameOver()
    {
        MergeObjectsController.Instance.PauseAllMergeObjects(true);

        SoundManager.Instance.PlayGameOverMusic();

        btnPause.gameObject.SetActive(false);

        isGameOver = true;
        gameOverGrp.SetActive(true);

        SaveLoadManager.SaveHighScore(score);

        float[] highscores = SaveLoadManager.LoadHighscores();
        string highscoreText = "";

        for (int i = 0; i < highscores.Length; i++)
        {
            if (Mathf.RoundToInt(highscores[i]) > 0)
                highscoreText += Mathf.RoundToInt(highscores[i]).ToString() + "\n";
            else
                highscoreText += "000\n";
        }

        txtHighscores.text = highscoreText;
    }


    void onBtnPause()
    {
        if (isPause)
        {
            onBtnResume();
            return;
        }

        panelPause.SetActive(true);
        isPause = true;
        MergeObjectsController.Instance.PauseAllMergeObjects(true);

    }

    void onBtnResume()
    {
        panelPause.SetActive(false);
        StartCoroutine(unPauseDelay());

    }

    private IEnumerator unPauseDelay()
    {
        yield return new WaitForSeconds(0.5f);
        isPause = false;
        MergeObjectsController.Instance.PauseAllMergeObjects(false);
    }

    void onBtnReplay()
    {
        RestartGame();
    }

    void onBtnExit()
    {
        Application.Quit();
    }

    void onBtnSound()
    {
        SoundManager.Instance.setSoundOnOff();
        imgSoundOff.gameObject.SetActive(!SoundManager.Instance.soundIsOn);
        SaveLoadManager.SaveSoundSetting();
    }

    void RestartGame()
    {
        if (MergeObject.starterTriggerRoutine != null)
            StopCoroutine(MergeObject.starterTriggerRoutine);

        foreach (Transform child in MergeObjectsController.Instance.lamasParent.transform)
        {
            Destroy(child.gameObject);
        }

        if (SystemInfo.supportsGyroscope)
            Input.gyro.enabled = false;

        score = 0;
        txtScore.text = "0";

        MergeObjectsController.Instance.firstTouch = true;
        MergeObjectsController.Instance.firstContact = true;

        addScore(0);
        SaveLoadManager.LoadSoundSetting();
        SoundManager.Instance.PlayMainMusic();
        imgSoundOff.gameObject.SetActive(!SoundManager.Instance.soundIsOn);
        SoundManager.Instance.setSoundOnOff(SoundManager.Instance.soundIsOn);

        panelPause.SetActive(false);
        tutorial.SetActive(true);
        gameOverGrp.SetActive(false);
        btnPause.gameObject.SetActive(true);
        txtComboCounter.gameObject.SetActive(false);

        isPause = false;
        isGameOver = false;
    }


}
