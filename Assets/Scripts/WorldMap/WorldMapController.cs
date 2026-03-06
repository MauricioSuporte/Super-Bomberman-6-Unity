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

    [Header("Debug")]
    [SerializeField] bool enableSurgicalLogs = true;

    [Header("Input Owner")]
    [SerializeField, Range(1, 4)] int ownerPlayerId = 1;

    [Header("Worlds")]
    [SerializeField] List<WorldData> worlds = new List<WorldData>();
    [SerializeField] int startWorldIndex = 0;

    [Header("Cursor")]
    [SerializeField] RectTransform cursor;
    [SerializeField] RectTransform cursorMovementArea;
    [SerializeField] float cursorMoveSpeed = 140f;
    [SerializeField] bool clampCursorInsideArea = true;
    [SerializeField] bool snapCursorToDefaultStageOnStart = true;
    [SerializeField] bool snapCursorToDefaultStageOnWorldChange = true;

    [Header("Stage Detection")]
    [SerializeField] float stageDetectRadius = 18f;
    [SerializeField] bool requireStageInRangeToConfirm = true;

    [Header("Stage Icons")]
    [SerializeField] Sprite unlockedStageSprite;
    [SerializeField] Sprite lockedStageSprite;
    [SerializeField] Vector2 iconSize = new Vector2(8f, 8f);
    [SerializeField] Vector2 iconOffset = Vector2.zero;
    [SerializeField] bool preserveAspectOnIcons = true;
    [SerializeField] bool createIconsOnStart = true;
    [SerializeField] string runtimeIconObjectName = "_StageIcon";

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

    AudioClip lastPlayedWorldMusic;
    float lastPlayedWorldMusicVolume;
    bool lastPlayedWorldMusicLoop;

    void SLog(string msg)
    {
        if (!enableSurgicalLogs) return;
        Debug.Log($"{LOG} {msg}", this);
    }

    void Start()
    {
        Time.timeScale = 1f;
        GamePauseController.ClearPauseFlag();

        if (cursorMovementArea == null)
            cursorMovementArea = transform as RectTransform;

        if (createIconsOnStart)
            EnsureAllStageIcons();

        currentWorldIndex = Mathf.Clamp(startWorldIndex, 0, Mathf.Max(0, worlds.Count - 1));

        ApplyWorldVisibility();
        UpdateAllStageIcons();

        if (snapCursorToDefaultStageOnStart)
            SnapCursorToDefaultStage();

        ClampCursorIfNeeded();
        RefreshHoveredStage();

        if (fadeImage != null)
            StartCoroutine(FadeInRoutine());

        PlayMusicForCurrentWorld(forceRestart: true);

        SLog($"Start | world={currentWorldIndex} hoveredNode={hoveredNodeIndex}");
    }

    void Update()
    {
        if (transitioning)
            return;

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
        {
            StartCoroutine(LoadSceneRoutine(titleSceneName));
        }
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
            cursor.anchoredPosition += move * cursorMoveSpeed * Time.unscaledDeltaTime;

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
        UpdateAllStageIcons();

        if (snapCursorToDefaultStageOnWorldChange)
            SnapCursorToDefaultStage();

        ClampCursorIfNeeded();
        RefreshHoveredStage();
        PlaySfx(changeWorldSfx, changeWorldSfxVolume);
        PlayMusicForCurrentWorld(forceRestart: false);

        SLog($"ChangeWorld | from={oldWorld} to={currentWorldIndex} hoveredNode={hoveredNodeIndex}");
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
        {
            if (worlds[i].root != null)
                worlds[i].root.SetActive(i == currentWorldIndex);
        }
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
            if (node != null)
                SLog($"Hover | world={currentWorldIndex} node={hoveredNodeIndex} displayName='{node.displayName}' scene='{node.sceneName}' unlocked={node.unlocked}");
        }
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

        if (requireStageInRangeToConfirm && bestDist > stageDetectRadius)
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
                EnsureStageIcon(world.nodes[n], w, n);
        }
    }

    void EnsureStageIcon(StageNode node, int worldIndex, int nodeIndex)
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
            rt.sizeDelta = iconSize;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            node.runtimeIcon = go.GetComponent<Image>();
            node.runtimeIcon.raycastTarget = false;
            node.runtimeIcon.preserveAspect = preserveAspectOnIcons;

            SLog($"CreateStageIcon | world={worldIndex} node={nodeIndex} anchor='{node.anchor.name}'");
        }
        else
        {
            var rt = node.runtimeIcon.rectTransform;
            rt.SetParent(node.anchor, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = iconOffset;
            rt.sizeDelta = iconSize;
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

                EnsureStageIcon(node, w, n);
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
        rt.sizeDelta = iconSize;
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
        if (world == null)
        {
            SLog("PlayMusicForCurrentWorld aborted | current world is NULL");
            return;
        }

        if (GameMusicController.Instance == null)
        {
            SLog("PlayMusicForCurrentWorld aborted | GameMusicController.Instance is NULL");
            return;
        }

        if (world.worldMusic == null)
        {
            SLog($"PlayMusicForCurrentWorld | world={currentWorldIndex} '{world.worldName}' has no music clip");
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
        {
            SLog($"PlayMusicForCurrentWorld skipped | world={currentWorldIndex} '{world.worldName}' music already playing");
            return;
        }

        GameMusicController.Instance.PlayMusic(world.worldMusic, world.worldMusicVolume, world.loopWorldMusic);

        lastPlayedWorldMusic = world.worldMusic;
        lastPlayedWorldMusicVolume = world.worldMusicVolume;
        lastPlayedWorldMusicLoop = world.loopWorldMusic;

        SLog($"PlayMusicForCurrentWorld | world={currentWorldIndex} '{world.worldName}' clip='{world.worldMusic.name}' volume={world.worldMusicVolume:F2} loop={world.loopWorldMusic}");
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
}