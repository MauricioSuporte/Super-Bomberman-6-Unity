using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class StunReceiver : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool isStunned;

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

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Stun(float seconds)
    {
        float dur = Mathf.Max(0.01f, seconds);
        float newEnd = Time.time + dur;

        suppressRestore = false;

        if (!isStunned || newEnd > stunEndTime)
            stunEndTime = newEnd;

        if (stunRoutine == null)
            stunRoutine = StartCoroutine(StunRoutine());
    }

    public void CancelStun(bool restoreVisuals)
    {
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
        }
        else
        {
            spriteBases.Clear();
            animStates.Clear();
        }
    }

    public void CancelStunForDeath()
    {
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
    }

    IEnumerator StunRoutine()
    {
        isStunned = true;

        CaptureSpriteBases();
        ForceIdleAndApplyOffsets();

        if (freezeAnimatedSprites)
            FreezeAnimations();

        float seed = Random.value * 1000f;

        while (Time.time < stunEndTime)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            if (shakeWhileStunned && spriteBases.Count > 0)
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

        if (!suppressRestore && isActiveAndEnabled && gameObject.activeInHierarchy)
        {
            RestoreSpriteBases();
            if (freezeAnimatedSprites)
                RestoreAnimations();
        }
        else
        {
            spriteBases.Clear();
            animStates.Clear();
        }

        stunRoutine = null;
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

    void OnDisable()
    {
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

        spriteBases.Clear();
        animStates.Clear();
        suppressRestore = false;
    }
}
