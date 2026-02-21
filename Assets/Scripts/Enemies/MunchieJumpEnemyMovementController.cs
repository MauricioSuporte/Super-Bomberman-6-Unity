using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(StunReceiver))]
public sealed class MunchieJumpEnemyMovementController : JunctionTurningEnemyMovementController
{
    [Header("Vision (Bombs)")]
    [SerializeField, Min(0.1f)] private float visionDistance = 10f;

    [Header("Vision Alignment")]
    [SerializeField, Min(0.001f)] private float alignedToleranceTiles = 0.15f;
    [SerializeField, Range(0.1f, 1f)] private float scanBoxSizePercent = 0.6f;

    private CharacterHealth _health;

    protected override void Awake()
    {
        base.Awake();

        _health = GetComponent<CharacterHealth>();

        if (bombLayerMask.value == 0)
            bombLayerMask = LayerMask.GetMask("Bomb");

        RemoveLayerFromObstacleMask("Bomb");
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (TryGetComponent<StunReceiver>(out var stun) && stun != null && stun.IsStunned)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isInDamagedLoop)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isStuck)
        {
            base.FixedUpdate();
            return;
        }

        if (TryGetBombDirection(out var dirToBomb))
        {
            if (dirToBomb != Vector2.zero && dirToBomb != direction)
            {
                SnapToGrid();
                direction = dirToBomb;
                UpdateSpriteDirection(direction);
                targetTile = rb.position + direction * tileSize;
            }
        }

        MoveTowardsTile();

        if (ReachedTile())
        {
            SnapToGrid();

            if (TryGetBombDirection(out var dirToBomb2))
            {
                direction = dirToBomb2 == Vector2.zero ? direction : dirToBomb2;
                UpdateSpriteDirection(direction);
                targetTile = rb.position + direction * tileSize;
            }
            else
            {
                DecideNextTile();
            }
        }
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead)
            return;

        int layer = other.gameObject.layer;

        if (layer == LayerMask.NameToLayer("Explosion"))
        {
            if (_health != null)
                _health.TakeDamage(1);
            return;
        }

        if (layer == LayerMask.NameToLayer("Bomb"))
        {
            DestroyBomb(other);
            return;
        }

        if (layer == LayerMask.NameToLayer("Enemy"))
        {
            var otherEnemy = other.GetComponent<EnemyMovementController>();
            HandleEnemyCollision(otherEnemy);
            return;
        }
    }

    private void DestroyBomb(Collider2D bombCol)
    {
        if (bombCol == null)
            return;

        GameObject bombGo =
            bombCol.attachedRigidbody != null ? bombCol.attachedRigidbody.gameObject : bombCol.gameObject;

        if (bombGo == null)
            return;

        Destroy(bombGo);
    }

    private bool TryGetBombDirection(out Vector2 dirToBomb)
    {
        dirToBomb = Vector2.zero;

        Vector2[] dirs =
        {
            Vector2.up,
            Vector2.down,
            Vector2.left,
            Vector2.right
        };

        int maxSteps = Mathf.Max(1, Mathf.FloorToInt(visionDistance / tileSize));

        float boxPercent = Mathf.Clamp(scanBoxSizePercent, 0.1f, 1f);
        Vector2 boxSize = Vector2.one * (tileSize * boxPercent);

        float alignedToleranceWorld = Mathf.Max(0.001f, alignedToleranceTiles * tileSize);

        Vector2 selfPos = rb.position;

        for (int i = 0; i < dirs.Length; i++)
        {
            var dir = dirs[i];
            bool verticalScan = dir == Vector2.up || dir == Vector2.down;

            for (int step = 1; step <= maxSteps; step++)
            {
                Vector2 tileCenter = selfPos + step * tileSize * dir;

                var hits = Physics2D.OverlapBoxAll(tileCenter, boxSize, 0f, bombLayerMask);

                if (hits != null && hits.Length > 0)
                {
                    for (int h = 0; h < hits.Length; h++)
                    {
                        var col = hits[h];
                        if (col == null)
                            continue;

                        Vector2 p = col.attachedRigidbody != null ? col.attachedRigidbody.position : (Vector2)col.transform.position;

                        bool aligned = verticalScan
                            ? Mathf.Abs(p.x - selfPos.x) <= alignedToleranceWorld
                            : Mathf.Abs(p.y - selfPos.y) <= alignedToleranceWorld;

                        if (!aligned)
                            continue;

                        dirToBomb = dir;
                        return true;
                    }
                }

                if (IsTileBlocked(tileCenter))
                    break;
            }
        }

        return false;
    }

    private void RemoveLayerFromObstacleMask(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
            return;

        int bit = 1 << layer;

        if ((obstacleMask.value & bit) != 0)
        {
            obstacleMask.value &= ~bit;
        }
        else if (obstacleMask.value == 0)
        {
            obstacleMask = LayerMask.GetMask("Stage", "Enemy");
        }
    }
}