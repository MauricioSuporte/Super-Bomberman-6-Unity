using System.Collections.Generic;
using UnityEngine;

public sealed class MagnetMovementController : EnemyMovementController
{
    [Header("Magnet Ability Sprite")]
    public AnimatedSpriteRenderer spriteAbility;

    [Header("Magnet Settings")]
    public float pullSpeed = 4f;
    public int visionTiles = 8;
    public LayerMask magnetBlockMask;
    public LayerMask playerLayerMask;

    private readonly Collider2D[] _playerHits = new Collider2D[8];
    private bool _isMagnetActive;

    private static readonly Vector2[] CardinalDirs =
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right
    };

    protected override void Awake()
    {
        base.Awake();

        if (magnetBlockMask.value == 0)
            magnetBlockMask = LayerMask.GetMask("Stage", "Bomb", "Enemy");

        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");
    }

    protected override void Die()
    {
        if (_isMagnetActive)
        {
            _isMagnetActive = false;

            if (spriteAbility != null)
            {
                spriteAbility.enabled = false;
                spriteAbility.idle = true;
                spriteAbility.loop = false;
            }
        }

        base.Die();
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (TryGetComponent<StunReceiver>(out var stun) && stun != null && stun.IsStunned)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            StopMagnetIfRunning();
            return;
        }

        if (TryMagnetAnyDirection(out var chosenDir, out var visiblePlayers))
        {
            direction = chosenDir;
            StartMagnetIfNeeded();
            PullPlayers(visiblePlayers, chosenDir);
            return;
        }

        StopMagnetIfRunning();

        if (HasBombAt(targetTile))
            HandleBombAhead();

        MoveTowardsTile();

        if (ReachedTile())
        {
            SnapToGrid();
            DecideNextTile();
        }
    }

    private bool TryMagnetAnyDirection(out Vector2 chosenDir, out List<Rigidbody2D> visiblePlayers)
    {
        chosenDir = default;
        visiblePlayers = null;

        for (int d = 0; d < CardinalDirs.Length; d++)
        {
            Vector2 dir = CardinalDirs[d];

            if (TryMagnetInDirection(dir, out var found))
            {
                chosenDir = dir;
                visiblePlayers = found;
                return true;
            }
        }

        return false;
    }

    private bool TryMagnetInDirection(Vector2 dir, out List<Rigidbody2D> visiblePlayers)
    {
        visiblePlayers = null;

        Vector2 origin = rb.position;
        origin.x = Mathf.Round(origin.x / tileSize) * tileSize;
        origin.y = Mathf.Round(origin.y / tileSize) * tileSize;

        var found = new List<Rigidbody2D>(4);

        int max = Mathf.Max(1, visionTiles);

        for (int i = 1; i <= max; i++)
        {
            Vector2 tileCenter = origin + dir * (tileSize * i);

            if (IsBlockedForMagnet(tileCenter))
                break;

            Collider2D[] hits = Physics2D.OverlapBoxAll(
                tileCenter,
                Vector2.one * (tileSize * 0.75f),
                0f,
                playerLayerMask
            );

            for (int h = 0; h < hits.Length; h++)
            {
                var col = hits[h];
                if (col == null)
                    continue;

                Rigidbody2D prb = col.attachedRigidbody != null ? col.attachedRigidbody : col.GetComponent<Rigidbody2D>();
                if (prb == null)
                    continue;

                bool already = false;
                for (int k = 0; k < found.Count; k++)
                {
                    if (found[k] == prb)
                    {
                        already = true;
                        break;
                    }
                }

                if (!already)
                    found.Add(prb);
            }
        }

        if (found.Count == 0)
            return false;

        visiblePlayers = found;
        return true;
    }

    private bool IsBlockedForMagnet(Vector2 tileCenter)
    {
        Collider2D hit = Physics2D.OverlapBox(
            tileCenter,
            Vector2.one * (tileSize * 0.8f),
            0f,
            magnetBlockMask
        );

        if (hit == null)
            return false;

        if (hit.gameObject == gameObject)
            return false;

        return true;
    }

    private void StartMagnetIfNeeded()
    {
        if (_isMagnetActive)
        {
            if (spriteAbility != null && spriteAbility.TryGetComponent<SpriteRenderer>(out var sr2))
                sr2.flipX = (direction == Vector2.right);

            return;
        }

        _isMagnetActive = true;

        SnapToGrid();
        targetTile = rb.position;
        rb.linearVelocity = Vector2.zero;

        if (spriteAbility != null)
        {
            DisableAllSprites();
            activeSprite = spriteAbility;

            spriteAbility.enabled = true;
            spriteAbility.loop = true;
            spriteAbility.idle = false;

            if (spriteAbility.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = (direction == Vector2.right);
        }
    }

    private void StopMagnetIfRunning()
    {
        if (!_isMagnetActive)
            return;

        _isMagnetActive = false;

        if (spriteAbility != null)
        {
            spriteAbility.enabled = false;
            spriteAbility.idle = true;
            spriteAbility.loop = false;
        }

        UpdateSpriteDirection(direction);
    }

    private void DisableAllSprites()
    {
        if (spriteUp != null) spriteUp.enabled = false;
        if (spriteDown != null) spriteDown.enabled = false;
        if (spriteLeft != null) spriteLeft.enabled = false;
        if (spriteDeath != null) spriteDeath.enabled = false;
        if (spriteAbility != null) spriteAbility.enabled = false;
    }

    private void PullPlayers(List<Rigidbody2D> players, Vector2 dir)
    {
        Vector2 magnetTile = rb.position;
        magnetTile.x = Mathf.Round(magnetTile.x / tileSize) * tileSize;
        magnetTile.y = Mathf.Round(magnetTile.y / tileSize) * tileSize;

        Vector2 stopTile = magnetTile;

        float step = pullSpeed * Time.fixedDeltaTime;

        for (int i = 0; i < players.Count; i++)
        {
            var prb = players[i];
            if (prb == null)
                continue;

            if ((prb.position - stopTile).sqrMagnitude <= 0.0004f)
                continue;

            if (IsPathBlockedBetween(prb.position, stopTile))
                continue;

            Vector2 next = Vector2.MoveTowards(prb.position, stopTile, step);
            prb.MovePosition(next);
        }
    }

    private bool IsPathBlockedBetween(Vector2 from, Vector2 to)
    {
        Vector2 dir = to - from;

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            dir = new Vector2(Mathf.Sign(dir.x), 0f);
        else
            dir = new Vector2(0f, Mathf.Sign(dir.y));

        Vector2 p = from;
        p.x = Mathf.Round(p.x / tileSize) * tileSize;
        p.y = Mathf.Round(p.y / tileSize) * tileSize;

        Vector2 end = to;
        end.x = Mathf.Round(end.x / tileSize) * tileSize;
        end.y = Mathf.Round(end.y / tileSize) * tileSize;

        int safety = 64;
        while ((p - end).sqrMagnitude > 0.001f && safety-- > 0)
        {
            p += dir * tileSize;

            if (IsBlockedForMagnet(p))
                return true;
        }

        return false;
    }
}
