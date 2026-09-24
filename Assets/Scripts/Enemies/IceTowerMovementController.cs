using System.Collections;
using UnityEngine;

/// <summary>
/// A junction-turning IceTower. It alternates between surface movement and an
/// underground movement phase, fires Icicle-style shots only while surfaced,
/// and splits into MiniIceTowers when it dies.
/// </summary>
public sealed class IceTowerMovementController : JunctionTurningEnemyMovementController
{
    private static readonly Vector2[] MiniIceTowerSpawnDirections =
    {
        Vector2.up, Vector2.right, Vector2.down, Vector2.left
    };

    private enum IceTowerState { Surface, EnteringSubmerge, Submerged, ExitingSubmerge, Waiting }

    [Header("IceTower Submerge")]
    [SerializeField, Min(0.1f)] private float surfaceDurationSeconds = 8f;
    [SerializeField, Min(0.1f)] private float submergedDurationSeconds = 8f;
    [SerializeField, Min(0.01f)] private float submergeFrameSeconds = 0.12f;
    [SerializeField, Range(0f, 1f)] private float waitAfterSubmergeChance = 0.35f;
    [SerializeField, Min(0.1f)] private float waitAfterSubmergeSeconds = 2f;

    [Header("Ice Shot")]
    [SerializeField, Min(0.1f)] private float shotCooldownSeconds = 2f;
    [SerializeField, Min(0.05f)] private float scanIntervalSeconds = 0.1f;
    [SerializeField, Min(1)] private int visionTiles = 8;
    [SerializeField, Range(0.1f, 1f)] private float scanBoxSizePercent = 0.7f;
    [SerializeField] private Sprite particleSpriteOne;
    [SerializeField] private Sprite particleSpriteTwo;

    [Header("Mini IceTower Spawn")]
    [SerializeField] private GameObject miniIceTowerPrefab;
    [SerializeField, Min(1)] private int miniIceTowerCount = 4;
    [SerializeField, Min(0f)] private float miniIceTowerSpawnInvulnerabilitySeconds = 2f;

    private AnimatedSpriteRenderer movementVisual;
    private AnimatedSpriteRenderer attackVisual;
    private AnimatedSpriteRenderer submergeMovementVisual;
    private AnimatedSpriteRenderer waitingVisual;
    private AnimatedSpriteRenderer submergingVisual;
    private AnimatedSpriteRenderer deathVisual;
    private Sprite[] movementFrames;
    private Sprite[] attackFrames;
    private Sprite[] submergeMovementFrames;
    private Sprite[] waitingFrames;
    private Sprite[] submergingFrames;
    private CharacterHealth iceTowerHealth;
    private Collider2D contactCollider;
    private IceTowerState state;
    private float stateTimer;
    private float nextScanTime;
    private float nextShotTime;
    private Vector2 deathTile;
    private bool hasDeathTile;
    private bool spawnedMiniIceTowers;

    protected override void Awake()
    {
        movementVisual = FindVisual("Movimentation");
        attackVisual = FindVisual("Attack");
        submergeMovementVisual = FindVisual("SubmergeMovimentation");
        waitingVisual = FindVisual("Waiting");
        submergingVisual = FindVisual("Submerging");
        deathVisual = FindVisual("Death");

        movementFrames = movementVisual != null ? movementVisual.animationSprite : null;
        attackFrames = attackVisual != null ? attackVisual.animationSprite : null;
        submergeMovementFrames = submergeMovementVisual != null ? submergeMovementVisual.animationSprite : null;
        waitingFrames = waitingVisual != null ? waitingVisual.animationSprite : null;
        submergingFrames = submergingVisual != null ? submergingVisual.animationSprite : null;
        spriteUp = movementVisual;
        spriteDown = movementVisual;
        spriteLeft = movementVisual;
        spriteRight = movementVisual;
        // IceTower has no authored death animation. Keep the child excluded
        // from regular visuals, but do not let the base controller display it
        // or delay the split.
        spriteDeath = null;
        waitForFullDeathAnimation = false;
        iceTowerHealth = GetComponent<CharacterHealth>();
        contactCollider = GetComponent<Collider2D>();
        base.Awake();
    }

    protected override void Start()
    {
        base.Start();
        state = IceTowerState.Surface;
        stateTimer = surfaceDurationSeconds;
        SetLoopingVisual(movementVisual, movementFrames);
    }

