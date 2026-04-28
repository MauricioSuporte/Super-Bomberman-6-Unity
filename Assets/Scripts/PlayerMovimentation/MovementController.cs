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
    [Header("Debug Curva")]
    [SerializeField] private bool debugCurvas;
    [SerializeField] private bool debugCurvasVerbose;
    [SerializeField] private int debugCurvasPlayerId = 1;
    private int _debugLastFixedFrameLogged = -1;

    [Header("Debug Bomb Escape")]
    [SerializeField] private bool debugBombEscape;
    [SerializeField] private bool debugBombEscapeVerbose;

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
    private Coroutine temporarySpeedOverrideRoutine;
    private bool hasTemporarySpeedOverride;
    private int temporarySpeedOverrideRestoreInternal;
    private Coroutine temporarySpeedBlinkRoutine;
    private readonly Dictionary<SpriteRenderer, Color> temporarySpeedBlinkOriginalColors = new();

    [Header("Player Id (only used if tagged Player)")]
    [SerializeField, Range(1, 6)] private int playerId = 1;
    public int PlayerId => playerId;

    [Header("Dual-Input (Zig-Zag / SB1 Style)")]
    [Tooltip("Enable the zig-zag behaviour when two directional buttons are held.")]
    [SerializeField] private bool enableDualInput = true;

    [Tooltip("Distance (world units) from a tile centre on the perpendicular axis that triggers the axis swap.")]
    [SerializeField, Range(0.01f, 0.49f)] private float dualInputCentreSnapTolerance = 0.18f;

    [Tooltip("Seconds to wait after a switch before the next switch can happen. Prevents rapid toggling on the same tile centre.")]
    [SerializeField, Range(0f, 0.5f)] private float dualInputSwitchCooldown = 0.08f;

    private Vector2 dualPrimary = Vector2.zero;
    private Vector2 dualSecondary = Vector2.zero;
    private float dualSwitchTimer = 0f;

    private Vector2 pendingTurnDirection = Vector2.zero;

    private Vector2 lockedMovementDirection = Vector2.zero;
    private Vector2 pendingSingleTurnDirection = Vector2.zero;

    [Header("Sprites")]
    public AnimatedSpriteRenderer spriteRendererUp;
    public AnimatedSpriteRenderer spriteRendererDown;
    public AnimatedSpriteRenderer spriteRendererLeft;
    public AnimatedSpriteRenderer spriteRendererRight;
    public AnimatedSpriteRenderer spriteRendererDeath;
    public AnimatedSpriteRenderer spriteRendererDeathByExplosion;
    public AnimatedSpriteRenderer spriteRendererEndStage;
    public AnimatedSpriteRenderer spriteRendererCheering;
    public AnimatedSpriteRenderer spriteRendererFall;
    public AnimatedSpriteRenderer spriteRendererCornered;
    public AnimatedSpriteRenderer spriteRendererBall;

    [Header("Mounted On Louie")]
    public AnimatedSpriteRenderer mountedSpriteUp;
    public AnimatedSpriteRenderer mountedSpriteDown;
    public AnimatedSpriteRenderer mountedSpriteLeft;
    public AnimatedSpriteRenderer mountedSpriteRight;

    [Header("Mounted On Louie - Head Only")]
    [SerializeField] private AnimatedSpriteRenderer headOnlyUp;
    [SerializeField] private AnimatedSpriteRenderer headOnlyDown;
    [SerializeField] private AnimatedSpriteRenderer headOnlyLeft;
    [SerializeField] private AnimatedSpriteRenderer headOnlyRight;

    [Header("Spring Launcher - Looking Up")]
    [SerializeField] private AnimatedSpriteRenderer springLookUpUp;
    [SerializeField] private AnimatedSpriteRenderer springLookUpDown;
    [SerializeField] private AnimatedSpriteRenderer springLookUpLeft;
    [SerializeField] private AnimatedSpriteRenderer springLookUpRight;

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
    public bool ExternalVisualSuppressed => externalVisualSuppressed;
    private SpriteUpdateLock spriteLock;
    private bool IsSpriteLocked => spriteLock != null && spriteLock.IsLocked;

    [Header("Grid Alignment")]
    [SerializeField, Range(0.5f, 20f)] private float perpendicularAlignMultiplier = 8f;
    [SerializeField, Range(0.0001f, 0.05f)] private float alignEpsilon = 0.0015f;

    [Header("Axis Lock")]
    [SerializeField] private bool enableCorridorAxisLock = true;
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

    [Header("Bomb Early Kick")]
    [Tooltip("Minimum axis distance from the bomb center before a non-solid bomb can kick or hard-block reentry.")]
    [SerializeField, Range(0.05f, 1f)] private float bombKickMinCenterOffset = 0.625f;
    public float BombKickMinCenterOffset => bombKickMinCenterOffset;

    private struct BombPlantTraversalState
    {
        public Vector2 PlantDirection;
        public bool DirectionChangedSincePlant;
    }

    private Bomb _lastAdjKickedBomb;
    private readonly Dictionary<Bomb, BombPlantTraversalState> _ownedBombPlantTraversal = new();
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
    public bool IsMounted => isMounted;

    protected bool deathRequestedByExplosion;
    public bool isDead;
    protected bool inputLocked;
    public bool InputLocked => inputLocked;

    private bool isEndingStage;
    public bool IsEndingStage => isEndingStage;

    protected Vector2 direction = Vector2.zero;
    protected bool hasInput;
    public bool explosionInvulnerable;

    private bool mountedSpritesYOverridden;
    private float nextContactDamageTime;

    private readonly HashSet<Collider2D> touchingHazards = new();

    private const float CenterEpsilon = 0.01f;
    private float SlideDeadZone => tileSize * 0.25f;

    private bool battleRevengeSwapDeathPending;
    private Action battleRevengeSwapDeathCompleted;

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
    private int bombLayer;
    private ContactFilter2D obstacleContactFilter;
    private readonly Collider2D[] obstacleOverlapBuffer = new Collider2D[16];

    private bool inactivityMountedDownOverride;

    [SerializeField] private bool externalMovementOverride;
    public bool ExternalMovementOverride => externalMovementOverride;

    private void SetFacingDirection(Vector2 newFace, string reason)
    {
        facingDirection = newFace;
    }

    public void SetPlayerId(int id)
    {
        playerId = Mathf.Clamp(id, 1, 6);

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
        bombLayer = LayerMask.NameToLayer("Bomb");
        RebuildObstacleContactFilter();

        CacheMovementAbilities();
        InitRuntimeState(loadPersistent: true);
    }

    protected virtual void OnEnable()
    {
        InitRuntimeState(loadPersistent: true);
    }

    protected virtual void OnDisable()
    {
        ClearTemporarySpeedOverride(restoreSpeed: true);
        touchingHazards.Clear();
    }

    private void OnValidate()
    {
        RebuildObstacleContactFilter();

        if (!Application.isPlaying)
            return;
    }

    private void InitRuntimeState(bool loadPersistent)
    {
        direction = Vector2.zero;
        hasInput = false;
        touchingHazards.Clear();
        currentAxis = MoveAxis.None;

        ResetDualInputAxes();
        ResetSingleInputTurnState();

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
        isMounted = st.MountedLouie != MountedType.None;

        if (facingDirection == Vector2.zero)
            SetFacingDirection(Vector2.down, "SyncMountedFromPersistent");
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
        SetMany(false, spriteRendererUp, spriteRendererDown, spriteRendererLeft, spriteRendererRight, spriteRendererBall);
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
            spriteRendererDeath, spriteRendererDeathByExplosion, spriteRendererEndStage, spriteRendererCheering, spriteRendererFall, spriteRendererCornered, spriteRendererBall,
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
            ResetDualInputAxes();
            return;
        }

        var input = PlayerInputManager.Instance;
        if (input == null)
        {
            ApplyDirectionFromVector(Vector2.zero);
            ResetDualInputAxes();
            return;
        }

        bool holdUp = input.Get(playerId, PlayerAction.MoveUp);
        bool holdDown = input.Get(playerId, PlayerAction.MoveDown);
        bool holdLeft = input.Get(playerId, PlayerAction.MoveLeft);
        bool holdRight = input.Get(playerId, PlayerAction.MoveRight);

        Vector2 vertDir = Vector2.zero;
        Vector2 horizDir = Vector2.zero;

        if (holdUp && !holdDown) vertDir = Vector2.up;
        if (holdDown && !holdUp) vertDir = Vector2.down;
        if (holdLeft && !holdRight) horizDir = Vector2.left;
        if (holdRight && !holdLeft) horizDir = Vector2.right;

        int axisCount = (vertDir != Vector2.zero ? 1 : 0) + (horizDir != Vector2.zero ? 1 : 0);

        LogCurve(
            $"HandleInput input U:{holdUp} D:{holdDown} L:{holdLeft} R:{holdRight} " +
            $"vert:{vertDir} horiz:{horizDir} axisCount:{axisCount} " +
            $"dirAtual:{direction} face:{facingDirection} dualPrimary:{dualPrimary} dualSecondary:{dualSecondary} " +
            $"pendingTurn:{pendingTurnDirection} pendingSingle:{pendingSingleTurnDirection}",
            verbose: true);

        if (!enableDualInput || axisCount <= 1)
        {
            Vector2 singleDir = vertDir != Vector2.zero ? vertDir : horizDir;
            bool wasUsingDualInput = HasDualInputState();

            ResetDualInputAxes();

            if (wasUsingDualInput)
            {
                LogCurve(
                    $"DualInput exit -> SingleAxis requested:{singleDir} " +
                    $"currentDir:{direction} pendingSingle:{pendingSingleTurnDirection}");
            }
            else
            {
                LogCurve($"SingleAxis requested:{singleDir} enableDualInput:{enableDualInput}");
            }

            HandleSingleAxisTurn(singleDir, wasUsingDualInput);
            return;
        }

        if (pendingSingleTurnDirection != Vector2.zero || lockedMovementDirection != Vector2.zero)
        {
            LogCurve(
                $"DualInput takeover -> clear pendingSingle:{pendingSingleTurnDirection} " +
                $"lockedDir:{lockedMovementDirection}",
                verbose: true);
            ResetSingleInputTurnState();
        }

        if (dualPrimary == Vector2.zero)
        {
            bool vertFree = !IsMoveBlocked(vertDir);
            bool horizFree = !IsMoveBlocked(horizDir);

            if (horizFree && !vertFree)
            {
                dualPrimary = horizDir;
                dualSecondary = vertDir;
            }
            else
            {
                dualPrimary = vertDir;
                dualSecondary = horizDir;
            }

            dualSwitchTimer = 0f;

            LogCurve(
                $"DualInput init vertFree:{vertFree} horizFree:{horizFree} " +
                $"dualPrimary:{dualPrimary} dualSecondary:{dualSecondary}");
        }
        else
        {
            bool primaryStillHeld = (dualPrimary == vertDir || dualPrimary == horizDir);
            if (!primaryStillHeld)
            {
                Vector2 oldPrimary = dualPrimary;
                Vector2 oldSecondary = dualSecondary;

                dualPrimary = (dualSecondary == vertDir || dualSecondary == horizDir)
                    ? dualSecondary
                    : (vertDir != Vector2.zero ? vertDir : horizDir);

                dualSecondary = (dualPrimary == vertDir) ? horizDir : vertDir;
                dualSwitchTimer = 0f;

                LogCurve(
                    $"DualInput rebind oldPrimary:{oldPrimary} oldSecondary:{oldSecondary} " +
                    $"newPrimary:{dualPrimary} newSecondary:{dualSecondary}");
            }
            else
            {
                dualSecondary = (dualPrimary == vertDir) ? horizDir : vertDir;
            }
        }

        if (dualSwitchTimer > 0f)
            dualSwitchTimer -= Time.deltaTime;

        Vector2 chosenDir = ResolveDualInputZigZag();

        LogCurve(
            $"DualInput resolve chosen:{chosenDir} primary:{dualPrimary} secondary:{dualSecondary} " +
            $"timer:{dualSwitchTimer:F4} pendingTurn:{pendingTurnDirection}");

        ApplyDirectionFromVector(chosenDir);
    }

    private Vector2 ResolveDualInputZigZag()
    {
        if (dualPrimary == Vector2.zero)
            return dualSecondary;

        bool primaryBlocked = IsMoveBlocked(dualPrimary);
        bool secondaryBlocked = dualSecondary != Vector2.zero && IsMoveBlocked(dualSecondary);

        bool nearCentre = dualSecondary != Vector2.zero && IsAtTileCentreOnPerpendicularAxis(dualSecondary);
        bool exactlyAligned = dualSecondary != Vector2.zero && IsExactlyAlignedForAxisSwap(dualSecondary);
        bool willCrossCentreNextStep = dualSecondary != Vector2.zero && WillCrossTileCentreOnPerpendicularAxisNextStep(dualSecondary, dualPrimary);

        LogCurve(
            $"ResolveDualInputZigZag " +
            $"primary:{dualPrimary} secondary:{dualSecondary} " +
            $"primaryBlocked:{primaryBlocked} secondaryBlocked:{secondaryBlocked} " +
            $"nearCentre:{nearCentre} exactlyAligned:{exactlyAligned} willCrossNext:{willCrossCentreNextStep} " +
            $"pendingTurnBefore:{pendingTurnDirection} dualSwitchTimer:{dualSwitchTimer:F4}");

        if (primaryBlocked && secondaryBlocked)
        {
            pendingTurnDirection = Vector2.zero;
            return Vector2.zero;
        }

        if (primaryBlocked && !secondaryBlocked)
        {
            LogCurve("ResolveDualInputZigZag -> primary blocked, forcing axis switch");
            DoAxisSwitch();
            pendingTurnDirection = Vector2.zero;
            return dualPrimary;
        }

        if (dualSecondary == Vector2.zero || secondaryBlocked)
        {
            pendingTurnDirection = Vector2.zero;
            return dualPrimary;
        }

        if (dualSwitchTimer > 0f)
        {
            pendingTurnDirection = Vector2.zero;
            return dualPrimary;
        }

        if ((nearCentre || willCrossCentreNextStep || exactlyAligned) && pendingTurnDirection == Vector2.zero)
        {
            pendingTurnDirection = dualSecondary;

            LogCurve(
                $"ResolveDualInputZigZag -> arm pending turn:{pendingTurnDirection} " +
                $"nearCentre:{nearCentre} exactlyAligned:{exactlyAligned} willCrossNext:{willCrossCentreNextStep}");
        }

        if (pendingTurnDirection != Vector2.zero)
        {
            bool shouldSwitchNow = exactlyAligned || willCrossCentreNextStep;

            LogCurve(
                $"ResolveDualInputZigZag pending active:{pendingTurnDirection} " +
                $"shouldSwitchNow:{shouldSwitchNow} exactlyAligned:{exactlyAligned} willCrossNext:{willCrossCentreNextStep}");

            if (shouldSwitchNow)
            {
                DoAxisSwitch();
                pendingTurnDirection = Vector2.zero;
                return dualPrimary;
            }

            return dualPrimary;
        }

        return dualPrimary;
    }

    private bool IsAtTileCentreOnPerpendicularAxis(Vector2 newDir)
    {
        if (tileSize <= 0.0001f)
            return true;

        Vector2 pos = Rigidbody != null ? Rigidbody.position : (Vector2)transform.position;

        float dynamicTolerance = dualInputCentreSnapTolerance;

        if (speed >= 6f)
            dynamicTolerance += 0.04f;

        if (speed >= 7f)
            dynamicTolerance += 0.04f;

        if (speed >= 8f)
            dynamicTolerance += 0.04f;

        dynamicTolerance = Mathf.Min(dynamicTolerance, 0.49f);

        if (Mathf.Abs(newDir.x) > 0.01f)
        {
            float nearest = Mathf.Round(pos.y / tileSize) * tileSize;
            return Mathf.Abs(pos.y - nearest) <= dynamicTolerance;
        }
        else
        {
            float nearest = Mathf.Round(pos.x / tileSize) * tileSize;
            return Mathf.Abs(pos.x - nearest) <= dynamicTolerance;
        }
    }

    private bool WillCrossTileCentreOnPerpendicularAxisNextStep(Vector2 requestedTurnDir, Vector2 movementDir)
    {
        if (tileSize <= 0.0001f)
            return false;

        requestedTurnDir = NormalizeCardinal(requestedTurnDir);
        movementDir = NormalizeCardinal(movementDir);

        if (requestedTurnDir == Vector2.zero || movementDir == Vector2.zero)
            return false;

        Vector2 pos = Rigidbody != null ? Rigidbody.position : (Vector2)transform.position;

        float rawMoveWorld = GetRawMoveWorldPerFixedFrame();
        float moveWorld = GetQuantizedMoveWorldPerFixedFrame(movementDir, rawMoveWorld);

        if (moveWorld <= 0f)
            return false;

        Vector2 nextPos = QuantizeToPixelGrid(pos + movementDir * moveWorld);

        if (Mathf.Abs(requestedTurnDir.x) > 0.01f)
        {
            float nearest = Mathf.Round(pos.y / tileSize) * tileSize;

            float currentDelta = pos.y - nearest;
            float nextDelta = nextPos.y - nearest;

            bool crosses =
                Mathf.Abs(currentDelta) <= alignEpsilon ||
                Mathf.Abs(nextDelta) <= alignEpsilon ||
                (currentDelta < 0f && nextDelta > 0f) ||
                (currentDelta > 0f && nextDelta < 0f);

            LogCurve(
                $"WillCrossCentreNextStep turn:{requestedTurnDir} move:{movementDir} " +
                $"axis:Y pos:{pos} nextPos:{nextPos} nearest:{nearest:F4} " +
                $"currentDelta:{currentDelta:F4} nextDelta:{nextDelta:F4} crosses:{crosses}",
                verbose: true);

            return crosses;
        }
        else
        {
            float nearest = Mathf.Round(pos.x / tileSize) * tileSize;

            float currentDelta = pos.x - nearest;
            float nextDelta = nextPos.x - nearest;

            bool crosses =
                Mathf.Abs(currentDelta) <= alignEpsilon ||
                Mathf.Abs(nextDelta) <= alignEpsilon ||
                (currentDelta < 0f && nextDelta > 0f) ||
                (currentDelta > 0f && nextDelta < 0f);

            LogCurve(
                $"WillCrossCentreNextStep turn:{requestedTurnDir} move:{movementDir} " +
                $"axis:X pos:{pos} nextPos:{nextPos} nearest:{nearest:F4} " +
                $"currentDelta:{currentDelta:F4} nextDelta:{nextDelta:F4} crosses:{crosses}",
                verbose: true);

            return crosses;
        }
    }

    private void DoAxisSwitch()
    {
        Vector2 oldPrimary = dualPrimary;
        Vector2 oldSecondary = dualSecondary;
        Vector2 pos = Rigidbody != null ? Rigidbody.position : (Vector2)transform.position;

        (dualPrimary, dualSecondary) = (dualSecondary, dualPrimary);
        dualSwitchTimer = dualInputSwitchCooldown;

        LogCurve(
            $"DoAxisSwitch pos:{pos} oldPrimary:{oldPrimary} oldSecondary:{oldSecondary} " +
            $"newPrimary:{dualPrimary} newSecondary:{dualSecondary} cooldown:{dualSwitchTimer:F4}");
    }

    private void ResetDualInputAxes()
    {
        dualPrimary = Vector2.zero;
        dualSecondary = Vector2.zero;
        dualSwitchTimer = 0f;
        pendingTurnDirection = Vector2.zero;
    }

    private void ResetSingleInputTurnState()
    {
        pendingSingleTurnDirection = Vector2.zero;
        lockedMovementDirection = Vector2.zero;
    }

    private bool HasDualInputState()
    {
        return dualPrimary != Vector2.zero ||
               dualSecondary != Vector2.zero ||
               pendingTurnDirection != Vector2.zero ||
               dualSwitchTimer > 0f;
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
            SetFacingDirection(dir, "ApplyDirectionFromVector");

        if (dir != Vector2.zero && TryGetComponent<BombKickAbility>(out var kickAbility) && kickAbility != null)
            kickAbility.NotifyOwnerDirectionChanged(dir);

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

    public void CopyHeadOnlyVisualsTo(
        AnimatedSpriteRenderer targetUp,
        AnimatedSpriteRenderer targetDown,
        AnimatedSpriteRenderer targetLeft,
        AnimatedSpriteRenderer targetRight)
    {
        CopyAnimatedSpriteRenderer(headOnlyUp != null ? headOnlyUp : spriteRendererUp, targetUp);
        CopyAnimatedSpriteRenderer(headOnlyDown != null ? headOnlyDown : spriteRendererDown, targetDown);
        CopyAnimatedSpriteRenderer(headOnlyLeft != null ? headOnlyLeft : spriteRendererLeft, targetLeft);
        CopyAnimatedSpriteRenderer(headOnlyRight != null ? headOnlyRight : spriteRendererRight, targetRight);

        LogAnimatedRendererState("CopiedTarget-Up", targetUp);
        LogAnimatedRendererState("CopiedTarget-Down", targetDown);
        LogAnimatedRendererState("CopiedTarget-Left", targetLeft);
        LogAnimatedRendererState("CopiedTarget-Right", targetRight);
    }

    private static void LogAnimatedRendererState(string label, AnimatedSpriteRenderer renderer)
    {
        renderer.TryGetComponent(out SpriteRenderer sr);

        int animCount = renderer.animationSprite != null ? renderer.animationSprite.Length : -1;
        string idleName = renderer.idleSprite != null ? renderer.idleSprite.name : "null";
        string shownName = sr != null && sr.sprite != null ? sr.sprite.name : "null";
    }

    static void CopyAnimatedSpriteRenderer(AnimatedSpriteRenderer source, AnimatedSpriteRenderer target)
    {
        if (source == null || target == null)
            return;

        target.idleSprite = source.idleSprite;
        target.animationSprite = source.animationSprite != null
            ? (Sprite[])source.animationSprite.Clone()
            : Array.Empty<Sprite>();
        target.allowFlipX = source.allowFlipX;
        target.animationTime = source.animationTime;
        target.useSequenceDuration = source.useSequenceDuration;
        target.sequenceDuration = source.sequenceDuration;
        target.loop = source.loop;
        target.idle = source.idle;
        target.frameOffsets = source.frameOffsets != null
            ? (Vector2[])source.frameOffsets.Clone()
            : Array.Empty<Vector2>();
        target.pingPong = source.pingPong;
        target.disableOffsetsIfThisObjectHasRigidbody2D = source.disableOffsetsIfThisObjectHasRigidbody2D;

        if (source.TryGetComponent<SpriteRenderer>(out var sourceRenderer) &&
            target.TryGetComponent<SpriteRenderer>(out var targetRenderer))
        {
            targetRenderer.flipX = sourceRenderer.flipX;
            targetRenderer.flipY = sourceRenderer.flipY;
            targetRenderer.color = sourceRenderer.color;
        }

        target.RefreshFrame();
    }

    public void ApplySpeedInternal(int newInternal)
    {
        speedInternal = PlayerPersistentStats.ClampSpeedInternal(newInternal);
        speed = PlayerPersistentStats.InternalSpeedToTilesPerSecond(speedInternal);
    }

    public void ApplyTemporarySpeedOverride(int newInternal, float durationSeconds)
    {
        if (temporarySpeedOverrideRoutine != null)
        {
            StopCoroutine(temporarySpeedOverrideRoutine);
            temporarySpeedOverrideRoutine = null;
        }

        if (!hasTemporarySpeedOverride)
            temporarySpeedOverrideRestoreInternal = speedInternal;

        hasTemporarySpeedOverride = true;
        ApplySpeedInternalUnclamped(newInternal);
        StartTemporarySpeedBlink(durationSeconds);
        temporarySpeedOverrideRoutine = StartCoroutine(TemporarySpeedOverride(durationSeconds));
    }

    IEnumerator TemporarySpeedOverride(float durationSeconds)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, durationSeconds));
        temporarySpeedOverrideRoutine = null;
        ClearTemporarySpeedOverride(restoreSpeed: true);
    }

    void ClearTemporarySpeedOverride(bool restoreSpeed)
    {
        if (temporarySpeedOverrideRoutine != null)
        {
            StopCoroutine(temporarySpeedOverrideRoutine);
            temporarySpeedOverrideRoutine = null;
        }

        if (!hasTemporarySpeedOverride)
            return;

        hasTemporarySpeedOverride = false;

        if (restoreSpeed)
            ApplySpeedInternal(temporarySpeedOverrideRestoreInternal);

        StopTemporarySpeedBlink(restoreColors: true);
    }

    void ApplySpeedInternalUnclamped(int newInternal)
    {
        speedInternal = Mathf.Max(0, newInternal);
        speed = PlayerPersistentStats.InternalSpeedToTilesPerSecond(speedInternal);
    }

    void StartTemporarySpeedBlink(float durationSeconds)
    {
        StopTemporarySpeedBlink(restoreColors: true);
        temporarySpeedBlinkRoutine = StartCoroutine(TemporarySpeedBlink(durationSeconds));
    }

    IEnumerator TemporarySpeedBlink(float durationSeconds)
    {
        float duration = Mathf.Max(0f, durationSeconds);
        float elapsed = 0f;
        const float blinkInterval = 0.1f;
        bool blackFrame = true;

        while (elapsed < duration)
        {
            ApplyTemporarySpeedBlinkColor(blackFrame);
            blackFrame = !blackFrame;

            float step = Mathf.Min(blinkInterval, duration - elapsed);
            if (step <= 0f)
                break;

            elapsed += step;
            yield return new WaitForSeconds(step);
        }

        temporarySpeedBlinkRoutine = null;
        StopTemporarySpeedBlink(restoreColors: true);
    }

    void ApplyTemporarySpeedBlinkColor(bool useBlack)
    {
        var renderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null)
                continue;

            if (!temporarySpeedBlinkOriginalColors.ContainsKey(sr))
                temporarySpeedBlinkOriginalColors.Add(sr, sr.color);

            Color original = temporarySpeedBlinkOriginalColors[sr];
            sr.color = useBlack
                ? new Color(0f, 0f, 0f, original.a)
                : original;
        }

        ApplyMountedVisualBlinkColor(useBlack);
        ApplyHudPortraitBlinkColor(useBlack);
    }

    void StopTemporarySpeedBlink(bool restoreColors)
    {
        if (temporarySpeedBlinkRoutine != null)
        {
            StopCoroutine(temporarySpeedBlinkRoutine);
            temporarySpeedBlinkRoutine = null;
        }

        if (restoreColors)
        {
            foreach (var kv in temporarySpeedBlinkOriginalColors)
            {
                if (kv.Key != null)
                    kv.Key.color = kv.Value;
            }
        }

        temporarySpeedBlinkOriginalColors.Clear();

        if (restoreColors)
        {
            ClearMountedVisualBlinkColor();
            ClearHudPortraitBlinkColor();
        }
    }

    void ApplyMountedVisualBlinkColor(bool useBlack)
    {
        var mountVisuals = GetComponentsInChildren<MountVisualController>(true);
        if (mountVisuals == null || mountVisuals.Length == 0)
            return;

        for (int i = 0; i < mountVisuals.Length; i++)
        {
            MountVisualController visual = mountVisuals[i];
            if (visual == null)
                continue;

            if (useBlack)
                visual.SetPlayerEffectTint(true, Color.black, 0f);
            else
                visual.SetPlayerEffectTint(false, Color.white, 1f);
        }
    }

    void ClearMountedVisualBlinkColor()
    {
        var mountVisuals = GetComponentsInChildren<MountVisualController>(true);
        if (mountVisuals == null || mountVisuals.Length == 0)
            return;

        for (int i = 0; i < mountVisuals.Length; i++)
        {
            MountVisualController visual = mountVisuals[i];
            if (visual != null)
                visual.SetPlayerEffectTint(false, Color.white, 1f);
        }
    }

    void ApplyHudPortraitBlinkColor(bool useBlack)
    {
        Color color = useBlack ? Color.black : Color.white;

        var hudPortrait = FindAnyObjectByType<HudPortraitInGridLayout>();
        if (hudPortrait != null)
            hudPortrait.SetPlayerPortraitTint(playerId, color);

        var battleHud = FindAnyObjectByType<BattleModeHud>();
        if (battleHud != null)
            battleHud.SetPlayerPortraitTint(playerId, color);
    }

    void ClearHudPortraitBlinkColor()
    {
        var hudPortrait = FindAnyObjectByType<HudPortraitInGridLayout>();
        if (hudPortrait != null)
            hudPortrait.ClearPlayerPortraitTint(playerId);

        var battleHud = FindAnyObjectByType<BattleModeHud>();
        if (battleHud != null)
            battleHud.ClearPlayerPortraitTint(playerId);
    }

    public bool TryAddSpeedUp(int speedStep = PlayerPersistentStats.SpeedStep)
    {
        int before = hasTemporarySpeedOverride
            ? temporarySpeedOverrideRestoreInternal
            : speedInternal;

        if (hasTemporarySpeedOverride)
        {
            temporarySpeedOverrideRestoreInternal = PlayerPersistentStats.ClampSpeedInternal(
                temporarySpeedOverrideRestoreInternal + speedStep);
            return temporarySpeedOverrideRestoreInternal != before;
        }

        ApplySpeedInternal(speedInternal + speedStep);
        return speedInternal != before;
    }

    private void RebuildObstacleContactFilter()
    {
        obstacleContactFilter = new ContactFilter2D { useLayerMask = true };
        obstacleContactFilter.SetLayerMask(obstacleMask);
        obstacleContactFilter.useTriggers = true;
    }

    private int GetObstacleHitCount(Vector2 worldPosition, Vector2 size)
    {
        return Physics2D.OverlapBox(worldPosition, size, 0f, obstacleContactFilter, obstacleOverlapBuffer);
    }

    private bool IsSolidAtCustom(Vector2 worldPosition, float sizeMul)
    {
        Vector2 size = Vector2.one * (tileSize * sizeMul);

        int hitCount = GetObstacleHitCount(worldPosition, size);
        if (hitCount <= 0)
            return false;

        bool canPassDestructibles = abilitySystem != null &&
                                   abilitySystem.IsEnabled(DestructiblePassAbility.AbilityId);

        for (int i = 0; i < hitCount; i++)
        {
            var hit = obstacleOverlapBuffer[i];
            obstacleOverlapBuffer[i] = null;
            if (hit == null) continue;
            if (hit.gameObject == gameObject) continue;

            var hitBombEarly = hit.GetComponent<Bomb>();
            if (hitBombEarly != null && !hitBombEarly.IsSolid)
                continue;

            if (hit.isTrigger) continue;

            if (canPassTaggedObstacles && hit.CompareTag(passObstacleTag))
                continue;

            if (canPassDestructibles && hit.CompareTag("Destructibles"))
                continue;

            if (_lastAdjKickedBomb != null && _lastAdjKickedBomb.IsBeingKicked
                && hit.transform.IsChildOf(_lastAdjKickedBomb.transform))
                continue;

            if (hit.gameObject.layer == bombLayer)
            {
                var bomb = hitBombEarly != null ? hitBombEarly : hit.GetComponent<Bomb>();
                if (bomb != null)
                {
                    if (!bomb.IsSolid)
                        continue;

                    if (bomb.Owner == bombController)
                    {
                        var bombCollider = bomb.GetComponent<Collider2D>();
                        if (bombCollider != null && bombCollider.isTrigger)
                            continue;
                    }
                }
            }

            return true;
        }

        return false;
    }

    protected virtual void FixedUpdate()
    {
        if (UpdateBombReentryCentering())
            return;

        if (ShouldSkipFixedUpdate())
            return;

        if (_lastAdjKickedBomb != null && !_lastAdjKickedBomb.IsBeingKicked)
            _lastAdjKickedBomb = null;

        Vector2 position = Rigidbody.position;
        position = QuantizeToPixelGrid(position);

        if (pendingSingleTurnDirection != Vector2.zero)
        {
            bool canTurnNow =
                !IsMoveBlocked(pendingSingleTurnDirection) &&
                (IsAtTileCentreOnPerpendicularAxis(pendingSingleTurnDirection) ||
                 IsExactlyAlignedForAxisSwap(pendingSingleTurnDirection));

            LogCurve(
                $"FixedUpdate pendingSingle check pending:{pendingSingleTurnDirection} " +
                $"currentDir:{direction} pos:{position} canTurnNow:{canTurnNow}");

            if (canTurnNow)
            {
                direction = pendingSingleTurnDirection;
                hasInput = true;
                pendingSingleTurnDirection = Vector2.zero;
                lockedMovementDirection = Vector2.zero;

                currentAxis = Mathf.Abs(direction.x) > 0.01f
                    ? MoveAxis.Horizontal
                    : MoveAxis.Vertical;

                SetFacingDirection(direction, "FixedUpdatePendingSingleTurn");
                LogCurve($"FixedUpdate pendingSingle -> APPLY turn:{direction}");
            }
        }

        float rawMoveWorld = GetRawMoveWorldPerFixedFrame();
        float moveWorld = GetQuantizedMoveWorldPerFixedFrame(direction, rawMoveWorld);

        bool movingVertical = Mathf.Abs(direction.y) > 0.01f;
        bool movingHorizontal = Mathf.Abs(direction.x) > 0.01f;

        UpdateCurrentAxis(movingHorizontal, movingVertical);
        UpdateOwnedBombPlantTraversal(direction);

        if (_debugLastFixedFrameLogged != Time.frameCount)
        {
            _debugLastFixedFrameLogged = Time.frameCount;
            LogCurve(
                $"FixedUpdate pos:{position} dir:{direction} face:{facingDirection} axis:{currentAxis} " +
                $"speed:{speed:F3} speedInternal:{speedInternal} rawMove:{rawMoveWorld:F4} move:{moveWorld:F4} " +
                $"accX:{accPixelsX:F4} accY:{accPixelsY:F4}");
        }

        if (TryKickAdjacentBombFromCurrentTile(position, direction))
        {
            Vector2 holdPos = Rigidbody != null ? Rigidbody.position : position;
            holdPos = QuantizeToPixelGrid(holdPos);

            LogCurve($"FixedUpdate TryKickAdjacentBombFromCurrentTile -> holdPos:{holdPos}");
            MovePositionPixelPerfect(holdPos);
            return;
        }

        if (ShouldHardBlockForwardFromTileCenter(position))
        {
            LogCurve($"FixedUpdate ShouldHardBlockForwardFromTileCenter -> hold position:{position}");
            MovePositionPixelPerfect(position);
            return;
        }

        Vector2 beforeAlign = position;
        AlignPerpendicularForCurrentAxis(ref position, rawMoveWorld);
        if (beforeAlign != position)
            LogCurve($"FixedUpdate AlignPerpendicular moved {beforeAlign} -> {position}");

        if (enableCorridorAxisLock && tileSize > 0.0001f)
        {
            if (TryApplyCorridorAxisLock(position, movingHorizontal, movingVertical, rawMoveWorld))
            {
                LogCurve("FixedUpdate corridor axis lock consumed movement");
                return;
            }
        }

        Vector2 beforeCentering = position;
        ApplyCenteringWhenSqueezed(ref position, rawMoveWorld, movingHorizontal, movingVertical);
        if (beforeCentering != position)
            LogCurve($"FixedUpdate ApplyCenteringWhenSqueezed moved {beforeCentering} -> {position}");

        if (pendingSingleTurnDirection != Vector2.zero)
        {
            bool canTurnAfterAlign =
                !IsMoveBlocked(pendingSingleTurnDirection) &&
                (IsAtTileCentreOnPerpendicularAxis(pendingSingleTurnDirection) ||
                 IsExactlyAlignedForAxisSwap(pendingSingleTurnDirection));

            LogCurve(
                $"FixedUpdate pendingSingle post-align pending:{pendingSingleTurnDirection} " +
                $"pos:{position} canTurnAfterAlign:{canTurnAfterAlign}",
                verbose: true);

            if (canTurnAfterAlign)
            {
                direction = pendingSingleTurnDirection;
                hasInput = true;
                pendingSingleTurnDirection = Vector2.zero;
                lockedMovementDirection = Vector2.zero;

                currentAxis = Mathf.Abs(direction.x) > 0.01f
                    ? MoveAxis.Horizontal
                    : MoveAxis.Vertical;

                SetFacingDirection(direction, "FixedUpdatePendingSinglePostAlign");
                LogCurve($"FixedUpdate pendingSingle post-align -> APPLY turn:{direction}");

                rawMoveWorld = GetRawMoveWorldPerFixedFrame();
                moveWorld = GetQuantizedMoveWorldPerFixedFrame(direction, rawMoveWorld);

                movingVertical = Mathf.Abs(direction.y) > 0.01f;
                movingHorizontal = Mathf.Abs(direction.x) > 0.01f;
            }
        }

        if (TryClampBlockedAxisAndCenter(ref position, rawMoveWorld, movingHorizontal, movingVertical))
        {
            MovePositionPixelPerfect(position);
            return;
        }

        if (moveWorld <= 0f)
        {
            LogCurve($"FixedUpdate moveWorld <= 0 -> hold {position}", verbose: true);
            MovePositionPixelPerfect(position);
            return;
        }

        Vector2 targetPosition = position + direction * moveWorld;
        targetPosition = QuantizeToPixelGrid(targetPosition);

        bool blocked = IsBlocked(targetPosition);

        LogCurve($"FixedUpdate target:{targetPosition} blocked:{blocked}");

        if (!blocked)
        {
            MovePositionPixelPerfect(targetPosition);
            return;
        }

        LogCurve("FixedUpdate target blocked -> TrySlideIfBlocked");
        TrySlideIfBlocked(position, moveWorld, movingHorizontal, movingVertical);
    }

    private bool TryClampBlockedAxisAndCenter(ref Vector2 position, float moveSpeed, bool movingHorizontal, bool movingVertical)
    {
        float half = tileSize * 0.5f;

        bool blockLeft = IsSolidAt(position + Vector2.left * half);
        bool blockRight = IsSolidAt(position + Vector2.right * half);
        bool blockUp = IsSolidAt(position + Vector2.up * half);
        bool blockDown = IsSolidAt(position + Vector2.down * half);

        bool horizontalAxisClosed = blockLeft && blockRight;
        bool verticalAxisClosed = blockUp && blockDown;

        float snapStep = moveSpeed * Mathf.Max(1f, perpendicularAlignMultiplier);

        if (movingVertical && verticalAxisClosed)
        {
            float targetY = Mathf.Round(position.y / tileSize) * tileSize;
            float newY = Mathf.MoveTowards(position.y, targetY, snapStep);

            LogCurve(
                $"TryClampBlockedAxisAndCenter vertical CLOSED " +
                $"pos:{position} targetY:{targetY:F3} newY:{newY:F3} " +
                $"blockUp:{blockUp} blockDown:{blockDown}");

            position.y = newY;
            position = QuantizeToPixelGrid(position);

            direction = Vector2.zero;
            hasInput = false;
            currentAxis = MoveAxis.None;

            return true;
        }

        if (movingHorizontal && horizontalAxisClosed)
        {
            float targetX = Mathf.Round(position.x / tileSize) * tileSize;
            float newX = Mathf.MoveTowards(position.x, targetX, snapStep);

            LogCurve(
                $"TryClampBlockedAxisAndCenter horizontal CLOSED " +
                $"pos:{position} targetX:{targetX:F3} newX:{newX:F3} " +
                $"blockLeft:{blockLeft} blockRight:{blockRight}");

            position.x = newX;
            position = QuantizeToPixelGrid(position);

            direction = Vector2.zero;
            hasInput = false;
            currentAxis = MoveAxis.None;

            return true;
        }

        return false;
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
            AlignPerpendicular(ref position, axisIsHorizontal: true, moveSpeed: moveSpeed);
        }
        else if (currentAxis == MoveAxis.Vertical)
        {
            AlignPerpendicular(ref position, axisIsHorizontal: false, moveSpeed: moveSpeed);
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

            LogCurve(
                $"AxisLock horizontal pos:{position} cx:{cx:F3} cy:{cy:F3} y0:{y0:F3} y1:{y1:F3} " +
                $"lrRow0:{lrRow0} lrRow1:{lrRow1} corridorVertical:{corridorVertical}",
                verbose: true);

            if (!corridorVertical)
                return false;

            LogCurve($"AxisLock horizontal -> SnapAndStop Y toward {cy:F3}");
            SnapAndStop(axisX: false, position, new Vector2(position.x, cy), moveSpeed);
            return true;
        }

        if (movingVertical)
        {
            bool udCol0 = SolidUDAt(x0, cy);
            bool udCol1 = SolidUDAt(x1, cy);
            bool corridorHorizontal = (udCol0 || udCol1);

            LogCurve(
                $"AxisLock vertical pos:{position} cx:{cx:F3} cy:{cy:F3} x0:{x0:F3} x1:{x1:F3} " +
                $"udCol0:{udCol0} udCol1:{udCol1} corridorHorizontal:{corridorHorizontal}",
                verbose: true);

            if (!corridorHorizontal)
                return false;

            LogCurve($"AxisLock vertical -> SnapAndStop X toward {cx:F3}");
            SnapAndStop(axisX: true, position, new Vector2(cx, position.y), moveSpeed);
            return true;
        }

        return false;
    }

    private void SnapAndStop(bool axisX, Vector2 currentPos, Vector2 snapTarget, float moveSpeed)
    {
        if (Rigidbody != null)
            Rigidbody.linearVelocity = Vector2.zero;

        LogCurve(
            $"SnapAndStop axisX:{axisX} current:{currentPos} snapTarget:{snapTarget} " +
            $"moveSpeed:{moveSpeed:F4} dirBefore:{direction} hasInputBefore:{hasInput} axisBefore:{currentAxis}");

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

        LogCurve($"SnapAndStop snappedPos:{snappedPos} snapStep:{snapStep:F4}");
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

    private void AlignPerpendicular(ref Vector2 position, bool axisIsHorizontal, float moveSpeed)
    {
        if (tileSize <= 0.0001f)
            return;

        float alignStep = Mathf.Min(moveSpeed, tileSize * 0.125f);
        Vector2 moveDir = direction;

        if (axisIsHorizontal)
        {
            float targetY = Mathf.Round(position.y / tileSize) * tileSize;
            float deltaY = position.y - targetY;

            if (Mathf.Abs(deltaY) <= alignEpsilon)
            {
                Vector2 snapped = new(position.x, targetY);
                bool canSnap = CanAlignToPerpendicularTarget(snapped, moveDir);

                LogCurve(
                    $"AlignPerpendicular H snapCheck pos:{position} targetY:{targetY:F3} deltaY:{deltaY:F4} " +
                    $"alignStep:{alignStep:F4} canSnap:{canSnap}",
                    verbose: true);

                if (canSnap)
                    position.y = targetY;

                return;
            }

            float newY = Mathf.MoveTowards(position.y, targetY, alignStep);
            Vector2 nextCandidate = new(position.x, newY);
            bool canMove = CanAlignToPerpendicularTarget(nextCandidate, moveDir);

            LogCurve(
                $"AlignPerpendicular H pos:{position} targetY:{targetY:F3} deltaY:{deltaY:F4} " +
                $"newY:{newY:F3} alignStep:{alignStep:F4} canMove:{canMove}",
                verbose: true);

            if (canMove)
                position.y = newY;

            return;
        }

        float targetX = Mathf.Round(position.x / tileSize) * tileSize;
        float deltaX = position.x - targetX;

        if (Mathf.Abs(deltaX) <= alignEpsilon)
        {
            Vector2 snapped = new(targetX, position.y);
            bool canSnap = CanAlignToPerpendicularTarget(snapped, moveDir);

            LogCurve(
                $"AlignPerpendicular V snapCheck pos:{position} targetX:{targetX:F3} deltaX:{deltaX:F4} " +
                $"alignStep:{alignStep:F4} canSnap:{canSnap}",
                verbose: true);

            if (canSnap)
                position.x = targetX;

            return;
        }

        float newX = Mathf.MoveTowards(position.x, targetX, alignStep);
        Vector2 nextCandidate2 = new(newX, position.y);
        bool canMove2 = CanAlignToPerpendicularTarget(nextCandidate2, moveDir);

        LogCurve(
            $"AlignPerpendicular V pos:{position} targetX:{targetX:F3} deltaX:{deltaX:F4} " +
            $"newX:{newX:F3} alignStep:{alignStep:F4} canMove:{canMove2}",
            verbose: true);

        if (canMove2)
            position.x = newX;
    }

    private void TrySlideHorizontally(Vector2 position, float moveSpeed)
    {
        float leftCenter = Mathf.Floor(position.x / tileSize) * tileSize;
        float rightCenter = Mathf.Ceil(position.x / tileSize) * tileSize;

        Vector2 verticalStep = new(0f, direction.y * moveSpeed);

        bool leftFree = !IsBlocked(new Vector2(leftCenter, position.y) + verticalStep);
        bool rightFree = !IsBlocked(new Vector2(rightCenter, position.y) + verticalStep);

        LogCurve(
            $"TrySlideHorizontally pos:{position} moveSpeed:{moveSpeed:F4} leftCenter:{leftCenter:F3} rightCenter:{rightCenter:F3} " +
            $"leftFree:{leftFree} rightFree:{rightFree}");

        if (!leftFree && !rightFree)
            return;

        float targetX;

        if (leftFree && !rightFree) targetX = leftCenter;
        else if (rightFree && !leftFree) targetX = rightCenter;
        else targetX = Mathf.Abs(position.x - leftCenter) <= Mathf.Abs(position.x - rightCenter) ? leftCenter : rightCenter;

        LogCurve($"TrySlideHorizontally targetX:{targetX:F3}");

        if (Mathf.Abs(position.x - targetX) > CenterEpsilon)
        {
            float newX = Mathf.MoveTowards(position.x, targetX, moveSpeed);
            LogCurve($"TrySlideHorizontally centering X {position.x:F3} -> {newX:F3}");
            MovePositionPixelPerfect(new Vector2(newX, position.y));
        }
        else
        {
            Vector2 newPos = new Vector2(targetX, position.y) + verticalStep;
            bool blocked = IsBlocked(newPos);
            LogCurve($"TrySlideHorizontally advance newPos:{newPos} blocked:{blocked}");
            if (!blocked)
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

        LogCurve(
            $"TrySlideVertically pos:{position} moveSpeed:{moveSpeed:F4} bottomCenter:{bottomCenter:F3} topCenter:{topCenter:F3} " +
            $"bottomFree:{bottomFree} topFree:{topFree}");

        if (!bottomFree && !topFree)
            return;

        float targetY;

        if (bottomFree && !topFree) targetY = bottomCenter;
        else if (topFree && !bottomFree) targetY = topCenter;
        else targetY = Mathf.Abs(position.y - bottomCenter) <= Mathf.Abs(position.y - topCenter) ? bottomCenter : topCenter;

        LogCurve($"TrySlideVertically targetY:{targetY:F3}");

        if (Mathf.Abs(position.y - targetY) > CenterEpsilon)
        {
            float newY = Mathf.MoveTowards(position.y, targetY, moveSpeed);
            LogCurve($"TrySlideVertically centering Y {position.y:F3} -> {newY:F3}");
            MovePositionPixelPerfect(new Vector2(position.x, newY));
        }
        else
        {
            Vector2 newPos = new Vector2(position.x, targetY) + horizontalStep;
            bool blocked = IsBlocked(newPos);
            LogCurve($"TrySlideVertically advance newPos:{newPos} blocked:{blocked}");
            if (!blocked)
                MovePositionPixelPerfect(newPos);
        }
    }

    protected bool IsSolidAt(Vector2 worldPosition)
    {
        Vector2 size = Vector2.one * (tileSize * 0.6f);

        int hitCount = GetObstacleHitCount(worldPosition, size);
        if (hitCount <= 0)
            return false;

        bool canPassDestructibles = abilitySystem != null &&
                                   abilitySystem.IsEnabled(DestructiblePassAbility.AbilityId);

        for (int i = 0; i < hitCount; i++)
        {
            var hit = obstacleOverlapBuffer[i];
            obstacleOverlapBuffer[i] = null;
            if (hit == null) continue;
            if (hit.gameObject == gameObject) continue;

            var hitBombEarly = hit.GetComponent<Bomb>();
            if (hitBombEarly != null && !hitBombEarly.IsSolid)
            {
                LogBombEscape(
                    $"IsSolidAt IGNORE trigger/non-solid bomb probe world:{worldPosition} bomb:{hitBombEarly.name} " +
                    $"bombPos:{hitBombEarly.GetLogicalPosition()} bombIsSolid:{hitBombEarly.IsSolid}",
                    verbose: true);

                continue;
            }

            if (hit.isTrigger) continue;

            if (canPassTaggedObstacles && hit.CompareTag(passObstacleTag))
                continue;

            if (canPassDestructibles && hit.CompareTag("Destructibles"))
                continue;

            if (hit.gameObject.layer == bombLayer)
            {
                var bomb = hitBombEarly != null ? hitBombEarly : hit.GetComponent<Bomb>();
                if (bomb != null)
                {
                    bool bombStillTrigger = !bomb.IsSolid;

                    LogBombEscape(
                        $"IsSolidAt bomb probe world:{worldPosition} bomb:{bomb.name} " +
                        $"bombPos:{bomb.GetLogicalPosition()} bombIsSolid:{bomb.IsSolid} " +
                        $"bombColliderIsTrigger:{bombStillTrigger} ownerIsMe:{(bomb.Owner == bombController)}",
                        verbose: true);

                    if (!bomb.IsSolid)
                        continue;
                }
            }

            return true;
        }

        return false;
    }

    protected bool IsBlocked(Vector2 targetPosition)
    {
        return IsBlockedAtPosition(targetPosition, direction, true);
    }

    private bool IsBlockedAtPosition(Vector2 targetPosition, Vector2 dirForSize, bool allowMovementAbilities = true)
    {
        Vector2 size = GetBlockProbeSize(dirForSize);

        int hitCount = GetObstacleHitCount(targetPosition, size);
        if (hitCount > 0)
        {
            bool canPassDestructibles = abilitySystem != null &&
                                       abilitySystem.IsEnabled(DestructiblePassAbility.AbilityId);

            for (int h = 0; h < hitCount; h++)
            {
                var hit = obstacleOverlapBuffer[h];
                obstacleOverlapBuffer[h] = null;
                if (hit == null) continue;
                if (hit.gameObject == gameObject) continue;

                var hitBombEarly = hit.GetComponent<Bomb>();
                if (hitBombEarly != null && !hitBombEarly.IsSolid)
                {
                    LogBombEscape(
                        $"IGNORE overlap non-solid bomb:{hitBombEarly.name} " +
                        $"target:{targetPosition} dir:{dirForSize} playerPos:{(Rigidbody != null ? Rigidbody.position : (Vector2)transform.position)} " +
                        $"bombPos:{hitBombEarly.GetLogicalPosition()}",
                        verbose: true);

                    continue;
                }

                if (hit.isTrigger) continue;

                if (canPassTaggedObstacles && hit.CompareTag(passObstacleTag))
                    continue;

                if (canPassDestructibles && hit.CompareTag("Destructibles"))
                    continue;

                if (_lastAdjKickedBomb != null && _lastAdjKickedBomb.IsBeingKicked
                    && hit.transform.IsChildOf(_lastAdjKickedBomb.transform))
                    continue;

                var hitBomb = hitBombEarly != null ? hitBombEarly : hit.GetComponent<Bomb>();
                if (hitBomb != null && !hitBomb.IsSolid)
                    continue;

                if (allowMovementAbilities)
                {
                    for (int i = 0; i < movementAbilities.Length; i++)
                    {
                        var ability = movementAbilities[i];
                        if (ability != null && ability.IsEnabled)
                        {
                            if (ability.TryHandleBlockedHit(hit, dirForSize, tileSize, obstacleMask))
                            {
                                if (hitBomb != null && hitBomb.IsBeingKicked)
                                    _lastAdjKickedBomb = hitBomb;

                                return IsBlockedAtPosition(targetPosition, dirForSize, false);
                            }
                        }
                    }
                }

                if (hitBomb != null)
                {
                    LogBombEscape(
                        $"BLOCKED by SOLID collider bomb:{hitBomb.name} " +
                        $"target:{targetPosition} dir:{dirForSize} " +
                        $"playerPos:{(Rigidbody != null ? Rigidbody.position : (Vector2)transform.position)} " +
                        $"bombPos:{hitBomb.GetLogicalPosition()} " +
                        $"bombIsSolid:{hitBomb.IsSolid} " +
                        $"bombOwnerIsMe:{(hitBomb.Owner == bombController)}");
                }

                return true;
            }
        }

        bool hasBombPass = abilitySystem != null && abilitySystem.IsEnabled(BombPassAbility.AbilityId);
        if (!hasBombPass)
        {
            Vector2 myPos = Rigidbody != null ? Rigidbody.position : (Vector2)transform.position;

            foreach (var bomb in Bomb.ActiveBombs)
            {
                if (bomb == null || bomb.HasExploded)
                    continue;

                if (bomb.IsSolid || bomb.IsBeingPunched)
                    continue;

                Vector2 bombPos = bomb.GetLogicalPosition();

                bool overlapsCurrent = IsInsideBombTileFootprint(myPos, bombPos);
                bool overlapsTarget = IsInsideBombTileFootprint(targetPosition, bombPos);
                bool physicallyStillInside = IsPhysicallyStillInsideTriggerBomb(bomb, myPos, targetPosition);

                if (!overlapsTarget)
                {
                    if (overlapsCurrent && physicallyStillInside)
                    {
                        LogBombEscape(
                            $"IGNORE leaving own/shared non-solid bomb by physical overlap bomb:{bomb.name} " +
                            $"myPos:{myPos} target:{targetPosition} bombPos:{bombPos} " +
                            $"overlapsCurrent:{overlapsCurrent} overlapsTarget:{overlapsTarget} " +
                            $"physicallyStillInside:{physicallyStillInside}",
                            verbose: false);

                        continue;
                    }

                    continue;
                }

                var bombCollider = bomb.GetComponent<Collider2D>();

                LogBombEscape(
                    $"TriggerBombCheck bomb:{bomb.name} " +
                    $"myPos:{myPos} target:{targetPosition} dir:{dirForSize} bombPos:{bombPos} " +
                    $"overlapsCurrent:{overlapsCurrent} overlapsTarget:{overlapsTarget} " +
                    $"physicallyStillInside:{physicallyStillInside} " +
                    $"bombIsSolid:{bomb.IsSolid} bombColliderIsTrigger:{(bombCollider != null && bombCollider.isTrigger)} " +
                    $"bombOwnerIsMe:{(bomb.Owner == bombController)}",
                    verbose: true);

                if (overlapsCurrent)
                {
                    bool nearEdgeForKick = IsNearBombEdgeForEarlyKick(myPos, bombPos, dirForSize);

                    LogBombEscape(
                        $"InsideOwnOrSharedBomb bomb:{bomb.name} overlapsCurrent:true overlapsTarget:true " +
                        $"nearEdgeForKick:{nearEdgeForKick}",
                        verbose: true);

                    if (allowMovementAbilities && nearEdgeForKick && bombCollider != null)
                    {
                        for (int i = 0; i < movementAbilities.Length; i++)
                        {
                            var ability = movementAbilities[i];
                            if (ability != null && ability.IsEnabled)
                            {
                                if (ability.TryHandleBlockedHit(bombCollider, dirForSize, tileSize, obstacleMask))
                                {
                                    if (bomb.IsBeingKicked)
                                        _lastAdjKickedBomb = bomb;

                                    return IsBlockedAtPosition(targetPosition, dirForSize, false);
                                }
                            }
                        }
                    }

                    continue;
                }

                if (bomb == _lastAdjKickedBomb && bomb.IsBeingKicked)
                    continue;

                if (ShouldAllowForwardTraversalThroughOwnTriggerBomb(
                        bomb,
                        myPos,
                        dirForSize,
                        physicallyStillInside,
                        out Vector2 plantDir))
                {
                    CancelBombReentryCentering();

                    LogBombEscape(
                        $"ALLOW same-direction trigger bomb traversal bomb:{bomb.name} " +
                        $"myPos:{myPos} target:{targetPosition} bombPos:{bombPos} " +
                        $"currentDir:{NormalizeCardinal(dirForSize)} plantDir:{plantDir} " +
                        $"physicallyStillInside:{physicallyStillInside}",
                        verbose: false);

                    continue;
                }

                bool nearReentryKickEdge = IsNearBombReentryKickEdge(myPos, bombPos, dirForSize);

                LogBombEscape(
                    $"REENTRY CHECK bomb:{bomb.name} " +
                    $"myPos:{myPos} target:{targetPosition} bombPos:{bombPos} " +
                    $"overlapsCurrent:{overlapsCurrent} overlapsTarget:{overlapsTarget} " +
                    $"physicallyStillInside:{physicallyStillInside} " +
                    $"nearReentryKickEdge:{nearReentryKickEdge}",
                    verbose: false);

                if (!nearReentryKickEdge)
                {
                    LogBombEscape(
                        $"ALLOW reentry trigger bomb traversal below kick threshold bomb:{bomb.name} " +
                        $"myPos:{myPos} target:{targetPosition} bombPos:{bombPos} " +
                        $"minCenterOffset:{bombKickMinCenterOffset:F3}",
                        verbose: false);

                    continue;
                }

                if (allowMovementAbilities && bombCollider != null)
                {
                    for (int i = 0; i < movementAbilities.Length; i++)
                    {
                        var ability = movementAbilities[i];
                        if (ability != null && ability.IsEnabled)
                        {
                            if (ability.TryHandleBlockedHit(bombCollider, dirForSize, tileSize, obstacleMask))
                            {
                                if (bomb.IsBeingKicked)
                                    _lastAdjKickedBomb = bomb;

                                if (!nearReentryKickEdge)
                                {
                                    LogBombEscape(
                                        $"REENTRY kick handled before centering bomb:{bomb.name} " +
                                        $"myPos:{myPos} target:{targetPosition} bombPos:{bombPos} " +
                                        $"nearReentryKickEdge:{nearReentryKickEdge}",
                                        verbose: false);
                                }

                                CancelBombReentryCentering();
                                return IsBlockedAtPosition(targetPosition, dirForSize, false);
                            }
                        }
                    }
                }

                if (bombReentryCenteringActive)
                    bombReentryCenteringActive = false;

                Vector2 reentryCenterTarget = GetBombReentryCenterTarget(bombPos, dirForSize);

                LogBombEscape(
                    $"REENTRY CENTER START bomb:{bomb.name} " +
                    $"myPos:{myPos} targetCenter:{reentryCenterTarget} " +
                    $"hasBombKick:{(abilitySystem != null && abilitySystem.IsEnabled(BombKickAbility.AbilityId))}",
                    verbose: false);

                StartBombReentryCentering(myPos, reentryCenterTarget);

                LogBombEscape(
                    $"BLOCKED by REENTRY logic bomb:{bomb.name} " +
                    $"myPos:{myPos} target:{targetPosition} bombPos:{bombPos} " +
                    $"reason:cannot re-enter trigger bomb tile even with kick ability",
                    verbose: false);

                return true;
            }
        }

        return false;
    }

    public void NotifyBombPlanted(Bomb bomb, Vector2 movementDirectionAtPlant)
    {
        if (bomb == null)
            return;

        Vector2 storedDir = NormalizeCardinal(movementDirectionAtPlant);
        if (storedDir == Vector2.zero)
            storedDir = NormalizeCardinal(facingDirection);

        if (storedDir == Vector2.zero)
            storedDir = Vector2.down;

        _ownedBombPlantTraversal[bomb] = new BombPlantTraversalState
        {
            PlantDirection = storedDir,
            DirectionChangedSincePlant = false
        };

        LogBombEscape(
            $"Track planted bomb traversal bomb:{bomb.name} bombPos:{bomb.GetLogicalPosition()} " +
            $"storedDir:{storedDir}",
            verbose: true);
    }

    private void UpdateOwnedBombPlantTraversal(Vector2 currentDirection)
    {
        if (_ownedBombPlantTraversal.Count == 0)
            return;

        Vector2 normalizedCurrentDirection = NormalizeCardinal(currentDirection);
        List<Bomb> bombsToRemove = null;
        List<Bomb> bombsToLock = null;

        foreach (var kv in _ownedBombPlantTraversal)
        {
            Bomb bomb = kv.Key;
            BombPlantTraversalState state = kv.Value;

            if (bomb == null || bomb.HasExploded || bomb.IsSolid || bomb.IsBeingKicked || bomb.Owner != bombController)
            {
                bombsToRemove ??= new List<Bomb>();
                bombsToRemove.Add(bomb);
                continue;
            }

            if (state.DirectionChangedSincePlant || normalizedCurrentDirection == Vector2.zero)
                continue;

            Vector2 plantDir = NormalizeCardinal(state.PlantDirection);
            if (plantDir == Vector2.zero)
                continue;

            float directionDot = Vector2.Dot(plantDir, normalizedCurrentDirection);
            if (directionDot > 0.1f)
                continue;

            bombsToLock ??= new List<Bomb>();
            bombsToLock.Add(bomb);
        }

        if (bombsToRemove != null)
        {
            for (int i = 0; i < bombsToRemove.Count; i++)
                _ownedBombPlantTraversal.Remove(bombsToRemove[i]);
        }

        if (bombsToLock == null)
            return;

        for (int i = 0; i < bombsToLock.Count; i++)
        {
            Bomb bomb = bombsToLock[i];
            if (bomb == null)
                continue;

            if (!_ownedBombPlantTraversal.TryGetValue(bomb, out BombPlantTraversalState state))
                continue;

            Vector2 plantDir = NormalizeCardinal(state.PlantDirection);
            state.DirectionChangedSincePlant = true;
            _ownedBombPlantTraversal[bomb] = state;

            LogBombEscape(
                $"Track planted bomb traversal LOCKED bomb:{bomb.name} bombPos:{bomb.GetLogicalPosition()} " +
                $"plantDir:{plantDir} currentDir:{normalizedCurrentDirection}",
                verbose: false);
        }
    }

    private bool ShouldAllowForwardTraversalThroughOwnTriggerBomb(
        Bomb bomb,
        Vector2 playerPos,
        Vector2 moveDir,
        bool physicallyStillInside,
        out Vector2 plantDir)
    {
        plantDir = Vector2.zero;

        if (!physicallyStillInside || bomb == null || bomb.Owner != bombController)
            return false;

        if (!_ownedBombPlantTraversal.TryGetValue(bomb, out BombPlantTraversalState state))
            return false;

        if (state.DirectionChangedSincePlant)
            return false;

        moveDir = NormalizeCardinal(moveDir);
        plantDir = NormalizeCardinal(state.PlantDirection);

        if (moveDir == Vector2.zero || plantDir == Vector2.zero)
            return false;

        float directionDot = Vector2.Dot(plantDir, moveDir);
        if (directionDot <= 0.1f)
            return false;

        Vector2 delta = playerPos - bomb.GetLogicalPosition();
        float signedAxisOffset =
            Mathf.Abs(moveDir.x) > 0.01f
                ? delta.x * Mathf.Sign(moveDir.x)
                : delta.y * Mathf.Sign(moveDir.y);

        return signedAxisOffset <= alignEpsilon;
    }

    private bool IsBlockedAtPositionIgnoringBomb(Vector2 targetPosition, Vector2 dirForSize, Bomb ignoreBomb)
    {
        Vector2 size = GetBlockProbeSize(dirForSize);

        int hitCount = GetObstacleHitCount(targetPosition, size);
        if (hitCount > 0)
        {
            bool canPassDestructibles = abilitySystem != null &&
                                       abilitySystem.IsEnabled(DestructiblePassAbility.AbilityId);

            for (int h = 0; h < hitCount; h++)
            {
                var hit = obstacleOverlapBuffer[h];
                obstacleOverlapBuffer[h] = null;
                if (hit == null) continue;
                if (hit.gameObject == gameObject) continue;

                var hitBombEarly = hit.GetComponent<Bomb>();
                if (hitBombEarly != null && !hitBombEarly.IsSolid)
                    continue;

                if (hit.isTrigger) continue;

                if (canPassTaggedObstacles && hit.CompareTag(passObstacleTag))
                    continue;

                if (canPassDestructibles && hit.CompareTag("Destructibles"))
                    continue;

                if (ignoreBomb != null && hit.transform.IsChildOf(ignoreBomb.transform))
                    continue;

                var hitBomb = hitBombEarly != null ? hitBombEarly : hit.GetComponent<Bomb>();
                if (hitBomb != null && !hitBomb.IsSolid)
                    continue;

                return true;
            }
        }

        bool hasBombPass = abilitySystem != null && abilitySystem.IsEnabled(BombPassAbility.AbilityId);
        if (!hasBombPass)
        {
            Vector2 myPos = Rigidbody != null ? Rigidbody.position : (Vector2)transform.position;

            foreach (var bomb in Bomb.ActiveBombs)
            {
                if (bomb == null || bomb.HasExploded)
                    continue;

                if (bomb == ignoreBomb)
                    continue;

                if (bomb.IsSolid || bomb.IsBeingPunched)
                    continue;

                Vector2 bombPos = bomb.GetLogicalPosition();

                if (!IsInsideBombTileFootprint(targetPosition, bombPos))
                    continue;

                bool overlapsCurrent = IsInsideBombTileFootprint(myPos, bombPos);
                if (overlapsCurrent)
                    continue;

                return true;
            }
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
            SetFacingDirection(newDirection, "SetDirection");

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
            bool fromExplosion = (layer == explosionLayer);

            if (fromExplosion &&
                cachedHealth.life <= 1 &&
                BattleRevengeSystem.Instance != null &&
                BattleRevengeSystem.Instance.TryHandleLethalRevengeHit(this, other))
            {
                nextContactDamageTime = Time.time + cd;
                return;
            }

            cachedHealth.TakeDamage(1, fromExplosion);
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

        deathRequestedByExplosion = false;

        if (!isDead)
            DeathSequence();
    }

    public virtual void KillByExplosion()
    {
        if (isEndingStage)
            return;

        deathRequestedByExplosion = true;

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

    private void BeginArenaRemovalCommon(bool notifyGameManagerDeath, bool resetStagePowerups)
    {
        isDead = true;
        inputLocked = true;
        inactivityMountedDownOverride = false;

        ResetDualInputAxes();

        if (spriteLock != null && spriteLock.IsLocked)
            spriteLock.EndLock();

        if (stunReceiver != null)
            stunReceiver.CancelStunForDeath();

        if (TryGetComponent<PowerGloveAbility>(out var glove) && glove != null)
            glove.DestroyHeldBombIfHolding();

        if (IsPlayer() &&
            notifyGameManagerDeath &&
            checkWinStateOnDeath &&
            !battleRevengeSwapDeathPending)
        {
            var gm = FindAnyObjectByType<GameManager>();
            if (gm != null)
                gm.NotifyPlayerDeathStarted(this);
        }

        touchingHazards.Clear();

        if (IsPlayer())
        {
            if (TryGetComponent<MountEggQueue>(out var q) && q != null)
                q.ClearQueueNow(resetHistoryToOwner: true, animateShift: false);

            if (resetStagePowerups)
                PlayerPersistentStats.StageResetTemporaryPowerupsOnDeath(playerId);
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

    private void BeginDeathCommon()
    {
        BeginArenaRemovalCommon(notifyGameManagerDeath: true, resetStagePowerups: true);
    }

    protected virtual void DeathSequence()
    {
        if (isDead || isEndingStage)
            return;

        NotifyHudPortraitDeathIfPlayer();

        holeDeathInProgress = false;

        BeginDeathCommon();

        if (audioSource != null && deathSfx != null)
            audioSource.PlayOneShot(deathSfx);

        DisableAllFootSprites();
        DisableAllMountedSprites();

        SetAnimEnabled(spriteRendererCheering, false);
        SetAnimEnabled(spriteRendererEndStage, false);
        SetAnimEnabled(spriteRendererDeath, false);
        SetAnimEnabled(spriteRendererDeathByExplosion, false);
        SetAnimEnabled(spriteRendererFall, false);

        AnimatedSpriteRenderer deathRendererToUse =
            deathRequestedByExplosion && spriteRendererDeathByExplosion != null
                ? spriteRendererDeathByExplosion
                : spriteRendererDeath;

        if (deathRendererToUse != null)
        {
            SetAnimEnabled(deathRendererToUse, true);
            deathRendererToUse.idle = false;
            deathRendererToUse.loop = false;
            deathRendererToUse.pingPong = false;
            deathRendererToUse.CurrentFrame = 0;
            activeSpriteRenderer = deathRendererToUse;
            deathRendererToUse.RefreshFrame();
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
        deathRequestedByExplosion = false;

        if (battleRevengeSwapDeathPending)
        {
            battleRevengeSwapDeathPending = false;

            Action callback = battleRevengeSwapDeathCompleted;
            battleRevengeSwapDeathCompleted = null;

            callback?.Invoke();
            return;
        }

        BattleRevengeSystem.Instance?.HandlePlayerDeathCompleted(this);
        Died?.Invoke(this);
        gameObject.SetActive(false);

        if (CompareTag("BossBomber"))
            return;

        if (!checkWinStateOnDeath)
            return;
    }

    public void RemoveForBattleRevengeSwap()
    {
        if (isEndingStage)
            return;

        CancelInvoke(nameof(OnDeathSequenceEnded));
        holeDeathInProgress = false;
        deathRequestedByExplosion = false;

        if (_holeDeathVisualRoutine != null)
        {
            StopCoroutine(_holeDeathVisualRoutine);
            _holeDeathVisualRoutine = null;
        }

        NotifyHudPortraitDeathIfPlayer();
        BeginArenaRemovalCommon(notifyGameManagerDeath: false, resetStagePowerups: false);

        if (cachedCompanion == null)
            TryGetComponent(out cachedCompanion);

        if (cachedCompanion != null)
            cachedCompanion.ClearMountedStateForForcedArenaRemoval();

        transform.localScale = Vector3.one;
        SetAllSpritesVisible(false);
        gameObject.SetActive(false);
    }

    public void RespawnFromBattleRevenge(Vector2 worldPosition, float invulnerabilitySeconds, float blinkInterval)
    {
        CancelInvoke(nameof(OnDeathSequenceEnded));
        holeDeathInProgress = false;
        deathRequestedByExplosion = false;
        isEndingStage = false;
        isDead = false;
        inputLocked = false;
        inactivityMountedDownOverride = false;
        nextContactDamageTime = 0f;
        direction = Vector2.zero;
        hasInput = false;

        if (_holeDeathVisualRoutine != null)
        {
            StopCoroutine(_holeDeathVisualRoutine);
            _holeDeathVisualRoutine = null;
        }

        ResetDualInputAxes();
        ResetSingleInputTurnState();
        touchingHazards.Clear();

        if (spriteLock != null && spriteLock.IsLocked)
            spriteLock.EndLock();

        if (cachedCompanion == null)
            TryGetComponent(out cachedCompanion);

        if (cachedCompanion != null)
            cachedCompanion.ClearMountedStateForForcedArenaRemoval();

        if (TryGetComponent<PowerGloveAbility>(out var glove) && glove != null)
            glove.DestroyHeldBombIfHolding();

        if (Rigidbody != null)
        {
            Rigidbody.simulated = true;
            Rigidbody.linearVelocity = Vector2.zero;
            Rigidbody.angularVelocity = 0f;
            Rigidbody.position = worldPosition;
        }

        transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
        transform.localScale = Vector3.one;

        if (TryGetComponent(out Collider2D col) && col != null)
            col.enabled = true;

        if (cachedHealth == null)
            cachedHealth = GetComponent<CharacterHealth>();

        int respawnLife = cachedHealth != null ? Mathf.Max(1, cachedHealth.life) : 1;
        cachedHealth?.ResetForRespawn(respawnLife);

        if (bombController != null)
        {
            bombController.enabled = true;
            PlayerPersistentStats.LoadInto(playerId, this, bombController);
            bombController.ResetRuntimeStateAfterRespawn();
        }

        CacheMovementAbilities();
        SyncMountedFromPersistent();
        ApplySpeedInternal(speedInternal);
        SetExplosionInvulnerable(false);
        SetExternalVisualSuppressed(false);
        EnableExclusiveFromState();

        if (cachedHealth != null && invulnerabilitySeconds > 0f)
            cachedHealth.StartSpawnInvulnerability(invulnerabilitySeconds, blinkInterval);

        NotifyHudPortraitRespawnIfPlayer();
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
        SetAnimEnabled(spriteRendererDeathByExplosion, false);
        SetAnimEnabled(spriteRendererFall, false);

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
            r.idle = false;
            r.loop = true;
            r.pingPong = false;
            r.CurrentFrame = 0;
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
        if (spriteRendererFall != null)
            return spriteRendererFall;

        if (activeSpriteRenderer != null)
            return activeSpriteRenderer;

        if (isMounted)
        {
            var r = PickMountedRenderer(facingDirection);
            if (r != null)
                return r;
        }

        return spriteRendererDown != null ? spriteRendererDown : null;
    }

    private IEnumerator HoleDeathSinkVisualRoutine(AnimatedSpriteRenderer r)
    {
        if (r == null)
            yield break;

        SetAnimEnabled(r, true);
        r.idle = false;
        r.loop = true;
        r.pingPong = false;
        r.CurrentFrame = 0;
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

    private void NotifyHudPortraitDeathIfPlayer()
    {
        if (!IsPlayer())
            return;

        var hud = FindAnyObjectByType<HudPortraitInGridLayout>();
        if (hud != null)
            hud.OnPlayerDied(PlayerId);

        var battleHud = FindAnyObjectByType<BattleModeHud>();
        if (battleHud != null)
            battleHud.OnPlayerDied(PlayerId);
    }

    private void NotifyHudPortraitRespawnIfPlayer()
    {
        if (!IsPlayer())
            return;

        var hud = FindAnyObjectByType<HudPortraitInGridLayout>();
        if (hud != null)
            hud.OnPlayerRespawn(PlayerId);

        var battleHud = FindAnyObjectByType<BattleModeHud>();
        if (battleHud != null)
            battleHud.OnPlayerRespawn(PlayerId);
    }

    public void PlayEndStageSequence(Vector2 portalCenter, bool snapToPortalCenter)
    {
        isEndingStage = true;
        SetExplosionInvulnerable(true);

        if (abilitySystem != null)
            abilitySystem.DisableAll();

        inputLocked = true;
        inactivityMountedDownOverride = false;
        ResetDualInputAxes();

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
        SetAnimEnabled(spriteRendererDeathByExplosion, false);
        SetAnimEnabled(spriteRendererCheering, false);
        SetAnimEnabled(spriteRendererEndStage, false);
        SetAnimEnabled(spriteRendererFall, false);

        if (isMounted)
        {
            SetFacingDirection(Vector2.down, "PlayEndStageSequence");

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

    public void SetInputLocked(bool locked, bool forceIdle, Vector2 idleFacing)
    {
        inputLocked = locked;
        if (locked) ResetDualInputAxes();

        if (locked && forceIdle)
            ForceIdleFacing(idleFacing, "SetInputLockedFacing");
    }

    public void SetInputLocked(bool locked, bool forceIdle)
    {
        inputLocked = locked;
        if (locked) ResetDualInputAxes();

        if (locked && forceIdle)
            ForceIdleUp();
    }

    public void SetInputLocked(bool locked)
    {
        SetInputLocked(locked, true);
    }

    public void ForceIdleFacing(Vector2 faceDir, string reason = "ForceIdleFacing")
    {
        direction = Vector2.zero;
        hasInput = false;
        ResetDualInputAxes();

        faceDir = NormalizeCardinal(faceDir);
        if (faceDir == Vector2.zero)
            faceDir = Vector2.down;

        SetFacingDirection(faceDir, reason);

        DisableAllFootSprites();
        DisableAllMountedSprites();

        AnimatedSpriteRenderer target = isMounted ? PickMountedRenderer(faceDir) : PickFootRenderer(faceDir);
        if (target == null)
            target = isMounted ? mountedSpriteDown : spriteRendererDown;

        SetDirection(Vector2.zero, target);
    }

    public void ForceIdleUp()
    {
        direction = Vector2.zero;
        hasInput = false;
        ResetDualInputAxes();

        SetFacingDirection(Vector2.up, "ForceIdleUp");

        DisableAllFootSprites();
        DisableAllMountedSprites();

        if (spriteRendererUp != null)
            SetDirection(Vector2.zero, spriteRendererUp);
        else
            SetDirection(Vector2.zero, activeSpriteRenderer);
    }

    public void ForceIdleUpConsideringMount()
    {
        direction = Vector2.zero;
        hasInput = false;
        ResetDualInputAxes();

        SetFacingDirection(Vector2.up, "ForceIdleUpConsideringMount");

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
        ResetDualInputAxes();

        SetFacingDirection(Vector2.up, "ForceMountedUpExclusive");

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
                SetFacingDirection(Vector2.down, "SetMountedOnLouie");

            direction = Vector2.zero;
            hasInput = false;
            ResetDualInputAxes();

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

        SetFacingDirection(faceDir, "ForceFacingDirection");

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
        return !IsBlockedAtPosition(ahead, moveDir, true);
    }

    protected bool CanAlignToPerpendicularTarget(Vector2 candidatePos, Vector2 moveDir)
    {
        if (IsBlockedAtPosition(candidatePos, moveDir, true))
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

        return IsBlockedAtPosition(probe, dir, true);
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
            LogCurve(
                $"QuantizedStep direction changed {lastMoveDirCardinal} -> {moveDir}. " +
                $"ResetPixelAccumulators accX:{accPixelsX:F4} accY:{accPixelsY:F4}");
            lastMoveDirCardinal = moveDir;
            ResetPixelAccumulators();
        }

        float rawPixels = rawWorldStep * pixelsPerUnit;

        if (Mathf.Abs(moveDir.x) > 0.01f)
        {
            accPixelsX += rawPixels * Mathf.Sign(moveDir.x);

            int whole = (int)accPixelsX;
            accPixelsX -= whole;

            float result = Mathf.Abs(whole) * PixelWorldStep;

            LogCurve(
                $"QuantizedStep X rawWorld:{rawWorldStep:F4} rawPixels:{rawPixels:F4} whole:{whole} " +
                $"accXRest:{accPixelsX:F4} result:{result:F4}",
                verbose: true);

            return result;
        }
        else
        {
            accPixelsY += rawPixels * Mathf.Sign(moveDir.y);

            int whole = (int)accPixelsY;
            accPixelsY -= whole;

            float result = Mathf.Abs(whole) * PixelWorldStep;

            LogCurve(
                $"QuantizedStep Y rawWorld:{rawWorldStep:F4} rawPixels:{rawPixels:F4} whole:{whole} " +
                $"accYRest:{accPixelsY:F4} result:{result:F4}",
                verbose: true);

            return result;
        }
    }

    private void MovePositionPixelPerfect(Vector2 worldPos)
    {
        if (Rigidbody == null)
            return;

        Vector2 quantized = QuantizeToPixelGrid(worldPos);

        Rigidbody.MovePosition(quantized);
    }

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
            ResetDualInputAxes();

            if (Rigidbody != null)
                Rigidbody.linearVelocity = Vector2.zero;
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

    public void ShowSpringLauncherLookUp(Vector2 faceDir)
    {
        if (isDead || isEndingStage || IsRidingPlaying())
            return;

        faceDir = NormalizeCardinal(faceDir);
        if (faceDir == Vector2.zero)
            faceDir = facingDirection != Vector2.zero ? facingDirection : Vector2.down;

        SetFacingDirection(faceDir, "ShowSpringLauncherLookUp");

        SetVisualOverrideActive(true);

        DisableAllFootSprites();
        DisableAllMountedSprites();

        SetAnimEnabled(spriteRendererDeath, false);
        SetAnimEnabled(spriteRendererDeathByExplosion, false);
        SetAnimEnabled(spriteRendererEndStage, false);
        SetAnimEnabled(spriteRendererCheering, false);
        SetAnimEnabled(spriteRendererFall, false);

        SetAnimEnabled(springLookUpUp, false);
        SetAnimEnabled(springLookUpDown, false);
        SetAnimEnabled(springLookUpLeft, false);
        SetAnimEnabled(springLookUpRight, false);

        AnimatedSpriteRenderer target = PickSpringLauncherLookUpRenderer(faceDir);
        if (target == null)
        {
            ForceIdleFacing(faceDir, "ShowSpringLauncherLookUpFallback");
            return;
        }

        SetAnimEnabled(target, true);
        target.idle = false;
        target.loop = true;
        target.pingPong = false;
        target.CurrentFrame = 0;
        target.RefreshFrame();

        activeSpriteRenderer = target;
        ApplyFlipForHorizontal(faceDir);
    }

    public void ClearSpringLauncherLookUp()
    {
        SetAnimEnabled(springLookUpUp, false);
        SetAnimEnabled(springLookUpDown, false);
        SetAnimEnabled(springLookUpLeft, false);
        SetAnimEnabled(springLookUpRight, false);

        if (isDead || isEndingStage || IsRidingPlaying())
            return;

        SetVisualOverrideActive(false);
    }

    private AnimatedSpriteRenderer PickSpringLauncherLookUpRenderer(Vector2 dir)
    {
        Vector2 face = NormalizeCardinal(dir);
        if (face == Vector2.zero)
            face = Vector2.up;

        if (face == Vector2.up)
            return springLookUpUp;

        if (face == Vector2.down)
            return springLookUpDown;

        if (face == Vector2.left)
            return springLookUpLeft;

        if (face == Vector2.right)
            return springLookUpRight;

        return springLookUpUp;
    }

    private Vector2 GetBlockProbeSize(Vector2 dirForSize)
    {
        return Mathf.Abs(dirForSize.x) > 0.01f
            ? new Vector2(tileSize * 0.6f, tileSize * 0.2f)
            : new Vector2(tileSize * 0.2f, tileSize * 0.6f);
    }

    private bool IsInsideBombTileFootprint(Vector2 worldPos, Vector2 bombPos)
    {
        float halfFootprint = tileSize * 0.35f;

        return Mathf.Abs(worldPos.x - bombPos.x) <= halfFootprint
            && Mathf.Abs(worldPos.y - bombPos.y) <= halfFootprint;
    }

    private bool IsExactlyAlignedForAxisSwap(Vector2 newDir)
    {
        if (tileSize <= 0.0001f)
            return true;

        Vector2 pos = Rigidbody != null ? Rigidbody.position : (Vector2)transform.position;

        if (Mathf.Abs(newDir.x) > 0.01f)
        {
            float nearestY = Mathf.Round(pos.y / tileSize) * tileSize;
            return Mathf.Abs(pos.y - nearestY) <= alignEpsilon;
        }
        else
        {
            float nearestX = Mathf.Round(pos.x / tileSize) * tileSize;
            return Mathf.Abs(pos.x - nearestX) <= alignEpsilon;
        }
    }

    private void HandleSingleAxisTurn(Vector2 singleDir, bool exitedDualInput = false)
    {
        singleDir = NormalizeCardinal(singleDir);

        LogCurve(
            $"HandleSingleAxisTurn requested:{singleDir} " +
            $"currentDir:{direction} currentAxis:{currentAxis} " +
            $"pendingSingleBefore:{pendingSingleTurnDirection} " +
            $"exitedDual:{exitedDualInput}",
            verbose: true);

        if (singleDir == Vector2.zero)
        {
            pendingSingleTurnDirection = Vector2.zero;
            lockedMovementDirection = Vector2.zero;
            ApplyDirectionFromVector(Vector2.zero);
            return;
        }

        if (direction == Vector2.zero)
        {
            pendingSingleTurnDirection = Vector2.zero;
            lockedMovementDirection = Vector2.zero;

            LogCurve($"HandleSingleAxisTurn -> immediate apply from idle:{singleDir}");
            ApplyDirectionFromVector(singleDir);
            return;
        }

        bool sameAxis =
            (Mathf.Abs(direction.x) > 0.01f && Mathf.Abs(singleDir.x) > 0.01f) ||
            (Mathf.Abs(direction.y) > 0.01f && Mathf.Abs(singleDir.y) > 0.01f);

        if (sameAxis)
        {
            pendingSingleTurnDirection = Vector2.zero;
            lockedMovementDirection = Vector2.zero;

            LogCurve($"HandleSingleAxisTurn -> same axis apply:{singleDir}");
            ApplyDirectionFromVector(singleDir);
            return;
        }

        bool moveBlocked = IsMoveBlocked(singleDir);
        bool nearCentre = IsAtTileCentreOnPerpendicularAxis(singleDir);
        bool exactlyAligned = IsExactlyAlignedForAxisSwap(singleDir);

        LogCurve(
            $"HandleSingleAxisTurn requested:{singleDir} " +
            $"moveBlocked:{moveBlocked} nearCentre:{nearCentre} exactlyAligned:{exactlyAligned} " +
            $"currentDir:{direction} pendingSingleBefore:{pendingSingleTurnDirection} " +
            $"exitedDual:{exitedDualInput}");

        if (!moveBlocked && (nearCentre || exactlyAligned))
        {
            pendingSingleTurnDirection = Vector2.zero;
            lockedMovementDirection = Vector2.zero;

            LogCurve($"HandleSingleAxisTurn -> APPLY turn to:{singleDir}");
            ApplyDirectionFromVector(singleDir);
            return;
        }

        if (exitedDualInput)
        {
            Vector2 alignmentDirection = GetSingleTurnAlignmentDirection(direction, singleDir);
            bool canUseAlignmentDirection =
                alignmentDirection != Vector2.zero &&
                !IsSingleTurnAlignmentBlocked(alignmentDirection, singleDir);

            pendingSingleTurnDirection = singleDir;

            if (canUseAlignmentDirection)
            {
                lockedMovementDirection = alignmentDirection;

                LogCurve(
                    $"HandleSingleAxisTurn -> dual-exit align pendingSingle:{pendingSingleTurnDirection} " +
                    $"alignDir:{alignmentDirection}");

                ApplyDirectionFromVector(alignmentDirection);
                return;
            }

            lockedMovementDirection = Vector2.zero;

            LogCurve(
                $"HandleSingleAxisTurn -> dual-exit stop pendingSingle:{pendingSingleTurnDirection} " +
                $"moveBlocked:{moveBlocked} alignDir:{alignmentDirection}");

            ApplyDirectionFromVector(Vector2.zero);
            return;
        }

        pendingSingleTurnDirection = singleDir;
        lockedMovementDirection = direction;

        LogCurve($"HandleSingleAxisTurn -> arm pendingSingle:{pendingSingleTurnDirection} keep current:{direction}");
        ApplyDirectionFromVector(direction);
    }

    private bool IsSingleTurnAlignmentBlocked(Vector2 alignDir, Vector2 requestedTurnDir)
    {
        alignDir = NormalizeCardinal(alignDir);
        requestedTurnDir = NormalizeCardinal(requestedTurnDir);

        if (alignDir == Vector2.zero || tileSize <= 0.0001f)
            return false;

        Vector2 pos = Rigidbody != null ? Rigidbody.position : (Vector2)transform.position;

        float centerX = Mathf.Round(pos.x / tileSize) * tileSize;
        float centerY = Mathf.Round(pos.y / tileSize) * tileSize;

        Vector2 alignTarget =
            Mathf.Abs(alignDir.x) > 0.01f
                ? new Vector2(centerX, pos.y)
                : new Vector2(pos.x, centerY);

        alignTarget = QuantizeToPixelGrid(alignTarget);

        if (IsMoveBlocked(alignDir))
            return true;

        if (IsBlockedAtPosition(alignTarget, alignDir, true))
            return true;

        if (!CanAlignToPerpendicularTarget(alignTarget, requestedTurnDir))
            return true;

        return false;
    }

    private Vector2 GetSingleTurnAlignmentDirection(Vector2 currentMove, Vector2 requestedDir)
    {
        if (tileSize <= 0.0001f)
            return currentMove;

        Vector2 pos = Rigidbody != null ? Rigidbody.position : (Vector2)transform.position;

        bool turningToHorizontal = Mathf.Abs(requestedDir.x) > 0.01f;
        bool turningToVertical = Mathf.Abs(requestedDir.y) > 0.01f;

        if (turningToVertical)
        {
            float centerX = Mathf.Round(pos.x / tileSize) * tileSize;
            float deltaX = centerX - pos.x;

            if (Mathf.Abs(deltaX) <= alignEpsilon)
                return Vector2.zero;

            return deltaX > 0f ? Vector2.right : Vector2.left;
        }

        if (turningToHorizontal)
        {
            float centerY = Mathf.Round(pos.y / tileSize) * tileSize;
            float deltaY = centerY - pos.y;

            if (Mathf.Abs(deltaY) <= alignEpsilon)
                return Vector2.zero;

            return deltaY > 0f ? Vector2.up : Vector2.down;
        }

        return currentMove;
    }

    private bool ShouldHardBlockForwardFromTileCenter(Vector2 position)
    {
        if (direction == Vector2.zero || tileSize <= 0.0001f)
            return false;

        Vector2 dir = NormalizeCardinal(direction);
        if (dir == Vector2.zero)
            return false;

        bool movingHorizontal = Mathf.Abs(dir.x) > 0.01f;

        float centeredX = Mathf.Round(position.x / tileSize) * tileSize;
        float centeredY = Mathf.Round(position.y / tileSize) * tileSize;

        bool alignedOnPerpendicular =
            movingHorizontal
                ? Mathf.Abs(position.y - centeredY) <= alignEpsilon
                : Mathf.Abs(position.x - centeredX) <= alignEpsilon;

        if (!alignedOnPerpendicular)
            return false;

        bool alreadyAtCurrentTileCenterOnMoveAxis =
            movingHorizontal
                ? Mathf.Abs(position.x - centeredX) <= alignEpsilon
                : Mathf.Abs(position.y - centeredY) <= alignEpsilon;

        if (!alreadyAtCurrentTileCenterOnMoveAxis)
            return false;

        Vector2 currentTileCenter = new(centeredX, centeredY);
        Vector2 nextTileCenter = currentTileCenter + dir * tileSize;

        bool nextBlocked = IsBlockedAtPosition(nextTileCenter, dir, true);

        if (nextBlocked && _lastAdjKickedBomb != null && _lastAdjKickedBomb.IsBeingKicked)
            nextBlocked = IsBlockedAtPositionIgnoringBomb(nextTileCenter, dir, _lastAdjKickedBomb);

        if (!nextBlocked)
            return false;

        if (pendingSingleTurnDirection != Vector2.zero &&
            IsExactlyAlignedForAxisSwap(pendingSingleTurnDirection) &&
            !IsMoveBlocked(pendingSingleTurnDirection))
        {
            lockedMovementDirection = pendingSingleTurnDirection;
            Vector2 turnDir = pendingSingleTurnDirection;
            pendingSingleTurnDirection = Vector2.zero;
            ApplyDirectionFromVector(turnDir);

            return false;
        }

        return true;
    }

    private bool IsNearBombEdgeForEarlyKick(Vector2 playerPos, Vector2 bombPos, Vector2 moveDir)
    {
        moveDir = moveDir.normalized;
        if (moveDir == Vector2.zero)
            return false;

        Vector2 delta = playerPos - bombPos;

        if (Mathf.Abs(moveDir.x) > 0.01f)
        {
            float signedOffsetX = delta.x * Mathf.Sign(moveDir.x);
            return signedOffsetX >= bombKickMinCenterOffset;
        }

        if (Mathf.Abs(moveDir.y) > 0.01f)
        {
            float signedOffsetY = delta.y * Mathf.Sign(moveDir.y);
            return signedOffsetY >= bombKickMinCenterOffset;
        }

        return false;
    }

    private bool IsNearBombReentryKickEdge(Vector2 playerPos, Vector2 bombPos, Vector2 moveDir)
    {
        moveDir = moveDir.normalized;
        if (moveDir == Vector2.zero)
            return false;

        Vector2 delta = playerPos - bombPos;

        if (Mathf.Abs(moveDir.x) > 0.01f)
        {
            float signedOffsetX = -delta.x * Mathf.Sign(moveDir.x);
            return signedOffsetX >= bombKickMinCenterOffset;
        }

        if (Mathf.Abs(moveDir.y) > 0.01f)
        {
            float signedOffsetY = -delta.y * Mathf.Sign(moveDir.y);
            return signedOffsetY >= bombKickMinCenterOffset;
        }

        return false;
    }

    private bool TryKickAdjacentBombFromCurrentTile(Vector2 position, Vector2 moveDir)
    {
        if (moveDir == Vector2.zero)
        {
            LogBombEscape($"[KickAdj] skip moveDir zero pos:{position}", verbose: true);
            return false;
        }

        if (movementAbilities == null || movementAbilities.Length == 0)
        {
            LogBombEscape($"[KickAdj] skip no movementAbilities pos:{position}", verbose: true);
            return false;
        }

        Vector2 dir = NormalizeCardinal(moveDir);
        if (dir == Vector2.zero)
        {
            LogBombEscape($"[KickAdj] skip normalized dir zero moveDir:{moveDir} pos:{position}", verbose: true);
            return false;
        }

        for (int i = 0; i < movementAbilities.Length; i++)
        {
            if (movementAbilities[i] is BombKickAbility kickAbility && kickAbility.IsEnabled)
                kickAbility.NotifyOwnerDirectionChanged(dir);
        }

        Vector2 snappedCurrentTile = new Vector2(
            Mathf.Round(position.x / tileSize) * tileSize,
            Mathf.Round(position.y / tileSize) * tileSize
        );

        Vector2 targetTileCenter = snappedCurrentTile + dir * tileSize;

        LogBombEscape(
            $"[KickAdj] START pos:{position} dir:{dir} snappedCurrentTile:{snappedCurrentTile} " +
            $"targetTileCenter:{targetTileCenter} tileSize:{tileSize:F3}",
            verbose: true);

        foreach (var bomb in Bomb.ActiveBombs)
        {
            if (bomb == null)
            {
                LogBombEscape("[KickAdj] skip null bomb", verbose: true);
                continue;
            }

            if (bomb.HasExploded)
            {
                LogBombEscape($"[KickAdj] skip exploded bomb:{bomb.name}", verbose: true);
                continue;
            }

            if (bomb.IsBeingKicked || bomb.IsBeingPunched)
            {
                LogBombEscape(
                    $"[KickAdj] skip moving bomb:{bomb.name} kicked:{bomb.IsBeingKicked} punched:{bomb.IsBeingPunched}",
                    verbose: true);
                continue;
            }

            Vector2 bombPos = bomb.GetLogicalPosition();
            float bombToTargetTileDist = Vector2.Distance(bombPos, targetTileCenter);
            float playerToBombDist = Vector2.Distance(position, bombPos);
            Vector2 playerMinusBomb = position - bombPos;

            float signedAxisOffset =
                Mathf.Abs(dir.x) > 0.01f
                    ? playerMinusBomb.x * Mathf.Sign(dir.x)
                    : playerMinusBomb.y * Mathf.Sign(dir.y);

            float perpendicularOffset =
                Mathf.Abs(dir.x) > 0.01f
                    ? Mathf.Abs(playerMinusBomb.y)
                    : Mathf.Abs(playerMinusBomb.x);

            bool bombMatchesTargetTile = bombToTargetTileDist <= 0.05f;

            LogBombEscape(
                $"[KickAdj] inspect bomb:{bomb.name} bombPos:{bombPos} bombMatchesTargetTile:{bombMatchesTargetTile} " +
                $"bombToTargetTileDist:{bombToTargetTileDist:F3} playerToBombDist:{playerToBombDist:F3} " +
                $"signedAxisOffset:{signedAxisOffset:F3} perpendicularOffset:{perpendicularOffset:F3} " +
                $"bombIsSolid:{bomb.IsSolid} canBeKicked:{bomb.CanBeKicked} canBeKickedEarly:{bomb.CanBeKickedEarly} " +
                $"isTrigger:{(bomb.GetComponent<Collider2D>() != null ? bomb.GetComponent<Collider2D>().isTrigger : false)}",
                verbose: true);

            if (!bombMatchesTargetTile)
                continue;

            var bombCollider = bomb.GetComponent<Collider2D>();
            if (bombCollider == null)
            {
                LogBombEscape($"[KickAdj] skip bomb without collider bomb:{bomb.name}", verbose: true);
                continue;
            }

            for (int i = 0; i < movementAbilities.Length; i++)
            {
                var ability = movementAbilities[i];
                if (ability == null)
                {
                    LogBombEscape($"[KickAdj] skip null ability idx:{i} bomb:{bomb.name}", verbose: true);
                    continue;
                }

                if (!ability.IsEnabled)
                {
                    LogBombEscape(
                        $"[KickAdj] skip disabled ability:{ability.GetType().Name} idx:{i} bomb:{bomb.name}",
                        verbose: true);
                    continue;
                }

                LogBombEscape(
                    $"[KickAdj] TRY ability:{ability.GetType().Name} bomb:{bomb.name} pos:{position} " +
                    $"bombPos:{bombPos} playerToBombDist:{playerToBombDist:F3} signedAxisOffset:{signedAxisOffset:F3} " +
                    $"perpendicularOffset:{perpendicularOffset:F3}",
                    verbose: true);

                bool handled = ability.TryHandleBlockedHit(bombCollider, dir, tileSize, obstacleMask);

                LogBombEscape(
                    $"[KickAdj] RESULT ability:{ability.GetType().Name} bomb:{bomb.name} handled:{handled} " +
                    $"playerPos:{position} bombPos:{bombPos}",
                    verbose: true);

                if (handled)
                {
                    _lastAdjKickedBomb = bomb;

                    LogBombEscape(
                        $"[KickAdj] SUCCESS bomb:{bomb.name} pos:{position} snappedCurrentTile:{snappedCurrentTile} " +
                        $"targetTileCenter:{targetTileCenter} playerToBombDist:{playerToBombDist:F3} " +
                        $"signedAxisOffset:{signedAxisOffset:F3} perpendicularOffset:{perpendicularOffset:F3}",
                        verbose: false);

                    return true;
                }
            }

            LogBombEscape(
                $"[KickAdj] FAIL bomb matched target tile but no ability handled it bomb:{bomb.name}",
                verbose: true);

            return false;
        }

        LogBombEscape(
            $"[KickAdj] END no adjacent bomb matched pos:{position} dir:{dir} targetTileCenter:{targetTileCenter}",
            verbose: true);

        return false;
    }

    private bool ShouldLogCurve()
    {
        return debugCurvas && (!IsPlayer() || playerId == debugCurvasPlayerId);
    }

    private void LogCurve(string msg, bool verbose = false)
    {
        if (!ShouldLogCurve())
            return;

        if (verbose && !debugCurvasVerbose)
            return;

        Debug.Log($"[MovementCurve][P{playerId}][f:{Time.frameCount}] {msg}", this);
    }

    private void LogBombEscape(string message, bool verbose = false)
    {
        if (!debugBombEscape)
            return;

        if (verbose && !debugBombEscapeVerbose)
            return;

        Debug.Log($"[BombEscape][Player:{name}] {message}", this);
    }

    private float GetOwnApproxRadius()
    {
        var col = GetComponent<Collider2D>();
        if (col == null)
            return 0.2f;

        Bounds b = col.bounds;
        return Mathf.Max(b.extents.x, b.extents.y);
    }

    private bool IsPhysicallyStillInsideTriggerBomb(Bomb bomb, Vector2 playerPos)
    {
        if (bomb == null || bomb.IsSolid || bomb.IsBeingPunched)
            return false;

        float playerRadius = GetOwnApproxRadius();
        float allowedDistance = bomb.ApproxRadius + playerRadius + 0.02f;

        Vector2 bombPos = bomb.GetLogicalPosition();
        float dist = Vector2.Distance(playerPos, bombPos);

        return dist <= allowedDistance;
    }

    private bool IsPhysicallyStillInsideTriggerBomb(Bomb bomb, Vector2 currentPos, Vector2 targetPos)
    {
        return IsPhysicallyStillInsideTriggerBomb(bomb, currentPos) ||
               IsPhysicallyStillInsideTriggerBomb(bomb, targetPos);
    }

    [Header("Bomb Reentry Centering")]
    [SerializeField, Range(0.01f, 0.25f)] private float bombReentryCenterDuration = 0.1f;

    private bool bombReentryCenteringActive;
    private Vector2 bombReentryCenterStart;
    private Vector2 bombReentryCenterTarget;
    private float bombReentryCenterElapsed;

    private void StartBombReentryCentering(Vector2 currentPosition, Vector2 targetPosition)
    {
        Vector2 quantizedCurrent = QuantizeToPixelGrid(currentPosition);
        Vector2 quantizedTarget = QuantizeToPixelGrid(targetPosition);

        if (Vector2.Distance(quantizedCurrent, quantizedTarget) <= PixelWorldStep * 0.5f)
        {
            bombReentryCenteringActive = false;

            if (Rigidbody != null)
                Rigidbody.MovePosition(quantizedTarget);

            return;
        }

        bombReentryCenteringActive = true;
        bombReentryCenterStart = quantizedCurrent;
        bombReentryCenterTarget = quantizedTarget;
        bombReentryCenterElapsed = 0f;
    }

    public void CancelBombReentryCentering()
    {
        bombReentryCenteringActive = false;
        bombReentryCenterElapsed = 0f;

        Vector2 current = Rigidbody != null ? Rigidbody.position : (Vector2)transform.position;
        current = QuantizeToPixelGrid(current);
        bombReentryCenterStart = current;
        bombReentryCenterTarget = current;
    }

    private bool UpdateBombReentryCentering()
    {
        if (!bombReentryCenteringActive)
            return false;

        if (Rigidbody == null)
        {
            bombReentryCenteringActive = false;
            return false;
        }

        if (bombReentryCenterDuration <= 0.0001f)
        {
            bombReentryCenteringActive = false;
            Rigidbody.MovePosition(QuantizeToPixelGrid(bombReentryCenterTarget));
            return true;
        }

        bombReentryCenterElapsed += Time.fixedDeltaTime;

        float t = Mathf.Clamp01(bombReentryCenterElapsed / bombReentryCenterDuration);
        Vector2 next = Vector2.Lerp(bombReentryCenterStart, bombReentryCenterTarget, t);
        next = QuantizeToPixelGrid(next);

        Rigidbody.linearVelocity = Vector2.zero;
        Rigidbody.MovePosition(next);

        if (t >= 1f || Vector2.Distance(next, bombReentryCenterTarget) <= PixelWorldStep * 0.5f)
        {
            bombReentryCenteringActive = false;
            Rigidbody.MovePosition(QuantizeToPixelGrid(bombReentryCenterTarget));
        }

        return true;
    }

    private Vector2 GetBombReentryCenterTarget(Vector2 bombPos, Vector2 dirForSize)
    {
        Vector2 dir = NormalizeCardinal(dirForSize);
        if (dir == Vector2.zero)
            dir = NormalizeCardinal(facingDirection);

        if (dir == Vector2.zero)
            dir = Vector2.down;

        Vector2 target = bombPos - dir * tileSize;

        target.x = Mathf.Round(target.x / tileSize) * tileSize;
        target.y = Mathf.Round(target.y / tileSize) * tileSize;

        return target;
    }

    public void PlayBattleRevengeSwapDeathSequence(Action onCompleted)
    {
        if (isEndingStage)
            return;

        battleRevengeSwapDeathPending = true;
        battleRevengeSwapDeathCompleted = onCompleted;
        deathRequestedByExplosion = true;

        if (!isDead)
            DeathSequence();
    }
}
