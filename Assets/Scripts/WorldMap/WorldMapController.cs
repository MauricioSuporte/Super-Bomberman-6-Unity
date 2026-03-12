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

        [Header("World Focus")]
        public Vector2 cameraPosition = new Vector2(-1f, -1f);
    }

    [Header("World Camera Focus")]
    [SerializeField] Camera targetWorldCamera;
    [SerializeField] bool applyWorldCameraPosition = true;

    [Header("Input Owner")]
    [SerializeField, Range(1, 4)] int ownerPlayerId = 1;

    [Header("Worlds")]
    [SerializeField] List<WorldData> worlds = new List<WorldData>();
    [SerializeField] int startWorldIndex = 0;

    [Header("Optional World Background Scroll")]
    [SerializeField] PixelPerfectScrollingSpriteRenderer world2BackgroundScroller;
    [SerializeField] int world2ScrollerWorldIndex = 1;

    [Header("Stage Anchor Scaling")]
    [SerializeField] bool scaleStageAnchorsWithSafeFrame = true;
    [SerializeField] int stageAnchorReferenceWidth = 256;
    [SerializeField] int stageAnchorReferenceHeight = 224;
    [SerializeField] bool useIntegerUpscaleForStageAnchors = true;
    [SerializeField] float extraStageAnchorScaleMultiplier = 1f;
    [SerializeField] float minStageAnchorScale = 1f;
    [SerializeField] float maxStageAnchorScale = 20f;

    [Header("Cursor Logic")]
    [SerializeField] RectTransform cursorMovementArea;
    [SerializeField] float cursorMoveSpeedNormalized = 0.25f;
    [SerializeField] float cursorMoveSpeed = 140f;
    [SerializeField] bool clampCursorInsideArea = true;
    [SerializeField] bool snapCursorToDefaultStageOnStart = true;
    [SerializeField] bool snapCursorToDefaultStageOnWorldChange = true;
    [SerializeField] Vector2 baseCursorLogicalSize = new Vector2(20f, 29f);
    [SerializeField] bool preserveCursorAspect = true;
    [SerializeField] float extraCursorScaleMultiplier = 1f;
    [SerializeField] float minCursorScale = 1f;
    [SerializeField] float maxCursorScale = 20f;

    [Header("Cursor Visual")]
    [SerializeField] bool useAnimatedCursorVisuals = true;
    [SerializeField] RectTransform cursorVisualRoot;
    [SerializeField] GameObject movingCursorVisual;
    [SerializeField] GameObject selectedCursorVisual;
    [SerializeField] Vector2 cursorVisualBaseSize = new Vector2(20f, 29f);
    [SerializeField] bool scaleCursorVisualWithLogicalSize = true;
    [SerializeField, Min(0.01f)] float selectedTransitionDuration = 1f;

    [Header("Stage Detection")]
    [SerializeField] float stageDetectRadius = 18f;
    [SerializeField] bool requireStageInRangeToConfirm = true;
    [SerializeField] bool scaleStageDetectRadiusWithSafeFrame = true;

    [Header("Stage Icons")]
    [SerializeField] Sprite lockedStageSprite;
    [SerializeField] Sprite availableStageSprite;
    [SerializeField] Sprite clearedStageSprite;
    [SerializeField] Sprite perfectStageSprite;
    [SerializeField] Vector2 baseIconLogicalSize = new Vector2(8f, 8f);
    [SerializeField] Vector2 iconOffset = Vector2.zero;
    [SerializeField] bool preserveAspectOnIcons = true;
    [SerializeField] bool createIconsOnStart = true;
    [SerializeField] string runtimeIconObjectName = "_StageIcon";

    [Header("Cleared Stage Spin")]
    [SerializeField] bool animateClearedStageIcons = true;
    [SerializeField, Min(0.01f)] float clearedStageSpinInterval = 2f;
    [SerializeField, Min(1f)] float clearedStageSpinStepMilliseconds = 80f;

    [Header("SNES Scaling Reference")]
    [SerializeField] int snesReferenceWidth = 256;
    [SerializeField] int snesReferenceHeight = 224;
    [SerializeField] bool useIntegerUpscaleForIcons = true;
    [SerializeField] float extraIconScaleMultiplier = 1f;
    [SerializeField] float minIconScale = 1f;
    [SerializeField] float maxIconScale = 20f;

    [Header("Stage Icon Colors")]
    [SerializeField] Color lockedStageColor = Color.white;
    [SerializeField] Color availableStageColor = Color.white;
    [SerializeField] Color clearedStageColor = Color.white;
    [SerializeField] Color perfectStageColor = Color.white;

    [Header("Fade")]
    [SerializeField] Image fadeImage;
    [SerializeField, Min(0.01f)] float fadeInDuration = 0.5f;
    [SerializeField, Min(0.01f)] float worldChangeFadeOutDuration = 0.5f;
    [SerializeField, Min(0.01f)] float worldChangeFadeInDuration = 0.5f;

    [Header("Audio SFX")]
    [SerializeField] AudioClip confirmStageSfx;
    [SerializeField, Range(0f, 1f)] float confirmStageSfxVolume = 1f;
    [SerializeField] AudioClip deniedSfx;
    [SerializeField, Range(0f, 1f)] float deniedSfxVolume = 1f;
    [SerializeField] AudioClip backSceneSfx;
    [SerializeField, Range(0f, 1f)] float backSceneSfxVolume = 1f;

    [Header("Back Scene")]
    private readonly string backSceneName = "SaveFileMenu";

    [Header("Top Left Label")]
    [SerializeField] Text worldStageLabel;
    [SerializeField] string worldLabelPrefix = "WORLD ";
    [SerializeField] string stageSeparator = " - ";

    [SerializeField] int worldStageLabelReferenceWidth = 256;
    [SerializeField] int worldStageLabelReferenceHeight = 224;
    [SerializeField] bool useIntegerUpscaleForWorldStageLabel = true;

    [SerializeField] int baseWorldStageLabelFontSize = 8;
    [SerializeField] Vector2 baseWorldStageLabelAnchoredPosition = new Vector2(3f, -3f);
    [SerializeField] Vector2 baseWorldStageLabelSize = new Vector2(80f, 12f);

    [SerializeField] float extraWorldStageLabelScaleMultiplier = 1f;
    [SerializeField] float minWorldStageLabelScale = 1f;
    [SerializeField] float maxWorldStageLabelScale = 20f;

    int currentWorldIndex;
    int hoveredNodeIndex = -1;
    bool transitioning;
    bool wasMovingLastFrame;
    bool authoredStageAnchorsCaptured;
    bool playingSelectedAnimation;

    AudioClip lastPlayedWorldMusic;
    float lastPlayedWorldMusicVolume;
    bool lastPlayedWorldMusicLoop;

    int lastScreenW = -1;
    int lastScreenH = -1;
    float lastCanvasScaleFactor = -1f;
    Rect lastMovementAreaPxRect;
    Rect lastMovementAreaLocalRect;

    Vector2 cursorLocalPosition;

    readonly Dictionary<string, Vector2> authoredStageAnchorPositions = new Dictionary<string, Vector2>();
    readonly Dictionary<Image, Coroutine> clearedStageSpinRoutines = new Dictionary<Image, Coroutine>();

    void Awake()
    {
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.transform.SetAsLastSibling();
            SetFadeAlpha(1f);
        }
    }

    void Start()
    {
        Time.timeScale = 1f;
        GamePauseController.ClearPauseFlag();

        if (cursorMovementArea == null)
            cursorMovementArea = transform as RectTransform;

        RegisterStageOrderInProgress();
        currentWorldIndex = GetInitialWorldIndexFromProgress();
        ApplyCurrentWorldCameraPosition();

        Canvas.ForceUpdateCanvases();

        CaptureAuthoredStageAnchorPositionsOnce();
        ApplyScaledStageAnchorPositions();
        ApplyScaledCursorSize();
        ApplyScaledWorldStageLabelLayout();

        ApplyUnlockedStagesFromProgress();

        if (createIconsOnStart)
            EnsureAllStageIcons();

        ApplyWorldVisibility();
        RefreshOptionalWorldScrollers();
        UpdateAllStageIcons();

        Canvas.ForceUpdateCanvases();

        if (snapCursorToDefaultStageOnStart)
            SnapCursorToDefaultStage();

        ClampCursorIfNeeded();
        RefreshHoveredStage();
        RefreshWorldStageLabel();
        RefreshCursorVisualState(false, true);
        ApplyCursorVisualTransform();

        PlayMusicForCurrentWorld(forceRestart: true);

        if (fadeImage != null)
            StartCoroutine(FadeInRoutine(fadeInDuration));

        RefreshClearedStageSpinAnimations();
    }

    void LateUpdate()
    {
        ApplyCursorVisualTransform();
    }

    void Update()
    {
        if (transitioning)
            return;

        CheckResolutionOrScaleChanges();
        RefreshOptionalWorldScrollers();

        var input = PlayerInputManager.Instance;
        if (input == null || worlds.Count == 0)
            return;

        if (input.GetDown(ownerPlayerId, PlayerAction.ActionL))
        {
            if (CanChangeWorld(-1))
                StartCoroutine(ChangeWorldRoutine(-1));
            return;
        }

        if (input.GetDown(ownerPlayerId, PlayerAction.ActionR))
        {
            if (CanChangeWorld(+1))
                StartCoroutine(ChangeWorldRoutine(+1));
            return;
        }

        UpdateFreeCursorMovement(input);

        if (input.GetDown(ownerPlayerId, PlayerAction.ActionA) || input.GetDown(ownerPlayerId, PlayerAction.Start))
        {
            ConfirmCurrentStage();
            return;
        }

        if (input.GetDown(ownerPlayerId, PlayerAction.ActionB))
            StartCoroutine(LoadSceneRoutine(backSceneName, backSceneSfx, backSceneSfxVolume));
    }

    void UpdateFreeCursorMovement(PlayerInputManager input)
    {
        if (cursorMovementArea == null || playingSelectedAnimation)
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

            float speedX = cursorMovementArea.rect.width * cursorMoveSpeedNormalized;
            float speedY = cursorMovementArea.rect.height * cursorMoveSpeedNormalized;
            Vector2 scaledMove = new Vector2(move.x * speedX, move.y * speedY);
            cursorLocalPosition += scaledMove * Time.unscaledDeltaTime;

            ClampCursorIfNeeded();
            RefreshHoveredStage();
        }

        wasMovingLastFrame = isMoving;
        RefreshCursorVisualState(isMoving, false);
    }

    bool CanChangeWorld(int delta)
    {
        if (worlds == null || worlds.Count <= 1)
            return false;

        int targetIndex = currentWorldIndex + delta;
        return targetIndex >= 0 && targetIndex < worlds.Count;
    }

    IEnumerator ChangeWorldRoutine(int delta)
    {
        if (transitioning || worlds.Count == 0)
            yield break;

        int targetWorldIndex = currentWorldIndex + delta;
        if (targetWorldIndex < 0 || targetWorldIndex >= worlds.Count)
            yield break;

        transitioning = true;
        playingSelectedAnimation = false;
        wasMovingLastFrame = false;

        if (fadeImage != null)
            yield return FadeOutWithMusicRoutine(worldChangeFadeOutDuration);
        else
            yield return FadeMusicOutRoutine(worldChangeFadeOutDuration);

        currentWorldIndex = targetWorldIndex;

        ApplyCurrentWorldCameraPosition();
        ApplyWorldVisibility();
        RefreshOptionalWorldScrollers();
        ApplyScaledStageAnchorPositions();
        UpdateAllStageIcons();
        RefreshClearedStageSpinAnimations();
        ApplyScaledCursorSize();
        ApplyScaledWorldStageLabelLayout();

        Canvas.ForceUpdateCanvases();

        if (snapCursorToDefaultStageOnWorldChange)
            SnapCursorToDefaultStage();

        ClampCursorIfNeeded();
        RefreshHoveredStage();
        RefreshWorldStageLabel();
        RefreshCursorVisualState(false, true);
        ApplyCursorVisualTransform();

        PlayMusicForCurrentWorld(forceRestart: true);

        if (fadeImage != null)
            yield return FadeInRoutine(worldChangeFadeInDuration);

        transitioning = false;
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

        StartCoroutine(ConfirmStageRoutine(node.sceneName));
    }

    IEnumerator ConfirmStageRoutine(string sceneName)
    {
        if (transitioning)
            yield break;

        transitioning = true;
        playingSelectedAnimation = true;

        PlaySfx(confirmStageSfx, confirmStageSfxVolume);

        RefreshCursorVisualState(false, true, true);

        if (fadeImage != null)
            yield return FadeOutRoutine(selectedTransitionDuration);
        else
            yield return new WaitForSecondsRealtime(selectedTransitionDuration);

        StagePreIntroPlayersWalk.SkipOnNextLoad();
        SceneManager.LoadScene(sceneName);
    }

    IEnumerator LoadSceneRoutine(string sceneName, AudioClip sfxClip = null, float sfxVolume = 1f)
    {
        if (transitioning)
            yield break;

        transitioning = true;

        if (sfxClip != null)
            PlaySfx(sfxClip, sfxVolume);

        if (fadeImage != null)
            yield return FadeOutWithMusicRoutine(selectedTransitionDuration);
        else
            yield return FadeMusicOutRoutine(selectedTransitionDuration);

        SceneManager.LoadScene(sceneName);
    }

    void ApplyWorldVisibility()
    {
        for (int i = 0; i < worlds.Count; i++)
        {
            if (worlds[i].root != null)
                worlds[i].root.SetActive(i == currentWorldIndex);
        }
    }

    void RefreshOptionalWorldScrollers()
    {
        if (world2BackgroundScroller == null)
            return;

        bool shouldEnable = currentWorldIndex == world2ScrollerWorldIndex;

        if (world2BackgroundScroller.enabled != shouldEnable)
            world2BackgroundScroller.enabled = shouldEnable;
    }

    void SnapCursorToDefaultStage()
    {
        if (cursorMovementArea == null)
            return;

        int defaultIndex = GetLastUnlockedNodeIndex(currentWorldIndex);
        var node = GetNode(currentWorldIndex, defaultIndex);
        if (node == null || node.anchor == null)
            return;

        cursorLocalPosition = GetAnchorPositionInMovementArea(node.anchor);
        hoveredNodeIndex = defaultIndex;
    }

    void RefreshHoveredStage()
    {
        hoveredNodeIndex = FindNearestNodeIndexToCursor();
        RefreshWorldStageLabel();
    }

    int FindNearestNodeIndexToCursor()
    {
        if (cursorMovementArea == null)
            return -1;

        var world = GetCurrentWorld();
        if (world == null || world.nodes == null || world.nodes.Count == 0)
            return -1;

        Vector2 cursorPos = cursorLocalPosition;

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
        if (!clampCursorInsideArea || cursorMovementArea == null)
            return;

        Rect r = cursorMovementArea.rect;
        Vector2 p = cursorLocalPosition;
        Vector2 size = GetScaledCursorSize();

        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;

        float minX = r.xMin + halfW;
        float maxX = r.xMax - halfW;
        float minY = r.yMin + halfH;
        float maxY = r.yMax - halfH;

        p.x = Mathf.Clamp(p.x, minX, maxX);
        p.y = Mathf.Clamp(p.y, minY, maxY);

        cursorLocalPosition = p;
    }

    void ApplyCursorVisualTransform()
    {
        if (cursorVisualRoot == null || cursorMovementArea == null)
            return;

        if (cursorVisualRoot.parent != cursorMovementArea)
            cursorVisualRoot.SetParent(cursorMovementArea, false);

        cursorVisualRoot.anchorMin = new Vector2(0.5f, 0.5f);
        cursorVisualRoot.anchorMax = new Vector2(0.5f, 0.5f);
        cursorVisualRoot.pivot = new Vector2(0.5f, 0.5f);
        cursorVisualRoot.anchoredPosition = cursorLocalPosition;
        cursorVisualRoot.localRotation = Quaternion.identity;
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

        bool isUnlocked = StageUnlockProgress.IsUnlocked(node.sceneName);
        bool isCleared = StageUnlockProgress.IsCleared(node.sceneName);
        bool isPerfect = StageUnlockProgress.IsPerfect(node.sceneName);

        Sprite sprite;
        Color color;

        if (!isUnlocked)
        {
            sprite = lockedStageSprite;
            color = lockedStageColor;
        }
        else if (isPerfect)
        {
            sprite = perfectStageSprite;
            color = perfectStageColor;
        }
        else if (isCleared)
        {
            sprite = clearedStageSprite;
            color = clearedStageColor;
        }
        else
        {
            sprite = availableStageSprite;
            color = availableStageColor;
        }

        node.runtimeIcon.sprite = sprite;
        node.runtimeIcon.color = color;
        node.runtimeIcon.enabled = node.runtimeIcon.sprite != null;

        var rt = node.runtimeIcon.rectTransform;
        rt.anchoredPosition = iconOffset;
        rt.sizeDelta = GetScaledIconSize();
        rt.localRotation = Quaternion.identity;
    }

    void RefreshCursorVisualState(bool isMoving, bool forceRestart = false, bool showSelected = false)
    {
        if (!useAnimatedCursorVisuals)
        {
            if (movingCursorVisual != null)
                movingCursorVisual.SetActive(false);

            if (selectedCursorVisual != null)
                selectedCursorVisual.SetActive(false);

            return;
        }

        bool usingSelected = showSelected || playingSelectedAnimation;

        if (usingSelected)
        {
            if (movingCursorVisual != null)
                movingCursorVisual.SetActive(false);

            RestartAndShow(selectedCursorVisual, forceRestart);
            return;
        }

        if (selectedCursorVisual != null)
            selectedCursorVisual.SetActive(false);

        if (movingCursorVisual != null)
            RestartAndShow(movingCursorVisual, forceRestart);
    }

    void RestartAndShow(GameObject target, bool restart)
    {
        if (target == null)
            return;

        if (restart && target.activeSelf)
            target.SetActive(false);

        target.SetActive(true);
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

    IEnumerator FadeInRoutine(float duration)
    {
        if (fadeImage == null)
            yield break;

        float usedDuration = Mathf.Max(0.01f, duration);

        fadeImage.gameObject.SetActive(true);
        SetFadeAlpha(1f);

        float t = 0f;
        while (t < usedDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(t / usedDuration);
            SetFadeAlpha(a);
            yield return null;
        }

        SetFadeAlpha(0f);
        fadeImage.gameObject.SetActive(false);
    }

    IEnumerator FadeOutRoutine(float duration)
    {
        if (fadeImage == null)
            yield break;

        fadeImage.gameObject.SetActive(true);
        SetFadeAlpha(0f);

        float t = 0f;
        float usedDuration = Mathf.Max(0.01f, duration);

        while (t < usedDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / usedDuration);
            SetFadeAlpha(a);
            yield return null;
        }

        SetFadeAlpha(1f);
    }

    IEnumerator FadeOutWithMusicRoutine(float duration)
    {
        float usedDuration = Mathf.Max(0.01f, duration);

        AudioSource musicSource = GameMusicController.Instance != null
            ? GameMusicController.Instance.GetMusicSource()
            : null;

        float initialMusicVolume = musicSource != null ? musicSource.volume : 0f;

        if (fadeImage == null)
        {
            yield return FadeMusicOutRoutine(usedDuration);
            yield break;
        }

        fadeImage.gameObject.SetActive(true);
        SetFadeAlpha(0f);

        float t = 0f;
        while (t < usedDuration)
        {
            t += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(t / usedDuration);

            SetFadeAlpha(normalized);

            if (musicSource != null)
                musicSource.volume = Mathf.Lerp(initialMusicVolume, 0f, normalized);

            yield return null;
        }

        SetFadeAlpha(1f);

        if (musicSource != null)
            musicSource.volume = 0f;
    }

    IEnumerator FadeMusicOutRoutine(float duration)
    {
        float usedDuration = Mathf.Max(0.01f, duration);

        AudioSource musicSource = GameMusicController.Instance != null
            ? GameMusicController.Instance.GetMusicSource()
            : null;

        if (musicSource == null)
        {
            yield return new WaitForSecondsRealtime(usedDuration);
            yield break;
        }

        float initialMusicVolume = musicSource.volume;
        float t = 0f;

        while (t < usedDuration)
        {
            t += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(t / usedDuration);
            musicSource.volume = Mathf.Lerp(initialMusicVolume, 0f, normalized);
            yield return null;
        }

        musicSource.volume = 0f;
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
        ApplyScaledWorldStageLabelLayout();
        UpdateAllStageIcons();
        RefreshClearedStageSpinAnimations();

        if (hoveredNodeIndex >= 0)
        {
            var hovered = GetHoveredNode();
            if (hovered != null && hovered.anchor != null)
                cursorLocalPosition = GetAnchorPositionInMovementArea(hovered.anchor);
        }

        ClampCursorIfNeeded();
        ApplyCursorVisualTransform();
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
        if (!scaleCursorVisualWithLogicalSize || cursorVisualRoot == null)
            return;

        Vector2 scaledSize = GetScaledCursorSize();

        float sx = cursorVisualBaseSize.x > 0.0001f ? scaledSize.x / cursorVisualBaseSize.x : 1f;
        float sy = cursorVisualBaseSize.y > 0.0001f ? scaledSize.y / cursorVisualBaseSize.y : 1f;

        if (preserveCursorAspect)
        {
            float uniform = Mathf.Min(sx, sy);
            cursorVisualRoot.localScale = new Vector3(uniform, uniform, 1f);
        }
        else
        {
            cursorVisualRoot.localScale = new Vector3(sx, sy, 1f);
        }
    }

    void RefreshWorldStageLabel()
    {
        if (worldStageLabel == null)
            return;

        int worldNumber = currentWorldIndex + 1;
        string text = worldLabelPrefix + worldNumber;

        var node = GetHoveredNode();
        if (node != null)
        {
            string stageSuffix = node.displayName;
            int dashIndex = stageSuffix.IndexOf('-');
            if (dashIndex >= 0 && dashIndex < stageSuffix.Length - 1)
                stageSuffix = stageSuffix.Substring(dashIndex + 1);

            text += stageSeparator + stageSuffix;
        }

        worldStageLabel.text = text;
    }

    float GetWorldStageLabelScale()
    {
        if (cursorMovementArea == null)
            return 1f;

        var canvas = GetRootCanvas();
        if (canvas == null)
            return 1f;

        Rect safePx = RectTransformUtility.PixelAdjustRect(cursorMovementArea, canvas);

        float sx = safePx.width / Mathf.Max(1f, worldStageLabelReferenceWidth);
        float sy = safePx.height / Mathf.Max(1f, worldStageLabelReferenceHeight);

        float rawScale = Mathf.Min(sx, sy);
        float usedScale = useIntegerUpscaleForWorldStageLabel ? Mathf.Floor(rawScale) : rawScale;

        if (usedScale < 1f)
            usedScale = 1f;

        usedScale *= Mathf.Max(0.01f, extraWorldStageLabelScaleMultiplier);
        usedScale = Mathf.Clamp(usedScale, minWorldStageLabelScale, maxWorldStageLabelScale);

        return usedScale;
    }

    void ApplyScaledWorldStageLabelLayout()
    {
        if (worldStageLabel == null)
            return;

        float scale = GetWorldStageLabelScale();

        RectTransform rt = worldStageLabel.rectTransform;
        rt.anchoredPosition = baseWorldStageLabelAnchoredPosition * scale;
        rt.sizeDelta = baseWorldStageLabelSize * scale;

        worldStageLabel.fontSize = Mathf.RoundToInt(baseWorldStageLabelFontSize * scale);
    }

    void EnsureTargetWorldCamera()
    {
        if (targetWorldCamera == null)
            targetWorldCamera = Camera.main;
    }

    void ApplyCurrentWorldCameraPosition()
    {
        if (!applyWorldCameraPosition)
            return;

        EnsureTargetWorldCamera();

        if (targetWorldCamera == null)
            return;

        var world = GetCurrentWorld();
        if (world == null)
            return;

        Vector3 currentCameraPosition = targetWorldCamera.transform.position;
        targetWorldCamera.transform.position = new Vector3(
            world.cameraPosition.x,
            world.cameraPosition.y,
            currentCameraPosition.z);
    }

    void RegisterStageOrderInProgress()
    {
        List<string> orderedSceneNames = new List<string>();

        for (int w = 0; w < worlds.Count; w++)
        {
            var world = worlds[w];
            if (world == null || world.nodes == null)
                continue;

            for (int n = 0; n < world.nodes.Count; n++)
            {
                var node = world.nodes[n];
                if (node == null || string.IsNullOrWhiteSpace(node.sceneName))
                    continue;

                orderedSceneNames.Add(node.sceneName);
            }
        }

        StageUnlockProgress.RegisterStageOrder(orderedSceneNames);
    }

    void ApplyUnlockedStagesFromProgress()
    {
        for (int w = 0; w < worlds.Count; w++)
        {
            var world = worlds[w];
            if (world == null || world.nodes == null)
                continue;

            for (int n = 0; n < world.nodes.Count; n++)
            {
                var node = world.nodes[n];
                if (node == null)
                    continue;

                node.unlocked = StageUnlockProgress.IsUnlocked(node.sceneName);
            }
        }
    }

    int GetInitialWorldIndexFromProgress()
    {
        if (worlds == null || worlds.Count == 0)
            return 0;

        int fallbackIndex = Mathf.Clamp(startWorldIndex, 0, worlds.Count - 1);

        if (!IsWorldFullyCleared(fallbackIndex))
            return fallbackIndex;

        for (int i = fallbackIndex + 1; i < worlds.Count; i++)
        {
            if (!IsWorldFullyCleared(i))
                return i;
        }

        for (int i = 0; i < fallbackIndex; i++)
        {
            if (!IsWorldFullyCleared(i))
                return i;
        }

        return Mathf.Clamp(worlds.Count - 1, 0, worlds.Count - 1);
    }

    bool IsWorldFullyCleared(int worldIndex)
    {
        if (worldIndex < 0 || worldIndex >= worlds.Count)
            return false;

        var world = worlds[worldIndex];
        if (world == null || world.nodes == null || world.nodes.Count == 0)
            return false;

        bool hasAtLeastOneValidStage = false;

        for (int i = 0; i < world.nodes.Count; i++)
        {
            var node = world.nodes[i];
            if (node == null || string.IsNullOrWhiteSpace(node.sceneName))
                continue;

            hasAtLeastOneValidStage = true;

            if (!StageUnlockProgress.IsCleared(node.sceneName))
                return false;
        }

        return hasAtLeastOneValidStage;
    }

    int GetLastUnlockedNodeIndex(int worldIndex)
    {
        if (worldIndex < 0 || worldIndex >= worlds.Count)
            return 0;

        var world = worlds[worldIndex];
        if (world == null || world.nodes == null || world.nodes.Count == 0)
            return 0;

        int lastUnlocked = -1;

        for (int i = 0; i < world.nodes.Count; i++)
        {
            var node = world.nodes[i];
            if (node != null && node.unlocked)
                lastUnlocked = i;
        }

        if (lastUnlocked >= 0)
            return lastUnlocked;

        return Mathf.Clamp(world.defaultNodeIndex, 0, world.nodes.Count - 1);
    }

    void RefreshClearedStageSpinAnimations()
    {
        StopAllClearedStageSpinAnimations();

        if (!animateClearedStageIcons)
            return;

        for (int w = 0; w < worlds.Count; w++)
        {
            var world = worlds[w];
            if (world == null || world.nodes == null)
                continue;

            bool worldVisible = w == currentWorldIndex;

            for (int n = 0; n < world.nodes.Count; n++)
            {
                var node = world.nodes[n];
                if (node == null || node.runtimeIcon == null)
                    continue;

                bool isCleared = StageUnlockProgress.IsCleared(node.sceneName);
                bool shouldAnimate = worldVisible && isCleared && node.runtimeIcon.enabled;

                if (!shouldAnimate)
                {
                    node.runtimeIcon.rectTransform.localRotation = Quaternion.identity;
                    continue;
                }

                Coroutine routine = StartCoroutine(ClearedStageSpinRoutine(node.runtimeIcon));
                clearedStageSpinRoutines[node.runtimeIcon] = routine;
            }
        }
    }

    void StopAllClearedStageSpinAnimations()
    {
        foreach (var kvp in clearedStageSpinRoutines)
        {
            if (kvp.Value != null)
                StopCoroutine(kvp.Value);

            if (kvp.Key != null)
                kvp.Key.rectTransform.localRotation = Quaternion.identity;
        }

        clearedStageSpinRoutines.Clear();
    }

    IEnumerator ClearedStageSpinRoutine(Image icon)
    {
        if (icon == null)
            yield break;

        float stepDelay = Mathf.Max(0.001f, clearedStageSpinStepMilliseconds / 1000f);

        while (icon != null)
        {
            yield return new WaitForSecondsRealtime(clearedStageSpinInterval);

            if (icon == null || !icon.isActiveAndEnabled)
                continue;

            RectTransform rt = icon.rectTransform;
            if (rt == null)
                continue;

            rt.localRotation = Quaternion.identity;

            for (int i = 0; i < 4; i++)
            {
                if (rt == null)
                    yield break;

                rt.localRotation = Quaternion.Euler(0f, 0f, -90f * (i + 1));
                yield return new WaitForSecondsRealtime(stepDelay);
            }

            if (rt != null)
                rt.localRotation = Quaternion.identity;
        }
    }

    void OnDestroy()
    {
        StopAllClearedStageSpinAnimations();
    }
}