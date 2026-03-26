using Assets.Scripts.Interface;
using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(CharacterHealth))]
public class PinkLouieJumpAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "PinkLouieJump";

    [SerializeField] private bool enabledAbility = true;

    [Header("Jump")]
    public float jumpDurationSeconds = 1f;
    public int forwardCells = 2;
    public float jumpArcHeight = 1f;
    public float jumpCooldownSeconds = 0.25f;

    [Header("Invulnerability")]
    public bool invulnerableDuringJump = true;
    public float postLandingInvulnerableSeconds = 0f;

    [Header("SFX")]
    public AudioClip jumpSfx;
    [Range(0f, 1f)] public float jumpSfxVolume = 1f;

    [Header("Shadow")]
    public PinkLouieShadowController shadow;

    MovementController movement;
    Rigidbody2D rb;
    AudioSource audioSource;

    CharacterHealth playerHealth;
    PlayerMountCompanion companion;

    Coroutine routine;
    Coroutine visualRoutine;

    float nextAllowedTime;

    IPinkLouieJumpExternalAnimator externalAnimator;

    Transform jumpVisualRoot;
    Vector3 jumpVisualBaseLocalPosition;
    bool jumpVisualBaseCached;

    float mountedPlayerBaseLocalY;
    bool mountedPlayerBaseLocalYCached;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    bool deathCancelInProgress;

    void Awake()
    {
        movement = GetComponent<MovementController>();
        rb = movement != null ? movement.Rigidbody : null;
        audioSource = GetComponent<AudioSource>();

        playerHealth = GetComponent<CharacterHealth>();
        TryGetComponent(out companion);

        if (shadow == null)
            shadow = GetComponentInChildren<PinkLouieShadowController>(true);

        BindShadowToPinkLouie();
        CacheMountedPlayerBaseLocalY();
    }

    void OnDisable() => CancelJump();
    void OnDestroy() => CancelJump();

    public void SetExternalAnimator(IPinkLouieJumpExternalAnimator animator)
    {
        externalAnimator = animator;
    }

    public void SetJumpSfx(AudioClip clip, float volume)
    {
        jumpSfx = clip;
        jumpSfxVolume = Mathf.Clamp01(volume);
    }

    void Update()
    {
        if (!enabledAbility)
            return;

        if (!CompareTag("Player"))
            return;

        if (movement == null || movement.isDead)
            return;

        if (Time.time < nextAllowedTime)
            return;

        if (GamePauseController.IsPaused ||
            ClownMaskBoss.BossIntroRunning ||
            MechaBossSequence.MechaIntroRunning ||
            (StageIntroTransition.Instance != null &&
             (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning)))
            return;

        var input = PlayerInputManager.Instance;
        int pid = movement.PlayerId;
        if (input == null || !input.GetDown(pid, PlayerAction.ActionC))
            return;

        nextAllowedTime = Time.time + jumpCooldownSeconds;

        if (routine != null)
            return;

        routine = StartCoroutine(JumpRoutine());
    }

    IEnumerator JumpRoutine()
    {
        if (movement == null || rb == null)
        {
            routine = null;
            yield break;
        }

        bool wasMountedAtStart = movement.IsMounted;
        var mountedLouieHealth = wasMountedAtStart ? GetMountedLouieHealth() : null;

        CacheMountedPlayerBaseLocalY();

        Vector2 inputDir = movement.Direction != Vector2.zero ? movement.Direction : Vector2.zero;
        Vector2 faceDir = movement.FacingDirection != Vector2.zero ? movement.FacingDirection : Vector2.down;

        Vector2 dir = inputDir != Vector2.zero ? inputDir : Vector2.zero;
        if (dir != Vector2.zero)
        {
            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
                dir = new Vector2(Mathf.Sign(dir.x), 0f);
            else
                dir = new Vector2(0f, Mathf.Sign(dir.y));
        }

        var gm = FindFirstObjectByType<GameManager>();
        var destructible = gm != null ? gm.destructibleTilemap : null;
        var indestructible = gm != null ? gm.indestructibleTilemap : null;
        var ground = gm != null ? gm.groundTilemap : null;

        Vector3Int startCell = destructible != null
            ? destructible.WorldToCell(rb.position)
            : new Vector3Int(Mathf.RoundToInt(rb.position.x), Mathf.RoundToInt(rb.position.y), 0);

        Vector3Int targetCell = startCell;

        if (dir != Vector2.zero)
        {
            Vector3Int step = new Vector3Int(Mathf.RoundToInt(dir.x), Mathf.RoundToInt(dir.y), 0);
            int maxCells = Mathf.Max(1, forwardCells);

            Vector3Int candidateFar = startCell + (step * maxCells);

            if (IsLandingAllowed(candidateFar, destructible, indestructible, ground))
                targetCell = candidateFar;
            else
            {
                Vector3Int candidateNear = startCell + step;
                if (IsLandingAllowed(candidateNear, destructible, indestructible, ground))
                    targetCell = candidateNear;
            }
        }

        Vector3 startPos = CellCenter(startCell, destructible, indestructible, ground);
        Vector3 endPos = CellCenter(targetCell, destructible, indestructible, ground);

        movement.SetInputLocked(true, false);

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = startPos;

        if (shadow == null)
            shadow = GetComponentInChildren<PinkLouieShadowController>(true);

        BindShadowToPinkLouie();
        CacheJumpVisualRoot();

        shadow?.BeginJump((Vector2)startPos);

        if (invulnerableDuringJump)
            StartJumpInvulnerabilityOnly(mountedLouieHealth);

        if (audioSource != null && jumpSfx != null)
            audioSource.PlayOneShot(jumpSfx, jumpSfxVolume);

        StartJumpVisuals(dir != Vector2.zero ? dir : faceDir);

        float dur = Mathf.Max(0.01f, jumpDurationSeconds);
        float t = 0f;

        while (t < 1f)
        {
            if (!enabledAbility || movement == null || movement.isDead)
                break;

            t += Time.deltaTime / dur;

            float tt = Mathf.Clamp01(t);

            Vector3 projectedGroundPos3 = Vector3.Lerp(startPos, endPos, tt);
            Vector2 projectedGroundPos = projectedGroundPos3;

            shadow?.SetJumpGroundPosition(projectedGroundPos);

            Vector2 rbPos = startPos;

            if (dir.x != 0f)
                rbPos.x = projectedGroundPos.x;

            if (dir.y != 0f)
                rbPos.y = projectedGroundPos.y;

            rb.position = rbPos;

            float arc = Mathf.Sin(tt * Mathf.PI) * Mathf.Max(0f, jumpArcHeight);

            ApplyJumpVisualOffset(arc);
            ApplyMountedPlayerJumpArc(arc);

            if (wasMountedAtStart && !movement.IsMounted)
            {
                HandleLoseLouieMidJump(projectedGroundPos3, startCell, destructible, indestructible, ground);

                if (!deathCancelInProgress)
                {
                    ResetJumpVisualOffset();
                    ResetMountedPlayerJumpArc();
                    StopJumpVisuals();
                    shadow?.EndJump();

                    if (movement != null)
                        movement.SetInputLocked(false);
                }

                routine = null;
                deathCancelInProgress = false;
                yield break;
            }

            yield return null;
        }

        ResetJumpVisualOffset();
        ResetMountedPlayerJumpArc();
        rb.position = endPos;

        if (invulnerableDuringJump)
            ApplyPostLandingInvulnerability(mountedLouieHealth);

        if (!deathCancelInProgress)
        {
            StopJumpVisuals();
            shadow?.EndJump();

            if (movement != null)
                movement.SetInputLocked(false);
        }

        routine = null;
        deathCancelInProgress = false;
    }

    void BindShadowToPinkLouie()
    {
        if (shadow == null)
            return;

        var louieVisual = shadow.GetComponentInParent<MountVisualController>();
        var target = louieVisual != null ? louieVisual.transform : shadow.transform.parent;

        if (target != null)
        {
            jumpVisualRoot = target;
            shadow.BindToPinkLouieRoot(target);
        }
    }

    void CacheJumpVisualRoot()
    {
        if (jumpVisualRoot == null)
            BindShadowToPinkLouie();

        if (jumpVisualRoot == null)
            return;

        jumpVisualBaseLocalPosition = jumpVisualRoot.localPosition;
        jumpVisualBaseCached = true;
    }

    void CacheMountedPlayerBaseLocalY()
    {
        if (movement == null)
            return;

        mountedPlayerBaseLocalY = movement.pinkMountedSpritesLocalY;
        mountedPlayerBaseLocalYCached = true;
    }

    void ApplyJumpVisualOffset(float arcY)
    {
        if (jumpVisualRoot == null || !jumpVisualBaseCached)
            return;

        jumpVisualRoot.localPosition =
            jumpVisualBaseLocalPosition + new Vector3(0f, arcY, 0f);
    }

    void ResetJumpVisualOffset()
    {
        if (jumpVisualRoot == null || !jumpVisualBaseCached)
            return;

        jumpVisualRoot.localPosition = jumpVisualBaseLocalPosition;
    }

    void ApplyMountedPlayerJumpArc(float arcY)
    {
        if (movement == null || !mountedPlayerBaseLocalYCached)
            return;

        float desiredY = mountedPlayerBaseLocalY + arcY;

        if (movement.mountedSpriteUp != null)
            movement.mountedSpriteUp.SetRuntimeBaseLocalY(desiredY);

        if (movement.mountedSpriteDown != null)
            movement.mountedSpriteDown.SetRuntimeBaseLocalY(desiredY);

        if (movement.mountedSpriteLeft != null)
            movement.mountedSpriteLeft.SetRuntimeBaseLocalY(desiredY);

        if (movement.mountedSpriteRight != null)
            movement.mountedSpriteRight.SetRuntimeBaseLocalY(desiredY);
    }

    void ResetMountedPlayerJumpArc()
    {
        if (movement == null || !mountedPlayerBaseLocalYCached)
            return;

        float desiredY = mountedPlayerBaseLocalY;

        if (movement.mountedSpriteUp != null)
            movement.mountedSpriteUp.SetRuntimeBaseLocalY(desiredY);

        if (movement.mountedSpriteDown != null)
            movement.mountedSpriteDown.SetRuntimeBaseLocalY(desiredY);

        if (movement.mountedSpriteLeft != null)
            movement.mountedSpriteLeft.SetRuntimeBaseLocalY(desiredY);

        if (movement.mountedSpriteRight != null)
            movement.mountedSpriteRight.SetRuntimeBaseLocalY(desiredY);
    }

    void StartJumpInvulnerabilityOnly(CharacterHealth mountedLouieHealth)
    {
        float seconds = Mathf.Max(0.01f, jumpDurationSeconds);

        if (playerHealth != null)
            playerHealth.StartTemporaryInvulnerability(seconds, withBlink: false);

        if (mountedLouieHealth != null)
            mountedLouieHealth.StartTemporaryInvulnerability(seconds, withBlink: false);
    }

    void ApplyPostLandingInvulnerability(CharacterHealth mountedLouieHealth)
    {
        float post = Mathf.Max(0f, postLandingInvulnerableSeconds);

        if (post <= 0f)
        {
            if (playerHealth != null)
                playerHealth.StopInvulnerability();

            if (mountedLouieHealth != null)
                mountedLouieHealth.StopInvulnerability();

            return;
        }

        if (playerHealth != null)
            playerHealth.StartTemporaryInvulnerability(post, withBlink: false);

        if (mountedLouieHealth != null)
            mountedLouieHealth.StartTemporaryInvulnerability(post, withBlink: false);
    }

    CharacterHealth GetMountedLouieHealth()
    {
        if (companion == null)
            return null;

        var louieMove = companion.GetComponentInChildren<MountMovementController>(true);
        if (louieMove == null)
            return null;

        return louieMove.GetComponent<CharacterHealth>();
    }

    void HandleLoseLouieMidJump(Vector3 projectedGroundPos, Vector3Int startCell, Tilemap destructible, Tilemap indestructible, Tilemap ground)
    {
        if (rb == null)
            return;

        Vector3Int safeCell;

        if (ground != null)
            safeCell = ground.WorldToCell(projectedGroundPos);
        else if (destructible != null)
            safeCell = destructible.WorldToCell(projectedGroundPos);
        else if (indestructible != null)
            safeCell = indestructible.WorldToCell(projectedGroundPos);
        else
            safeCell = new Vector3Int(Mathf.RoundToInt(projectedGroundPos.x), Mathf.RoundToInt(projectedGroundPos.y), 0);

        if (!IsLandingAllowed(safeCell, destructible, indestructible, ground))
            safeCell = startCell;

        ResetJumpVisualOffset();
        ResetMountedPlayerJumpArc();
        rb.position = CellCenter(safeCell, destructible, indestructible, ground);
    }

    Vector3 CellCenter(Vector3Int cell, Tilemap destructible, Tilemap indestructible, Tilemap ground)
    {
        if (ground != null)
            return ground.GetCellCenterWorld(cell);

        if (destructible != null)
            return destructible.GetCellCenterWorld(cell);

        if (indestructible != null)
            return indestructible.GetCellCenterWorld(cell);

        return new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
    }

    bool IsLandingAllowed(Vector3Int cell, Tilemap destructible, Tilemap indestructible, Tilemap ground)
    {
        Vector3 world = CellCenter(cell, destructible, indestructible, ground);

        if (ground != null && ground.GetTile(cell) == null)
            return false;

        int obstacleMask = movement != null ? movement.obstacleMask.value : LayerMask.GetMask("Stage", "Bomb");
        Vector2 size = Vector2.one * 0.6f;

        var hits = Physics2D.OverlapBoxAll(world, size, 0f, obstacleMask);
        if (hits == null || hits.Length == 0)
            return true;

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null)
                continue;

            if (hit.gameObject == gameObject)
                continue;

            if (hit.isTrigger)
                continue;

            if (hit.gameObject.layer == LayerMask.NameToLayer("Bomb"))
            {
                var bomb = hit.GetComponent<Bomb>();
                var myBombController = GetComponent<BombController>();

                if (bomb != null && myBombController != null && bomb.Owner == myBombController)
                {
                    var bombCollider = bomb.GetComponent<Collider2D>();
                    if (bombCollider != null && bombCollider.isTrigger)
                        continue;
                }
            }

            return false;
        }

        return true;
    }

    void StartJumpVisuals(Vector2 dir)
    {
        externalAnimator?.Play(dir);

        if (visualRoutine != null)
            StopCoroutine(visualRoutine);

        visualRoutine = null;
    }

    void StopJumpVisuals()
    {
        if (visualRoutine != null)
        {
            StopCoroutine(visualRoutine);
            visualRoutine = null;
        }

        externalAnimator?.Stop();
        ResetJumpVisualOffset();
        ResetMountedPlayerJumpArc();
    }

    void CancelJump()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        deathCancelInProgress = false;

        ResetJumpVisualOffset();
        ResetMountedPlayerJumpArc();
        StopJumpVisuals();
        shadow?.EndJump();

        if (movement != null && !movement.isDead)
            movement.SetInputLocked(false);
    }

    public void Enable()
    {
        enabledAbility = true;
    }

    public void Disable()
    {
        enabledAbility = false;
        CancelJump();
    }

    public void CancelJumpForDeath()
    {
        deathCancelInProgress = true;
        CancelJump();
    }
}