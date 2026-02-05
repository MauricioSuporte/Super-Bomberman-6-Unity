using UnityEngine;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(MovementController))]
public sealed class InactivityAnimation : MonoBehaviour
{
    [Header("Inactivity")]
    [SerializeField, Min(0.1f)] private float secondsToTrigger = 5f;

    [Header("Emote Visual (loop)")]
    [SerializeField] private AnimatedSpriteRenderer emoteLoopRenderer;
    [SerializeField] private bool refreshFrameOnEnter = true;

    private MovementController movement;
    private float lastInputTime;

    private bool isPlaying;
    private EmoteTarget currentTarget;

    private LouieVisualController cachedLouieVisual;
    private float nextLouieResolveTime;

    private enum EmoteTarget
    {
        None = 0,
        Player = 1,
        Louie = 2
    }

    private void Awake()
    {
        movement = GetComponent<MovementController>();
        lastInputTime = Time.time;
        currentTarget = EmoteTarget.None;
        SetPlayerEmoteEnabled(false);
    }

    private void OnEnable()
    {
        lastInputTime = Time.time;
        StopEmote();
    }

    private void OnDisable()
    {
        StopEmote();
    }

    private void Update()
    {
        if (movement == null)
            return;

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

        var desiredTarget = ResolveDesiredTarget();

        if (isPlaying && desiredTarget != currentTarget)
        {
            StopEmote();
            lastInputTime = Time.time;
            return;
        }

        if (!isPlaying && (Time.time - lastInputTime) >= secondsToTrigger)
            StartEmote(desiredTarget);
    }

    private EmoteTarget ResolveDesiredTarget()
    {
        if (!movement.IsMountedOnLouie)
            return EmoteTarget.Player;

        var lv = ResolveLouieVisual();
        if (lv != null && lv.HasInactivityEmoteRenderer)
            return EmoteTarget.Louie;

        return EmoteTarget.Player;
    }

    private LouieVisualController ResolveLouieVisual()
    {
        if (!movement.IsMountedOnLouie)
        {
            cachedLouieVisual = null;
            nextLouieResolveTime = 0f;
            return null;
        }

        if (cachedLouieVisual != null && cachedLouieVisual.owner == movement)
            return cachedLouieVisual;

        if (Time.time < nextLouieResolveTime)
            return cachedLouieVisual;

        nextLouieResolveTime = Time.time + 0.25f;

        var all = FindObjectsByType<LouieVisualController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var v = all[i];
            if (v != null && v.owner == movement)
            {
                cachedLouieVisual = v;
                return cachedLouieVisual;
            }
        }

        cachedLouieVisual = null;
        return null;
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

    private void StartEmote(EmoteTarget target)
    {
        if (isPlaying)
            return;

        isPlaying = true;
        currentTarget = target;

        if (target == EmoteTarget.Louie)
        {
            movement.SetInactivityMountedDownOverride(true);

            var lv = ResolveLouieVisual();
            if (lv != null)
                lv.SetInactivityEmote(true);

            SetPlayerEmoteEnabled(false);
            return;
        }

        movement.SetInactivityMountedDownOverride(false);
        movement.SetVisualOverrideActive(true);

        if (emoteLoopRenderer != null)
        {
            emoteLoopRenderer.loop = true;
            emoteLoopRenderer.idle = false;
        }

        SetPlayerEmoteEnabled(true);

        if (refreshFrameOnEnter && emoteLoopRenderer != null)
            emoteLoopRenderer.RefreshFrame();
    }

    private void StopEmote()
    {
        if (!isPlaying && currentTarget == EmoteTarget.None)
        {
            SetPlayerEmoteEnabled(false);
            movement?.SetInactivityMountedDownOverride(false);
            movement?.SetVisualOverrideActive(false);
            return;
        }

        if (currentTarget == EmoteTarget.Louie)
        {
            var lv = ResolveLouieVisual();
            if (lv != null)
                lv.SetInactivityEmote(false);

            movement?.SetInactivityMountedDownOverride(false);
            SetPlayerEmoteEnabled(false);
        }
        else if (currentTarget == EmoteTarget.Player)
        {
            SetPlayerEmoteEnabled(false);
            movement?.SetVisualOverrideActive(false);
            movement?.SetInactivityMountedDownOverride(false);
        }
        else
        {
            SetPlayerEmoteEnabled(false);
            movement?.SetInactivityMountedDownOverride(false);
            movement?.SetVisualOverrideActive(false);
        }

        isPlaying = false;
        currentTarget = EmoteTarget.None;
    }

    private void SetPlayerEmoteEnabled(bool on)
    {
        if (emoteLoopRenderer == null)
            return;

        emoteLoopRenderer.enabled = on;

        if (emoteLoopRenderer.TryGetComponent(out SpriteRenderer sr) && sr != null)
            sr.enabled = on;
    }
}
