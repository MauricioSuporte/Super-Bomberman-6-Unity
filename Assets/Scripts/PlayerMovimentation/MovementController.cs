using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(BombController))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AbilitySystem))]
[RequireComponent(typeof(StunReceiver))]
public class MovementController : MonoBehaviour, IKillable
{
    public event Action<MovementController> Died;

    [Header("SFX")]
    public AudioClip deathSfx;

    [Header("Stats")]
    public float speed = 5f;
    public float tileSize = 1f;
    public LayerMask obstacleMask;

    [Header("Speed (SB5 Internal)")]
    [SerializeField] private int speedInternal = PlayerPersistentStats.BaseSpeedNormal;
    public int SpeedInternal => speedInternal;

    [Header("Player Id (only used if tagged Player)")]
    [SerializeField, Range(1, 4)] private int playerId = 1;
    public int PlayerId => playerId;

    [Header("Sprites")]
    public AnimatedSpriteRenderer spriteRendererUp;
    public AnimatedSpriteRenderer spriteRendererDown;
    public AnimatedSpriteRenderer spriteRendererLeft;
    public AnimatedSpriteRenderer spriteRendererRight;
    public AnimatedSpriteRenderer spriteRendererDeath;
    public AnimatedSpriteRenderer spriteRendererEndStage;
    public AnimatedSpriteRenderer spriteRendererCheering;

    [Header("Mounted On Louie")]
    public AnimatedSpriteRenderer mountedSpriteUp;
    public AnimatedSpriteRenderer mountedSpriteDown;
    public AnimatedSpriteRenderer mountedSpriteLeft;
    public AnimatedSpriteRenderer mountedSpriteRight;

    [Header("Mounted On Louie - Pink Y Override")]
    public float pinkMountedSpritesLocalY = 1.3f;

    [Header("Contact Damage")]
    public float contactDamageCooldownSeconds = 0.15f;

    [Header("Death Timing")]
    public float deathDisableSeconds = 2f;

    [Header("Death Behavior")]
    public bool checkWinStateOnDeath = true;

    [Header("End Stage Animation")]
    public float endStageTotalTime = 1f;
    public int endStageFrameCount = 9;

    [Header("Visual Override (external)")]
    [SerializeField] private bool visualOverrideActive;
    public bool VisualOverrideActive => visualOverrideActive;

    [Header("Grid Alignment")]
    [SerializeField, Range(0.5f, 20f)] private float perpendicularAlignMultiplier = 8f;
    [SerializeField] private bool snapPerpendicularOnAxisStart = true;
    [SerializeField, Range(0.0001f, 0.05f)] private float alignEpsilon = 0.0015f;

    public Rigidbody2D Rigidbody { get; private set; }
    public Vector2 Direction => direction;

    protected Vector2 facingDirection = Vector2.down;
    public Vector2 FacingDirection => facingDirection;

    private bool isMountedOnLouie;
    public bool IsMountedOnLouie => isMountedOnLouie;

    public bool isDead;
    protected bool inputLocked;
    public bool InputLocked => inputLocked;

    private bool isEndingStage;
    public bool IsEndingStage => isEndingStage;

    protected Vector2 direction = Vector2.zero;
    protected bool hasInput;
    protected bool explosionInvulnerable;

    private bool mountedSpritesYOverridden;
    private float nextContactDamageTime;

    private readonly HashSet<Collider2D> touchingHazards = new();

    private const float CenterEpsilon = 0.01f;
    private float SlideDeadZone => tileSize * 0.25f;

    private enum MoveAxis
    {
        None = 0,
        Horizontal = 1,
        Vertical = 2
    }

    private MoveAxis currentAxis = MoveAxis.None;

    protected AudioSource audioSource;
    protected BombController bombController;
    protected AbilitySystem abilitySystem;
    private StunReceiver stunReceiver;

    private CharacterHealth cachedHealth;
    private PlayerLouieCompanion cachedCompanion;
    private PlayerRidingController cachedRiding;

    protected AnimatedSpriteRenderer activeSpriteRenderer;

    private IMovementAbility[] movementAbilities = Array.Empty<IMovementAbility>();
    private int explosionLayer;
    private int enemyLayer;

    public void SetPlayerId(int id)
    {
        playerId = Mathf.Clamp(id, 1, 4);

        if (!IsPlayer())
            return;

        if (bombController != null)
            bombController.SetPlayerId(playerId);

        var skin = GetComponentInChildren<PlayerBomberSkinController>(true);
        if (skin != null)
            skin.Apply(PlayerPersistentStats.Get(playerId).Skin);

        SyncMountedFromPersistent();
        ApplySpeedInternal(speedInternal);
        EnableExclusiveFromState();
    }

    protected virtual void Awake()
    {
        Rigidbody = GetComponent<Rigidbody2D>();
        stunReceiver = GetComponent<StunReceiver>();
        bombController = GetComponent<BombController>();
        abilitySystem = GetComponent<AbilitySystem>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.clip = null;
        }

