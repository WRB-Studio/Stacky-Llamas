using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    [SerializeField, Tooltip("Opt in to display-notch adaptation. Disabled to preserve the editor layout.")]
    private bool applyDeviceSafeArea;
    private RectTransform rectTransform;
    private Vector2 authoredAnchorMin;
    private Vector2 authoredAnchorMax;
    private Rect previousArea;
    private Vector2Int previousSize;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        authoredAnchorMin = rectTransform.anchorMin;
        authoredAnchorMax = rectTransform.anchorMax;
    }

    private void OnEnable() => ApplySafeArea();
    private void Update() => ApplySafeArea();

    private void ApplySafeArea()
    {
        if (!applyDeviceSafeArea || Screen.width <= 0 || Screen.height <= 0) return;
        Rect area = Screen.safeArea;
        var size = new Vector2Int(Screen.width, Screen.height);
        if (area == previousArea && size == previousSize) return;
        var minimum = new Vector2(area.xMin / size.x, area.yMin / size.y);
        var available = new Vector2(area.width / size.x, area.height / size.y);
        rectTransform.anchorMin = minimum + Vector2.Scale(available, authoredAnchorMin);
        rectTransform.anchorMax = minimum + Vector2.Scale(available, authoredAnchorMax);
        previousArea = area;
        previousSize = size;
    }
}
