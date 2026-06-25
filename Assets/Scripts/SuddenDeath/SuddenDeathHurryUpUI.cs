using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class SuddenDeathHurryUpUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Image image;
    [SerializeField] private RectTransform rectTransform;

    [Header("Timing")]
    [SerializeField] private float duration = 2f;

    [Header("Teleport Animation")]
    [SerializeField] private bool enableTeleportIntro = true;
    [SerializeField] private float teleportDuration = 1f;
    [SerializeField] private float teleportInterval = 0.04f;
    [SerializeField] private int minimumTeleportMoves = 48;
    [SerializeField] private Vector2 teleportAreaAtStart = new Vector2(220f, 120f);

    [Header("Dynamic Scale (igual ao StageLabel)")]
    [SerializeField] private bool dynamicScale = true;
    [SerializeField] private int referenceWidth = 256;
    [SerializeField] private int referenceHeight = 224;
    [SerializeField] private bool useIntegerUpscale = true;
    [SerializeField] private int designUpscale = 4;
    [SerializeField] private float extraScaleMultiplier = 1f;
    [SerializeField] private float minScale = 0.25f;
    [SerializeField] private float maxScale = 10f;

    [Header("Base Layout")]
    [SerializeField] private bool centerOnPlayableArea = true;
    [SerializeField] private float topHudHeightAtDesign = 22f;
    [SerializeField] private Vector2 anchoredPosition = new Vector2(0f, 80f);
    [SerializeField] private float baseWidthAtDesign = 420f;
    [SerializeField] private float baseHeightAtDesign = 64f;

    [Header("Scale")]
    [SerializeField] private float baseScale = 1f;

    Coroutine routine;

    float UiScale
    {
        get
        {
            float canvasScale = 1f;
            if (image != null && image.canvas != null)
                canvasScale = Mathf.Max(0.01f, image.canvas.scaleFactor);

            if (!dynamicScale)
                return 1f / canvasScale;

            Camera cam = Camera.main;

            float usedW = cam != null ? cam.pixelRect.width : Screen.width;
            float usedH = cam != null ? cam.pixelRect.height : Screen.height;

            float sx = usedW / referenceWidth;
            float sy = usedH / referenceHeight;

            float baseScaleRaw = Mathf.Min(sx, sy);
            float baseScale = useIntegerUpscale ? Mathf.Round(baseScaleRaw) : baseScaleRaw;

            if (baseScale < 1f)
                baseScale = 1f;

            float normalized = baseScale / designUpscale;
            float ui = normalized * extraScaleMultiplier;

            ui /= canvasScale;
            return Mathf.Clamp(ui, minScale, maxScale);
        }
    }

    void Awake()
    {
        if (image == null)
            image = GetComponent<Image>();

        if (rectTransform == null && image != null)
            rectTransform = image.rectTransform;

        if (image != null)
            image.enabled = false;
    }

    public void Play()
    {
        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(PlayRoutine());
    }

    IEnumerator PlayRoutine()
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        float uiScale = UiScale * baseScale;

        Vector2 basePos = GetBaseAnchoredPosition(uiScale);

        rectTransform.anchoredPosition = basePos;
        rectTransform.sizeDelta = new Vector2(
            Mathf.Round(baseWidthAtDesign * uiScale),
            Mathf.Round(baseHeightAtDesign * uiScale));

        rectTransform.localScale = Vector3.one;

        image.enabled = true;

        Vector2 teleportArea = teleportAreaAtStart * uiScale;
        float teleportStepInterval = GetTeleportStepInterval();

        float elapsed = 0f;
        float teleportElapsed = 0f;

        while (elapsed < duration)
        {
            float dt = Time.deltaTime;
            elapsed += dt;

            if (enableTeleportIntro && elapsed <= teleportDuration)
            {
                teleportElapsed += dt;

                if (teleportElapsed >= teleportStepInterval)
                {
                    teleportElapsed -= teleportStepInterval;

                    float t = Mathf.Clamp01(elapsed / teleportDuration);

                    float rangeX = Mathf.Lerp(teleportArea.x, 0f, t);
                    float rangeY = Mathf.Lerp(teleportArea.y, 0f, t);

                    Vector2 offset = new Vector2(
                        Random.Range(-rangeX, rangeX),
                        Random.Range(-rangeY, rangeY));

                    rectTransform.anchoredPosition = basePos + offset;
                }
            }
            else
            {
                rectTransform.anchoredPosition = basePos;
            }

            yield return null;
        }

        rectTransform.anchoredPosition = basePos;
        image.enabled = false;
        routine = null;
    }

    Vector2 GetBaseAnchoredPosition(float uiScale)
    {
        Vector2 basePosition = anchoredPosition;

        if (centerOnPlayableArea)
        {
            float hudHeight = Mathf.Clamp(topHudHeightAtDesign, 0f, referenceHeight);
            basePosition = new Vector2(0f, -hudHeight * 0.5f);
        }

        return RoundToPixel(basePosition * uiScale);
    }

    float GetTeleportStepInterval()
    {
        float interval = Mathf.Max(0.001f, teleportInterval);
        int moves = Mathf.Max(1, minimumTeleportMoves);

        if (teleportDuration > 0f)
            interval = Mathf.Min(interval, teleportDuration / moves);

        return interval;
    }

    static Vector2 RoundToPixel(Vector2 value)
    {
        return new Vector2(Mathf.Round(value.x), Mathf.Round(value.y));
    }
}
