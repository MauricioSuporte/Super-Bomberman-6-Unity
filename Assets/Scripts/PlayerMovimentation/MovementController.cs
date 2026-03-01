using System;
using System.Collections;
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
    private int abilitySystemVersion;

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

    [Header("Mounted On Louie - Head Only (optional)")]
    [SerializeField] private AnimatedSpriteRenderer headOnlyUp;
    [SerializeField] private AnimatedSpriteRenderer headOnlyDown;
    [SerializeField] private AnimatedSpriteRenderer headOnlyLeft;
    [SerializeField] private AnimatedSpriteRenderer headOnlyRight;

    [Header("Mounted On Louie - Use HeadOnly When Mounted (default)")]
    [SerializeField] private bool useHeadOnlyWhenMountedDefault = false;

    private bool useHeadOnlyWhenMountedRuntime;

    [Header("Mounted On Louie - Pink Y Override")]
    public float pinkMountedSpritesLocalY = 1.3f;

    [Header("Contact Damage")]
    public float contactDamageCooldownSeconds = 0.15f;

    [Header("Death Timing")]
    public float deathDisableSeconds = 2f;

    [Header("Death Behavior")]
    public bool checkWinStateOnDeath = true;

    [Header("Hole Death State")]
    [SerializeField] private bool holeDeathInProgress;
    public bool IsHoleDeathInProgress => holeDeathInProgress;

    [Header("Hole Death SFX")]
    [SerializeField] private AudioClip holeDeathSfx;
    [SerializeField, Range(0f, 1f)] private float holeDeathSfxVolume = 1f;

    [Header("Hole Death Visual (Sink + Black)")]
    [SerializeField] private bool useHoleDeathSinkVisual = true;
    [SerializeField, Min(0.05f)] private float holeDeathSinkSeconds = 0.55f;
    [SerializeField] private AnimationCurve holeDeathSinkCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Coroutine _holeDeathVisualRoutine;

    [Header("End Stage Animation")]
    public float endStageTotalTime = 1f;
    public int endStageFrameCount = 9;

    [Header("Visual Override (external)")]
    [SerializeField] private bool visualOverrideActive;
    public bool VisualOverrideActive => visualOverrideActive;

    [Header("Visual Suppress (external)")]
    [SerializeField] private bool externalVisualSuppressed;
    private SpriteUpdateLock spriteLock;
    private bool IsSpriteLocked => spriteLock != null && spriteLock.IsLocked;

    [Header("Grid Alignment")]
    [SerializeField, Range(0.5f, 20f)] private float perpendicularAlignMultiplier = 8f;
    [SerializeField] private bool snapPerpendicularOnAxisStart = true;
    [SerializeField, Range(0.0001f, 0.05f)] private float alignEpsilon = 0.0015f;

    [Header("Axis Lock")]
    [SerializeField] private bool enableCorridorAxisLock = true;

    [SerializeField, Range(0.0001f, 0.25f)] private float corridorCenterEpsilon = 0.06f;

    [SerializeField, Range(0.2f, 1.2f)] private float corridorSolidProbeSizeMul = 0.9f;

    [Header("Special Pass (by Tag)")]
    [SerializeField] private bool canPassTaggedObstacles;
    [SerializeField] private string passObstacleTag = "Water";

    [Header("Pixel Perfect Step (Option C)")]
    [SerializeField, Min(1)] private int pixelsPerUnit = 16;
    [SerializeField] private bool useIntegerPixelSteps = true;

    private float accPixelsX;
    private float accPixelsY;

    private Vector2 lastMoveDirCardinal = Vector2.zero;

    private float PixelWorldStep => (pixelsPerUnit > 0) ? (1f / pixelsPerUnit) : 0.0625f;

    [Header("Inactivity (external)")]
    [SerializeField] private bool suppressInactivityAnimation;
    public bool SuppressInactivityAnimation => suppressInactivityAnimation;

    public void SetPassTaggedObstacles(bool canPass, string tag)
    {
        canPassTaggedObstacles = canPass;
        passObstacleTag = string.IsNullOrWhiteSpace(tag) ? "Water" : tag;
    }

    public Rigidbody2D Rigidbody { get; private set; }
    public Vector2 Direction => direction;

    protected Vector2 facingDirection = Vector2.down;
    public Vector2 FacingDirection => facingDirection;

    private bool isMounted;
    public bool IsMountedOnLouie => isMounted;

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
    private PlayerMountCompanion cachedCompanion;
    private PlayerRidingController cachedRiding;

    protected AnimatedSpriteRenderer activeSpriteRenderer;

    private IMovementAbility[] movementAbilities = Array.Empty<IMovementAbility>();
    private int explosionLayer;
    private int enemyLayer;

    private bool inactivityMountedDownOverride;

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
        abilitySystemVersion = abilitySystem != null ? abilitySystem.Version : 0;

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
        TryGetComponent(out spriteLock);

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

        CacheMovementAbilities();
        SyncMountedFromPersistent();

        useHeadOnlyWhenMountedRuntime = useHeadOnlyWhenMountedDefault;

        if (IsPlayer() && isMounted)
        {
            if (cachedCompanion == null)
                TryGetComponent(out cachedCompanion);

            if (cachedCompanion != null)
                useHeadOnlyWhenMountedRuntime = cachedCompanion.GetUseHeadOnlyPlayerVisual();
        }

        if (activeSpriteRenderer == null)
            activeSpriteRenderer = spriteRendererDown;

        ApplySpeedInternal(speedInternal);

        if (!IsSpriteLocked)
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
        bool before = isMounted;
        isMounted = st.MountedLouie != MountedType.None;

        if (facingDirection == Vector2.zero)
            facingDirection = Vector2.down;
    }

    private bool IsPlayer() => CompareTag("Player");

    public bool IsRidingPlaying()
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
        SetMany(false,
            mountedSpriteUp, mountedSpriteDown, mountedSpriteLeft, mountedSpriteRight,
            headOnlyUp, headOnlyDown, headOnlyLeft, headOnlyRight);
    }

    public void SetAllSpritesVisible(bool visible)
    {
        SetMany(visible,
            spriteRendererUp, spriteRendererDown, spriteRendererLeft, spriteRendererRight,
            spriteRendererDeath, spriteRendererEndStage, spriteRendererCheering,
            mountedSpriteUp, mountedSpriteDown, mountedSpriteLeft, mountedSpriteRight,
            headOnlyUp, headOnlyDown, headOnlyLeft, headOnlyRight);

        var rider = GetComponentInChildren<MountVisualController>(true);
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
        if (visualOverrideActive || inactivityMountedDownOverride || IsSpriteLocked)
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
        if (inputLocked || GamePauseController.IsPaused || isDead)
            return;

        SyncMovementAbilitiesFromAbilitySystemIfChanged();

        if (stunReceiver != null && stunReceiver.IsStunned)
        {
            hasInput = false;
            direction = Vector2.zero;
            return;
        }

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

        if (inactivityMountedDownOverride)
            return;

        hasInput = dir != Vector2.zero;

        direction = dir;
        if (dir != Vector2.zero)
            facingDirection = dir;

        if (externalVisualSuppressed || visualOverrideActive)
            return;

        if (IsSpriteLocked)
            return;

        if (isMounted)
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

        bool useHead =
            useHeadOnlyWhenMountedRuntime &&
            (headOnlyUp != null || headOnlyDown != null || headOnlyLeft != null || headOnlyRight != null);

        if (useHead)
        {
            if (face == Vector2.up) return headOnlyUp != null ? headOnlyUp : (mountedSpriteUp != null ? mountedSpriteUp : spriteRendererUp);
            if (face == Vector2.down) return headOnlyDown != null ? headOnlyDown : (mountedSpriteDown != null ? mountedSpriteDown : spriteRendererDown);
            if (face == Vector2.left) return headOnlyLeft != null ? headOnlyLeft : (mountedSpriteLeft != null ? mountedSpriteLeft : spriteRendererLeft);
            if (face == Vector2.right) return headOnlyRight != null ? headOnlyRight : (mountedSpriteRight != null ? mountedSpriteRight : spriteRendererRight);

            return headOnlyDown != null ? headOnlyDown : (mountedSpriteDown != null ? mountedSpriteDown : spriteRendererDown);
        }

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

    private bool IsSolidAtCustom(Vector2 worldPosition, float sizeMul)
    {
        Vector2 size = Vector2.one * (tileSize * sizeMul);

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

            if (canPassTaggedObstacles && hit.CompareTag(passObstacleTag))
                continue;

            if (canPassDestructibles && hit.CompareTag("Destructibles"))
                continue;

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

    protected virtual void FixedUpdate()
    {
        if (ShouldSkipFixedUpdate())
            return;

        float rawMoveWorld = GetRawMoveWorldPerFixedFrame();

        float moveWorld = GetQuantizedMoveWorldPerFixedFrame(direction, rawMoveWorld);

        Vector2 position = Rigidbody.position;
        position = QuantizeToPixelGrid(position);

        bool movingVertical = Mathf.Abs(direction.y) > 0.01f;
        bool movingHorizontal = Mathf.Abs(direction.x) > 0.01f;

        UpdateCurrentAxis(movingHorizontal, movingVertical);

        AlignPerpendicularForCurrentAxis(ref position, rawMoveWorld);

        if (enableCorridorAxisLock && tileSize > 0.0001f)
        {
            if (TryApplyCorridorAxisLock(position, movingHorizontal, movingVertical, rawMoveWorld))
                return;
        }

        ApplyCenteringWhenSqueezed(ref position, rawMoveWorld, movingHorizontal, movingVertical);

        if (moveWorld <= 0f)
        {
            MovePositionPixelPerfect(position);
            return;
        }

        Vector2 targetPosition = position + direction * moveWorld;
        targetPosition = QuantizeToPixelGrid(targetPosition);

        if (!IsBlocked(targetPosition))
        {
            MovePositionPixelPerfect(targetPosition);
            return;
        }

        TrySlideIfBlocked(position, moveWorld, movingHorizontal, movingVertical);
    }

    private bool ShouldSkipFixedUpdate()
    {
        if (externalMovementOverride)
        {
            if (Rigidbody != null)
                Rigidbody.linearVelocity = Vector2.zero;

            currentAxis = MoveAxis.None;
            return true;
        }

        if (inputLocked || GamePauseController.IsPaused || isDead)
            return true;

        SyncMovementAbilitiesFromAbilitySystemIfChanged();

        if (!hasInput || direction == Vector2.zero)
        {
            currentAxis = MoveAxis.None;
            return true;
        }

        if (stunReceiver != null && stunReceiver.IsStunned)
        {
            if (Rigidbody != null)
                Rigidbody.linearVelocity = Vector2.zero;

            currentAxis = MoveAxis.None;
            return true;
        }

        return false;
    }

    private void UpdateCurrentAxis(bool movingHorizontal, bool movingVertical)
    {
        MoveAxis newAxis = movingHorizontal
            ? MoveAxis.Horizontal
            : (movingVertical ? MoveAxis.Vertical : MoveAxis.None);

        currentAxis = newAxis;
    }

    private void AlignPerpendicularForCurrentAxis(ref Vector2 position, float moveSpeed)
    {
        if (currentAxis == MoveAxis.Horizontal)
        {
            AlignPerpendicular(ref position, axisIsHorizontal: true, moveSpeed: moveSpeed,
                snapImmediate: snapPerpendicularOnAxisStart);
        }
        else if (currentAxis == MoveAxis.Vertical)
        {
            AlignPerpendicular(ref position, axisIsHorizontal: false, moveSpeed: moveSpeed,
                snapImmediate: snapPerpendicularOnAxisStart);
        }
    }

    private bool TryApplyCorridorAxisLock(Vector2 position, bool movingHorizontal, bool movingVertical, float moveSpeed)
    {
        float x0 = TileFloor(position.x, tileSize);
        float x1 = TileCeil(position.x, tileSize);
        float y0 = TileFloor(position.y, tileSize);
        float y1 = TileCeil(position.y, tileSize);

        float cx = Mathf.Round(position.x / tileSize) * tileSize;
        float cy = Mathf.Round(position.y / tileSize) * tileSize;

        if (movingHorizontal)
        {
            bool lrRow0 = SolidLRAt(cx, y0);
            bool lrRow1 = SolidLRAt(cx, y1);

            bool corridorVertical = (lrRow0 || lrRow1);
            if (!corridorVertical)
                return false;

            SnapAndStop(axisX: true, position, new Vector2(cx, position.y), moveSpeed);
            return true;
        }

        if (movingVertical)
        {
            bool udCol0 = SolidUDAt(x0, cy);
            bool udCol1 = SolidUDAt(x1, cy);

            bool corridorHorizontal = (udCol0 || udCol1);
            if (!corridorHorizontal)
                return false;

            SnapAndStop(axisX: false, position, new Vector2(position.x, cy), moveSpeed);
            return true;
        }

        return false;
    }

    private void SnapAndStop(bool axisX, Vector2 currentPos, Vector2 snapTarget, float moveSpeed)
    {
        if (Rigidbody != null)
            Rigidbody.linearVelocity = Vector2.zero;

        direction = Vector2.zero;
        hasInput = false;
        currentAxis = MoveAxis.None;

        float snapStep = moveSpeed * Mathf.Max(1f, perpendicularAlignMultiplier);

        Vector2 snappedPos;
        if (axisX)
        {
            float newX = Mathf.MoveTowards(currentPos.x, snapTarget.x, snapStep);
            snappedPos = new Vector2(newX, currentPos.y);
        }
        else
        {
            float newY = Mathf.MoveTowards(currentPos.y, snapTarget.y, snapStep);
            snappedPos = new Vector2(currentPos.x, newY);
        }

        MovePositionPixelPerfect(snappedPos);
    }

    private void ApplyCenteringWhenSqueezed(ref Vector2 position, float moveSpeed, bool movingHorizontal, bool movingVertical)
    {
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
    }

    private void TrySlideIfBlocked(Vector2 position, float moveSpeed, bool movingHorizontal, bool movingVertical)
    {
        if (movingVertical)
        {
            float centerX = Mathf.Round(position.x / tileSize) * tileSize;
            float offsetX = Mathf.Abs(position.x - centerX);

            if (offsetX > SlideDeadZone)
                TrySlideHorizontally(position, moveSpeed);

            return;
        }

        if (movingHorizontal)
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
            MovePositionPixelPerfect(new Vector2(newX, position.y));
        }
        else
        {
            Vector2 newPos = new Vector2(targetX, position.y) + verticalStep;
            if (!IsBlocked(newPos))
                MovePositionPixelPerfect(newPos);
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
            MovePositionPixelPerfect(new Vector2(position.x, newY));
        }
        else
        {
            Vector2 newPos = new Vector2(position.x, targetY) + horizontalStep;
            if (!IsBlocked(newPos))
                MovePositionPixelPerfect(newPos);
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

            if (canPassTaggedObstacles && hit.CompareTag(passObstacleTag))
                continue;

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

            if (canPassTaggedObstacles && hit.CompareTag(passObstacleTag))
                continue;

            if (canPassDestructibles && hit.CompareTag("Destructibles"))
                continue;

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
        if (visualOverrideActive || inactivityMountedDownOverride)
            return;

        if (IsRidingPlaying())
            return;

        direction = newDirection;
        if (newDirection != Vector2.zero)
            facingDirection = newDirection;

        if (IsSpriteLocked)
            return;

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
        if (externalMovementOverride)
            return;

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

        if (IsPlayer() && isMounted)
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
        var louieMove = GetComponentInChildren<MountMovementController>(true);
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

    public void KillByHole()
    {
        if (isEndingStage)
            return;

        if (!isDead)
        {
            var glove = GetComponent<PowerGloveAbility>();
            if (glove != null && glove.IsEnabled)
                glove.TryDestroyHeldBombByHoleUsingBombController();

            HoleDeathSequence();
        }
    }

    private void BeginDeathCommon()
    {
        isDead = true;
        inputLocked = true;
        inactivityMountedDownOverride = false;

        if (spriteLock != null && spriteLock.IsLocked)
            spriteLock.EndLock();

        if (stunReceiver != null)
            stunReceiver.CancelStunForDeath();

        if (IsPlayer() && checkWinStateOnDeath)
        {
            var gm = FindFirstObjectByType<GameManager>();
            if (gm != null)
                gm.NotifyPlayerDeathStarted();
        }

        touchingHazards.Clear();

        if (IsPlayer())
        {
            PlayerPersistentStats.StageResetTemporaryPowerupsOnDeath(playerId);
            PlayerPersistentStats.ResetTemporaryPowerups(playerId);

            if (TryGetComponent<MountEggQueue>(out var q) && q != null)
                q.ClearQueueNow(resetHistoryToOwner: true, animateShift: false);
        }

        if (abilitySystem != null)
            abilitySystem.DisableAll();

        if (bombController != null)
            bombController.enabled = false;

        if (Rigidbody != null)
        {
            Rigidbody.linearVelocity = Vector2.zero;
            Rigidbody.simulated = false;
        }

        if (TryGetComponent(out Collider2D col) && col != null)
            col.enabled = false;
    }

    protected virtual void DeathSequence()
    {
        if (isDead || isEndingStage)
            return;

        holeDeathInProgress = false;

        BeginDeathCommon();

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

    private void HoleDeathSequence()
    {
        if (isDead || isEndingStage)
            return;

        holeDeathInProgress = true;

        BeginDeathCommon();

        PlayHoleDeathSfx();

        DisableAllFootSprites();
        DisableAllMountedSprites();

        SetAnimEnabled(spriteRendererCheering, false);
        SetAnimEnabled(spriteRendererEndStage, false);
        SetAnimEnabled(spriteRendererDeath, false);

        var r = PickRendererForHoleDeathVisual();
        activeSpriteRenderer = r;

        if (useHoleDeathSinkVisual && r != null)
        {
            if (_holeDeathVisualRoutine != null)
                StopCoroutine(_holeDeathVisualRoutine);

            _holeDeathVisualRoutine = StartCoroutine(HoleDeathSinkVisualRoutine(r));
        }
        else if (r != null)
        {
            SetAnimEnabled(r, true);
            r.idle = true;
            r.loop = false;
            r.RefreshFrame();
        }

        Invoke(nameof(OnDeathSequenceEnded), deathDisableSeconds);
    }

    private void PlayHoleDeathSfx()
    {
        if (audioSource == null || holeDeathSfx == null)
            return;

        if (audioSource.isPlaying)
            audioSource.Stop();

        audioSource.clip = holeDeathSfx;
        audioSource.volume = holeDeathSfxVolume;
        audioSource.loop = false;
        audioSource.Play();
    }

    private AnimatedSpriteRenderer PickRendererForHoleDeathVisual()
    {
        if (activeSpriteRenderer != null)
            return activeSpriteRenderer;

        if (isMounted)
        {
            var r = PickMountedRenderer(facingDirection);
            if (r != null) return r;
        }

        return spriteRendererDown != null ? spriteRendererDown : null;
    }

    private IEnumerator HoleDeathSinkVisualRoutine(AnimatedSpriteRenderer r)
    {
        if (r == null)
            yield break;

        SetAnimEnabled(r, true);
        r.idle = true;
        r.loop = false;
        r.pingPong = false;
        r.RefreshFrame();

        var srs = GetComponentsInChildren<SpriteRenderer>(true);
        if (srs == null || srs.Length == 0)
            yield break;

        var startColors = new Color[srs.Length];
        for (int i = 0; i < srs.Length; i++)
            startColors[i] = srs[i] != null ? srs[i].color : Color.white;

        Transform root = transform;
        Vector3 startScale = root.localScale;

        float dur = Mathf.Max(0.05f, holeDeathSinkSeconds);
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / dur);
            float eased = holeDeathSinkCurve != null ? holeDeathSinkCurve.Evaluate(a) : a;

            root.localScale = Vector3.LerpUnclamped(startScale, Vector3.zero, eased);

            for (int i = 0; i < srs.Length; i++)
            {
                var sr = srs[i];
                if (sr == null) continue;

                Color sc = startColors[i];
                Color endColor = new Color(0f, 0f, 0f, sc.a);

                sr.color = Color.LerpUnclamped(sc, endColor, eased);
            }

            yield return null;
        }

        root.localScale = Vector3.zero;

        for (int i = 0; i < srs.Length; i++)
        {
            var sr = srs[i];
            if (sr == null) continue;

            Color sc = startColors[i];
            sr.color = new Color(0f, 0f, 0f, sc.a);
        }
    }

    public void PlayEndStageSequence(Vector2 portalCenter, bool snapToPortalCenter)
    {
        isEndingStage = true;
        SetExplosionInvulnerable(true);

        if (abilitySystem != null)
            abilitySystem.DisableAll();

        inputLocked = true;
        inactivityMountedDownOverride = false;
        if (spriteLock != null && spriteLock.IsLocked)
            spriteLock.EndLock();

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

        if (isMounted)
        {
            facingDirection = Vector2.down;

            var r = PickMountedRenderer(Vector2.down);
            if (r == null)
                r = mountedSpriteDown != null ? mountedSpriteDown : spriteRendererDown;

            if (r != null)
            {
                SetAnimEnabled(r, true);
                r.idle = true;
                r.loop = false;
                r.pingPong = false;
                r.RefreshFrame();
                activeSpriteRenderer = r;
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

    public void SetSuppressInactivityAnimation(bool suppress)
    {
        suppressInactivityAnimation = suppress;

        if (suppressInactivityAnimation)
        {
            SetInactivityMountedDownOverride(false);
            SetVisualOverrideActive(false);
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

        if (isMounted)
        {
            var up = mountedSpriteUp != null ? mountedSpriteUp : spriteRendererUp;
            SetDirection(Vector2.zero, up);

            var rider = GetComponentInChildren<MountVisualController>(true);
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

        if (!isMounted)
        {
            ForceIdleUp();
            return;
        }

        DisableAllFootSprites();
        DisableAllMountedSprites();

        var up = PickMountedRenderer(Vector2.up);
        if (up == null)
            up = mountedSpriteUp != null ? mountedSpriteUp : spriteRendererUp;

        if (up != null)
        {
            SetAnimEnabled(up, true);
            up.idle = true;
            up.loop = false;
            up.RefreshFrame();
            activeSpriteRenderer = up;
        }

        var rider = GetComponentInChildren<MountVisualController>(true);
        if (rider != null)
            rider.ForceOnlyUpEnabled();
    }

    public void SetExplosionInvulnerable(bool value)
    {
        explosionInvulnerable = value;
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
        if (inactivityMountedDownOverride && !active)
            return;

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

    public void SetInactivityMountedDownOverride(bool on)
    {
        if (inactivityMountedDownOverride == on)
            return;

        inactivityMountedDownOverride = on;

        if (on)
        {
            if (!isMounted)
            {
                inactivityMountedDownOverride = false;
                return;
            }

            visualOverrideActive = true;

            SetAllSpritesVisible(false);
            DisableAllFootSprites();
            DisableAllMountedSprites();

            var r = PickMountedRenderer(Vector2.down);
            if (r == null)
                r = mountedSpriteDown != null ? mountedSpriteDown : spriteRendererDown;

            if (r != null)
            {
                SetAnimEnabled(r, true);
                r.idle = true;
                r.loop = false;
                r.pingPong = false;
                activeSpriteRenderer = r;
                r.RefreshFrame();
            }

            return;
        }

        visualOverrideActive = false;

        if (isDead || isEndingStage || IsRidingPlaying())
            return;

        EnableExclusiveFromState();
    }

    public void SetMountedOnLouie(bool mounted)
    {
        isMounted = mounted;

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

        if (IsSpriteLocked)
            return;

        DisableAllFootSprites();
        DisableAllMountedSprites();

        Vector2 face = facingDirection;
        if (face == Vector2.zero)
            face = Vector2.down;

        AnimatedSpriteRenderer target = isMounted ? PickMountedRenderer(face) : PickFootRenderer(face);

        activeSpriteRenderer = null;
        SetDirection(Vector2.zero, target);
    }

    public void ForceFacingDirection(Vector2 faceDir)
    {
        faceDir = NormalizeCardinal(faceDir);
        if (faceDir == Vector2.zero)
            faceDir = Vector2.down;

        facingDirection = faceDir;

        if (isDead || isEndingStage) return;
        if (IsRidingPlaying()) return;
        if (externalVisualSuppressed || visualOverrideActive || IsSpriteLocked) return;

        EnableExclusiveFromState();
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

        worldPos = QuantizeToPixelGrid(worldPos);

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

    private void SyncMovementAbilitiesFromAbilitySystemIfChanged()
    {
        if (abilitySystem == null)
            return;

        int v = abilitySystem.Version;
        if (v == abilitySystemVersion)
            return;

        abilitySystemVersion = v;
        CacheMovementAbilities();
    }

    public void SetExternalVisualSuppressed(bool suppressed)
    {
        externalVisualSuppressed = suppressed;

        if (externalVisualSuppressed)
        {
            SetAllSpritesVisible(false);
        }
        else
        {
            if (isDead || isEndingStage || IsRidingPlaying())
                return;

            EnableExclusiveFromState();
        }
    }

    public void SetUseHeadOnlyWhenMounted(bool useHeadOnly)
    {
        useHeadOnlyWhenMountedRuntime = useHeadOnly;

        if (isDead || isEndingStage) return;
        if (IsRidingPlaying()) return;
        if (externalVisualSuppressed || visualOverrideActive || IsSpriteLocked) return;

        EnableExclusiveFromState();
    }

    public void SetHeadOnlyMountedOffsets(Vector2 up, Vector2 down, Vector2 left, Vector2 right)
    {
        ApplyHeadOnlyOffset(headOnlyUp, up);
        ApplyHeadOnlyOffset(headOnlyDown, down);
        ApplyHeadOnlyOffset(headOnlyLeft, left);
        ApplyHeadOnlyOffset(headOnlyRight, right);

        if (isDead || isEndingStage) return;
        if (IsRidingPlaying()) return;
        if (externalVisualSuppressed || visualOverrideActive || IsSpriteLocked) return;

        EnableExclusiveFromState();
    }

    public void ClearHeadOnlyMountedOffsets()
    {
        ClearHeadOnlyOffset(headOnlyUp);
        ClearHeadOnlyOffset(headOnlyDown);
        ClearHeadOnlyOffset(headOnlyLeft);
        ClearHeadOnlyOffset(headOnlyRight);

        if (isDead || isEndingStage) return;
        if (IsRidingPlaying()) return;
        if (externalVisualSuppressed || visualOverrideActive || IsSpriteLocked) return;

        EnableExclusiveFromState();
    }

    private void ApplyHeadOnlyOffset(AnimatedSpriteRenderer r, Vector2 localOffset)
    {
        if (r == null) return;

        r.SetRuntimeBaseLocalX(localOffset.x);
        r.SetRuntimeBaseLocalY(localOffset.y);
    }

    private void ClearHeadOnlyOffset(AnimatedSpriteRenderer r)
    {
        if (r == null) return;

        r.ClearRuntimeBaseOffset();
    }

    private static float TileFloor(float v, float t) => Mathf.Floor(v / t) * t;
    private static float TileCeil(float v, float t) => Mathf.Ceil(v / t) * t;

    private bool SolidUDAt(float xCenter, float yCenter)
    {
        Vector2 c = new(xCenter, yCenter);
        bool u = IsSolidAtCustom(c + Vector2.up * tileSize, corridorSolidProbeSizeMul);
        bool d = IsSolidAtCustom(c + Vector2.down * tileSize, corridorSolidProbeSizeMul);
        return u && d;
    }

    private bool SolidLRAt(float xCenter, float yCenter)
    {
        Vector2 c = new(xCenter, yCenter);
        bool l = IsSolidAtCustom(c + Vector2.left * tileSize, corridorSolidProbeSizeMul);
        bool r = IsSolidAtCustom(c + Vector2.right * tileSize, corridorSolidProbeSizeMul);
        return l && r;
    }

    private Vector2 QuantizeToPixelGrid(Vector2 world)
    {
        if (!useIntegerPixelSteps || pixelsPerUnit <= 0)
            return world;

        float ppu = pixelsPerUnit;
        return new Vector2(
            Mathf.Round(world.x * ppu) / ppu,
            Mathf.Round(world.y * ppu) / ppu
        );
    }

    private void ResetPixelAccumulators()
    {
        accPixelsX = 0f;
        accPixelsY = 0f;
    }

    private float GetRawMoveWorldPerFixedFrame()
    {
        float dt = Time.fixedDeltaTime;
        float speedWorldPerSecond = speed * tileSize;
        return speedWorldPerSecond * dt;
    }

    private float GetQuantizedMoveWorldPerFixedFrame(Vector2 moveDir, float rawWorldStep)
    {
        if (!useIntegerPixelSteps || pixelsPerUnit <= 0)
            return rawWorldStep;

        moveDir = NormalizeCardinal(moveDir);
        if (moveDir == Vector2.zero)
            return 0f;

        if (moveDir != lastMoveDirCardinal)
        {
            lastMoveDirCardinal = moveDir;
            ResetPixelAccumulators();
        }

        float rawPixels = rawWorldStep * pixelsPerUnit;

        if (Mathf.Abs(moveDir.x) > 0.01f)
        {
            accPixelsX += rawPixels * Mathf.Sign(moveDir.x);

            int whole = (int)accPixelsX;
            accPixelsX -= whole;

            return Mathf.Abs(whole) * PixelWorldStep;
        }
        else
        {
            accPixelsY += rawPixels * Mathf.Sign(moveDir.y);

            int whole = (int)accPixelsY;
            accPixelsY -= whole;

            return Mathf.Abs(whole) * PixelWorldStep;
        }
    }

    private void MovePositionPixelPerfect(Vector2 worldPos)
    {
        if (Rigidbody == null)
            return;

        Rigidbody.MovePosition(QuantizeToPixelGrid(worldPos));
    }

    [SerializeField] private bool externalMovementOverride;
    public bool ExternalMovementOverride => externalMovementOverride;

    public void SetExternalMovementOverride(bool active)
    {
        externalMovementOverride = active;

        if (active)
        {
            direction = Vector2.zero;
            hasInput = false;
            currentAxis = MoveAxis.None;
            lastMoveDirCardinal = Vector2.zero;
            ResetPixelAccumulators();

            if (Rigidbody != null)
            {
                Rigidbody.linearVelocity = Vector2.zero;
            }
        }
        else
        {
            direction = Vector2.zero;
            hasInput = false;
            currentAxis = MoveAxis.None;

            if (Rigidbody != null)
                Rigidbody.linearVelocity = Vector2.zero;
        }
    }
}