    private void Update()
    {
        if (isDead || state == IceTowerState.EnteringSubmerge || state == IceTowerState.Submerged || state == IceTowerState.ExitingSubmerge)
            return;

        if (Time.time < nextScanTime || Time.time < nextShotTime)
            return;

        nextScanTime = Time.time + scanIntervalSeconds;
        if (!TryGetPlayerDirection(out Vector2 shotDirection))
            return;

        Vector2 spawnPosition = (Vector2)transform.position + shotDirection * tileSize;
        IcicleIceProjectile.Create(
            spawnPosition,
            shotDirection,
            gameObject,
            attackFrames != null && attackFrames.Length > 0 ? attackFrames[0] : null,
            particleSpriteOne,
            particleSpriteTwo,
            destroysBombs: false,
            projectileAnimationFrames: attackFrames,
            projectileAnimationTime: attackVisual != null ? attackVisual.animationTime : 0.1f);
        nextShotTime = Time.time + shotCooldownSeconds;
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (state == IceTowerState.Surface || state == IceTowerState.Submerged)
        {
            base.FixedUpdate();
            stateTimer -= Time.fixedDeltaTime;
            if (stateTimer <= 0f)
            {
                if (state == IceTowerState.Surface)
                    StartCoroutine(EnterSubmergeRoutine());
                else
                    StartCoroutine(ExitSubmergeRoutine());
            }

            return;
        }

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (state == IceTowerState.Waiting)
        {
            stateTimer -= Time.fixedDeltaTime;
            if (stateTimer <= 0f)
            {
                state = IceTowerState.Surface;
                stateTimer = surfaceDurationSeconds;
                SetLoopingVisual(movementVisual, movementFrames);
                UpdateSpriteDirection(direction);
                DecideNextTile();
            }
        }
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (state == IceTowerState.EnteringSubmerge || state == IceTowerState.ExitingSubmerge || state == IceTowerState.Waiting)
            return;

        if (state == IceTowerState.Submerged)
        {
            SetLoopingVisual(submergeMovementVisual, submergeMovementFrames);
            return;
        }

        SetLoopingVisual(movementVisual, movementFrames);
    }

    protected override void Die()
    {
        if (isDead)
            return;

        deathTile = rb != null ? rb.position : (Vector2)transform.position;
        deathTile.x = Mathf.Round(deathTile.x / tileSize) * tileSize;
        deathTile.y = Mathf.Round(deathTile.y / tileSize) * tileSize;
        hasDeathTile = true;
        iceTowerHealth?.SetExternalInvulnerability(false);
        SpawnMiniIceTowers();
        base.Die();
    }

    protected override float GetDeathAnimationDuration() => 0f;

    private IEnumerator EnterSubmergeRoutine()
    {
        state = IceTowerState.EnteringSubmerge;
        SnapToGrid();
        yield return PlaySubmerging(forward: true);

        if (isDead)
            yield break;

        iceTowerHealth?.SetExternalInvulnerability(true);
        SetContactDamageEnabled(false);
        state = IceTowerState.Submerged;
        stateTimer = submergedDurationSeconds;
        SetLoopingVisual(submergeMovementVisual, submergeMovementFrames);
        UpdateSpriteDirection(direction);
    }

    private IEnumerator ExitSubmergeRoutine()
    {
        state = IceTowerState.ExitingSubmerge;
        SnapToGrid();
        yield return PlaySubmerging(forward: false);

        if (isDead)
            yield break;

        iceTowerHealth?.SetExternalInvulnerability(false);
        SetContactDamageEnabled(true);
        if (Random.value < waitAfterSubmergeChance)
        {
            state = IceTowerState.Waiting;
            stateTimer = waitAfterSubmergeSeconds;
            SetLoopingVisual(waitingVisual, waitingFrames);
            yield break;
        }

        state = IceTowerState.Surface;
        stateTimer = surfaceDurationSeconds;
        SetLoopingVisual(movementVisual, movementFrames);
        UpdateSpriteDirection(direction);
        DecideNextTile();
    }

