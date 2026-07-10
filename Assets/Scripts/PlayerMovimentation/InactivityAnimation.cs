using UnityEngine;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(MovementController))]
public sealed class InactivityAnimation : MonoBehaviour
{
    [Header("Inactivity")]
    [SerializeField, Min(0.1f)] private float secondsToTrigger = 5f;
    [SerializeField, Min(0.02f)] private float idleCheckInterval = 0.1f;

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
    private bool externalPoseActive;
    private EmoteTarget currentTarget;

    private MountVisualController cachedLouieVisual;
    private float nextLouieResolveTime;
    private float nextIdleCheckTime;

    private enum EmoteTarget
    {
        None = 0,
        Player = 1,
        Mount = 2
    }

    public float ChanceAltAnimation => Mathf.Clamp01(chanceAltAnimation);
    public bool RefreshFrameOnEnter => refreshFrameOnEnter;

    public void CancelForExternalOverride()
    {
        externalPoseActive = false;
        StopEmote();
        lastInputTime = Time.time;
    }

    public void PlayBattleTimeUpPose(bool mounted)
    {
        externalPoseActive = false;
        StopEmote();
        externalPoseActive = true;

        isPlaying = true;
        usingAlt = false;
        activeRenderer = null;

        if (mounted)
        {
            currentTarget = EmoteTarget.Mount;
            movement.SetInactivityMountedDownOverride(true);

            MountVisualController louieVisual = ResolveLouieVisual();
            if (louieVisual != null)
            {
                AnimatedSpriteRenderer mountAfk2 =
                    louieVisual.LouieInactivityEmoteLoopAlt != null
                        ? louieVisual.LouieInactivityEmoteLoopAlt
                        : louieVisual.LouieInactivityEmoteLoop;

                louieVisual.SetInactivityEmote(mountAfk2, true);
            }

            SetPlayerEmoteEnabled(false);
            return;
        }

        currentTarget = EmoteTarget.Player;
        activeRenderer = emoteLoopRenderer != null
            ? emoteLoopRenderer
            : emoteLoopRendererAlt;

        movement.SetInactivityMountedDownOverride(false);
        movement.SetVisualOverrideActive(true);

        if (activeRenderer != null)
        {
            activeRenderer.loop = true;
            activeRenderer.idle = false;
            activeRenderer.pingPong = false;
            activeRenderer.CurrentFrame = 0;
            activeRenderer.RefreshFrame();
        }

        SetPlayerEmoteEnabled(true);
    }

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
        externalPoseActive = false;
        lastInputTime = Time.time;
        StopEmote();
    }

    private void OnDisable()
    {
        externalPoseActive = false;
        StopEmote();
    }

    private void Update()
    {
        using var performanceSample = BattleModePerformanceMarkers.InactivityAnimationUpdate.Auto();

        if (movement == null)
            return;

        if (externalPoseActive)
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

        float idleTime = Time.time - lastInputTime;

        if (!isPlaying && idleTime < secondsToTrigger)
            return;

        if (!isPlaying && Time.time < nextIdleCheckTime)
            return;

        nextIdleCheckTime = Time.time + idleCheckInterval;

        var desiredTarget = ResolveDesiredTarget();

        if (isPlaying && desiredTarget != currentTarget)
        {
            StopEmote();
            lastInputTime = Time.time;
            return;
        }

        if (!isPlaying && idleTime >= secondsToTrigger)
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

        var all = FindObjectsByType<MountVisualController>(FindObjectsInactive.Include);
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

        return input.HasAnyHeldInput(movement.PlayerId);
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
        HudPortraitStateNotifier.SetInactive(movement.PlayerId, true);

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
        bool wasPlaying = isPlaying;

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

        if (wasPlaying && movement != null)
            HudPortraitStateNotifier.SetInactive(movement.PlayerId, false);
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
