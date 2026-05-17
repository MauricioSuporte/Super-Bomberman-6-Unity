using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(AudioSource))]
public sealed class PlayerPushedOutOfInvalidTile : MonoBehaviour
{
    [Header("Bounce Sprites")]
    [SerializeField] private AnimatedSpriteRenderer bouncingUp;
    [SerializeField] private AnimatedSpriteRenderer bouncingDown;
    [SerializeField] private AnimatedSpriteRenderer bouncingLeft;

    [Header("Hole Detection")]
    [SerializeField] private LayerMask holeLayerMask;
    [SerializeField] private string holeTag = "Hole";

    [Header("Resolve When Stuck")]
    [SerializeField, Min(0.1f)] private float resolveTilesPerSecond = 12f;
    [SerializeField, Min(0f)] private float bounceArcHeightTiles = 0.35f;
    [SerializeField, Min(1)] private int maxResolveSteps = 80;
    [SerializeField] private bool disablePlayerColliderWhileResolving = true;
    [SerializeField] private bool snapToGridBeforeResolve = true;
    [SerializeField] private bool lockInputWhileResolving = true;

    [Header("Bounce SFX")]
    [SerializeField] private AudioClip bounceSfx;
    [SerializeField, Range(0f, 1f)] private float bounceSfxVolume = 1f;

    [Header("Debug")]
    [SerializeField] private bool debugBounceLogs = true;

    private Rigidbody2D _rb;
    private Collider2D _col;
    private MovementController _move;
    private AbilitySystem _abilitySystem;
    private AudioSource _audio;
    private MountVisualController _mountVisual;

    private Coroutine _resolveRoutine;
    private SpriteRenderer _bouncingLeftSpriteRenderer;
    private bool _bouncingLeftOriginalFlipX;
    private bool _usingMountBounceVisual;

    private static readonly WaitForFixedUpdate _waitFixed = new();

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();
        _move = GetComponent<MovementController>();
        _abilitySystem = GetComponent<AbilitySystem>();
        _audio = GetComponent<AudioSource>();
        _mountVisual = GetComponentInChildren<MountVisualController>(true);

        if (holeLayerMask.value == 0)
            holeLayerMask = LayerMask.GetMask("Stage");

        if (bouncingLeft != null)
        {
            _bouncingLeftSpriteRenderer = bouncingLeft.GetComponent<SpriteRenderer>();
            if (_bouncingLeftSpriteRenderer != null)
                _bouncingLeftOriginalFlipX = _bouncingLeftSpriteRenderer.flipX;
        }

