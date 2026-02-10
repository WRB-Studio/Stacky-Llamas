using System.Collections;
using UnityEngine;

public class MergeObject : MonoBehaviour
{
    public int value;
    [HideInInspector] public bool inMergeProcess = false;
    [HideInInspector] public static Coroutine starterTriggerRoutine;

    private bool isBounce = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!GameManager.Instance.isGameOver && collision.CompareTag("OverflowTrigger"))
        {
            GameManager.Instance.setGameOver();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        MergeObject otherMergeObject = collision.transform.GetComponent<MergeObject>();

        if (MergeObjectsController.Instance.firstContact && otherMergeObject != null)
        {
            MergeObjectsController.Instance.firstContact = false;
            starterTriggerRoutine = StartCoroutine(starterContactTriggerRoutine());
        }

        if (collision.relativeVelocity.magnitude >= 4f)
        {
            // nur einmal Sound abspielen (bei MergeObject vs MergeObject) um Doppel-Sounds zu vermeiden
            if (otherMergeObject == null || GetInstanceID() < otherMergeObject.GetInstanceID())
                SoundManager.Instance.PlayBounceSound();
        }

        if (inMergeProcess)
            return;

        if (otherMergeObject != null && !isBounce && collision.relativeVelocity.magnitude >= 4)
            StartCoroutine(bounceEffectRoutine(0.1f, 0.05f));

        if (otherMergeObject != null && otherMergeObject.value == value)
        {
            if (GetInstanceID() < otherMergeObject.gameObject.GetInstanceID())
            {
                inMergeProcess = true;
                otherMergeObject.inMergeProcess = true;

                Destroy(otherMergeObject.gameObject);
                Destroy(gameObject);

                GameObject newMergeObject = MergeObjectsController.Instance.SpawnMergeObjectByValue(value + 1);
                if (newMergeObject != null)
                {
                    Vector3 spawnPosition = otherMergeObject.transform.position;
                    newMergeObject.transform.position = spawnPosition;
                }

                GameManager.Instance.addScore(value * 10);
                MergeObjectsController.Instance.SpawnMergeEffect(transform.position, value);
            }
        }
    }

    private IEnumerator bounceEffectRoutine(float duration, float bounceAmount)
    {
        isBounce = true;

        Vector3 originalScale = transform.localScale;
        Vector3 smallerScale = new Vector3(originalScale.x - bounceAmount, originalScale.y - bounceAmount, originalScale.z);

        float elapsedTime = 0f;

        while (elapsedTime < duration / 2)
        {
            transform.localScale = Vector3.Lerp(originalScale, smallerScale, (elapsedTime / (duration / 2)));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        elapsedTime = 0f;

        while (elapsedTime < duration / 2)
        {
            transform.localScale = Vector3.Lerp(smallerScale, originalScale, (elapsedTime / (duration / 2)));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.localScale = originalScale;
        isBounce = false;
    }

    private static IEnumerator starterContactTriggerRoutine()
    {
        Rigidbody2D[] allRigidbody2D = MergeObjectsController.Instance.lamasParent.GetComponentsInChildren<Rigidbody2D>();

        for (int i = 0; i < allRigidbody2D.Length; i++)
        {
            if (allRigidbody2D[i] == null)
                continue;

            allRigidbody2D[i].gravityScale = 1;
            yield return null;
        }
    }
}
