using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Junction-turning Pygmin that periodically stops on a tile centre and
/// extends its chain toward the nearest player. The ball is always one tile
/// ahead of the last chain link and the whole effect retracts in reverse.
/// </summary>
public sealed class PygminMovementController : JunctionTurningEnemyMovementController
{
    [Header("Chain Attack")]
    [SerializeField, Min(0.01f)] private float minimumAttackInterval = 5f;
    [SerializeField, Min(0.01f)] private float maximumAttackInterval = 9f;
    [SerializeField, Min(0.01f)] private float chainStepSeconds = 0.2f;
    [SerializeField, Range(1, 3)] private int maximumChainLength = 3;
    [SerializeField] private LayerMask playerLayerMask;

    private readonly Dictionary<Vector2, AnimatedSpriteRenderer> chainTemplates = new();
    private readonly List<GameObject> spawnedChainLinks = new();

    private AnimatedSpriteRenderer ballTemplate;
    private AnimatedSpriteRenderer attackUp;
    private AnimatedSpriteRenderer attackDown;
    private AnimatedSpriteRenderer attackLeft;
    private GameObject spawnedBall;
    private Coroutine attackRoutine;
    private float nextAttackAt;
    private bool attacking;

    protected override void Awake()
    {
        base.Awake();
        CacheTemplate("ChainUp", Vector2.up);
        CacheTemplate("ChainDown", Vector2.down);
        CacheTemplate("ChainLeft", Vector2.left);
        CacheTemplate("ChainRigth", Vector2.right);
        CacheTemplate("ChainRight", Vector2.right);
        ballTemplate = FindAnimation("Ball");
        attackUp = FindAnimation("AttackinUp") ?? FindAnimation("AttackingUp");
        attackDown = FindAnimation("AttackinDown") ?? FindAnimation("AttackingDown");
        attackLeft = FindAnimation("AttackinLeft") ?? FindAnimation("AttackingLeft");
        SetTemplatesVisible(false);
        SetAttackVisualsVisible(false);
    }

    protected override void Start()
    {
        base.Start();
        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player", "Louie");

        ScheduleNextAttack();
    }

    protected override void FixedUpdate()
    {
        if (attacking)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }

        if (CanBeginAttack() && Time.time >= nextAttackAt && TryGetNearestPlayerPosition(out Vector2 playerPosition))
        {
            attackRoutine = StartCoroutine(ChainAttackRoutine(ToCardinal(playerPosition - rb.position)));
            return;
        }

