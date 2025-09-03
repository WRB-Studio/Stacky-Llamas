using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnObject : MonoBehaviour
{
    public int value;
    [HideInInspector] public bool inMergeProcess = false;
    [HideInInspector] public static Coroutine starterTriggerRoutine;

    private bool isBounce = false;


    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!GameManager.Instance.isGameOver && collision.transform.tag.Equals("OverflowTrigger"))
        {
            GameManager.Instance.setGameOver();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        SpawnObject otherSpawnObject = collision.transform.GetComponent<SpawnObject>();

        if (GameManager.Instance.firstContact && otherSpawnObject != null)
        {
            GameManager.Instance.firstContact = false;
            starterTriggerRoutine = StartCoroutine(starterContactTriggerRoutine());
        }

        if (otherSpawnObject != null && collision.relativeVelocity.magnitude >= 4)
        {
            if (GetInstanceID() < otherSpawnObject.gameObject.GetInstanceID())
            {
                SoundManager.Instance.PlayBounceSound();
            }
        }
        else if(collision.relativeVelocity.magnitude >= 4)
        {
            SoundManager.Instance.PlayBounceSound();
        }

        if (inMergeProcess)
            return;

        if (otherSpawnObject != null && !isBounce && collision.relativeVelocity.magnitude >= 4)
            StartCoroutine(bounceEffectRoutine(0.1f, 0.05f));

        if (otherSpawnObject != null && otherSpawnObject.value == value)
        {
            if (GetInstanceID() < otherSpawnObject.gameObject.GetInstanceID())
            {
                inMergeProcess = true;
                otherSpawnObject.inMergeProcess = true;

                Destroy(otherSpawnObject.gameObject);
                Destroy(gameObject);

                GameObject newSpawnObject = TouchableSpawner.Instance.SpawnObjectByValue(value + 1);
                if (newSpawnObject != null)
                {
                    Vector3 spawnPosition = otherSpawnObject.transform.position;
                    newSpawnObject.transform.position = spawnPosition;
                }

                GameManager.Instance.addScore(value * 10);
            }
        }
    }

    private IEnumerator bounceEffectRoutine(float duration, float bounceAmount)
    {
        isBounce = true;

        Vector3 originalScale = transform.localScale;
        Vector3 smallerScale = new Vector3(originalScale.x - bounceAmount, originalScale.y - bounceAmount, originalScale.z);

        float elapsedTime = 0f;

        // Shrink the object to the smaller scale
        while (elapsedTime < duration / 2)
        {
            transform.localScale = Vector3.Lerp(originalScale, smallerScale, (elapsedTime / (duration / 2)));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        elapsedTime = 0f;

        // Grow the object back to the original scale
        while (elapsedTime < duration / 2)
        {
            transform.localScale = Vector3.Lerp(smallerScale, originalScale, (elapsedTime / (duration / 2)));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Ensure it's set to the original scale at the end
        transform.localScale = originalScale;

        isBounce = false;
    }

    private static IEnumerator starterContactTriggerRoutine()
    {
        Rigidbody2D[] allRigidbody2D = GameManager.Instance.lamasParent.GetComponentsInChildren<Rigidbody2D>();

        for (int i = 0; i < allRigidbody2D.Length; i++)
        {
            if (allRigidbody2D[i] == null)
                continue;

            allRigidbody2D[i].gravityScale = 1;
            yield return null;
        }
    }


}
