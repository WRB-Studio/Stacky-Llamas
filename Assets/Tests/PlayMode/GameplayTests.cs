using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class GameplayTests
{
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
        yield return SceneManager.LoadSceneAsync("Ingame");
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
        SafeArea.CreateForCanvas(canvas);
        Assert.That(canvas.transform.childCount, Is.EqualTo(1));
        var rect = safeArea.GetComponent<RectTransform>();
        if (Screen.width > 0 && Screen.height > 0)
        {
            Assert.That(rect.anchorMin.x, Is.EqualTo(Screen.safeArea.xMin / Screen.width).Within(0.001f));
            Assert.That(rect.anchorMax.y, Is.EqualTo(Screen.safeArea.yMax / Screen.height).Within(0.001f));
        }
    }
}
