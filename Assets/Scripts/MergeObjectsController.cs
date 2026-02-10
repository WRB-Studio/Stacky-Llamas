using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class MergeObjectsController : MonoBehaviour
{
    public static MergeObjectsController Instance;

    private Camera mainCam;

    [Header("Spawn Settings")]
    public float moveRange;
    public float spawnDelay;
    public int spawnableIndexRange = 3;
    public GameObject[] mergeObjects;

    [Header("Runtime Data")]
    private GameObject currentMergeObject;
    public List<MergeObject> instantiatedMergeObjects = new List<MergeObject>();
    private Dictionary<Rigidbody2D, (Vector2 vel, float angVel, float grav)> savedStates = new();

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
        Instance = this;
        mainCam = Camera.main;
    }

    public void Init()
    {
        InitStarterMergeObjects();
        StartCoroutine(SpawnDelayRoutine());
    }

    private void InitStarterMergeObjects()
    {
        GameObject instStarterGrp = Instantiate(starterGrpPrefabs[Random.Range(0, starterGrpPrefabs.Length)], lamasParent);

        List<Transform> children = new List<Transform>();
        foreach (Transform child in instStarterGrp.transform)
        {
            children.Add(child);
            child.GetComponent<Rigidbody2D>().gravityScale = 0;
        }

        foreach (Transform child in children)
        {
            child.SetParent(lamasParent.transform);
            instantiatedMergeObjects.Add(child.GetComponent<MergeObject>());
        }

        Destroy(instStarterGrp);
    }

    private void Update()
    {
        if (GameManager.Instance.isGameOver || GameManager.Instance.isPause || firstTouch)
            return;

        Vector3 mousePosition = Input.mousePosition;
        mousePosition.z = Mathf.Abs(mainCam.transform.position.z);

        Vector3 worldPosition = mainCam.ScreenToWorldPoint(mousePosition);
        worldPosition.x = Mathf.Clamp(worldPosition.x, -moveRange, moveRange);
        worldPosition.y = transform.position.y;
        worldPosition.z = transform.position.z;

        transform.position = worldPosition;

        if (currentMergeObject != null)
            currentMergeObject.transform.position = transform.position;

        // Spawn object on mouse click
        if (currentMergeObject != null && Input.GetMouseButtonUp(0) && !IsPointerOverGUIElements())
        {
            SoundManager.Instance.PlaySpawnSound();
            currentMergeObject.GetComponent<SpriteRenderer>().sortingOrder = 1;

            StartCoroutine(activateDelayRoutine(currentMergeObject));
            currentMergeObject = null;

            StartCoroutine(SpawnDelayRoutine());
        }

        if (!firstContact)
        {
            foreach (var mergeObject in instantiatedMergeObjects)
            {
                if (mergeObject != null && mergeObject.GetComponent<Rigidbody2D>().gravityScale != 1)
                    mergeObject.GetComponent<Rigidbody2D>().gravityScale = 1;
            }
        }
    }

    public void PauseAllMergeObjects(bool pause)
    {
        if (pause)
        {
            savedStates.Clear();
            foreach (var go in instantiatedMergeObjects)
            {
                if (go && go.TryGetComponent(out Rigidbody2D rb) && !savedStates.ContainsKey(rb))
                {
                    savedStates[rb] = (rb.linearVelocity, rb.angularVelocity, rb.gravityScale);
                    rb.simulated = false;
                }
            }
        }
        else
        {
            foreach (var kvp in savedStates)
            {
                var rb = kvp.Key;
                if (!rb) continue;

                rb.simulated = true;
                rb.linearVelocity = kvp.Value.vel;
                rb.angularVelocity = kvp.Value.angVel;
                rb.gravityScale = kvp.Value.grav;
            }
        }
    }

    private IEnumerator activateDelayRoutine(GameObject mergeObject)
    {
        yield return new WaitForSeconds(0.25f);
        mergeObject.GetComponent<Rigidbody2D>().simulated = true;

        Color tmpColor = mergeObject.GetComponent<SpriteRenderer>().color;
        tmpColor.a = 1f;
        mergeObject.GetComponent<SpriteRenderer>().color = tmpColor;
    }

    private IEnumerator SpawnDelayRoutine()
    {
        yield return new WaitForSeconds(spawnDelay);

        int randomIndex = Random.Range(0, spawnableIndexRange);
        currentMergeObject = SpawnMergeObject(mergeObjects[randomIndex]);
        currentMergeObject.GetComponent<Rigidbody2D>().simulated = false;
        currentMergeObject.GetComponent<SpriteRenderer>().sortingOrder = -1;

        Color tmpColor = currentMergeObject.GetComponent<SpriteRenderer>().color;
        tmpColor.a = 0.6f;
        currentMergeObject.GetComponent<SpriteRenderer>().color = tmpColor;
    }

    public GameObject SpawnMergeObject(GameObject mergeObject)
    {
        GameObject newMergeObject = Instantiate(mergeObject, transform.position, Quaternion.Euler(0, 0, Random.Range(0f, 360f)), lamasParent);
        instantiatedMergeObjects.Add(newMergeObject.GetComponent<MergeObject>());
        return newMergeObject;
    }

    public GameObject SpawnMergeObjectByValue(int value)
    {
        foreach (GameObject mergeObject in mergeObjects)
        {
            MergeObject mergeObjectForSpawning = mergeObject.GetComponent<MergeObject>();
            if (mergeObjectForSpawning.value == value)
                return SpawnMergeObject(mergeObjectForSpawning.gameObject);
        }

        return null;
    }

    private bool IsPointerOverGUIElements()
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject == GameManager.Instance.btnPause.gameObject || result.gameObject == GameManager.Instance.panelPause.gameObject)
            {
                return true;
            }
        }

        return false;
    }

    public void SpawnMergeEffect(Vector2 position, int mergeValue)
    {
        GameObject newMergeEffect = Instantiate(mergeEffect, position, Quaternion.identity, mergeEffectParent);

        float t = Mathf.InverseLerp(0, mergeObjects.Length - 1, mergeValue);
        float scale = Mathf.Lerp(1f, 3f, t);
        newMergeEffect.transform.localScale = Vector3.one * scale;

        Destroy(newMergeEffect, 3f);
    }
}
