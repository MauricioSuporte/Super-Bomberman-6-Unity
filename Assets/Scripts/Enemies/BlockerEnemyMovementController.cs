using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class BlockerEnemyMovementController : EnemyMovementController
{
    [Header("Single Movement Sprite (all directions)")]
    [SerializeField] private AnimatedSpriteRenderer moveSprite;

    [Header("Turn Pause + Drop")]
    [SerializeField, Min(0.01f)] private float turnPauseSeconds = 0.25f;

    [Header("Drop As AnimatedTile")]
    [SerializeField] private Tilemap destructiblesTilemap;
    [SerializeField] private AnimatedTile destructibleTile;

    [Header("Block Drop On Indestructibles")]
    [SerializeField] private Tilemap indestructiblesTilemap;

    private bool _turnPauseActive;
    private float _turnPauseTimer;
    private Vector2 _pendingDir;

    private readonly Dictionary<Vector3Int, Coroutine> _tileClearRoutines = new();

    protected override void Awake()
    {
        if (moveSprite != null)
        {
            spriteDown = moveSprite;
            spriteUp = null;
            spriteLeft = null;
        }

        base.Awake();

        if (moveSprite != null)
            activeSprite = moveSprite;
    }

    protected override void Start()
    {
        base.Start();
        TryAutoResolveDestructiblesTilemap();
        TryAutoResolveIndestructiblesTilemap();
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (TryGetComponent<StunReceiver>(out var stun) && stun != null && stun.IsStunned)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }

        if (isInDamagedLoop)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }

        if (isStuck)
        {
            base.FixedUpdate();
            return;
        }

        if (_turnPauseActive)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            if (activeSprite != null)
                activeSprite.idle = false;

            _turnPauseTimer -= Time.fixedDeltaTime;

            if (_turnPauseTimer <= 0f)
            {
                _turnPauseActive = false;

                direction = _pendingDir;
                UpdateSpriteDirection(direction);
                targetTile = rb.position + direction * tileSize;
            }

            return;
        }

        if (HasBombAt(targetTile))
            HandleBombAhead();

        MoveTowardsTile();

        if (ReachedTile())
        {
            SnapToGrid();
            DecideNextTile();
        }
    }

    protected override void DecideNextTile()
    {
        if (_turnPauseActive)
        {
            targetTile = rb.position;
            if (activeSprite != null) activeSprite.idle = false;
            return;
        }

        isStuck = false;

        Vector2 forwardTile = rb.position + direction * tileSize;
        if (!IsTileBlocked(forwardTile))
        {
            targetTile = forwardTile;
            return;
        }

        var freeDirs = new List<Vector2>(4);

        foreach (var dir in Dirs)
        {
            if (dir == direction)
                continue;

            Vector2 checkTile = rb.position + dir * tileSize;
            if (!IsTileBlocked(checkTile))
                freeDirs.Add(dir);
        }

        if (freeDirs.Count == 0)
        {
            if (TryPickAnyFreeDirection(out var anyDir))
            {
                if (anyDir != direction)
                {
                    BeginTurnPause(anyDir);
                    return;
                }

                direction = anyDir;
                UpdateSpriteDirection(direction);
                targetTile = rb.position + direction * tileSize;
                return;
            }

            targetTile = rb.position;

            if (activeSprite != null)
                activeSprite.enabled = true;

            isStuck = true;
            stuckTimer = recheckStuckEverySeconds;
            return;
        }

        Vector2 chosen = freeDirs[Random.Range(0, freeDirs.Count)];

        if (chosen != direction)
        {
            BeginTurnPause(chosen);
            return;
        }

        direction = chosen;
        UpdateSpriteDirection(direction);
        targetTile = rb.position + direction * tileSize;
    }

    private void BeginTurnPause(Vector2 newDir)
    {
        if (_turnPauseActive || isDead || isInDamagedLoop)
            return;

        SnapToGrid();

        DropDestructibleTileAt(rb.position);

        _turnPauseActive = true;
        _turnPauseTimer = Mathf.Max(0.01f, turnPauseSeconds);
        _pendingDir = newDir;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        targetTile = rb.position;

        if (activeSprite != null)
            activeSprite.idle = false;
    }

    private void DropDestructibleTileAt(Vector2 worldPos)
    {
        if (destructiblesTilemap == null || destructibleTile == null)
            return;

        Vector3Int cellOnDestructibles = destructiblesTilemap.WorldToCell(worldPos);

        // NÃO coloca se existir Indestructibles nessa mesma célula (checando no tilemap de Indestructibles)
        if (indestructiblesTilemap != null)
        {
            Vector3Int cellOnIndestructibles = indestructiblesTilemap.WorldToCell(worldPos);
            if (indestructiblesTilemap.GetTile(cellOnIndestructibles) != null)
                return;
        }

        // Opcional: não sobrescrever algo já existente no Destructibles
        if (destructiblesTilemap.GetTile(cellOnDestructibles) != null)
            return;

        destructiblesTilemap.SetTile(cellOnDestructibles, destructibleTile);
    }

    private void TryAutoResolveDestructiblesTilemap()
    {
        if (destructiblesTilemap != null)
            return;

        var tms = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        if (tms == null)
            return;

        for (int i = 0; i < tms.Length; i++)
        {
            var tm = tms[i];
            if (tm == null) continue;

            if (tm.name == "Destructibles" || tm.gameObject.name == "Destructibles")
            {
                destructiblesTilemap = tm;
                return;
            }
        }
    }

    private void TryAutoResolveIndestructiblesTilemap()
    {
        if (indestructiblesTilemap != null)
            return;

        var tms = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        if (tms == null)
            return;

        for (int i = 0; i < tms.Length; i++)
        {
            var tm = tms[i];
            if (tm == null) continue;

            if (tm.name == "Indestructibles" || tm.gameObject.name == "Indestructibles")
            {
                indestructiblesTilemap = tm;
                return;
            }
        }
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (isInDamagedLoop)
            return;

        if (moveSprite != null)
            activeSprite = moveSprite;
        else if (activeSprite == null)
            activeSprite = spriteDown;

        if (activeSprite == null)
            return;

        activeSprite.enabled = true;
        activeSprite.idle = false;

        if (activeSprite.TryGetComponent<SpriteRenderer>(out var sr))
            sr.flipX = (dir == Vector2.right);
    }

    protected override void OnDestroy()
    {
        foreach (var kv in _tileClearRoutines)
        {
            if (kv.Value != null)
                StopCoroutine(kv.Value);
        }
        _tileClearRoutines.Clear();

        base.OnDestroy();
    }
}