using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ChameleonMovementController : EnemyMovementController
{
    [Header("Chameleon Animation")]
    public SpriteRenderer spriteRenderer;
    public Sprite idleSprite;
    public Sprite[] blinkSprites;
    public float blinkMinInterval = 2f;
    public float blinkMaxInterval = 3f;
    public float blinkDuration = 0.2f;

    private Coroutine blinkRoutine;

    protected override void Awake()
    {
        base.Awake();

        if (!spriteRenderer)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    protected override void Start()
    {
        base.Start();

        if (spriteRenderer && idleSprite)
            spriteRenderer.sprite = idleSprite;

        StartBlinkLoop();
    }

    private void OnEnable()
    {
        if (spriteRenderer && idleSprite)
            spriteRenderer.sprite = idleSprite;

        StartBlinkLoop();
    }

    private void OnDisable()
    {
        StopBlinkLoop();
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
    }

    private void StartBlinkLoop()
    {
        if (blinkRoutine != null)
            StopCoroutine(blinkRoutine);

        if (gameObject.activeInHierarchy)
            blinkRoutine = StartCoroutine(BlinkLoop());
    }

    private void StopBlinkLoop()
    {
        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }
    }

    private IEnumerator BlinkLoop()
    {
        while (!isDead)
        {
            float wait = Random.Range(blinkMinInterval, blinkMaxInterval);
            yield return new WaitForSeconds(wait);

            yield return BlinkOnce();
        }

        blinkRoutine = null;
    }

    private IEnumerator BlinkOnce()
    {
        if (!spriteRenderer || blinkSprites == null || blinkSprites.Length == 0)
            yield break;

        float duration = Mathf.Max(blinkDuration, 0.01f);
        float frameTime = duration / blinkSprites.Length;

        for (int i = 0; i < blinkSprites.Length; i++)
        {
            spriteRenderer.sprite = blinkSprites[i];
            yield return new WaitForSeconds(frameTime);
        }

        if (idleSprite)
            spriteRenderer.sprite = idleSprite;
    }

    protected override void Die()
    {
        if (isDead)
            return;

        StopBlinkLoop();

        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        base.Die();
    }
}
