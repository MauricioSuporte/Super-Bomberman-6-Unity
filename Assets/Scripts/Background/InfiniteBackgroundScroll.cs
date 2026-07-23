using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class InfiniteBackgroundScroll : MonoBehaviour
{
    static readonly int ScrollUvId = Shader.PropertyToID("_ScrollUv");

    [Header("Scroll")]
    [SerializeField, Min(0f)] private float scrollSpeed = 0.5f;

    [Header("Auto Tiling (keeps sprite scale)")]
    [SerializeField] private bool autoTilingFromSprite = true;

    [Tooltip("Extra multiplier on computed tiling (1 = keep original scale).")]
    [SerializeField] private Vector2 tilingMultiplier = Vector2.one;

    private SpriteRenderer _sr;
    private Material _mat;
    private float _offsetX;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _mat = _sr.material;

        RefreshTiling();
    }

    void OnDestroy()
    {
        if (_mat != null)
            Destroy(_mat);
    }

    void OnValidate()
    {
        if (!Application.isPlaying) return;
        RefreshTiling();
    }

    void Update()
    {
        if (_mat == null) return;

        _offsetX += scrollSpeed * Time.deltaTime;
        ApplyScrollUv();
    }

    private void RefreshTiling()
    {
        if (_mat == null || _sr == null) return;

        if (!autoTilingFromSprite || _sr.sprite == null) return;

        Vector2 spriteWorldSize = _sr.sprite.bounds.size;

        Vector2 targetSize = _sr.size;

        float tileX = spriteWorldSize.x <= 0.0001f ? 1f : (targetSize.x / spriteWorldSize.x);
        float tileY = spriteWorldSize.y <= 0.0001f ? 1f : (targetSize.y / spriteWorldSize.y);

        Vector2 tiling = new(tileX, tileY);
        tiling = new Vector2(tiling.x * tilingMultiplier.x, tiling.y * tilingMultiplier.y);

        _mat.SetVector(ScrollUvId, new Vector4(tiling.x, tiling.y, _offsetX, 0f));
    }

    private void ApplyScrollUv()
    {
        Vector4 scrollUv = _mat.GetVector(ScrollUvId);
        if (scrollUv.x == 0f || scrollUv.y == 0f)
            scrollUv = new Vector4(1f, 1f, 0f, 0f);

        scrollUv.z = _offsetX;
        scrollUv.w = 0f;
        _mat.SetVector(ScrollUvId, scrollUv);
    }
}
