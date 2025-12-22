using Assets.Scripts.Interface;
using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(AudioSource))]
public class PinkLouieJumpAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "PinkLouieJump";

    [SerializeField] private bool enabledAbility = true;

    [Header("Input")]
    public KeyCode triggerKey = KeyCode.B;

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

    MovementController movement;
    Rigidbody2D rb;
    AudioSource audioSource;

    CharacterHealth playerHealth;
    PlayerLouieCompanion companion;

    Coroutine routine;
    Coroutine visualRoutine;

    float nextAllowedTime;

    IPinkLouieJumpExternalAnimator externalAnimator;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    void Awake()
    {
        movement = GetComponent<MovementController>();
        rb = movement != null ? movement.Rigidbody : null;
        audioSource = GetComponent<AudioSource>();

        playerHealth = GetComponent<CharacterHealth>();
        TryGetComponent(out companion);
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

        if (!Input.GetKeyDown(triggerKey))
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

        bool wasMountedAtStart = movement.IsMountedOnLouie;
        var mountedLouieHealth = wasMountedAtStart ? GetMountedLouieHealth() : null;

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

        Vector3Int startCell = destructible != null
            ? destructible.WorldToCell(rb.position)
            : new Vector3Int(Mathf.RoundToInt(rb.position.x), Mathf.RoundToInt(rb.position.y), 0);

        Vector3Int targetCell = startCell;

        if (dir != Vector2.zero)
        {
            Vector3Int step = new Vector3Int(Mathf.RoundToInt(dir.x), Mathf.RoundToInt(dir.y), 0);
            int maxCells = Mathf.Max(1, forwardCells);

            Vector3Int candidateFar = startCell + (step * maxCells);

            if (IsLandingFree(candidateFar, destructible, indestructible))
            {
                targetCell = candidateFar;
            }
            else
            {
                Vector3Int candidateNear = startCell + step;

                if (IsLandingFree(candidateNear, destructible, indestructible))
                    targetCell = candidateNear;
            }
        }

        Vector3 startPos = CellCenter(startCell, destructible, indestructible);
        Vector3 endPos = CellCenter(targetCell, destructible, indestructible);

        movement.SetInputLocked(true, false);

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

            Vector3 projectedGroundPos = Vector3.Lerp(startPos, endPos, tt);

            Vector3 pos = projectedGroundPos;
            float arc = Mathf.Sin(tt * Mathf.PI) * Mathf.Max(0f, jumpArcHeight);
            pos += new Vector3(0f, arc, 0f);

            rb.position = pos;

            if (wasMountedAtStart && !movement.IsMountedOnLouie)
            {
                HandleLoseLouieMidJump(projectedGroundPos, startCell, destructible, indestructible);
                StopJumpVisuals();

                if (movement != null)
                    movement.SetInputLocked(false);

                routine = null;
                yield break;
            }

            yield return null;
        }

        rb.position = endPos;

        if (invulnerableDuringJump)
            ApplyPostLandingInvulnerability(mountedLouieHealth);

        StopJumpVisuals();

        if (movement != null)
            movement.SetInputLocked(false);

        routine = null;
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

        var louieMove = companion.GetComponentInChildren<LouieMovementController>(true);
        if (louieMove == null)
            return null;

        return louieMove.GetComponent<CharacterHealth>();
    }

    void HandleLoseLouieMidJump(Vector3 projectedGroundPos, Vector3Int startCell, Tilemap destructible, Tilemap indestructible)
    {
        if (rb == null)
            return;

        Vector3Int safeCell;

        if (destructible != null)
            safeCell = destructible.WorldToCell(projectedGroundPos);
        else if (indestructible != null)
            safeCell = indestructible.WorldToCell(projectedGroundPos);
        else
            safeCell = new Vector3Int(Mathf.RoundToInt(projectedGroundPos.x), Mathf.RoundToInt(projectedGroundPos.y), 0);

        if (!IsLandingFree(safeCell, destructible, indestructible))
            safeCell = startCell;

        rb.position = CellCenter(safeCell, destructible, indestructible);
    }

    Vector3 CellCenter(Vector3Int cell, Tilemap destructible, Tilemap indestructible)
    {
        if (destructible != null)
            return destructible.GetCellCenterWorld(cell);

        if (indestructible != null)
            return indestructible.GetCellCenterWorld(cell);

        return new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
    }

    bool IsLandingFree(Vector3Int cell, Tilemap destructible, Tilemap indestructible)
    {
        if (destructible != null && destructible.GetTile(cell) != null)
            return false;

        if (indestructible != null && indestructible.GetTile(cell) != null)
            return false;

        return true;
    }

    void StartJumpVisuals(Vector2 dir)
    {
        if (visualRoutine != null)
        {
            StopCoroutine(visualRoutine);
            visualRoutine = null;
        }

        externalAnimator?.Play(dir);
        visualRoutine = StartCoroutine(StopVisualsAfter(jumpDurationSeconds));
    }

    IEnumerator StopVisualsAfter(float seconds)
    {
        float end = Time.time + Mathf.Max(0.01f, seconds);
        while (Time.time < end)
        {
            if (!enabledAbility || movement == null || movement.isDead)
                break;

            yield return null;
        }

        StopJumpVisuals();
    }

    void StopJumpVisuals()
    {
        externalAnimator?.Stop();

        if (visualRoutine != null)
        {
            StopCoroutine(visualRoutine);
            visualRoutine = null;
        }
    }

    void CancelJump()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        StopJumpVisuals();

        if (movement != null)
            movement.SetInputLocked(false);
    }

    public void Enable() => enabledAbility = true;

    public void Disable()
    {
        enabledAbility = false;
        CancelJump();
    }
}
