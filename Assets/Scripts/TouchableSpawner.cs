using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class TouchableSpawner : MonoBehaviour
{
    public float moveRange;
    public GameObject[] spawnObjects;
    public float spawnDelay;
    public int spawnableIndexRange = 3;

    private GameObject currentSpawnObject;
    public List<SpawnObject> allSpawnObjects = new List<SpawnObject>();

    public static TouchableSpawner Instance;


    private void Awake()
    {
        Instance = this;
    }

    public void Init()
    {
        StartCoroutine(SpawnDelayRoutine());
    }

    private void Update()
    {
        if (GameManager.Instance.isGameOver || GameManager.Instance.isPause || GameManager.Instance.firstTouch)
            return;

        Vector3 mousePosition = Input.mousePosition;
        mousePosition.z = Mathf.Abs(Camera.main.transform.position.z);

        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(mousePosition);
        worldPosition.x = Mathf.Clamp(worldPosition.x, -moveRange, moveRange);
        worldPosition.y = transform.position.y;
        worldPosition.z = transform.position.z;

        transform.position = worldPosition;

        if (currentSpawnObject != null)
            currentSpawnObject.transform.position = transform.position;

        // Spawn object on mouse click
        if (currentSpawnObject != null && Input.GetMouseButtonUp(0) && !IsPointerOverGUIElements())
        {
            SoundManager.Instance.PlaySpawnSound();
            currentSpawnObject.GetComponent<SpriteRenderer>().sortingOrder = 1;

            StartCoroutine(activateDelayRoutine(currentSpawnObject));
            currentSpawnObject = null;

            StartCoroutine(SpawnDelayRoutine());
        }

        if (!GameManager.Instance.firstContact)
        {
            foreach (var spawnObject in allSpawnObjects)
            {
                if (spawnObject != null && spawnObject.GetComponent<Rigidbody2D>().gravityScale != 1)
                    spawnObject.GetComponent<Rigidbody2D>().gravityScale = 1;
            }
        }
    }

    private IEnumerator activateDelayRoutine(GameObject spawnObject)
    {
        yield return new WaitForSeconds(0.25f);
        spawnObject.GetComponent<Rigidbody2D>().simulated = true;

        Color tmpColor = spawnObject.GetComponent<SpriteRenderer>().color;
        tmpColor.a = 1f;
        spawnObject.GetComponent<SpriteRenderer>().color = tmpColor;
    }

    private IEnumerator SpawnDelayRoutine()
    {
        yield return new WaitForSeconds(spawnDelay);

        int randomIndex = Random.Range(0, spawnableIndexRange);
        currentSpawnObject = SpawnObject(spawnObjects[randomIndex]);
        currentSpawnObject.GetComponent<Rigidbody2D>().simulated = false;
        currentSpawnObject.GetComponent<SpriteRenderer>().sortingOrder = -1;

        Color tmpColor = currentSpawnObject.GetComponent<SpriteRenderer>().color;
        tmpColor.a = 0.6f;
        currentSpawnObject.GetComponent<SpriteRenderer>().color = tmpColor;
    }

    public GameObject SpawnObject(GameObject spawnObject)
    {
        GameObject newSpawnObject = Instantiate(spawnObject, transform.position, Quaternion.Euler(0, 0, Random.Range(0f, 360f)), GameManager.Instance.lamasParent);
        allSpawnObjects.Add(newSpawnObject.GetComponent<SpawnObject>());
        return newSpawnObject;
    }

    public GameObject SpawnObjectByValue(int value)
    {
        foreach (GameObject spawnObject in spawnObjects)
        {
            SpawnObject objectForSpawning = spawnObject.GetComponent<SpawnObject>();
            if (objectForSpawning.value == value)
                return SpawnObject(objectForSpawning.gameObject);
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

}
