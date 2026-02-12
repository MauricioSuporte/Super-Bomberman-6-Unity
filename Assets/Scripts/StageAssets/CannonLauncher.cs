using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(AnimatedSpriteRenderer))]
public sealed class CannonLauncher : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private AnimatedSpriteRenderer cannonAnim;

    [Header("SFX")]
    [SerializeField] private AudioClip fireSfx;
    [SerializeField, Min(0f)] private float fireSfxDelay = 0f;

    [Header("Direction Override")]
    [SerializeField] private bool fireToLeft = false;

    [Header("Fire - Timing")]
    [SerializeField, Min(0f)] private float warmupSeconds = 0.5f;
    [SerializeField, Min(0f)] private float smokeSeconds = 0.25f;

    [Header("Steam")]
    [SerializeField] private AnimatedSpriteRenderer steamPrefab;
    [SerializeField] private Vector2Int steamTileOffset = new Vector2Int(1, 1);

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
        if (TryGetComponent<Collider2D>(out var col)) col.isTrigger = true;
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

        SetCannonIdle(true);
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

        float warm = Mathf.Max(0f, warmupSeconds);
        float smoke = Mathf.Max(0f, smokeSeconds);
        float totalFire = warm + smoke;

        StartCoroutine(PlayFireSfxWithDelay());

        yield return PlayCannonFire(totalFire, warm);

        yield return LaunchPlayerArc(mover, dir);

        mover.SetInputLocked(prevInputLocked);

        if (rearmSeconds > 0f)
            yield return new WaitForSeconds(rearmSeconds);

        busy = false;
    }

    private IEnumerator PlayFireSfxWithDelay()
    {
        if (fireSfx == null || audioSource == null)
            yield break;

        if (fireSfxDelay > 0f)
            yield return new WaitForSeconds(fireSfxDelay);

        audioSource.PlayOneShot(fireSfx);
    }

    private IEnumerator PlayCannonFire(float totalSeconds, float smokeAtSeconds)
    {
        if (totalSeconds <= 0f)
            yield break;

        SetCannonIdle(false);

        float t = 0f;
        bool steamSpawned = false;

        while (t < totalSeconds)
        {
            if (!steamSpawned && t >= smokeAtSeconds)
            {
                SpawnSteam();
                steamSpawned = true;
            }

            t += Time.deltaTime;
            yield return null;
        }

        SetCannonIdle(true);
    }

    private void SetCannonIdle(bool idle)
    {
        if (cannonAnim == null)
            return;

        cannonAnim.idle = idle;

        if (!idle)
        {
            cannonAnim.loop = true;
            cannonAnim.CurrentFrame = 0;
        }

        cannonAnim.RefreshFrame();
    }

    private void SpawnSteam()
    {
        if (steamPrefab == null)
            return;

        Vector2 tileCenter = GetCannonTileCenter();
        float tileSize = GetTileSizeFallback();

        Vector2Int off = GetSteamOffsetFinal();
        Vector2 spawn = tileCenter + new Vector2(off.x * tileSize, off.y * tileSize);

        var steam = Instantiate(steamPrefab, spawn, Quaternion.identity);

        if (steam.TryGetComponent<SpriteRenderer>(out var sr))
            sr.flipX = fireToLeft;

        steam.idle = false;
        steam.loop = false;
        steam.useSequenceDuration = true;
        steam.sequenceDuration = Mathf.Max(0.01f, smokeSeconds);
        steam.CurrentFrame = 0;
        steam.RefreshFrame();

        Destroy(steam.gameObject, smokeSeconds + 0.05f);
    }

    private Vector2Int GetSteamOffsetFinal()
    {
        if (!fireToLeft)
            return steamTileOffset;

        return new Vector2Int(-steamTileOffset.x, steamTileOffset.y);
    }

    private float GetTileSizeFallback()
    {
        if (boxCollider != null)
        {
            float s = Mathf.Min(Mathf.Abs(boxCollider.size.x), Mathf.Abs(boxCollider.size.y));
            if (s > 0.0001f)
                return s;
        }

        return 1f;
    }

    private Vector2 GetCannonTileCenter()
    {
        Vector2 center = boxCollider != null ? (Vector2)boxCollider.bounds.center : (Vector2)transform.position;
        float tileSize = GetTileSizeFallback();

        return new Vector2(
            Mathf.Round(center.x / tileSize) * tileSize,
            Mathf.Round(center.y / tileSize) * tileSize
        );
    }

    private void CenterPlayerOnCannon(MovementController mover)
    {
        if (mover == null || mover.Rigidbody == null)
            return;

        Vector2 center = GetCannonTileCenter();
        mover.SnapToWorldPoint(center, roundToGrid: false);
    }

    private Vector2 GetLaunchDirection()
    {
        Vector2 dir = useTransformRightAsDirection ? (Vector2)transform.right : fallbackDirection;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.right;

        dir.Normalize();

        if (fireToLeft)
            dir = -dir;

        return dir;
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
            float tt = elapsed / duration;

            Vector2 flat = Vector2.Lerp(start, end, tt);
            float parabola = 4f * tt * (1f - tt);
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
