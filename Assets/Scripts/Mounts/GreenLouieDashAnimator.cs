using System.Collections.Generic;
using UnityEngine;

public class GreenLouieDashAnimator : MonoBehaviour, IGreenLouieDashExternalAnimator
{
    public AnimatedSpriteRenderer rollUp;
    public AnimatedSpriteRenderer rollDown;
    public AnimatedSpriteRenderer rollLeft;
    public AnimatedSpriteRenderer rollRight;

    [Header("Dash Afterimage")]
    [SerializeField, Min(0.01f)] private float afterimageInterval = 0.06f;
    [SerializeField, Min(0.01f)] private float afterimageDuration = 0.22f;
    [SerializeField, Range(0f, 1f)] private float afterimageInitialAlpha = 0.45f;
    [SerializeField] private Color afterimageTint = new(0.65f, 1f, 0.65f, 1f);

    AnimatedSpriteRenderer active;

    struct CachedState
    {
        public AnimatedSpriteRenderer asr;
        public bool enabled;
        public bool idle;
        public bool loop;
    }

    readonly List<CachedState> cachedMovementStates = new();
    bool dashing;
    float nextAfterimageTime;

    public void Play(Vector2 dir)
    {
        CacheAndDisableAllMovementSprites();

        dashing = true;
        nextAfterimageTime = Time.time;

        active = GetRoll(dir);
        DisableAllRolls();

        if (active != null)
        {
            if (active.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = (dir == Vector2.right);

            active.enabled = true;
            active.idle = false;
            active.loop = true;
            active.RefreshFrame();
        }
    }

    public void Stop()
    {
        dashing = false;

        if (active != null)
        {
            if (active.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = false;

            active.enabled = false;
        }

        active = null;
        DisableAllRolls();
        RestoreAllMovementSprites();
    }

    public void CancelForDeath()
    {
        dashing = false;

        if (active != null)
        {
            if (active.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = false;

            active.enabled = false;
        }

        active = null;

        DisableAllRolls();

        cachedMovementStates.Clear();

        enabled = false;
    }

    void LateUpdate()
    {
        if (!dashing)
            return;

        for (int i = 0; i < cachedMovementStates.Count; i++)
        {
            var s = cachedMovementStates[i].asr;
            if (s == null)
                continue;

            if (s.enabled)
                s.enabled = false;
        }

        if (Time.time >= nextAfterimageTime)
        {
            SpawnAfterimage();
            nextAfterimageTime = Time.time + Mathf.Max(0.01f, afterimageInterval);
        }
    }

    void SpawnAfterimage()
    {
        if (active == null || !active.enabled ||
            !active.TryGetComponent(out SpriteRenderer source) ||
            source == null || source.sprite == null)
            return;

        GameObject ghost = new($"{name}_DashAfterimage");
        ghost.layer = source.gameObject.layer;
        ghost.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        ghost.transform.localScale = source.transform.lossyScale;

        SpriteRenderer ghostRenderer = ghost.AddComponent<SpriteRenderer>();
        ghostRenderer.sprite = source.sprite;
        ghostRenderer.flipX = source.flipX;
        ghostRenderer.flipY = source.flipY;
        ghostRenderer.sortingLayerID = source.sortingLayerID;
        ghostRenderer.sortingOrder = source.sortingOrder - 1;
        ghostRenderer.maskInteraction = source.maskInteraction;
        ghostRenderer.spriteSortPoint = source.spriteSortPoint;
        ghostRenderer.sharedMaterial = source.sharedMaterial;

        Color color = source.color * afterimageTint;
        color.a = source.color.a * afterimageTint.a * Mathf.Clamp01(afterimageInitialAlpha);
        ghostRenderer.color = color;

        LouieDashAfterimage fade = ghost.AddComponent<LouieDashAfterimage>();
        fade.Initialize(ghostRenderer, Mathf.Max(0.01f, afterimageDuration));
    }

    AnimatedSpriteRenderer GetRoll(Vector2 dir)
    {
        if (dir == Vector2.up) return rollUp;
        if (dir == Vector2.down) return rollDown;
        if (dir == Vector2.left) return rollLeft;
        if (dir == Vector2.right) return rollRight;
        return rollDown;
    }

    void DisableAllRolls()
    {
        if (rollUp) rollUp.enabled = false;
        if (rollDown) rollDown.enabled = false;
        if (rollLeft) rollLeft.enabled = false;
        if (rollRight) rollRight.enabled = false;
    }

    void CacheAndDisableAllMovementSprites()
    {
        cachedMovementStates.Clear();

        var sprites = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        foreach (var s in sprites)
        {
            if (s == null)
                continue;

            if (s == rollUp || s == rollDown || s == rollLeft || s == rollRight)
                continue;

            cachedMovementStates.Add(new CachedState
            {
                asr = s,
                enabled = s.enabled,
                idle = s.idle,
                loop = s.loop
            });

            s.enabled = false;
        }
    }

    void RestoreAllMovementSprites()
    {
        for (int i = 0; i < cachedMovementStates.Count; i++)
        {
            var st = cachedMovementStates[i];
            if (st.asr == null)
                continue;

            st.asr.idle = st.idle;
            st.asr.loop = st.loop;
            st.asr.enabled = st.enabled;

            if (st.asr.enabled)
                st.asr.RefreshFrame();
        }

        cachedMovementStates.Clear();
    }
}

sealed class LouieDashAfterimage : MonoBehaviour
{
    SpriteRenderer spriteRenderer;
    float duration;
    float elapsed;
    float initialAlpha;

    public void Initialize(SpriteRenderer renderer, float lifetime)
    {
        spriteRenderer = renderer;
        duration = Mathf.Max(0.01f, lifetime);
        initialAlpha = spriteRenderer != null ? spriteRenderer.color.a : 0f;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / duration);

        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = Mathf.Lerp(initialAlpha, 0f, progress);
            spriteRenderer.color = color;
        }

        if (progress >= 1f)
            Destroy(gameObject);
    }
}
