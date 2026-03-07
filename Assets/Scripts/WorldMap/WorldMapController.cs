using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WorldMapController : MonoBehaviour
{
    [System.Serializable]
    public class StageNode
    {
        public string displayName = "1-1";
        public string sceneName = "Stage_1-1";
        public RectTransform anchor;
        public bool unlocked = true;

        [HideInInspector] public Image runtimeIcon;
    }

    [System.Serializable]
    public class WorldData
    {
        public string worldName = "World 1";
        public GameObject root;
        public List<StageNode> nodes = new List<StageNode>();
        public int defaultNodeIndex = 0;

        [Header("World Music")]
        public AudioClip worldMusic;
        [Range(0f, 1f)] public float worldMusicVolume = 1f;
        public bool loopWorldMusic = true;
    }

    [Header("Input Owner")]
    [SerializeField, Range(1, 4)] int ownerPlayerId = 1;

    [Header("Worlds")]
    [SerializeField] List<WorldData> worlds = new List<WorldData>();
    [SerializeField] int startWorldIndex = 0;

    [Header("Stage Anchor Scaling")]
    [SerializeField] bool scaleStageAnchorsWithSafeFrame = true;
    [SerializeField] int stageAnchorReferenceWidth = 256;
    [SerializeField] int stageAnchorReferenceHeight = 224;
    [SerializeField] bool useIntegerUpscaleForStageAnchors = true;
    [SerializeField] float extraStageAnchorScaleMultiplier = 1f;
    [SerializeField] float minStageAnchorScale = 1f;
    [SerializeField] float maxStageAnchorScale = 20f;

    [Header("Cursor")]
    [SerializeField] RectTransform cursor;
    [SerializeField] RectTransform cursorMovementArea;
    [SerializeField] float cursorMoveSpeedNormalized = 0.25f;
    [SerializeField] float cursorMoveSpeed = 140f;
    [SerializeField] bool clampCursorInsideArea = true;
    [SerializeField] bool snapCursorToDefaultStageOnStart = true;
    [SerializeField] bool snapCursorToDefaultStageOnWorldChange = true;
    [SerializeField] Vector2 baseCursorLogicalSize = new Vector2(16f, 22f);
    [SerializeField] bool preserveCursorAspect = true;
    [SerializeField] float extraCursorScaleMultiplier = 1f;
    [SerializeField] float minCursorScale = 1f;
    [SerializeField] float maxCursorScale = 20f;

    [Header("Stage Detection")]
    [SerializeField] float stageDetectRadius = 18f;
    [SerializeField] bool requireStageInRangeToConfirm = true;
    [SerializeField] bool scaleStageDetectRadiusWithSafeFrame = true;

    [Header("Stage Icons")]
    [SerializeField] Sprite unlockedStageSprite;
    [SerializeField] Sprite lockedStageSprite;
    [SerializeField] Vector2 baseIconLogicalSize = new Vector2(8f, 8f);
    [SerializeField] Vector2 iconOffset = Vector2.zero;
    [SerializeField] bool preserveAspectOnIcons = true;
    [SerializeField] bool createIconsOnStart = true;
    [SerializeField] string runtimeIconObjectName = "_StageIcon";

    [Header("SNES Scaling Reference")]
    [SerializeField] int snesReferenceWidth = 256;
    [SerializeField] int snesReferenceHeight = 224;
    [SerializeField] bool useIntegerUpscaleForIcons = true;
    [SerializeField] float extraIconScaleMultiplier = 1f;
    [SerializeField] float minIconScale = 1f;
    [SerializeField] float maxIconScale = 20f;

    [Header("Stage Icon Colors")]
    [SerializeField] Color unlockedStageColor = Color.white;
    [SerializeField] Color lockedStageColor = Color.white;

    [Header("Fade")]
    [SerializeField] Image fadeImage;
    [SerializeField, Min(0.01f)] float fadeInDuration = 0.35f;
    [SerializeField, Min(0.01f)] float fadeOutDuration = 0.35f;

    [Header("Audio SFX")]
    [SerializeField] AudioClip moveCursorSfx;
    [SerializeField, Range(0f, 1f)] float moveCursorSfxVolume = 1f;
    [SerializeField] AudioClip changeWorldSfx;
    [SerializeField, Range(0f, 1f)] float changeWorldSfxVolume = 1f;
    [SerializeField] AudioClip confirmStageSfx;
    [SerializeField, Range(0f, 1f)] float confirmStageSfxVolume = 1f;
    [SerializeField] AudioClip deniedSfx;
    [SerializeField, Range(0f, 1f)] float deniedSfxVolume = 1f;

    [Header("Optional Back")]
    [SerializeField] bool allowReturnToTitle = false;
    [SerializeField] string titleSceneName = "TitleScreen";

    int currentWorldIndex;
    int hoveredNodeIndex = -1;
    bool transitioning;
    bool wasMovingLastFrame;
    bool authoredStageAnchorsCaptured;

    AudioClip lastPlayedWorldMusic;
    float lastPlayedWorldMusicVolume;
    bool lastPlayedWorldMusicLoop;

    int lastScreenW = -1;
    int lastScreenH = -1;
    float lastCanvasScaleFactor = -1f;
    Rect lastMovementAreaPxRect;
    Rect lastMovementAreaLocalRect;

    readonly Dictionary<string, Vector2> authoredStageAnchorPositions = new Dictionary<string, Vector2>();

    void Start()
    {
        Time.timeScale = 1f;
        GamePauseController.ClearPauseFlag();

        if (cursorMovementArea == null)
            cursorMovementArea = transform as RectTransform;

        currentWorldIndex = Mathf.Clamp(startWorldIndex, 0, Mathf.Max(0, worlds.Count - 1));

        Canvas.ForceUpdateCanvases();

        CaptureAuthoredStageAnchorPositionsOnce();
        ApplyScaledStageAnchorPositions();
        ApplyScaledCursorSize();

        if (createIconsOnStart)
            EnsureAllStageIcons();

        ApplyWorldVisibility();
        UpdateAllStageIcons();

        Canvas.ForceUpdateCanvases();

        if (snapCursorToDefaultStageOnStart)
            SnapCursorToDefaultStage();

        ClampCursorIfNeeded();
        RefreshHoveredStage();

        if (fadeImage != null)
            StartCoroutine(FadeInRoutine());

        PlayMusicForCurrentWorld(forceRestart: true);
    }

    void Update()
    {
        if (transitioning)
            return;

        CheckResolutionOrScaleChanges();

        var input = PlayerInputManager.Instance;
        if (input == null || worlds.Count == 0)
            return;

        if (input.GetDown(ownerPlayerId, PlayerAction.ActionL))
        {
            ChangeWorld(-1);
            return;
        }

        if (input.GetDown(ownerPlayerId, PlayerAction.ActionR))
        {
            ChangeWorld(+1);
            return;
        }

        UpdateFreeCursorMovement(input);

        if (input.GetDown(ownerPlayerId, PlayerAction.ActionA) || input.GetDown(ownerPlayerId, PlayerAction.Start))
        {
            ConfirmCurrentStage();
            return;
        }

        if (allowReturnToTitle && input.GetDown(ownerPlayerId, PlayerAction.ActionB))
            StartCoroutine(LoadSceneRoutine(titleSceneName));
    }

    void UpdateFreeCursorMovement(PlayerInputManager input)
    {
        if (cursor == null || cursorMovementArea == null)
            return;

        float x = 0f;
        float y = 0f;

        if (input.Get(ownerPlayerId, PlayerAction.MoveLeft)) x -= 1f;
        if (input.Get(ownerPlayerId, PlayerAction.MoveRight)) x += 1f;
        if (input.Get(ownerPlayerId, PlayerAction.MoveUp)) y += 1f;
        if (input.Get(ownerPlayerId, PlayerAction.MoveDown)) y -= 1f;

        Vector2 move = new Vector2(x, y);
        bool isMoving = move.sqrMagnitude > 0.0001f;

        if (isMoving)
        {
            move = move.normalized;

            cursor.SetParent(cursorMovementArea, false);
            float speedX = cursorMovementArea.rect.width * cursorMoveSpeedNormalized;
            float speedY = cursorMovementArea.rect.height * cursorMoveSpeedNormalized;
            Vector2 scaledMove = new Vector2(move.x * speedX, move.y * speedY);
            cursor.anchoredPosition += scaledMove * Time.unscaledDeltaTime;

            ClampCursorIfNeeded();
            RefreshHoveredStage();

            if (!wasMovingLastFrame)
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
        }

        wasMovingLastFrame = isMoving;
    }

    void ChangeWorld(int delta)
    {
        if (worlds.Count == 0)
            return;

        currentWorldIndex += delta;

        if (currentWorldIndex < 0)
            currentWorldIndex = worlds.Count - 1;
        else if (currentWorldIndex >= worlds.Count)
            currentWorldIndex = 0;

        ApplyWorldVisibility();
        ApplyScaledStageAnchorPositions();
        UpdateAllStageIcons();
        ApplyScaledCursorSize();

        Canvas.ForceUpdateCanvases();

        if (snapCursorToDefaultStageOnWorldChange)
            SnapCursorToDefaultStage();

        ClampCursorIfNeeded();
        RefreshHoveredStage();
        PlaySfx(changeWorldSfx, changeWorldSfxVolume);
        PlayMusicForCurrentWorld(forceRestart: false);
    }

    void ConfirmCurrentStage()
    {
        var node = GetHoveredNode();

        if (node == null)
            return;

        if (!node.unlocked || string.IsNullOrEmpty(node.sceneName))
        {
            PlaySfx(deniedSfx, deniedSfxVolume);
            return;
        }

        PlaySfx(confirmStageSfx, confirmStageSfxVolume);
        StartCoroutine(LoadSceneRoutine(node.sceneName));
    }

    IEnumerator LoadSceneRoutine(string sceneName)
    {
        if (transitioning)
            yield break;

        transitioning = true;

        if (fadeImage != null)
            yield return FadeOutRoutine();

        SceneManager.LoadScene(sceneName);
    }

    void ApplyWorldVisibility()
    {
        for (int i = 0; i < worlds.Count; i++)
            if (worlds[i].root != null)
                worlds[i].root.SetActive(i == currentWorldIndex);
    }

    void SnapCursorToDefaultStage()
    {
        if (cursor == null || cursorMovementArea == null)
            return;

        int defaultIndex = GetSafeDefaultNodeIndex(currentWorldIndex);
        var node = GetNode(currentWorldIndex, defaultIndex);
        if (node == null || node.anchor == null)
            return;

        cursor.SetParent(cursorMovementArea, false);
        cursor.anchoredPosition = GetAnchorPositionInMovementArea(node.anchor);
        hoveredNodeIndex = defaultIndex;
    }

    void RefreshHoveredStage()
    {
        hoveredNodeIndex = FindNearestNodeIndexToCursor();
    }

    int FindNearestNodeIndexToCursor()
    {
        if (cursor == null || cursorMovementArea == null)
            return -1;

        var world = GetCurrentWorld();
        if (world == null || world.nodes == null || world.nodes.Count == 0)
            return -1;

        Vector2 cursorPos = cursor.anchoredPosition;

        int bestIndex = -1;
        float bestDist = float.MaxValue;
        float detectRadius = GetScaledStageDetectRadius();

        for (int i = 0; i < world.nodes.Count; i++)
        {
            var node = world.nodes[i];
            if (node == null || node.anchor == null)
                continue;

            Vector2 nodePos = GetAnchorPositionInMovementArea(node.anchor);
            float d = Vector2.Distance(cursorPos, nodePos);

            if (d < bestDist)
            {
                bestDist = d;
                bestIndex = i;
            }
        }

        if (requireStageInRangeToConfirm && bestDist > detectRadius)
            return -1;

        return bestIndex;
    }

    Vector2 GetAnchorPositionInMovementArea(RectTransform anchor)
    {
        if (anchor == null || cursorMovementArea == null)
            return Vector2.zero;

        Vector3 worldPos = anchor.TransformPoint(anchor.rect.center);
        Vector3 localPos = cursorMovementArea.InverseTransformPoint(worldPos);
        return new Vector2(localPos.x, localPos.y);
    }

    void ClampCursorIfNeeded()
    {
        if (!clampCursorInsideArea || cursor == null || cursorMovementArea == null)
            return;

        Rect r = cursorMovementArea.rect;
        Vector2 p = cursor.anchoredPosition;

        float halfW = cursor.rect.width * cursor.pivot.x;
        float halfH = cursor.rect.height * cursor.pivot.y;
        float halfWRight = cursor.rect.width * (1f - cursor.pivot.x);
        float halfHUp = cursor.rect.height * (1f - cursor.pivot.y);

        float minX = r.xMin + halfW;
        float maxX = r.xMax - halfWRight;
        float minY = r.yMin + halfH;
        float maxY = r.yMax - halfHUp;

        p.x = Mathf.Clamp(p.x, minX, maxX);
        p.y = Mathf.Clamp(p.y, minY, maxY);

        cursor.anchoredPosition = p;
    }

    void EnsureAllStageIcons()
    {
        for (int w = 0; w < worlds.Count; w++)
        {
            var world = worlds[w];
            if (world == null || world.nodes == null)
                continue;

            for (int n = 0; n < world.nodes.Count; n++)
                EnsureStageIcon(world.nodes[n]);
        }
    }

    void EnsureStageIcon(StageNode node)
    {
        if (node == null || node.anchor == null)
            return;

        if (node.runtimeIcon == null)
        {
            Transform existing = node.anchor.Find(runtimeIconObjectName);
            if (existing != null)
                node.runtimeIcon = existing.GetComponent<Image>();
        }

        if (node.runtimeIcon == null)
        {
            var go = new GameObject(runtimeIconObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(node.anchor, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = iconOffset;
            rt.sizeDelta = GetScaledIconSize();
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            node.runtimeIcon = go.GetComponent<Image>();
            node.runtimeIcon.raycastTarget = false;
            node.runtimeIcon.preserveAspect = preserveAspectOnIcons;
        }
        else
        {
            var rt = node.runtimeIcon.rectTransform;
            rt.SetParent(node.anchor, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = iconOffset;
            rt.sizeDelta = GetScaledIconSize();
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            node.runtimeIcon.raycastTarget = false;
            node.runtimeIcon.preserveAspect = preserveAspectOnIcons;
        }
    }

    void UpdateAllStageIcons()
    {
        for (int w = 0; w < worlds.Count; w++)
        {
            var world = worlds[w];
            if (world == null || world.nodes == null)
                continue;

            for (int n = 0; n < world.nodes.Count; n++)
            {
                var node = world.nodes[n];
                if (node == null || node.anchor == null)
                    continue;

                EnsureStageIcon(node);
                RefreshStageIconVisual(node);
            }
        }
    }

    void RefreshStageIconVisual(StageNode node)
    {
        if (node == null || node.runtimeIcon == null)
            return;

        node.runtimeIcon.sprite = node.unlocked ? unlockedStageSprite : lockedStageSprite;
        node.runtimeIcon.color = node.unlocked ? unlockedStageColor : lockedStageColor;
        node.runtimeIcon.enabled = node.runtimeIcon.sprite != null;

        var rt = node.runtimeIcon.rectTransform;
        rt.anchoredPosition = iconOffset;
        rt.sizeDelta = GetScaledIconSize();
    }

    WorldData GetCurrentWorld()
    {
        if (currentWorldIndex < 0 || currentWorldIndex >= worlds.Count)
            return null;

        return worlds[currentWorldIndex];
    }

    StageNode GetHoveredNode()
    {
        return GetNode(currentWorldIndex, hoveredNodeIndex);
    }

    StageNode GetNode(int worldIndex, int nodeIndex)
    {
        if (worldIndex < 0 || worldIndex >= worlds.Count)
            return null;

        var world = worlds[worldIndex];
        if (world == null || world.nodes == null)
            return null;

        if (nodeIndex < 0 || nodeIndex >= world.nodes.Count)
            return null;

        return world.nodes[nodeIndex];
    }

    int GetSafeDefaultNodeIndex(int worldIndex)
    {
        if (worldIndex < 0 || worldIndex >= worlds.Count)
            return 0;

        var world = worlds[worldIndex];
        if (world == null || world.nodes == null || world.nodes.Count == 0)
            return 0;

        return Mathf.Clamp(world.defaultNodeIndex, 0, world.nodes.Count - 1);
    }

    void PlayMusicForCurrentWorld(bool forceRestart)
    {
        var world = GetCurrentWorld();
        if (world == null || GameMusicController.Instance == null)
            return;

        if (world.worldMusic == null)
        {
            GameMusicController.Instance.StopMusic();
            lastPlayedWorldMusic = null;
            lastPlayedWorldMusicVolume = 0f;
            lastPlayedWorldMusicLoop = false;
            return;
        }

        bool sameClip =
            lastPlayedWorldMusic == world.worldMusic &&
            Mathf.Approximately(lastPlayedWorldMusicVolume, world.worldMusicVolume) &&
            lastPlayedWorldMusicLoop == world.loopWorldMusic;

        if (!forceRestart && sameClip)
            return;

        GameMusicController.Instance.PlayMusic(world.worldMusic, world.worldMusicVolume, world.loopWorldMusic);

        lastPlayedWorldMusic = world.worldMusic;
        lastPlayedWorldMusicVolume = world.worldMusicVolume;
        lastPlayedWorldMusicLoop = world.loopWorldMusic;
    }

    void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null || GameMusicController.Instance == null)
            return;

        GameMusicController.Instance.PlaySfx(clip, volume);
    }

    IEnumerator FadeInRoutine()
    {
        if (fadeImage == null)
            yield break;

        fadeImage.gameObject.SetActive(true);
        SetFadeAlpha(1f);

        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(t / fadeInDuration);
            SetFadeAlpha(a);
            yield return null;
        }

        SetFadeAlpha(0f);
        fadeImage.gameObject.SetActive(false);
    }

    IEnumerator FadeOutRoutine()
    {
        if (fadeImage == null)
            yield break;

        fadeImage.gameObject.SetActive(true);
        SetFadeAlpha(0f);

        float t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / fadeOutDuration);
            SetFadeAlpha(a);
            yield return null;
        }

        SetFadeAlpha(1f);
    }

    void SetFadeAlpha(float a)
    {
        if (fadeImage == null)
            return;

        var c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;
    }

    Canvas GetRootCanvas()
    {
        return GetComponentInParent<Canvas>();
    }

    void CaptureAuthoredStageAnchorPositionsOnce()
    {
        if (authoredStageAnchorsCaptured)
            return;

        authoredStageAnchorPositions.Clear();

        for (int w = 0; w < worlds.Count; w++)
        {
            var world = worlds[w];
            if (world == null || world.nodes == null)
                continue;

            for (int n = 0; n < world.nodes.Count; n++)
            {
                var node = world.nodes[n];
                if (node == null || node.anchor == null)
                    continue;

                string key = GetAnchorKey(w, n);
                authoredStageAnchorPositions[key] = node.anchor.anchoredPosition;
            }
        }

        authoredStageAnchorsCaptured = true;
    }

    void ApplyScaledStageAnchorPositions()
    {
        if (!scaleStageAnchorsWithSafeFrame)
            return;

        CaptureAuthoredStageAnchorPositionsOnce();

        float anchorScale = GetStageAnchorScale();

        for (int w = 0; w < worlds.Count; w++)
        {
            var world = worlds[w];
            if (world == null || world.nodes == null)
                continue;

            for (int n = 0; n < world.nodes.Count; n++)
            {
                var node = world.nodes[n];
                if (node == null || node.anchor == null)
                    continue;

                string key = GetAnchorKey(w, n);
                if (!authoredStageAnchorPositions.TryGetValue(key, out Vector2 authoredLogical))
                    continue;

                node.anchor.anchoredPosition = authoredLogical * anchorScale;
            }
        }
    }

    float GetStageAnchorScale()
    {
        if (cursorMovementArea == null)
            return 1f;

        var canvas = GetRootCanvas();
        if (canvas == null)
            return 1f;

        Rect safePx = RectTransformUtility.PixelAdjustRect(cursorMovementArea, canvas);

        float sx = safePx.width / Mathf.Max(1f, stageAnchorReferenceWidth);
        float sy = safePx.height / Mathf.Max(1f, stageAnchorReferenceHeight);

        float rawScale = Mathf.Min(sx, sy);
        float usedScale = useIntegerUpscaleForStageAnchors ? Mathf.Floor(rawScale) : rawScale;

        if (usedScale < 1f)
            usedScale = 1f;

        usedScale *= Mathf.Max(0.01f, extraStageAnchorScaleMultiplier);
        usedScale = Mathf.Clamp(usedScale, minStageAnchorScale, maxStageAnchorScale);

        return usedScale;
    }

    float GetScaledStageDetectRadius()
    {
        if (!scaleStageDetectRadiusWithSafeFrame)
            return stageDetectRadius;

        return stageDetectRadius * GetStageAnchorScale();
    }

    string GetAnchorKey(int worldIndex, int nodeIndex)
    {
        return $"{worldIndex}:{nodeIndex}";
    }

    void CheckResolutionOrScaleChanges()
    {
        var canvas = GetRootCanvas();
        float scaleFactor = canvas != null ? canvas.scaleFactor : -1f;
        Rect movementAreaPx = canvas != null && cursorMovementArea != null
            ? RectTransformUtility.PixelAdjustRect(cursorMovementArea, canvas)
            : default;
        Rect movementAreaLocal = cursorMovementArea != null ? cursorMovementArea.rect : default;

        bool changed =
            Screen.width != lastScreenW ||
            Screen.height != lastScreenH ||
            !Mathf.Approximately(scaleFactor, lastCanvasScaleFactor) ||
            movementAreaPx.width != lastMovementAreaPxRect.width ||
            movementAreaPx.height != lastMovementAreaPxRect.height ||
            movementAreaPx.x != lastMovementAreaPxRect.x ||
            movementAreaPx.y != lastMovementAreaPxRect.y ||
            movementAreaLocal.width != lastMovementAreaLocalRect.width ||
            movementAreaLocal.height != lastMovementAreaLocalRect.height ||
            movementAreaLocal.x != lastMovementAreaLocalRect.x ||
            movementAreaLocal.y != lastMovementAreaLocalRect.y;

        if (!changed)
            return;

        lastScreenW = Screen.width;
        lastScreenH = Screen.height;
        lastCanvasScaleFactor = scaleFactor;
        lastMovementAreaPxRect = movementAreaPx;
        lastMovementAreaLocalRect = movementAreaLocal;

        Canvas.ForceUpdateCanvases();
        ApplyScaledStageAnchorPositions();
        ApplyScaledCursorSize();
        UpdateAllStageIcons();

        if (hoveredNodeIndex >= 0)
        {
            var hovered = GetHoveredNode();
            if (hovered != null && hovered.anchor != null && cursor != null)
                cursor.anchoredPosition = GetAnchorPositionInMovementArea(hovered.anchor);
        }
    }

    Vector2 GetScaledIconSize()
    {
        if (cursorMovementArea == null)
            return baseIconLogicalSize;

        var canvas = GetRootCanvas();
        if (canvas == null)
            return baseIconLogicalSize;

        Rect safePx = RectTransformUtility.PixelAdjustRect(cursorMovementArea, canvas);

        float sx = safePx.width / Mathf.Max(1f, snesReferenceWidth);
        float sy = safePx.height / Mathf.Max(1f, snesReferenceHeight);

        float rawScale = Mathf.Min(sx, sy);
        float usedScale = useIntegerUpscaleForIcons ? Mathf.Floor(rawScale) : rawScale;

        if (usedScale < 1f)
            usedScale = 1f;

        usedScale *= Mathf.Max(0.01f, extraIconScaleMultiplier);
        usedScale = Mathf.Clamp(usedScale, minIconScale, maxIconScale);

        return baseIconLogicalSize * usedScale;
    }

    Vector2 GetScaledCursorSize()
    {
        if (cursorMovementArea == null)
            return baseCursorLogicalSize;

        var canvas = GetRootCanvas();
        if (canvas == null)
            return baseCursorLogicalSize;

        Rect safePx = RectTransformUtility.PixelAdjustRect(cursorMovementArea, canvas);

        float sx = safePx.width / Mathf.Max(1f, snesReferenceWidth);
        float sy = safePx.height / Mathf.Max(1f, snesReferenceHeight);

        float rawScale = Mathf.Min(sx, sy);
        float usedScale = useIntegerUpscaleForIcons ? Mathf.Floor(rawScale) : rawScale;

        if (usedScale < 1f)
            usedScale = 1f;

        usedScale *= Mathf.Max(0.01f, extraCursorScaleMultiplier);
        usedScale = Mathf.Clamp(usedScale, minCursorScale, maxCursorScale);

        return baseCursorLogicalSize * usedScale;
    }

    void ApplyScaledCursorSize()
    {
        if (cursor == null)
            return;

        cursor.sizeDelta = GetScaledCursorSize();

        var img = cursor.GetComponent<Image>();
        if (img != null)
            img.preserveAspect = preserveCursorAspect;
    }
}