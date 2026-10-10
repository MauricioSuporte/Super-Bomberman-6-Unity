using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FreezerVenusIntro : MonoBehaviour
{
    [Min(0.1f)] public float descendSeconds = 1.35f;
    [Min(0.1f)] public float beamOpenSeconds = 0.55f;
    [Min(0.1f)] public float absorptionSeconds = 1.5f;
    private const int BeamWidth = 32;
    private Texture2D beamTexture;
    private Sprite beamSprite;
    private Color32[] pixels;
    private GameObject beam;
    private Texture2D shadowTexture;
    private Sprite growingShadow;
    private Sprite originalShadow;
    private SpriteRenderer shadowRenderer;
    private Material absorptionMaterial;
    private SpriteRenderer[] absorptionRenderers;
    private Material[] originalMaterials;

    // The hall sequence retains its existing player locks throughout this coroutine.
    public IEnumerator Play(FreezerVenusBoss boss, GameObject pretty)
    {
        Vector3 prettyOrigin = FreezerVenusArena.Snap(pretty.transform.position);
        Vector3 stop = prettyOrigin + Vector3.up * 3.75f;
        Vector3 start = stop + Vector3.up * 3f;
        var camera = Camera.main;
        if (camera != null)
            start.y = Mathf.Max(stop.y + 2f, camera.ViewportToWorldPoint(new Vector3(0.5f, 1f,
                Mathf.Abs(camera.transform.position.z))).y + 1.875f);
        Vector3 shadowPosition = FreezerVenusArena.Snap(stop + Vector3.down * 1.75f);
        boss.SetWorldPosition(start);
        BeginShadowGrowth(boss.shadow);
        if (boss.shadow != null) boss.shadow.transform.position = shadowPosition;
        float elapsed = 0f;
        while (elapsed < descendSeconds)
        {
            if (!GamePauseController.IsPaused)
            {
                elapsed += Time.deltaTime;
                boss.SetWorldPosition(Vector3.Lerp(start, stop, Mathf.Clamp01(elapsed / descendSeconds)));
                if (boss.shadow != null) boss.shadow.transform.position = shadowPosition;
                DrawShadow(Mathf.Clamp01(elapsed / descendSeconds));
                boss.SetFrame(boss.closedFrames, (int)(elapsed * 4f) % Mathf.Max(1, boss.closedFrames.Length));
            }
            yield return null;
        }
        CleanupShadow();
        yield return FreezerVenusBoss.WaitUnpaused(0.2f);
        Vector3 tip = boss.CrownPosition;
        // Same floor and full width as PrettyBomberMagicEffect's portal ellipse.
        Vector3 bottom = prettyOrigin + Vector3.down * 0.5f;
        CreateBeam(boss, tip, bottom);
        yield return AnimateBeam(true);
        var renderers = pretty.GetComponentsInChildren<SpriteRenderer>(true);
        var colors = new Color[renderers.Length];
        var visibleStates = new bool[renderers.Length];
        BeginAbsorptionAppearance(boss, renderers);
        for (int i = 0; i < renderers.Length; i++)
        {
            colors[i] = renderers[i].color;
            visibleStates[i] = renderers[i].enabled;
        }
        var prettyRb = pretty.GetComponent<Rigidbody2D>();
        elapsed = 0f;
        float blinkPhase = 0f;
        while (elapsed < absorptionSeconds)
        {
            if (!GamePauseController.IsPaused)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / absorptionSeconds);
                Vector3 position = FreezerVenusArena.Snap(Vector3.Lerp(prettyOrigin, tip + Vector3.down * 2f, t));
                pretty.transform.position = position;
                if (prettyRb != null) prettyRb.position = position;
                blinkPhase += Time.deltaTime / Mathf.Lerp(0.09f, 0.0275f, t);
                ApplyAbsorptionAppearance(renderers, colors, visibleStates, t, blinkPhase);
            }
            yield return null;
        }
        pretty.SetActive(false);
        CleanupAbsorptionAppearance();
        Destroy(pretty);
        yield return AnimateBeam(false);
        CleanupBeam();
        for (int i = 0; i < boss.openingFrames.Length; i++)
        {
            boss.SetFrame(boss.openingFrames, i);
            yield return FreezerVenusBoss.WaitUnpaused(0.3f);
        }
        yield return FreezerVenusBoss.WaitUnpaused(0.2f);
    }

    private void BeginAbsorptionAppearance(FreezerVenusBoss boss, SpriteRenderer[] renderers)
    {
        absorptionRenderers = renderers;
        originalMaterials = new Material[renderers.Length];
        var shader = Resources.Load<Shader>("StageAssets/FreezerVenusAbsorption");
        if (shader != null) absorptionMaterial = new Material(shader) { name = "Pretty Bomber absorption light" };
        for (int i = 0; i < renderers.Length; i++)
        {
            originalMaterials[i] = renderers[i].sharedMaterial;
            renderers[i].sortingLayerID = boss.body.sortingLayerID;
            renderers[i].sortingOrder = boss.body.sortingOrder + 3 + i;
            if (absorptionMaterial != null) renderers[i].sharedMaterial = absorptionMaterial;
        }
    }

    private void ApplyAbsorptionAppearance(SpriteRenderer[] renderers, Color[] colors,
        bool[] visibleStates, float progress, float blinkPhase)
    {
        if (absorptionMaterial != null)
            absorptionMaterial.SetFloat("_LightAmount", Mathf.SmoothStep(0f, 1f, progress));
        bool visible = Mathf.FloorToInt(blinkPhase) % 2 == 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            Color color = colors[i];
            color.a *= 1f - progress;
            renderers[i].color = color;
            renderers[i].enabled = visibleStates[i] && visible;
        }
    }

    private void CleanupAbsorptionAppearance()
    {
        if (absorptionRenderers != null)
            for (int i = 0; i < absorptionRenderers.Length; i++)
                if (absorptionRenderers[i] != null) absorptionRenderers[i].sharedMaterial = originalMaterials[i];
        if (absorptionMaterial != null) Destroy(absorptionMaterial);
        absorptionMaterial = null;
        absorptionRenderers = null;
        originalMaterials = null;
    }

    private void BeginShadowGrowth(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null) return;
        shadowRenderer = renderer;
        originalShadow = renderer.sprite;
        int width = Mathf.RoundToInt(originalShadow.rect.width);
        int height = Mathf.RoundToInt(originalShadow.rect.height);
        shadowTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "FreezerVenus growing shadow" };
        growingShadow = Sprite.Create(shadowTexture, new Rect(0, 0, width, height),
            originalShadow.pivot / new Vector2(width, height), originalShadow.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        renderer.sprite = growingShadow;
        DrawShadow(0f);
    }

    private void DrawShadow(float progress)
    {
        if (shadowTexture == null) return;
        int width = shadowTexture.width;
        int height = shadowTexture.height;
        int scaledWidth = Mathf.Max(2, Mathf.RoundToInt(width * Mathf.Lerp(0.2f, 1f, progress)));
        int scaledHeight = Mathf.Max(2, Mathf.RoundToInt(height * Mathf.Lerp(0.2f, 1f, progress)));
        int left = Mathf.RoundToInt(originalShadow.pivot.x) - scaledWidth / 2;
        int bottom = Mathf.RoundToInt(originalShadow.pivot.y) - scaledHeight / 2;
        var result = new Color32[width * height];
        Color32[] source = originalShadow.texture.GetPixels32();
        Rect rect = originalShadow.rect;
        for (int y = 0; y < scaledHeight; y++)
            for (int x = 0; x < scaledWidth; x++)
            {
                int targetX = left + x;
                int targetY = bottom + y;
                if (targetX < 0 || targetX >= width || targetY < 0 || targetY >= height) continue;
                int sourceX = (int)rect.x + x * width / scaledWidth;
                int sourceY = (int)rect.y + y * height / scaledHeight;
                result[targetY * width + targetX] = source[sourceY * originalShadow.texture.width + sourceX];
            }
        shadowTexture.SetPixels32(result);
        shadowTexture.Apply(false, false);
    }

    private void CleanupShadow()
    {
        if (shadowRenderer != null && originalShadow != null) shadowRenderer.sprite = originalShadow;
        if (growingShadow != null) Destroy(growingShadow);
        if (shadowTexture != null) Destroy(shadowTexture);
        shadowRenderer = null;
        originalShadow = null;
        growingShadow = null;
        shadowTexture = null;
    }

    private void CreateBeam(FreezerVenusBoss boss, Vector3 tip, Vector3 bottom)
    {
        int height = Mathf.Max(2, Mathf.RoundToInt((tip.y - bottom.y) * 16f) + 1);
        beamTexture = new Texture2D(BeamWidth, height, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "FreezerVenus yellow pixel light" };
        pixels = new Color32[BeamWidth * height];
        beamSprite = Sprite.Create(beamTexture, new Rect(0, 0, BeamWidth, height),
            new Vector2(0.5f, 0f), 16f, 0, SpriteMeshType.FullRect);
        beam = new GameObject("FreezerVenus absorption light");
        beam.transform.SetParent(transform, false);
        beam.transform.position = FreezerVenusArena.Snap(bottom);
        var renderer = beam.AddComponent<SpriteRenderer>();
        renderer.sprite = beamSprite;
        renderer.sortingLayerID = boss.body.sortingLayerID;
        renderer.sortingOrder = boss.body.sortingOrder + 2;
        DrawBeam(0f);
    }

    private IEnumerator AnimateBeam(bool opening)
    {
        float elapsed = 0f;
        while (elapsed < beamOpenSeconds)
        {
            if (!GamePauseController.IsPaused)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / beamOpenSeconds);
                DrawBeam(opening ? t : 1f - t);
            }
            yield return null;
        }
        DrawBeam(opening ? 1f : 0f);
    }

    private void DrawBeam(float progress)
    {
        int height = beamTexture.height;
        int rayLength = Mathf.RoundToInt((height - 1) * Mathf.Clamp01(progress * 3f));
        float expansion = Mathf.Clamp01((progress - 0.33f) / 0.67f);
        for (int y = 0; y < height; y++)
        {
            int fromTip = height - 1 - y;
            float distance = fromTip / (float)(height - 1);
            int halfWidth = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(1f, BeamWidth * 0.5f, distance) * expansion));
            Color32 yellow = new(255, 232, 72, (byte)Mathf.RoundToInt(Mathf.Lerp(135f, 85f, distance)));
            for (int x = 0; x < BeamWidth; x++)
                pixels[y * BeamWidth + x] = progress > 0f && fromTip <= rayLength &&
                    x >= BeamWidth / 2 - halfWidth && x < BeamWidth / 2 + halfWidth ? yellow : new Color32(0, 0, 0, 0);
        }
        beamTexture.SetPixels32(pixels);
        beamTexture.Apply(false, false);
    }

    private void CleanupBeam()
    {
        if (beam != null) Destroy(beam);
        if (beamSprite != null) Destroy(beamSprite);
        if (beamTexture != null) Destroy(beamTexture);
        beam = null;
        beamSprite = null;
        beamTexture = null;
        pixels = null;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        CleanupAbsorptionAppearance();
        CleanupShadow();
        CleanupBeam();
    }
}
