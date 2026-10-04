using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect previousArea;
    private Vector2Int previousSize;

    public static void CreateForCanvas(Canvas canvas)
    {
        if (!canvas || canvas.GetComponentInChildren<SafeArea>()) return;
        var container = new GameObject("SafeArea", typeof(RectTransform));
        var rect = container.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        int childCount = canvas.transform.childCount - 1;
        for (int i = 0; i < childCount; i++)
            canvas.transform.GetChild(0).SetParent(rect, false);
        container.AddComponent<SafeArea>();
    }

    private void Awake() => rectTransform = GetComponent<RectTransform>();
    private void OnEnable() => ApplySafeArea();
    private void Update() => ApplySafeArea();

    private void ApplySafeArea()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;
        Rect area = Screen.safeArea;
        var size = new Vector2Int(Screen.width, Screen.height);
        if (area == previousArea && size == previousSize) return;
        rectTransform.anchorMin = new Vector2(area.xMin / size.x, area.yMin / size.y);
        rectTransform.anchorMax = new Vector2(area.xMax / size.x, area.yMax / size.y);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        previousArea = area;
        previousSize = size;
    }
}
