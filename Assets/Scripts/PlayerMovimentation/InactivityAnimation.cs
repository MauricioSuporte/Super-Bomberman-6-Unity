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
    [SerializeField] private AnimatedSpriteRenderer emoteLoopRendererAlt;
    [SerializeField, Range(0f, 1f)] private float chanceAltAnimation = 0.3f;
    [SerializeField] private bool refreshFrameOnEnter = true;

    [Header("Alternating")]
    [SerializeField, Min(1f)] private float switchInterval = 15f;

    private float nextSwitchTime;
    private bool usingAlt;

    private AnimatedSpriteRenderer activeRenderer;

    private MovementController movement;
    private float lastInputTime;

    private bool isPlaying;
    private EmoteTarget currentTarget;

    private MountVisualController cachedLouieVisual;
    private float nextLouieResolveTime;

    private enum EmoteTarget
    {
        None = 0,
        Player = 1,
        Mount = 2
    }

    public float ChanceAltAnimation => Mathf.Clamp01(chanceAltAnimation);
    public bool RefreshFrameOnEnter => refreshFrameOnEnter;

    private void Awake()
    {
        movement = GetComponent<MovementController>();
        lastInputTime = Time.time;
        currentTarget = EmoteTarget.None;
        activeRenderer = null;
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

        if (movement.SuppressInactivityAnimation)
        {
            if (isPlaying)
                StopEmote();

            lastInputTime = Time.time;
            return;
        }

        if (movement.InputLocked || movement.isDead || GamePauseController.IsPaused)
        {
            if (isPlaying)
                StopEmote();

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

        if (isPlaying && Time.time >= nextSwitchTime)
        {
            SwitchEmote();
            nextSwitchTime = Time.time + switchInterval;
        }
    }

    private EmoteTarget ResolveDesiredTarget()
    {
        if (!movement.IsMounted)
            return EmoteTarget.Player;

        var lv = ResolveLouieVisual();
        if (lv != null && lv.HasInactivityEmoteRenderer)
            return EmoteTarget.Mount;

        return EmoteTarget.Player;
    }

    private MountVisualController ResolveLouieVisual()
    {
        if (!movement.IsMounted)
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

        var all = FindObjectsByType<MountVisualController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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

    private AnimatedSpriteRenderer ChooseRenderer(AnimatedSpriteRenderer primary, AnimatedSpriteRenderer alternative)
    {
        float chance = Mathf.Clamp01(chanceAltAnimation);

        if (alternative != null && Random.value <= chance)
            return alternative;

        return primary;
    }

    private void StartEmote(EmoteTarget target)
    {
        if (isPlaying)
            return;

        isPlaying = true;
        currentTarget = target;

        if (target == EmoteTarget.Mount)
        {
            movement.SetInactivityMountedDownOverride(true);

            var lv = ResolveLouieVisual();
            if (lv != null)
            {
                var chosenMountRenderer = ChooseRenderer(
                    lv.LouieInactivityEmoteLoop,
                    lv.LouieInactivityEmoteLoopAlt);

                lv.SetInactivityEmote(chosenMountRenderer, refreshFrameOnEnter);
            }

            SetPlayerEmoteEnabled(false);
            return;
        }

        movement.SetInactivityMountedDownOverride(false);
        movement.SetVisualOverrideActive(true);

        usingAlt = emoteLoopRendererAlt != null && Random.value <= chanceAltAnimation;
        activeRenderer = usingAlt ? emoteLoopRendererAlt : emoteLoopRenderer;

        nextSwitchTime = Time.time + switchInterval;

        if (activeRenderer != null)
        {
            activeRenderer.loop = true;
            activeRenderer.idle = false;
        }

        SetPlayerEmoteEnabled(true);

        if (refreshFrameOnEnter && activeRenderer != null)
            activeRenderer.RefreshFrame();
    }

    private void StopEmote()
    {
        if (!isPlaying && currentTarget == EmoteTarget.None)
        {
            SetPlayerEmoteEnabled(false);
            movement?.SetInactivityMountedDownOverride(false);
            movement?.SetVisualOverrideActive(false);
            activeRenderer = null;
            return;
        }

        if (currentTarget == EmoteTarget.Mount)
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
        activeRenderer = null;
    }

    private void SetPlayerEmoteEnabled(bool on)
    {
        if (emoteLoopRenderer != null)
            ToggleRenderer(emoteLoopRenderer, on && activeRenderer == emoteLoopRenderer);

        if (emoteLoopRendererAlt != null)
            ToggleRenderer(emoteLoopRendererAlt, on && activeRenderer == emoteLoopRendererAlt);
    }

    private void ToggleRenderer(AnimatedSpriteRenderer renderer, bool on)
    {
        renderer.enabled = on;

        if (renderer.TryGetComponent(out SpriteRenderer sr) && sr != null)
            sr.enabled = on;
    }

    private void SwitchEmote()
    {
        usingAlt = !usingAlt;

        if (currentTarget == EmoteTarget.Mount)
        {
            var lv = ResolveLouieVisual();
            if (lv != null)
            {
                var renderer = usingAlt
                    ? lv.LouieInactivityEmoteLoopAlt
                    : lv.LouieInactivityEmoteLoop;

                if (renderer != null)
                    lv.SetInactivityEmote(renderer, refreshFrameOnEnter);
            }

            return;
        }

        activeRenderer = usingAlt ? emoteLoopRendererAlt : emoteLoopRenderer;

        if (activeRenderer != null)
        {
            activeRenderer.loop = true;
            activeRenderer.idle = false;

            if (refreshFrameOnEnter)
                activeRenderer.RefreshFrame();
        }

        SetPlayerEmoteEnabled(true);
    }
}