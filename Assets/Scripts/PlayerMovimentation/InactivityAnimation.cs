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
    private bool manualTriggerHeld;
    private bool manualPoseActive;
    private PlayerAction? consumedDirection;
    private StunReceiver stunReceiver;
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
    public bool SuppressMovementInput => manualTriggerHeld;
    public bool KeepPrimaryLoopUntilInput { get; set; }

    public void CancelForExternalOverride()
    {
        externalPoseActive = false;
        manualTriggerHeld = false;
        StopEmote();
        lastInputTime = Time.time;
    }

    public void PlayBattleTimeUpPose(bool mounted)
    {
        externalPoseActive = false;
        manualTriggerHeld = false;
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
        stunReceiver = GetComponent<StunReceiver>();
        lastInputTime = Time.time;
        currentTarget = EmoteTarget.None;
        activeRenderer = null;
        SetPlayerEmoteEnabled(false);
    }

    private void OnEnable()
    {
        externalPoseActive = false;
        manualTriggerHeld = false;
        lastInputTime = Time.time;
        StopEmote();
    }

    private void OnDisable()
    {
        externalPoseActive = false;
        manualTriggerHeld = false;
        StopEmote();
    }

    private void Update()
    {
        using var performanceSample = BattleModePerformanceMarkers.InactivityAnimationUpdate.Auto();

        if (movement == null)
            return;

        if (GamePauseController.IsPaused)
            return;

        if (stunReceiver != null && stunReceiver.IsStunned)
        {
            if (isPlaying)
                CancelForExternalOverride();
            lastInputTime = Time.time;
            return;
        }

        if (externalPoseActive)
        {
            if (HasAnyPlayerInput())
                CancelForExternalOverride();
            return;
        }

        if (!movement.InputLocked && !movement.isDead &&
            movement.SuppressInactivityAnimation &&
            TryGetComponent<CorneredAnimation>(out var cornered) && cornered.IsPlaying &&
            TryStartManualEmote())
            return;

        if (movement.SuppressInactivityAnimation)
        {
            if (isPlaying)
                StopEmote();

            lastInputTime = Time.time;
            return;
        }

        if (movement.InputLocked || movement.isDead)
        {
            if (isPlaying)
                StopEmote();

            lastInputTime = Time.time;
            return;
        }

        if (TryStartManualEmote())
            return;

        if (consumedDirection.HasValue)
        {
            var input = PlayerInputManager.Instance;
            if (input == null || !input.Get(movement.PlayerId, consumedDirection.Value))
                consumedDirection = null;
        }
        manualTriggerHeld = consumedDirection.HasValue;

        if (HasAnyPlayerInput())
        {
            lastInputTime = Time.time;

            if (isPlaying)
                StopEmote();

            return;
        }

        if (manualPoseActive && movement.IsMounted != (currentTarget == EmoteTarget.Mount))
        {
            CancelForExternalOverride();
            return;
        }

        // Forced poses suspend automatic AFK selection and switching.
        if (manualPoseActive)
            return;

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

        bool keepPrimaryLoop = KeepPrimaryLoopUntilInput && currentTarget == EmoteTarget.Player && !usingAlt;
        if (isPlaying && !keepPrimaryLoop && Time.time >= nextSwitchTime)
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

        return HasPoseCancelInput(movement.PlayerId, consumedDirection);
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
        if (manualPoseActive && activeRenderer != null)
            ToggleRenderer(activeRenderer, false);
        manualPoseActive = false;
        manualTriggerHeld = false;
        consumedDirection = null;

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
            var lv = cachedLouieVisual != null ? cachedLouieVisual : ResolveLouieVisual();
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

    private bool TryStartManualEmote()
    {
        if (!CompareTag("Player"))
            return false;

        var input = PlayerInputManager.Instance;
        if (input == null || !input.Get(movement.PlayerId, PlayerAction.ActionL))
            return false;

        PlayerAction? pressed = null;
        for (int index = (int)PlayerAction.MoveUp; index <= (int)PlayerAction.MoveRight; index++)
        {
            var action = (PlayerAction)index;
            if (input.GetDown(movement.PlayerId, action))
            {
                pressed = action;
                break;
            }
        }
        if (!pressed.HasValue)
            return false;

        CancelForExternalOverride();
        if (TryGetComponent<CorneredAnimation>(out var cornered))
            cornered.CancelForExternalOverride();
        consumedDirection = pressed;
        manualTriggerHeld = true;
        lastInputTime = Time.time;

        if (pressed == PlayerAction.MoveUp)
        {
            StartEmote(ResolveDesiredTarget());
            return true;
        }

        manualPoseActive = true;
        isPlaying = true;
        currentTarget = movement.IsMounted ? EmoteTarget.Mount : EmoteTarget.Player;
        if (currentTarget == EmoteTarget.Mount)
        {
            movement.SetInactivityMountedDownOverride(true);
            var mount = ResolveLouieVisual();
            if (mount != null)
            {
                if (pressed == PlayerAction.MoveRight && mount.louieEndStage != null)
                {
                    mount.louieEndStage.CurrentFrame = 0;
                    if (movement.endStageFrameCount > 0)
                        mount.louieEndStage.animationTime = movement.endStageTotalTime / movement.endStageFrameCount;
                }
                var renderer = pressed == PlayerAction.MoveLeft
                    ? mount.LouieInactivityEmoteLoopAlt ?? mount.LouieInactivityEmoteLoop
                    : pressed == PlayerAction.MoveRight
                        ? mount.louieEndStage
                        : mount.louieCornered;
                mount.SetInactivityEmote(renderer, true);
            }
        }
        else
        {
            activeRenderer = pressed == PlayerAction.MoveLeft
                ? movement.spriteRendererTimeOver
                : pressed == PlayerAction.MoveDown
                    ? movement.spriteRendererCornered
                    : movement.spriteRendererEndStage;
            if (activeRenderer == null)
            {
                string rendererName = pressed == PlayerAction.MoveLeft ? "TimeOver"
                    : pressed == PlayerAction.MoveDown ? "Cornered" : "EndStage";
                foreach (var renderer in GetComponentsInChildren<AnimatedSpriteRenderer>(true))
                {
                    if (renderer.gameObject.name == rendererName)
                    {
                        activeRenderer = renderer;
                        break;
                    }
                }
            }
            movement.SetVisualOverrideActive(true);
            if (activeRenderer != null)
            {
                activeRenderer.loop = pressed != PlayerAction.MoveRight;
                if (pressed == PlayerAction.MoveRight)
                {
                    activeRenderer.pingPong = false;
                    if (movement.endStageFrameCount > 0)
                        activeRenderer.animationTime = movement.endStageTotalTime / movement.endStageFrameCount;
                }
                activeRenderer.idle = false;
                activeRenderer.CurrentFrame = 0;
                ToggleRenderer(activeRenderer, true);
                activeRenderer.RefreshFrame();
            }
        }
        return true;
    }

    public static bool HasPoseCancelInput(int playerId, PlayerAction? ignoredDirection = null)
    {
        var input = PlayerInputManager.Instance;
        if (input == null)
            return false;

        for (int index = (int)PlayerAction.MoveUp; index <= (int)PlayerAction.Select; index++)
        {
            var action = (PlayerAction)index;
            if (action != PlayerAction.Start && action != PlayerAction.ActionL &&
                action != ignoredDirection && input.Get(playerId, action))
                return true;
        }
        return false;
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
