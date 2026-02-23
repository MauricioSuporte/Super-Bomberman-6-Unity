using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(BombController))]
[RequireComponent(typeof(MovementController))]
public sealed class PowerGloveAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "PowerGlove";

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

    private AnimatedSpriteRenderer activeCarryRenderer;
    private Vector2 activeCarryFace = Vector2.down;

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

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    private const int CarryOrderInLayer = 6;
    private const int GroundOrderInLayer = 3;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        bombController = GetComponent<BombController>();
        movement = GetComponent<MovementController>();
    }

    private void OnDisable()
    {
        ForceDropIfHolding();
        SetAllPickupSprites(false);
        SetAllCarrySprites(false);
        activeCarryRenderer = null;
        isHoldingBomb = false;
    }

    private void Update()
    {
        if (!enabledAbility) return;
        if (!CompareTag("Player")) return;
        if (GamePauseController.IsPaused) return;
        if (ClownMaskBoss.BossIntroRunning) return;

        if (movement == null || movement.isDead) return;

        if (StageIntroTransition.Instance != null &&
            (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning))
            return;

        if (movement.InputLocked) return;

        if (movement.Direction != Vector2.zero)
            lastFacingDir = movement.Direction;

        if (!holding)
        {
            TryPickupInput();
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

    private void TryPickupInput()
    {
        if (animLocking) return;
        if (movement == null) return;

        if (movement.IsMountedOnLouie) return;
        if (movement.IsRidingPlaying()) return;

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

        var bomb = hit.GetComponent<Bomb>();
        if (bomb == null) return;

        if (pickupRoutine != null) StopCoroutine(pickupRoutine);
        pickupRoutine = StartCoroutine(PickupRoutine(bomb, lastFacingDir));
    }

    private IEnumerator PickupRoutine(Bomb bomb, Vector2 dir)
    {
        animLocking = true;

        dir = NormalizeCardinalOrDown(dir);

        bool wasLocked = movement.InputLocked;

        movement.SetInputLocked(true, false);

        CacheBombRefs(bomb);

        PauseBombFuse(bomb, true);

        if (heldBombNotifier != null)
            heldBombNotifier.enabled = false;

        if (heldBombCollider != null)
            heldBombCollider.enabled = false;

        ForceBombIdle(true);

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
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / dur);

            if (heldBomb != null)
            {
                float y = Mathf.Lerp(0f, movement.tileSize, a);
                heldBomb.transform.position = new Vector3(p.x, p.y + y, z);
            }

            yield return null;
        }

        if (heldBomb != null)
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
            }

            bombController.useAIInput = true;
        }

        bool globalLock =
            GamePauseController.IsPaused ||
            MechaBossSequence.MechaIntroRunning ||
            ClownMaskBoss.BossIntroRunning ||
            (StageIntroTransition.Instance != null &&
             (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning));

        movement.SetInputLocked(globalLock || wasLocked, false);

        animLocking = false;
        pickupRoutine = null;

        var input = PlayerInputManager.Instance;
        if (input == null || !input.Get(movement.PlayerId, PlayerAction.ActionA))
            yield break;
    }

    private void BeginRelease(Vector2 dir)
    {
        if (!holding) return;
        if (animLocking) return;

        if (releaseRoutine != null) StopCoroutine(releaseRoutine);
        releaseRoutine = StartCoroutine(ReleaseRoutine(dir));
    }

    private IEnumerator ReleaseRoutine(Vector2 dir)
    {
        animLocking = true;

        dir = NormalizeCardinalOrDown(dir);

        bool wasLocked = movement.InputLocked;

        movement.SetInputLocked(true, false);

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

        SnapHeldBombToPlayerGround();

        DetachBombFromPlayerKeepWorld();

        SetBombSorting(GroundOrderInLayer);

        if (heldBombCollider != null)
            heldBombCollider.enabled = true;

        ForceBombIdle(true);

        ThrowHeldBomb(dir);

        float t = 0f;
        float dur = Mathf.Max(0.01f, releaseLockTime);

        while (t < dur)
        {
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

        bool globalLock =
            GamePauseController.IsPaused ||
            MechaBossSequence.MechaIntroRunning ||
            ClownMaskBoss.BossIntroRunning ||
            (StageIntroTransition.Instance != null &&
             (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning));

        movement.SetInputLocked(globalLock || wasLocked, false);

        animLocking = false;
        releaseRoutine = null;

        if (landWatchRoutine != null)
            StopCoroutine(landWatchRoutine);

        if (heldBomb != null)
            landWatchRoutine = StartCoroutine(WatchBombLandingThenResume(heldBomb));
        else
            RestoreBombControllerInputModeIfNeeded();
    }

    private void UpdateCarryVisual()
    {
        if (!holding) return;

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
            activeCarryFace = face;

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

        if (heldBomb != null)
        {
            var p = heldBomb.transform.localPosition;
            p.x = 0f;
            p.y = movement.tileSize;
            heldBomb.transform.localPosition = p;

            ForceBombIdle(true);
        }
    }

    private IEnumerator WatchBombLandingThenResume(Bomb bomb)
    {
        float timeout = 6f;
        float t = 0f;

        Rigidbody2D rb = bomb != null ? bomb.GetComponent<Rigidbody2D>() : null;

        while (t < timeout)
        {
            t += Time.deltaTime;

            if (bomb == null)
                break;

            Vector2 pos = rb != null ? rb.position : (Vector2)bomb.transform.position;
            Vector2 vel = rb != null ? rb.linearVelocity : Vector2.zero;

            bool slow = vel.sqrMagnitude <= 0.04f;
            bool aligned =
                Mathf.Abs(pos.x - Mathf.Round(pos.x / movement.tileSize) * movement.tileSize) <= 0.02f &&
                Mathf.Abs(pos.y - Mathf.Round(pos.y / movement.tileSize) * movement.tileSize) <= 0.02f;

            if (slow && aligned)
            {
                var col = bomb.GetComponent<Collider2D>();
                if (col != null)
                    col.enabled = true;

                var notifier = bomb.GetComponent<BombAtGroundTileNotifier>();
                if (notifier != null)
                    notifier.enabled = true;

                RestoreBombAnimation();
                PauseBombFuse(bomb, false);
                landWatchRoutine = null;
                RestoreBombControllerInputModeIfNeeded();
                yield break;
            }

            yield return null;
        }

        if (bomb != null)
        {
            var col = bomb.GetComponent<Collider2D>();
            if (col != null)
                col.enabled = true;

            var notifier = bomb.GetComponent<BombAtGroundTileNotifier>();
            if (notifier != null)
                notifier.enabled = true;

            RestoreBombAnimation();
            PauseBombFuse(bomb, false);
        }

        landWatchRoutine = null;
        RestoreBombControllerInputModeIfNeeded();
    }

    private void ThrowHeldBomb(Vector2 dir)
    {
        if (heldBomb == null)
            return;

        LayerMask obstacles = movement.obstacleMask | LayerMask.GetMask("Enemy", "Bomb", "Player");

        heldBomb.StartPunch(
            dir,
            movement.tileSize,
            throwDistanceTiles,
            obstacles,
            bombController != null ? bombController.destructibleTiles : null
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
        if (heldBomb == null)
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

    private void ForceBombIdle(bool keepIdle)
    {
        if (heldBombAnim == null)
            return;

        heldBombAnim.idle = true;
        heldBombAnim.loop = true;
        heldBombAnim.pingPong = false;
        heldBombAnim.RefreshFrame();
    }

    private void PauseBombFuse(Bomb bomb, bool pause)
    {
        if (bomb == null)
            return;

        bomb.SetFusePaused(pause);
    }

    private void RestoreBombControllerInputModeIfNeeded()
    {
        if (bombController == null)
            return;

        if (!bombControllerUseAIInputOverridden)
            return;

        bombController.useAIInput = prevBombControllerUseAIInput;
        bombControllerUseAIInputOverridden = false;
    }

    public void DestroyHeldBombIfHolding()
    {
        if (!holding && !isHoldingBomb)
            return;

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

        if (movement != null)
        {
            movement.SetExternalVisualSuppressed(false);

            if (!movement.isDead && !movement.IsEndingStage && !movement.IsRidingPlaying())
                movement.EnableExclusiveFromState();
        }

        RestoreBombControllerInputModeIfNeeded();
    }

    private void ForceDropIfHolding()
    {
        if (!holding)
        {
            RestoreBombControllerInputModeIfNeeded();
            isHoldingBomb = false;
            return;
        }

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

        if (heldBomb != null)
        {
            SnapHeldBombToPlayerGround();
            DetachBombFromPlayerKeepWorld();

            SetBombSorting(GroundOrderInLayer);

            if (heldBombCollider != null)
                heldBombCollider.enabled = true;

            if (heldBombNotifier != null)
                heldBombNotifier.enabled = true;

            RestoreBombAnimation();
            PauseBombFuse(heldBomb, false);
        }

        movement.SetExternalVisualSuppressed(false);
        movement.EnableExclusiveFromState();

        heldBomb = null;
        heldBombCollider = null;
        heldBombAnim = null;
        heldBombRb = null;
        heldBombNotifier = null;
        heldBombSpriteRenderer = null;

        RestoreBombControllerInputModeIfNeeded();
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

    public void Enable()
    {
        enabledAbility = true;
    }

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