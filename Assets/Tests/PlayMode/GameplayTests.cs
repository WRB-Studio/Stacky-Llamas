using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class GameplayTests
{
    private Vector2 authoredComboPosition;
    private Vector2 authoredComboSize;
    private Vector2 authoredComboAnchorMin;
    private Vector2 authoredComboAnchorMax;
    private Vector2 authoredComboPivot;
    private int authoredUIElementCount;
    private Transform authoredComboParent;
    private MergeFeedback authoredFeedback;
    private GameManager game;
    private MergeObjectsController controller;
    private SimulationMode2D originalSimulationMode;
    private bool hadBestScore;
    private float originalBestScore;
    private bool hadSoundSetting;
    private int originalSoundSetting;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        hadBestScore = PlayerPrefs.HasKey("BestScore");
        originalBestScore = PlayerPrefs.GetFloat("BestScore");
        hadSoundSetting = PlayerPrefs.HasKey("SoundOnOff");
        originalSoundSetting = PlayerPrefs.GetInt("SoundOnOff");
        PlayerPrefs.SetFloat("BestScore", 0);
        originalSimulationMode = Physics2D.simulationMode;
        Physics2D.simulationMode = SimulationMode2D.Script;
        SceneManager.sceneLoaded += CaptureAuthoredUI;
        yield return SceneManager.LoadSceneAsync("Ingame");
        SceneManager.sceneLoaded -= CaptureAuthoredUI;
        yield return null;
        game = GameManager.Instance;
        controller = MergeObjectsController.Instance;
        Assert.That(game, Is.Not.Null);
        Assert.That(game.enabled, Is.True, "The real scene must pass configuration validation.");
        controller.firstTouch = false;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        var oldScene = SceneManager.GetActiveScene();
        var emptyScene = SceneManager.CreateScene("TestCleanup");
        SceneManager.SetActiveScene(emptyScene);
        yield return SceneManager.UnloadSceneAsync(oldScene);
        Physics2D.simulationMode = originalSimulationMode;
        if (hadBestScore) PlayerPrefs.SetFloat("BestScore", originalBestScore);
        else PlayerPrefs.DeleteKey("BestScore");
        if (hadSoundSetting) PlayerPrefs.SetInt("SoundOnOff", originalSoundSetting);
        else PlayerPrefs.DeleteKey("SoundOnOff");
        PlayerPrefs.Save();
    }

    private void CaptureAuthoredUI(Scene scene, LoadSceneMode mode)
    {
        var manager = GameManager.Instance;
        var rect = manager.txtComboCounter.rectTransform;
        authoredComboPosition = rect.anchoredPosition;
        authoredComboSize = rect.sizeDelta;
        authoredComboAnchorMin = rect.anchorMin;
        authoredComboAnchorMax = rect.anchorMax;
        authoredComboPivot = rect.pivot;
        authoredComboParent = rect.parent;
        authoredUIElementCount = manager.txtScore.canvas.rootCanvas.GetComponentsInChildren<RectTransform>(true).Length;
        authoredFeedback = manager.txtScore.canvas.rootCanvas.GetComponentInChildren<MergeFeedback>(true);
    }

    [Test]
    public void FeedbackUsesAuthoredHierarchyAndPreservesComboLayout()
    {
        var rect = game.txtComboCounter.rectTransform;
        Assert.That(authoredFeedback, Is.Not.Null, "Feedback must exist before Start.");
        Assert.That(authoredFeedback.ValidateConfiguration(), Is.True);
        Assert.That(rect.parent, Is.EqualTo(authoredComboParent));
        Assert.That(rect.anchoredPosition, Is.EqualTo(authoredComboPosition));
        Assert.That(rect.sizeDelta, Is.EqualTo(authoredComboSize));
        Assert.That(rect.anchorMin, Is.EqualTo(authoredComboAnchorMin));
        Assert.That(rect.anchorMax, Is.EqualTo(authoredComboAnchorMax));
        Assert.That(rect.pivot, Is.EqualTo(authoredComboPivot));
        var canvas = game.txtScore.canvas.rootCanvas;
        Assert.That(canvas.GetComponentsInChildren<RectTransform>(true).Length, Is.EqualTo(authoredUIElementCount));
        game.AddScore(10, Vector2.zero);
        game.btnPause.onClick.Invoke();
        game.btnReplay.onClick.Invoke();
        Assert.That(rect.anchoredPosition, Is.EqualTo(authoredComboPosition));
        Assert.That(canvas.GetComponentsInChildren<RectTransform>(true).Length, Is.EqualTo(authoredUIElementCount));
    }

    private MergeObject Spawn(int value, Vector2 position)
    {
        var item = controller.SpawnMergeObjectByValue(value).GetComponent<MergeObject>();
        item.transform.position = position;
        return item;
    }

    private static void Call(object target, string method, params object[] arguments)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    }

    [Test]
    public void GameOverImageHasAValidSpriteAndIsVisibleWhenTheRunEnds()
    {
        game.SetGameOver();
        var image = game.gameOverGrp.transform.Find("imgGameOver").GetComponent<UnityEngine.UI.Image>();
        Assert.That(image.sprite, Is.Not.Null, "The Game Over image must reference an imported sprite.");
        Assert.That(image.isActiveAndEnabled, Is.True);
        Assert.That(image.color.a, Is.GreaterThan(0));
        Assert.That(image.rectTransform.rect.width, Is.GreaterThan(0));
        Assert.That(image.rectTransform.rect.height, Is.GreaterThan(0));
    }

    [UnityTest]
    public IEnumerator MergeFeedbackShowsBasePointsTimerAndTheAwardedBonus()
    {
        game.maxComboTime = 0.3f;
        Assert.That(Spawn(1, Vector2.zero).TryMerge(Spawn(1, Vector2.right)), Is.True);
        var root = game.txtComboCounter.transform.parent.Find("MergePoints");
        var labels = root.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
        Assert.That(labels.Length, Is.EqualTo(1));
        Assert.That(labels[0].text, Is.EqualTo("+10"));
        Assert.That(labels[0].raycastTarget, Is.False);
        Assert.That(game.txtComboCounter.gameObject.activeSelf, Is.True);
        Assert.That(Spawn(1, Vector2.zero).TryMerge(Spawn(1, Vector2.left)), Is.True);
        var bonus = game.txtComboCounter.transform.Find("PendingComboBonus").GetComponent<TMPro.TextMeshProUGUI>();
        Assert.That(bonus.text, Is.EqualTo("+20 BONUS"));
        var fill = game.txtComboCounter.transform.Find("ComboTimer/RemainingTime").GetComponent<UnityEngine.UI.Image>();
        Assert.That(fill.fillAmount, Is.EqualTo(1).Within(0.001f));
        yield return new WaitForSeconds(0.05f);
        Assert.That(fill.fillAmount, Is.InRange(0.01f, 0.99f));
        game.btnPause.onClick.Invoke();
        float pausedFill = fill.fillAmount;
        var pausedPosition = labels[0].rectTransform.anchoredPosition;
        yield return new WaitForSeconds(0.1f);
        Assert.That(fill.fillAmount, Is.EqualTo(pausedFill));
        Assert.That(labels[0].rectTransform.anchoredPosition, Is.EqualTo(pausedPosition));
        game.btnResume.onClick.Invoke();
        yield return new WaitForSeconds(0.35f);
        Assert.That(game.score, Is.EqualTo(40));
        Assert.That(game.txtComboCounter.gameObject.activeSelf, Is.False);
        Assert.That(System.Array.Exists(root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(), label => label.text == "+20 BONUS"), Is.True);
        game.btnReplay.onClick.Invoke();
        Assert.That(root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(), Is.Empty);
    }

    [Test]
    public void FeedbackIsBoundedAndLongChainsBoostTheExistingEffect()
    {
        for (int i = 0; i < 30; i++) game.AddScore(10, Vector2.zero);
        var root = game.txtComboCounter.transform.parent.Find("MergePoints");
        Assert.That(root.childCount, Is.EqualTo(12));
        Assert.That(root.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Length, Is.EqualTo(12));
        controller.SpawnMergeEffect(Vector2.zero, 1, 1);
        controller.SpawnMergeEffect(Vector2.zero, 1, 30);
        var normal = controller.mergeEffectParent.GetChild(0);
        var boosted = controller.mergeEffectParent.GetChild(1);
        Assert.That(boosted.localScale.x, Is.EqualTo(normal.localScale.x * 1.5f).Within(0.001f));
        Assert.That(boosted.GetComponent<ParticleSystem>().main.startSpeedMultiplier,
            Is.GreaterThan(normal.GetComponent<ParticleSystem>().main.startSpeedMultiplier));
        game.SetGameOver();
        Assert.That(root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(), Is.Empty);
    }

    [Test]
    public void ComboMultiplierMatchesDisplayedValueAndFinalScore()
    {
        Assert.That(game.txtComboCounter.gameObject.activeSelf, Is.False);
        game.AddScore(10);
        game.AddScore(20);
        game.AddScore(30);
        Assert.That(game.txtComboCounter.text, Is.EqualTo("x3"));
        Assert.That(game.score, Is.EqualTo(60));
        game.SetGameOver();
        Assert.That(game.score, Is.EqualTo(180));
        Assert.That(SaveLoadManager.LoadBestScore(), Is.EqualTo(180));
        game.SetGameOver();
        game.AddScore(100);
        Assert.That(game.score, Is.EqualTo(180));
    }

    [UnityTest]
    public IEnumerator ExpiredCombosAreNotCombinedWithTheNextMerge()
    {
        game.maxComboTime = 0.02f;
        game.AddScore(10);
        game.AddScore(10);
        yield return new WaitForSeconds(0.05f);
        Assert.That(game.score, Is.EqualTo(40));
        game.AddScore(10);
        game.SetGameOver();
        Assert.That(game.score, Is.EqualTo(50));
    }

    [UnityTest]
    public IEnumerator PauseFreezesTheComboWindow()
    {
        game.maxComboTime = 0.05f;
        game.AddScore(10);
        game.AddScore(10);
        game.btnPause.onClick.Invoke();
        yield return new WaitForSeconds(0.1f);
        Assert.That(game.score, Is.EqualTo(20));
        Assert.That(game.txtComboCounter.text, Is.EqualTo("x2"));
        game.btnResume.onClick.Invoke();
        yield return new WaitForSeconds(0.1f);
        Assert.That(game.score, Is.EqualTo(40));
    }

    [Test]
    public void ZeroComboWindowAwardsOnlyBasePoints()
    {
        game.maxComboTime = 0;
        game.AddScore(10);
        game.AddScore(10);
        game.SetGameOver();
        Assert.That(game.score, Is.EqualTo(20));
    }

    [Test]
    public void InvalidSpawnRangeIsRejectedBeforePlaying()
    {
        controller.spawnableIndexRange = controller.mergeObjects.Length + 1;
        LogAssert.Expect(LogType.Error, "Invalid spawn configuration: check camera, EventSystem, references, prefab components, values and spawn range.");
        Assert.That(controller.ValidateConfiguration(), Is.False);
    }

    [Test]
    public void StarterGravityIsReleasedOnceForTheWholeGroup()
    {
        controller.firstTouch = true;
        controller.Init();
        Assert.That(controller.instantiatedMergeObjects.Count, Is.GreaterThan(0));
        Assert.That(controller.instantiatedMergeObjects.TrueForAll(item => item.Body.gravityScale == 0), Is.True);
        controller.ReleaseStarterObjects();
        Assert.That(controller.firstContact, Is.False);
        Assert.That(controller.instantiatedMergeObjects.TrueForAll(item => item.Body.gravityScale == 1), Is.True);
    }

    [Test]
    public void PausePreservesPreviewAndFallingBodyStates()
    {
        var falling = Spawn(1, Vector2.zero);
        var preview = Spawn(2, Vector2.up * 4);
        falling.Body.linearVelocity = new Vector2(2, -3);
        falling.Body.angularVelocity = 45;
        preview.Body.simulated = false;
        controller.PauseAllMergeObjects(true);
        controller.PauseAllMergeObjects(true);
        Assert.That(falling.Body.simulated, Is.False);
        controller.PauseAllMergeObjects(false);
        Assert.That(falling.Body.simulated, Is.True);
        Assert.That(falling.Body.linearVelocity, Is.EqualTo(new Vector2(2, -3)));
        Assert.That(falling.Body.angularVelocity, Is.EqualTo(45));
        Assert.That(preview.Body.simulated, Is.False);
    }

    [UnityTest]
    public IEnumerator SpawnTimerFreezesDuringPauseAndIsCancelledByRestart()
    {
        controller.firstTouch = true;
        controller.Init();
        int starterCount = controller.instantiatedMergeObjects.Count;
        game.btnPause.onClick.Invoke();
        yield return new WaitForSeconds(controller.spawnDelay + 0.1f);
        Assert.That(controller.instantiatedMergeObjects.Count, Is.EqualTo(starterCount));
        game.btnReplay.onClick.Invoke();
        yield return new WaitForSeconds(controller.spawnDelay + 0.1f);
        Assert.That(controller.instantiatedMergeObjects, Is.Empty);
        Assert.That(controller.firstTouch, Is.True);
        Assert.That(controller.firstContact, Is.True);
        Assert.That(game.score, Is.Zero);
    }

    [UnityTest]
    public IEnumerator PendingDropDoesNotActivateDuringPauseOrAfterRestart()
    {
        var item = Spawn(1, Vector2.zero);
        item.Body.simulated = false;
        var field = typeof(MergeObjectsController).GetField("pendingActivations", BindingFlags.Instance | BindingFlags.NonPublic);
        var pending = (System.Collections.Generic.Dictionary<MergeObject, float>)field.GetValue(controller);
        pending.Add(item, 0.05f);
        game.btnPause.onClick.Invoke();
        yield return new WaitForSeconds(0.1f);
        Assert.That(item.Body.simulated, Is.False);
        game.btnResume.onClick.Invoke();
        yield return new WaitForSeconds(0.1f);
        Assert.That(item.Body.simulated, Is.True);
        item.Body.simulated = false;
        pending.Add(item, 0.05f);
        game.btnReplay.onClick.Invoke();
        yield return new WaitForSeconds(0.1f);
        Assert.That(controller.instantiatedMergeObjects, Is.Empty);
    }

    [Test]
    public void AnObjectCannotBeConsumedByTwoMerges()
    {
        var a = Spawn(1, Vector2.zero);
        var b = Spawn(1, Vector2.right);
        var c = Spawn(1, Vector2.left);
        Assert.That(a.TryMerge(b), Is.True);
        Assert.That(c.TryMerge(b), Is.False);
        Assert.That(controller.instantiatedMergeObjects.Count, Is.EqualTo(2));
        Assert.That(controller.instantiatedMergeObjects.Exists(item => item.value == 2), Is.True);
        Assert.That(game.score, Is.EqualTo(10));
    }

    [Test]
    public void ThreeSimultaneousContactsProduceOnlyOneSuccessor()
    {
        Spawn(1, Vector2.zero);
        Spawn(1, new Vector2(0.1f, 0));
        Spawn(1, new Vector2(-0.1f, 0));
        Physics2D.Simulate(0.02f);
        Assert.That(controller.instantiatedMergeObjects.Count, Is.EqualTo(2));
        Assert.That(controller.instantiatedMergeObjects.FindAll(item => item.value == 2).Count, Is.EqualTo(1));
        Assert.That(game.score, Is.EqualTo(10));
    }

    [Test]
    public void HighestLevelPairIsConsumedAndScored()
    {
        var a = Spawn(16, Vector2.zero);
        var b = Spawn(16, Vector2.right);
        Assert.That(a.TryMerge(b), Is.True);
        Assert.That(controller.instantiatedMergeObjects, Is.Empty);
        Assert.That(game.score, Is.EqualTo(160));
    }

    [UnityTest]
    public IEnumerator DestroyedObjectsAreRemovedFromTheRegistry()
    {
        var item = Spawn(1, Vector2.zero);
        Object.Destroy(item.gameObject);
        yield return null;
        Assert.That(controller.instantiatedMergeObjects, Is.Empty);
    }

    [Test]
    public void ApplicationSuspensionRequiresExplicitResume()
    {
        var item = Spawn(1, Vector2.zero);
        game.AddScore(10);
        game.AddScore(10);
        Call(game, "OnApplicationPause", true);
        Assert.That(game.isPause, Is.True);
        Assert.That(item.Body.simulated, Is.False);
        Assert.That(SaveLoadManager.LoadBestScore(), Is.EqualTo(40));
        game.btnResume.onClick.Invoke();
        Assert.That(game.isPause, Is.True);
        Call(game, "OnApplicationFocus", false);
        Call(game, "OnApplicationPause", false);
        game.btnResume.onClick.Invoke();
        Assert.That(game.isPause, Is.True, "Focus loss must still block resume.");
        Call(game, "OnApplicationFocus", true);
        Assert.That(game.isPause, Is.True);
        game.btnResume.onClick.Invoke();
        Assert.That(game.IsPlaying, Is.True);
        Assert.That(item.Body.simulated, Is.True);
    }

    [Test]
    public void PauseRejectsMergesAndScoreChanges()
    {
        var a = Spawn(1, Vector2.zero);
        var b = Spawn(1, Vector2.right);
        game.btnPause.onClick.Invoke();
        Assert.That(a.TryMerge(b), Is.False);
        game.AddScore(10);
        Assert.That(game.score, Is.Zero);
        Assert.That(controller.instantiatedMergeObjects.Count, Is.EqualTo(2));
    }

    [Test]
    public void SoundSettingAndBestScoreKeepExistingStorageKeys()
    {
        SaveLoadManager.SaveSoundSetting(false);
        Assert.That(SaveLoadManager.LoadSoundSetting(), Is.False);
        SaveLoadManager.SaveSoundSetting(true);
        Assert.That(SaveLoadManager.LoadSoundSetting(), Is.True);
        SaveLoadManager.SaveBestScore(100);
        SaveLoadManager.SaveBestScore(50);
        SaveLoadManager.SaveBestScore(float.NaN);
        Assert.That(SaveLoadManager.LoadBestScore(), Is.EqualTo(100));
    }

    [Test]
    public void SafeAreaKeepsTheExistingUIInsideOneContainer()
    {
        var canvas = game.txtScore.canvas.rootCanvas;
        Assert.That(canvas.transform.childCount, Is.EqualTo(1));
        var safeArea = canvas.GetComponentInChildren<SafeArea>();
        Assert.That(safeArea, Is.Not.Null);
        Assert.That(game.btnPause.transform.IsChildOf(safeArea.transform), Is.True);
        Assert.That(canvas.transform.childCount, Is.EqualTo(1));

    }
    [UnityTest]
    public IEnumerator StaticHUDPreservesEditedContainerAndTimerLayoutDuringPlay()
    {
        var container = game.txtScore.canvas.rootCanvas.GetComponentInChildren<SafeArea>().GetComponent<RectTransform>();
        container.anchorMin = new Vector2(0.05f, 0.03f);
        container.anchorMax = new Vector2(0.95f, 0.98f);
        container.offsetMin = new Vector2(17, 13);
        container.offsetMax = new Vector2(-19, -11);
        var minimumOffset = container.offsetMin;
        var maximumOffset = container.offsetMax;
        var combo = game.txtComboCounter.rectTransform;
        var comboScale = combo.localScale;
        var fill = combo.Find("ComboTimer/RemainingTime").GetComponent<UnityEngine.UI.Image>();
        var fillRect = fill.rectTransform;
        var fillMin = fillRect.anchorMin;
        var fillMax = fillRect.anchorMax;
        var fillPosition = fillRect.anchoredPosition;
        var fillSize = fillRect.sizeDelta;
        Assert.That(fill.type, Is.EqualTo(UnityEngine.UI.Image.Type.Filled));
        Assert.That(fill.sprite, Is.Not.Null, "fillAmount needs a sprite to render the timer.");
        game.maxComboTime = 0.5f;
        game.AddScore(10, Vector2.zero);
        game.AddScore(10, Vector2.zero);
        yield return new WaitForSeconds(0.05f);
        Assert.That(container.anchorMin, Is.EqualTo(new Vector2(0.05f, 0.03f)));
        Assert.That(container.anchorMax, Is.EqualTo(new Vector2(0.95f, 0.98f)));
        Assert.That(container.offsetMin.x, Is.EqualTo(minimumOffset.x).Within(0.001f));
        Assert.That(container.offsetMin.y, Is.EqualTo(minimumOffset.y).Within(0.001f));
        Assert.That(container.offsetMax.x, Is.EqualTo(maximumOffset.x).Within(0.001f));
        Assert.That(container.offsetMax.y, Is.EqualTo(maximumOffset.y).Within(0.001f));
        Assert.That(combo.localScale, Is.EqualTo(comboScale));
        Assert.That(fillRect.anchorMin, Is.EqualTo(fillMin));
        Assert.That(fillRect.anchorMax, Is.EqualTo(fillMax));
        Assert.That(fillRect.anchoredPosition, Is.EqualTo(fillPosition));
        Assert.That(fillRect.sizeDelta, Is.EqualTo(fillSize));
        Assert.That(fill.fillAmount, Is.InRange(0.01f, 0.99f));
    }

    [Test]
    public void FreshDropsOnlyMergeOnceBothHaveFallenBelowTheirSpawnHeight()
    {
        Vector2 spawn = controller.transform.position;
        var a = Spawn(1, spawn);
        var b = Spawn(1, spawn + Vector2.right * 0.1f);
        a.PrepareDrop(spawn.y);
        b.PrepareDrop(spawn.y);
        Assert.That(a.TryMerge(b), Is.False);
        a.Body.position += Vector2.down * 3;
        Assert.That(a.TryMerge(b), Is.False, "Both drops must clear the spawn height.");
        Assert.That(b.TryMerge(a), Is.False);
        Assert.That(game.score, Is.Zero);
        b.Body.position += Vector2.down * 3;
        Assert.That(a.TryMerge(b), Is.True);
        Assert.That(game.score, Is.EqualTo(10));
    }

    [Test]
    public void FastDropsKeepTheOriginalPreviewSpawnDelay()
    {
        controller.firstTouch = true;
        controller.Init();
        var dropped = Spawn(1, controller.transform.position);
        dropped.PrepareDrop(controller.transform.position.y);
        dropped.Body.simulated = false;
        var field = typeof(MergeObjectsController).GetField("pendingActivations", BindingFlags.Instance | BindingFlags.NonPublic);
        var pending = (System.Collections.Generic.Dictionary<MergeObject, float>)field.GetValue(controller);
        pending.Add(dropped, 0.25f);
        int count = controller.instantiatedMergeObjects.Count;
        Call(controller, "UpdateDelayedObjects", controller.spawnDelay + 0.01f);
        Assert.That(controller.instantiatedMergeObjects.Count, Is.EqualTo(count + 1), "The next preview must appear even while the previous drop is still at spawn.");
        Assert.That(dropped.Body.simulated, Is.False);
        Assert.That(game.score, Is.Zero);
    }

    [Test]
    public void TouchingDropsCanMergeAfterFallingWithoutASecondImpact()
    {
        var a = Spawn(1, Vector2.zero);
        var b = Spawn(1, Vector2.right * 0.1f);
        a.PrepareDrop(0);
        b.PrepareDrop(0);
        Physics2D.Simulate(0.02f);
        Assert.That(game.score, Is.Zero);
        for (int i = 0; i < 80 && game.score == 0; i++) Physics2D.Simulate(0.02f);
        Assert.That(game.score, Is.EqualTo(10), "Existing contacts must retry merges after leaving the spawn height.");
        Assert.That(controller.instantiatedMergeObjects.Count, Is.EqualTo(1));
    }}
