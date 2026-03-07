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

    const string LOG = "[WorldMapCursor]";

    [Header("Debug (Surgical Logs)")]
    [SerializeField] bool enableSurgicalLogs = true;
    [SerializeField] bool logOnStart = true;
    [SerializeField] bool logCanvasAndCameraState = true;
    [SerializeField] bool logCursorTransform = true;
    [SerializeField] bool logCursorScale = true;
    [SerializeField] bool logHoveredStage = true;
    [SerializeField] bool logResolutionChanges = true;
    [SerializeField] bool logRendererState = true;
    [SerializeField] bool logWarningsOnlyWhenBroken = true;

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
    [SerializeField] float selectedAnimationDelayBeforeLoad = 0.25f;

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
    private readonly bool allowReturnToTitle = true;
    [SerializeField] string titleSceneName = "TitleScreen";

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

    Vector3 lastLoggedCursorWorldPosition = new Vector3(float.MinValue, float.MinValue, float.MinValue);
    Vector2 lastLoggedCursorLocalPosition = new Vector2(float.MinValue, float.MinValue);
    Vector3 lastLoggedCursorScale = new Vector3(float.MinValue, float.MinValue, float.MinValue);
    int lastLoggedHoveredNodeIndex = int.MinValue;

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
        ApplyScaledWorldStageLabelLayout();

        if (createIconsOnStart)
            EnsureAllStageIcons();

        ApplyWorldVisibility();
        UpdateAllStageIcons();

        Canvas.ForceUpdateCanvases();

        if (snapCursorToDefaultStageOnStart)
            SnapCursorToDefaultStage();

        ClampCursorIfNeeded();
        RefreshHoveredStage();
        RefreshWorldStageLabel();
        RefreshCursorVisualState(false, true);
        ApplyCursorVisualTransform();

        if (enableSurgicalLogs && logOnStart)
        {
            DumpCanvasAndCameraState("Start");
            DumpCursorState("Start");
            DumpCursorRendererState("Start");
        }

        if (fadeImage != null)
            StartCoroutine(FadeInRoutine());

        PlayMusicForCurrentWorld(forceRestart: true);
    }

    void LateUpdate()
    {
        ApplyCursorVisualTransform();
        SurgicalTick();
    }

    void SurgicalTick()
    {
        if (!enableSurgicalLogs)
            return;

        if (logCursorTransform)
        {
            Vector2 cursorAnchored = cursorVisualRoot != null ? cursorVisualRoot.anchoredPosition : Vector2.zero;
            if (cursorLocalPosition != lastLoggedCursorLocalPosition || (Vector3)cursorAnchored != lastLoggedCursorWorldPosition)
            {
                lastLoggedCursorLocalPosition = cursorLocalPosition;
                lastLoggedCursorWorldPosition = cursorAnchored;
                Debug.Log(
                    $"{LOG} CursorTransform | local={cursorLocalPosition} anchored={cursorAnchored} hoveredNodeIndex={hoveredNodeIndex}",
                    this);
            }
        }

        if (logCursorScale && cursorVisualRoot != null && cursorVisualRoot.localScale != lastLoggedCursorScale)
        {
            lastLoggedCursorScale = cursorVisualRoot.localScale;
            Debug.Log(
                $"{LOG} CursorScale | visualRoot='{cursorVisualRoot.name}' localScale={cursorVisualRoot.localScale} " +
                $"scaledLogicalSize={GetScaledCursorSize()} baseVisualSize={cursorVisualBaseSize}",
                this);
        }

        if (logHoveredStage && hoveredNodeIndex != lastLoggedHoveredNodeIndex)
        {
            lastLoggedHoveredNodeIndex = hoveredNodeIndex;
            var hovered = GetHoveredNode();
            string hoveredName = hovered != null ? hovered.displayName : "<none>";
            Debug.Log($"{LOG} HoveredStage | hoveredNodeIndex={hoveredNodeIndex} hovered='{hoveredName}'", this);
        }
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

            if (!wasMovingLastFrame)
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
        }

        wasMovingLastFrame = isMoving;
        RefreshCursorVisualState(isMoving, false);
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

        playingSelectedAnimation = false;

        ApplyWorldVisibility();
        ApplyScaledStageAnchorPositions();
        UpdateAllStageIcons();
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

        if (enableSurgicalLogs)
        {
            DumpCursorState("ChangeWorld");
            DumpCursorRendererState("ChangeWorld");
        }

        PlaySfx(changeWorldSfx, changeWorldSfxVolume);
        PlayMusicForCurrentWorld(forceRestart: false);
    }

    void ConfirmCurrentStage()
    {
        var node = GetHoveredNode();

        if (node == null)
        {
            if (enableSurgicalLogs && logWarningsOnlyWhenBroken)
                Debug.LogWarning($"{LOG} ConfirmCurrentStage failed: no hovered node.", this);
            return;
        }

        if (!node.unlocked || string.IsNullOrEmpty(node.sceneName))
        {
            if (enableSurgicalLogs && logWarningsOnlyWhenBroken)
                Debug.LogWarning(
                    $"{LOG} ConfirmCurrentStage denied | node='{node.displayName}' unlocked={node.unlocked} scene='{node.sceneName}'",
                    this);

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

        if (enableSurgicalLogs)
            Debug.Log($"{LOG} ConfirmStageRoutine | scene='{sceneName}'", this);

        PlaySfx(confirmStageSfx, confirmStageSfxVolume);
        RefreshCursorVisualState(false, true, true);

        if (selectedAnimationDelayBeforeLoad > 0f)
            yield return new WaitForSecondsRealtime(selectedAnimationDelayBeforeLoad);

        if (fadeImage != null)
            yield return FadeOutRoutine();

        SceneManager.LoadScene(sceneName);
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
        if (cursorMovementArea == null)
            return;

        int defaultIndex = GetSafeDefaultNodeIndex(currentWorldIndex);
        var node = GetNode(currentWorldIndex, defaultIndex);
        if (node == null || node.anchor == null)
            return;

        cursorLocalPosition = GetAnchorPositionInMovementArea(node.anchor);
        hoveredNodeIndex = defaultIndex;

        if (enableSurgicalLogs)
            Debug.Log($"{LOG} SnapCursorToDefaultStage | node='{node.displayName}' local={cursorLocalPosition}", this);
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

        node.runtimeIcon.sprite = node.unlocked ? unlockedStageSprite : lockedStageSprite;
        node.runtimeIcon.color = node.unlocked ? unlockedStageColor : lockedStageColor;
        node.runtimeIcon.enabled = node.runtimeIcon.sprite != null;

        var rt = node.runtimeIcon.rectTransform;
        rt.anchoredPosition = iconOffset;
        rt.sizeDelta = GetScaledIconSize();
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
        ApplyScaledWorldStageLabelLayout();
        UpdateAllStageIcons();

        if (hoveredNodeIndex >= 0)
        {
            var hovered = GetHoveredNode();
            if (hovered != null && hovered.anchor != null)
                cursorLocalPosition = GetAnchorPositionInMovementArea(hovered.anchor);
        }

        ClampCursorIfNeeded();
        ApplyCursorVisualTransform();

        if (enableSurgicalLogs && logResolutionChanges)
        {
            Debug.Log(
                $"{LOG} ResolutionOrScaleChanged | screen={Screen.width}x{Screen.height} " +
                $"canvasScaleFactor={scaleFactor} movementAreaPx={movementAreaPx} movementAreaLocal={movementAreaLocal}",
                this);

            DumpCanvasAndCameraState("ResolutionChange");
            DumpCursorState("ResolutionChange");
            DumpCursorRendererState("ResolutionChange");
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

        if (enableSurgicalLogs && logCursorScale)
        {
            Debug.Log(
                $"{LOG} ApplyScaledCursorSize | scaledLogicalSize={scaledSize} baseVisualSize={cursorVisualBaseSize} " +
                $"appliedScale={cursorVisualRoot.localScale}",
                this);
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

    void DumpCanvasAndCameraState(string reason)
    {
        if (!logCanvasAndCameraState)
            return;

        Canvas canvas = GetRootCanvas();
        Camera mainCam = Camera.main;

        string canvasInfo = canvas == null
            ? "canvas=<null>"
            : $"canvas='{canvas.name}' renderMode={canvas.renderMode} worldCamera={(canvas.worldCamera ? canvas.worldCamera.name : "<null>")} scaleFactor={canvas.scaleFactor} pixelPerfect={canvas.pixelPerfect}";

        string cameraInfo = mainCam == null
            ? "mainCamera=<null>"
            : $"mainCamera='{mainCam.name}' position={mainCam.transform.position} orthographic={mainCam.orthographic} orthographicSize={mainCam.orthographicSize} pixelRect={mainCam.pixelRect}";

        Debug.Log($"{LOG} CanvasCameraState | reason={reason} {canvasInfo} {cameraInfo}", this);

        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            Debug.Log(
                $"{LOG} Canvas is Screen Space Overlay and cursor is now UI/Image-based, which is correct for this setup.",
                this);
        }
    }

    void DumpCursorState(string reason)
    {
        if (!logCursorTransform)
            return;

        Vector2 scaledCursor = GetScaledCursorSize();
        Vector3 worldPos = cursorVisualRoot != null ? cursorVisualRoot.position : Vector3.zero;
        Rect localRect = cursorMovementArea != null ? cursorMovementArea.rect : default;

        Debug.Log(
            $"{LOG} CursorState | reason={reason} local={cursorLocalPosition} world={worldPos} " +
            $"scaledCursorSize={scaledCursor} movementAreaRect={localRect} visualRoot={(cursorVisualRoot ? cursorVisualRoot.name : "<null>")} " +
            $"movingActive={(movingCursorVisual != null && movingCursorVisual.activeSelf)} selectedActive={(selectedCursorVisual != null && selectedCursorVisual.activeSelf)}",
            this);
    }

    void DumpCursorRendererState(string reason)
    {
        if (!logRendererState)
            return;

        DumpOneImageState(reason, "Root", cursorVisualRoot != null ? cursorVisualRoot.GetComponent<Image>() : null);
        DumpOneImageState(reason, "Moving", movingCursorVisual != null ? movingCursorVisual.GetComponent<Image>() : null);
        DumpOneImageState(reason, "Select", selectedCursorVisual != null ? selectedCursorVisual.GetComponent<Image>() : null);
    }

    void DumpOneImageState(string reason, string label, Image img)
    {
        if (img == null)
        {
            Debug.LogWarning($"{LOG} ImageState | reason={reason} label={label} image=<null>", this);
            return;
        }

        string spriteName = img.sprite != null ? img.sprite.name : "<null>";
        RectTransform rt = img.rectTransform;

        Debug.Log(
            $"{LOG} ImageState | reason={reason} label={label} go='{img.gameObject.name}' enabled={img.enabled} activeInHierarchy={img.gameObject.activeInHierarchy} " +
            $"sprite='{spriteName}' color={img.color} anchoredPos={rt.anchoredPosition} sizeDelta={rt.sizeDelta} localScale={rt.localScale}",
            img);

        if (logWarningsOnlyWhenBroken)
        {
            if (img.sprite == null)
                Debug.LogWarning($"{LOG} ImageState problem | label={label} has no sprite.", img);

            if (!img.enabled)
                Debug.LogWarning($"{LOG} ImageState problem | label={label} image disabled.", img);

            if (!img.gameObject.activeInHierarchy)
                Debug.LogWarning($"{LOG} ImageState problem | label={label} GameObject inactive.", img);

            if (rt.rect.width <= 0.0001f || rt.rect.height <= 0.0001f)
                Debug.LogWarning($"{LOG} ImageState problem | label={label} rect too small: {rt.rect.size}", img);
        }
    }
}