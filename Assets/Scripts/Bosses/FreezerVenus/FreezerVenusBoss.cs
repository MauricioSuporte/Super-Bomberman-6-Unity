using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterHealth), typeof(Rigidbody2D), typeof(BoxCollider2D))]
public sealed class FreezerVenusBoss : MonoBehaviour, IKillable
{
    [Header("Sprites")]
    public SpriteRenderer body;
    public SpriteRenderer shadow;
    public Sprite[] closedFrames;
    public Sprite[] openingFrames;
    public Sprite[] idleFrames;
    public Sprite[] castFrames;
    public Sprite[] iceCastFrames;
    public Sprite[] dollCastFrames;
    public Sprite[] hurtFrames;
    public Sprite[] tornadoFrames;
    public Sprite[] iceFrames;
    public Sprite[] dollFrames;
    [Header("Combat")]
    [Min(0.1f)] public float moveSpeed = 2.1f;
    [Min(0.1f)] public float attackInterval = 2.4f;
    [Min(0.1f)] public float deathSeconds = 6f;
    [Header("Presentation")]
    public AudioClip deathSfx;
    public AudioClip endStageMusic;
    public GameObject explosionPrefab;

    private readonly List<FreezerVenusProjectile> projectiles = new();
    private readonly List<GameObject> deathEffects = new();
    private CharacterHealth health;
    private Rigidbody2D rb;
    private Collider2D hitbox;
    private AudioSource audioSource;
    private MovementController[] players;
    private float retargetTimer;
    private float animationTime;
    private float hurtTime;
    private float attackTimer;
    private Vector3 logicalGroundPosition;
    private Vector3 movementDestination;
    private bool moving;
    private Sprite[] smallDollFrames;
    private int attackIndex;
    private bool attacking;
    private bool dead;
    public bool CombatActive { get; private set; }
    public int Life => health != null ? health.life : 10;
    public FreezerVenusArena Arena { get; private set; }
    public Vector3 GroundPosition => FreezerVenusArena.Snap(transform.position + Vector3.down * 1.75f);
    public Vector3 CrownPosition => FreezerVenusArena.Snap(transform.position + Vector3.up * 0.875f);
    public Sprite[] SmallDollFrames => smallDollFrames;

    private void Awake()
    {
        health = GetComponent<CharacterHealth>();
        rb = GetComponent<Rigidbody2D>();
        hitbox = GetComponent<Collider2D>();
        audioSource = GetComponent<AudioSource>();
        health.life = 10;
        health.hitInvulnerableDuration = 0.85f;
        health.SetExternalInvulnerability(true);
        health.Damaged += OnDamaged;
        health.Died += Kill;
        hitbox.enabled = false;
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.None;
        Arena = new FreezerVenusArena(FindAnyObjectByType<GameManager>());
        smallDollFrames = CreateSmallDollFrames();
        SetFrame(closedFrames, 0);
    }

    public void BeginCombat()
    {
        if (dead || CombatActive) return;
        CombatActive = true;
        health.SetExternalInvulnerability(false);
        hitbox.enabled = true;
        attackTimer = attackInterval;
        RefreshPlayers();
        if (shadow != null) shadow.transform.localPosition = Vector3.down * 1.75f;
        logicalGroundPosition = GroundPosition;
        SetWorldPosition(logicalGroundPosition + Vector3.up * 1.75f);
        ChooseMovementDestination();
    }

    public void SetWorldPosition(Vector3 position)
    {
        position = FreezerVenusArena.Snap(position);
        transform.position = position;
        if (rb != null) rb.position = position;
    }

    public void SetFrame(Sprite[] frames, int index)
    {
        if (body != null && frames != null && frames.Length > 0)
            body.sprite = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
    }

