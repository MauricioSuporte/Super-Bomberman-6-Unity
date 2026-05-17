using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

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

    [Header("Stage Wrap")]
    [SerializeField] private Tilemap stageBoundsTilemap;

    private Rigidbody2D _rb;
    private Collider2D _col;
    private MovementController _move;
    private AbilitySystem _abilitySystem;
    private AudioSource _audio;
    private MountVisualController _mountVisual;
    private BattleSuddenDeathController _suddenDeathController;

    private Coroutine _resolveRoutine;
    private SpriteRenderer _bouncingLeftSpriteRenderer;
    private bool _bouncingLeftOriginalFlipX;
    private bool _usingMountBounceVisual;
    private bool _stageBoundsReady;
    private BoundsInt _stageCellBounds;

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
        pushDir = NormalizeCardinal(pushDir);
        if (pushDir == Vector2.zero)
            return;

        if (_resolveRoutine != null)
        {
            StopCoroutine(_resolveRoutine);
            ResetResolveState();
        }

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
            _resolveRoutine = null;
            yield break;
        }

        float tileSize = Mathf.Max(0.0001f, _move.tileSize);
        Vector2 pos = _rb.position;

        if (snapToGridBeforeResolve)
            pos = SnapToGrid(pos, tileSize);

        _rb.position = pos;
        transform.position = pos;

        if (IsOnHole(pos))
        {
            _move.KillByHole();
            _resolveRoutine = null;
            yield break;
        }

        if (IsActiveSuddenDeathIndestructibleAt(pos))
        {
            _resolveRoutine = null;
            yield break;
        }

        if (!IsBlockedAtPosition(pos, pushDir))
        {
            _resolveRoutine = null;
            yield break;
        }

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
                ClearActiveBounceVisual(Vector2.zero);
                _col.enabled = prevColliderEnabled;
                _move.KillByHole();
                _resolveRoutine = null;
                yield break;
            }

            if (IsActiveSuddenDeathIndestructibleAt(cur))
            {
                break;
            }

            if (!IsBlockedAtPosition(cur, pushDir))
            {
                break;
            }

            PlayBounceSfx();

            if (!TryStepWithWrap(cur, pushDir, tileSize, out Vector2 next, out bool didWrap))
            {
                break;
            }

            if (didWrap)
            {
                TeleportWrappedBounce(next, pushDir);
                cur = next;
                continue;
            }

            if (IsOnHole(next))
            {
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
    }

    private void TeleportWrappedBounce(Vector2 position, Vector2 direction)
    {
        SetActiveBounceVisual(direction);
        _rb.position = position;
        transform.position = position;
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

        bool canPassBombs =
            _abilitySystem != null &&
            _abilitySystem.IsEnabled(BombPassAbility.AbilityId);

        foreach (var hit in hits)
        {
            if (hit == null) continue;
            if (hit.gameObject == gameObject) continue;

            if (IsBombCollider(hit, out Bomb bomb))
            {
                if (bomb != null && bomb.HasExploded)
                    continue;

                if (canPassBombs)
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

    private bool IsActiveSuddenDeathIndestructibleAt(Vector2 worldPosition)
    {
        BattleSuddenDeathController controller = GetSuddenDeathController();
        return controller != null && controller.IsActiveSuddenDeathWorldPosition(worldPosition);
    }

    private BattleSuddenDeathController GetSuddenDeathController()
    {
        if (_suddenDeathController != null && _suddenDeathController.isActiveAndEnabled)
            return _suddenDeathController;

        _suddenDeathController = FindAnyObjectByType<BattleSuddenDeathController>();
        return _suddenDeathController;
    }

    private void EnsureStageBounds()
    {
        if (_stageBoundsReady && stageBoundsTilemap != null)
            return;

        _stageBoundsReady = false;

        if (stageBoundsTilemap != null)
        {
            stageBoundsTilemap.CompressBounds();
            _stageCellBounds = stageBoundsTilemap.cellBounds;
            _stageBoundsReady = true;
            return;
        }

        var tilemaps = FindObjectsByType<Tilemap>();
        Tilemap ground = null;
        Tilemap indestructible = null;

        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tm = tilemaps[i];
            if (tm == null)
                continue;

            string n = tm.name.ToLowerInvariant();
            if (ground == null && n.Contains("ground"))
                ground = tm;

            if (indestructible == null && n.Contains("indestruct"))
                indestructible = tm;
        }

        if (ground != null)
            ground.CompressBounds();

        if (indestructible != null)
            indestructible.CompressBounds();

        if (ground != null && indestructible != null)
        {
            BoundsInt a = ground.cellBounds;
            BoundsInt b = indestructible.cellBounds;

            int xMin = Mathf.Min(a.xMin, b.xMin);
            int xMax = Mathf.Max(a.xMax, b.xMax);
            int yMin = Mathf.Min(a.yMin, b.yMin);
            int yMax = Mathf.Max(a.yMax, b.yMax);

            _stageCellBounds = new BoundsInt(xMin, yMin, 0, xMax - xMin, yMax - yMin, 1);
            stageBoundsTilemap = ground;
            _stageBoundsReady = true;
            return;
        }

        Tilemap fallback = ground != null ? ground : indestructible;
        if (fallback == null)
            return;

        _stageCellBounds = fallback.cellBounds;
        stageBoundsTilemap = fallback;
        _stageBoundsReady = true;
    }

    private bool TryStepWithWrap(Vector2 from, Vector2 direction, float tileSize, out Vector2 next, out bool didWrap)
    {
        EnsureStageBounds();

        direction = NormalizeCardinal(direction);
        if (direction == Vector2.zero)
        {
            next = from;
            didWrap = false;
            return false;
        }

        Vector2 raw = from + direction * tileSize;
        didWrap = false;

        if (!_stageBoundsReady)
        {
            next = raw;
            return true;
        }

        Vector3Int cell = stageBoundsTilemap != null
            ? stageBoundsTilemap.WorldToCell(raw)
            : new Vector3Int(Mathf.RoundToInt(raw.x / tileSize), Mathf.RoundToInt(raw.y / tileSize), 0);

        int minX = _stageCellBounds.xMin;
        int maxX = _stageCellBounds.xMax - 1;
        int minY = _stageCellBounds.yMin;
        int maxY = _stageCellBounds.yMax - 1;

        if (cell.x < minX)
        {
            cell.x = maxX;
            didWrap = true;
        }
        else if (cell.x > maxX)
        {
            cell.x = minX;
            didWrap = true;
        }

        if (cell.y < minY)
        {
            cell.y = maxY;
            didWrap = true;
        }
        else if (cell.y > maxY)
        {
            cell.y = minY;
            didWrap = true;
        }

        if (!didWrap)
        {
            next = raw;
            return true;
        }

        if (stageBoundsTilemap != null)
        {
            Vector3 center = stageBoundsTilemap.GetCellCenterWorld(cell);
            center.z = transform.position.z;
            next = (Vector2)center;
        }
        else
        {
            next = new Vector2(cell.x * tileSize, cell.y * tileSize);
        }

        return true;
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

    private static string FormatVec(Vector2 value)
    {
        return $"({value.x:F2},{value.y:F2})";
    }
}