    private IEnumerator PlaySubmerging(bool forward)
    {
        if (submergingVisual == null || submergingFrames == null || submergingFrames.Length == 0)
            yield break;

        ShowOnly(submergingVisual);
        submergingVisual.animationSprite = submergingFrames;
        submergingVisual.SetManualAnimationUpdate(true);
        submergingVisual.idle = false;
        submergingVisual.loop = false;

        int index = forward ? 0 : submergingFrames.Length - 1;
        int finalIndex = forward ? submergingFrames.Length - 1 : 0;
        int step = forward ? 1 : -1;
        while (true)
        {
            submergingVisual.CurrentFrame = index;
            submergingVisual.RefreshFrame();
            yield return new WaitForSeconds(submergeFrameSeconds);
            if (index == finalIndex)
                break;
            index += step;
        }

        submergingVisual.SetManualAnimationUpdate(false);
    }

    private void SetLoopingVisual(AnimatedSpriteRenderer visual, Sprite[] frames)
    {
        if (visual == null || frames == null || frames.Length == 0)
            return;

        ShowOnly(visual);
        visual.SetManualAnimationUpdate(false);
        visual.idleSprite = frames[0];
        visual.animationSprite = frames;
        visual.animationTime = submergeFrameSeconds;
        visual.loop = true;
        visual.idle = false;
        activeSprite = visual;
    }

    private void ShowOnly(AnimatedSpriteRenderer selected)
    {
        foreach (AnimatedSpriteRenderer visual in GetComponentsInChildren<AnimatedSpriteRenderer>(true))
        {
            if (visual == null || visual == deathVisual)
                continue;

            bool isSelected = visual == selected;
            visual.enabled = isSelected;
            if (visual.TryGetComponent(out SpriteRenderer renderer))
                renderer.enabled = isSelected;
        }
    }

    private AnimatedSpriteRenderer FindVisual(string childName)
        => transform.Find(childName)?.GetComponent<AnimatedSpriteRenderer>();

    private void SetContactDamageEnabled(bool value)
    {
        if (contactCollider != null)
            contactCollider.enabled = value;
    }

    private bool TryGetPlayerDirection(out Vector2 targetDirection)
    {
        Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        Vector2 scanSize = Vector2.one * (tileSize * scanBoxSizePercent);
        Vector2 origin = transform.position;

        for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
        {
            Vector2 scanDirection = directions[directionIndex];
            for (int step = 1; step <= visionTiles; step++)
            {
                Vector2 tileCenter = origin + scanDirection * tileSize * step;
                Collider2D[] hits = Physics2D.OverlapBoxAll(tileCenter, scanSize, 0f);
                bool blocked = false;
                for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
                {
                    Collider2D hit = hits[hitIndex];
                    if (hit == null || hit.transform.IsChildOf(transform))
                        continue;

                    if (hit.GetComponentInParent<PlayerIdentity>() != null)
                    {
                        targetDirection = scanDirection;
                        return true;
                    }

                    if (hit.GetComponentInParent<Bomb>() != null || hit.gameObject.layer == LayerMask.NameToLayer("Stage"))
                        blocked = true;
                }

                if (blocked)
                    break;
            }
        }

        targetDirection = Vector2.zero;
        return false;
    }

    private void SpawnMiniIceTowers()
    {
        if (spawnedMiniIceTowers || miniIceTowerPrefab == null)
            return;

        spawnedMiniIceTowers = true;
        Vector3 spawnPosition = hasDeathTile
            ? new Vector3(deathTile.x, deathTile.y, transform.position.z)
            : transform.position;

        for (int i = 0; i < miniIceTowerCount; i++)
        {
            GameObject miniIceTower = Instantiate(miniIceTowerPrefab, spawnPosition, Quaternion.identity);
            if (miniIceTower.TryGetComponent(out CharacterHealth miniHealth))
                miniHealth.StartTemporaryInvulnerability(miniIceTowerSpawnInvulnerabilitySeconds);

            if (miniIceTower.TryGetComponent(out JunctionTurningEnemyMovementController movement))
            {
                movement.SetInitialDirection(MiniIceTowerSpawnDirections[i % MiniIceTowerSpawnDirections.Length]);
                movement.preferTurnAtJunction = true;
            }
        }

        GameManager gameManager = FindAnyObjectByType<GameManager>();
        if (gameManager != null)
            gameManager.NotifyEnemySpawned(miniIceTowerCount);

    }
}
