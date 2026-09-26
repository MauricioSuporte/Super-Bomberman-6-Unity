using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MovementController))]
public class PurpleLouieBombLineAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "PurpleLouieBombLine";

    [SerializeField] private bool enabledAbility;

    public float lockSeconds = 0.25f;

    MovementController movement;
    BombController bomb;
    Vector2 lastFacingDir = Vector2.down;
    Coroutine routine;

    IPurpleLouieBombLineExternalAnimator externalAnimator;

    bool deathCancelInProgress;

    bool lastLineWasControlBombs;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    void Awake()
    {
        movement = GetComponent<MovementController>();
        bomb = movement != null ? movement.GetComponent<BombController>() : null;
    }

    public void SetExternalAnimator(IPurpleLouieBombLineExternalAnimator animator)
    {
        externalAnimator = animator;
    }

    void Update()
    {
        if (!enabledAbility)
            return;

        if (!CompareTag("Player"))
            return;

        if (GamePauseController.IsPaused)
            return;

        if (ClownMaskBoss.BossIntroRunning)
            return;

        if (StageIntroTransition.Instance != null &&
            (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning))
            return;

        if (movement == null || movement.isDead || movement.InputLocked)
            return;

        Vector2 moveDir = movement.Direction;
        if (moveDir != Vector2.zero)
            lastFacingDir = moveDir;

        var input = PlayerInputManager.Instance;
        int pid = movement.PlayerId;
        if (input == null)
            return;

        if (lastLineWasControlBombs && input.GetDown(pid, PlayerAction.ActionB))
        {
            if (bomb == null)
                bomb = movement.GetComponent<BombController>();

            if (bomb != null)
            {
                bomb.TryExplodeAllControlledBombs();
                lastLineWasControlBombs = false;
            }
            return;
        }

        if (!input.GetDown(pid, PlayerAction.ActionC))
            return;

        if (bomb == null)
            bomb = movement.GetComponent<BombController>();

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(DoCast());
    }

    IEnumerator DoCast()
    {
        bool wasLocked = movement.InputLocked;
        movement.SetInputLocked(true, false);

        Vector2 dir = lastFacingDir == Vector2.zero ? Vector2.down : lastFacingDir;
        dir = ToCardinal(dir);

        bool isControlLine = bomb != null && IsControlEnabled();

        bool placedAny = DropBombsInFrontLine(dir);
        if (placedAny)
        {
            PlayPlaceBombSfxOnce();
            lastLineWasControlBombs = isControlLine;
        }

        if (externalAnimator != null)
            yield return externalAnimator.Play(dir, lockSeconds);
        else
            yield return new WaitForSeconds(lockSeconds);

        bool globalLock =
            GamePauseController.IsPaused ||
            MechaBossSequence.MechaIntroRunning ||
            ClownMaskBoss.BossIntroRunning ||
            (StageIntroTransition.Instance != null &&
             (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning));

        if (!deathCancelInProgress)
        {
            movement.SetInputLocked(globalLock || wasLocked, false);
        }

        routine = null;
        deathCancelInProgress = false;
    }

    bool IsControlEnabled()
    {
        if (TryGetComponent<AbilitySystem>(out var ab) && ab != null)
            return ab.IsEnabled(ControlBombAbility.AbilityId);

        return false;
    }

    private Vector2 ToCardinal(Vector2 v)
    {
        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y))
            return v.x >= 0f ? Vector2.right : Vector2.left;

        return v.y >= 0f ? Vector2.up : Vector2.down;
    }

    private void PlayPlaceBombSfxOnce()
    {
        if (bomb == null)
            return;

        if (bomb.playerAudioSource != null && bomb.placeBombSfx != null)
            GameAudioSettings.PlaySfx(bomb.playerAudioSource, bomb.placeBombSfx);
    }

    bool DropBombsInFrontLine(Vector2 dir)
    {
        return LineBombAbility.PlaceLine(movement, bomb, dir) > 0;
    }

    public void Enable() => enabledAbility = true;

    public void Disable()
    {
        enabledAbility = false;
        lastLineWasControlBombs = false;
        externalAnimator?.ForceStop();

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (movement != null)
            movement.SetInputLocked(false);
    }

    public void CancelCastForDeath()
    {
        deathCancelInProgress = true;
        enabledAbility = false;
        lastLineWasControlBombs = false;
        externalAnimator?.ForceStop();

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (movement != null)
            movement.SetInputLocked(false);
    }
}
