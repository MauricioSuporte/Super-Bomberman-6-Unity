using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AudioSource))]
public sealed class CannonLauncher : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private AnimatedSpriteRenderer cannonAnim;

    [Header("SFX")]
    [SerializeField] private AudioClip fireSfx;

    [Header("Fire - Timing")]
    [SerializeField, Min(0f)] private float warmupSeconds = 0.5f;

    [Header("Launch - Distance")]
    [SerializeField, Min(1)] private int launchTiles = 9;

    [Header("Launch - Arc")]
    [SerializeField, Min(0f)] private float arcHeightTiles = 1.6f;
    [SerializeField, Min(0.05f)] private float flightSeconds = 0.55f;

    [Header("Launch - Direction")]
    [SerializeField] private bool useTransformRightAsDirection = true;
    [SerializeField] private Vector2 fallbackDirection = Vector2.right;

    [Header("End Snap")]
    [SerializeField] private bool roundEndToGrid = true;

    [Header("Cooldown")]
    [SerializeField, Min(0f)] private float rearmSeconds = 0.1f;

    private bool busy;
    private AudioSource audioSource;
    private BoxCollider2D boxCollider;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;

        boxCollider = GetComponent<BoxCollider2D>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }

        if (cannonAnim == null)
            cannonAnim = GetComponent<AnimatedSpriteRenderer>();

        if (cannonAnim != null)
        {
            cannonAnim.idle = true;
            cannonAnim.loop = true;
            cannonAnim.RefreshFrame();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (busy)
            return;

        if (other == null)
            return;

        if (!other.CompareTag("Player"))
            return;

        var mover = other.GetComponent<MovementController>();
        if (mover == null || mover.Rigidbody == null)
            return;

        StartCoroutine(FireRoutine(mover));
    }

    private IEnumerator FireRoutine(MovementController mover)
    {
        busy = true;

        Vector2 dir = GetLaunchDirection();
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.right;
        dir.Normalize();

        bool prevInputLocked = mover.InputLocked;
        mover.SetInputLocked(true);

        CenterPlayerOnCannon(mover);

        yield return PlayCannonWarmup();

        if (audioSource != null && fireSfx != null)
            audioSource.PlayOneShot(fireSfx);

        yield return LaunchPlayerArc(mover, dir);

        mover.SetInputLocked(prevInputLocked);

        if (rearmSeconds > 0f)
            yield return new WaitForSeconds(rearmSeconds);

        busy = false;
    }

    private void CenterPlayerOnCannon(MovementController mover)
    {
        if (mover == null || mover.Rigidbody == null)
            return;

        Vector2 center;

        if (boxCollider != null)
            center = boxCollider.bounds.center;
        else
            center = transform.position;

        mover.SnapToWorldPoint(center, roundToGrid: false);
    }

    private Vector2 GetLaunchDirection()
    {
        if (useTransformRightAsDirection)
            return (Vector2)transform.right;

        return fallbackDirection;
    }

    private IEnumerator PlayCannonWarmup()
    {
        if (warmupSeconds <= 0f)
            yield break;

        if (cannonAnim == null || !cannonAnim.isActiveAndEnabled)
        {
            yield return new WaitForSeconds(warmupSeconds);
            yield break;
        }

        bool prevUseSeq = cannonAnim.useSequenceDuration;
        float prevSeq = cannonAnim.sequenceDuration;

        cannonAnim.useSequenceDuration = true;
        cannonAnim.sequenceDuration = warmupSeconds;

        yield return cannonAnim.PlayCycles(1);

        cannonAnim.useSequenceDuration = prevUseSeq;
        cannonAnim.sequenceDuration = prevSeq;
    }

    private IEnumerator LaunchPlayerArc(MovementController mover, Vector2 dir)
    {
        if (mover == null || mover.Rigidbody == null)
            yield break;

        float tileSize = Mathf.Max(0.0001f, mover.tileSize);

        Rigidbody2D rb = mover.Rigidbody;
        var playerCol = mover.GetComponent<Collider2D>();
        var bombController = mover.GetComponent<BombController>();

        bool prevColliderEnabled = (playerCol != null) && playerCol.enabled;
        bool prevBombEnabled = (bombController != null) && bombController.enabled;

        mover.SetExplosionInvulnerable(true);

        if (bombController != null) bombController.enabled = false;
        if (playerCol != null) playerCol.enabled = false;

        rb.linearVelocity = Vector2.zero;

        Vector2 start = rb.position;
        float distanceWorld = launchTiles * tileSize;
        Vector2 end = start + dir * distanceWorld;

        float arcWorld = arcHeightTiles * tileSize;

        float duration = Mathf.Max(0.05f, flightSeconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;

            Vector2 flat = Vector2.Lerp(start, end, t);
            float parabola = 4f * t * (1f - t);
            Vector2 pos = flat + Vector2.up * (arcWorld * parabola);

            rb.position = pos;

            elapsed += Time.deltaTime;
            yield return null;
        }

        Vector2 finalPos = end;

        if (roundEndToGrid)
        {
            finalPos = new Vector2(
                Mathf.Round(finalPos.x / tileSize) * tileSize,
                Mathf.Round(finalPos.y / tileSize) * tileSize
            );
        }

        rb.position = finalPos;
        rb.linearVelocity = Vector2.zero;

        if (playerCol != null) playerCol.enabled = prevColliderEnabled;
        if (bombController != null) bombController.enabled = prevBombEnabled;

        mover.SetExplosionInvulnerable(false);
    }
}
