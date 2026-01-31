using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MovementController))]
public sealed class PlayerRidingController : MonoBehaviour
{
    [Header("Riding Sprites (Directional)")]
    public AnimatedSpriteRenderer ridingUp;
    public AnimatedSpriteRenderer ridingDown;
    public AnimatedSpriteRenderer ridingLeft;
    public AnimatedSpriteRenderer ridingRight;

    [Header("Timing")]
    public float ridingSeconds = 5f;

    MovementController movement;
    BombController bomb;

    Coroutine routine;
    bool isPlaying;

    public bool IsPlaying => isPlaying;

    void Awake()
    {
        movement = GetComponent<MovementController>();
        bomb = GetComponent<BombController>();

        SetAnimEnabled(ridingUp, false);
        SetAnimEnabled(ridingDown, false);
        SetAnimEnabled(ridingLeft, false);
        SetAnimEnabled(ridingRight, false);
    }

    public bool TryPlayRiding(Vector2 facing, System.Action onComplete = null, System.Action onStart = null)
    {
        if (isPlaying || movement == null)
            return false;

        if (routine != null)
            StopCoroutine(routine);

        isPlaying = true;

        movement.SetInputLocked(true, forceIdle: true);

        movement.SetAllSpritesVisible(false);


        var r = PickRidingRenderer(facing);
        DisableAllRiding();

        if (r != null)
        {
            SetAnimEnabled(r, true);
            r.idle = false;
            r.loop = false;
            r.RefreshFrame();
        }

        onStart?.Invoke();

        routine = StartCoroutine(FinishRoutine(onComplete));
        return true;
    }

    IEnumerator FinishRoutine(System.Action onComplete)
    {
        yield return new WaitForSeconds(ridingSeconds);

        DisableAllRiding();

        onComplete?.Invoke();

        movement.EnableExclusiveFromState();

        movement.SetInputLocked(false, forceIdle: true);

        isPlaying = false;
        routine = null;
    }


    AnimatedSpriteRenderer PickRidingRenderer(Vector2 facing)
    {
        var f = facing;
        if (f == Vector2.zero)
            f = movement != null ? movement.FacingDirection : Vector2.down;

        if (f == Vector2.up) return ridingUp;
        if (f == Vector2.down) return ridingDown;
        if (f == Vector2.left) return ridingLeft;
        if (f == Vector2.right) return ridingRight;

        return ridingDown;
    }

    void DisableAllRiding()
    {
        SetAnimEnabled(ridingUp, false);
        SetAnimEnabled(ridingDown, false);
        SetAnimEnabled(ridingLeft, false);
        SetAnimEnabled(ridingRight, false);
    }

    static void SetAnimEnabled(AnimatedSpriteRenderer r, bool on)
    {
        if (r == null) return;

        r.enabled = on;

        if (r.TryGetComponent<SpriteRenderer>(out var sr))
            sr.enabled = on;
    }
}
