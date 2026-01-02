using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(BombController))]
[RequireComponent(typeof(MovementController))]
public class BombPunchAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "BombPunch";

    private const string PunchClipResourcesPath = "Sounds/PunchBomb";
    private static AudioClip cachedPunchClip;

    [SerializeField] private bool enabledAbility;

    [Header("Punch Settings")]
    public int punchDistanceTiles = 3;
    public float punchLockTime = 0.25f;

    [Header("Punch Sprites (PLAYER)")]
    public AnimatedSpriteRenderer punchUp;
    public AnimatedSpriteRenderer punchDown;
    public AnimatedSpriteRenderer punchLeft;
    public AnimatedSpriteRenderer punchRight;

    private AudioSource audioSource;
    private BombController bombController;
    private MovementController movement;

    private AnimatedSpriteRenderer prevMoveSprite;
    private AnimatedSpriteRenderer activePunchSprite;

    private Vector2 lastFacingDir = Vector2.down;
    private Coroutine punchLockRoutine;

    private IBombPunchExternalAnimator externalAnimator;

    private bool lockedByLouie;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        bombController = GetComponent<BombController>();
        movement = GetComponent<MovementController>();

        if (cachedPunchClip == null)
            cachedPunchClip = Resources.Load<AudioClip>(PunchClipResourcesPath);
    }

    public void SetExternalAnimator(IBombPunchExternalAnimator animator)
    {
        if (externalAnimator != null && externalAnimator != animator)
            externalAnimator.ForceStop();

        externalAnimator = animator;
    }

    public void SetLockedByLouie(bool locked)
    {
        if (lockedByLouie == locked)
            return;

        lockedByLouie = locked;

        if (lockedByLouie)
        {
            externalAnimator?.ForceStop();
            ForceResetPunchSprites();
        }
    }

    private void Update()
    {
        if (!enabledAbility)
            return;

        if (lockedByLouie)
            return;

        if (!CompareTag("Player"))
            return;

        if (GamePauseController.IsPaused)
            return;

        if (ClownMaskBoss.BossIntroRunning)
            return;

        if (movement == null || movement.isDead)
            return;

        if (StageIntroTransition.Instance != null &&
            (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning))
            return;

        if (movement.InputLocked)
            return;

        Vector2 moveDir = movement.Direction;
        if (moveDir != Vector2.zero)
            lastFacingDir = moveDir;

        if (!PlayerInputManager.Instance.GetDown(PlayerAction.ActionC))
            return;

        Vector2 dir = lastFacingDir;
        if (dir == Vector2.zero)
            dir = Vector2.down;

        if (audioSource != null && cachedPunchClip != null)
            audioSource.PlayOneShot(cachedPunchClip);

        if (punchLockRoutine != null)
            StopCoroutine(punchLockRoutine);

        punchLockRoutine = StartCoroutine(PunchAnimLock(dir));

        Vector2 origin = movement.Rigidbody != null ? movement.Rigidbody.position : (Vector2)transform.position;
        origin.x = Mathf.Round(origin.x / movement.tileSize) * movement.tileSize;
        origin.y = Mathf.Round(origin.y / movement.tileSize) * movement.tileSize;

        Vector2 front = origin + dir * movement.tileSize;

        int bombLayer = LayerMask.NameToLayer("Bomb");
        int bombMask = 1 << bombLayer;

        Collider2D hit = Physics2D.OverlapBox(front, Vector2.one * (movement.tileSize * 0.6f), 0f, bombMask);
        if (hit == null)
            return;

        var bomb = hit.GetComponent<Bomb>();
        if (bomb == null || !bomb.CanBePunched)
            return;

        LayerMask obstacles = movement.obstacleMask | LayerMask.GetMask("Enemy", "Bomb", "Player");

        bomb.StartPunch(
            dir,
            movement.tileSize,
            punchDistanceTiles,
            obstacles,
            bombController != null ? bombController.destructibleTiles : null
        );
    }

    private IEnumerator PunchAnimLock(Vector2 dir)
    {
        bool wasLocked = movement.InputLocked;

        movement.SetInputLocked(true, false);

        if (externalAnimator != null)
        {
            yield return externalAnimator.Play(dir, punchLockTime);

            bool globalLock =
                GamePauseController.IsPaused ||
                MechaBossSequence.MechaIntroRunning ||
                ClownMaskBoss.BossIntroRunning ||
                (StageIntroTransition.Instance != null &&
                 (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning));

            movement.SetInputLocked(globalLock || wasLocked, false);
            punchLockRoutine = null;
            yield break;
        }

        prevMoveSprite = GetMoveSprite(dir);
        activePunchSprite = GetPunchSprite(dir);

        SetMoveSprites(false);
        SetPunchSprites(false);

        if (activePunchSprite != null)
        {
            activePunchSprite.enabled = true;
            activePunchSprite.idle = false;
            activePunchSprite.loop = false;
            activePunchSprite.CurrentFrame = 0;
            activePunchSprite.RefreshFrame();
        }

        yield return new WaitForSeconds(punchLockTime);

        if (activePunchSprite != null)
            activePunchSprite.enabled = false;

        if (prevMoveSprite != null)
        {
            prevMoveSprite.enabled = true;
            prevMoveSprite.idle = true;
        }

        bool globalLock2 =
            GamePauseController.IsPaused ||
            MechaBossSequence.MechaIntroRunning ||
            ClownMaskBoss.BossIntroRunning ||
            (StageIntroTransition.Instance != null &&
             (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning));

        movement.SetInputLocked(globalLock2 || wasLocked, false);
        punchLockRoutine = null;
    }

    private AnimatedSpriteRenderer GetMoveSprite(Vector2 dir)
    {
        if (dir == Vector2.up) return movement.spriteRendererUp;
        if (dir == Vector2.down) return movement.spriteRendererDown;
        if (dir == Vector2.left) return movement.spriteRendererLeft;
        if (dir == Vector2.right) return movement.spriteRendererRight;

        if (movement.spriteRendererDown != null) return movement.spriteRendererDown;
        return null;
    }

    private AnimatedSpriteRenderer GetPunchSprite(Vector2 dir)
    {
        if (dir == Vector2.up) return punchUp;
        if (dir == Vector2.down) return punchDown;
        if (dir == Vector2.left) return punchLeft;
        if (dir == Vector2.right) return punchRight;

        return punchDown;
    }

    private void SetMoveSprites(bool enabled)
    {
        if (movement.spriteRendererUp != null) movement.spriteRendererUp.enabled = enabled;
        if (movement.spriteRendererDown != null) movement.spriteRendererDown.enabled = enabled;
        if (movement.spriteRendererLeft != null) movement.spriteRendererLeft.enabled = enabled;
        if (movement.spriteRendererRight != null) movement.spriteRendererRight.enabled = enabled;
    }

    private void SetPunchSprites(bool enabled)
    {
        if (punchUp != null) punchUp.enabled = enabled;
        if (punchDown != null) punchDown.enabled = enabled;
        if (punchLeft != null) punchLeft.enabled = enabled;
        if (punchRight != null) punchRight.enabled = enabled;
    }

    public void Enable()
    {
        enabledAbility = true;
    }

    public void Disable()
    {
        enabledAbility = false;

        SetPunchSprites(false);
        externalAnimator?.ForceStop();

        if (punchLockRoutine != null)
        {
            StopCoroutine(punchLockRoutine);
            punchLockRoutine = null;
        }
    }

    public void ForceResetPunchSprites()
    {
        if (punchLockRoutine != null)
        {
            StopCoroutine(punchLockRoutine);
            punchLockRoutine = null;
        }

        externalAnimator?.ForceStop();

        SetPunchSprites(false);

        if (movement == null)
            return;

        SetMoveSprites(false);

        var sprite = GetMoveSprite(lastFacingDir);
        if (sprite == null) sprite = movement.spriteRendererDown;

        if (sprite != null)
        {
            sprite.enabled = true;
            sprite.idle = true;
            sprite.loop = true;
            sprite.RefreshFrame();
        }
    }
}
