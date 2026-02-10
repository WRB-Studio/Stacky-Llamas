using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Score")]
    public TextMeshProUGUI txtScore;
    public float score = 0;

    [Header("Combo")]
    public TextMeshProUGUI txtComboCounter;
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
    public TextMeshProUGUI txtHighscores; // jetzt: SCORE + BEST
    public Button btnRestart;

    [Header("Others")]
    public GameObject tutorial;

    private void Awake()
    {
        Instance = this;
        gameOverGrp.SetActive(false);

        // Pause
        btnPause.onClick.AddListener(OnBtnPause);
        btnResume.onClick.AddListener(OnBtnResume);
        btnReplay.onClick.AddListener(RestartGame);
        btnExit.onClick.AddListener(OnBtnExit);
        btnSound.onClick.AddListener(OnBtnSound);

        // GameOver
        btnRestart.onClick.AddListener(RestartGame);

        if (txtHighscores) txtHighscores.text = "";
        if (txtComboCounter) txtComboCounter.gameObject.SetActive(false);
    }

    private void Start()
    {
        AddScore(0);

        SaveLoadManager.LoadSoundSetting();
        SoundManager.Instance.PlayMainMusic();
        imgSoundOff.gameObject.SetActive(!SoundManager.Instance.soundIsOn);

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

        GravitationByDeviceRotation();
        ComboHandler();
    }

    private void GravitationByDeviceRotation()
    {
        if (!SystemInfo.supportsGyroscope) return;

        Vector3 gravityDirection = Input.gyro.gravity;
        Physics2D.gravity = new Vector2(gravityDirection.x * 9.81f, gravityDirection.y * 9.81f);
    }

    private void ComboHandler()
    {
        if (comboCounter <= 0) return;

        comboTimer -= Time.deltaTime;

        if (comboTimer <= 0f)
        {
            score += comboPoints * comboCounter;
            UpdateScoreUI();

            comboCounter = 0;
            comboPoints = 0;

            if (txtComboCounter) txtComboCounter.gameObject.SetActive(false);
        }
    }

    public void AddScore(float scorePoints)
    {
        // Logik beibehalten (Combo-Verhalten wie vorher)
        if (comboCounter > 0 && comboTimer > 0)
        {
            comboCounter++;
            comboPoints += scorePoints;

            if (txtComboCounter)
            {
                txtComboCounter.gameObject.SetActive(true);
                txtComboCounter.text = "x" + comboCounter;
            }
        }

        comboTimer = maxComboTime;
        comboCounter++;

        score += scorePoints;
        UpdateScoreUI();
    }

    public void setGameOver()
    {
        MergeObjectsController.Instance.PauseAllMergeObjects(true);
        SoundManager.Instance.PlayGameOverMusic();

        btnPause.gameObject.SetActive(false);

        isGameOver = true;
        gameOverGrp.SetActive(true);

        SaveLoadManager.SaveBestScore(score);

        int current = Mathf.RoundToInt(score);
        int best = Mathf.RoundToInt(SaveLoadManager.LoadBestScore());

        if (txtHighscores)
            txtHighscores.text = $"SCORE: {current}\nBEST: {best}";
    }

    private void OnBtnPause()
    {
        if (isPause)
        {
            OnBtnResume();
            return;
        }

        panelPause.SetActive(true);
        isPause = true;
        MergeObjectsController.Instance.PauseAllMergeObjects(true);
    }

    private void OnBtnResume()
    {
        panelPause.SetActive(false);
        StartCoroutine(UnPauseDelay());
    }

    private IEnumerator UnPauseDelay()
    {
        yield return new WaitForSeconds(0.5f);
        isPause = false;
        MergeObjectsController.Instance.PauseAllMergeObjects(false);
    }

    private void OnBtnExit()
    {
        Application.Quit();
    }

    private void OnBtnSound()
    {
        SoundManager.Instance.ToggleSound();
        imgSoundOff.gameObject.SetActive(!SoundManager.Instance.soundIsOn);
        SaveLoadManager.SaveSoundSetting();
    }

    private void RestartGame()
    {
        if (MergeObject.starterTriggerRoutine != null)
            StopCoroutine(MergeObject.starterTriggerRoutine);

        foreach (Transform child in MergeObjectsController.Instance.lamasParent.transform)
            Destroy(child.gameObject);

        if (SystemInfo.supportsGyroscope)
            Input.gyro.enabled = false;

        score = 0;
        comboCounter = 0;
        comboTimer = 0;
        comboPoints = 0;
        UpdateScoreUI();

        MergeObjectsController.Instance.firstTouch = true;
        MergeObjectsController.Instance.firstContact = true;

        SaveLoadManager.LoadSoundSetting();
        SoundManager.Instance.PlayMainMusic();
        imgSoundOff.gameObject.SetActive(!SoundManager.Instance.soundIsOn);

        panelPause.SetActive(false);
        tutorial.SetActive(true);
        gameOverGrp.SetActive(false);
        btnPause.gameObject.SetActive(true);
        if (txtComboCounter) txtComboCounter.gameObject.SetActive(false);

        isPause = false;
        isGameOver = false;
    }

    private void UpdateScoreUI()
    {
        if (txtScore)
            txtScore.text = Mathf.RoundToInt(score).ToString();
    }

    // Backwards compatibility (falls irgendwo addScore() genutzt wird)
    public void addScore(float scorePoints) => AddScore(scorePoints);
}
