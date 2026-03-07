using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WorldMapController : MonoBehaviour
{
    const string LOG = "[WorldMap]";

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

    [Header("Debug (Surgical Logs)")]
    [SerializeField] bool enableSurgicalLogs = true;

    [SerializeField] bool logLayoutDiagnosticsOnStart = true;
    [SerializeField] bool logLayoutDiagnosticsOnWorldChange = true;
    [SerializeField] bool logLayoutDiagnosticsOnResolutionChange = true;
    [SerializeField] bool logHoveredAnchorChanges = true;

    [Header("Anchor Drift Diagnostics")]
    [SerializeField] bool captureNormalizedBaselineOnStart = true;
    [SerializeField] bool recaptureNormalizedBaselineOnWorldChange = true;
    [SerializeField] bool logCapturedBaseline = true;
    [SerializeField] bool logAnchorDriftAgainstBaseline = true;
    [SerializeField] float driftLogThresholdNormalized = 0.0001f;
    [SerializeField] bool logFullAnchorHierarchy = true;
    [SerializeField] bool logMovementAreaWorldCorners = true;
    [SerializeField] bool logAllActiveWorldAnchorsInDiagnostics = true;

    [Header("Stage Anchor Scaling Fix")]
    [Tooltip("Posições das stages são tratadas como coordenadas lógicas SNES e reescaladas para o SafeFrame atual.")]
    [SerializeField] bool scaleStageAnchorsWithSafeFrame = true;

    [Tooltip("Largura lógica usada ao posicionar as stages no mapa.")]
    [SerializeField] int stageAnchorReferenceWidth = 256;

    [Tooltip("Altura lógica usada ao posicionar as stages no mapa.")]
    [SerializeField] int stageAnchorReferenceHeight = 224;

    [Tooltip("Usa upscale inteiro igual ao mapa SNES.")]
    [SerializeField] bool useIntegerUpscaleForStageAnchors = true;

    [SerializeField] float extraStageAnchorScaleMultiplier = 1f;
    [SerializeField] float minStageAnchorScale = 1f;
    [SerializeField] float maxStageAnchorScale = 20f;

    [SerializeField] bool logStageAnchorScaling = true;

    [Header("Input Owner")]
    [SerializeField, Range(1, 4)] int ownerPlayerId = 1;

    [Header("Worlds")]
    [SerializeField] List<WorldData> worlds = new List<WorldData>();
    [SerializeField] int startWorldIndex = 0;

    [Header("Cursor")]
    [SerializeField] RectTransform cursor;
    [SerializeField] RectTransform cursorMovementArea;
    [SerializeField] float cursorMoveSpeedNormalized = 0.25f;
    [SerializeField] float cursorMoveSpeed = 140f;
    [SerializeField] bool clampCursorInsideArea = true;
    [SerializeField] bool snapCursorToDefaultStageOnStart = true;
    [SerializeField] bool snapCursorToDefaultStageOnWorldChange = true;

    [Tooltip("Tamanho lógico SNES do cursor, antes do upscale.")]
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

    [Tooltip("Tamanho lógico SNES do ícone, antes do upscale.")]
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
    int lastLoggedHoveredNodeIndex = -2;

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

    readonly Dictionary<string, Vector2> capturedAnchorNormalizedPositions = new Dictionary<string, Vector2>();
    readonly Dictionary<string, Vector2> authoredStageAnchorPositions = new Dictionary<string, Vector2>();

    void SLog(string msg)
    {
        if (!enableSurgicalLogs) return;
        Debug.Log($"{LOG} {msg}", this);
    }

    void SWarn(string msg)
    {
        if (!enableSurgicalLogs) return;
        Debug.LogWarning($"{LOG} {msg}", this);
    }

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

        if (captureNormalizedBaselineOnStart)
            CaptureNormalizedPositionsFromCurrentLayout("Start");

        if (snapCursorToDefaultStageOnStart)
            SnapCursorToDefaultStage();

        ClampCursorIfNeeded();
        RefreshHoveredStage();

        if (fadeImage != null)
            StartCoroutine(FadeInRoutine());

        PlayMusicForCurrentWorld(forceRestart: true);

        if (logLayoutDiagnosticsOnStart)
            DumpResolutionSpeedAndAnchorDiagnostics("Start");
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

    void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled)
            return;

        if (!enableSurgicalLogs || !logLayoutDiagnosticsOnResolutionChange)
            return;

        if (cursorMovementArea == null)
            return;

        SLog("OnRectTransformDimensionsChange | detected on controller hierarchy");
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

        int oldWorld = currentWorldIndex;
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

        if (recaptureNormalizedBaselineOnWorldChange)
            CaptureNormalizedPositionsFromCurrentLayout("WorldChange");

        if (snapCursorToDefaultStageOnWorldChange)
            SnapCursorToDefaultStage();

        ClampCursorIfNeeded();
        RefreshHoveredStage();
        PlaySfx(changeWorldSfx, changeWorldSfxVolume);
        PlayMusicForCurrentWorld(forceRestart: false);

        SLog($"ChangeWorld | from={oldWorld} to={currentWorldIndex} hoveredNode={hoveredNodeIndex}");

        if (logLayoutDiagnosticsOnWorldChange)
            DumpResolutionSpeedAndAnchorDiagnostics("ChangeWorld");
    }

    void ConfirmCurrentStage()
    {
        var node = GetHoveredNode();
        if (node == null)
        {
            PlaySfx(deniedSfx, deniedSfxVolume);
            SLog("Confirm denied | no hovered stage");
            return;
        }

        if (!node.unlocked || string.IsNullOrEmpty(node.sceneName))
        {
            PlaySfx(deniedSfx, deniedSfxVolume);
            SLog($"Confirm denied | hovered='{node.displayName}' unlocked={node.unlocked} scene='{node.sceneName}'");
            return;
        }

        PlaySfx(confirmStageSfx, confirmStageSfxVolume);
        SLog($"Confirm | loading scene='{node.sceneName}' from hovered='{node.displayName}'");
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

        SLog($"SnapCursorToDefaultStage | world={currentWorldIndex} node={defaultIndex} anchor='{node.anchor.name}'");
    }

    void RefreshHoveredStage()
    {
        hoveredNodeIndex = FindNearestNodeIndexToCursor();

        if (hoveredNodeIndex >= 0)
        {
            var node = GetHoveredNode();
            if (node != null && logHoveredAnchorChanges && hoveredNodeIndex != lastLoggedHoveredNodeIndex)
            {
                Vector2 anchorLocal = GetAnchorPositionInMovementArea(node.anchor);
                Vector2 anchorNorm = GetNormalizedPointInMovementArea(anchorLocal);

                SLog(
                    $"HoverChanged | world={currentWorldIndex} node={hoveredNodeIndex} " +
                    $"displayName='{node.displayName}' scene='{node.sceneName}' unlocked={node.unlocked} " +
                    $"anchorLocalInArea=({anchorLocal.x:F3},{anchorLocal.y:F3}) " +
                    $"anchorNormInArea=({anchorNorm.x:F4},{anchorNorm.y:F4}) " +
                    $"cursorPos=({cursor.anchoredPosition.x:F3},{cursor.anchoredPosition.y:F3})");
            }
        }

        lastLoggedHoveredNodeIndex = hoveredNodeIndex;
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

    Vector2 GetNormalizedPointInMovementArea(Vector2 localPoint)
    {
        if (cursorMovementArea == null)
            return Vector2.zero;

        Rect r = cursorMovementArea.rect;
        if (Mathf.Abs(r.width) < 0.0001f || Mathf.Abs(r.height) < 0.0001f)
            return Vector2.zero;

        float nx = Mathf.InverseLerp(r.xMin, r.xMax, localPoint.x);
        float ny = Mathf.InverseLerp(r.yMin, r.yMax, localPoint.y);
        return new Vector2(nx, ny);
    }

    Vector2 GetPixelsPerLocalUnitInMovementArea()
    {
        var canvas = GetRootCanvas();
        if (canvas == null || cursorMovementArea == null)
            return Vector2.one;

        Rect px = RectTransformUtility.PixelAdjustRect(cursorMovementArea, canvas);
        Rect lr = cursorMovementArea.rect;

        float pxPerLocalX = Mathf.Abs(lr.width) > 0.0001f ? px.width / lr.width : 1f;
        float pxPerLocalY = Mathf.Abs(lr.height) > 0.0001f ? px.height / lr.height : 1f;

        return new Vector2(pxPerLocalX, pxPerLocalY);
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

    CanvasScaler GetRootCanvasScaler()
    {
        var canvas = GetRootCanvas();
        if (canvas == null)
            return null;

        return canvas.GetComponent<CanvasScaler>();
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

                if (logStageAnchorScaling)
                {
                    SLog(
                        $"CaptureAuthoredAnchor | world={w} node={n} displayName='{node.displayName}' " +
                        $"anchor='{node.anchor.name}' authoredLogical=({node.anchor.anchoredPosition.x:F3},{node.anchor.anchoredPosition.y:F3})");
                }
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

                Vector2 scaled = authoredLogical * anchorScale;
                node.anchor.anchoredPosition = scaled;

                if (logStageAnchorScaling)
                {
                    SLog(
                        $"ApplyScaledStageAnchor | world={w} node={n} displayName='{node.displayName}' " +
                        $"anchor='{node.anchor.name}' authoredLogical=({authoredLogical.x:F3},{authoredLogical.y:F3}) " +
                        $"scale={anchorScale:F4} scaledAnchored=({scaled.x:F3},{scaled.y:F3})");
                }
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

    void CaptureNormalizedPositionsFromCurrentLayout(string reason)
    {
        capturedAnchorNormalizedPositions.Clear();

        var world = GetCurrentWorld();
        if (world == null || world.nodes == null)
        {
            SLog($"CaptureNormalized skipped | reason={reason} world=NULL");
            return;
        }

        Canvas.ForceUpdateCanvases();

        for (int i = 0; i < world.nodes.Count; i++)
        {
            var node = world.nodes[i];
            if (node == null || node.anchor == null)
                continue;

            Vector2 localInArea = GetAnchorPositionInMovementArea(node.anchor);
            Vector2 normalized = GetNormalizedPointInMovementArea(localInArea);

            string key = GetAnchorKey(currentWorldIndex, i);
            capturedAnchorNormalizedPositions[key] = normalized;

            if (logCapturedBaseline)
            {
                SLog(
                    $"CaptureNormalized | world={currentWorldIndex} node={i} displayName='{node.displayName}' " +
                    $"anchor='{node.anchor.name}' localInArea=({localInArea.x:F3},{localInArea.y:F3}) " +
                    $"normalized=({normalized.x:F4},{normalized.y:F4})");
            }
        }
    }

    void DumpAnchorDriftFromCapturedBaseline(string reason)
    {
        if (!logAnchorDriftAgainstBaseline)
            return;

        var world = GetCurrentWorld();
        if (world == null || world.nodes == null)
            return;

        if (capturedAnchorNormalizedPositions.Count == 0)
        {
            SLog($"AnchorDrift[{reason}] | baseline empty");
            return;
        }

        for (int i = 0; i < world.nodes.Count; i++)
        {
            var node = world.nodes[i];
            if (node == null || node.anchor == null)
                continue;

            string key = GetAnchorKey(currentWorldIndex, i);
            if (!capturedAnchorNormalizedPositions.TryGetValue(key, out Vector2 baseline))
                continue;

            Vector2 localInArea = GetAnchorPositionInMovementArea(node.anchor);
            Vector2 current = GetNormalizedPointInMovementArea(localInArea);
            Vector2 delta = current - baseline;

            if (Mathf.Abs(delta.x) < driftLogThresholdNormalized &&
                Mathf.Abs(delta.y) < driftLogThresholdNormalized)
                continue;

            SLog(
                $"AnchorDrift[{reason}] | world={currentWorldIndex} node={i} displayName='{node.displayName}' " +
                $"anchor='{node.anchor.name}' baselineNorm=({baseline.x:F4},{baseline.y:F4}) " +
                $"currentNorm=({current.x:F4},{current.y:F4}) deltaNorm=({delta.x:F4},{delta.y:F4}) " +
                $"localInArea=({localInArea.x:F3},{localInArea.y:F3})");
        }
    }

    string GetAnchorKey(int worldIndex, int nodeIndex)
    {
        return $"{worldIndex}:{nodeIndex}";
    }

    void DumpIconSizeDiagnostics(string reason)
    {
        var canvas = GetRootCanvas();
        if (canvas == null)
        {
            SLog($"Diag[{reason}] | canvas=NULL");
            return;
        }

        var scaler = GetRootCanvasScaler();

        Rect movementAreaPx = cursorMovementArea != null
            ? RectTransformUtility.PixelAdjustRect(cursorMovementArea, canvas)
            : default;

        Vector2 scaledIconSize = GetScaledIconSize();
        Vector2 scaledCursorSize = GetScaledCursorSize();
        Vector2 pxPerLocal = GetPixelsPerLocalUnitInMovementArea();
        float stageAnchorScale = GetStageAnchorScale();

        string scalerInfo = scaler == null
            ? "canvasScaler=NULL"
            : $"canvasScaler(uiScaleMode={scaler.uiScaleMode} refRes=({scaler.referenceResolution.x:F0}x{scaler.referenceResolution.y:F0}) match={scaler.matchWidthOrHeight:F3})";

        SLog(
            $"Diag[{reason}] | " +
            $"screen=({Screen.width}x{Screen.height}) " +
            $"canvasScaleFactor={canvas.scaleFactor:F4} referencePPU={canvas.referencePixelsPerUnit:F2} " +
            $"{scalerInfo} " +
            $"movementAreaLocal=({cursorMovementArea.rect.xMin:F3},{cursorMovementArea.rect.yMin:F3},{cursorMovementArea.rect.width:F3},{cursorMovementArea.rect.height:F3}) " +
            $"movementAreaPx=({movementAreaPx.xMin:F2},{movementAreaPx.yMin:F2},{movementAreaPx.width:F2},{movementAreaPx.height:F2}) " +
            $"pxPerLocal=({pxPerLocal.x:F4},{pxPerLocal.y:F4}) " +
            $"stageAnchorScale={stageAnchorScale:F4} stageDetectRadiusScaled={GetScaledStageDetectRadius():F3} " +
            $"baseIconLogicalSize=({baseIconLogicalSize.x:F2}x{baseIconLogicalSize.y:F2}) scaledIconSize=({scaledIconSize.x:F2}x{scaledIconSize.y:F2}) " +
            $"baseCursorLogicalSize=({baseCursorLogicalSize.x:F2}x{baseCursorLogicalSize.y:F2}) scaledCursorSize=({scaledCursorSize.x:F2}x{scaledCursorSize.y:F2})");

        if (cursorMovementArea != null)
            DumpRectTransformGeometry($"MovementArea[{reason}]", cursorMovementArea);

        if (logMovementAreaWorldCorners && cursorMovementArea != null)
            DumpWorldCorners($"MovementAreaCorners[{reason}]", cursorMovementArea);

        var world = GetCurrentWorld();
        if (world == null || world.nodes == null || !logAllActiveWorldAnchorsInDiagnostics)
            return;

        for (int i = 0; i < world.nodes.Count; i++)
        {
            var node = world.nodes[i];
            if (node == null || node.anchor == null)
                continue;

            DumpAnchorDetailedDiagnostics(reason, i, node);
        }
    }

    void DumpAnchorDetailedDiagnostics(string reason, int nodeIndex, StageNode node)
    {
        var canvas = GetRootCanvas();
        if (canvas == null || node == null || node.anchor == null)
            return;

        Rect anchorPx = RectTransformUtility.PixelAdjustRect(node.anchor, canvas);
        Vector2 anchorLocalInArea = GetAnchorPositionInMovementArea(node.anchor);
        Vector2 anchorNormInArea = GetNormalizedPointInMovementArea(anchorLocalInArea);

        Vector3 worldCenter = node.anchor.TransformPoint(node.anchor.rect.center);
        Vector3 worldPivot = node.anchor.position;
        Vector3 localPivotInArea = cursorMovementArea != null
            ? cursorMovementArea.InverseTransformPoint(worldPivot)
            : Vector3.zero;
        Vector3 localCenterInArea = cursorMovementArea != null
            ? cursorMovementArea.InverseTransformPoint(worldCenter)
            : Vector3.zero;

        string key = GetAnchorKey(currentWorldIndex, nodeIndex);
        Vector2 authoredLogical = authoredStageAnchorPositions.TryGetValue(key, out Vector2 authored)
            ? authored
            : node.anchor.anchoredPosition;

        string iconInfo = "icon=NULL";
        if (node.runtimeIcon != null)
        {
            Rect iconPx = RectTransformUtility.PixelAdjustRect(node.runtimeIcon.rectTransform, canvas);
            Vector3 iconWorld = node.runtimeIcon.rectTransform.TransformPoint(node.runtimeIcon.rectTransform.rect.center);
            Vector3 iconLocalInArea = cursorMovementArea.InverseTransformPoint(iconWorld);

            iconInfo =
                $"iconAnchored=({node.runtimeIcon.rectTransform.anchoredPosition.x:F3},{node.runtimeIcon.rectTransform.anchoredPosition.y:F3}) " +
                $"iconSizeDelta=({node.runtimeIcon.rectTransform.sizeDelta.x:F3},{node.runtimeIcon.rectTransform.sizeDelta.y:F3}) " +
                $"iconPxRect=({iconPx.xMin:F2},{iconPx.yMin:F2},{iconPx.width:F2},{iconPx.height:F2}) " +
                $"iconLocalInArea=({iconLocalInArea.x:F3},{iconLocalInArea.y:F3})";
        }

        SLog(
            $"AnchorDiag[{reason}] | world={currentWorldIndex} node={nodeIndex} displayName='{node.displayName}' unlocked={node.unlocked} " +
            $"anchor='{node.anchor.name}' " +
            $"authoredLogical=({authoredLogical.x:F3},{authoredLogical.y:F3}) " +
            $"anchorMin=({node.anchor.anchorMin.x:F3},{node.anchor.anchorMin.y:F3}) " +
            $"anchorMax=({node.anchor.anchorMax.x:F3},{node.anchor.anchorMax.y:F3}) " +
            $"pivot=({node.anchor.pivot.x:F3},{node.anchor.pivot.y:F3}) " +
            $"anchored=({node.anchor.anchoredPosition.x:F3},{node.anchor.anchoredPosition.y:F3}) " +
            $"sizeDelta=({node.anchor.sizeDelta.x:F3},{node.anchor.sizeDelta.y:F3}) " +
            $"rect=({node.anchor.rect.xMin:F3},{node.anchor.rect.yMin:F3},{node.anchor.rect.width:F3},{node.anchor.rect.height:F3}) " +
            $"localPos=({node.anchor.localPosition.x:F3},{node.anchor.localPosition.y:F3},{node.anchor.localPosition.z:F3}) " +
            $"worldPivot=({worldPivot.x:F3},{worldPivot.y:F3},{worldPivot.z:F3}) " +
            $"worldCenter=({worldCenter.x:F3},{worldCenter.y:F3},{worldCenter.z:F3}) " +
            $"lossyScale=({node.anchor.lossyScale.x:F4},{node.anchor.lossyScale.y:F4},{node.anchor.lossyScale.z:F4}) " +
            $"anchorPxRect=({anchorPx.xMin:F2},{anchorPx.yMin:F2},{anchorPx.width:F2},{anchorPx.height:F2}) " +
            $"localPivotInArea=({localPivotInArea.x:F3},{localPivotInArea.y:F3}) " +
            $"localCenterInArea=({localCenterInArea.x:F3},{localCenterInArea.y:F3}) " +
            $"anchorNormInArea=({anchorNormInArea.x:F4},{anchorNormInArea.y:F4}) " +
            $"{iconInfo}");

        if (logFullAnchorHierarchy)
            DumpRectTransformHierarchy($"AnchorHierarchy[{reason}] world={currentWorldIndex} node={nodeIndex}", node.anchor);
    }

    void DumpRectTransformGeometry(string label, RectTransform rt)
    {
        if (rt == null)
        {
            SLog($"{label} | NULL");
            return;
        }

        SLog(
            $"{label} | " +
            $"name='{rt.name}' parent='{(rt.parent != null ? rt.parent.name : "NULL")}' " +
            $"anchorMin=({rt.anchorMin.x:F3},{rt.anchorMin.y:F3}) " +
            $"anchorMax=({rt.anchorMax.x:F3},{rt.anchorMax.y:F3}) " +
            $"pivot=({rt.pivot.x:F3},{rt.pivot.y:F3}) " +
            $"anchored=({rt.anchoredPosition.x:F3},{rt.anchoredPosition.y:F3}) " +
            $"sizeDelta=({rt.sizeDelta.x:F3},{rt.sizeDelta.y:F3}) " +
            $"rect=({rt.rect.xMin:F3},{rt.rect.yMin:F3},{rt.rect.width:F3},{rt.rect.height:F3}) " +
            $"offsetMin=({rt.offsetMin.x:F3},{rt.offsetMin.y:F3}) " +
            $"offsetMax=({rt.offsetMax.x:F3},{rt.offsetMax.y:F3}) " +
            $"localPos=({rt.localPosition.x:F3},{rt.localPosition.y:F3},{rt.localPosition.z:F3}) " +
            $"lossyScale=({rt.lossyScale.x:F4},{rt.lossyScale.y:F4},{rt.lossyScale.z:F4})");
    }

    void DumpRectTransformHierarchy(string label, RectTransform rt)
    {
        if (rt == null)
        {
            SLog($"{label} | NULL");
            return;
        }

        int depth = 0;
        Transform t = rt;

        while (t != null)
        {
            var crt = t as RectTransform;
            if (crt != null)
            {
                SLog(
                    $"{label} | depth={depth} name='{crt.name}' " +
                    $"anchorMin=({crt.anchorMin.x:F3},{crt.anchorMin.y:F3}) " +
                    $"anchorMax=({crt.anchorMax.x:F3},{crt.anchorMax.y:F3}) " +
                    $"pivot=({crt.pivot.x:F3},{crt.pivot.y:F3}) " +
                    $"anchored=({crt.anchoredPosition.x:F3},{crt.anchoredPosition.y:F3}) " +
                    $"sizeDelta=({crt.sizeDelta.x:F3},{crt.sizeDelta.y:F3}) " +
                    $"rect=({crt.rect.xMin:F3},{crt.rect.yMin:F3},{crt.rect.width:F3},{crt.rect.height:F3}) " +
                    $"localPos=({crt.localPosition.x:F3},{crt.localPosition.y:F3},{crt.localPosition.z:F3}) " +
                    $"lossyScale=({crt.lossyScale.x:F4},{crt.lossyScale.y:F4},{crt.lossyScale.z:F4})");
            }
            else
            {
                SLog($"{label} | depth={depth} name='{t.name}' non-RectTransform");
            }

            t = t.parent;
            depth++;
        }
    }

    void DumpWorldCorners(string label, RectTransform rt)
    {
        if (rt == null)
        {
            SLog($"{label} | NULL");
            return;
        }

        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        SLog(
            $"{label} | BL=({corners[0].x:F3},{corners[0].y:F3},{corners[0].z:F3}) " +
            $"TL=({corners[1].x:F3},{corners[1].y:F3},{corners[1].z:F3}) " +
            $"TR=({corners[2].x:F3},{corners[2].y:F3},{corners[2].z:F3}) " +
            $"BR=({corners[3].x:F3},{corners[3].y:F3},{corners[3].z:F3})");
    }

    void CheckResolutionOrScaleChanges()
    {
        if (!logLayoutDiagnosticsOnResolutionChange)
            return;

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

        DumpResolutionSpeedAndAnchorDiagnostics("ResolutionOrScaleChanged");
    }

    void DumpResolutionSpeedAndAnchorDiagnostics(string reason)
    {
        DumpIconSizeDiagnostics(reason);
        DumpAnchorDriftFromCapturedBaseline(reason);

        if (cursor != null)
        {
            Vector2 cursorNorm = GetNormalizedPointInMovementArea(cursor.anchoredPosition);

            SLog(
                $"CursorDiag[{reason}] | " +
                $"cursorAnchored=({cursor.anchoredPosition.x:F3},{cursor.anchoredPosition.y:F3}) " +
                $"cursorNormInArea=({cursorNorm.x:F4},{cursorNorm.y:F4}) " +
                $"cursorRectSize=({cursor.rect.width:F3}x{cursor.rect.height:F3}) " +
                $"cursorSizeDelta=({cursor.sizeDelta.x:F3}x{cursor.sizeDelta.y:F3}) " +
                $"cursorPivot=({cursor.pivot.x:F3},{cursor.pivot.y:F3})");
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