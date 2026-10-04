using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Score")]
    public TextMeshProUGUI txtScore;
    public float score;

    [Header("Combo")]
    public TextMeshProUGUI txtComboCounter;
    [Min(0)] public float maxComboTime;
    private int comboCounter;
    private float comboTimer;
    private float comboPoints;
    [SerializeField] private MergeFeedback mergeFeedback;
    private Vector2? lastMergePosition;
    public int ComboCount => comboCounter;
    public float ComboTimeRemaining => Mathf.Max(0, comboTimer);
    public float PendingComboBonus => comboPoints * Mathf.Max(0, comboCounter - 1);

    [Header("Pause Menu")]
    public Button btnPause;
    public GameObject panelPause;
    public GameObject pauseTitle;
    public Button btnResume;
    public Button btnReplay;
    public Button btnSound;
    public Button btnExit;
    public Image imgSoundOff;
    public bool isPause { get; private set; }

    [Header("GameOver")]
    public GameObject gameOverGrp;
    public TextMeshProUGUI txtHighscores;
    public Button btnRestart;
    public bool isGameOver { get; private set; }

    [Header("Others")]
    public GameObject tutorial;

    private bool initialized;
    private bool applicationPaused;
    private bool applicationFocused = true;
    private bool applicationSuspended;
    private Vector2 defaultGravity;
    public bool IsPlaying => initialized && !isPause && !isGameOver
        && MergeObjectsController.Instance && !MergeObjectsController.Instance.firstTouch;

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Debug.LogError("Only one GameManager is allowed in a scene.", this);
            enabled = false;
            return;
        }
        Instance = this;
        defaultGravity = Physics2D.gravity;
    }

    private void Start()
    {
        bool valid = txtScore && txtComboCounter && btnPause && panelPause && btnResume
            && btnReplay && btnSound && btnExit && imgSoundOff && gameOverGrp
            && txtHighscores && btnRestart && tutorial && maxComboTime >= 0
            && MergeObjectsController.Instance && SoundManager.Instance && mergeFeedback;
        if (!valid || !MergeObjectsController.Instance.ValidateConfiguration()
            || !SoundManager.Instance.ValidateConfiguration() || !mergeFeedback.ValidateConfiguration())
        {
            Debug.LogError("Game setup is incomplete. Assign the required manager and UI references.", this);
            enabled = false;
            return;
        }

        btnPause.onClick.AddListener(OnBtnPause);
        btnResume.onClick.AddListener(OnBtnResume);
        btnReplay.onClick.AddListener(RestartGame);
        btnExit.onClick.AddListener(OnBtnExit);
        btnSound.onClick.AddListener(OnBtnSound);
        btnRestart.onClick.AddListener(RestartGame);
        mergeFeedback.Init(this);
        initialized = true;
        ResetSessionUI();
        UpdateApplicationSuspension();
    }

    private void Update()
    {
        if (!initialized || isGameOver) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPause) OnBtnResume();
            else OnBtnPause();
            return;
        }
        if (isPause) return;
        if (MergeObjectsController.Instance.firstTouch && Input.GetMouseButtonDown(0)
            && !MergeObjectsController.Instance.IsPointerOverGUIElements())
        {
            if (SystemInfo.supportsGyroscope) Input.gyro.enabled = true;
            MergeObjectsController.Instance.Init();
            tutorial.SetActive(false);
        }
        if (!IsPlaying || comboCounter == 0) return;
        comboTimer -= Time.deltaTime;
        if (comboTimer <= 0) FinishCombo();
    }

    private void FixedUpdate()
    {
        if (!IsPlaying || !SystemInfo.supportsGyroscope || !Input.gyro.enabled) return;
        Vector3 gravity = Input.gyro.gravity;
        // Ignore the first, empty sensor sample; keep normal gravity until data arrives.
        if (gravity.sqrMagnitude > 0.001f)
            Physics2D.gravity = new Vector2(gravity.x, gravity.y) * 9.81f;
    }

    public void AddScore(float points, Vector2? mergePosition = null)
    {
        if (!IsPlaying || points <= 0 || float.IsNaN(points) || float.IsInfinity(points)) return;
        if (comboCounter > 0 && comboTimer <= 0) FinishCombo();
        score += points;
        comboPoints += points;
        comboCounter++;
        comboTimer = maxComboTime;
        lastMergePosition = mergePosition;
        txtComboCounter.gameObject.SetActive(maxComboTime > 0);
        txtComboCounter.text = "x" + comboCounter;
        mergeFeedback.RefreshCombo();
        if (mergePosition.HasValue) mergeFeedback.ShowPoints(mergePosition.Value, points, comboCounter);
        UpdateScoreUI();
        if (maxComboTime == 0) FinishCombo();
    }

    private void FinishCombo()
    {
        // Base points are awarded immediately; this makes the full chain worth xN.
        float bonus = PendingComboBonus;
        score += bonus;
        if (bonus > 0 && lastMergePosition.HasValue && !isGameOver)
            mergeFeedback.ShowPoints(lastMergePosition.Value, bonus, comboCounter, true);
        comboCounter = 0;
        comboTimer = 0;
        comboPoints = 0;
        lastMergePosition = null;
        txtComboCounter.gameObject.SetActive(false);
        mergeFeedback.RefreshCombo();
        UpdateScoreUI();
    }

    public void SetGameOver()
    {
        if (!IsPlaying) return;
        isGameOver = true;
        FinishCombo();
        var controller = MergeObjectsController.Instance;
        controller.StopSpawning();
        controller.PauseAllMergeObjects(true);
        mergeFeedback.Clear();
        if (SystemInfo.supportsGyroscope) Input.gyro.enabled = false;
        SoundManager.Instance.PlayGameOverMusic();
        btnPause.gameObject.SetActive(false);
        panelPause.SetActive(false);
        gameOverGrp.SetActive(true);
        SaveLoadManager.SaveBestScore(score);
        txtHighscores.text = $"SCORE: {Mathf.RoundToInt(score)}\nBEST: {Mathf.RoundToInt(SaveLoadManager.LoadBestScore())}";
    }

    private void OnBtnPause()
    {
        if (!initialized || isGameOver || isPause) return;
        isPause = true;
        mergeFeedback.RefreshCombo();
        panelPause.SetActive(true);
        MergeObjectsController.Instance.PauseAllMergeObjects(true);
        if (SystemInfo.supportsGyroscope) Input.gyro.enabled = false;
    }

    private void OnBtnResume()
    {
        if (!initialized || !isPause || isGameOver || applicationSuspended) return;
        panelPause.SetActive(false);
        MergeObjectsController.Instance.PauseAllMergeObjects(false);
        isPause = false;
        if (SystemInfo.supportsGyroscope && !MergeObjectsController.Instance.firstTouch)
            Input.gyro.enabled = true;
    }

    private void OnBtnExit()
    {
        FinishCombo();
        SaveLoadManager.SaveBestScore(score);
        Application.Quit();
    }

    private void OnBtnSound()
    {
        SoundManager.Instance.ToggleSound();
        imgSoundOff.gameObject.SetActive(!SoundManager.Instance.soundIsOn);
        SaveLoadManager.SaveSoundSetting(SoundManager.Instance.soundIsOn);
    }

    private void RestartGame()
    {
        if (!initialized) return;
        FinishCombo();
        SaveLoadManager.SaveBestScore(score);
        MergeObjectsController.Instance.ResetGame();
        if (SystemInfo.supportsGyroscope) Input.gyro.enabled = false;
        Physics2D.gravity = defaultGravity;
        isPause = false;
        isGameOver = false;
        ResetSessionUI();
        if (applicationSuspended) OnBtnPause();
    }

    private void ResetSessionUI()
    {
        score = 0;
        comboCounter = 0;
        comboTimer = 0;
        comboPoints = 0;
        lastMergePosition = null;
        mergeFeedback.Clear();
        UpdateScoreUI();
        txtComboCounter.gameObject.SetActive(false);
        txtHighscores.text = "";
        SoundManager.Instance.SetSound(SaveLoadManager.LoadSoundSetting());
        SoundManager.Instance.PlayMainMusic();
        imgSoundOff.gameObject.SetActive(!SoundManager.Instance.soundIsOn);
        panelPause.SetActive(false);
        tutorial.SetActive(true);
        gameOverGrp.SetActive(false);
        btnPause.gameObject.SetActive(true);
    }

    private void UpdateScoreUI() => txtScore.text = Mathf.RoundToInt(score).ToString();

    private void OnApplicationPause(bool paused)
    {
        applicationPaused = paused;
        UpdateApplicationSuspension();
    }

    private void OnApplicationFocus(bool focused)
    {
        applicationFocused = focused;
        UpdateApplicationSuspension();
    }

    private void UpdateApplicationSuspension()
    {
        if (!initialized) return;
        applicationSuspended = applicationPaused || !applicationFocused;
        SoundManager.Instance.SetApplicationSuspended(applicationSuspended);
        if (applicationSuspended)
        {
            OnBtnPause();
            // Preserve earned bonuses without ending the active combo when leaving the app.
            SaveLoadManager.SaveBestScore(score + comboPoints * Mathf.Max(0, comboCounter - 1));
        }
    }

    private void OnApplicationQuit()
    {
        if (!initialized) return;
        FinishCombo();
        SaveLoadManager.SaveBestScore(score);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        if (initialized)
        {
            if (btnPause) btnPause.onClick.RemoveListener(OnBtnPause);
            if (btnResume) btnResume.onClick.RemoveListener(OnBtnResume);
            if (btnReplay) btnReplay.onClick.RemoveListener(RestartGame);
            if (btnExit) btnExit.onClick.RemoveListener(OnBtnExit);
            if (btnSound) btnSound.onClick.RemoveListener(OnBtnSound);
            if (btnRestart) btnRestart.onClick.RemoveListener(RestartGame);
        }
        if (SystemInfo.supportsGyroscope) Input.gyro.enabled = false;
        Physics2D.gravity = defaultGravity;
        Instance = null;
    }
}