    private void Update()
    {
        if (!CombatActive || dead || GamePauseController.IsPaused) return;
        animationTime += Time.deltaTime;
        hurtTime = Mathf.Max(0f, hurtTime - Time.deltaTime);
        retargetTimer -= Time.deltaTime;
        if (retargetTimer <= 0f) RefreshPlayers();
        if (!attacking && hurtTime > 0f) SetFrame(hurtFrames, (int)(animationTime * 8f) % Mathf.Max(1, hurtFrames.Length));
        else if (!attacking) SetFrame(idleFrames, (int)(animationTime * 5f) % Mathf.Max(1, idleFrames.Length));
        if (attacking || moving || hurtTime > 0f) return;
        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f && FindTarget(transform.position) != null)
            StartCoroutine(AttackRoutine());
    }

    private void FixedUpdate()
    {
        if (!CombatActive || dead || attacking || !moving || hurtTime > 0f || GamePauseController.IsPaused) return;
        // Fly diagonally until one axis reaches its destination, then finish cardinally.
        // Floor obstacles and bombs only constrain grounded projectiles, not the boss.
        Vector3 delta = movementDestination - logicalGroundPosition;
        Vector3 direction = new(Mathf.Sign(delta.x), Mathf.Sign(delta.y), 0f);
        if (Mathf.Abs(delta.x) < 0.0001f) direction.x = 0f;
        if (Mathf.Abs(delta.y) < 0.0001f) direction.y = 0f;
        Vector3 travel = direction.normalized * (moveSpeed * Time.fixedDeltaTime);
        logicalGroundPosition += new Vector3(
            Mathf.Clamp(travel.x, -Mathf.Abs(delta.x), Mathf.Abs(delta.x)),
            Mathf.Clamp(travel.y, -Mathf.Abs(delta.y), Mathf.Abs(delta.y)), 0f);
        SetWorldPosition(logicalGroundPosition + Vector3.up * 1.75f);
        if ((logicalGroundPosition - movementDestination).sqrMagnitude < 0.000001f) moving = false;
    }

    private void ChooseMovementDestination()
    {
        var target = FindTarget(GroundPosition);
        if (target == null || Arena.Ground == null) { moving = false; return; }
        Vector3Int origin = Arena.Cell(GroundPosition);
        BoundsInt bounds = Arena.Ground.cellBounds;
        int upperHalf = bounds.yMin + bounds.size.y / 2;
        // Only destinations need clear floor; flight can still cross pillars and bombs.
        // Four of every five trips stay above the midpoint, keeping downward attacks useful.
        bool preferUpperHalf = attackIndex % 5 != 4;
        var candidates = new List<Vector3Int>();
        foreach (var cell in bounds.allPositionsWithin)
        {
            int distance = Mathf.Max(Mathf.Abs(cell.x - origin.x), Mathf.Abs(cell.y - origin.y));
            if (distance >= 2 && distance <= 5 && cell.y <= bounds.yMax - 3 &&
                (!preferUpperHalf || cell.y >= upperHalf) && Arena.IsStaticWalkable(cell)) candidates.Add(cell);
        }
        if (candidates.Count == 0) { moving = false; return; }
        if (attackIndex % 3 == 1)
        {
            candidates.Sort((a, b) => (Arena.Center(a) - target.transform.position).sqrMagnitude.CompareTo(
                (Arena.Center(b) - target.transform.position).sqrMagnitude));
            movementDestination = Arena.Center(candidates[0]);
        }
        else movementDestination = Arena.Center(candidates[Random.Range(0, candidates.Count)]);
        moving = true;
    }

    private IEnumerator AttackRoutine()
    {
        attacking = true;
        // Alternate attacks so all patterns remain available even if players camp a corner.
        int attack = attackIndex++ % 3;
        Sprite[] preparation = attack == 1 ? iceCastFrames : attack == 2 ? dollCastFrames : castFrames;
        for (int i = 0; i < preparation.Length; i++)
        {
            SetFrame(preparation, i);
            yield return WaitUnpaused(0.16f);
        }
        if (attack == 0)
            SpawnProjectile(FreezerVenusProjectile.AttackKind.Tornado, Vector2.down,
                Arena.Center(Arena.NearestFloor(GroundPosition)));
        else if (attack == 1)
        {
            for (int i = 0; i < 6; i++)
            {
                Vector2 direction = Quaternion.Euler(0f, 0f, Mathf.Lerp(-65f, 65f, i / 5f)) * Vector2.down;
                SpawnProjectile(FreezerVenusProjectile.AttackKind.Ice, direction, GroundPosition);
            }
        }
        else
        {
            foreach (var cell in Arena.DollSpawnCells(GroundPosition))
            {
                SpawnProjectile(FreezerVenusProjectile.AttackKind.Doll, Vector2.down, Arena.Center(cell));
            }
        }
        yield return WaitUnpaused(0.45f);
        attacking = false;
        attackTimer = attackInterval;
        ChooseMovementDestination();
    }

    private void SpawnProjectile(FreezerVenusProjectile.AttackKind kind, Vector2 direction, Vector3 position)
    {
        projectiles.RemoveAll(p => p == null);
        Sprite[] frames = kind == FreezerVenusProjectile.AttackKind.Tornado ? tornadoFrames :
            kind == FreezerVenusProjectile.AttackKind.Ice ? iceFrames : smallDollFrames;
        var projectile = FreezerVenusProjectile.Create(this, kind, frames,
            position, direction);
        projectiles.Add(projectile);
    }

    private void RefreshPlayers()
    {
        players = FindObjectsByType<MovementController>();
        retargetTimer = 0.3f;
    }

    public MovementController FindTarget(Vector2 origin)
    {
        MovementController closest = null;
        float distance = float.PositiveInfinity;
        if (players == null) return null;
        foreach (var player in players)
        {
            if (player == null || !player.isActiveAndEnabled || !player.CompareTag("Player") ||
                player.isDead || player.IsEndingStage) continue;
            float candidate = ((Vector2)player.transform.position - origin).sqrMagnitude;
            if (candidate >= distance) continue;
            closest = player;
            distance = candidate;
        }
        return closest;
    }

    private void OnDamaged(int amount)
    {
        if (dead) return;
        // Damage still counts, but casting owns the sprite until the attack finishes.
        if (!attacking)
        {
            hurtTime = 0.45f;
            SetFrame(hurtFrames, 0);
        }
    }

    private void OnTriggerEnter2D(Collider2D other) => HandleContact(other);
    private void OnTriggerStay2D(Collider2D other) => HandleContact(other);

    private void HandleContact(Collider2D other)
    {
        if (!CombatActive || dead || GamePauseController.IsPaused) return;
        if (other.gameObject.layer == LayerMask.NameToLayer("Explosion"))
            health.TakeDamage(1, fromExplosion: true);
        else
            TryHitPlayer(other);
    }

    public static bool TryHitPlayer(Collider2D other)
    {
        var player = other.GetComponentInParent<MovementController>();
        if (player == null || !player.CompareTag("Player") || player.isDead || player.IsEndingStage ||
            player.InputLocked || player.explosionInvulnerable) return false;
        var playerHealth = player.GetComponent<CharacterHealth>();
        if (playerHealth != null && playerHealth.IsInvulnerable) return false;
        if (player.IsMounted && player.TryGetComponent<PlayerMountCompanion>(out var mount))
            mount.OnMountedLouieHit(1, false);
        else if (playerHealth != null) playerHealth.TakeDamage(1);
        else player.Kill();
        return true;
    }

    public void KillByExplosion() => Kill();

    public void Kill()
    {
        if (dead) return;
        dead = true;
        CombatActive = false;
        hitbox.enabled = false;
        StopAllCoroutines();
        ClearProjectiles();
        var duel = FindAnyObjectByType<StageAssets.World3HallStageSevenSequence>();
        if (duel != null) duel.FinishDuel();
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        SetFrame(hurtFrames, hurtFrames.Length - 1);
        int bursts = Mathf.CeilToInt(deathSeconds / 0.15f);
        float lastEffectDuration = 0f;
        for (int i = 0; i < bursts; i++)
        {
            body.enabled = i % 2 == 0;
            if (shadow != null) shadow.enabled = body.enabled;
            if (explosionPrefab != null)
                lastEffectDuration = SpawnDeathExplosion();
            if (i % 3 == 0 && audioSource != null && deathSfx != null)
                GameAudioSettings.PlaySfx(audioSource, deathSfx);
            yield return WaitUnpaused(0.15f);
        }
        yield return WaitUnpaused(lastEffectDuration);
        body.enabled = false;
        if (shadow != null) shadow.enabled = false;
        var manager = FindAnyObjectByType<GameManager>();
        // 3-8 is the current campaign's final stage, hosted in World3Hall.
        if (manager != null) manager.nextStageSceneName = string.Empty;
        var ending = gameObject.AddComponent<BossEndStageSequence>();
        ending.completedStageSceneName = "Stage_3-8";
        ending.endStageMusic = endStageMusic;
        ending.StartBossDefeatedSequence();
        Destroy(gameObject);
    }

    private void ClearProjectiles()
    {
        foreach (var projectile in projectiles)
            if (projectile != null) Destroy(projectile.gameObject);
        projectiles.Clear();
    }

    private float SpawnDeathExplosion()
    {
        Vector3 position = FreezerVenusArena.Snap(transform.position + (Vector3)(Random.insideUnitCircle * 1.1f));
        var effect = Instantiate(explosionPrefab, position, Quaternion.identity);
        deathEffects.RemoveAll(e => e == null);
        deathEffects.Add(effect);
        float duration = 0.5f;
        var animation = effect.GetComponentInChildren<AnimatedSpriteRenderer>(true);
        if (animation != null)
        {
            animation.idle = false;
            animation.loop = false;
            animation.pingPong = false;
            animation.SetManualAnimationUpdate(true);
            animation.RestartAnimation();
            duration = animation.useSequenceDuration ? animation.sequenceDuration :
                animation.animationTime * (animation.animationSprite != null ? animation.animationSprite.Length : 1);
            if (animation.frameDurations != null && animation.animationSprite != null &&
                animation.frameDurations.Length == animation.animationSprite.Length)
            {
                duration = 0f;
                foreach (float seconds in animation.frameDurations) duration += Mathf.Max(0.01f, seconds);
            }
        }
        duration = Mathf.Max(0.05f, duration) + 0.05f;
        StartCoroutine(PlayDeathEffect(effect, animation, duration));
        return duration;
    }

    private static IEnumerator PlayDeathEffect(GameObject effect, AnimatedSpriteRenderer animation, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && effect != null)
        {
            yield return null;
            if (GamePauseController.IsPaused) continue;
            elapsed += Time.deltaTime;
            if (animation != null) animation.AdvanceAnimation(Time.deltaTime, Time.deltaTime);
        }
        if (effect != null) Destroy(effect);
    }

    private Sprite[] CreateSmallDollFrames()
    {
        var result = new Sprite[dollFrames.Length];
        for (int i = 0; i < result.Length; i++)
        {
            Sprite source = dollFrames[i];
            Rect rect = source.rect;
            int width = Mathf.FloorToInt(rect.width / 2f);
            int height = Mathf.FloorToInt(rect.height / 2f);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "FreezerVenus doll pixels" };
            var pixels = new Color32[width * height];
            Color32[] sheet = source.texture.GetPixels32();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = sheet[((int)rect.y + y * 2) * source.texture.width + (int)rect.x + x * 2];
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            result[i] = Sprite.Create(texture, new Rect(0, 0, width, height),
                new Vector2(Mathf.Floor(width / 2f) / width, Mathf.Floor(height / 2f) / height), 16f, 0, SpriteMeshType.FullRect);
        }
        return result;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ClearProjectiles();
        foreach (var effect in deathEffects) if (effect != null) Destroy(effect);
        deathEffects.Clear();
    }

    private void OnDestroy()
    {
        if (smallDollFrames != null)
            foreach (var sprite in smallDollFrames)
                if (sprite != null) { Destroy(sprite.texture); Destroy(sprite); }
        if (health == null) return;
        health.Damaged -= OnDamaged;
        health.Died -= Kill;
    }

    public static IEnumerator WaitUnpaused(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            yield return null;
            if (!GamePauseController.IsPaused) elapsed += Time.deltaTime;
        }
    }
}