        cachedHealth = GetComponent<CharacterHealth>();
        TryGetComponent(out cachedCompanion);
        TryGetComponent(out cachedRiding);

        if (obstacleMask.value == 0)
            obstacleMask = LayerMask.GetMask("Stage", "Bomb");

        explosionLayer = LayerMask.NameToLayer("Explosion");
        enemyLayer = LayerMask.NameToLayer("Enemy");

        CacheMovementAbilities();

        InitRuntimeState(loadPersistent: true);
    }

    protected virtual void OnEnable()
    {
        InitRuntimeState(loadPersistent: true);
    }

    protected virtual void OnDisable()
    {
        touchingHazards.Clear();
    }

    private void InitRuntimeState(bool loadPersistent)
    {
        direction = Vector2.zero;
        hasInput = false;
        touchingHazards.Clear();
        currentAxis = MoveAxis.None;

        if (bombController != null)
            bombController.SetPlayerId(playerId);

        if (loadPersistent && IsPlayer())
            PlayerPersistentStats.LoadInto(playerId, this, bombController);

        SyncMountedFromPersistent();

        if (activeSpriteRenderer == null)
            activeSpriteRenderer = spriteRendererDown;

        ApplySpeedInternal(speedInternal);
        EnableExclusiveFromState();
    }

    private void CacheMovementAbilities()
    {
        var monos = GetComponents<MonoBehaviour>();
        if (monos == null || monos.Length == 0)
        {
            movementAbilities = Array.Empty<IMovementAbility>();
            return;
        }

        var list = new List<IMovementAbility>(4);
        for (int i = 0; i < monos.Length; i++)
        {
            if (monos[i] is IMovementAbility a)
                list.Add(a);
        }

        movementAbilities = list.Count > 0 ? list.ToArray() : Array.Empty<IMovementAbility>();
    }

    public void SyncMountedFromPersistent()
    {
        if (!IsPlayer())
            return;

        var st = PlayerPersistentStats.Get(playerId);
        isMountedOnLouie = st.MountedLouie != MountedLouieType.None;

        if (facingDirection == Vector2.zero)
            facingDirection = Vector2.down;
    }

    private bool IsPlayer() => CompareTag("Player");

    private bool IsRidingPlaying()
    {
        if (cachedRiding == null)
            TryGetComponent(out cachedRiding);

        return cachedRiding != null && cachedRiding.IsPlaying;
    }

    private static void SetAnimEnabled(AnimatedSpriteRenderer r, bool on)
    {
        if (r == null) return;

        r.enabled = on;

        if (r.TryGetComponent(out SpriteRenderer sr) && sr != null)
            sr.enabled = on;
    }

    private void SetMany(bool enabled, params AnimatedSpriteRenderer[] arr)
    {
        for (int i = 0; i < arr.Length; i++)
            SetAnimEnabled(arr[i], enabled);
    }

    private void DisableAllFootSprites()
    {
        SetMany(false, spriteRendererUp, spriteRendererDown, spriteRendererLeft, spriteRendererRight);
    }

    private void DisableAllMountedSprites()
    {
        SetMany(false, mountedSpriteUp, mountedSpriteDown, mountedSpriteLeft, mountedSpriteRight);
    }

    public void SetAllSpritesVisible(bool visible)
    {
        SetMany(visible,
            spriteRendererUp, spriteRendererDown, spriteRendererLeft, spriteRendererRight,
            spriteRendererDeath, spriteRendererEndStage, spriteRendererCheering,
            mountedSpriteUp, mountedSpriteDown, mountedSpriteLeft, mountedSpriteRight);

        var rider = GetComponentInChildren<LouieVisualController>(true);
        if (rider != null)
        {
            var srs = rider.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < srs.Length; i++)
                if (srs[i] != null)
                    srs[i].enabled = visible;

            var anims = rider.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
            for (int i = 0; i < anims.Length; i++)
                if (anims[i] != null)
                    anims[i].enabled = visible;
        }
    }

    public void EnableExclusiveFromState()
    {
        if (visualOverrideActive)
            return;

        if (IsRidingPlaying())
        {
            DisableAllFootSprites();
            DisableAllMountedSprites();
            return;
        }

        SetAllSpritesVisible(false);
        ForceExclusiveSpriteFromState();
    }

    protected virtual void Update()
    {
        if (stunReceiver != null && stunReceiver.IsStunned)
        {
            hasInput = false;
            direction = Vector2.zero;

            if (activeSpriteRenderer != null)
            {
                activeSpriteRenderer.idle = true;
                activeSpriteRenderer.RefreshFrame();
            }

            return;
        }

        if (inputLocked || GamePauseController.IsPaused || isDead)
            return;

        if (visualOverrideActive)
            return;

        hasInput = false;
        HandleInput();
    }

    protected virtual void HandleInput()
    {
        if (!IsPlayer())
        {
            ApplyDirectionFromVector(Vector2.zero);
            return;
        }

        var input = PlayerInputManager.Instance;
        if (input == null)
        {
            ApplyDirectionFromVector(Vector2.zero);
            return;
        }

        if (input.Get(playerId, PlayerAction.MoveUp)) ApplyDirectionFromVector(Vector2.up);
        else if (input.Get(playerId, PlayerAction.MoveDown)) ApplyDirectionFromVector(Vector2.down);
        else if (input.Get(playerId, PlayerAction.MoveLeft)) ApplyDirectionFromVector(Vector2.left);
        else if (input.Get(playerId, PlayerAction.MoveRight)) ApplyDirectionFromVector(Vector2.right);
        else ApplyDirectionFromVector(Vector2.zero);
    }

    public void ApplyDirectionFromVector(Vector2 dir)
    {
        if (IsRidingPlaying())
            return;

        hasInput = dir != Vector2.zero;

        if (dir != Vector2.zero)
            facingDirection = dir;

        if (isMountedOnLouie)
        {
            DisableAllFootSprites();
            DisableAllMountedSprites();

            var target = PickMountedRenderer(dir);
            if (target == null)
                target = mountedSpriteDown;

            SetDirection(dir, target);
            return;
        }

        var foot = PickFootRenderer(dir);
        SetDirection(dir == Vector2.zero ? Vector2.zero : dir, foot);
    }

    private Vector2 GetFacing(Vector2 dir)
    {
        Vector2 face = dir != Vector2.zero ? dir : facingDirection;
        if (face == Vector2.zero) face = Vector2.down;
        return face;
    }

    private AnimatedSpriteRenderer PickFootRenderer(Vector2 dir)
    {
        Vector2 face = GetFacing(dir);

        if (face == Vector2.up) return spriteRendererUp;
        if (face == Vector2.down) return spriteRendererDown;
        if (face == Vector2.left) return spriteRendererLeft;
        if (face == Vector2.right) return spriteRendererRight;

        return spriteRendererDown != null ? spriteRendererDown : activeSpriteRenderer;
    }

    private AnimatedSpriteRenderer PickMountedRenderer(Vector2 dir)
    {
        Vector2 face = GetFacing(dir);

        if (face == Vector2.up) return mountedSpriteUp != null ? mountedSpriteUp : spriteRendererUp;
        if (face == Vector2.down) return mountedSpriteDown != null ? mountedSpriteDown : spriteRendererDown;
        if (face == Vector2.left) return mountedSpriteLeft != null ? mountedSpriteLeft : spriteRendererLeft;
        if (face == Vector2.right) return mountedSpriteRight != null ? mountedSpriteRight : spriteRendererRight;

        return mountedSpriteDown != null ? mountedSpriteDown : spriteRendererDown;
    }

    public void ApplySpeedInternal(int newInternal)
    {
        speedInternal = PlayerPersistentStats.ClampSpeedInternal(newInternal);
        speed = PlayerPersistentStats.InternalSpeedToTilesPerSecond(speedInternal);
    }

    public bool TryAddSpeedUp(int speedStep = PlayerPersistentStats.SpeedStep)
    {
        int before = speedInternal;
        ApplySpeedInternal(speedInternal + speedStep);
        return speedInternal != before;
    }

    protected virtual void FixedUpdate()
    {
        if (inputLocked || GamePauseController.IsPaused || isDead)
            return;

        if (!hasInput || direction == Vector2.zero)
        {
            currentAxis = MoveAxis.None;
            return;
        }

        if (stunReceiver != null && stunReceiver.IsStunned)
        {
            if (Rigidbody != null)
                Rigidbody.linearVelocity = Vector2.zero;

            currentAxis = MoveAxis.None;
            return;
        }

        float dt = Time.fixedDeltaTime;
        float speedWorldPerSecond = speed * tileSize;
        float moveSpeed = speedWorldPerSecond * dt;

        Vector2 position = Rigidbody.position;

        bool movingVertical = Mathf.Abs(direction.y) > 0.01f;
        bool movingHorizontal = Mathf.Abs(direction.x) > 0.01f;

        MoveAxis newAxis = movingHorizontal ? MoveAxis.Horizontal : (movingVertical ? MoveAxis.Vertical : MoveAxis.None);
        bool axisJustStartedOrChanged = (newAxis != MoveAxis.None && newAxis != currentAxis);
        currentAxis = newAxis;

        if (currentAxis == MoveAxis.Horizontal)
        {
            AlignPerpendicular(ref position, axisIsHorizontal: true, moveSpeed: moveSpeed,
                snapImmediate: axisJustStartedOrChanged && snapPerpendicularOnAxisStart);
        }
        else if (currentAxis == MoveAxis.Vertical)
        {
            AlignPerpendicular(ref position, axisIsHorizontal: false, moveSpeed: moveSpeed,
                snapImmediate: axisJustStartedOrChanged && snapPerpendicularOnAxisStart);
        }

        float half = tileSize * 0.5f;

        bool blockLeft = IsSolidAt(position + Vector2.left * half);
        bool blockRight = IsSolidAt(position + Vector2.right * half);
        bool blockUp = IsSolidAt(position + Vector2.up * half);
        bool blockDown = IsSolidAt(position + Vector2.down * half);

        if (movingVertical && blockLeft && blockRight)
        {
            float targetX = Mathf.Round(position.x / tileSize) * tileSize;
            position.x = Mathf.MoveTowards(position.x, targetX, moveSpeed);
        }

        if (movingHorizontal && blockUp && blockDown)
        {
            float targetY = Mathf.Round(position.y / tileSize) * tileSize;
            position.y = Mathf.MoveTowards(position.y, targetY, moveSpeed);
        }

        Vector2 targetPosition = position + direction * moveSpeed;

        if (!IsBlocked(targetPosition))
        {
            Rigidbody.MovePosition(targetPosition);
            return;
        }

        if (movingVertical)
        {
            float centerX = Mathf.Round(position.x / tileSize) * tileSize;
            float offsetX = Mathf.Abs(position.x - centerX);

            if (offsetX > SlideDeadZone)
                TrySlideHorizontally(position, moveSpeed);
        }
        else if (movingHorizontal)
        {
            float centerY = Mathf.Round(position.y / tileSize) * tileSize;
            float offsetY = Mathf.Abs(position.y - centerY);

            if (offsetY > SlideDeadZone)
                TrySlideVertically(position, moveSpeed);
        }
    }

    private void AlignPerpendicular(ref Vector2 position, bool axisIsHorizontal, float moveSpeed, bool snapImmediate)
    {
        if (tileSize <= 0.0001f)
            return;

        float alignStep = moveSpeed * Mathf.Max(0.5f, perpendicularAlignMultiplier);
        Vector2 moveDir = direction;

        if (axisIsHorizontal)
        {
            float targetY = Mathf.Round(position.y / tileSize) * tileSize;

            if (Mathf.Abs(position.y - targetY) <= alignEpsilon)
            {
                Vector2 snapped = new(position.x, targetY);
                if (CanAlignToPerpendicularTarget(snapped, moveDir))
                    position.y = targetY;
                return;
            }

            Vector2 candidate = new(position.x, targetY);

            if (snapImmediate && CanAlignToPerpendicularTarget(candidate, moveDir))
            {
                position = candidate;
                return;
            }

            float newY = Mathf.MoveTowards(position.y, targetY, alignStep);
            Vector2 nextCandidate = new(position.x, newY);

            if (CanAlignToPerpendicularTarget(nextCandidate, moveDir))
                position.y = newY;

            return;
        }

        float targetX = Mathf.Round(position.x / tileSize) * tileSize;

        if (Mathf.Abs(position.x - targetX) <= alignEpsilon)
        {
            Vector2 snapped = new(targetX, position.y);
            if (CanAlignToPerpendicularTarget(snapped, moveDir))
                position.x = targetX;
            return;
        }

        Vector2 candidate2 = new(targetX, position.y);

        if (snapImmediate && CanAlignToPerpendicularTarget(candidate2, moveDir))
        {
            position = candidate2;
            return;
        }

        float newX = Mathf.MoveTowards(position.x, targetX, alignStep);
        Vector2 nextCandidate2 = new(newX, position.y);

        if (CanAlignToPerpendicularTarget(nextCandidate2, moveDir))
            position.x = newX;
    }

    private void TrySlideHorizontally(Vector2 position, float moveSpeed)
    {
        float leftCenter = Mathf.Floor(position.x / tileSize) * tileSize;
        float rightCenter = Mathf.Ceil(position.x / tileSize) * tileSize;

        Vector2 verticalStep = new(0f, direction.y * moveSpeed);

        bool leftFree = !IsBlocked(new Vector2(leftCenter, position.y) + verticalStep);
        bool rightFree = !IsBlocked(new Vector2(rightCenter, position.y) + verticalStep);

        if (!leftFree && !rightFree)
            return;

        float targetX;

        if (leftFree && !rightFree) targetX = leftCenter;
        else if (rightFree && !leftFree) targetX = rightCenter;
        else targetX = Mathf.Abs(position.x - leftCenter) <= Mathf.Abs(position.x - rightCenter) ? leftCenter : rightCenter;

        if (Mathf.Abs(position.x - targetX) > CenterEpsilon)
        {
            float newX = Mathf.MoveTowards(position.x, targetX, moveSpeed);
            Rigidbody.MovePosition(new Vector2(newX, position.y));
        }
        else
        {
            Vector2 newPos = new Vector2(targetX, position.y) + verticalStep;
            if (!IsBlocked(newPos))
                Rigidbody.MovePosition(newPos);
        }
    }

    private void TrySlideVertically(Vector2 position, float moveSpeed)
    {
        float bottomCenter = Mathf.Floor(position.y / tileSize) * tileSize;
        float topCenter = Mathf.Ceil(position.y / tileSize) * tileSize;

        Vector2 horizontalStep = new(direction.x * moveSpeed, 0f);

        bool bottomFree = !IsBlocked(new Vector2(position.x, bottomCenter) + horizontalStep);
        bool topFree = !IsBlocked(new Vector2(position.x, topCenter) + horizontalStep);

        if (!bottomFree && !topFree)
            return;

        float targetY;

        if (bottomFree && !topFree) targetY = bottomCenter;
        else if (topFree && !bottomFree) targetY = topCenter;
        else targetY = Mathf.Abs(position.y - bottomCenter) <= Mathf.Abs(position.y - topCenter) ? bottomCenter : topCenter;

        if (Mathf.Abs(position.y - targetY) > CenterEpsilon)
        {
            float newY = Mathf.MoveTowards(position.y, targetY, moveSpeed);
            Rigidbody.MovePosition(new Vector2(position.x, newY));
        }
        else
        {
            Vector2 newPos = new Vector2(position.x, targetY) + horizontalStep;
            if (!IsBlocked(newPos))
                Rigidbody.MovePosition(newPos);
        }
    }

    protected bool IsSolidAt(Vector2 worldPosition)
    {
        Vector2 size = Vector2.one * (tileSize * 0.6f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(worldPosition, size, 0f, obstacleMask);
        if (hits == null || hits.Length == 0)
            return false;

        bool canPassDestructibles = abilitySystem != null &&
                                   abilitySystem.IsEnabled(DestructiblePassAbility.AbilityId);

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null) continue;
            if (hit.gameObject == gameObject) continue;
            if (hit.isTrigger) continue;
            if (canPassDestructibles && hit.CompareTag("Destructibles")) continue;

            if (hit.gameObject.layer == LayerMask.NameToLayer("Bomb"))
            {
                var bomb = hit.GetComponent<Bomb>();
                if (bomb != null && bomb.Owner == bombController)
                {
                    var bombCollider = bomb.GetComponent<Collider2D>();
                    if (bombCollider != null && bombCollider.isTrigger)
                        continue;
                }
            }

            return true;
        }

        return false;
    }

    protected bool IsBlocked(Vector2 targetPosition)
    {
        return IsBlockedAtPosition(targetPosition, direction);
    }

    private bool IsBlockedAtPosition(Vector2 targetPosition, Vector2 dirForSize)
    {
        Vector2 size =
            Mathf.Abs(dirForSize.x) > 0f
                ? new Vector2(tileSize * 0.6f, tileSize * 0.2f)
                : new Vector2(tileSize * 0.2f, tileSize * 0.6f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(targetPosition, size, 0f, obstacleMask);
        if (hits == null || hits.Length == 0)
            return false;

        bool canPassDestructibles = abilitySystem != null &&
                                   abilitySystem.IsEnabled(DestructiblePassAbility.AbilityId);

        for (int h = 0; h < hits.Length; h++)
        {
            var hit = hits[h];
            if (hit == null) continue;
            if (hit.gameObject == gameObject) continue;
            if (hit.isTrigger) continue;
            if (canPassDestructibles && hit.CompareTag("Destructibles")) continue;

            for (int i = 0; i < movementAbilities.Length; i++)
            {
                var ability = movementAbilities[i];
                if (ability != null && ability.IsEnabled)
                {
                    if (ability.TryHandleBlockedHit(hit, dirForSize, tileSize, obstacleMask))
                        return true;
                }
            }

            return true;
        }

        return false;
    }

    protected void SetDirection(Vector2 newDirection, AnimatedSpriteRenderer spriteRenderer)
    {
        if (visualOverrideActive)
            return;

        if (IsRidingPlaying())
            return;

        direction = newDirection;

        if (newDirection != Vector2.zero)
            facingDirection = newDirection;

        if (spriteRenderer == null)
        {
            if (activeSpriteRenderer != null)
            {
                activeSpriteRenderer.idle = (direction == Vector2.zero);
                activeSpriteRenderer.RefreshFrame();
            }
            return;
        }

        bool isIdle = (direction == Vector2.zero);

        if (activeSpriteRenderer != spriteRenderer)
        {
            if (activeSpriteRenderer != null)
                SetAnimEnabled(activeSpriteRenderer, false);

            spriteRenderer.idle = isIdle;
            SetAnimEnabled(spriteRenderer, true);
            activeSpriteRenderer = spriteRenderer;
        }
        else
        {
            activeSpriteRenderer.idle = isIdle;
            SetAnimEnabled(activeSpriteRenderer, true);
        }

        activeSpriteRenderer.RefreshFrame();
        ApplyFlipForHorizontal(facingDirection);
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        RegisterHazard(other);
        TryApplyHazardDamage(other);
    }

    protected virtual void OnTriggerStay2D(Collider2D other)
    {
        RegisterHazard(other);
        TryApplyHazardDamage(other);
    }

    protected virtual void OnTriggerExit2D(Collider2D other)
    {
        if (other == null)
            return;

        touchingHazards.Remove(other);
    }

    private void RegisterHazard(Collider2D other)
    {
        if (other == null)
            return;

        int layer = other.gameObject.layer;
        if (layer != explosionLayer && layer != enemyLayer)
            return;

        touchingHazards.Add(other);
    }

    private void TryApplyHazardDamage(Collider2D other)
    {
        if (isDead || isEndingStage)
            return;

        if (other == null)
            return;

        int layer = other.gameObject.layer;
        if (layer != explosionLayer && layer != enemyLayer)
            return;

        if (Time.time < nextContactDamageTime)
            return;

        if (layer == explosionLayer && explosionInvulnerable)
            return;

        if (cachedHealth == null)
            cachedHealth = GetComponent<CharacterHealth>();

        if (cachedHealth != null && cachedHealth.IsInvulnerable)
            return;

        float cd = Mathf.Max(0.01f, contactDamageCooldownSeconds);

        if (IsPlayer() && IsRidingPlaying())
        {
            if (cachedCompanion == null)
                TryGetComponent(out cachedCompanion);

            if (cachedCompanion != null)
            {
                cachedCompanion.HandleDamageWhileMounting(1);
                nextContactDamageTime = Time.time + cd;
                return;
            }
        }

        if (IsPlayer() && isMountedOnLouie)
        {
            var mountedHealth = GetMountedLouieHealth();
            if (mountedHealth != null && mountedHealth.IsInvulnerable)
                return;

            if (cachedCompanion == null)
                TryGetComponent(out cachedCompanion);

            if (cachedCompanion != null)
            {
                bool fromExplosion = (layer == explosionLayer);
                cachedCompanion.OnMountedLouieHit(1, fromExplosion);
                nextContactDamageTime = Time.time + cd;
                return;
            }

            if (cachedHealth != null)
            {
                cachedHealth.TakeDamage(1);
                nextContactDamageTime = Time.time + cd;
                return;
            }

            Kill();
            nextContactDamageTime = Time.time + cd;
            return;
        }

        if (cachedHealth != null)
        {
            cachedHealth.TakeDamage(1);
            nextContactDamageTime = Time.time + cd;
            return;
        }

        DeathSequence();
        nextContactDamageTime = Time.time + cd;
    }

    private CharacterHealth GetMountedLouieHealth()
    {
        var louieMove = GetComponentInChildren<LouieMovementController>(true);
        if (louieMove == null)
            return null;

        return louieMove.GetComponent<CharacterHealth>();
    }

    public virtual void Kill()
    {
        if (isEndingStage)
            return;

        if (!isDead)
            DeathSequence();
    }

    protected virtual void DeathSequence()
    {
        if (isDead || isEndingStage)
            return;

        isDead = true;
        inputLocked = true;

        if (stunReceiver != null)
            stunReceiver.CancelStunForDeath();

        if (IsPlayer())
        {
            TryGetComponent(out CharacterHealth health);
            PlayerPersistentStats.SavePermanentFrom(playerId, this, bombController, health);
        }

        if (IsPlayer() && checkWinStateOnDeath)
        {
            var gm = FindFirstObjectByType<GameManager>();
            if (gm != null)
                gm.NotifyPlayerDeathStarted();
        }

        touchingHazards.Clear();

        if (abilitySystem != null)
            abilitySystem.DisableAll();

        if (IsPlayer())
            PlayerPersistentStats.ResetTemporaryPowerups(playerId);

        if (bombController != null)
            bombController.enabled = false;

        if (Rigidbody != null)
        {
            Rigidbody.linearVelocity = Vector2.zero;
            Rigidbody.simulated = false;
        }

        if (TryGetComponent(out Collider2D col) && col != null)
            col.enabled = false;

        if (audioSource != null && deathSfx != null)
            audioSource.PlayOneShot(deathSfx);

        DisableAllFootSprites();
        DisableAllMountedSprites();

        SetAnimEnabled(spriteRendererCheering, false);
        SetAnimEnabled(spriteRendererEndStage, false);

        if (spriteRendererDeath != null)
        {
            SetAnimEnabled(spriteRendererDeath, true);
            spriteRendererDeath.idle = false;
            spriteRendererDeath.loop = false;
            activeSpriteRenderer = spriteRendererDeath;
            spriteRendererDeath.RefreshFrame();
        }
        else if (activeSpriteRenderer != null)
        {
            SetAnimEnabled(activeSpriteRenderer, true);
            activeSpriteRenderer.idle = true;
            activeSpriteRenderer.loop = false;
            activeSpriteRenderer.RefreshFrame();
        }

        Invoke(nameof(OnDeathSequenceEnded), deathDisableSeconds);
    }

    protected virtual void OnDeathSequenceEnded()
    {
        Died?.Invoke(this);

        gameObject.SetActive(false);

        if (CompareTag("BossBomber"))
            return;

        if (!checkWinStateOnDeath)
            return;
    }

    public void PlayEndStageSequence(Vector2 portalCenter, bool snapToPortalCenter)
    {
        isEndingStage = true;
        SetExplosionInvulnerable(true);

        if (abilitySystem != null)
            abilitySystem.DisableAll();

        inputLocked = true;

        if (bombController != null)
            bombController.enabled = false;

        if (Rigidbody != null)
        {
            Rigidbody.linearVelocity = Vector2.zero;

            if (snapToPortalCenter)
                Rigidbody.position = portalCenter;
        }

        direction = Vector2.zero;
        hasInput = false;

        DisableAllFootSprites();
        DisableAllMountedSprites();

        SetAnimEnabled(spriteRendererDeath, false);
        SetAnimEnabled(spriteRendererCheering, false);
        SetAnimEnabled(spriteRendererEndStage, false);

        if (isMountedOnLouie)
        {
            facingDirection = Vector2.down;

            var mountedDown = mountedSpriteDown != null ? mountedSpriteDown : spriteRendererDown;

            if (mountedDown != null)
            {
                SetAnimEnabled(mountedDown, true);
                mountedDown.idle = true;
                mountedDown.loop = false;
                mountedDown.RefreshFrame();
                activeSpriteRenderer = mountedDown;
            }

            if (cachedCompanion == null)
                TryGetComponent(out cachedCompanion);

            if (cachedCompanion != null)
                cachedCompanion.TryPlayMountedLouieEndStage(endStageTotalTime, endStageFrameCount);

            return;
        }

        var endSprite = spriteRendererEndStage != null ? spriteRendererEndStage : spriteRendererDown;

        if (endSprite != null)
        {
            SetAnimEnabled(endSprite, true);
            endSprite.idle = false;
            endSprite.loop = false;

            if (endStageFrameCount > 0)
                endSprite.animationTime = endStageTotalTime / endStageFrameCount;

            activeSpriteRenderer = endSprite;
        }
    }

    public void SetInputLocked(bool locked, bool forceIdle)
    {
        inputLocked = locked;

        if (locked && forceIdle)
            ForceIdleUp();
    }

    public void SetInputLocked(bool locked)
    {
        SetInputLocked(locked, true);
    }

    public void ForceIdleUp()
    {
        direction = Vector2.zero;
        hasInput = false;

        if (spriteRendererUp != null)
            SetDirection(Vector2.zero, spriteRendererUp);
        else
            SetDirection(Vector2.zero, activeSpriteRenderer);
    }

    public void ForceIdleUpConsideringMount()
    {
        direction = Vector2.zero;
        hasInput = false;

        facingDirection = Vector2.up;

        if (isMountedOnLouie)
        {
            var up = mountedSpriteUp != null ? mountedSpriteUp : spriteRendererUp;
            SetDirection(Vector2.zero, up);

            var rider = GetComponentInChildren<LouieVisualController>(true);
            if (rider != null)
                rider.ForceIdleUp();
        }
        else
        {
            if (spriteRendererUp != null)
                SetDirection(Vector2.zero, spriteRendererUp);
            else
                SetDirection(Vector2.zero, activeSpriteRenderer);
        }
    }

    public void ForceMountedUpExclusive()
    {
        direction = Vector2.zero;
        hasInput = false;

        facingDirection = Vector2.up;

        if (!isMountedOnLouie)
        {
            ForceIdleUp();
            return;
        }

        DisableAllFootSprites();
        DisableAllMountedSprites();

        var up = mountedSpriteUp != null ? mountedSpriteUp : spriteRendererUp;

        if (up != null)
        {
            SetAnimEnabled(up, true);
            up.idle = true;
            up.loop = false;
            up.RefreshFrame();
            activeSpriteRenderer = up;
        }

        var rider = GetComponentInChildren<LouieVisualController>(true);
        if (rider != null)
            rider.ForceOnlyUpEnabled();
    }

    public void SetExplosionInvulnerable(bool value)
    {
        explosionInvulnerable = value;
    }

    public void StartCheering()
    {
        if (isDead)
            return;

        SetExplosionInvulnerable(true);
        inputLocked = true;

        if (bombController != null)
            bombController.enabled = false;

        direction = Vector2.zero;
        hasInput = false;

        if (Rigidbody != null)
            Rigidbody.linearVelocity = Vector2.zero;

        DisableAllFootSprites();
        DisableAllMountedSprites();

        SetAnimEnabled(spriteRendererDeath, false);
        SetAnimEnabled(spriteRendererEndStage, false);

        if (spriteRendererCheering != null)
        {
            SetAnimEnabled(spriteRendererCheering, true);
            spriteRendererCheering.idle = false;
            spriteRendererCheering.loop = true;
            activeSpriteRenderer = spriteRendererCheering;
        }
    }

    protected void ApplyFlipForHorizontal(Vector2 dir)
    {
        if (activeSpriteRenderer == null)
            return;

        if (!activeSpriteRenderer.TryGetComponent(out SpriteRenderer sr) || sr == null)
            return;

        if (!activeSpriteRenderer.allowFlipX)
        {
            sr.flipX = false;
            return;
        }

        if (dir == Vector2.right) sr.flipX = true;
        else if (dir == Vector2.left) sr.flipX = false;
    }

    public void SetVisualOverrideActive(bool active)
    {
        visualOverrideActive = active;

        if (visualOverrideActive)
        {
            SetAllSpritesVisible(false);
            return;
        }

        if (isDead || isEndingStage || IsRidingPlaying())
            return;

        EnableExclusiveFromState();
    }

    public void SetMountedOnLouie(bool mounted)
    {
        isMountedOnLouie = mounted;

        if (IsRidingPlaying())
        {
            DisableAllFootSprites();
            DisableAllMountedSprites();
            return;
        }

        if (mounted)
        {
            DisableAllFootSprites();
            DisableAllMountedSprites();

            if (facingDirection == Vector2.zero)
                facingDirection = Vector2.down;

            direction = Vector2.zero;
            hasInput = false;

            activeSpriteRenderer = null;
            ForceExclusiveSpriteFromState();

            DisableAllFootSprites();
            return;
        }

        if (mountedSpritesYOverridden)
            SetMountedSpritesLocalYOverride(false, 0f);

        DisableAllMountedSprites();
        ApplyDirectionFromVector(direction);
    }

    public void SetMountedSpritesLocalYOverride(bool enable, float localY)
    {
        if (enable)
        {
            ApplyMountedSpritesLocalY(localY);
            mountedSpritesYOverridden = true;
            return;
        }

        ClearMountedSpritesLocalYOverride();
        mountedSpritesYOverridden = false;
    }

    private void ApplyMountedSpritesLocalY(float localY)
    {
        if (mountedSpriteUp != null) mountedSpriteUp.SetRuntimeBaseLocalY(localY);
        if (mountedSpriteDown != null) mountedSpriteDown.SetRuntimeBaseLocalY(localY);
        if (mountedSpriteLeft != null) mountedSpriteLeft.SetRuntimeBaseLocalY(localY);
        if (mountedSpriteRight != null) mountedSpriteRight.SetRuntimeBaseLocalY(localY);
    }

    private void ClearMountedSpritesLocalYOverride()
    {
        if (mountedSpriteUp != null) mountedSpriteUp.ClearRuntimeBaseOffset();
        if (mountedSpriteDown != null) mountedSpriteDown.ClearRuntimeBaseOffset();
        if (mountedSpriteLeft != null) mountedSpriteLeft.ClearRuntimeBaseOffset();
        if (mountedSpriteRight != null) mountedSpriteRight.ClearRuntimeBaseOffset();
    }

    private void ForceExclusiveSpriteFromState()
    {
        if (isDead)
            return;

        if (IsRidingPlaying())
        {
            DisableAllFootSprites();
            DisableAllMountedSprites();
            return;
        }

        DisableAllFootSprites();
        DisableAllMountedSprites();

        Vector2 face = facingDirection;
        if (face == Vector2.zero)
            face = Vector2.down;

        AnimatedSpriteRenderer target = isMountedOnLouie ? PickMountedRenderer(face) : PickFootRenderer(face);

        activeSpriteRenderer = null;
        SetDirection(Vector2.zero, target);
    }

    private bool IsForwardOpen(Vector2 pos, Vector2 moveDir)
    {
        if (moveDir == Vector2.zero)
            return true;

        Vector2 ahead = pos + moveDir.normalized * (tileSize * 0.5f);
        return !IsBlockedAtPosition(ahead, moveDir);
    }

    protected bool CanAlignToPerpendicularTarget(Vector2 candidatePos, Vector2 moveDir)
    {
        if (IsBlockedAtPosition(candidatePos, moveDir))
            return false;

        if (!IsForwardOpen(candidatePos, moveDir))
            return false;

        return true;
    }

    protected bool IsMoveBlocked(Vector2 dir)
    {
        dir = NormalizeCardinal(dir);
        if (dir == Vector2.zero)
            return false;

        Vector2 pos = (Rigidbody != null) ? Rigidbody.position : (Vector2)transform.position;

        float halfStep = tileSize * 0.5f;
        Vector2 probe = pos + dir * halfStep;

        return IsBlockedAtPosition(probe, dir);
    }

    protected bool IsMoveOpen(Vector2 dir) => !IsMoveBlocked(dir);

    protected static Vector2 NormalizeCardinal(Vector2 dir)
    {
        if (dir.sqrMagnitude <= 0.01f)
            return Vector2.zero;

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return new Vector2(Mathf.Sign(dir.x), 0f);

        return new Vector2(0f, Mathf.Sign(dir.y));
    }

    public void SnapToWorldPoint(Vector2 worldPos, bool roundToGrid = false)
    {
        if (roundToGrid && tileSize > 0.0001f)
        {
            worldPos = new Vector2(
                Mathf.Round(worldPos.x / tileSize) * tileSize,
                Mathf.Round(worldPos.y / tileSize) * tileSize
            );
        }

        if (Rigidbody != null)
        {
            Rigidbody.linearVelocity = Vector2.zero;
            Rigidbody.position = worldPos;
        }
        else
        {
            transform.position = worldPos;
        }
    }

    public void SnapToColliderCenter(Collider2D col, bool roundToGrid = false)
    {
        if (col == null)
            return;

        Vector2 center = col.bounds.center;
        SnapToWorldPoint(center, roundToGrid);
    }
}
