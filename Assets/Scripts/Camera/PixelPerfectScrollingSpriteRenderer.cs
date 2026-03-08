using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class PixelPerfectScrollingSpriteRenderer : MonoBehaviour
{
    public enum ScrollDirection
    {
        TopToBottom,
        BottomToTop,
        LeftToRight,
        RightToLeft
    }

    [Header("References")]
    [SerializeField] SpriteRenderer targetRenderer;
    [SerializeField] Camera targetCamera;

    [Header("Scroll")]
    [SerializeField] bool scroll = true;
    [SerializeField] bool unscaledTime = true;
    [SerializeField] float scrollSpeedUnitsPerSecond = 0.5f;
    [SerializeField] ScrollDirection direction = ScrollDirection.TopToBottom;

    [Header("Loop")]
    [SerializeField] bool infiniteLoop = true;
    [SerializeField] bool autoCalculateLoopDistance = true;
    [SerializeField] Vector2 startPosition = new Vector2(20f, -0.5f);
    [SerializeField] float loopDistance = 14f;

    [Header("Seam Fix")]
    [SerializeField] bool useTileOverlap = true;
    [SerializeField, Min(0f)] float overlapPixels = 1f;
    [SerializeField, Min(0f)] float recycleOutOfViewExtraMargin = 0.25f;

    [Header("Pixel Perfect")]
    [SerializeField] bool snapToPixels = true;
    [SerializeField] float pixelsPerUnit = 16f;

    [Header("Runtime Copies")]
    [SerializeField] string previousCopyName = "_LoopCopyPrev";
    [SerializeField] string nextCopyName = "_LoopCopyNext";

    Transform cachedTransform;
    SpriteRenderer previousCopyRenderer;
    SpriteRenderer nextCopyRenderer;

    readonly List<SpriteRenderer> tiles = new List<SpriteRenderer>(3);
    readonly Dictionary<SpriteRenderer, float> logicalAxisByRenderer = new Dictionary<SpriteRenderer, float>(3);

    float tileSizeUnits;
    float effectiveStepUnits;

    void Reset()
    {
        targetRenderer = GetComponent<SpriteRenderer>();
        cachedTransform = transform;
        startPosition = new Vector2(cachedTransform.position.x, cachedTransform.position.y);
        EnsureTargetCamera();
        AutoAssignPixelsPerUnit();

        if (autoCalculateLoopDistance)
            RecalculateLoopDistance();

        RecalculateDerivedValues();
    }

    void Awake()
    {
        Initialize();
    }

    void OnEnable()
    {
        Initialize();
    }

    void OnValidate()
    {
        Initialize();
    }

    void LateUpdate()
    {
        EnsureRefs();

        if (targetRenderer == null || targetRenderer.sprite == null)
            return;

        if (autoCalculateLoopDistance)
            RecalculateLoopDistance();

        RecalculateDerivedValues();
        EnsureCopies();
        RefreshCopiesVisual();
        RebuildTileList();

        if (!Application.isPlaying)
        {
            InitializeLogicalPositions();
            ApplyVisualPositions();
            return;
        }

        if (!scroll || !infiniteLoop)
            return;

        float dt = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float delta = scrollSpeedUnitsPerSecond * dt;

        MoveLogicalTiles(delta);
        RecycleTilesIfNeeded();
        ApplyVisualPositions();
    }

    void Initialize()
    {
        cachedTransform = transform;
        EnsureRefs();
        EnsureStartPosition();
        EnsureTargetCamera();
        AutoAssignPixelsPerUnit();

        if (autoCalculateLoopDistance)
            RecalculateLoopDistance();

        RecalculateDerivedValues();
        EnsureCopies();
        RefreshCopiesVisual();
        RebuildTileList();
        InitializeLogicalPositions();
        ApplyVisualPositions();
    }

    void EnsureRefs()
    {
        if (cachedTransform == null)
            cachedTransform = transform;

        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();
    }

    void EnsureStartPosition()
    {
        if (cachedTransform == null)
            return;

        if (startPosition == Vector2.zero)
            startPosition = new Vector2(cachedTransform.position.x, cachedTransform.position.y);
    }

    void EnsureTargetCamera()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    void AutoAssignPixelsPerUnit()
    {
        if (targetRenderer == null || targetRenderer.sprite == null)
            return;

        float spritePpu = targetRenderer.sprite.pixelsPerUnit;
        if (spritePpu > 0f)
            pixelsPerUnit = spritePpu;
    }

    void RecalculateLoopDistance()
    {
        if (targetRenderer == null || targetRenderer.sprite == null)
            return;

        Sprite sprite = targetRenderer.sprite;

        if (direction == ScrollDirection.TopToBottom || direction == ScrollDirection.BottomToTop)
            loopDistance = sprite.rect.height / sprite.pixelsPerUnit;
        else
            loopDistance = sprite.rect.width / sprite.pixelsPerUnit;
    }

    void RecalculateDerivedValues()
    {
        tileSizeUnits = Mathf.Max(0.0001f, loopDistance);
        effectiveStepUnits = Mathf.Max(0.0001f, tileSizeUnits - GetOverlapUnits());
    }

    void EnsureCopies()
    {
        if (!infiniteLoop || targetRenderer == null)
        {
            DestroyCopiesImmediate();
            return;
        }

        Transform parent = cachedTransform.parent;

        if (previousCopyRenderer == null)
        {
            Transform existingPrev = parent != null ? parent.Find(previousCopyName) : null;
            if (existingPrev != null)
                previousCopyRenderer = existingPrev.GetComponent<SpriteRenderer>();

            if (previousCopyRenderer == null)
                previousCopyRenderer = CreateCopy(previousCopyName, parent);
        }

        if (nextCopyRenderer == null)
        {
            Transform existingNext = parent != null ? parent.Find(nextCopyName) : null;
            if (existingNext != null)
                nextCopyRenderer = existingNext.GetComponent<SpriteRenderer>();

            if (nextCopyRenderer == null)
                nextCopyRenderer = CreateCopy(nextCopyName, parent);
        }
    }

    SpriteRenderer CreateCopy(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName);

        if (parent != null)
            go.transform.SetParent(parent, false);

        go.transform.localScale = cachedTransform.localScale;
        go.transform.localRotation = cachedTransform.localRotation;

        return go.AddComponent<SpriteRenderer>();
    }

    void RefreshCopiesVisual()
    {
        RefreshSingleCopyVisual(previousCopyRenderer);
        RefreshSingleCopyVisual(nextCopyRenderer);
    }

    void RefreshSingleCopyVisual(SpriteRenderer copy)
    {
        if (targetRenderer == null || copy == null)
            return;

        copy.sprite = targetRenderer.sprite;
        copy.sharedMaterial = targetRenderer.sharedMaterial;
        copy.color = targetRenderer.color;
        copy.flipX = targetRenderer.flipX;
        copy.flipY = targetRenderer.flipY;
        copy.drawMode = targetRenderer.drawMode;
        copy.sortingLayerID = targetRenderer.sortingLayerID;
        copy.sortingOrder = targetRenderer.sortingOrder;
        copy.maskInteraction = targetRenderer.maskInteraction;
        copy.enabled = targetRenderer.enabled;
        copy.transform.localScale = cachedTransform.localScale;
        copy.transform.localRotation = cachedTransform.localRotation;
    }

    void RebuildTileList()
    {
        tiles.Clear();

        if (targetRenderer != null)
            tiles.Add(targetRenderer);

        if (previousCopyRenderer != null)
            tiles.Add(previousCopyRenderer);

        if (nextCopyRenderer != null)
            tiles.Add(nextCopyRenderer);
    }

    void InitializeLogicalPositions()
    {
        logicalAxisByRenderer.Clear();

        if (targetRenderer == null)
            return;

        float baseAxis = GetStartAxis();

        logicalAxisByRenderer[targetRenderer] = baseAxis;

        if (previousCopyRenderer != null)
            logicalAxisByRenderer[previousCopyRenderer] = baseAxis - effectiveStepUnits;

        if (nextCopyRenderer != null)
            logicalAxisByRenderer[nextCopyRenderer] = baseAxis + effectiveStepUnits;
    }

    float GetStartAxis()
    {
        switch (direction)
        {
            case ScrollDirection.TopToBottom:
            case ScrollDirection.BottomToTop:
                return startPosition.y;

            default:
                return startPosition.x;
        }
    }

    void MoveLogicalTiles(float delta)
    {
        float signedDelta = 0f;

        switch (direction)
        {
            case ScrollDirection.TopToBottom:
            case ScrollDirection.RightToLeft:
                signedDelta = -delta;
                break;

            case ScrollDirection.BottomToTop:
            case ScrollDirection.LeftToRight:
                signedDelta = delta;
                break;
        }

        for (int i = 0; i < tiles.Count; i++)
        {
            SpriteRenderer tile = tiles[i];
            if (tile == null)
                continue;

            if (!logicalAxisByRenderer.ContainsKey(tile))
                logicalAxisByRenderer[tile] = GetAxis(tile.transform.position);

            logicalAxisByRenderer[tile] += signedDelta;
        }
    }

    void RecycleTilesIfNeeded()
    {
        if (tiles.Count < 3 || targetCamera == null)
            return;

        float camMin;
        float camMax;
        GetCameraAxisBounds(out camMin, out camMax);

        bool recycledAny = true;
        int safety = 0;

        while (recycledAny && safety < 8)
        {
            recycledAny = false;
            safety++;

            SpriteRenderer minTile = GetMinTileLogical();
            SpriteRenderer maxTile = GetMaxTileLogical();

            if (minTile == null || maxTile == null)
                break;

            float minCenter = logicalAxisByRenderer[minTile];
            float maxCenter = logicalAxisByRenderer[maxTile];

            float minTileMin = minCenter - tileSizeUnits * 0.5f;
            float minTileMax = minCenter + tileSizeUnits * 0.5f;
            float maxTileMin = maxCenter - tileSizeUnits * 0.5f;
            float maxTileMax = maxCenter + tileSizeUnits * 0.5f;

            switch (direction)
            {
                case ScrollDirection.BottomToTop:
                    if (maxTileMin >= camMax + recycleOutOfViewExtraMargin)
                    {
                        float newAxis = minCenter - effectiveStepUnits;
                        logicalAxisByRenderer[maxTile] = newAxis;
                        recycledAny = true;
                    }
                    break;

                case ScrollDirection.TopToBottom:
                    if (minTileMax <= camMin - recycleOutOfViewExtraMargin)
                    {
                        float newAxis = maxCenter + effectiveStepUnits;
                        logicalAxisByRenderer[minTile] = newAxis;
                        recycledAny = true;
                    }
                    break;

                case ScrollDirection.LeftToRight:
                    if (maxTileMin >= camMax + recycleOutOfViewExtraMargin)
                    {
                        float newAxis = minCenter - effectiveStepUnits;
                        logicalAxisByRenderer[maxTile] = newAxis;
                        recycledAny = true;
                    }
                    break;

                case ScrollDirection.RightToLeft:
                    if (minTileMax <= camMin - recycleOutOfViewExtraMargin)
                    {
                        float newAxis = maxCenter + effectiveStepUnits;
                        logicalAxisByRenderer[minTile] = newAxis;
                        recycledAny = true;
                    }
                    break;
            }
        }
    }

    void ApplyVisualPositions()
    {
        for (int i = 0; i < tiles.Count; i++)
        {
            SpriteRenderer tile = tiles[i];
            if (tile == null)
                continue;

            if (!logicalAxisByRenderer.ContainsKey(tile))
                continue;

            Vector3 pos = tile.transform.position;

            switch (direction)
            {
                case ScrollDirection.TopToBottom:
                case ScrollDirection.BottomToTop:
                    pos.x = startPosition.x;
                    pos.y = logicalAxisByRenderer[tile];
                    break;

                default:
                    pos.x = logicalAxisByRenderer[tile];
                    pos.y = startPosition.y;
                    break;
            }

            pos.z = cachedTransform.position.z;

            if (snapToPixels && pixelsPerUnit > 0f)
                pos = SnapPosition(pos);

            tile.transform.position = pos;
        }
    }

    void GetCameraAxisBounds(out float min, out float max)
    {
        EnsureTargetCamera();

        if (targetCamera == null)
        {
            min = -9999f;
            max = 9999f;
            return;
        }

        if (direction == ScrollDirection.TopToBottom || direction == ScrollDirection.BottomToTop)
        {
            float halfHeight = targetCamera.orthographicSize;
            min = targetCamera.transform.position.y - halfHeight;
            max = targetCamera.transform.position.y + halfHeight;
        }
        else
        {
            float halfHeight = targetCamera.orthographicSize;
            float halfWidth = halfHeight * targetCamera.aspect;
            min = targetCamera.transform.position.x - halfWidth;
            max = targetCamera.transform.position.x + halfWidth;
        }
    }

    SpriteRenderer GetMinTileLogical()
    {
        if (tiles.Count == 0)
            return null;

        SpriteRenderer best = null;
        float bestAxis = float.MaxValue;

        for (int i = 0; i < tiles.Count; i++)
        {
            SpriteRenderer tile = tiles[i];
            if (tile == null || !logicalAxisByRenderer.ContainsKey(tile))
                continue;

            float axis = logicalAxisByRenderer[tile];
            if (axis < bestAxis)
            {
                best = tile;
                bestAxis = axis;
            }
        }

        return best;
    }

    SpriteRenderer GetMaxTileLogical()
    {
        if (tiles.Count == 0)
            return null;

        SpriteRenderer best = null;
        float bestAxis = float.MinValue;

        for (int i = 0; i < tiles.Count; i++)
        {
            SpriteRenderer tile = tiles[i];
            if (tile == null || !logicalAxisByRenderer.ContainsKey(tile))
                continue;

            float axis = logicalAxisByRenderer[tile];
            if (axis > bestAxis)
            {
                best = tile;
                bestAxis = axis;
            }
        }

        return best;
    }

    float GetOverlapUnits()
    {
        if (!useTileOverlap || pixelsPerUnit <= 0f)
            return 0f;

        return overlapPixels / pixelsPerUnit;
    }

    Vector3 SnapPosition(Vector3 pos)
    {
        pos.x = Mathf.Round(pos.x * pixelsPerUnit) / pixelsPerUnit;
        pos.y = Mathf.Round(pos.y * pixelsPerUnit) / pixelsPerUnit;
        return pos;
    }

    void DestroyCopiesImmediate()
    {
        DestroySingleCopyImmediate(previousCopyRenderer);
        DestroySingleCopyImmediate(nextCopyRenderer);
        previousCopyRenderer = null;
        nextCopyRenderer = null;
        tiles.Clear();
        logicalAxisByRenderer.Clear();
    }

    void DestroySingleCopyImmediate(SpriteRenderer copy)
    {
        if (copy == null)
            return;

        if (Application.isPlaying)
            Destroy(copy.gameObject);
        else
            DestroyImmediate(copy.gameObject);
    }

    float GetAxis(Vector3 position)
    {
        switch (direction)
        {
            case ScrollDirection.TopToBottom:
            case ScrollDirection.BottomToTop:
                return position.y;

            default:
                return position.x;
        }
    }
}