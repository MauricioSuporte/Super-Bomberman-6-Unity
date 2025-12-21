using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public class PurpleLouieBombLineAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "PurpleLouieBombLine";

    [SerializeField] private bool enabledAbility;

    [Header("Input (usa a mesma tecla do Punch)")]
    public KeyCode triggerKey = KeyCode.H;

    [Header("Behavior")]
    public float lockSeconds = 0.25f;

    MovementController movement;
    BombController bomb;

    Vector2 lastFacingDir = Vector2.down;
    Coroutine routine;

    IPurpleLouieBombLineExternalAnimator externalAnimator;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    void Awake()
    {
        movement = GetComponent<MovementController>();

        if (movement != null)
            bomb = movement.GetComponent<BombController>();
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

        // Mantém direção "encarada"
        Vector2 moveDir = movement.Direction;
        if (moveDir != Vector2.zero)
            lastFacingDir = moveDir;

        if (!Input.GetKeyDown(triggerKey))
            return;

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(DoCast());
    }

    IEnumerator DoCast()
    {
        bool wasLocked = movement.InputLocked;
        movement.SetInputLocked(true, false);

        Vector2 dir = lastFacingDir;
        if (dir == Vector2.zero)
            dir = Vector2.down;

        // animação no Louie (opcional)
        if (externalAnimator != null)
            yield return externalAnimator.Play(dir, lockSeconds);
        else
            yield return new WaitForSeconds(lockSeconds);

        // Solta todas as bombas restantes em linha
        DropBombsInFrontLine(dir);

        bool globalLock =
            GamePauseController.IsPaused ||
            MechaBossSequence.MechaIntroRunning ||
            ClownMaskBoss.BossIntroRunning ||
            (StageIntroTransition.Instance != null &&
             (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning));

        movement.SetInputLocked(globalLock || wasLocked, false);
        routine = null;
    }

    void DropBombsInFrontLine(Vector2 dir)
    {
        if (bomb == null || movement == null)
            return;

        int count = bomb.BombsRemaining;
        if (count <= 0)
            return;

        Vector2 origin = movement.Rigidbody != null ? movement.Rigidbody.position : (Vector2)transform.position;
        origin.x = Mathf.Round(origin.x / movement.tileSize) * movement.tileSize;
        origin.y = Mathf.Round(origin.y / movement.tileSize) * movement.tileSize;

        // começa no tile da frente
        Vector2 pos = origin + dir * movement.tileSize;

        for (int i = 0; i < count; i++)
        {
            // tenta colocar no tile atual
            bool placed = bomb.TryPlaceBombAt(pos);

            // Se não conseguiu colocar (obstáculo/bomba/tile destrutível/etc), para a linha aqui.
            if (!placed)
                break;

            // próximo tile na mesma direção
            pos += dir * movement.tileSize;

            // se acabou bombsRemaining, para
            if (bomb.BombsRemaining <= 0)
                break;
        }
    }

    public void Enable()
    {
        enabledAbility = true;
    }

    public void Disable()
    {
        enabledAbility = false;

        externalAnimator?.ForceStop();

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }
}
