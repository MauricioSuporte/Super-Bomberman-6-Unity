using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(AudioSource))]
public sealed class PlayerPushedOutOfInvalidTile : MonoBehaviour
{
    [Header("Hole Detection")]
    [SerializeField] private LayerMask holeLayerMask;
    [SerializeField] private string holeTag = "Hole";

    [Header("Resolve When Stuck")]
    [SerializeField, Min(0.1f)] private float resolveTilesPerSecond = 12f;
    [SerializeField, Min(1)] private int maxResolveSteps = 80;
    [SerializeField] private bool disablePlayerColliderWhileResolving = true;
    [SerializeField] private bool snapToGridBeforeResolve = true;
    [SerializeField] private bool lockInputWhileResolving = true;

    [Header("Bounce SFX")]
    [SerializeField] private AudioClip bounceSfx;
    [SerializeField, Range(0f, 1f)] private float bounceSfxVolume = 1f;

    private Rigidbody2D _rb;
    private Collider2D _col;
    private MovementController _move;
    private AbilitySystem _abilitySystem;
    private AudioSource _audio;

    private Coroutine _resolveRoutine;

    private static readonly WaitForFixedUpdate _waitFixed = new();

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();
        _move = GetComponent<MovementController>();
        _abilitySystem = GetComponent<AbilitySystem>();
        _audio = GetComponent<AudioSource>();

        if (holeLayerMask.value == 0)
            holeLayerMask = LayerMask.GetMask("Stage");
    }

    public void NotifyExternalPushed(Vector2 pushDir)
    {
        pushDir = NormalizeCardinal(pushDir);
        if (pushDir == Vector2.zero)
            return;

        if (_resolveRoutine != null)
            StopCoroutine(_resolveRoutine);

        _resolveRoutine = StartCoroutine(ResolveIfStuckRoutine(pushDir));
    }

    private IEnumerator ResolveIfStuckRoutine(Vector2 pushDir)
    {
        if (_move.isDead)
            yield break;

        float tileSize = Mathf.Max(0.0001f, _move.tileSize);
        Vector2 pos = _rb.position;

        if (snapToGridBeforeResolve)
            pos = SnapToGrid(pos, tileSize);

        _rb.position = pos;
        transform.position = pos;

        if (IsOnHole(pos))
        {
            _move.KillByHole();
            yield break;
        }

        if (!IsBlockedAtPosition(pos, pushDir))
            yield break;

        bool prevColliderEnabled = _col.enabled;

        if (lockInputWhileResolving)
            _move.SetInputLocked(true, true);

        _move.ForceFacingDirection(pushDir);

        if (disablePlayerColliderWhileResolving)
            _col.enabled = false;

        float speedTilesPerSec = Mathf.Max(0.1f, resolveTilesPerSecond);
        float travelTime = 1f / speedTilesPerSec;

        Vector2 cur = pos;

        for (int step = 0; step < maxResolveSteps; step++)
        {
            if (IsOnHole(cur))
            {
                _col.enabled = prevColliderEnabled;
                _move.KillByHole();
                yield break;
            }

            if (!IsBlockedAtPosition(cur, pushDir))
                break;

            PlayBounceSfx();

            Vector2 next = cur + pushDir * tileSize;

            if (IsOnHole(next))
            {
                yield return MoveOneTile(cur, next, travelTime);
                _col.enabled = prevColliderEnabled;
                _move.KillByHole();
                yield break;
            }

            yield return MoveOneTile(cur, next, travelTime);
            cur = next;
        }

        cur = SnapToGrid(cur, tileSize);
        _rb.position = cur;
        transform.position = cur;

        _col.enabled = prevColliderEnabled;

        if (lockInputWhileResolving && !_move.isDead)
            _move.SetInputLocked(false, true);
    }

    private IEnumerator MoveOneTile(Vector2 start, Vector2 end, float travelTime)
    {
        float t = 0f;

        while (t < travelTime)
        {
            t += Time.fixedDeltaTime;
            float a = Mathf.Clamp01(t / travelTime);

            Vector2 p = Vector2.Lerp(start, end, a);
            _rb.MovePosition(p);
            transform.position = p;

            yield return _waitFixed;
        }

        _rb.position = end;
        transform.position = end;
    }

    private bool IsOnHole(Vector2 worldPos)
    {
        Collider2D hit = Physics2D.OverlapBox(worldPos, Vector2.one * 0.45f, 0f, holeLayerMask);
        if (hit == null)
            return false;

        return hit.CompareTag(holeTag);
    }

    private bool IsBlockedAtPosition(Vector2 targetPosition, Vector2 dirForSize)
    {
        float tileSize = Mathf.Max(0.0001f, _move.tileSize);

        Vector2 size =
            Mathf.Abs(dirForSize.x) > 0f
                ? new Vector2(tileSize * 0.6f, tileSize * 0.2f)
                : new Vector2(tileSize * 0.2f, tileSize * 0.6f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(targetPosition, size, 0f, _move.obstacleMask);
        if (hits == null || hits.Length == 0)
            return false;

        bool canPassDestructibles =
            _abilitySystem != null &&
            _abilitySystem.IsEnabled(DestructiblePassAbility.AbilityId);

        foreach (var hit in hits)
        {
            if (hit == null) continue;
            if (hit.gameObject == gameObject) continue;
            if (hit.isTrigger) continue;

            if (canPassDestructibles && hit.CompareTag("Destructibles"))
                continue;

            return true;
        }

        return false;
    }

    private static Vector2 NormalizeCardinal(Vector2 dir)
    {
        if (dir.sqrMagnitude <= 0.01f)
            return Vector2.zero;

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return new Vector2(Mathf.Sign(dir.x), 0f);

        return new Vector2(0f, Mathf.Sign(dir.y));
    }

    private static Vector2 SnapToGrid(Vector2 worldPos, float tileSize)
    {
        worldPos.x = Mathf.Round(worldPos.x / tileSize) * tileSize;
        worldPos.y = Mathf.Round(worldPos.y / tileSize) * tileSize;
        return worldPos;
    }

    private void PlayBounceSfx()
    {
        if (_audio == null || bounceSfx == null)
            return;

        if (_audio.isPlaying && _audio.clip == bounceSfx)
            _audio.Stop();

        _audio.clip = bounceSfx;
        _audio.volume = bounceSfxVolume;
        _audio.Play();
    }
}
