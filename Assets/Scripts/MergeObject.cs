using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer), typeof(CircleCollider2D))]
public class MergeObject : MonoBehaviour
{
    [Min(1)] public int value;
    public bool inMergeProcess { get; private set; }
    public Rigidbody2D Body { get; private set; }
    public SpriteRenderer Sprite { get; private set; }
    private bool isBounce;

    private void Awake()
    {
        Body = GetComponent<Rigidbody2D>();
        Sprite = GetComponent<SpriteRenderer>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (GameManager.Instance && GameManager.Instance.IsPlaying && !inMergeProcess
            && collision.CompareTag("OverflowTrigger"))
            GameManager.Instance.SetGameOver();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!GameManager.Instance || !GameManager.Instance.IsPlaying || inMergeProcess) return;
        var other = collision.collider.GetComponent<MergeObject>();
        if (other && other.inMergeProcess) return;
        if (other) MergeObjectsController.Instance.ReleaseStarterObjects();
        if (collision.relativeVelocity.sqrMagnitude >= 16f)
        {
            if (!other || GetInstanceID() < other.GetInstanceID())
                SoundManager.Instance.PlayBounceSound();
            if (other && !isBounce) StartCoroutine(BounceEffectRoutine(0.1f, 0.05f));
        }
        if (other && GetInstanceID() < other.GetInstanceID()) TryMerge(other);
    }

    public bool TryMerge(MergeObject other)
    {
        if (!GameManager.Instance || !GameManager.Instance.IsPlaying || !other || other == this
            || inMergeProcess || other.inMergeProcess || value != other.value
            || !Body.simulated || !other.Body.simulated) return false;
        var controller = MergeObjectsController.Instance;
        Vector3 position = other.transform.position;
        var successor = controller.SpawnMergeObjectByValue(value + 1);
        if (!successor && !controller.IsHighestValue(value)) return false;
        inMergeProcess = true;
        other.inMergeProcess = true;
        if (successor) successor.transform.position = position;
        GameManager.Instance.AddScore(value * 10);
        controller.SpawnMergeEffect(position, value);
        Consume();
        other.Consume();
        return true;
    }

    private void Consume()
    {
        MergeObjectsController.Instance.Unregister(this);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private IEnumerator BounceEffectRoutine(float duration, float bounceAmount)
    {
        isBounce = true;
        Vector3 originalScale = transform.localScale;
        Vector3 smallerScale = originalScale - new Vector3(bounceAmount, bounceAmount, 0);
        float elapsed = 0;
        while (elapsed < duration)
        {
            if (GameManager.Instance && GameManager.Instance.IsPlaying)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localScale = Vector3.Lerp(originalScale, smallerScale, 1 - Mathf.Abs(2 * t - 1));
            }
            yield return null;
        }
        transform.localScale = originalScale;
        isBounce = false;
    }

    private void OnDestroy()
    {
        if (MergeObjectsController.Instance) MergeObjectsController.Instance.Unregister(this);
    }
}
