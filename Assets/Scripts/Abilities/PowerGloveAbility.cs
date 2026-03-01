using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(BombController))]
[RequireComponent(typeof(MovementController))]
public sealed class PowerGloveAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "PowerGlove";

    private const string LOG = "[PowerGlove]";

    [SerializeField] private bool enabledAbility;

    [Header("State (read-only)")]
    [SerializeField] private bool isHoldingBomb;
    public bool IsHoldingBomb => isHoldingBomb;

    [Header("Timings")]
    [SerializeField, Min(0.01f)] private float pickupLockTime = 0.25f;
    [SerializeField, Min(0.01f)] private float releaseLockTime = 0.25f;

    [Header("Throw Settings")]
    [SerializeField, Min(1)] private int throwDistanceTiles = 3;

    [Header("Pickup Sprites (PLAYER)")]
    [SerializeField] private AnimatedSpriteRenderer pickupUp;
    [SerializeField] private AnimatedSpriteRenderer pickupDown;
    [SerializeField] private AnimatedSpriteRenderer pickupLeft;
    [SerializeField] private AnimatedSpriteRenderer pickupRight;

    [Header("Carry Walk Sprites (PLAYER)")]
    [SerializeField] private AnimatedSpriteRenderer carryUp;
    [SerializeField] private AnimatedSpriteRenderer carryDown;
    [SerializeField] private AnimatedSpriteRenderer carryLeft;
    [SerializeField] private AnimatedSpriteRenderer carryRight;

    [Header("Throw SFX (Resources/Sounds)")]
    [SerializeField, Range(0f, 1f)] private float throwSfxVolume = 1f;

    private static readonly string[] ThrowSfxPaths =
    {
        "Sounds/throw",
        "Sounds/throw2",
        "Sounds/throw3"
    };

    private AudioClip[] throwSfxClips;

    private AnimatedSpriteRenderer activeCarryRenderer;

    private AudioSource audioSource;
    private BombController bombController;
    private MovementController movement;

    private Vector2 lastFacingDir = Vector2.down;

    private bool holding;
    private bool animLocking;

    private Bomb heldBomb;
    private Collider2D heldBombCollider;
    private AnimatedSpriteRenderer heldBombAnim;
    private Rigidbody2D heldBombRb;
    private BombAtGroundTileNotifier heldBombNotifier;
    private SpriteRenderer heldBombSpriteRenderer;

    private Transform heldBombOriginalParent;
    private Vector3 heldBombOriginalLocalPos;
    private Quaternion heldBombOriginalLocalRot;
    private Vector3 heldBombOriginalLocalScale;

    private Coroutine pickupRoutine;
    private Coroutine releaseRoutine;
    private Coroutine landWatchRoutine;

    private bool prevBombControllerUseAIInput;
    private bool bombControllerUseAIInputOverridden;

    private bool prevMovementLocked;
    private bool movementLockCaptured;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    private const int CarryOrderInLayer = 6;
    private const int GroundOrderInLayer = 3;

    private void Log(string msg)
    {
        Debug.Log($"{LOG} {msg}", this);
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        bombController = GetComponent<BombController>();
        movement = GetComponent<MovementController>();
        LoadThrowSfxClipsIfNeeded();
    }

    private void OnDisable()
    {
        SetAllPickupSprites(false);
        SetAllCarrySprites(false);
        activeCarryRenderer = null;
        isHoldingBomb = false;

        holding = false;
        animLocking = false;

        movementLockCaptured = false;
        bombControllerUseAIInputOverridden = false;
    }

    private bool IsHeldBombValid()
        => heldBomb != null && !heldBomb.HasExploded;

    private void LateUpdate()
    {
        if (!enabledAbility) return;
        if (!CompareTag("Player")) return;
        if (movement == null) return;

        // FIX: se o baseline ficou capturado durante um lock externo, libera quando o lock externo acabou.
        if (movementLockCaptured && !IsGlobalLockActive() && !animLocking && !holding && !isHoldingBomb)
        {
            Log($"Clear stale movement baseline (lockCaptured=true, globalLock=false, holding=false). prevMovementLocked={prevMovementLocked} movement.InputLocked={movement.InputLocked}");
            movementLockCaptured = false;
        }

        if ((animLocking || holding || isHoldingBomb) && !IsHeldBombValid())
        {
            Log($"LateUpdate emergency reset: state says holding/locking but bomb invalid. holding={holding} isHoldingBomb={isHoldingBomb} animLocking={animLocking} heldBomb={(heldBomb ? heldBomb.name : "NULL")}");
            EmergencyUnlockAndReset("LateUpdate_InvalidBomb");
        }
    }

    private void Update()
    {
        if (!enabledAbility) return;
        if (!CompareTag("Player")) return;
        if (GamePauseController.IsPaused) return;
        if (movement == null || movement.isDead) return;

        if (StageIntroTransition.Instance != null &&
            (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning))
            return;

        if (IsExternalBlockingDismount())
        {
            ApplyExternalBlockAndCancel("Update_ExternalBlock");
            return;
        }

        if (movement.InputLocked) return;

        if (movement.Direction != Vector2.zero)
            lastFacingDir = movement.Direction;

        if (!holding)
        {
            TryPickupInput();
            return;
        }

        if (!IsHeldBombValid())
        {
            Log($"Update emergency reset: holding but bomb invalid. heldBomb={(heldBomb ? heldBomb.name : "NULL")}");
            EmergencyUnlockAndReset("Update_InvalidBomb");
            return;
        }

        var input = PlayerInputManager.Instance;
        if (input == null) return;

        int pid = movement.PlayerId;

        if (bombController != null)
        {
            if (!bombControllerUseAIInputOverridden)
            {
                prevBombControllerUseAIInput = bombController.useAIInput;
                bombControllerUseAIInputOverridden = true;
                Log($"Captured bombController.useAIInput baseline={prevBombControllerUseAIInput}");
            }

            bombController.useAIInput = true;
        }

        if (!input.Get(pid, PlayerAction.ActionA))
        {
            BeginRelease(lastFacingDir);
            return;
        }

        UpdateCarryVisual();
    }

    static bool IsExternalBlockingDismount()
    {
        if (MechaBossSequence.MechaIntroRunning) return true;
        if (BossIntroFlowBase.BossIntroRunning) return true;
        if (BossEscapeOnLastLife.AnyBossEscapeRunning) return true;

        var mechaIntro = StageMechaIntroController.Instance;
        if (mechaIntro != null && (mechaIntro.IntroRunning || mechaIntro.FlashRunning))
            return true;

        return false;
    }

    private void ApplyExternalBlockAndCancel(string reason)
    {
        if (movement == null)
            return;

        // FIX: NÃO capturar baseline aqui. Esse método roda em loop durante escape.
        // Se capturar baseline aqui, você “grava” InputLocked=true do escape e isso vaza pro próximo uso.
        movement.SetInputLocked(true, false);

        Log($"ApplyExternalBlockAndCancel reason={reason}. holding={holding} isHoldingBomb={isHoldingBomb} animLocking={animLocking} heldBomb={(heldBomb ? heldBomb.name : "NULL")} inputLocked={movement.InputLocked} extSupp={movement.ExternalVisualSuppressed}");

        if (animLocking || holding || isHoldingBomb || heldBomb != null)
            CancelPowerGloveAndDestroyHeldBomb("ExternalBlockCancel");

        // Garantia extra: não deixar sprite suprimido quando estamos só em lock externo
        if (!holding && !animLocking && movement.ExternalVisualSuppressed)
        {
            Log("ApplyExternalBlockAndCancel forcing externalVisualSuppressed=false (no holding/locking).");
            movement.SetExternalVisualSuppressed(false);
        }

        RestoreBombControllerInputModeIfNeeded("ExternalBlockCancel");
    }

    public void DestroyHeldBombIfHolding()
    {
        if (!holding && !isHoldingBomb)
            return;

        Log($"DestroyHeldBombIfHolding called. heldBomb={(heldBomb ? heldBomb.name : "NULL")}");

        holding = false;
        isHoldingBomb = false;
        animLocking = false;
        activeCarryRenderer = null;

        if (pickupRoutine != null) StopCoroutine(pickupRoutine);
        if (releaseRoutine != null) StopCoroutine(releaseRoutine);
        if (landWatchRoutine != null) StopCoroutine(landWatchRoutine);

        pickupRoutine = null;
        releaseRoutine = null;
        landWatchRoutine = null;

        // FIX: hard reset visual da luva (carry/pickup)
        SetAllPickupSprites(false);
        SetAllCarrySprites(false);

        if (movement != null)
        {
            movement.SetExternalVisualSuppressed(false);

            // garante que os sprites base do movement voltem a aparecer (caso tenham sido desligados durante pickup)
            SetMoveSprites(true);

            if (!movement.isDead && !movement.IsEndingStage && !movement.IsRidingPlaying())
                movement.EnableExclusiveFromState();
        }

        if (heldBomb != null)
        {
            var go = heldBomb.gameObject;

            heldBomb = null;
            heldBombCollider = null;
            heldBombAnim = null;
            heldBombRb = null;
            heldBombNotifier = null;
            heldBombSpriteRenderer = null;

            if (go != null)
                Destroy(go);
        }
        else
        {
            heldBomb = null;
            heldBombCollider = null;
            heldBombAnim = null;
            heldBombRb = null;
            heldBombNotifier = null;
            heldBombSpriteRenderer = null;
        }

        RestoreMovementLockToBaseline(IsGlobalLockActive(), "DestroyHeldBombIfHolding");
        RestoreBombControllerInputModeIfNeeded("DestroyHeldBombIfHolding");
    }

    private void EmergencyUnlockAndReset(string reason)
    {
        Log($"EmergencyUnlockAndReset reason={reason} holding={holding} isHoldingBomb={isHoldingBomb} animLocking={animLocking} heldBomb={(heldBomb ? heldBomb.name : "NULL")}");

        if (pickupRoutine != null) StopCoroutine(pickupRoutine);
        if (releaseRoutine != null) StopCoroutine(releaseRoutine);
        if (landWatchRoutine != null) StopCoroutine(landWatchRoutine);

        pickupRoutine = null;
        releaseRoutine = null;
        landWatchRoutine = null;

        holding = false;
        isHoldingBomb = false;
        animLocking = false;
        activeCarryRenderer = null;

        // FIX: hard reset visual da luva
        SetAllPickupSprites(false);
        SetAllCarrySprites(false);

        if (movement != null)
        {
            movement.SetExternalVisualSuppressed(false);
            SetMoveSprites(true);

            if (!movement.isDead && !movement.IsEndingStage && !movement.IsRidingPlaying())
                movement.EnableExclusiveFromState();
        }

        RestoreMovementLockToBaseline(IsGlobalLockActive(), $"Emergency:{reason}");

        heldBomb = null;
        heldBombCollider = null;
        heldBombAnim = null;
        heldBombRb = null;
        heldBombNotifier = null;
        heldBombSpriteRenderer = null;

        RestoreBombControllerInputModeIfNeeded($"Emergency:{reason}");
    }

    private void LoadThrowSfxClipsIfNeeded()
    {
        if (throwSfxClips != null && throwSfxClips.Length == ThrowSfxPaths.Length)
            return;

        throwSfxClips = new AudioClip[ThrowSfxPaths.Length];
        for (int i = 0; i < ThrowSfxPaths.Length; i++)
            throwSfxClips[i] = Resources.Load<AudioClip>(ThrowSfxPaths[i]);
    }

    private void PlayRandomThrowSfx()
    {
        if (audioSource == null)
            return;

        LoadThrowSfxClipsIfNeeded();

        if (throwSfxClips == null || throwSfxClips.Length == 0)
            return;

        int tries = throwSfxClips.Length;
        while (tries-- > 0)
        {
            int idx = Random.Range(0, throwSfxClips.Length);
            var clip = throwSfxClips[idx];
            if (clip != null)
            {
                audioSource.PlayOneShot(clip, Mathf.Clamp01(throwSfxVolume));
                return;
            }
        }
    }

    private void TryPickupInput()
    {
        if (animLocking) return;
        if (movement == null) return;

        if (movement.IsMountedOnLouie) return;
        if (movement.IsRidingPlaying()) return;

        if (IsExternalBlockingDismount())
        {
            ApplyExternalBlockAndCancel("TryPickupInput_ExternalBlock");
            return;
        }

        var input = PlayerInputManager.Instance;
        if (input == null) return;

        int pid = movement.PlayerId;

        if (!input.GetDown(pid, PlayerAction.ActionA))
            return;

        Vector2 origin = movement.Rigidbody != null ? movement.Rigidbody.position : (Vector2)transform.position;
        origin.x = Mathf.Round(origin.x / movement.tileSize) * movement.tileSize;
        origin.y = Mathf.Round(origin.y / movement.tileSize) * movement.tileSize;

        int bombLayer = LayerMask.NameToLayer("Bomb");
        int bombMask = 1 << bombLayer;

        Collider2D hit = Physics2D.OverlapBox(origin, Vector2.one * (movement.tileSize * 0.6f), 0f, bombMask);
        if (hit == null) return;

        if (hit.GetComponent<BoilerCapturedBomb>() != null) return;

        if (!hit.TryGetComponent<Bomb>(out var bomb)) return;
        if (bomb == null) return;
        if (bomb.HasExploded) return;

        if (bomb.GetComponent<BoilerCapturedBomb>() != null) return;

        Log($"Pickup input. Found bomb={bomb.name} exploded={bomb.HasExploded} origin={origin} lastFacing={lastFacingDir}");

        if (pickupRoutine != null) StopCoroutine(pickupRoutine);
        pickupRoutine = StartCoroutine(PickupRoutine(bomb, lastFacingDir));
    }

    private IEnumerator PickupRoutine(Bomb bomb, Vector2 dir)
    {
        animLocking = true;

        dir = NormalizeCardinalOrDown(dir);

        Log($"PickupRoutine START bomb={(bomb ? bomb.name : "NULL")} dir={dir} inputLocked(before)={(movement ? movement.InputLocked : false)} extSupp(before)={(movement ? movement.ExternalVisualSuppressed : false)}");

        CaptureMovementLockBaselineIfNeeded("PickupRoutine_Start");
        movement.SetInputLocked(true, false);

        CacheBombRefs(bomb);

        if (IsExternalBlockingDismount())
        {
            ApplyExternalBlockAndCancel("PickupRoutine_ExternalBlock");
            yield break;
        }

        if (!IsHeldBombValid())
        {
            EmergencyUnlockAndReset("PickupRoutine_InvalidBombEarly");
            yield break;
        }

        PauseBombFuse(bomb, true);

        if (heldBombNotifier != null)
            heldBombNotifier.enabled = false;

        if (heldBombCollider != null)
            heldBombCollider.enabled = false;

        movement.SetExternalVisualSuppressed(true);
        SetMoveSprites(false);
        SetAllCarrySprites(false);
        SetAllPickupSprites(false);

        var pick = GetPickupSprite(dir);
        if (pick != null)
        {
            pick.enabled = true;
            pick.idle = false;
            pick.loop = false;
            pick.pingPong = false;
            pick.CurrentFrame = 0;
            pick.RefreshFrame();
        }

        AttachBombToPlayerAtGround();

        if (!IsHeldBombValid())
        {
            if (pick != null) pick.enabled = false;
            EmergencyUnlockAndReset("PickupRoutine_BombInvalidAfterAttach");
            yield break;
        }

        if (dir == Vector2.down)
            SetBombSorting(CarryOrderInLayer);
        else
            SetBombSorting(GroundOrderInLayer);

        float dur = Mathf.Max(0.01f, pickupLockTime);
        float t = 0f;

        Vector3 p = transform.position;
        p.x = Mathf.Round(p.x / movement.tileSize) * movement.tileSize;
        p.y = Mathf.Round(p.y / movement.tileSize) * movement.tileSize;

        float z = heldBomb != null ? heldBomb.transform.position.z : 0f;

        while (t < dur)
        {
            if (IsExternalBlockingDismount())
            {
                if (pick != null) pick.enabled = false;
                ApplyExternalBlockAndCancel("PickupRoutine_ExternalBlock_Loop");
                yield break;
            }

            if (!IsHeldBombValid())
            {
                if (pick != null) pick.enabled = false;
                EmergencyUnlockAndReset("PickupRoutine_BombInvalid_Loop");
                yield break;
            }

            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / dur);

            float y = Mathf.Lerp(0f, movement.tileSize, a);
            heldBomb.transform.position = new Vector3(p.x, p.y + y, z);

            yield return null;
        }

        if (IsExternalBlockingDismount())
        {
            if (pick != null) pick.enabled = false;
            ApplyExternalBlockAndCancel("PickupRoutine_ExternalBlock_End");
            yield break;
        }

        if (!IsHeldBombValid())
        {
            if (pick != null) pick.enabled = false;
            EmergencyUnlockAndReset("PickupRoutine_BombInvalid_End");
            yield break;
        }

        heldBomb.transform.position = new Vector3(p.x, p.y + movement.tileSize, z);

        if (pick != null)
            pick.enabled = false;

        holding = true;
        isHoldingBomb = true;

        SetBombSorting(CarryOrderInLayer);

        if (bombController != null)
        {
            if (!bombControllerUseAIInputOverridden)
            {
                prevBombControllerUseAIInput = bombController.useAIInput;
                bombControllerUseAIInputOverridden = true;
                Log($"PickupRoutine captured bombController.useAIInput baseline={prevBombControllerUseAIInput}");
            }

            bombController.useAIInput = true;
        }

        RestoreMovementLockToBaseline(IsGlobalLockActive(), "PickupRoutine_End");

        animLocking = false;
        pickupRoutine = null;

        Log($"PickupRoutine END holding={holding} isHoldingBomb={isHoldingBomb} inputLocked(now)={(movement ? movement.InputLocked : false)} extSupp(now)={(movement ? movement.ExternalVisualSuppressed : false)} heldBomb={(heldBomb ? heldBomb.name : "NULL")}");

        var input = PlayerInputManager.Instance;
        if (input == null || !input.Get(movement.PlayerId, PlayerAction.ActionA))
            yield break;
    }

    private void BeginRelease(Vector2 dir)
    {
        if (!holding) return;
        if (animLocking) return;

        if (IsExternalBlockingDismount())
        {
            ApplyExternalBlockAndCancel("BeginRelease_ExternalBlock");
            return;
        }

        if (!IsHeldBombValid())
        {
            EmergencyUnlockAndReset("BeginRelease_InvalidBomb");
            return;
        }

        if (releaseRoutine != null) StopCoroutine(releaseRoutine);
        releaseRoutine = StartCoroutine(ReleaseRoutine(dir));
    }

    private IEnumerator ReleaseRoutine(Vector2 dir)
    {
        animLocking = true;

        dir = NormalizeCardinalOrDown(dir);

        Log($"ReleaseRoutine START dir={dir} inputLocked(before)={(movement ? movement.InputLocked : false)} extSupp(before)={(movement ? movement.ExternalVisualSuppressed : false)} heldBomb={(heldBomb ? heldBomb.name : "NULL")}");

        CaptureMovementLockBaselineIfNeeded("ReleaseRoutine_Start");
        movement.SetInputLocked(true, false);

        if (IsExternalBlockingDismount())
        {
            ApplyExternalBlockAndCancel("ReleaseRoutine_ExternalBlock");
            yield break;
        }

        SetAllPickupSprites(false);
        SetAllCarrySprites(false);

        var pick = GetPickupSprite(dir);
        if (pick != null)
        {
            pick.enabled = true;
            pick.idle = false;
            pick.loop = false;
            pick.pingPong = false;

            int frames = GetAnimFrames(pick);
            int last = Mathf.Max(0, frames - 1);
            pick.CurrentFrame = ClampFrame(pick, last);
            pick.RefreshFrame();
        }

        if (!IsHeldBombValid())
        {
            if (pick != null) pick.enabled = false;
            EmergencyUnlockAndReset("ReleaseRoutine_InvalidBombEarly");
            yield break;
        }

        SnapHeldBombToPlayerGround();
        DetachBombFromPlayerKeepWorld();

        SetBombSorting(GroundOrderInLayer);

        if (heldBombCollider != null)
            heldBombCollider.enabled = true;

        if (!IsHeldBombValid())
        {
            if (pick != null) pick.enabled = false;
            EmergencyUnlockAndReset("ReleaseRoutine_InvalidBombAfterDetach");
            yield break;
        }

        PlayRandomThrowSfx();
        ThrowHeldBomb(dir);

        float t = 0f;
        float dur = Mathf.Max(0.01f, releaseLockTime);

        while (t < dur)
        {
            if (IsExternalBlockingDismount())
            {
                if (pick != null) pick.enabled = false;
                ApplyExternalBlockAndCancel("ReleaseRoutine_ExternalBlock_Loop");
                yield break;
            }

            if (!IsHeldBombValid())
            {
                if (pick != null) pick.enabled = false;
                EmergencyUnlockAndReset("ReleaseRoutine_InvalidBomb_Loop");
                yield break;
            }

            t += Time.deltaTime;

            if (pick != null)
            {
                int frames = GetAnimFrames(pick);
                if (frames > 1)
                {
                    float a = Mathf.Clamp01(t / dur);
                    int frame = Mathf.RoundToInt(Mathf.Lerp(frames - 1, 0, a));
                    pick.CurrentFrame = ClampFrame(pick, frame);
                    pick.RefreshFrame();
                }
            }

            yield return null;
        }

        if (pick != null)
            pick.enabled = false;

        holding = false;
        isHoldingBomb = false;
        activeCarryRenderer = null;

        movement.SetExternalVisualSuppressed(false);
        movement.EnableExclusiveFromState();

        RestoreMovementLockToBaseline(IsGlobalLockActive(), "ReleaseRoutine_End");
        RestoreBombControllerInputModeIfNeeded("ReleaseRoutine_End");

        animLocking = false;
        releaseRoutine = null;

        Log($"ReleaseRoutine END holding={holding} isHoldingBomb={isHoldingBomb} inputLocked(now)={(movement ? movement.InputLocked : false)} extSupp(now)={(movement ? movement.ExternalVisualSuppressed : false)}");

        if (landWatchRoutine != null)
            StopCoroutine(landWatchRoutine);

        if (heldBomb != null && !heldBomb.HasExploded)
            landWatchRoutine = StartCoroutine(WatchBombLandingThenResume(heldBomb));
        else
            RestoreBombControllerInputModeIfNeeded("ReleaseRoutine_End_NoWatch");
    }

    private void UpdateCarryVisual()
    {
        if (!holding) return;

        if (IsExternalBlockingDismount())
        {
            ApplyExternalBlockAndCancel("UpdateCarryVisual_ExternalBlock");
            return;
        }

        if (!IsHeldBombValid())
        {
            EmergencyUnlockAndReset("UpdateCarryVisual_InvalidBomb");
            return;
        }

        Vector2 face = movement.Direction != Vector2.zero ? movement.Direction : movement.FacingDirection;
        face = NormalizeCardinalOrDown(face);

        bool isIdle = (movement.Direction == Vector2.zero);

        SetAllPickupSprites(false);

        var desired = GetCarrySprite(face);
        if (desired == null)
            return;

        if (activeCarryRenderer != desired)
        {
            if (activeCarryRenderer != null)
                activeCarryRenderer.enabled = false;

            SetAllCarrySprites(false);

            desired.enabled = true;
            desired.loop = true;
            desired.pingPong = false;

            activeCarryRenderer = desired;

            if (!desired.idle)
                desired.CurrentFrame = 0;

            desired.idle = isIdle;
            desired.RefreshFrame();
        }
        else
        {
            activeCarryRenderer.idle = isIdle;
            activeCarryRenderer.loop = true;
            activeCarryRenderer.pingPong = false;
            activeCarryRenderer.RefreshFrame();
        }

        if (IsHeldBombValid())
        {
            var p = heldBomb.transform.localPosition;
            p.x = 0f;
            p.y = movement.tileSize;
            heldBomb.transform.localPosition = p;
        }
    }

    private IEnumerator WatchBombLandingThenResume(Bomb bomb)
    {
        if (bomb == null || bomb.HasExploded)
        {
            landWatchRoutine = null;
            yield break;
        }

        float timeout = 6f;
        float t = 0f;

        Rigidbody2D rb = bomb.GetComponent<Rigidbody2D>();
        Collider2D col = bomb.GetComponent<Collider2D>();
        BombAtGroundTileNotifier notifier = bomb.GetComponent<BombAtGroundTileNotifier>();

        while (t < timeout)
        {
            if (IsExternalBlockingDismount())
            {
                ApplyExternalBlockAndCancel("WatchLanding_ExternalBlock");
                landWatchRoutine = null;
                yield break;
            }

            t += Time.deltaTime;

            if (bomb == null || bomb.HasExploded)
                break;

            Vector2 pos = rb != null ? rb.position : (Vector2)bomb.transform.position;
            Vector2 vel = rb != null ? rb.linearVelocity : Vector2.zero;

            bool slow = vel.sqrMagnitude <= 0.04f;
            bool aligned =
                Mathf.Abs(pos.x - Mathf.Round(pos.x / movement.tileSize) * movement.tileSize) <= 0.02f &&
                Mathf.Abs(pos.y - Mathf.Round(pos.y / movement.tileSize) * movement.tileSize) <= 0.02f;

            if (bomb.IsBeingPunched || bomb.IsBeingKicked)
            {
                yield return null;
                continue;
            }

            if (slow && aligned)
            {
                if (col != null)
                    col.enabled = true;

                if (notifier != null)
                    notifier.enabled = true;

                RestoreBombAnimation();

                if (bomb.IsFusePaused)
                    PauseBombFuse(bomb, false);

                landWatchRoutine = null;
                RestoreBombControllerInputModeIfNeeded("WatchLanding_Aligned");
                yield break;
            }

            yield return null;
        }

        if (bomb != null && !bomb.HasExploded)
        {
            if (col != null)
                col.enabled = true;

            if (notifier != null)
                notifier.enabled = true;

            RestoreBombAnimation();

            if (bomb.IsFusePaused)
                PauseBombFuse(bomb, false);
        }

        landWatchRoutine = null;
        RestoreBombControllerInputModeIfNeeded("WatchLanding_Timeout");
    }

    private void ThrowHeldBomb(Vector2 dir)
    {
        if (!IsHeldBombValid())
            return;

        LayerMask obstacles = movement.obstacleMask | LayerMask.GetMask("Enemy", "Bomb", "Player");

        Vector2 logicalOrigin = movement.Rigidbody != null ? movement.Rigidbody.position : (Vector2)transform.position;
        logicalOrigin.x = Mathf.Round(logicalOrigin.x / movement.tileSize) * movement.tileSize;
        logicalOrigin.y = Mathf.Round(logicalOrigin.y / movement.tileSize) * movement.tileSize;

        heldBomb.StartPunch(
            dir,
            movement.tileSize,
            throwDistanceTiles,
            obstacles,
            bombController != null ? bombController.destructibleTiles : null,
            visualStartYOffset: movement.tileSize,
            logicalOriginOverride: logicalOrigin
        );
    }

    private void CacheBombRefs(Bomb bomb)
    {
        heldBomb = bomb;
        heldBombCollider = bomb != null ? bomb.GetComponent<Collider2D>() : null;
        heldBombAnim = bomb != null ? bomb.GetComponentInChildren<AnimatedSpriteRenderer>(true) : null;
        heldBombRb = bomb != null ? bomb.GetComponent<Rigidbody2D>() : null;
        heldBombNotifier = bomb != null ? bomb.GetComponent<BombAtGroundTileNotifier>() : null;
        heldBombSpriteRenderer = bomb != null ? bomb.GetComponent<SpriteRenderer>() : null;
    }

    private void AttachBombToPlayerAtGround()
    {
        if (!IsHeldBombValid())
            return;

        heldBombOriginalParent = heldBomb.transform.parent;
        heldBombOriginalLocalPos = heldBomb.transform.localPosition;
        heldBombOriginalLocalRot = heldBomb.transform.localRotation;
        heldBombOriginalLocalScale = heldBomb.transform.localScale;

        heldBomb.transform.SetParent(transform, true);

        Vector3 p = transform.position;
        p.x = Mathf.Round(p.x / movement.tileSize) * movement.tileSize;
        p.y = Mathf.Round(p.y / movement.tileSize) * movement.tileSize;

        float z = heldBomb.transform.position.z;

        heldBomb.transform.position = new Vector3(p.x, p.y, z);
        heldBomb.transform.localRotation = Quaternion.identity;

        SetBombSorting(CarryOrderInLayer);
    }

    private void DetachBombFromPlayerKeepWorld()
    {
        if (heldBomb == null)
            return;

        Vector3 worldPos = heldBomb.transform.position;
        Quaternion worldRot = heldBomb.transform.rotation;

        heldBomb.transform.SetParent(heldBombOriginalParent, true);
        heldBomb.transform.SetPositionAndRotation(worldPos, worldRot);
        heldBomb.transform.localScale = heldBombOriginalLocalScale;
    }

    private void SnapHeldBombToPlayerGround()
    {
        if (heldBomb == null)
            return;

        Vector3 p = transform.position;
        p.x = Mathf.Round(p.x / movement.tileSize) * movement.tileSize;
        p.y = Mathf.Round(p.y / movement.tileSize) * movement.tileSize;

        heldBomb.transform.position = new Vector3(p.x, p.y, heldBomb.transform.position.z);
    }

    private void SetBombSorting(int orderInLayer)
    {
        if (heldBombSpriteRenderer != null)
            heldBombSpriteRenderer.sortingOrder = orderInLayer;
    }

    private void RestoreBombAnimation()
    {
        if (heldBombAnim == null)
            return;

        heldBombAnim.idle = false;
        heldBombAnim.loop = true;
        heldBombAnim.pingPong = false;
        heldBombAnim.SetFrozen(false);
        heldBombAnim.RefreshFrame();
    }

    private void PauseBombFuse(Bomb bomb, bool pause)
    {
        if (bomb == null)
            return;

        bomb.SetFusePaused(pause);
    }

    private void RestoreBombControllerInputModeIfNeeded(string reason)
    {
        if (bombController == null)
            return;

        if (!bombControllerUseAIInputOverridden)
            return;

        bombController.useAIInput = prevBombControllerUseAIInput;
        bombControllerUseAIInputOverridden = false;
        Log($"Restore bombController.useAIInput={prevBombControllerUseAIInput} reason={reason}");
    }

    private void CaptureMovementLockBaselineIfNeeded(string reason)
    {
        if (movementLockCaptured) return;
        if (movement == null) return;

        prevMovementLocked = movement.InputLocked;
        movementLockCaptured = true;
        Log($"CaptureMovementLockBaseline reason={reason} prevMovementLocked={prevMovementLocked}");
    }

    private void RestoreMovementLockToBaseline(bool globalLock, string reason)
    {
        if (movement == null)
        {
            movementLockCaptured = false;
            return;
        }

        if (!movementLockCaptured)
            return;

        if (globalLock)
        {
            movement.SetInputLocked(true, false);
            Log($"RestoreMovementLockToBaseline reason={reason} globalLock=TRUE -> set InputLocked=true");
            movementLockCaptured = false;
            return;
        }

        bool baseline = prevMovementLocked;
        movement.SetInputLocked(baseline, false);
        Log($"RestoreMovementLockToBaseline reason={reason} globalLock=FALSE -> set InputLocked={baseline}");

        movementLockCaptured = false;
    }

    private void CancelPowerGloveAndDestroyHeldBomb(string reason)
    {
        Log($"CancelPowerGloveAndDestroyHeldBomb reason={reason}");

        if (pickupRoutine != null) StopCoroutine(pickupRoutine);
        if (releaseRoutine != null) StopCoroutine(releaseRoutine);
        if (landWatchRoutine != null) StopCoroutine(landWatchRoutine);

        pickupRoutine = null;
        releaseRoutine = null;
        landWatchRoutine = null;

        holding = false;
        isHoldingBomb = false;
        animLocking = false;
        activeCarryRenderer = null;

        // FIX: hard reset visual da luva (carry/pickup)
        SetAllPickupSprites(false);
        SetAllCarrySprites(false);

        if (movement != null)
        {
            movement.SetExternalVisualSuppressed(false);

            // garante retorno dos sprites base (caso tenham sido desligados durante pickup)
            SetMoveSprites(true);

            if (!movement.isDead && !movement.IsEndingStage && !movement.IsRidingPlaying())
                movement.EnableExclusiveFromState();
        }

        if (heldBomb != null)
        {
            var go = heldBomb.gameObject;

            heldBomb = null;
            heldBombCollider = null;
            heldBombAnim = null;
            heldBombRb = null;
            heldBombNotifier = null;
            heldBombSpriteRenderer = null;

            if (go != null)
                Destroy(go);
        }
        else
        {
            heldBomb = null;
            heldBombCollider = null;
            heldBombAnim = null;
            heldBombRb = null;
            heldBombNotifier = null;
            heldBombSpriteRenderer = null;
        }

        RestoreMovementLockToBaseline(IsGlobalLockActive(), $"Cancel:{reason}");
        RestoreBombControllerInputModeIfNeeded($"Cancel:{reason}");
    }

    private void ForceDropIfHolding()
    {
        if (!holding)
        {
            RestoreBombControllerInputModeIfNeeded("ForceDrop_NotHolding");
            isHoldingBomb = false;

            RestoreMovementLockToBaseline(false, "ForceDrop_NotHolding");
            return;
        }

        Log($"ForceDropIfHolding. heldBomb={(heldBomb ? heldBomb.name : "NULL")}");

        holding = false;
        isHoldingBomb = false;
        activeCarryRenderer = null;
        animLocking = false;

        if (pickupRoutine != null) StopCoroutine(pickupRoutine);
        if (releaseRoutine != null) StopCoroutine(releaseRoutine);
        if (landWatchRoutine != null) StopCoroutine(landWatchRoutine);

        pickupRoutine = null;
        releaseRoutine = null;
        landWatchRoutine = null;

        if (heldBomb != null && !heldBomb.HasExploded)
        {
            DetachBombFromPlayerKeepWorld();

            SetBombSorting(GroundOrderInLayer);

            if (heldBombCollider != null)
                heldBombCollider.enabled = true;

            if (heldBombNotifier != null)
                heldBombNotifier.enabled = true;

            RestoreBombAnimation();
            PauseBombFuse(heldBomb, false);
        }

        if (movement != null)
        {
            movement.SetExternalVisualSuppressed(false);
            movement.EnableExclusiveFromState();
        }

        heldBomb = null;
        heldBombCollider = null;
        heldBombAnim = null;
        heldBombRb = null;
        heldBombNotifier = null;
        heldBombSpriteRenderer = null;

        RestoreMovementLockToBaseline(IsGlobalLockActive(), "ForceDropIfHolding");
        RestoreBombControllerInputModeIfNeeded("ForceDropIfHolding");
    }

    private bool IsGlobalLockActive()
    {
        if (GamePauseController.IsPaused) return true;
        if (ClownMaskBoss.BossIntroRunning) return true;
        if (IsExternalBlockingDismount()) return true;

        if (StageIntroTransition.Instance != null &&
            (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning))
            return true;

        return false;
    }

    private AnimatedSpriteRenderer GetPickupSprite(Vector2 dir)
    {
        if (dir == Vector2.up) return pickupUp;
        if (dir == Vector2.down) return pickupDown;
        if (dir == Vector2.left) return pickupLeft;
        if (dir == Vector2.right) return pickupRight;
        return pickupDown;
    }

    private AnimatedSpriteRenderer GetCarrySprite(Vector2 dir)
    {
        if (dir == Vector2.up) return carryUp;
        if (dir == Vector2.down) return carryDown;
        if (dir == Vector2.left) return carryLeft;
        if (dir == Vector2.right) return carryRight;
        return carryDown;
    }

    private void SetAllPickupSprites(bool enabled)
    {
        if (pickupUp != null) pickupUp.enabled = enabled;
        if (pickupDown != null) pickupDown.enabled = enabled;
        if (pickupLeft != null) pickupLeft.enabled = enabled;
        if (pickupRight != null) pickupRight.enabled = enabled;
    }

    private void SetAllCarrySprites(bool enabled)
    {
        if (carryUp != null) carryUp.enabled = enabled;
        if (carryDown != null) carryDown.enabled = enabled;
        if (carryLeft != null) carryLeft.enabled = enabled;
        if (carryRight != null) carryRight.enabled = enabled;
    }

    private void SetMoveSprites(bool enabled)
    {
        if (movement == null) return;

        if (movement.spriteRendererUp != null) movement.spriteRendererUp.enabled = enabled;
        if (movement.spriteRendererDown != null) movement.spriteRendererDown.enabled = enabled;
        if (movement.spriteRendererLeft != null) movement.spriteRendererLeft.enabled = enabled;
        if (movement.spriteRendererRight != null) movement.spriteRendererRight.enabled = enabled;
    }

    private static Vector2 NormalizeCardinalOrDown(Vector2 dir)
    {
        if (dir.sqrMagnitude <= 0.01f)
            return Vector2.down;

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return new Vector2(Mathf.Sign(dir.x), 0f);

        return new Vector2(0f, Mathf.Sign(dir.y));
    }

    private static int GetAnimFrames(AnimatedSpriteRenderer r)
    {
        if (r == null) return 0;
        return r.animationSprite != null ? r.animationSprite.Length : 0;
    }

    private static int ClampFrame(AnimatedSpriteRenderer r, int frame)
    {
        int n = GetAnimFrames(r);
        if (n <= 0) return 0;
        if (frame < 0) return 0;
        if (frame >= n) return n - 1;
        return frame;
    }

    public void TryDestroyHeldBombByHoleUsingBombController()
    {
        if (bombController == null) return;
        if (movement == null) return;
        if (heldBomb == null) return;

        var bombGo = heldBomb.gameObject;
        if (bombGo == null) return;

        Log($"TryDestroyHeldBombByHoleUsingBombController bomb={heldBomb.name} exploded={heldBomb.HasExploded}");

        if (heldBomb.HasExploded)
        {
            EmergencyUnlockAndReset("Hole_Destroy_AlreadyExploded");
            return;
        }

        if (pickupRoutine != null) StopCoroutine(pickupRoutine);
        if (releaseRoutine != null) StopCoroutine(releaseRoutine);
        if (landWatchRoutine != null) StopCoroutine(landWatchRoutine);

        pickupRoutine = null;
        releaseRoutine = null;
        landWatchRoutine = null;

        holding = false;
        isHoldingBomb = false;
        animLocking = false;
        activeCarryRenderer = null;

        SetAllPickupSprites(false);
        SetAllCarrySprites(false);

        movement.SetExternalVisualSuppressed(false);

        Vector2 ground = movement.Rigidbody != null ? movement.Rigidbody.position : (Vector2)transform.position;
        ground.x = Mathf.Round(ground.x / movement.tileSize) * movement.tileSize;
        ground.y = Mathf.Round(ground.y / movement.tileSize) * movement.tileSize;

        Vector2 visual = ground + Vector2.up * movement.tileSize;

        if (heldBombRb != null)
        {
            heldBombRb.linearVelocity = Vector2.zero;
            heldBombRb.angularVelocity = 0f;
        }

        if (heldBombNotifier != null)
            heldBombNotifier.enabled = false;

        if (heldBombCollider != null)
            heldBombCollider.enabled = false;

        if (heldBombSpriteRenderer != null)
            heldBombSpriteRenderer.sortingOrder = CarryOrderInLayer;

        DetachBombFromPlayerKeepWorld();
        bombGo.transform.position = new Vector3(visual.x, visual.y, bombGo.transform.position.z);

        heldBomb = null;
        heldBombCollider = null;
        heldBombAnim = null;
        heldBombRb = null;
        heldBombNotifier = null;
        heldBombSpriteRenderer = null;

        RestoreBombControllerInputModeIfNeeded("Hole_Destroy");
        RestoreMovementLockToBaseline(IsGlobalLockActive(), "Hole_Destroy");

        bombController.NotifyBombAtHoleWithVisualOffset(ground, bombGo, 1f, refund: true);
    }

    public void Enable() => enabledAbility = true;

    public void Disable()
    {
        enabledAbility = false;
        activeCarryRenderer = null;
        ForceDropIfHolding();
        SetAllPickupSprites(false);
        SetAllCarrySprites(false);
        isHoldingBomb = false;
    }
}