        ClearActiveBounceVisual(Vector2.zero);
    }

    public void NotifyExternalPushed(Vector2 pushDir)
    {
        Vector2 originalDir = pushDir;
        pushDir = NormalizeCardinal(pushDir);
        if (pushDir == Vector2.zero)
        {
            LogBounce($"NotifyExternalPushed ignored invalid dir original:{FormatVec(originalDir)}");
            return;
        }

        if (_resolveRoutine != null)
        {
            LogBounce($"NotifyExternalPushed restarting active bounce dir:{FormatVec(pushDir)}");
            StopCoroutine(_resolveRoutine);
            ResetResolveState();
        }

        LogBounce($"NotifyExternalPushed start dir:{FormatVec(pushDir)} rb:{FormatVec(_rb != null ? _rb.position : (Vector2)transform.position)} transform:{FormatVec(transform.position)}");
        _resolveRoutine = StartCoroutine(ResolveIfStuckRoutine(pushDir));
    }

    private void OnDisable()
    {
        ResetResolveState();
    }

    private void ResetResolveState()
    {
        _resolveRoutine = null;

        ClearActiveBounceVisual(Vector2.zero);

        if (_col != null)
            _col.enabled = true;

        if (_move != null && !_move.isDead)
        {
            _move.EnableExclusiveFromState();
            _move.SetInputLocked(false, true);
        }
    }

    private IEnumerator ResolveIfStuckRoutine(Vector2 pushDir)
    {
        yield return null;

        if (_move.isDead)
        {
            LogBounce("Resolve aborted: player is dead");
            _resolveRoutine = null;
            yield break;
        }

        float tileSize = Mathf.Max(0.0001f, _move.tileSize);
        Vector2 pos = _rb.position;
        Vector2 beforeSnap = pos;

        if (snapToGridBeforeResolve)
            pos = SnapToGrid(pos, tileSize);

        _rb.position = pos;
        transform.position = pos;

        LogBounce($"Resolve check start beforeSnap:{FormatVec(beforeSnap)} snapped:{FormatVec(pos)} dir:{FormatVec(pushDir)} tileSize:{tileSize:F3} obstacleMask:{_move.obstacleMask.value}");

        if (IsOnHole(pos))
        {
            LogBounce($"Resolve landing is hole at:{FormatVec(pos)} -> KillByHole");
            _move.KillByHole();
            _resolveRoutine = null;
            yield break;
        }

        if (!IsBlockedAtPosition(pos, pushDir, out string initialBlockReason))
        {
            LogBounce($"Resolve no bounce: initial tile safe at:{FormatVec(pos)}");
            _resolveRoutine = null;
            yield break;
        }

        LogBounce($"Resolve bounce required at:{FormatVec(pos)} reason:{initialBlockReason}");

        bool prevColliderEnabled = _col.enabled;

        if (lockInputWhileResolving)
            _move.SetInputLocked(true, true);

        _move.ForceFacingDirection(pushDir);

        if (disablePlayerColliderWhileResolving)
            _col.enabled = false;

        _move.SetAllSpritesVisible(false);

        float speedTilesPerSec = Mathf.Max(0.1f, resolveTilesPerSecond);
        float travelTime = 1f / speedTilesPerSec;

        Vector2 cur = pos;

        for (int step = 0; step < maxResolveSteps; step++)
        {
            if (IsOnHole(cur))
            {
                LogBounce($"Resolve hit hole during step:{step} at:{FormatVec(cur)} -> KillByHole");
                ClearActiveBounceVisual(Vector2.zero);
                _col.enabled = prevColliderEnabled;
                _move.KillByHole();
                _resolveRoutine = null;
                yield break;
            }

            if (!IsBlockedAtPosition(cur, pushDir, out string blockReason))
            {
                LogBounce($"Resolve found safe tile at step:{step} pos:{FormatVec(cur)}");
                break;
            }

            PlayBounceSfx();

            Vector2 next = cur + pushDir * tileSize;
            LogBounce($"Resolve step:{step + 1} from:{FormatVec(cur)} to:{FormatVec(next)} reason:{blockReason}");

            if (IsOnHole(next))
            {
                LogBounce($"Resolve next tile is hole at:{FormatVec(next)}; playing final hop then KillByHole");
                yield return MoveOneTile(cur, next, travelTime, pushDir, tileSize);
                ClearActiveBounceVisual(pushDir);
                _col.enabled = prevColliderEnabled;
                _move.KillByHole();
                _resolveRoutine = null;
                yield break;
            }

            yield return MoveOneTile(cur, next, travelTime, pushDir, tileSize);
            cur = next;
        }

        cur = SnapToGrid(cur, tileSize);
        _rb.position = cur;
        transform.position = cur;

        ClearActiveBounceVisual(pushDir);
        _col.enabled = prevColliderEnabled;
        _move.EnableExclusiveFromState();

        if (lockInputWhileResolving && !_move.isDead)
            _move.SetInputLocked(false, true);

        LogBounce($"Resolve finished final:{FormatVec(cur)}");
        _resolveRoutine = null;
    }

    private IEnumerator MoveOneTile(Vector2 start, Vector2 end, float travelTime, Vector2 direction, float tileSize)
    {
        float t = 0f;
        float arcHeight = Mathf.Max(0f, bounceArcHeightTiles) * Mathf.Max(0.0001f, tileSize);
        SetActiveBounceVisual(direction);

        while (t < travelTime)
        {
            t += Time.fixedDeltaTime;
            float a = Mathf.Clamp01(t / travelTime);

            Vector2 p = Vector2.Lerp(start, end, a);
            p.y += 4f * arcHeight * a * (1f - a);

            _rb.MovePosition(p);
            transform.position = p;

            yield return _waitFixed;
        }

        _rb.position = end;
        transform.position = end;
        LogBounce($"Resolve landed tile:{FormatVec(end)} renderer:{GetActiveBounceVisualName(direction)}");
    }

    private void SetActiveBounceVisual(Vector2 direction)
    {
        if (TryUseMountedBounceVisual(direction))
            return;

        SetMountBounceRenderer(false, direction);
        SetBounceRenderer(PickBounceRenderer(direction), direction);
    }

    private void ClearActiveBounceVisual(Vector2 direction)
    {
        SetBounceRenderer(null, Vector2.zero);
        SetMountBounceRenderer(false, direction);
    }

    private bool TryUseMountedBounceVisual(Vector2 direction)
    {
        if (_move == null || !_move.IsMounted)
            return false;

        MountVisualController mountVisual = GetMountVisual();
        if (mountVisual == null)
            return false;

        SetBounceRenderer(null, Vector2.zero);

        if (mountVisual.HasJumpDescendVisuals())
            SetMountBounceRenderer(true, direction);
        else
            SetMountBounceRenderer(false, direction);

        _move.ApplyMountedBounceVisual(direction, mountVisual.useHeadOnlyPlayerVisual);
        return true;
    }

    private MountVisualController GetMountVisual()
    {
        if (_mountVisual != null && _mountVisual.isActiveAndEnabled)
            return _mountVisual;

        _mountVisual = GetComponentInChildren<MountVisualController>(true);
        return _mountVisual;
    }

    private void SetMountBounceRenderer(bool active, Vector2 direction)
    {
        MountVisualController mountVisual = GetMountVisual();
        if (mountVisual == null)
        {
            _usingMountBounceVisual = false;
            return;
        }

        if (active)
        {
            _usingMountBounceVisual = true;
            mountVisual.SetJumpVisual(true, direction, descending: true);
            return;
        }

        if (_usingMountBounceVisual)
        {
            mountVisual.SetJumpVisual(false, direction, descending: true);
            _move.ClearMountedBounceVisual();
        }

        _usingMountBounceVisual = false;
    }

    private string GetActiveBounceVisualName(Vector2 direction)
    {
        if (_usingMountBounceVisual)
            return "MountVisualController.JumpDescend";

        AnimatedSpriteRenderer renderer = PickBounceRenderer(direction);
        return renderer != null ? renderer.name : "null";
    }

    private AnimatedSpriteRenderer PickBounceRenderer(Vector2 dir)
    {
        dir = NormalizeCardinal(dir);

        if (dir == Vector2.up)
            return bouncingUp;

        if (dir == Vector2.down)
            return bouncingDown;

        return bouncingLeft;
    }

    private void SetBounceRenderer(AnimatedSpriteRenderer active, Vector2 dir)
    {
        SetAnimEnabled(bouncingUp, active == bouncingUp);
        SetAnimEnabled(bouncingDown, active == bouncingDown);
        SetAnimEnabled(bouncingLeft, active == bouncingLeft);

        if (_bouncingLeftSpriteRenderer != null)
            _bouncingLeftSpriteRenderer.flipX = dir == Vector2.right
                ? !_bouncingLeftOriginalFlipX
                : _bouncingLeftOriginalFlipX;

        if (active != null)
        {
            active.idle = false;
            active.loop = true;
            active.RestartAnimation();
            active.RefreshFrame();
        }
    }

    private static void SetAnimEnabled(AnimatedSpriteRenderer renderer, bool enabled)
    {
        if (renderer == null)
            return;

        renderer.enabled = enabled;

        if (renderer.TryGetComponent<SpriteRenderer>(out var sr))
            sr.enabled = enabled;
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
        return IsBlockedAtPosition(targetPosition, dirForSize, out _);
    }

    private bool IsBlockedAtPosition(Vector2 targetPosition, Vector2 dirForSize, out string reason)
    {
        reason = "none";
        float tileSize = Mathf.Max(0.0001f, _move.tileSize);

        Vector2 size =
            Mathf.Abs(dirForSize.x) > 0f
                ? new Vector2(tileSize * 0.6f, tileSize * 0.2f)
                : new Vector2(tileSize * 0.2f, tileSize * 0.6f);

        LayerMask blockMask = _move.obstacleMask | LayerMask.GetMask("Bomb");
        Collider2D[] hits = Physics2D.OverlapBoxAll(targetPosition, size, 0f, blockMask);
        if (hits == null || hits.Length == 0)
            return false;

        bool canPassDestructibles =
            _abilitySystem != null &&
            _abilitySystem.IsEnabled(DestructiblePassAbility.AbilityId);

        foreach (var hit in hits)
        {
            if (hit == null) continue;
            if (hit.gameObject == gameObject) continue;

            if (IsBombCollider(hit, out Bomb bomb))
            {
                if (bomb != null && bomb.HasExploded)
                    continue;

                reason = $"bomb:{hit.name} trigger:{hit.isTrigger} layer:{LayerMask.LayerToName(hit.gameObject.layer)} pos:{FormatVec(hit.transform.position)}";
                return true;
            }

            if (hit.isTrigger) continue;

            if (canPassDestructibles && hit.CompareTag("Destructibles"))
                continue;

            reason = $"hit:{hit.name} tag:{hit.tag} layer:{LayerMask.LayerToName(hit.gameObject.layer)} pos:{FormatVec(hit.transform.position)}";
            return true;
        }

        return false;
    }

    private static bool IsBombCollider(Collider2D hit, out Bomb bomb)
    {
        bomb = null;

        if (hit == null)
            return false;

        if (hit.gameObject.layer == LayerMask.NameToLayer("Bomb"))
        {
            bomb =
                hit.GetComponent<Bomb>() ??
                hit.GetComponentInParent<Bomb>() ??
                hit.GetComponentInChildren<Bomb>();

            return true;
        }

        bomb =
            hit.GetComponent<Bomb>() ??
            hit.GetComponentInParent<Bomb>() ??
            hit.GetComponentInChildren<Bomb>();

        return bomb != null;
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

    private void LogBounce(string message)
    {
        if (!debugBounceLogs)
            return;

        string player = _move != null ? $"P{_move.PlayerId}" : name;
        Debug.Log($"[PlayerBounce] {player} {message}", this);
    }

    private static string FormatVec(Vector2 value)
    {
        return $"({value.x:F2},{value.y:F2})";
    }
}
