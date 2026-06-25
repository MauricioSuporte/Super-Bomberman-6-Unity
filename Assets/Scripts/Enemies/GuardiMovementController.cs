using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class GuardiMovementController : JunctionTurningEnemyMovementController
{
    [Header("Firework Death Rule")]
    [SerializeField] private bool dieWhenFireworksCleared = true;
    [SerializeField, Min(0f)] private float fireworkDeathBlinkSeconds = 2f;
    [SerializeField, Min(0.01f)] private float fireworkDeathBlinkInterval = 0.1f;

    static readonly Dictionary<Sprite, Sprite> whiteSpriteCache = new();

    bool fireworkDeathSequenceStarted;
    Coroutine fireworkDeathRoutine;

    void OnEnable()
    {
        FireworkTileHandler.AllFireworksDestroyedAtPhase2Start += HandleAllFireworksDestroyedAtPhase2Start;
    }

    void OnDisable()
    {
        FireworkTileHandler.AllFireworksDestroyedAtPhase2Start -= HandleAllFireworksDestroyedAtPhase2Start;

        if (fireworkDeathRoutine != null)
        {
            StopCoroutine(fireworkDeathRoutine);
            fireworkDeathRoutine = null;
        }

        RestoreActiveSpriteVisual();
    }

    void HandleAllFireworksDestroyedAtPhase2Start()
    {
        if (!dieWhenFireworksCleared)
            return;

        if (isDead || fireworkDeathSequenceStarted)
            return;

        fireworkDeathRoutine = StartCoroutine(FireworkDeathSequence());
    }

    protected override void FixedUpdate()
    {
        if (fireworkDeathSequenceStarted)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }

        base.FixedUpdate();
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead || fireworkDeathSequenceStarted)
            return;

        int explosionLayer = LayerMask.NameToLayer("Explosion");
        if (explosionLayer >= 0 && other.gameObject.layer == explosionLayer)
            return;

        base.OnTriggerEnter2D(other);
    }

    IEnumerator FireworkDeathSequence()
    {
        fireworkDeathSequenceStarted = true;
        isStuck = false;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (activeSprite != null)
        {
            activeSprite.enabled = true;
            activeSprite.SetFrozen(true);
            activeSprite.RefreshFrame();
        }

        SpriteRenderer sr = ResolveSpriteRenderer(activeSprite);
        Sprite originalSprite = sr != null ? sr.sprite : null;
        Color originalColor = sr != null ? sr.color : Color.white;
        Sprite whiteSprite = originalSprite != null ? GetOrCreateWhiteSprite(originalSprite) : null;
        if (sr != null)
        {
            sr.color = originalColor;
            sr.sprite = originalSprite;
        }

        float elapsed = 0f;
        bool showWhite = true;
        float interval = Mathf.Max(0.01f, fireworkDeathBlinkInterval);
        float duration = Mathf.Max(0f, fireworkDeathBlinkSeconds);

        while (elapsed < duration)
        {
            if (sr != null && originalSprite != null && whiteSprite != null)
            {
                sr.sprite = showWhite ? whiteSprite : originalSprite;
                sr.color = originalColor;
            }

            showWhite = !showWhite;

            float wait = Mathf.Min(interval, duration - elapsed);
            elapsed += wait;
            yield return new WaitForSeconds(wait);
        }

        if (sr != null)
        {
            sr.sprite = originalSprite;
            sr.color = originalColor;
        }

        if (activeSprite != null)
            activeSprite.SetFrozen(false);

        fireworkDeathRoutine = null;
        base.Die();
    }

    void RestoreActiveSpriteVisual()
    {
        if (activeSprite == null)
            return;

        activeSprite.SetFrozen(false);
        activeSprite.RefreshFrame();
    }

    static SpriteRenderer ResolveSpriteRenderer(AnimatedSpriteRenderer animatedSprite)
    {
        if (animatedSprite == null)
            return null;

        if (animatedSprite.TryGetComponent(out SpriteRenderer sr) && sr != null)
            return sr;

        return animatedSprite.GetComponentInChildren<SpriteRenderer>(true);
    }

    static Sprite GetOrCreateWhiteSprite(Sprite source)
    {
        if (source == null)
            return null;

        if (whiteSpriteCache.TryGetValue(source, out Sprite cached) && cached != null)
            return cached;

        Texture2D sourceTexture = source.texture;
        Rect rect = source.rect;
        int width = Mathf.Max(1, Mathf.RoundToInt(rect.width));
        int height = Mathf.Max(1, Mathf.RoundToInt(rect.height));

        RenderTexture previous = RenderTexture.active;
        RenderTexture rt = RenderTexture.GetTemporary(
            sourceTexture.width,
            sourceTexture.height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Default);

        Texture2D readable = null;

        try
        {
            Graphics.Blit(sourceTexture, rt);
            RenderTexture.active = rt;

            readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
            readable.ReadPixels(rect, 0, 0, false);
            readable.Apply(false, false);

            Color[] pixels = readable.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(1f, 1f, 1f, pixels[i].a);

            readable.SetPixels(pixels);
            readable.Apply(false, true);

            readable.filterMode = sourceTexture != null ? sourceTexture.filterMode : FilterMode.Point;
            readable.wrapMode = TextureWrapMode.Clamp;

            Sprite whiteSprite = Sprite.Create(
                readable,
                new Rect(0f, 0f, width, height),
                new Vector2(
                    source.pivot.x / Mathf.Max(1f, rect.width),
                    source.pivot.y / Mathf.Max(1f, rect.height)),
                source.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                source.border);

            whiteSprite.name = source.name + "_WhiteBlink";
            whiteSpriteCache[source] = whiteSprite;
            return whiteSprite;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        }
    }
}
