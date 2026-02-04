using UnityEngine;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(MovementController))]
public sealed class PlayerInactivityEmote : MonoBehaviour
{
    [Header("Inactivity")]
    [SerializeField, Min(0.1f)] private float secondsToTrigger = 5f;

    [Header("Emote Visual (loop)")]
    [SerializeField] private AnimatedSpriteRenderer emoteLoopRenderer;
    [SerializeField] private bool refreshFrameOnEnter = true;

    private MovementController movement;
    private float lastInputTime;
    private bool isPlaying;

    private void Awake()
    {
        movement = GetComponent<MovementController>();
        lastInputTime = Time.time;
        SetEmoteEnabled(false);
    }

    private void OnEnable()
    {
        lastInputTime = Time.time;
        SetEmoteEnabled(false);

        if (movement != null)
            movement.SetVisualOverrideActive(false);
    }

    private void OnDisable()
    {
        SetEmoteEnabled(false);

        if (movement != null)
            movement.SetVisualOverrideActive(false);
    }

    private void Update()
    {
        if (movement == null)
            return;

        if (movement.IsMountedOnLouie)
        {
            if (isPlaying) StopEmote();
            lastInputTime = Time.time;
            return;
        }

        if (movement.InputLocked || movement.isDead || GamePauseController.IsPaused)
        {
            if (isPlaying) StopEmote();
            lastInputTime = Time.time;
            return;
        }

        if (HasAnyPlayerInput())
        {
            lastInputTime = Time.time;

            if (isPlaying)
                StopEmote();

            return;
        }

        if (!isPlaying && (Time.time - lastInputTime) >= secondsToTrigger)
            StartEmote();
    }

    private bool HasAnyPlayerInput()
    {
        if (!CompareTag("Player"))
            return false;

        var input = PlayerInputManager.Instance;
        if (input == null)
            return false;

        int id = movement.PlayerId;

        return
            input.Get(id, PlayerAction.MoveUp) ||
            input.Get(id, PlayerAction.MoveDown) ||
            input.Get(id, PlayerAction.MoveLeft) ||
            input.Get(id, PlayerAction.MoveRight) ||
            input.Get(id, PlayerAction.Start) ||
            input.Get(id, PlayerAction.ActionA) ||
            input.Get(id, PlayerAction.ActionB) ||
            input.Get(id, PlayerAction.ActionC) ||
            input.Get(id, PlayerAction.ActionL) ||
            input.Get(id, PlayerAction.ActionR);
    }

    private void StartEmote()
    {
        if (isPlaying)
            return;

        isPlaying = true;

        movement.SetVisualOverrideActive(true);

        if (emoteLoopRenderer != null)
        {
            emoteLoopRenderer.loop = true;
            emoteLoopRenderer.idle = false;
        }

        SetEmoteEnabled(true);

        if (refreshFrameOnEnter && emoteLoopRenderer != null)
            emoteLoopRenderer.RefreshFrame();
    }

    private void StopEmote()
    {
        if (!isPlaying)
            return;

        isPlaying = false;

        SetEmoteEnabled(false);

        movement.SetVisualOverrideActive(false);
    }

    private void SetEmoteEnabled(bool on)
    {
        if (emoteLoopRenderer == null)
            return;

        emoteLoopRenderer.enabled = on;

        if (emoteLoopRenderer.TryGetComponent(out SpriteRenderer sr) && sr != null)
            sr.enabled = on;
    }
}
