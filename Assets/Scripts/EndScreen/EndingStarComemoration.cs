using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EndingStarComemoration : MonoBehaviour
{
    [Header("References")]
    [SerializeField] RectTransform spawnArea;
    [SerializeField] RectTransform starTemplate;
    [SerializeField] EndingScreenController endingScreenController;

    [Header("Eligibility")]
    [SerializeField] bool requireTrueCompletion = true;

    [Header("Spawn")]
    [SerializeField] bool playOnEnable = false;
    [SerializeField] int maxAliveStars = 18;
    [SerializeField] float spawnIntervalMin = 0.14f;
    [SerializeField] float spawnIntervalMax = 0.28f;
    [SerializeField] float spawnPaddingX = 18f;
    [SerializeField] float spawnOffsetY = 20f;

    [Header("Movement")]
    [SerializeField] float fallSpeedMin = 52f;
    [SerializeField] float fallSpeedMax = 96f;
    [SerializeField] float swayAmplitudeMin = 4f;
    [SerializeField] float swayAmplitudeMax = 14f;
    [SerializeField] float swayFrequencyMin = 0.55f;
    [SerializeField] float swayFrequencyMax = 1.15f;
    [SerializeField] float driftXMin = -4f;
    [SerializeField] float driftXMax = 4f;
    [SerializeField] float rotationSpeedMin = -18f;
    [SerializeField] float rotationSpeedMax = 18f;

    [Header("Scale")]
    [SerializeField] float scaleMin = 0.38f;
    [SerializeField] float scaleMax = 0.72f;

    [Header("Alpha")]
    [SerializeField, Range(0f, 1f)] float maxAlphaMin = 0.28f;
    [SerializeField, Range(0f, 1f)] float maxAlphaMax = 0.58f;

    [Header("Fade")]
    [SerializeField] bool fadeIn = true;
    [SerializeField] float fadeInDuration = 0.22f;
    [SerializeField] bool fadeOutNearBottom = true;
    [SerializeField] float fadeOutBottomDistance = 150f;

    [Header("Sprite Animation")]
    [SerializeField] bool animateStars = true;
    [SerializeField] Sprite idleSprite;
    [SerializeField] Sprite[] animationFrames;
    [SerializeField] float animationFrameTime = 0.10f;
    [SerializeField] bool loopAnimation = true;
    [SerializeField] bool randomizeStartFrame = true;

    readonly List<StarRuntime> activeStars = new();
    Coroutine spawnRoutine;
    bool playing;
    int spawnedCount;

    sealed class StarRuntime
    {
        public int Id;
        public RectTransform Rect;
        public Image Image;
        public CanvasGroup CanvasGroup;
        public float BaseX;
        public float DriftX;
        public float Y;
        public float FallSpeed;
        public float SwayAmplitude;
        public float SwayFrequency;
        public float SwayPhase;
        public float Rotation;
        public float RotationSpeed;
        public float Age;
        public float Scale;
        public float MaxAlpha;

        public Sprite[] AnimationFrames;
        public float AnimationFrameTime;
        public float AnimationTimer;
        public int AnimationFrameIndex;
    }

    void Awake()
    {
        if (spawnArea == null)
            spawnArea = transform as RectTransform;

        if (endingScreenController == null)
            endingScreenController = GetComponentInParent<EndingScreenController>(true);

        if (starTemplate != null)
            starTemplate.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        if (playOnEnable)
            PlayIfEligible();
    }

    void OnDisable()
    {
        StopAndClear();
    }

    void Update()
    {
        if (!playing)
            return;

        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f)
            return;

        Rect rect = GetSpawnRect();
        float bottomY = rect.yMin - 60f;

        for (int i = activeStars.Count - 1; i >= 0; i--)
        {
            StarRuntime star = activeStars[i];
            if (star == null || star.Rect == null)
            {
                activeStars.RemoveAt(i);
                continue;
            }

            star.Age += dt;
            star.Y -= star.FallSpeed * dt;
            star.Rotation += star.RotationSpeed * dt;

            float sway = Mathf.Sin((star.Age * star.SwayFrequency * Mathf.PI * 2f) + star.SwayPhase) * star.SwayAmplitude;
            float x = star.BaseX + sway + (star.DriftX * star.Age);

            star.Rect.anchoredPosition = new Vector2(x, star.Y);
            star.Rect.localRotation = Quaternion.Euler(0f, 0f, star.Rotation);
            star.Rect.localScale = Vector3.one * star.Scale;

            UpdateStarAnimation(star, dt);

            if (fadeOutNearBottom && star.CanvasGroup != null)
            {
                float distToBottom = star.Y - rect.yMin;
                if (distToBottom <= fadeOutBottomDistance)
                    star.CanvasGroup.alpha = Mathf.Clamp01(distToBottom / Mathf.Max(1f, fadeOutBottomDistance)) * star.MaxAlpha;
                else if (!fadeIn || star.Age >= fadeInDuration)
                    star.CanvasGroup.alpha = star.MaxAlpha;
            }

            if (star.Y < bottomY)
            {
                Destroy(star.Rect.gameObject);
                activeStars.RemoveAt(i);
            }
        }

        while (activeStars.Count > maxAliveStars)
        {
            int last = activeStars.Count - 1;
            if (activeStars[last]?.Rect != null)
                Destroy(activeStars[last].Rect.gameObject);

            activeStars.RemoveAt(last);
        }
    }

    void UpdateStarAnimation(StarRuntime star, float dt)
    {
        if (star == null || star.Image == null)
            return;

        if (!animateStars)
            return;

        if (star.AnimationFrames == null || star.AnimationFrames.Length == 0)
            return;

        if (star.AnimationFrameTime <= 0f)
            return;

        star.AnimationTimer += dt;

        while (star.AnimationTimer >= star.AnimationFrameTime)
        {
            star.AnimationTimer -= star.AnimationFrameTime;
            star.AnimationFrameIndex++;

            if (loopAnimation)
                star.AnimationFrameIndex %= star.AnimationFrames.Length;
            else
                star.AnimationFrameIndex = Mathf.Min(star.AnimationFrameIndex, star.AnimationFrames.Length - 1);

            Sprite next = star.AnimationFrames[star.AnimationFrameIndex];
            if (next != null)
                star.Image.sprite = next;
        }
    }

    public void PlayIfEligible()
    {
        if (requireTrueCompletion && !ShouldPlayForCurrentSave())
        {
            StopAndClear();
            return;
        }

        if (spawnArea == null || starTemplate == null)
            return;

        if (playing)
            return;

        playing = true;
        spawnedCount = 0;
        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    public void StopAndClear()
    {
        playing = false;

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        for (int i = activeStars.Count - 1; i >= 0; i--)
        {
            if (activeStars[i]?.Rect != null)
                Destroy(activeStars[i].Rect.gameObject);
        }

        activeStars.Clear();
    }

    IEnumerator SpawnLoop()
    {
        while (playing)
        {
            if (activeStars.Count < maxAliveStars)
                SpawnOne();

            float wait = UnityEngine.Random.Range(spawnIntervalMin, spawnIntervalMax);
            yield return new WaitForSecondsRealtime(wait);
        }
    }

    void SpawnOne()
    {
        if (starTemplate == null || spawnArea == null)
            return;

        Rect rect = GetSpawnRect();

        float xMin = rect.xMin + spawnPaddingX;
        float xMax = rect.xMax - spawnPaddingX;

        if (xMax < xMin)
            return;

        float spawnX = UnityEngine.Random.Range(xMin, xMax);
        float spawnY = rect.yMax + spawnOffsetY;
        float scale = UnityEngine.Random.Range(scaleMin, scaleMax);
        float maxAlpha = UnityEngine.Random.Range(maxAlphaMin, maxAlphaMax);

        RectTransform clone = Instantiate(starTemplate, spawnArea);
        clone.gameObject.SetActive(true);
        clone.name = $"{starTemplate.name}_Runtime_{spawnedCount + 1}";
        clone.SetAsLastSibling();

        clone.anchorMin = new Vector2(0.5f, 0.5f);
        clone.anchorMax = new Vector2(0.5f, 0.5f);
        clone.pivot = new Vector2(0.5f, 0.5f);
        clone.anchoredPosition = new Vector2(spawnX, spawnY);
        clone.localScale = Vector3.one * scale;
        clone.localRotation = Quaternion.identity;
        clone.sizeDelta = new Vector2(48f, 48f);

        Image img = clone.GetComponent<Image>();
        if (img == null)
            img = clone.GetComponentInChildren<Image>(true);

        CanvasGroup cg = clone.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = clone.gameObject.AddComponent<CanvasGroup>();

        if (img != null)
            img.raycastTarget = false;

        Sprite[] frames = null;
        if (animateStars && animationFrames != null && animationFrames.Length > 0)
            frames = animationFrames;

        int startFrame = 0;
        if (frames != null && frames.Length > 0 && randomizeStartFrame)
            startFrame = UnityEngine.Random.Range(0, frames.Length);

        if (img != null)
        {
            if (frames != null && frames.Length > 0 && frames[startFrame] != null)
                img.sprite = frames[startFrame];
            else if (idleSprite != null)
                img.sprite = idleSprite;

            Color c = img.color;
            c.a = 1f;
            img.color = c;
        }

        if (fadeIn && cg != null)
            cg.alpha = 0f;
        else if (cg != null)
            cg.alpha = maxAlpha;

        spawnedCount++;

        StarRuntime star = new StarRuntime
        {
            Id = spawnedCount,
            Rect = clone,
            Image = img,
            CanvasGroup = cg,
            BaseX = spawnX,
            DriftX = UnityEngine.Random.Range(driftXMin, driftXMax),
            Y = spawnY,
            FallSpeed = UnityEngine.Random.Range(fallSpeedMin, fallSpeedMax),
            SwayAmplitude = UnityEngine.Random.Range(swayAmplitudeMin, swayAmplitudeMax),
            SwayFrequency = UnityEngine.Random.Range(swayFrequencyMin, swayFrequencyMax),
            SwayPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
            Rotation = UnityEngine.Random.Range(0f, 360f),
            RotationSpeed = UnityEngine.Random.Range(rotationSpeedMin, rotationSpeedMax),
            Age = 0f,
            Scale = scale,
            MaxAlpha = maxAlpha,
            AnimationFrames = frames,
            AnimationFrameTime = Mathf.Max(0.01f, animationFrameTime),
            AnimationTimer = 0f,
            AnimationFrameIndex = startFrame
        };

        activeStars.Add(star);

        if (fadeIn && cg != null)
            StartCoroutine(FadeInStar(cg, star.MaxAlpha));
    }

    IEnumerator FadeInStar(CanvasGroup cg, float targetAlpha)
    {
        if (cg == null)
            yield break;

        float t = 0f;
        float duration = Mathf.Max(0.01f, fadeInDuration);

        while (t < duration && cg != null)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(0f, targetAlpha, Mathf.Clamp01(t / duration));
            yield return null;
        }

        if (cg != null)
            cg.alpha = targetAlpha;
    }

    Rect GetSpawnRect()
    {
        if (spawnArea == null)
            return new Rect(-960f, -540f, 1920f, 1080f);

        return spawnArea.rect;
    }

    bool ShouldPlayForCurrentSave()
    {
        int totalStages = 0;
        int clearedStages = 0;
        int perfectStages = 0;

        var slot = SaveSystem.ActiveSlot;
        if (slot != null)
        {
            totalStages = slot.stageOrder != null ? slot.stageOrder.Count : 0;
            clearedStages = slot.clearedStages != null ? slot.clearedStages.Count : 0;
            perfectStages = slot.perfectStages != null ? slot.perfectStages.Count : 0;
        }

        int percent = ComputeCompletionPercent(totalStages, clearedStages, perfectStages);

        int totalBombers = Enum.GetValues(typeof(BomberSkin)).Length;
        int unlockedBombers = CountUnlockedBombers();

        bool allBombersUnlocked = totalBombers > 0 && unlockedBombers >= totalBombers;
        bool eligible = percent >= 200 && allBombersUnlocked;

        return eligible;
    }

    int ComputeCompletionPercent(int totalStages, int clearedCount, int perfectCount)
    {
        if (totalStages <= 0)
            return 0;

        float clearedPercent = (Mathf.Clamp(clearedCount, 0, totalStages) / (float)totalStages) * 100f;
        float perfectPercent = (Mathf.Clamp(perfectCount, 0, totalStages) / (float)totalStages) * 100f;

        return Mathf.RoundToInt(clearedPercent + perfectPercent);
    }

    int CountUnlockedBombers()
    {
        if (SaveSystem.Data == null || SaveSystem.Data.unlockedSkins == null)
            return 0;

        HashSet<string> uniqueUnlocked = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < SaveSystem.Data.unlockedSkins.Count; i++)
        {
            string skinName = SaveSystem.Data.unlockedSkins[i];
            if (string.IsNullOrWhiteSpace(skinName))
                continue;

            if (Enum.TryParse(skinName, true, out BomberSkin parsedSkin))
                uniqueUnlocked.Add(parsedSkin.ToString());
        }

        return uniqueUnlocked.Count;
    }
}