        base.FixedUpdate();
    }

    protected override void Die()
    {
        StopAttack();
        base.Die();
    }

    protected override void OnDestroy()
    {
        StopAttack();
        base.OnDestroy();
    }

    private IEnumerator ChainAttackRoutine(Vector2 attackDirection)
    {
        attacking = true;
        // Complete the current move before snapping, so the attack never
        // shifts Pygmin across a wall or bomb to reach the grid centre.
        while (!isDead && !isInDamagedLoop && rb != null && !ReachedTile())
        {
            MoveTowardsTile();
            yield return new WaitForFixedUpdate();
        }

        if (isDead || isInDamagedLoop || rb == null)
        {
            StopAttack();
            yield break;
        }

        SnapToGrid();
        targetTile = rb.position;
        direction = attackDirection;
        BeginAttackVisuals(attackDirection);

        int deployedLinks = 0;
        int stopLength = Mathf.Clamp(maximumChainLength, 1, 3);
        while (!isDead && !isInDamagedLoop && deployedLinks < stopLength)
        {
            Vector2 ballTile = rb.position + attackDirection * tileSize * (deployedLinks + 1);
            if (IsBallBlocked(ballTile, out _))
            {
                break;
            }

            SpawnChainLink(rb.position + attackDirection * tileSize * deployedLinks, attackDirection);
            SpawnBall(ballTile);
            deployedLinks++;
            DamageTargetsAt(ballTile);
            yield return new WaitForSeconds(Mathf.Max(0.01f, chainStepSeconds));
        }

        while (!isDead && deployedLinks > 0)
        {
            DestroyBall();
            DestroyLastChainLink();
            deployedLinks--;
            if (deployedLinks > 0)
            {
                Vector2 returningBallTile = rb.position + attackDirection * tileSize * deployedLinks;
                SpawnBall(returningBallTile);
                DamageTargetsAt(returningBallTile);
            }

            yield return new WaitForSeconds(Mathf.Max(0.01f, chainStepSeconds));
        }

        if (!isDead)
            FinishAttack();
    }

    private bool CanBeginAttack()
    {
        return !isDead && !isInDamagedLoop && !GamePauseController.IsPaused &&
               !(TryGetComponent<StunReceiver>(out StunReceiver stun) && stun.IsStunned);
    }

    private bool IsBallBlocked(Vector2 tilePosition, out string reason)
    {
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb != null && !bomb.HasExploded &&
                Vector2.Distance(bomb.GetLogicalPosition(), tilePosition) < tileSize * 0.2f)
            {
                reason = "bomb";
                return true;
            }
        }

        GameManager manager = GameManager.Instance;
        if (manager != null)
        {
            if (HasTile(manager.destructibleTilemap, tilePosition))
            {
                reason = "destructible";
                return true;
            }

            if (HasTile(manager.indestructibleTilemap, tilePosition))
            {
                reason = "indestructible";
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }

    private static bool HasTile(Tilemap tilemap, Vector2 worldPosition)
    {
        return tilemap != null && tilemap.HasTile(tilemap.WorldToCell(worldPosition));
    }

    private void SpawnChainLink(Vector2 tilePosition, Vector2 attackDirection)
    {
        AnimatedSpriteRenderer template = GetChainTemplate(attackDirection);
        if (template == null)
        {
            return;
        }

        // ChainLeft and ChainRigth have authored offsets so their links join
        // correctly at the source tile. Keep those offsets and rotations for
        // every spawned link instead of replacing them with a generic flip.
        Vector2 visualPosition = tilePosition + (Vector2)template.transform.localPosition;
        GameObject link = Instantiate(template.gameObject, visualPosition, template.transform.rotation);
        link.name = "Pygmin Chain Link";
        link.SetActive(true);
        SetLayerRecursively(link, LayerMask.NameToLayer("Enemy"));
        EnsureEnemyDamageHitbox(link);
        SetVisualEnabled(link.GetComponent<AnimatedSpriteRenderer>(), true);

        spawnedChainLinks.Add(link);
        DamageTargetsAt(tilePosition);
    }

    private void SpawnBall(Vector2 tilePosition)
    {
        DestroyBall();
        if (ballTemplate == null)
        {
            return;
        }

        Vector2 visualPosition = tilePosition + (Vector2)ballTemplate.transform.localPosition;
        spawnedBall = Instantiate(ballTemplate.gameObject, visualPosition, ballTemplate.transform.rotation);
        spawnedBall.name = "Pygmin Chain Ball";
        spawnedBall.SetActive(true);
        SetLayerRecursively(spawnedBall, LayerMask.NameToLayer("Enemy"));
        EnsureEnemyDamageHitbox(spawnedBall);
        SetVisualEnabled(spawnedBall.GetComponent<AnimatedSpriteRenderer>(), true);
    }

    private void DamageTargetsAt(Vector2 tilePosition)
    {
        if (playerLayerMask.value == 0)
            return;

        float hitboxSize = tileSize * 0.7f;
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(tilePosition, Vector2.one * hitboxSize, 0f, playerLayerMask))
        {
            PlayerMountCompanion mount = hit.GetComponentInParent<PlayerMountCompanion>();
            if (mount != null)
                mount.OnMountedLouieHit(1, fromExplosion: false);
            else
                hit.GetComponentInParent<CharacterHealth>()?.TakeDamage(1);
        }
    }

    private bool TryGetNearestPlayerPosition(out Vector2 position)
    {
        position = default;
        if (rb == null)
            return false;

        float closestDistance = float.MaxValue;
        foreach (MovementController candidate in FindObjectsByType<MovementController>(FindObjectsInactive.Exclude))
        {
            if (candidate == null || !candidate.isActiveAndEnabled || candidate.isDead || !candidate.CompareTag("Player"))
                continue;

            float distance = ((Vector2)candidate.transform.position - rb.position).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                position = candidate.transform.position;
            }
        }

        return closestDistance < float.MaxValue;
    }

    private void BeginAttackVisuals(Vector2 attackDirection)
    {
        if (activeSprite != null)
            SetVisualEnabled(activeSprite, false);

        SetAttackVisualsVisible(false);
        AnimatedSpriteRenderer attackVisual = GetAttackVisual(attackDirection);
        if (attackVisual == null)
        {
            return;
        }

        SetVisualEnabled(attackVisual, true);
        attackVisual.loop = true;
        attackVisual.idle = false;
        attackVisual.RestartAnimation();
        if (attackVisual.TryGetComponent(out SpriteRenderer renderer))
            renderer.flipX = attackDirection == Vector2.right;
        activeSprite = attackVisual;
    }

    private void FinishAttack()
    {
        DestroyAttackVisuals();
        attacking = false;
        attackRoutine = null;
        UpdateSpriteDirection(direction);
        ScheduleNextAttack();
        DecideNextTile();
    }

    private void StopAttack()
    {
        if (attackRoutine != null)
            StopCoroutine(attackRoutine);

        attackRoutine = null;
        attacking = false;
        DestroyAttackVisuals();
    }

    private void DestroyAttackVisuals()
    {
        DestroyBall();
        while (spawnedChainLinks.Count > 0)
            DestroyLastChainLink();
        SetAttackVisualsVisible(false);
    }

    private void DestroyBall()
    {
        if (spawnedBall != null)
            Destroy(spawnedBall);
        spawnedBall = null;
    }

    private void DestroyLastChainLink()
    {
        int index = spawnedChainLinks.Count - 1;
        if (index < 0)
            return;

        if (spawnedChainLinks[index] != null)
            Destroy(spawnedChainLinks[index]);
        spawnedChainLinks.RemoveAt(index);
    }

    private void ScheduleNextAttack()
    {
        float minimum = Mathf.Max(0.01f, minimumAttackInterval);
        float maximum = Mathf.Max(minimum, maximumAttackInterval);
        nextAttackAt = Time.time + Random.Range(minimum, maximum);
    }

    private void CacheTemplate(string childName, Vector2 templateDirection)
    {
        AnimatedSpriteRenderer template = FindAnimation(childName);
        if (template != null)
            chainTemplates[templateDirection] = template;
    }

    private AnimatedSpriteRenderer FindAnimation(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<AnimatedSpriteRenderer>() : null;
    }

    private AnimatedSpriteRenderer GetChainTemplate(Vector2 attackDirection)
    {
        return chainTemplates.TryGetValue(attackDirection, out AnimatedSpriteRenderer template) ? template : null;
    }

    private AnimatedSpriteRenderer GetAttackVisual(Vector2 attackDirection)
    {
        if (attackDirection == Vector2.up) return attackUp;
        if (attackDirection == Vector2.down) return attackDown;
        return attackLeft;
    }

    private void SetTemplatesVisible(bool visible)
    {
        foreach (AnimatedSpriteRenderer template in chainTemplates.Values)
            SetVisualEnabled(template, visible);
        SetVisualEnabled(ballTemplate, visible);
    }

    private void SetAttackVisualsVisible(bool visible)
    {
        SetVisualEnabled(attackUp, visible);
        SetVisualEnabled(attackDown, visible);
        SetVisualEnabled(attackLeft, visible);
    }

    private static void SetVisualEnabled(AnimatedSpriteRenderer animation, bool visible)
    {
        if (animation == null)
            return;

        animation.enabled = visible;
        if (animation.TryGetComponent(out SpriteRenderer renderer))
            renderer.enabled = visible;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null || layer < 0)
            return;

        target.layer = layer;
        foreach (Transform child in target.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private void EnsureEnemyDamageHitbox(GameObject target)
    {
        if (target == null || target.TryGetComponent(out Collider2D _))
            return;

        BoxCollider2D hitbox = target.AddComponent<BoxCollider2D>();
        hitbox.isTrigger = true;
        hitbox.size = Vector2.one * (tileSize * 0.8f);
    }

    private static Vector2 ToCardinal(Vector2 value)
    {
        if (Mathf.Abs(value.x) >= Mathf.Abs(value.y))
            return value.x >= 0f ? Vector2.right : Vector2.left;
        return value.y >= 0f ? Vector2.up : Vector2.down;
    }

}
