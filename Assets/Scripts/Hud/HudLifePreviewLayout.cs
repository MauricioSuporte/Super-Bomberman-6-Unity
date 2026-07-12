using Assets.Scripts.SaveSystem;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public sealed class HudLifePreviewLayout : MonoBehaviour
{
    const int HardcoreIconIndex = 10;

    [Header("Camera Logical Size (SNES pixels)")]
    [SerializeField] private Vector2 frameSize = new(256f, 224f);

    [Header("HUD And Counter Logical Size")]
    [SerializeField] private float hudHeight = 23f;
    [SerializeField] private Vector2 counterFrameSize = new(21f, 12f);
    [SerializeField] private Vector2 counterIconSize = new(9f, 9f);
    [SerializeField] private float overlapHudPixels = 1f;

    [Header("Sprites")]
    [SerializeField] private Sprite counterFrameSprite;
    [SerializeField] private Sprite[] lifeIconSprites = new Sprite[11];

    RectTransform rectTransform;
    Image frameImage;
    Image iconImage;
    bool attachedToSafeFrame;

    void Awake()
    {
        rectTransform = (RectTransform)transform;
        frameImage = GetComponent<Image>();
        CreateIconImage();
        AttachToSafeFrame();
        Refresh();
    }

    void LateUpdate()
    {
        using var performanceSample = BattleModePerformanceMarkers.HudLifePreviewLateUpdate.Auto();

        Refresh();
    }

    void Refresh()
    {
        AttachToSafeFrame();
        ApplyFrameLayout();
        ApplyIconLayout();
        ApplyVisualState();
    }

    void AttachToSafeFrame()
    {
        if (attachedToSafeFrame)
            return;

        RectTransform hudRoot = transform.parent as RectTransform;
        RectTransform safeFrame = hudRoot != null ? hudRoot.parent as RectTransform : null;

        if (safeFrame == null)
            return;

        rectTransform.SetParent(safeFrame, false);
        rectTransform.SetAsLastSibling();
        attachedToSafeFrame = true;
    }

    void ApplyFrameLayout()
    {
        if (!attachedToSafeFrame || frameSize.x <= 0f || frameSize.y <= 0f)
            return;

        float left = (frameSize.x - counterFrameSize.x) * 0.5f;
        float right = left + counterFrameSize.x;
        float top = frameSize.y - hudHeight + overlapHudPixels;
        float bottom = top - counterFrameSize.y;

        Vector2 anchorMin = new(left / frameSize.x, bottom / frameSize.y);
        Vector2 anchorMax = new(right / frameSize.x, top / frameSize.y);

        if (rectTransform.anchorMin != anchorMin)
            rectTransform.anchorMin = anchorMin;
        if (rectTransform.anchorMax != anchorMax)
            rectTransform.anchorMax = anchorMax;
        if (rectTransform.offsetMin != Vector2.zero)
            rectTransform.offsetMin = Vector2.zero;
        if (rectTransform.offsetMax != Vector2.zero)
            rectTransform.offsetMax = Vector2.zero;
        if (rectTransform.localScale != Vector3.one)
            rectTransform.localScale = Vector3.one;
    }

    void ApplyIconLayout()
    {
        if (iconImage == null || counterFrameSize.x <= 0f || counterFrameSize.y <= 0f)
            return;

        RectTransform iconRect = iconImage.rectTransform;
        float left = (counterFrameSize.x - counterIconSize.x) * 0.5f;
        float bottom = (counterFrameSize.y - counterIconSize.y) * 0.5f;

        Vector2 anchorMin = new(left / counterFrameSize.x, bottom / counterFrameSize.y);
        Vector2 anchorMax = new(
            (left + counterIconSize.x) / counterFrameSize.x,
            (bottom + counterIconSize.y) / counterFrameSize.y);

        if (iconRect.anchorMin != anchorMin)
            iconRect.anchorMin = anchorMin;
        if (iconRect.anchorMax != anchorMax)
            iconRect.anchorMax = anchorMax;
        if (iconRect.offsetMin != Vector2.zero)
            iconRect.offsetMin = Vector2.zero;
        if (iconRect.offsetMax != Vector2.zero)
            iconRect.offsetMax = Vector2.zero;
        if (iconRect.localScale != Vector3.one)
            iconRect.localScale = Vector3.one;
    }

    void ApplyVisualState()
    {
        NormalGameDifficulty difficulty = SaveSystem.GetActiveNormalGameDifficulty();
        bool visible = !BossRushSession.IsActive &&
            (difficulty == NormalGameDifficulty.Hard || difficulty == NormalGameDifficulty.Hardcore);

        frameImage.sprite = counterFrameSprite;
        frameImage.enabled = visible && counterFrameSprite != null;

        int hardRemainingLives = GameSession.Instance != null
            ? GameSession.Instance.HardNormalGameRemainingLives
            : GameSession.HardNormalGameStartingLives;
        int iconIndex = difficulty == NormalGameDifficulty.Hardcore
            ? HardcoreIconIndex
            : hardRemainingLives;

        Sprite iconSprite = lifeIconSprites != null && iconIndex < lifeIconSprites.Length
            ? lifeIconSprites[iconIndex]
            : null;

        iconImage.sprite = iconSprite;
        iconImage.enabled = visible && iconSprite != null;
    }

    void CreateIconImage()
    {
        GameObject icon = new("LifeCountIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        icon.layer = gameObject.layer;
        icon.transform.SetParent(transform, false);

        iconImage = icon.GetComponent<Image>();
        iconImage.raycastTarget = false;
        iconImage.preserveAspect = false;
    }
}
