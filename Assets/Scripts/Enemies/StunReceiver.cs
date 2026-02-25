using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class StunReceiver : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool isStunned;

    [Header("Custom Stun Animation (AnimatedSpriteRenderer)")]
    [SerializeField] private bool useAnimatedStunRenderer = false;

    [SerializeField] private AnimatedSpriteRenderer stunAnimatedRenderer;
    [SerializeField] private bool disableShakeWhenUsingAnimatedStun = true;

    [Header("Override Controllers While Stunned (like InactivityAnimation)")]
    [SerializeField] private bool visualOverrideWhileStunned = true;
    [SerializeField] private bool disablePlayerControllersWhileStunned = true;
    [SerializeField] private bool onlyDisableIfTaggedPlayer = true;

    [Header("Shake (visual feedback)")]
    public bool shakeWhileStunned = true;
    public float shakeAmplitude = 0.03f;
    public float shakeFrequency = 22f;

    [Header("Freeze Animations During Stun")]
    public bool freezeAnimatedSprites = true;

    Rigidbody2D rb;

    Coroutine stunRoutine;
    float stunEndTime;

    bool suppressRestore;

    public bool IsStunned => isStunned;

    struct SpriteTBase
    {
        public Transform t;
        public Vector3 baseLocalPos;
    }

    struct AnimState
    {
        public AnimatedSpriteRenderer anim;
        public bool enabled;
        public bool idle;
        public bool loop;
        public float animationTime;
        public int frame;
        public bool frozen;
    }

    readonly List<SpriteTBase> spriteBases = new();
    readonly List<AnimState> animStates = new();

    private bool customStunActive;
    private bool savedVisualOverrideActive;
    private bool savedStunRendererEnabled;
    private bool savedStunRendererIdle;
    private bool savedStunRendererLoop;

    private bool stunVisualSuppressedByDamaged;
    private bool hadFrozenBeforeDamaged;
    private bool stunVisualInitialized;
    private bool stunWantsCustomAnim;

    private MovementController cachedMovement;
    private BombController cachedBombController;
    private PlayerManualDismount cachedManualDismount;

    private bool savedBombControllerEnabled;
    private bool savedManualDismountEnabled;

    private AIMovementController cachedAIMove;
    private EnemyMovementController cachedEnemyMove;

    Coroutine deferredVisualRestoreRoutine;
    int stunSessionToken;
    bool deferredRestoreWantsCustomAnim;

    bool customStunControllersRestored;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        cachedMovement = GetComponent<MovementController>();
        cachedBombController = GetComponent<BombController>();
        cachedManualDismount = GetComponent<PlayerManualDismount>();

        cachedAIMove = GetComponent<AIMovementController>();
        cachedEnemyMove = GetComponent<EnemyMovementController>();

        if (stunAnimatedRenderer == null)
            stunAnimatedRenderer = GetComponent<AnimatedSpriteRenderer>();
    }

    public void Stun(float seconds)
    {
        float dur = Mathf.Max(0.01f, seconds);
        float newEnd = Time.time + dur;

        suppressRestore = false;

        if (deferredVisualRestoreRoutine != null)
        {
            StopCoroutine(deferredVisualRestoreRoutine);
            deferredVisualRestoreRoutine = null;
        }

        if (!isStunned || newEnd > stunEndTime)
            stunEndTime = newEnd;

        if (stunRoutine == null)
            stunRoutine = StartCoroutine(StunRoutine());
    }

    public void CancelStun(bool restoreVisuals)
    {
        if (deferredVisualRestoreRoutine != null)
        {
            StopCoroutine(deferredVisualRestoreRoutine);
            deferredVisualRestoreRoutine = null;
        }

        stunSessionToken++;

        if (stunRoutine != null)
        {
            StopCoroutine(stunRoutine);
            stunRoutine = null;
        }

        isStunned = false;
        stunEndTime = 0f;

        suppressRestore = !restoreVisuals;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (restoreVisuals)
        {
            RestoreSpriteBases();
            if (freezeAnimatedSprites)
                RestoreAnimations();

            ExitCustomStunOverride();
        }
        else
        {
            spriteBases.Clear();
            animStates.Clear();
            customStunActive = false;
        }

        stunVisualSuppressedByDamaged = false;
        hadFrozenBeforeDamaged = false;
    }

    public void CancelStunForDeath()
    {
        if (deferredVisualRestoreRoutine != null)
        {
            StopCoroutine(deferredVisualRestoreRoutine);
            deferredVisualRestoreRoutine = null;
        }

        stunSessionToken++;

        if (stunRoutine != null)
        {
            StopCoroutine(stunRoutine);
            stunRoutine = null;
        }

        isStunned = false;
        stunEndTime = 0f;

        suppressRestore = true;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        UnfreezeAnimationsKeepCurrentFrame();

        spriteBases.Clear();
        animStates.Clear();

        customStunActive = false;
        stunVisualSuppressedByDamaged = false;
        hadFrozenBeforeDamaged = false;
    }

    IEnumerator StunRoutine()
    {
        isStunned = true;

        bool wantsCustomAnim = useAnimatedStunRenderer && (stunAnimatedRenderer != null);

        stunVisualSuppressedByDamaged = false;
        hadFrozenBeforeDamaged = false;

        stunVisualInitialized = false;
        stunWantsCustomAnim = wantsCustomAnim;

        bool damagedAtStart = IsDamagedVisualActiveNow();

        if (!damagedAtStart)
        {
            InitializeStunVisualIfNeeded(wantsCustomAnim);
        }
        else
        {
            stunVisualSuppressedByDamaged = true;

            if (freezeAnimatedSprites)
                UnfreezeAnimationsKeepCurrentFrame();
        }

        float seed = Random.value * 1000f;

        while (Time.time < stunEndTime)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            bool damagedActive = IsDamagedVisualActiveNow();

            if (damagedActive)
            {
                if (!stunVisualSuppressedByDamaged)
                {
                    RestoreSpriteBases();

                    if (wantsCustomAnim)
                    {
                        SuppressCustomStunVisual(true);
                    }
                    else
                    {
                        hadFrozenBeforeDamaged = freezeAnimatedSprites && animStates.Count > 0;
                        if (freezeAnimatedSprites)
                            RestoreAnimations();
                    }

                    stunVisualSuppressedByDamaged = true;
                }

                if (freezeAnimatedSprites)
                    UnfreezeAnimationsKeepCurrentFrame();

                if (suppressRestore || !isActiveAndEnabled || !gameObject.activeInHierarchy)
                    break;

                yield return null;
                continue;
            }

            if (!stunVisualInitialized)
            {
                InitializeStunVisualIfNeeded(wantsCustomAnim);
                stunVisualSuppressedByDamaged = false;
                hadFrozenBeforeDamaged = false;
            }

            if (stunVisualSuppressedByDamaged)
            {
                if (wantsCustomAnim)
                {
                    SuppressCustomStunVisual(false);
                }
                else
                {
                    if (freezeAnimatedSprites && hadFrozenBeforeDamaged)
                        FreezeAnimations();
                }

                stunVisualSuppressedByDamaged = false;
                hadFrozenBeforeDamaged = false;
            }

            bool doShake =
                shakeWhileStunned &&
                spriteBases.Count > 0 &&
                !(wantsCustomAnim && disableShakeWhenUsingAnimatedStun);

            if (doShake)
            {
                float amp = Mathf.Max(0f, shakeAmplitude);
                float hz = Mathf.Max(1f, shakeFrequency);

                float phase = (Time.time * hz) * (Mathf.PI * 2f);
                float x = Mathf.Sin(phase + seed) * amp;
                float y = Mathf.Cos(phase * 1.23f + seed) * amp;

                var offset = new Vector3(x, y, 0f);

                for (int i = 0; i < spriteBases.Count; i++)
                {
                    var sb = spriteBases[i];
                    if (sb.t != null)
                        sb.t.localPosition = sb.baseLocalPos + offset;
                }
            }

            if (suppressRestore || !isActiveAndEnabled || !gameObject.activeInHierarchy)
                break;

            yield return null;
        }

        isStunned = false;

        int myToken = ++stunSessionToken;
        bool damagedStillActive = IsDamagedVisualActiveNow();

        if (!suppressRestore && isActiveAndEnabled && gameObject.activeInHierarchy)
        {
            if (damagedStillActive)
            {
                if (wantsCustomAnim)
                    RestoreCustomStunControllersIfNeeded();

                deferredRestoreWantsCustomAnim = wantsCustomAnim;

                if (deferredVisualRestoreRoutine != null)
                    StopCoroutine(deferredVisualRestoreRoutine);

                deferredVisualRestoreRoutine = StartCoroutine(DeferredRestoreAfterDamaged(myToken));
            }
            else
            {
                if (wantsCustomAnim)
                {
                    ExitCustomStunOverride();
                }
                else
                {
                    RestoreSpriteBases();
                    if (freezeAnimatedSprites)
                        RestoreAnimations();
                }
            }
        }
        else
        {
            spriteBases.Clear();
            animStates.Clear();
            customStunActive = false;
        }

        stunVisualSuppressedByDamaged = false;
        hadFrozenBeforeDamaged = false;

        stunRoutine = null;
    }

    private IEnumerator DeferredRestoreAfterDamaged(int token)
    {
        while (IsDamagedVisualActiveNow())
        {
            if (token != stunSessionToken)
            {
                deferredVisualRestoreRoutine = null;
                yield break;
            }

            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                deferredVisualRestoreRoutine = null;
                yield break;
            }

            yield return null;
        }

        if (token != stunSessionToken)
        {
            deferredVisualRestoreRoutine = null;
            yield break;
        }

        if (isStunned)
        {
            deferredVisualRestoreRoutine = null;
            yield break;
        }

        if (suppressRestore || !isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            spriteBases.Clear();
            animStates.Clear();
            customStunActive = false;
            deferredVisualRestoreRoutine = null;
            yield break;
        }

        if (deferredRestoreWantsCustomAnim)
        {
            ExitCustomStunOverrideVisualOnly();
        }
        else
        {
            RestoreSpriteBases();
            if (freezeAnimatedSprites)
                RestoreAnimations();
        }

        deferredVisualRestoreRoutine = null;
    }

    private bool IsDamagedVisualActiveNow()
    {
        if (cachedAIMove != null && cachedAIMove.IsDamagedVisualActive)
            return true;

        if (cachedEnemyMove != null && cachedEnemyMove.IsDamagedVisualActive)
            return true;

        return false;
    }

    void CaptureSpriteBases()
    {
        spriteBases.Clear();

        var srs = GetComponentsInChildren<SpriteRenderer>(true);
        if (srs == null || srs.Length == 0)
            return;

        var seen = new HashSet<Transform>();

        for (int i = 0; i < srs.Length; i++)
        {
            var sr = srs[i];
            if (sr == null)
                continue;

            var t = sr.transform;
            if (t == null)
                continue;

            if (!seen.Add(t))
                continue;

            spriteBases.Add(new SpriteTBase
            {
                t = t,
                baseLocalPos = t.localPosition
            });
        }
    }

    void RestoreSpriteBases()
    {
        for (int i = 0; i < spriteBases.Count; i++)
        {
            var sb = spriteBases[i];
            if (sb.t != null)
                sb.t.localPosition = sb.baseLocalPos;
        }
    }

    void ForceIdleAndApplyOffsets()
    {
        var anims = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        if (anims == null || anims.Length == 0)
            return;

        for (int i = 0; i < anims.Length; i++)
        {
            var a = anims[i];
            if (a == null) continue;

            a.idle = true;
            a.RefreshFrame();
        }
    }

    void FreezeAnimations()
    {
        animStates.Clear();

        var anims = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        if (anims == null || anims.Length == 0)
            return;

        for (int i = 0; i < anims.Length; i++)
        {
            var a = anims[i];
            if (a == null)
                continue;

            animStates.Add(new AnimState
            {
                anim = a,
                enabled = a.enabled,
                idle = a.idle,
                loop = a.loop,
                animationTime = a.animationTime,
                frame = a.CurrentFrame,
                frozen = false
            });

            a.SetFrozen(true);

            a.idle = true;
            a.loop = false;
            a.CurrentFrame = a.CurrentFrame;
            a.RefreshFrame();
        }
    }

    void RestoreAnimations()
    {
        for (int i = 0; i < animStates.Count; i++)
        {
            var st = animStates[i];
            if (st.anim == null)
                continue;

            st.anim.SetFrozen(false);

            st.anim.enabled = st.enabled;
            st.anim.idle = st.idle;
            st.anim.loop = st.loop;
            st.anim.animationTime = st.animationTime;
            st.anim.CurrentFrame = st.frame;

            st.anim.RefreshFrame();

            if (freezeAnimatedSprites && st.anim.isActiveAndEnabled && st.anim.idle)
            {
                st.anim.idle = false;
                st.anim.loop = true;

                int len = (st.anim.animationSprite != null) ? st.anim.animationSprite.Length : 0;
                if (len > 1)
                    st.anim.CurrentFrame = (st.anim.CurrentFrame + 1) % len;

                st.anim.RefreshFrame();
            }
        }

        animStates.Clear();
    }

    void UnfreezeAnimationsKeepCurrentFrame()
    {
        var anims = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        if (anims == null || anims.Length == 0)
            return;

        for (int i = 0; i < anims.Length; i++)
        {
            var a = anims[i];
            if (a == null) continue;

            a.SetFrozen(false);
        }
    }

    private bool ShouldAffectControllers()
    {
        if (!disablePlayerControllersWhileStunned)
            return false;

        if (!onlyDisableIfTaggedPlayer)
            return true;

        return CompareTag("Player");
    }

    private void SetAnimEnabled(AnimatedSpriteRenderer r, bool on)
    {
        if (r == null) return;

        r.enabled = on;

        if (r.TryGetComponent(out SpriteRenderer sr) && sr != null)
            sr.enabled = on;
    }

    private void RestoreCustomStunControllersIfNeeded()
    {
        if (!customStunActive)
            return;

        if (customStunControllersRestored)
            return;

        if (ShouldAffectControllers())
        {
            if (cachedBombController != null)
                cachedBombController.enabled = savedBombControllerEnabled;

            if (cachedManualDismount != null)
                cachedManualDismount.enabled = savedManualDismountEnabled;
        }

        customStunControllersRestored = true;
    }

    private void EnterCustomStunOverride()
    {
        if (customStunActive)
            return;

        if (stunAnimatedRenderer == null)
            stunAnimatedRenderer = GetComponent<AnimatedSpriteRenderer>();

        if (stunAnimatedRenderer != null)
        {
            savedStunRendererEnabled = stunAnimatedRenderer.enabled;
            savedStunRendererIdle = stunAnimatedRenderer.idle;
            savedStunRendererLoop = stunAnimatedRenderer.loop;
        }

        if (visualOverrideWhileStunned && cachedMovement != null)
        {
            savedVisualOverrideActive = cachedMovement.VisualOverrideActive;
            cachedMovement.SetVisualOverrideActive(true);
        }
        else
        {
            savedVisualOverrideActive = false;
        }

        if (stunAnimatedRenderer != null)
        {
            stunAnimatedRenderer.loop = true;
            stunAnimatedRenderer.idle = false;

            SetAnimEnabled(stunAnimatedRenderer, true);
            stunAnimatedRenderer.RefreshFrame();
        }

        if (ShouldAffectControllers())
        {
            if (cachedBombController != null)
            {
                savedBombControllerEnabled = cachedBombController.enabled;
                cachedBombController.enabled = false;
            }

            if (cachedManualDismount != null)
            {
                savedManualDismountEnabled = cachedManualDismount.enabled;
                cachedManualDismount.enabled = false;
            }
        }

        spriteBases.Clear();
        animStates.Clear();

        customStunControllersRestored = false;
        customStunActive = true;
    }

    private void SuppressCustomStunVisual(bool suppress)
    {
        if (!customStunActive)
            return;

        if (stunAnimatedRenderer == null)
            return;

        if (suppress)
        {
            SetAnimEnabled(stunAnimatedRenderer, false);
            return;
        }

        stunAnimatedRenderer.loop = true;
        stunAnimatedRenderer.idle = false;
        SetAnimEnabled(stunAnimatedRenderer, true);
        stunAnimatedRenderer.RefreshFrame();
    }

    private void ExitCustomStunOverrideVisualOnly()
    {
        if (!customStunActive)
            return;

        if (stunAnimatedRenderer != null)
        {
            stunAnimatedRenderer.idle = savedStunRendererIdle;
            stunAnimatedRenderer.loop = savedStunRendererLoop;
            SetAnimEnabled(stunAnimatedRenderer, savedStunRendererEnabled);
            stunAnimatedRenderer.RefreshFrame();
        }

        if (visualOverrideWhileStunned && cachedMovement != null)
        {
            cachedMovement.SetVisualOverrideActive(savedVisualOverrideActive);
        }

        customStunActive = false;
    }

    private void ExitCustomStunOverride()
    {
        if (!customStunActive)
            return;

        RestoreCustomStunControllersIfNeeded();

        if (stunAnimatedRenderer != null)
        {
            stunAnimatedRenderer.idle = savedStunRendererIdle;
            stunAnimatedRenderer.loop = savedStunRendererLoop;
            SetAnimEnabled(stunAnimatedRenderer, savedStunRendererEnabled);
            stunAnimatedRenderer.RefreshFrame();
        }

        if (visualOverrideWhileStunned && cachedMovement != null)
        {
            cachedMovement.SetVisualOverrideActive(savedVisualOverrideActive);
        }

        customStunActive = false;
    }

    void OnDisable()
    {
        if (deferredVisualRestoreRoutine != null)
        {
            StopCoroutine(deferredVisualRestoreRoutine);
            deferredVisualRestoreRoutine = null;
        }

        stunSessionToken++;

        if (stunRoutine != null)
        {
            StopCoroutine(stunRoutine);
            stunRoutine = null;
        }

        isStunned = false;
        stunEndTime = 0f;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        RestoreSpriteBases();
        if (freezeAnimatedSprites)
            RestoreAnimations();

        ExitCustomStunOverride();

        spriteBases.Clear();
        animStates.Clear();
        suppressRestore = false;

        stunVisualSuppressedByDamaged = false;
        hadFrozenBeforeDamaged = false;
    }

    private void InitializeStunVisualIfNeeded(bool wantsCustomAnim)
    {
        if (stunVisualInitialized)
            return;

        stunWantsCustomAnim = wantsCustomAnim;

        if (wantsCustomAnim)
        {
            EnterCustomStunOverride();
        }
        else
        {
            CaptureSpriteBases();
            ForceIdleAndApplyOffsets();

            if (freezeAnimatedSprites)
                FreezeAnimations();
        }

        stunVisualInitialized = true;
    }
}