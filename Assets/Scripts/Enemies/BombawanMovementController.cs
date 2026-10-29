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

    [Header("Death Explosion")]
    [SerializeField, Min(0f)] private float explosionDelay = 0.5f;
    [SerializeField, Min(1)] private int explosionRadius = 5;
    [SerializeField] private bool pierceExplosion;

    private BombController cachedBombController;
    private AudioSource explosionAudioSource;
    private Vector2 deathPosition;
    private bool explosionScheduled;

    protected override void Awake()
    {
        base.Awake();
        explosionAudioSource = GetComponent<AudioSource>();
    }

    protected override void Die()
    {
        if (!Assets.Scripts.Netcode.NetSync.ShouldSimulateLocally) return;
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
