using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
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

    [Header("Starter")]
    public bool firstTouch = true;
    public GameObject[] starterGrpPrefabs;
    public bool firstContact = true;
    public GameObject tutorial;
    public Transform lamasParent;

    public static GameManager Instance;


    private void Awake()
    {
        Instance = this;
        gameOverGrp.SetActive(false);

        InitAnimations();

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

        GameObject instStarterGrp = Instantiate(starterGrpPrefabs[Random.Range(0, starterGrpPrefabs.Length)], lamasParent);
        List<Transform> children = new List<Transform>();
        foreach (Transform child in instStarterGrp.transform)
            children.Add(child);

        foreach (Transform child in children)
        {
            child.SetParent(lamasParent.transform);
            TouchableSpawner.Instance.allSpawnObjects.Add(child.GetComponent<SpawnObject>());
        }
        Destroy(instStarterGrp);

        TouchableSpawner.Instance.Init();
    }

    private void InitAnimations()
    {
        void SetupAnimator(GameObject go, string stateName = "")
        {
            var anim = go.GetComponent<Animator>();
            if (anim == null) return;

            anim.updateMode = AnimatorUpdateMode.UnscaledTime;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.speed = Random.Range(0.9f, 1.1f);

            // Optional: Clip direkt starten
            if (!string.IsNullOrEmpty(stateName))
                anim.Play(stateName, 0, 0f);
        }

        SetupAnimator(pauseTitle, "MenuTitleWiggle");
        SetupAnimator(btnResume.gameObject, "ButtonIdle");
        SetupAnimator(btnReplay.gameObject, "ButtonIdle");
        SetupAnimator(btnSound.gameObject, "ButtonIdle");
        SetupAnimator(btnExit.gameObject, "ButtonIdle");

        SetupAnimator(btnRestart.gameObject, "ButtonIdle");
    }



    private void FixedUpdate()
    {
        if (firstTouch && Input.GetMouseButton(0))
        {
            firstTouch = false;
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
        SoundManager.Instance.PlayGameOverMusic();

        Time.timeScale = 0f;


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
        panelPause.SetActive(true);
        isPause = true;
        Time.timeScale = 0f;
    }

    void onBtnResume()
    {
        panelPause.SetActive(false);
        StartCoroutine(unPauseDelay());
        Time.timeScale = 1f;
    }

    private IEnumerator unPauseDelay()
    {
        yield return new WaitForSeconds(0.5f);
        isPause = false;
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
        if (SpawnObject.starterTriggerRoutine != null)
            StopCoroutine(SpawnObject.starterTriggerRoutine);

        foreach (Transform child in lamasParent.transform)
        {
            Destroy(child.gameObject);
        }

        Time.timeScale = 1f;

        if (SystemInfo.supportsGyroscope)
            Input.gyro.enabled = false;

        score = 0;
        txtScore.text = "0";

        firstTouch = true;
        firstContact = true;

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
