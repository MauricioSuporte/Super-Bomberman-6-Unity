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
        if (input == null || !input.GetDown(pid, PlayerAction.ActionC))
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

        bool placedAny = DropBombsInFrontLine(dir);
        if (placedAny)
            PlayPlaceBombSfxOnce();

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
            bomb.playerAudioSource.PlayOneShot(bomb.placeBombSfx);
    }

    bool DropBombsInFrontLine(Vector2 dir)
    {
        if (bomb == null || movement == null)
            return false;

        int count = bomb.BombsRemaining;
        if (count <= 0)
            return false;

        Vector2 origin = movement.Rigidbody != null
            ? movement.Rigidbody.position
            : (Vector2)transform.position;

        origin.x = Mathf.Round(origin.x / movement.tileSize) * movement.tileSize;
        origin.y = Mathf.Round(origin.y / movement.tileSize) * movement.tileSize;

        // Começa 1 tile à frente, nunca no tile do player
        Vector2 pos = origin + dir * movement.tileSize;

        bool placedAny = false;

        for (int i = 0; i < count; i++)
        {
            if (HasIndestructibleAt(pos))
                break;

            if (HasEnemyAt(pos))
                break;

            if (!bomb.TryPlaceBombAtIgnoringInputLock(pos, out _, consumeBomb: true, playSfx: false))
                break;

            placedAny = true;

            pos += dir * movement.tileSize;

            if (bomb.BombsRemaining <= 0)
                break;
        }

        return placedAny;
    }

    public void Enable() => enabledAbility = true;

    public void Disable()
    {
        enabledAbility = false;
        externalAnimator?.ForceStop();

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (movement != null)
            movement.SetInputLocked(false);
    }

    private bool HasIndestructibleAt(Vector2 worldPos)
    {
        int mask = 1 << LayerMask.NameToLayer("Stage");
        return Physics2D.OverlapBox(worldPos, Vector2.one * 0.4f, 0f, mask) != null;
    }

    private bool HasEnemyAt(Vector2 worldPos)
    {
        int mask = 1 << LayerMask.NameToLayer("Enemy");
        return Physics2D.OverlapBox(worldPos, Vector2.one * 0.4f, 0f, mask) != null;
    }

    public void CancelCastForDeath()
    {
        deathCancelInProgress = true;

        enabledAbility = false;

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