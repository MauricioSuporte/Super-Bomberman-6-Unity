using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
public class JunctionTurningGhostEnemyMovementController : JunctionTurningEnemyMovementController
{
    [Header("Destructibles Pass Through")]
    [SerializeField] private string destructiblesTag = "Destructibles";

    [Header("Ghost Transparency")]
    [SerializeField, Range(0.05f, 1f)] private float ghostAlpha = 0.55f;
    [SerializeField] private float deathAlpha = 1f;
    [SerializeField] private bool applyToAllChildSpriteRenderers = true;

    private Collider2D selfCollider;

    protected override void Awake()
    {
        base.Awake();

        selfCollider = GetComponent<Collider2D>();

        ApplyGhostAlpha();

        if (selfCollider == null)
            return;

        var destructibles = GameObject.FindGameObjectsWithTag(destructiblesTag);
        if (destructibles == null || destructibles.Length == 0)
            return;

        for (int i = 0; i < destructibles.Length; i++)
        {
            var go = destructibles[i];
            if (go == null)
                continue;

            var cols = go.GetComponentsInChildren<Collider2D>(true);
            if (cols == null || cols.Length == 0)
                continue;

            for (int c = 0; c < cols.Length; c++)
            {
                var col = cols[c];
                if (col == null)
                    continue;

                Physics2D.IgnoreCollision(selfCollider, col, true);
            }
        }
    }

    protected override void Start()
    {
        base.Start();
        ApplyGhostAlpha();
    }

    private void OnEnable()
    {
        ApplyGhostAlpha();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        ApplyGhostAlpha();
    }

    protected override void Die()
    {
        SetAlphaAll(deathAlpha);
        base.Die();
    }

    private void ApplyGhostAlpha()
    {
        SetAlphaAll(ghostAlpha);

        if (spriteDeath != null)
            SetAlphaFromAnimated(spriteDeath, deathAlpha);
    }

    private void SetAlphaAll(float alpha)
    {
        SetAlphaFromAnimated(spriteUp, alpha);
        SetAlphaFromAnimated(spriteDown, alpha);
        SetAlphaFromAnimated(spriteLeft, alpha);
        SetAlphaFromAnimated(spriteDamaged, alpha);

        if (spriteDeath != null)
            SetAlphaFromAnimated(spriteDeath, alpha);

        if (!applyToAllChildSpriteRenderers)
            return;

        var srs = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
            SetAlpha(srs[i], alpha);

        var imgs = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < imgs.Length; i++)
            SetAlpha(imgs[i], alpha);
    }

    private void SetAlphaFromAnimated(AnimatedSpriteRenderer asr, float alpha)
    {
        if (asr == null)
            return;

        var sr = asr.GetComponent<SpriteRenderer>();
        if (sr == null)
            sr = asr.GetComponentInChildren<SpriteRenderer>(true);

        if (sr != null)
        {
            SetAlpha(sr, alpha);
            return;
        }

        var img = asr.GetComponent<Image>();
        if (img == null)
            img = asr.GetComponentInChildren<Image>(true);

        if (img != null)
            SetAlpha(img, alpha);
    }

    private void SetAlpha(SpriteRenderer sr, float alpha)
    {
        if (sr == null)
            return;

        var c = sr.color;
        c.a = alpha;
        sr.color = c;
    }

    private void SetAlpha(Image img, float alpha)
    {
        if (img == null)
            return;

        var c = img.color;
        c.a = alpha;
        img.color = c;
    }

    protected override bool IsTileBlocked(Vector2 tileCenter)
    {
        Vector2 size = Vector2.one * (tileSize * 0.8f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(tileCenter, size, 0f, obstacleMask);

        if (hits == null || hits.Length == 0)
            return false;

        foreach (var hit in hits)
        {
            if (hit == null)
                continue;

            if (hit.gameObject == gameObject)
                continue;

            if (hit.CompareTag(destructiblesTag))
                continue;

            return true;
        }

        return false;
    }
}
