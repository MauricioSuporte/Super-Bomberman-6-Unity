using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class GreinMovementController : JunctionTurningEnemyMovementController
{
    [Header("Grein Fire Ability")]
    [SerializeField, Min(0.01f)] private float abilityMinCooldown = 5f;
    [SerializeField, Min(0.01f)] private float abilityMaxCooldown = 10f;
    [SerializeField, Min(0.01f)] private float fireDuration = 2f;
    [SerializeField, Min(0f)] private float distanceStartDelay = 0.25f;
    [SerializeField, Min(1)] private int fireRangeTiles = 2;
    [SerializeField] private float fireSpriteYOffset;
    [SerializeField] private Sprite[] fireSprites;

    private readonly List<GameObject> activeFireTiles = new();
    private Tilemap groundTilemap;
    private Tilemap destructiblesTilemap;
    private Tilemap indestructiblesTilemap;
    private Coroutine abilityLoop;
    private bool started;
    private bool usingAbility;

    private void OnEnable()
    {
        TryStartAbilityLoop();
    }

    private void OnDisable()
    {
        StopAbilityLoop();
        ClearActiveFireTiles();
        usingAbility = false;
    }

    protected override void Start()
    {
        base.Start();
        ResolveTilemaps();
        started = true;
        TryStartAbilityLoop();
    }

    protected override void FixedUpdate()
    {
        if (isDead || usingAbility)
        {
            if (usingAbility && rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }

        base.FixedUpdate();
    }

    protected override void Die()
    {
        StopAbilityLoop();
        ClearActiveFireTiles();
        usingAbility = false;
        base.Die();
    }

    private void TryStartAbilityLoop()
    {
        if (!started || !isActiveAndEnabled || isDead || abilityLoop != null)
            return;

        abilityLoop = StartCoroutine(AbilityLoop());
    }

    private void StopAbilityLoop()
    {
        if (abilityLoop == null)
            return;

        StopCoroutine(abilityLoop);
        abilityLoop = null;
    }

    private IEnumerator AbilityLoop()
    {
        while (isActiveAndEnabled && !isDead)
        {
            float min = Mathf.Max(0.01f, abilityMinCooldown);
            float max = Mathf.Max(min, abilityMaxCooldown);
            yield return new WaitForSeconds(Random.Range(min, max));

            if (isDead || !isActiveAndEnabled || usingAbility || isInDamagedLoop || IsStunned())
                continue;

            yield return ExecuteAbility();
        }

        abilityLoop = null;
    }

    private IEnumerator ExecuteAbility()
    {
        usingAbility = true;
        SnapToGrid();
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (activeSprite != null)
        {
            activeSprite.enabled = true;
            activeSprite.idle = false;
            activeSprite.CurrentFrame = 0;
            activeSprite.RefreshFrame();
        }

        var targets = GetFireTargets();
        float maxDelay = 0f;
        for (int i = 0; i < targets.Count; i++)
        {
            FireTarget target = targets[i];
            maxDelay = Mathf.Max(maxDelay, target.delay);
            StartCoroutine(SpawnFireAfterDelay(target.position, target.delay));
        }

        if (targets.Count > 0)
            yield return WaitGameplaySeconds(maxDelay + Mathf.Max(0.01f, fireDuration));

        usingAbility = false;

        if (!isDead && isActiveAndEnabled)
        {
            UpdateSpriteDirection(direction);
            DecideNextTile();
        }
    }

    private List<FireTarget> GetFireTargets()
    {
        ResolveTilemaps();
        var targets = new List<FireTarget>();
        if (groundTilemap == null)
            return targets;

        Vector2 origin = rb != null ? rb.position : transform.position;
        origin.x = Mathf.Round(origin.x / tileSize) * tileSize;
        origin.y = Mathf.Round(origin.y / tileSize) * tileSize;

        Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        for (int i = 0; i < directions.Length; i++)
        {
            Vector2 direction = directions[i];
            for (int distance = 1; distance <= Mathf.Max(1, fireRangeTiles); distance++)
            {
                Vector2 position = origin + direction * (distance * tileSize);
                if (!IsFreeGroundTile(position))
                    break;

                targets.Add(new FireTarget(
                    position,
                    (distance - 1) * Mathf.Max(0f, distanceStartDelay)));
            }
        }

        return targets;
    }

    private bool IsFreeGroundTile(Vector2 worldPosition)
    {
        Vector3Int cell = groundTilemap.WorldToCell(worldPosition);
        if (!groundTilemap.HasTile(cell))
            return false;

        if (destructiblesTilemap != null && destructiblesTilemap.HasTile(destructiblesTilemap.WorldToCell(worldPosition)))
            return false;

        return indestructiblesTilemap == null ||
               !indestructiblesTilemap.HasTile(indestructiblesTilemap.WorldToCell(worldPosition));
    }

    private IEnumerator SpawnFireAfterDelay(Vector2 position, float delay)
    {
        if (delay > 0f)
            yield return WaitGameplaySeconds(delay);

        if (!isActiveAndEnabled || isDead)
            yield break;

        SpawnFireTile(position);
    }

    private void SpawnFireTile(Vector2 position)
    {
        if (fireSprites == null || fireSprites.Length < 6)
            return;

        var fire = new GameObject("Grein Fire");
        fire.layer = LayerMask.NameToLayer("Explosion");
        fire.transform.position = position;

        var fireVisual = new GameObject("Visual");
        fireVisual.layer = fire.layer;
        fireVisual.transform.SetParent(fire.transform, false);
        fireVisual.transform.localPosition = Vector3.up * fireSpriteYOffset;

        var renderer = fireVisual.AddComponent<SpriteRenderer>();
        renderer.sprite = fireSprites[0];
        renderer.sortingOrder = 6;

        var animation = fire.AddComponent<AnimatedSpriteRenderer>();
        animation.idleSprite = fireSprites[0];
        animation.animationSprite = new[]
        {
            fireSprites[0], fireSprites[1], fireSprites[2], fireSprites[3], fireSprites[4], fireSprites[5],
            fireSprites[4], fireSprites[5], fireSprites[4], fireSprites[5], fireSprites[4], fireSprites[3],
            fireSprites[2], fireSprites[1], fireSprites[0]
        };
        animation.loop = false;
        animation.useSequenceDuration = true;
        animation.sequenceDuration = Mathf.Max(0.01f, fireDuration);
        animation.idle = false;
        animation.RestartAnimation();

        var collider = fire.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = Vector2.one * tileSize * 0.8f;

        activeFireTiles.Add(fire);
        StartCoroutine(DestroyFireAfterDuration(fire));
    }

    private IEnumerator DestroyFireAfterDuration(GameObject fire)
    {
        yield return WaitGameplaySeconds(Mathf.Max(0.01f, fireDuration));
        activeFireTiles.Remove(fire);
        if (fire != null)
            Destroy(fire);
    }

    private IEnumerator WaitGameplaySeconds(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds && isActiveAndEnabled && !isDead)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private bool IsStunned() => TryGetComponent(out StunReceiver stun) && stun.IsStunned;

    private void ResolveTilemaps()
    {
        if (groundTilemap == null)
            groundTilemap = FindTilemap("Ground");
        if (destructiblesTilemap == null)
            destructiblesTilemap = FindTilemap("Destructibles");
        if (indestructiblesTilemap == null)
            indestructiblesTilemap = FindTilemap("Indestructibles");
    }

    private static Tilemap FindTilemap(string tilemapName)
    {
        foreach (Tilemap tilemap in FindObjectsByType<Tilemap>(FindObjectsInactive.Include))
            if (tilemap.name == tilemapName)
                return tilemap;

        return null;
    }

    private void ClearActiveFireTiles()
    {
        for (int i = 0; i < activeFireTiles.Count; i++)
            if (activeFireTiles[i] != null)
                Destroy(activeFireTiles[i]);

        activeFireTiles.Clear();
    }

    private readonly struct FireTarget
    {
        public readonly Vector2 position;
        public readonly float delay;

        public FireTarget(Vector2 position, float delay)
        {
            this.position = position;
            this.delay = delay;
        }
    }
}
