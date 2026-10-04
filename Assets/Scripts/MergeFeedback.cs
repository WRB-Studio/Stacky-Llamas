using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MergeFeedback : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private RectTransform popupRoot;
    [SerializeField] private TextMeshProUGUI[] popups;
    [SerializeField] private Image timerColor;
    [SerializeField] private TextMeshProUGUI bonusLabel;

    [Header("Point animation")]
    [SerializeField, Min(0.01f)] private float popupLifetime = 0.85f;
    [SerializeField] private Vector2 popupOffset = new Vector2(0, 50);
    [SerializeField, Min(0)] private float popupRise = 80;
    [SerializeField, Min(1)] private float popupStartScale = 1.15f;
    [SerializeField, Min(1)] private float chainFontScale = 1.38f;
    [SerializeField, Min(1)] private float bonusFontScale = 1.14f;
    [SerializeField] private Color chainColor = new Color(1, 0.6f, 0.2f);

    [Header("Combo colors")]
    [SerializeField] private Color comboChainColor = new Color(1, 0.7f, 0.2f);
    [SerializeField] private Color timerLowColor = new Color(1, 0.35f, 0.2f);

    private Vector2[] popupPositions;
    private float[] popupAges;
    private float[] popupFontSizes;
    private Vector3[] popupScales;
    private Color[] popupColors;
    private GameManager game;
    private Camera worldCamera;
    private Color originalComboColor;
    private Color originalTimerColor;
    private int nextPopup;
    private float displayedBonus = -1;

    public bool ValidateConfiguration()
    {
        if (!popupRoot || !timerColor || timerColor.type != Image.Type.Filled || !timerColor.sprite || !bonusLabel || !Camera.main
            || popups == null || popups.Length == 0 || popupLifetime <= 0) return false;
        foreach (var popup in popups)
            if (!popup || popup.rectTransform.parent != popupRoot) return false;
        return true;
    }

    public void Init(GameManager manager)
    {
        game = manager;
        worldCamera = Camera.main;
        originalComboColor = game.txtComboCounter.color;
        originalTimerColor = timerColor.color;
        popupPositions = new Vector2[popups.Length];
        popupAges = new float[popups.Length];
        popupFontSizes = new float[popups.Length];
        popupScales = new Vector3[popups.Length];
        popupColors = new Color[popups.Length];
        for (int i = 0; i < popups.Length; i++)
        {
            popupFontSizes[i] = popups[i].fontSize;
            popupScales[i] = popups[i].transform.localScale;
            popupColors[i] = popups[i].color;
        }
        Clear();
    }

    public void ShowPoints(Vector2 worldPosition, float points, int comboCount, bool bonus = false)
    {
        int index = nextPopup;
        nextPopup = (nextPopup + 1) % popups.Length;
        var label = popups[index];
        var canvas = game.txtScore.canvas.rootCanvas;
        var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector3 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(popupRoot, screenPosition, uiCamera, out var localPosition)) return;
        localPosition += popupOffset;
        var halfSize = Vector2.Scale(label.rectTransform.rect.size, new Vector2(Mathf.Abs(popupScales[index].x), Mathf.Abs(popupScales[index].y))) * 0.5f;
        localPosition.x = Mathf.Clamp(localPosition.x, popupRoot.rect.xMin + halfSize.x, popupRoot.rect.xMax - halfSize.x);
        localPosition.y = Mathf.Clamp(localPosition.y, popupRoot.rect.yMin + halfSize.y, popupRoot.rect.yMax - halfSize.y - popupRise);
        // Local coordinates use the parent's pivot; preserve each prefab's authored anchors and pivot.
        var anchor = label.rectTransform.anchorMin + Vector2.Scale(label.rectTransform.anchorMax - label.rectTransform.anchorMin, label.rectTransform.pivot);
        var anchorPosition = popupRoot.rect.min + Vector2.Scale(popupRoot.rect.size, anchor);
        popupPositions[index] = localPosition - anchorPosition;
        popupAges[index] = 0;
        label.rectTransform.anchoredPosition = popupPositions[index];
        label.text = "+" + Mathf.RoundToInt(points) + (bonus ? " BONUS" : "");
        float intensity = Mathf.Clamp01((comboCount - 1) / 5f);
        label.fontSize = popupFontSizes[index] * (bonus ? bonusFontScale : Mathf.Lerp(1, chainFontScale, intensity));
        label.color = Color.Lerp(popupColors[index], chainColor, intensity);
        label.transform.localScale = popupScales[index] * popupStartScale;
        label.gameObject.SetActive(true);
    }

    public void RefreshCombo()
    {
        float fraction = game.maxComboTime > 0 ? Mathf.Clamp01(game.ComboTimeRemaining / game.maxComboTime) : 0;
        timerColor.fillAmount = fraction;
        timerColor.color = Color.Lerp(timerLowColor, originalTimerColor, fraction);
        if (displayedBonus != game.PendingComboBonus)
        {
            displayedBonus = game.PendingComboBonus;
            bonusLabel.gameObject.SetActive(displayedBonus > 0);
            if (displayedBonus > 0) bonusLabel.text = "+" + Mathf.RoundToInt(displayedBonus) + " BONUS";
        }
        game.txtComboCounter.color = Color.Lerp(originalComboColor, comboChainColor, Mathf.Clamp01((game.ComboCount - 1) / 5f));
    }

    private void LateUpdate()
    {
        if (!game) return;
        RefreshCombo();
        if (!game.IsPlaying) return;

        for (int i = 0; i < popups.Length; i++)
        {
            var label = popups[i];
            if (!label.gameObject.activeSelf) continue;
            popupAges[i] += Time.deltaTime;
            float progress = popupAges[i] / popupLifetime;
            if (progress >= 1)
            {
                label.gameObject.SetActive(false);
                continue;
            }
            label.rectTransform.anchoredPosition = popupPositions[i] + Vector2.up * (popupRise * progress);
            label.transform.localScale = popupScales[i] * Mathf.Lerp(popupStartScale, 1, Mathf.Clamp01(progress * 5));
            var color = label.color;
            color.a = popupColors[i].a * (1 - Mathf.InverseLerp(0.4f, 1, progress));
            label.color = color;
        }
    }

    public void Clear()
    {
        foreach (var popup in popups)
            if (popup) popup.gameObject.SetActive(false);
        nextPopup = 0;
        if (game) RefreshCombo();
    }
}
