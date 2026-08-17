using System.Collections;
using UnityEngine;

/// <summary>
/// Junction-turning enemy that detonates with a normal cross explosion shortly
/// after its death animation begins.
/// </summary>
public sealed class BombawanMovementController : JunctionTurningEnemyMovementController
{
    [Header("Fuse")]
    [SerializeField] private AnimatedSpriteRenderer fuseAnimation;

    [Header("Vertical Walk")]
    [Tooltip("Alternates the body and fuse horizontally each time an Up or Down walk cycle restarts.")]
    [SerializeField] private bool flipOnVerticalWalkCycle = true;

    [Header("Death Explosion")]
    [SerializeField, Min(0f)] private float explosionDelay = 0.5f;
    [SerializeField, Min(1)] private int explosionRadius = 5;
    [SerializeField] private bool pierceExplosion;

    private BombController cachedBombController;
    private AudioSource explosionAudioSource;
    private Vector2 deathPosition;
    private bool explosionScheduled;
    private SpriteRenderer fuseRenderer;
    private int previousVerticalFrame = -1;
    private bool verticalCycleFlipped;
    private bool isMovingVertically;

    protected override void Awake()
    {
        base.Awake();
        explosionAudioSource = GetComponent<AudioSource>();
        fuseRenderer = fuseAnimation != null ? fuseAnimation.GetComponent<SpriteRenderer>() : null;
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        base.UpdateSpriteDirection(dir);

        isMovingVertically = Mathf.Abs(dir.y) > 0.5f;
        if (!isMovingVertically)
        {
            previousVerticalFrame = -1;
            // EnemyMovementController handles the body: Left stays unflipped
            // and Right mirrors that same Left renderer. Keep the fuse aligned
            // without overriding the controller's horizontal body flip.
            if (fuseRenderer != null)
                fuseRenderer.flipX = dir.x > 0.5f;
            return;
        }

        ApplyVerticalCycleFlip(verticalCycleFlipped);
    }

    private void LateUpdate()
    {
        if (!flipOnVerticalWalkCycle || !isMovingVertically || activeSprite == null || !activeSprite.enabled)
            return;

        int currentFrame = activeSprite.CurrentFrame;
        if (previousVerticalFrame >= 0 && currentFrame < previousVerticalFrame)
        {
            verticalCycleFlipped = !verticalCycleFlipped;
            ApplyVerticalCycleFlip(verticalCycleFlipped);
        }

        previousVerticalFrame = currentFrame;
    }

    private void ApplyVerticalCycleFlip(bool flipX)
    {
        if (activeSprite != null && activeSprite.TryGetComponent(out SpriteRenderer bodyRenderer))
            bodyRenderer.flipX = flipX;

        if (fuseRenderer != null)
            fuseRenderer.flipX = flipX;
    }

    protected override void Die()
    {
        if (isDead)
            return;

        deathPosition = rb != null ? rb.position : (Vector2)transform.position;

        if (fuseAnimation != null)
            fuseAnimation.enabled = false;

        base.Die();

        if (!explosionScheduled)
        {
            explosionScheduled = true;
            StartCoroutine(ExplodeAfterDeathDelay());
        }
    }

    private IEnumerator ExplodeAfterDeathDelay()
    {
        yield return new WaitForSeconds(explosionDelay);

        CacheBombController();
        if (cachedBombController == null)
            yield break;

        cachedBombController.SpawnExplosionCrossForEffectWithTileEffects(
            deathPosition,
            explosionRadius,
            pierceExplosion,
            explosionAudioSource);
    }

    private void CacheBombController()
    {
        if (cachedBombController != null)
            return;

        BombController[] controllers = FindObjectsByType<BombController>();
        for (int i = 0; i < controllers.Length; i++)
        {
            BombController controller = controllers[i];
            if (controller != null && controller.CompareTag("Player"))
            {
                cachedBombController = controller;
                return;
            }
        }
    }
}
