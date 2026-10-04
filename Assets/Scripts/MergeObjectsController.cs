using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class MergeObjectsController : MonoBehaviour
{
    public static MergeObjectsController Instance { get; private set; }
    private Camera mainCam;
    private Vector3 initialPosition;

    [Header("Spawn Settings")]
    [Min(0)] public float moveRange;
    [Min(0)] public float spawnDelay;
    [Min(1)] public int spawnableIndexRange = 3;
    public GameObject[] mergeObjects;

    private MergeObject currentMergeObject;
    public List<MergeObject> instantiatedMergeObjects = new List<MergeObject>();
    private readonly Dictionary<int, GameObject> prefabsByValue = new();
    private readonly Dictionary<Rigidbody2D, (Vector2 velocity, float angularVelocity, float gravity, bool simulated)> savedStates = new();
    private readonly Dictionary<MergeObject, float> pendingActivations = new();
    private readonly List<MergeObject> activationBuffer = new();
    private readonly List<RaycastResult> raycastResults = new();
    private float spawnTimer = -1f;
    private bool physicsPaused;
    private bool dragging;
    private int highestValue;

    [Header("Starter Settings")]
    public bool firstTouch = true;
    public bool firstContact = true;
    public GameObject[] starterGrpPrefabs;
    public Transform lamasParent;

    [Header("Effects")]
    public GameObject mergeEffect;
    public Transform mergeEffectParent;

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Debug.LogError("Only one MergeObjectsController is allowed in a scene.", this);
            enabled = false;
            return;
        }
        Instance = this;
        mainCam = Camera.main;
        initialPosition = transform.position;
    }

    public bool ValidateConfiguration()
    {
        prefabsByValue.Clear();
        highestValue = 0;
        bool valid = mainCam && lamasParent && mergeEffect && mergeEffectParent
            && EventSystem.current && spawnDelay >= 0 && moveRange >= 0
            && mergeObjects != null && mergeObjects.Length > 0
            && spawnableIndexRange > 0 && spawnableIndexRange <= mergeObjects.Length
            && starterGrpPrefabs != null && starterGrpPrefabs.Length > 0;
        if (mergeObjects != null)
        {
            foreach (var prefab in mergeObjects)
            {
                if (!prefab || !prefab.TryGetComponent(out MergeObject item)
                    || !prefab.GetComponent<Rigidbody2D>() || !prefab.GetComponent<SpriteRenderer>()
                    || !prefab.GetComponent<Collider2D>() || item.value <= 0
                    || prefabsByValue.ContainsKey(item.value))
                {
                    valid = false;
                    continue;
                }
                prefabsByValue.Add(item.value, prefab);
                highestValue = Mathf.Max(highestValue, item.value);
            }
        }
        for (int value = 1; value <= highestValue; value++)
            valid &= prefabsByValue.ContainsKey(value);
        if (starterGrpPrefabs != null)
        {
            foreach (var prefab in starterGrpPrefabs)
            {
                if (!prefab || prefab.transform.childCount == 0)
                {
                    valid = false;
                    continue;
                }
                foreach (Transform child in prefab.transform)
                {
                    var item = child.GetComponent<MergeObject>();
                    valid &= item && child.GetComponent<Rigidbody2D>()
                        && child.GetComponent<SpriteRenderer>() && child.GetComponent<Collider2D>()
                        && prefabsByValue.ContainsKey(item.value);
                }
            }
        }
        if (!valid) Debug.LogError("Invalid spawn configuration: check camera, EventSystem, references, prefab components, values and spawn range.", this);
        return valid;
    }

    public void Init()
    {
        if (!firstTouch) return;
        firstTouch = false;
        var group = Instantiate(starterGrpPrefabs[Random.Range(0, starterGrpPrefabs.Length)], lamasParent);
        while (group.transform.childCount > 0)
        {
            var child = group.transform.GetChild(0);
            child.SetParent(lamasParent, true);
            var item = child.GetComponent<MergeObject>();
            item.Body.gravityScale = 0;
            instantiatedMergeObjects.Add(item);
        }
        Destroy(group);
        spawnTimer = spawnDelay;
    }

    private void Update()
    {
        if (!GameManager.Instance || !GameManager.Instance.IsPlaying)
        {
            dragging = false;
            return;
        }
        UpdateDelayedObjects(Time.deltaTime);
        if (Input.GetMouseButtonDown(0)) dragging = !IsPointerOverGUIElements();
        if (!dragging) return;
        var position = Input.mousePosition;
        position.z = Mathf.Abs(mainCam.transform.position.z);
        var worldPosition = mainCam.ScreenToWorldPoint(position);
        worldPosition.x = Mathf.Clamp(worldPosition.x, -moveRange, moveRange);
        worldPosition.y = transform.position.y;
        worldPosition.z = transform.position.z;
        transform.position = worldPosition;
        if (currentMergeObject) currentMergeObject.transform.position = worldPosition;
        if (!Input.GetMouseButtonUp(0)) return;
        dragging = false;
        if (!currentMergeObject || IsPointerOverGUIElements()) return;
        SoundManager.Instance.PlaySpawnSound();
        currentMergeObject.Sprite.sortingOrder = 1;
        currentMergeObject.PrepareDrop(transform.position.y);
        pendingActivations.Add(currentMergeObject, 0.25f);
        currentMergeObject = null;
        spawnTimer = spawnDelay;
    }

    private void UpdateDelayedObjects(float deltaTime)
    {
        activationBuffer.Clear();
        activationBuffer.AddRange(pendingActivations.Keys);
        foreach (var item in activationBuffer)
        {
            float remaining = pendingActivations[item] - deltaTime;
            if (!item || remaining <= 0)
            {
                pendingActivations.Remove(item);
                if (item)
                {
                    item.Body.simulated = true;
                    var color = item.Sprite.color;
                    color.a = 1;
                    item.Sprite.color = color;
                }
            }
            else pendingActivations[item] = remaining;
        }
        if (spawnTimer < 0) return;
        spawnTimer -= deltaTime;
        if (spawnTimer > 0) return;
        spawnTimer = -1;
        var prefab = mergeObjects[Random.Range(0, spawnableIndexRange)];
        currentMergeObject = SpawnMergeObject(prefab).GetComponent<MergeObject>();
        currentMergeObject.Body.simulated = false;
        currentMergeObject.Sprite.sortingOrder = -1;
        var previewColor = currentMergeObject.Sprite.color;
        previewColor.a = 0.6f;
        currentMergeObject.Sprite.color = previewColor;
    }

    public void ReleaseStarterObjects()
    {
        if (!firstContact) return;
        firstContact = false;
        foreach (var item in instantiatedMergeObjects)
            if (item) item.Body.gravityScale = 1;
    }

    public void PauseAllMergeObjects(bool pause)
    {
        dragging = false;
        if (physicsPaused == pause) return;
        physicsPaused = pause;
        if (pause)
        {
            savedStates.Clear();
            foreach (var item in instantiatedMergeObjects)
            {
                if (!item) continue;
                var rb = item.Body;
                savedStates[rb] = (rb.linearVelocity, rb.angularVelocity, rb.gravityScale, rb.simulated);
                rb.simulated = false;
            }
        }
        else
        {
            foreach (var state in savedStates)
            {
                var rb = state.Key;
                if (!rb) continue;
                rb.simulated = state.Value.simulated;
                rb.linearVelocity = state.Value.velocity;
                rb.angularVelocity = state.Value.angularVelocity;
                rb.gravityScale = state.Value.gravity;
            }
            savedStates.Clear();
        }
    }

    public void StopSpawning()
    {
        spawnTimer = -1;
        pendingActivations.Clear();
        dragging = false;
    }

    public void ResetGame()
    {
        StopSpawning();
        foreach (Transform child in lamasParent)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        foreach (Transform child in mergeEffectParent) Destroy(child.gameObject);
        instantiatedMergeObjects.Clear();
        savedStates.Clear();
        activationBuffer.Clear();
        currentMergeObject = null;
        physicsPaused = false;
        firstTouch = true;
        firstContact = true;
        transform.position = initialPosition;
    }

    public void Unregister(MergeObject item)
    {
        instantiatedMergeObjects.Remove(item);
        pendingActivations.Remove(item);
        if (item.Body) savedStates.Remove(item.Body);
        if (currentMergeObject == item) currentMergeObject = null;
    }

    public GameObject SpawnMergeObject(GameObject prefab)
    {
        var go = Instantiate(prefab, transform.position, Quaternion.Euler(0, 0, Random.Range(0f, 360f)), lamasParent);
        instantiatedMergeObjects.Add(go.GetComponent<MergeObject>());
        return go;
    }

    public GameObject SpawnMergeObjectByValue(int value)
    {
        return prefabsByValue.TryGetValue(value, out var prefab) ? SpawnMergeObject(prefab) : null;
    }

    public bool IsHighestValue(int value) => value == highestValue;

    public bool IsPointerOverGUIElements()
    {
        if (!EventSystem.current) return false;
        var pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        raycastResults.Clear();
        EventSystem.current.RaycastAll(pointer, raycastResults);
        foreach (var hit in raycastResults)
            if (hit.gameObject.GetComponentInParent<UnityEngine.UI.Selectable>()) return true;
        return false;
    }

    public void SpawnMergeEffect(Vector2 position, int value, int comboCount = 1)
    {
        var effect = Instantiate(mergeEffect, position, Quaternion.identity, mergeEffectParent);
        float scale = Mathf.Lerp(1, 3, Mathf.InverseLerp(1, highestValue, value));
        float intensity = Mathf.Clamp01((comboCount - 1) / 5f);
        effect.transform.localScale = Vector3.one * scale * Mathf.Lerp(1, 1.5f, intensity);
        var particles = effect.GetComponent<ParticleSystem>();
        if (particles && intensity > 0)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.startSpeedMultiplier *= Mathf.Lerp(1, 1.35f, intensity);
            main.startSizeMultiplier *= Mathf.Lerp(1, 1.2f, intensity);
            main.startColor = Color.Lerp(Color.white, new Color(1, 0.75f, 0.25f), intensity);
            particles.Play();
            particles.Emit(Mathf.RoundToInt(12 * intensity));
        }
        Destroy(effect, 3);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
