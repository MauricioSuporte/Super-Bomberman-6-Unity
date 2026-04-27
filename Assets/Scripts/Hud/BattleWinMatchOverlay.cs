using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public sealed class BattleWinMatchOverlay : MonoBehaviour
{
    const float ScreenWidth = 256f;
    const float ScreenHeight = 224f;
    const float FieldHeight = 65f;
    const float TileSize = 16f;
    const float VictoryBomberWidth = 94f;
    const float VictoryBomberHeight = 117f;
    const float TotalDuration = 10f;
    const float FadeInDuration = 1f;
    const float WinnerEntranceDelayAfterFadeIn = 1f;
    const float WinnerEntranceDuration = 0.55f;
    const float FinalFadeDuration = 1f;
    const int CrowdPaletteCount = 16;
    const string PrefabResourcesPath = "HUD/WinMatch/BattleWinMatchOverlay";
    const string BackgroundResourcesPath = "HUD/WinMatch/WinMatchBackground";
    const string RecoloredFrameResourcesPathFormat = "HUD/WinMatch/Recolors/Bomber{0:00}_{1}";
    const string VictoryBomberResourcesPathFormat = "HUD/WinMatch/VictoryRecolors/BomberVictory{0:00}";
    const string VictoryBomberFallbackResourcesPath = "HUD/WinMatch/BomberVictory";
    const string SafeFrameName = "SafeFrame4x3";
    static readonly string[] BlueFrameResourcesPaths =
    {
        "HUD/WinMatch/Blue1",
        "HUD/WinMatch/Blue2",
        "HUD/WinMatch/Blue3",
        "HUD/WinMatch/Blue4",
        "HUD/WinMatch/Blue5"
    };
    static readonly int[] WaveFrames = { 1, 2, 3, 3, 3, 4, 5, 5, 4, 3, 3, 2, 1 };
    static readonly float[] WaveOffsets = { 0f, 1f, 2f, 3f, 4f, 5f, 6f, 5f, 4f, 3f, 2f, 1f, 0f };

    [Header("Win Match Audio")]
    [SerializeField] private AudioClip winMatchMusic;
    [SerializeField, Range(0f, 1f)] private float winMatchMusicVolume = 1f;

    [Header("Crowd Wave")]
    [SerializeField, Min(0.01f)] private float waveStepDuration = 0.2f;
    [SerializeField, Min(0f)] private float columnDelay = 0.13f;

    readonly List<ColumnUi> columns = new();
    readonly List<int> winnerPlayerIds = new(GameSession.MaxPlayerId);
    readonly List<WinnerBomberUi> winnerBomberUis = new(GameSession.MaxPlayerId);
    RectTransform rootRect;
    RectTransform runtimeRoot;
    RectMask2D runtimeMask;
    CanvasGroup canvasGroup;
    Sprite backgroundSprite;
    Sprite[] blueFrames;
    Sprite[][] recoloredFrames;
    Sprite[] victoryBomberSprites;
    Sprite fallbackVictoryBomberSprite;

    sealed class ColumnUi
    {
        public int PaletteIndex;
        public Image[] Images;
        public Vector2[] BasePositions;
    }

    sealed class WinnerBomberUi
    {
        public RectTransform Rect;
        public Vector2 StartPosition;
        public Vector2 TargetPosition;
    }

    public static IEnumerator PlayRoutine(int winnerPlayerId)
    {
        BattleWinMatchOverlay overlay = CreateOverlay();
        if (overlay == null)
            yield break;

        overlay.ConfigureWinners(winnerPlayerId);
        yield return overlay.Play();
    }

    static BattleWinMatchOverlay CreateOverlay()
    {
        GameObject prefab = Resources.Load<GameObject>(PrefabResourcesPath);
        RectTransform safeFrame = ResolveSafeFrame();
        Transform parent = safeFrame != null ? safeFrame : ResolveCanvasTransform();

        GameObject instance = prefab != null
            ? Instantiate(prefab, parent, false)
            : new GameObject("BattleWinMatchOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(BattleWinMatchOverlay));

        if (prefab == null)
            instance.transform.SetParent(parent, false);

        instance.name = "BattleWinMatchOverlay";

        if (safeFrame != null)
            safeFrame.SetAsLastSibling();

        instance.transform.SetAsLastSibling();

        BattleWinMatchOverlay overlay = instance.GetComponent<BattleWinMatchOverlay>();
        if (overlay == null)
            overlay = instance.AddComponent<BattleWinMatchOverlay>();

        if (instance.GetComponent<CanvasGroup>() == null)
            instance.AddComponent<CanvasGroup>();

        return overlay;
    }

    static RectTransform ResolveSafeFrame()
    {
        Transform canvasTransform = ResolveCanvasTransform();
        if (canvasTransform == null)
            return null;

        RectTransform existing = FindSafeFrame(canvasTransform);
        if (existing != null)
            return existing;

        GameObject go = new GameObject(SafeFrameName, typeof(RectTransform), typeof(UICameraViewportFitter));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(canvasTransform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
        return rect;
    }

    static RectTransform FindSafeFrame(Transform canvasTransform)
    {
        if (canvasTransform == null)
            return null;

        Transform direct = canvasTransform.Find(SafeFrameName);
        if (direct is RectTransform directRect)
            return directRect;

        UICameraViewportFitter[] fitters = canvasTransform.GetComponentsInChildren<UICameraViewportFitter>(true);
        for (int i = 0; i < fitters.Length; i++)
        {
            if (fitters[i] != null && fitters[i].transform is RectTransform rect)
                return rect;
        }

        return null;
    }

    static Transform ResolveCanvasTransform()
    {
        if (StageIntroTransition.Instance != null && StageIntroTransition.Instance.fadeImage != null)
            return StageIntroTransition.Instance.fadeImage.canvas.transform;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas != null)
            return canvas.transform;

        GameObject go = new GameObject("BattleWinMatchOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas createdCanvas = go.GetComponent<Canvas>();
        createdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        createdCanvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ScreenWidth, ScreenHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 16f;

        return go.transform;
    }

    IEnumerator Play()
    {
        rootRect = (RectTransform)transform;
        canvasGroup = GetComponent<CanvasGroup>();
        RectTransform parentRect = transform.parent as RectTransform;

        EnsureSpritesLoaded();
        BuildUi();
        ConfigureRoot(parentRect);
        PlayWinMatchMusic();

        yield return AnimateWave(TotalDuration);
    }

    void EnsureSpritesLoaded()
    {
        backgroundSprite = LoadFirstSprite(BackgroundResourcesPath);
        blueFrames = new Sprite[BlueFrameResourcesPaths.Length];

        for (int i = 0; i < BlueFrameResourcesPaths.Length; i++)
            blueFrames[i] = LoadFirstSprite(BlueFrameResourcesPaths[i]);

        recoloredFrames = new Sprite[CrowdPaletteCount][];
        for (int paletteIndex = 0; paletteIndex < CrowdPaletteCount; paletteIndex++)
        {
            recoloredFrames[paletteIndex] = new Sprite[BlueFrameResourcesPaths.Length];
            for (int frame = 1; frame <= BlueFrameResourcesPaths.Length; frame++)
            {
                string path = string.Format(RecoloredFrameResourcesPathFormat, paletteIndex, frame);
                recoloredFrames[paletteIndex][frame - 1] = LoadFirstSprite(path);
            }
        }

        fallbackVictoryBomberSprite = LoadFirstSprite(VictoryBomberFallbackResourcesPath);
        victoryBomberSprites = new Sprite[CrowdPaletteCount];
        for (int paletteIndex = 0; paletteIndex < CrowdPaletteCount; paletteIndex++)
        {
            string path = string.Format(VictoryBomberResourcesPathFormat, paletteIndex);
            victoryBomberSprites[paletteIndex] = LoadFirstSprite(path);
        }
    }

    static Sprite LoadFirstSprite(string resourcesPath)
    {
        Sprite sprite = Resources.Load<Sprite>(resourcesPath);
        if (sprite != null)
            return sprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcesPath);
        return sprites != null && sprites.Length > 0 ? sprites[0] : null;
    }

    void BuildUi()
    {
        EnsureRuntimeRoot();
        ClearRuntimeChildren();
        columns.Clear();
        winnerBomberUis.Clear();

        Image background = CreateImage("WinMatchBackground", backgroundSprite);
        ApplyLogicalRect(background.rectTransform, 0f, 0f, ScreenWidth, ScreenHeight, ScreenWidth, ScreenHeight);
        background.rectTransform.SetAsFirstSibling();

        BuildCrowd();
        BuildVictoryBombers();
    }

    void EnsureRuntimeRoot()
    {
        Transform existing = transform.Find("__BattleWinMatchRuntime");
        runtimeRoot = existing as RectTransform;
        if (runtimeRoot == null)
        {
            GameObject go = new GameObject("__BattleWinMatchRuntime", typeof(RectTransform));
            runtimeRoot = go.GetComponent<RectTransform>();
            runtimeRoot.SetParent(transform, false);
        }

        ApplyLogicalRect(runtimeRoot, 0f, 0f, ScreenWidth, ScreenHeight, ScreenWidth, ScreenHeight);

        runtimeMask = runtimeRoot.GetComponent<RectMask2D>();
        if (runtimeMask == null)
            runtimeMask = runtimeRoot.gameObject.AddComponent<RectMask2D>();
    }

    void ClearRuntimeChildren()
    {
        if (runtimeRoot == null)
            return;

        for (int i = runtimeRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = runtimeRoot.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    void BuildCrowd()
    {
        int columnCount = Mathf.FloorToInt(ScreenWidth / TileSize);
        int rowCount = Mathf.CeilToInt((ScreenHeight - FieldHeight) / TileSize);
        float bottom = FieldHeight;

        for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            ColumnUi column = new ColumnUi
            {
                PaletteIndex = columnIndex % CrowdPaletteCount,
                Images = new Image[rowCount],
                BasePositions = new Vector2[rowCount]
            };

            float left = columnIndex * TileSize;
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                Image image = CreateImage("Bomber_" + columnIndex + "_" + rowIndex, GetCrowdFrame(column.PaletteIndex, 1));
                ApplyLogicalRect(image.rectTransform, left, bottom + (rowIndex * TileSize), TileSize, TileSize, ScreenWidth, ScreenHeight);
                column.Images[rowIndex] = image;
                column.BasePositions[rowIndex] = image.rectTransform.anchoredPosition;
            }

            columns.Add(column);
        }
    }

    void ConfigureWinners(int winnerPlayerId)
    {
        winnerPlayerIds.Clear();

        if (!GameSession.IsValidPlayerId(winnerPlayerId))
            return;

        if (BattleModeRules.Instance != null && BattleModeRules.Instance.UsesTeams)
        {
            BattleModeRules.TeamId winningTeam = BattleModeRules.Instance.GetTeamForPlayer(winnerPlayerId);
            List<int> activePlayerIds = new(GameSession.MaxPlayerId);
            if (GameSession.Instance != null)
                GameSession.Instance.GetActivePlayerIds(activePlayerIds);

            for (int i = 0; i < activePlayerIds.Count; i++)
            {
                int playerId = activePlayerIds[i];
                if (BattleModeRules.Instance.GetTeamForPlayer(playerId) == winningTeam)
                    winnerPlayerIds.Add(playerId);
            }
        }
        else
        {
            winnerPlayerIds.Add(winnerPlayerId);
        }

        if (winnerPlayerIds.Count <= 0)
            winnerPlayerIds.Add(winnerPlayerId);
    }

    void BuildVictoryBombers()
    {
        if (winnerPlayerIds.Count <= 0)
            return;

        for (int i = 0; i < winnerPlayerIds.Count; i++)
        {
            int playerId = winnerPlayerIds[i];
            int paletteIndex = GetVictoryPaletteIndex(playerId);
            Image image = CreateImage("WinnerBomber_" + playerId, GetVictoryBomberSprite(paletteIndex));
            Vector2 targetPosition = GetWinnerBomberTargetPosition(i, winnerPlayerIds.Count);
            Vector2 startPosition = GetWinnerBomberStartPosition(targetPosition);

            ConfigureWinnerBomberRect(image.rectTransform, startPosition);
            image.rectTransform.SetAsLastSibling();

            winnerBomberUis.Add(new WinnerBomberUi
            {
                Rect = image.rectTransform,
                StartPosition = startPosition,
                TargetPosition = targetPosition
            });
        }
    }

    static Vector2 GetWinnerBomberTargetPosition(int index, int count)
    {
        float centerY = GetWinnerBomberCenterY();

        if (count <= 1)
            return new Vector2(0f, centerY);

        float minCenterX = (VictoryBomberWidth * 0.5f) - (ScreenWidth * 0.5f);
        float maxCenterX = (ScreenWidth * 0.5f) - (VictoryBomberWidth * 0.5f);
        float t = index / (float)(count - 1);
        float centerX = Mathf.Lerp(minCenterX, maxCenterX, t);
        return new Vector2(Mathf.Round(centerX), centerY);
    }

    static Vector2 GetWinnerBomberStartPosition(Vector2 targetPosition)
    {
        float outsideLeftCenterX = (-ScreenWidth * 0.5f) - (VictoryBomberWidth * 0.5f);
        return new Vector2(outsideLeftCenterX, targetPosition.y);
    }

    static float GetWinnerBomberCenterY()
    {
        return (VictoryBomberHeight * 0.5f) - (ScreenHeight * 0.5f);
    }

    static void ConfigureWinnerBomberRect(RectTransform rect, Vector2 anchoredPosition)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(VictoryBomberWidth, VictoryBomberHeight);
        rect.anchoredPosition = anchoredPosition;
        rect.localScale = Vector3.one;
    }

    IEnumerator AnimateWave(float duration)
    {
        float elapsed = 0f;
        bool finalFadeStarted = false;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            for (int i = 0; i < columns.Count; i++)
            {
                int sequenceIndex = GetSequenceIndexForColumn(elapsed, i);
                ApplyColumnFrame(columns[i], WaveFrames[sequenceIndex], WaveOffsets[sequenceIndex]);
            }

            if (canvasGroup != null)
            {
                if (elapsed < FadeInDuration)
                    canvasGroup.alpha = Mathf.Clamp01(elapsed / FadeInDuration);
                else
                    canvasGroup.alpha = 1f;
            }

            UpdateWinnerBomberEntrance(elapsed);

            if (!finalFadeStarted && elapsed >= duration - FinalFadeDuration)
            {
                finalFadeStarted = true;
                if (StageIntroTransition.Instance != null)
                    StageIntroTransition.Instance.StartFadeOut(FinalFadeDuration, false);
            }

            yield return null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    void UpdateWinnerBomberEntrance(float elapsed)
    {
        float entranceStart = FadeInDuration + WinnerEntranceDelayAfterFadeIn;
        float progress = Mathf.Clamp01((elapsed - entranceStart) / WinnerEntranceDuration);
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);

        for (int i = 0; i < winnerBomberUis.Count; i++)
        {
            WinnerBomberUi ui = winnerBomberUis[i];
            if (ui?.Rect == null)
                continue;

            ui.Rect.anchoredPosition = Vector2.Lerp(ui.StartPosition, ui.TargetPosition, easedProgress);
        }
    }

    int GetSequenceIndexForColumn(float elapsed, int columnIndex)
    {
        float localTime = elapsed - (columnIndex * columnDelay);
        if (localTime < 0f)
            return 0;

        float stepDuration = Mathf.Max(0.01f, waveStepDuration);
        return Mathf.FloorToInt(localTime / stepDuration) % WaveFrames.Length;
    }

    void ApplyColumnFrame(ColumnUi column, int frame, float yOffset)
    {
        if (column == null || column.Images == null)
            return;

        Sprite sprite = GetCrowdFrame(column.PaletteIndex, frame);

        for (int i = 0; i < column.Images.Length; i++)
        {
            Image image = column.Images[i];
            if (image == null)
                continue;

            image.sprite = sprite;
            image.rectTransform.anchoredPosition = column.BasePositions[i] + new Vector2(0f, yOffset);
        }
    }

    Sprite GetCrowdFrame(int paletteIndex, int frame)
    {
        int frameIndex = Mathf.Clamp(frame - 1, 0, blueFrames != null ? blueFrames.Length - 1 : 0);

        if (recoloredFrames != null && recoloredFrames.Length > 0)
        {
            int clampedPaletteIndex = Mathf.Abs(paletteIndex) % recoloredFrames.Length;
            Sprite[] paletteFrames = recoloredFrames[clampedPaletteIndex];
            if (paletteFrames != null && frameIndex < paletteFrames.Length && paletteFrames[frameIndex] != null)
                return paletteFrames[frameIndex];
        }

        return GetBlueFrame(frameIndex);
    }

    Sprite GetBlueFrame(int frameIndex)
    {
        if (blueFrames == null || blueFrames.Length <= 0)
            return null;

        return blueFrames[Mathf.Clamp(frameIndex, 0, blueFrames.Length - 1)];
    }

    Sprite GetVictoryBomberSprite(int paletteIndex)
    {
        if (victoryBomberSprites != null && victoryBomberSprites.Length > 0)
        {
            int clampedPaletteIndex = Mathf.Abs(paletteIndex) % victoryBomberSprites.Length;
            if (victoryBomberSprites[clampedPaletteIndex] != null)
                return victoryBomberSprites[clampedPaletteIndex];
        }

        return fallbackVictoryBomberSprite;
    }

    int GetVictoryPaletteIndex(int playerId)
    {
        BomberSkin skin = PlayerPersistentStats.Get(playerId).Skin;
        return Mathf.Abs(GetPortraitIndex(skin)) % CrowdPaletteCount;
    }

    static int GetPortraitIndex(BomberSkin skin)
    {
        switch (skin)
        {
            case BomberSkin.White: return 0;
            case BomberSkin.Black: return 1;
            case BomberSkin.Red: return 2;
            case BomberSkin.Blue: return 3;
            case BomberSkin.Green: return 4;
            case BomberSkin.Yellow: return 5;
            case BomberSkin.Pink: return 6;
            case BomberSkin.Aqua: return 7;
            case BomberSkin.Orange: return 8;
            case BomberSkin.Purple: return 9;
            case BomberSkin.Gray: return 10;
            case BomberSkin.Olive: return 11;
            case BomberSkin.DarkGreen: return 12;
            case BomberSkin.Cyan: return 13;
            case BomberSkin.DarkBlue: return 14;
            case BomberSkin.Brown: return 15;
            default: return 3;
        }
    }

    void ConfigureRoot(RectTransform parentRect)
    {
        float uiScale = GetPixelPerfectUiScale(parentRect);
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = new Vector2(ScreenWidth, ScreenHeight);
        rootRect.localScale = new Vector3(uiScale, uiScale, 1f);

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
    }

    void PlayWinMatchMusic()
    {
        if (GameMusicController.Instance == null)
            return;

        GameMusicController.Instance.StopMusic();
        if (winMatchMusic != null)
            GameMusicController.Instance.PlayMusic(winMatchMusic, winMatchMusicVolume, false);
    }

    Image CreateImage(string childName, Sprite sprite)
    {
        GameObject go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(runtimeRoot, false);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.sprite = sprite;
        image.preserveAspect = false;
        image.color = Color.white;
        return image;
    }

    static void ApplyLogicalRect(
        RectTransform rect,
        float left,
        float bottom,
        float width,
        float height,
        float logicalParentWidth,
        float logicalParentHeight)
    {
        if (rect == null || logicalParentWidth <= 0f || logicalParentHeight <= 0f)
            return;

        rect.anchorMin = new Vector2(left / logicalParentWidth, bottom / logicalParentHeight);
        rect.anchorMax = new Vector2((left + width) / logicalParentWidth, (bottom + height) / logicalParentHeight);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    static float GetPixelPerfectUiScale(RectTransform parentRect)
    {
        float parentWidth = parentRect != null ? parentRect.rect.width : 0f;
        float parentHeight = parentRect != null ? parentRect.rect.height : 0f;

        if (parentWidth <= 0f || parentHeight <= 0f)
        {
            Rect cameraPixelRect = Camera.main != null
                ? Camera.main.pixelRect
                : new Rect(0f, 0f, Screen.width, Screen.height);

            parentWidth = cameraPixelRect.width;
            parentHeight = cameraPixelRect.height;
        }

        float rawScale = Mathf.Min(parentWidth / ScreenWidth, parentHeight / ScreenHeight);
        float integerScale = Mathf.Max(1f, Mathf.Round(rawScale));
        return integerScale;
    }
}
