using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class BomberSkinSelectMenu : MonoBehaviour
{
    [Header("Auto Fix Layout")]
    [SerializeField] bool forceRootPanelStretchToParent = false;
    [SerializeField] bool forceBackgroundStretchToRootPanel = false;

    [Header("UI")]
    [SerializeField] GameObject root;
    [SerializeField] Image backgroundImage;

    [Header("Fade")]
    [SerializeField] Image fadeImage;
    [SerializeField] float fadeDuration = 1f;
    [SerializeField] float fadeOutOnConfirmDuration = 2f;

    [Header("Grid")]
    [SerializeField] Transform gridRoot;
    [SerializeField] Image skinItemPrefab;
    [SerializeField] int columns = 4;
    [SerializeField] Vector2 cellSize = new(120f, 120f);
    [SerializeField] Vector2 spacing = new(16f, 16f);
    [SerializeField] Color lockedTint = new(1f, 1f, 1f, 0.35f);
    [SerializeField] Color normalTint = Color.white;
    [SerializeField] Color selectedTint = Color.white;

    [Header("Cursor Prefab (RectTransform)")]
    [SerializeField] RectTransform skinCursorPrefab;

    [Header("Cursor Per Player")]
    [SerializeField] bool staggerCursorStartByPlayer = true;

    [Tooltip("Optional: override cursor sprite per player (index 0 = P1, 1 = P2, 2 = P3, 3 = P4).")]
    [SerializeField] Sprite[] cursorSpriteByPlayer = new Sprite[4];
    [SerializeField] Vector2 cursorPadding = new(18f, 18f);
    [SerializeField] Vector2 cursorSizeMultiplier = new(0.9f, 0.9f);
    [SerializeField] float cursorYOffset = 8f;

    [Header("Cursor Blink (Idle)")]
    [SerializeField] bool cursorBlinkWhileNotConfirmed = true;
    [SerializeField] float cursorBlinkSpeed = 5.5f;
    [SerializeField, Range(0f, 1f)] float cursorBlinkMinAlpha = 0.25f;
    [SerializeField, Range(0f, 1f)] float cursorBlinkMaxAlpha = 1f;

    [Header("Background Sprite")]
    [SerializeField] Sprite[] backgroundSprites = new Sprite[2];
    [SerializeField] float backgroundSwapInterval = 2f;
    [SerializeField] bool backgroundSwapLoop = true;

    int _backgroundSpriteIndex;
    float _backgroundSwapTimer;

    [Header("Resources")]
    [SerializeField] string spritesResourcesPath = "Sprites/Bombers/Bomberman";
    [SerializeField] int idleFrameIndex = 16;

    [Header("Preview Animations")]
    [SerializeField] float downFrameTime = 0.22f;
    [SerializeField] float endStageFrameTime = 0.1f;
    [SerializeField] int[] downFrames = new[] { 14, 16, 18, 16 };
    [SerializeField] int[] endStageFrames = new[] { 148, 148, 146, 148, 147, 148, 146, 148, 147, 147 };

    [Header("EndStage Offset + Stop")]
    [SerializeField] float endStageYOffset = 10f;
    [SerializeField] int endStageLoopsToStop = 1;

    [Header("Music")]
    [SerializeField] AudioClip selectMusic;
    [SerializeField, Range(0f, 1f)] float selectMusicVolume = 1f;
    [SerializeField] bool loopSelectMusic = true;

    [Header("SFX")]
    [SerializeField] AudioClip moveCursorSfx;
    [SerializeField, Range(0f, 1f)] float moveCursorSfxVolume = 1f;
    [SerializeField] AudioClip confirmSfx;
    [SerializeField, Range(0f, 1f)] float confirmSfxVolume = 1f;
    [SerializeField] AudioClip lockedConfirmSfx;
    [SerializeField, Range(0f, 1f)] float lockedConfirmSfxVolume = 1f;

    [Header("SFX (Return)")]
    [FormerlySerializedAs("backToTitleSfx")]
    [SerializeField] AudioClip returnSfx;
    [FormerlySerializedAs("backToTitleSfxVolume")]
    [SerializeField, Range(0f, 1f)] float returnSfxVolume = 1f;

    [Header("Reference Frame (SafeFrame4x3)")]
    [SerializeField] RectTransform referenceRect;

    [Header("Layout Root")]
    [SerializeField] RectTransform rootPanel;

    [Header("Dynamic Scale (Pixel Perfect SNES)")]
    [SerializeField] bool dynamicScale = true;
    [SerializeField] int referenceWidth = 256;
    [SerializeField] int referenceHeight = 224;
    [SerializeField] bool useIntegerUpscale = true;
    [SerializeField, Min(1)] int designUpscale = 4;
    [SerializeField, Min(0.01f)] float extraScaleMultiplier = 1f;
    [SerializeField, Min(0.01f)] float minScale = 0.5f;
    [SerializeField, Min(0.01f)] float maxScale = 10f;

    [Header("Grid Position")]
    [SerializeField] Vector2 gridBaseAnchoredPos = new(0f, -10f);

    float _currentUiScale = 1f;
    int _currentBaseScaleInt = 1;

    int _lastScreenW = -1;
    int _lastScreenH = -1;
    Rect _lastRefPixelRect;
    Rect _lastCameraRect;

    Vector2 _baseCellSize;
    Vector2 _baseSpacing;
    Vector2 _baseCursorPadding;
    float _baseCursorYOffset;
    float _baseEndStageYOffset;
    bool _baseValuesCaptured;

    Coroutine fadeInCoroutine;
    public bool ReturnToTitleRequested { get; private set; }

    [Header("Skins (menu order)")]
    [SerializeField]
    List<BomberSkin> selectableSkins = new()
    {
        BomberSkin.Golden,
        BomberSkin.White,
        BomberSkin.Gray,
        BomberSkin.Black,
        BomberSkin.Red,
        BomberSkin.Orange,
        BomberSkin.Yellow,
        BomberSkin.Lime,
        BomberSkin.Green,
        BomberSkin.Cyan,
        BomberSkin.Aqua,
        BomberSkin.Blue,
        BomberSkin.DarkBlue,
        BomberSkin.Purple,
        BomberSkin.Magenta,
        BomberSkin.Pink,
        BomberSkin.Brown,
        BomberSkin.DarkGreen,
        BomberSkin.Nightmare,
        BomberSkin.Gold
    };

    readonly Dictionary<BomberSkin, Sprite> idleCache = new();
    readonly Dictionary<BomberSkin, Dictionary<int, Sprite>> sheetFrameCache = new();

    readonly List<RectTransform> slotRoots = new();
    readonly List<Image> slotImages = new();

    AudioClip previousClip;
    float previousVolume;
    bool previousLoop;
    bool capturedPreviousMusic;

    float downTimer;
    int downFrameIdx;

    sealed class EndStageState
    {
        public int slotIndex;
        public BomberSkin skin;
        public float timer;
        public int frameIdx;
        public int loopsDone;
        public bool stopped;
        public Vector2 baseAnchoredPos;
        public bool baseCaptured;
    }

    readonly Dictionary<int, EndStageState> endStageBySlot = new();

    float cursorBlinkT;
    int overlapZTick;

    int[] selectedBySlot;

    sealed class PlayerCursorState
    {
        public int playerId;
        public int index;
        public bool confirmed;
        public BomberSkin selected;
        public int selectedIndex = -1;
        public RectTransform cursorRt;
        public Image cursorImg;
    }

    readonly List<PlayerCursorState> players = new();

    void Awake()
    {
        if (root == null)
            root = gameObject;

        CaptureBaseValuesIfNeeded();
        ApplyAutoFixesIfEnabled();

        ApplyCurrentBackgroundSprite();

        BuildGrid();
        ApplyDynamicScaleIfNeeded(true);

        if (root != null)
            root.SetActive(false);
    }

    void Update()
    {
        if (!menuActive)
            return;

        ApplyDynamicScaleIfNeeded(false);

        TickBackgroundSpriteSwap();
        TickCursorBlink();
        TickDownClock();
        TickEndStageClocks();
        UpdateSlotVisuals();
    }

    void LateUpdate()
    {
        if (!menuActive)
            return;

        Canvas.ForceUpdateCanvases();
        UpdateAllCursorsToSelected();
        CycleOverlappedCursors();
    }

    bool menuActive;

    public void Hide()
    {
        menuActive = false;

        RestoreAllSlotPositions();
        endStageBySlot.Clear();

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].cursorRt != null)
                Destroy(players[i].cursorRt.gameObject);
        }

        players.Clear();

        if (root != null) root.SetActive(false);
        else gameObject.SetActive(false);
    }

    public IEnumerator SelectSkinRoutine()
    {
        if (root == null)
            root = gameObject;

        root.transform.SetAsLastSibling();
        root.SetActive(true);

        ApplyAutoFixesIfEnabled();
        ApplyDynamicScaleIfNeeded(true);

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.transform.SetAsLastSibling();
            SetFadeAlpha(1f);
        }

        ResetBackgroundSpriteSwap();
        ApplyCurrentBackgroundSprite();

        if (slotImages.Count != selectableSkins.Count)
            BuildGrid();

        PlayerPersistentStats.EnsureSessionBooted();

        ReturnToTitleRequested = false;

        downTimer = 0f;
        downFrameIdx = 0;

        cursorBlinkT = 0f;
        overlapZTick = 0;

        RestoreAllSlotPositions();
        endStageBySlot.Clear();

        selectedBySlot = new int[selectableSkins.Count];
        for (int i = 0; i < selectedBySlot.Length; i++)
            selectedBySlot[i] = 0;

        StartSelectMusic();

        var input = PlayerInputManager.Instance;

        int count = 1;
        if (GameSession.Instance != null)
            count = GameSession.Instance.ActivePlayerCount;

        BuildPlayerCursors(count);

        for (int p = 1; p <= count; p++)
        {
            while (input.Get(p, PlayerAction.ActionA) ||
                   input.Get(p, PlayerAction.ActionB) ||
                   input.Get(p, PlayerAction.Start) ||
                   input.Get(p, PlayerAction.MoveLeft) ||
                   input.Get(p, PlayerAction.MoveRight) ||
                   input.Get(p, PlayerAction.MoveUp) ||
                   input.Get(p, PlayerAction.MoveDown))
                yield return null;
        }

        yield return null;

        PreloadIdleSprites();

        for (int i = 0; i < players.Count; i++)
        {
            int pid = players[i].playerId;
            int idx;

            if (staggerCursorStartByPlayer)
                idx = (pid - 1) % selectableSkins.Count;
            else
            {
                idx = selectableSkins.IndexOf(PlayerPersistentStats.Get(pid).Skin);
                if (idx < 0) idx = 0;
            }

            players[i].index = idx;
            players[i].selected = PlayerPersistentStats.Get(pid).Skin;
            players[i].confirmed = false;
            players[i].selectedIndex = -1;
        }

        UpdateSlotVisuals();

        Canvas.ForceUpdateCanvases();
        yield return null;

        menuActive = true;

        if (fadeInCoroutine != null)
            StopCoroutine(fadeInCoroutine);

        fadeInCoroutine = StartCoroutine(FadeInRoutine());

        bool done = false;
        while (!done)
        {
            bool anyReturnToTitle = false;

            for (int i = 0; i < players.Count; i++)
            {
                var ps = players[i];
                int pid = ps.playerId;

                bool upDown = input.GetDown(pid, PlayerAction.MoveUp);
                bool downDown = input.GetDown(pid, PlayerAction.MoveDown);
                bool leftDown = input.GetDown(pid, PlayerAction.MoveLeft);
                bool rightDown = input.GetDown(pid, PlayerAction.MoveRight);
                bool aDown = input.GetDown(pid, PlayerAction.ActionA);
                bool bDown = input.GetDown(pid, PlayerAction.ActionB);
                bool startDown = input.GetDown(pid, PlayerAction.Start);

                if (ps.confirmed && bDown)
                {
                    DeselectPlayer(pid);
                    PlaySfx(returnSfx, returnSfxVolume);
                    UpdateSlotVisuals();
                    continue;
                }

                if (!ps.confirmed)
                {
                    bool moved = false;
                    int nextIndex = ps.index;

                    if (leftDown) { nextIndex = MoveLeftWrap(ps.index); moved = true; }
                    else if (rightDown) { nextIndex = MoveRightWrap(ps.index); moved = true; }
                    else if (upDown) { nextIndex = MoveUpWrap(ps.index); moved = true; }
                    else if (downDown) { nextIndex = MoveDownWrap(ps.index); moved = true; }

                    bool confirmPressed = startDown || aDown;
                    bool backPressed = bDown;

                    if (moved)
                    {
                        if (nextIndex != ps.index)
                        {
                            ps.index = nextIndex;
                            PlaySfx(moveCursorSfx, moveCursorSfxVolume);
                            UpdateSlotVisuals();
                        }
                    }
                    else if (backPressed)
                    {
                        anyReturnToTitle = true;
                    }
                    else if (confirmPressed)
                    {
                        TryConfirm(pid);
                        UpdateSlotVisuals();

                        if (AllPlayersConfirmed())
                        {
                            fadeDuration = fadeOutOnConfirmDuration;
                            done = true;
                            break;
                        }
                    }
                }
            }

            if (anyReturnToTitle)
            {
                ReturnToTitleRequested = true;
                PlaySfx(returnSfx, returnSfxVolume);
                done = true;
            }

            yield return null;
        }

        if (fadeInCoroutine != null)
        {
            StopCoroutine(fadeInCoroutine);
            fadeInCoroutine = null;
        }

        yield return FadeOutRoutine();

        StopSelectMusicAndRestorePrevious(restorePrevious: ReturnToTitleRequested);
        Hide();

        if (fadeImage != null)
            fadeImage.gameObject.SetActive(false);
    }

    public BomberSkin GetSelectedSkin(int playerId)
    {
        playerId = Mathf.Clamp(playerId, 1, 4);

        for (int i = 0; i < players.Count; i++)
            if (players[i].playerId == playerId)
                return players[i].selected;

        return PlayerPersistentStats.Get(playerId).Skin;
    }

    void TryConfirm(int playerId)
    {
        var ps = GetPlayerState(playerId);
        if (ps == null) return;

        int slot = Mathf.Clamp(ps.index, 0, selectableSkins.Count - 1);
        var skin = selectableSkins[slot];

        if (!BomberSkinUnlockProgress.IsUnlocked(skin))
        {
            PlaySfx(lockedConfirmSfx, lockedConfirmSfxVolume);
            return;
        }

        int owner = GetSelectedOwner(slot);
        if (owner != 0 && owner != playerId)
        {
            PlaySfx(lockedConfirmSfx, lockedConfirmSfxVolume);
            return;
        }

        selectedBySlot[slot] = playerId;

        ps.selected = skin;
        ps.selectedIndex = slot;
        ps.confirmed = true;

        PlayerPersistentStats.Get(playerId).Skin = skin;
        PlayerPersistentStats.SaveSelectedSkin(playerId);

        StartEndStageForSlot(slot, skin);

        if (ps.cursorImg != null)
        {
            var c = ps.cursorImg.color;
            c.a = 1f;
            ps.cursorImg.color = c;
        }

        PlaySfx(confirmSfx, confirmSfxVolume);
    }

    void DeselectPlayer(int playerId)
    {
        var ps = GetPlayerState(playerId);
        if (ps == null) return;

        if (!ps.confirmed || ps.selectedIndex < 0)
            return;

        int slot = ps.selectedIndex;

        if (slot >= 0 && slot < selectedBySlot.Length && selectedBySlot[slot] == playerId)
            selectedBySlot[slot] = 0;

        StopEndStageForSlot(slot);

        ps.confirmed = false;
        ps.selectedIndex = -1;
    }

    PlayerCursorState GetPlayerState(int playerId)
    {
        for (int i = 0; i < players.Count; i++)
            if (players[i].playerId == playerId)
                return players[i];

        return null;
    }

    int GetSelectedOwner(int slotIndex)
    {
        if (selectedBySlot == null || slotIndex < 0 || slotIndex >= selectedBySlot.Length)
            return 0;

        return selectedBySlot[slotIndex];
    }

    bool IsSlotSelected(int slotIndex) => GetSelectedOwner(slotIndex) != 0;

    void StartEndStageForSlot(int slotIndex, BomberSkin skin)
    {
        if (!endStageBySlot.TryGetValue(slotIndex, out var st) || st == null)
        {
            st = new EndStageState();
            endStageBySlot[slotIndex] = st;
        }

        st.slotIndex = slotIndex;
        st.skin = skin;
        st.timer = 0f;
        st.frameIdx = 0;
        st.loopsDone = 0;
        st.stopped = false;
        st.baseCaptured = false;
    }

    void StopEndStageForSlot(int slotIndex)
    {
        if (endStageBySlot.TryGetValue(slotIndex, out var st) && st != null)
        {
            if (slotIndex >= 0 && slotIndex < slotImages.Count)
            {
                var img = slotImages[slotIndex];
                if (img != null && img.rectTransform != null)
                    img.rectTransform.anchoredPosition = st.baseCaptured ? st.baseAnchoredPos : Vector2.zero;
            }
        }

        endStageBySlot.Remove(slotIndex);
    }

    bool AllPlayersConfirmed()
    {
        for (int i = 0; i < players.Count; i++)
            if (!players[i].confirmed)
                return false;

        return players.Count > 0;
    }

    void TickCursorBlink()
    {
        if (!cursorBlinkWhileNotConfirmed)
            return;

        cursorBlinkT += Time.unscaledDeltaTime * Mathf.Max(0.01f, cursorBlinkSpeed);
        float s = (Mathf.Sin(cursorBlinkT) + 1f) * 0.5f;
        float a = Mathf.Lerp(cursorBlinkMinAlpha, cursorBlinkMaxAlpha, s);

        for (int i = 0; i < players.Count; i++)
        {
            var ps = players[i];
            if (ps.cursorImg == null || ps.cursorRt == null || !ps.cursorRt.gameObject.activeSelf)
                continue;

            if (ps.confirmed)
            {
                var c0 = ps.cursorImg.color;
                if (c0.a != 1f)
                {
                    c0.a = 1f;
                    ps.cursorImg.color = c0;
                }
                continue;
            }

            var col = ps.cursorImg.color;
            if (!Mathf.Approximately(col.a, a))
            {
                col.a = a;
                ps.cursorImg.color = col;
            }
        }
    }

    void TickDownClock()
    {
        if (downFrames == null || downFrames.Length == 0)
            return;

        float ft = Mathf.Max(0.001f, downFrameTime);
        downTimer += Time.unscaledDeltaTime;

        while (downTimer >= ft)
        {
            downTimer -= ft;
            downFrameIdx = (downFrameIdx + 1) % downFrames.Length;
        }
    }

    void TickEndStageClocks()
    {
        if (endStageFrames == null || endStageFrames.Length == 0)
            return;

        float ft = Mathf.Max(0.001f, endStageFrameTime);

        foreach (var kv in endStageBySlot)
        {
            var st = kv.Value;
            if (st == null || st.stopped)
                continue;

            st.timer += Time.unscaledDeltaTime;

            while (st.timer >= ft)
            {
                st.timer -= ft;

                int next = st.frameIdx + 1;

                if (next >= endStageFrames.Length)
                {
                    st.loopsDone++;

                    if (st.loopsDone >= Mathf.Max(1, endStageLoopsToStop))
                    {
                        st.stopped = true;
                        st.frameIdx = endStageFrames.Length - 1;
                        break;
                    }

                    next = 0;
                }

                st.frameIdx = next;
            }
        }
    }

    void UpdateSlotVisuals()
    {
        if (selectableSkins == null || selectableSkins.Count == 0)
            return;

        for (int i = 0; i < slotImages.Count; i++)
        {
            var img = slotImages[i];
            if (img == null) continue;

            var skin = selectableSkins[i];
            bool unlocked = BomberSkinUnlockProgress.IsUnlocked(skin);
            bool selected = IsSlotSelected(i);

            bool anyCursorHere = false;
            for (int p = 0; p < players.Count; p++)
            {
                if (!players[p].confirmed && players[p].index == i)
                {
                    anyCursorHere = true;
                    break;
                }
            }

            if (!unlocked)
                img.color = lockedTint;
            else if (selected || anyCursorHere)
                img.color = selectedTint;
            else
                img.color = normalTint;

            if (selected && endStageBySlot.TryGetValue(i, out var st) && st != null)
            {
                var rt = img.rectTransform;
                if (rt != null)
                {
                    if (!st.baseCaptured)
                    {
                        st.baseAnchoredPos = rt.anchoredPosition;
                        st.baseCaptured = true;
                    }

                    rt.anchoredPosition = st.baseAnchoredPos + new Vector2(0f, endStageYOffset);
                }

                int f = endStageFrames[Mathf.Clamp(st.frameIdx, 0, endStageFrames.Length - 1)];
                img.sprite = GetSpriteByFrame(st.skin, f) ?? GetIdleSprite(st.skin);
            }
            else
            {
                if (img.rectTransform != null)
                    img.rectTransform.anchoredPosition = Vector2.zero;

                if (unlocked && anyCursorHere && !selected)
                {
                    int f = downFrames != null && downFrames.Length > 0
                        ? downFrames[Mathf.Clamp(downFrameIdx, 0, downFrames.Length - 1)]
                        : idleFrameIndex;

                    img.sprite = GetSpriteByFrame(skin, f) ?? GetIdleSprite(skin);
                }
                else
                {
                    img.sprite = GetIdleSprite(skin);
                }
            }

            img.enabled = img.sprite != null;
        }
    }

    void RestoreAllSlotPositions()
    {
        for (int i = 0; i < slotImages.Count; i++)
        {
            var img = slotImages[i];
            if (img != null && img.rectTransform != null)
                img.rectTransform.anchoredPosition = Vector2.zero;
        }
    }

    void BuildPlayerCursors(int activeCount)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].cursorRt != null)
                Destroy(players[i].cursorRt.gameObject);
        }

        players.Clear();

        for (int p = 1; p <= Mathf.Clamp(activeCount, 1, 4); p++)
        {
            var st = new PlayerCursorState { playerId = p };

            if (skinCursorPrefab != null)
            {
                var c = Instantiate(skinCursorPrefab, gridRoot);
                c.gameObject.SetActive(true);
                c.SetAsLastSibling();
                st.cursorRt = c;

                st.cursorImg = c.GetComponent<Image>();
                if (st.cursorImg != null)
                {
                    st.cursorImg.raycastTarget = false;

                    int spriteIdx = p - 1;
                    if (cursorSpriteByPlayer != null &&
                        spriteIdx >= 0 &&
                        spriteIdx < cursorSpriteByPlayer.Length &&
                        cursorSpriteByPlayer[spriteIdx] != null)
                    {
                        st.cursorImg.sprite = cursorSpriteByPlayer[spriteIdx];
                        st.cursorImg.preserveAspect = true;
                    }

                    var col = st.cursorImg.color;
                    col.a = 1f;
                    st.cursorImg.color = col;
                }
            }

            players.Add(st);
        }
    }

    void BuildGrid()
    {
        slotRoots.Clear();
        slotImages.Clear();

        if (gridRoot == null || skinItemPrefab == null)
            return;

        if (gridRoot.TryGetComponent<GridLayoutGroup>(out var grid))
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, columns);
            grid.cellSize = cellSize;
            grid.spacing = spacing;
        }

        for (int i = gridRoot.childCount - 1; i >= 0; i--)
        {
            var child = gridRoot.GetChild(i);
            if (child == null) continue;
            if (skinItemPrefab != null && child == skinItemPrefab.transform) continue;
            if (skinCursorPrefab != null && child == skinCursorPrefab.transform) continue;
            Destroy(child.gameObject);
        }

        skinItemPrefab.gameObject.SetActive(false);

        for (int i = 0; i < selectableSkins.Count; i++)
        {
            var slotGo = new GameObject($"SkinSlot_{i}", typeof(RectTransform));
            var slotRt = slotGo.GetComponent<RectTransform>();
            slotRt.SetParent(gridRoot, false);
            slotRt.localScale = Vector3.one;
            slotRt.localRotation = Quaternion.identity;
            slotRt.anchorMin = new Vector2(0.5f, 0.5f);
            slotRt.anchorMax = new Vector2(0.5f, 0.5f);
            slotRt.pivot = new Vector2(0.5f, 0.5f);

            var img = Instantiate(skinItemPrefab, slotRt);
            img.gameObject.SetActive(true);
            img.preserveAspect = true;
            img.enabled = false;

            var imgRt = img.rectTransform;
            imgRt.anchorMin = Vector2.zero;
            imgRt.anchorMax = Vector2.one;
            imgRt.pivot = new Vector2(0.5f, 0.5f);
            imgRt.offsetMin = Vector2.zero;
            imgRt.offsetMax = Vector2.zero;
            imgRt.localScale = Vector3.one;
            imgRt.localRotation = Quaternion.identity;

            slotRoots.Add(slotRt);
            slotImages.Add(img);
        }
    }

    void UpdateAllCursorsToSelected()
    {
        for (int i = 0; i < players.Count; i++)
        {
            var ps = players[i];
            if (ps.cursorRt == null) continue;

            int idx = ps.confirmed ? ps.selectedIndex : ps.index;

            if (idx < 0 || idx >= slotRoots.Count || slotRoots[idx] == null)
            {
                ps.cursorRt.gameObject.SetActive(false);
                continue;
            }

            var slotRt = slotRoots[idx];

            ps.cursorRt.gameObject.SetActive(true);
            ps.cursorRt.SetParent(slotRt, false);
            ps.cursorRt.SetAsLastSibling();

            ps.cursorRt.anchorMin = new Vector2(0.5f, 0.5f);
            ps.cursorRt.anchorMax = new Vector2(0.5f, 0.5f);
            ps.cursorRt.pivot = new Vector2(0.5f, 0.5f);
            ps.cursorRt.anchoredPosition = new Vector2(0f, cursorYOffset);

            var baseSize = slotRt.rect.size;
            var targetSize = new Vector2(
                baseSize.x * cursorSizeMultiplier.x,
                baseSize.y * cursorSizeMultiplier.y
            ) + cursorPadding;

            ps.cursorRt.sizeDelta = targetSize;
            ps.cursorRt.localScale = Vector3.one;
            ps.cursorRt.localRotation = Quaternion.identity;
        }
    }

    void CycleOverlappedCursors()
    {
        overlapZTick++;

        for (int slot = 0; slot < slotRoots.Count; slot++)
        {
            var slotRt = slotRoots[slot];
            if (slotRt == null) continue;

            List<PlayerCursorState> here = null;

            for (int i = 0; i < players.Count; i++)
            {
                var ps = players[i];
                if (ps.cursorRt == null) continue;
                if (!ps.cursorRt.gameObject.activeSelf) continue;

                int idx = ps.confirmed ? ps.selectedIndex : ps.index;
                if (idx != slot) continue;

                here ??= new List<PlayerCursorState>(4);
                here.Add(ps);
            }

            if (here == null || here.Count <= 1)
                continue;

            int n = here.Count;
            int shift = overlapZTick % n;

            for (int k = 0; k < n; k++)
            {
                int pick = (k + shift) % n;
                var ps = here[pick];
                if (ps.cursorRt == null) continue;
                ps.cursorRt.SetSiblingIndex(1 + k);
            }
        }
    }

    void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        var music = GameMusicController.Instance;
        if (music == null)
            return;

        music.PlaySfx(clip, volume);
    }

    void StartSelectMusic()
    {
        var music = GameMusicController.Instance;
        if (music == null || selectMusic == null)
            return;

        if (!capturedPreviousMusic)
        {
            var src = music.GetComponent<AudioSource>();
            if (src != null)
            {
                previousClip = src.clip;
                previousVolume = src.volume;
                previousLoop = src.loop;
                capturedPreviousMusic = true;
            }
        }

        music.PlayMusic(selectMusic, selectMusicVolume, loopSelectMusic);
    }

    void StopSelectMusicAndRestorePrevious(bool restorePrevious)
    {
        var music = GameMusicController.Instance;
        if (music == null)
            return;

        if (!restorePrevious)
        {
            music.StopMusic();
            return;
        }

        if (!capturedPreviousMusic)
        {
            music.StopMusic();
            return;
        }

        if (previousClip != null)
            music.PlayMusic(previousClip, previousVolume, previousLoop);
        else
            music.StopMusic();
    }

    Sprite GetIdleSprite(BomberSkin skin)
    {
        if (idleCache.TryGetValue(skin, out var cached))
            return cached;

        var s = GetSpriteByFrame(skin, idleFrameIndex);
        idleCache[skin] = s;
        return s;
    }

    Sprite GetSpriteByFrame(BomberSkin skin, int frameIndex)
    {
        var map = GetOrBuildFrameMap(skin);
        if (map == null)
            return null;

        if (map.TryGetValue(frameIndex, out var s))
            return s;

        return null;
    }

    Dictionary<int, Sprite> GetOrBuildFrameMap(BomberSkin skin)
    {
        if (sheetFrameCache.TryGetValue(skin, out var map) && map != null)
            return map;

        string sheetName = skin + "Bomber";
        string sheetPath = $"{spritesResourcesPath}/{sheetName}";
        var sprites = Resources.LoadAll<Sprite>(sheetPath);

        map = new Dictionary<int, Sprite>();

        if (sprites != null && sprites.Length > 0)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                var sp = sprites[i];
                if (sp == null) continue;

                string n = sp.name;
                int u = n.LastIndexOf('_');

                if (u < 0 || u >= n.Length - 1)
                    continue;

                if (int.TryParse(n.Substring(u + 1), out int idx))
                {
                    if (!map.ContainsKey(idx))
                        map.Add(idx, sp);
                }
            }
        }

        sheetFrameCache[skin] = map;
        return map;
    }

    void SetFadeAlpha(float a)
    {
        if (fadeImage == null)
            return;

        var c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;
    }

    void PreloadIdleSprites()
    {
        for (int i = 0; i < selectableSkins.Count; i++)
            GetIdleSprite(selectableSkins[i]);
    }

    IEnumerator FadeInRoutine()
    {
        if (fadeImage == null)
            yield break;

        float duration = Mathf.Max(0.001f, fadeDuration);
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(t / duration);
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
        fadeImage.transform.SetAsLastSibling();
        SetFadeAlpha(0f);

        float duration = Mathf.Max(0.001f, fadeDuration);
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / duration);
            SetFadeAlpha(a);
            yield return null;
        }

        SetFadeAlpha(1f);
    }

    int MoveLeftWrap(int current)
    {
        int col = current % columns;
        int rowStart = current - col;

        if (col > 0)
            return current - 1;

        int lastInRow = Mathf.Min(rowStart + (columns - 1), selectableSkins.Count - 1);
        return lastInRow;
    }

    int MoveRightWrap(int current)
    {
        int col = current % columns;
        int rowStart = current - col;
        int rowEnd = Mathf.Min(rowStart + (columns - 1), selectableSkins.Count - 1);

        if (current < rowEnd)
            return current + 1;

        return rowStart;
    }

    int MoveUpWrap(int current)
    {
        int col = current % columns;
        int count = selectableSkins.Count;

        int prev = current - columns;
        if (prev >= 0)
            return prev;

        int lastRowStart = ((count - 1) / columns) * columns;
        int candidate = lastRowStart + col;

        while (candidate >= count)
            candidate -= columns;

        return Mathf.Clamp(candidate, 0, count - 1);
    }

    int MoveDownWrap(int current)
    {
        int col = current % columns;
        int count = selectableSkins.Count;

        int next = current + columns;
        if (next < count)
            return next;

        int candidate = col;

        while (candidate >= count)
            candidate -= columns;

        return Mathf.Clamp(candidate, 0, count - 1);
    }

    Canvas GetRootCanvas()
    {
        if (root != null)
            return root.GetComponentInParent<Canvas>();

        return GetComponentInParent<Canvas>();
    }

    Camera GetMainCameraSafe()
    {
        var cam = Camera.main;
        if (cam != null) return cam;

        var any = FindFirstObjectByType<Camera>();
        return any;
    }

    Rect GetReferencePixelRect(out string source)
    {
        var canvas = GetRootCanvas();
        if (canvas == null)
        {
            source = "NO_CANVAS";
            return new Rect(0, 0, Screen.width, Screen.height);
        }

        RectTransform rt = referenceRect;
        if (rt == null)
        {
            if (rootPanel != null) rt = rootPanel;
            else if (root != null) rt = root.GetComponent<RectTransform>();
        }

        if (rt == null)
        {
            source = "FALLBACK_SCREEN";
            return new Rect(0, 0, Screen.width, Screen.height);
        }

        source = rt.name;

        Rect px = RectTransformUtility.PixelAdjustRect(rt, canvas);
        if (px.width <= 1f || px.height <= 1f)
        {
            var r = rt.rect;
            px = new Rect(0, 0, r.width, r.height);
            source += "+rt.rect";
        }
        else
        {
            source += "+PixelAdjustRect";
        }

        return px;
    }

    float ComputeUiScaleForRect(float usedW, float usedH, out int baseScaleInt)
    {
        baseScaleInt = 1;

        if (!dynamicScale)
            return 1f;

        float sx = usedW / Mathf.Max(1f, referenceWidth);
        float sy = usedH / Mathf.Max(1f, referenceHeight);

        float baseScaleRaw = Mathf.Min(sx, sy);
        float baseScaleForUi = useIntegerUpscale ? Mathf.Floor(baseScaleRaw) : baseScaleRaw;
        if (baseScaleForUi < 1f) baseScaleForUi = 1f;

        baseScaleInt = Mathf.Max(1, Mathf.RoundToInt(baseScaleForUi));

        float normalized = baseScaleInt / Mathf.Max(1f, designUpscale);
        float ui = normalized * Mathf.Max(0.01f, extraScaleMultiplier);
        ui = Mathf.Clamp(ui, minScale, maxScale);
        return ui;
    }

    static bool ApproximatelyRect(Rect a, Rect b)
    {
        return
            Mathf.Abs(a.x - b.x) < 0.01f &&
            Mathf.Abs(a.y - b.y) < 0.01f &&
            Mathf.Abs(a.width - b.width) < 0.01f &&
            Mathf.Abs(a.height - b.height) < 0.01f;
    }

    void CaptureBaseValuesIfNeeded()
    {
        if (_baseValuesCaptured)
            return;

        _baseCellSize = cellSize;
        _baseSpacing = spacing;
        _baseCursorPadding = cursorPadding;
        _baseCursorYOffset = cursorYOffset;
        _baseEndStageYOffset = endStageYOffset;
        _baseValuesCaptured = true;
    }

    void ApplyDynamicScaleIfNeeded(bool force = false)
    {
        CaptureBaseValuesIfNeeded();
        ApplyAutoFixesIfEnabled();

        int sw = Screen.width;
        int sh = Screen.height;

        var cam = GetMainCameraSafe();
        Rect camRect = cam != null ? cam.rect : new Rect(0, 0, 1, 1);
        Rect refPx = GetReferencePixelRect(out _);

        bool changed =
            force ||
            sw != _lastScreenW ||
            sh != _lastScreenH ||
            camRect != _lastCameraRect ||
            !ApproximatelyRect(refPx, _lastRefPixelRect);

        if (!changed)
            return;

        _lastScreenW = sw;
        _lastScreenH = sh;
        _lastCameraRect = camRect;
        _lastRefPixelRect = refPx;

        _currentUiScale = ComputeUiScaleForRect(refPx.width, refPx.height, out _currentBaseScaleInt);

        cellSize = _baseCellSize * _currentUiScale;
        spacing = _baseSpacing * _currentUiScale;
        cursorPadding = _baseCursorPadding * _currentUiScale;
        cursorYOffset = _baseCursorYOffset * _currentUiScale;
        endStageYOffset = _baseEndStageYOffset * _currentUiScale;

        ApplyScaledLayout();
    }

    void ApplyScaledLayout()
    {
        if (gridRoot == null)
            return;

        if (gridRoot.TryGetComponent<GridLayoutGroup>(out var grid))
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, columns);
            grid.cellSize = cellSize;
            grid.spacing = spacing;
        }

        var gridRt = gridRoot as RectTransform;
        if (gridRt != null)
        {
            gridRt.anchorMin = new Vector2(0.5f, 0.5f);
            gridRt.anchorMax = new Vector2(0.5f, 0.5f);
            gridRt.pivot = new Vector2(0.5f, 0.5f);
            gridRt.anchoredPosition = gridBaseAnchoredPos * _currentUiScale;

            int rows = Mathf.CeilToInt(selectableSkins.Count / (float)Mathf.Max(1, columns));
            float totalW = columns * cellSize.x + (columns - 1) * spacing.x;
            float totalH = rows * cellSize.y + (rows - 1) * spacing.y;

            gridRt.sizeDelta = new Vector2(totalW, totalH);
        }
    }

    void ApplyAutoFixesIfEnabled()
    {
        if (forceRootPanelStretchToParent && rootPanel != null)
            StretchToParent(rootPanel);

        if (forceBackgroundStretchToRootPanel && backgroundImage != null)
            StretchToParent(backgroundImage.rectTransform);
    }

    static void StretchToParent(RectTransform rt)
    {
        if (rt == null) return;

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    void ResetBackgroundSpriteSwap()
    {
        _backgroundSpriteIndex = 0;
        _backgroundSwapTimer = 0f;
    }

    void TickBackgroundSpriteSwap()
    {
        if (backgroundImage == null)
            return;

        if (backgroundSprites == null || backgroundSprites.Length == 0)
            return;

        if (backgroundSprites.Length == 1)
        {
            if (backgroundImage.sprite != backgroundSprites[0])
                backgroundImage.sprite = backgroundSprites[0];
            return;
        }

        _backgroundSwapTimer += Time.unscaledDeltaTime;

        if (_backgroundSwapTimer < Mathf.Max(0.01f, backgroundSwapInterval))
            return;

        _backgroundSwapTimer = 0f;
        _backgroundSpriteIndex++;

        if (_backgroundSpriteIndex >= backgroundSprites.Length)
        {
            if (backgroundSwapLoop)
                _backgroundSpriteIndex = 0;
            else
                _backgroundSpriteIndex = backgroundSprites.Length - 1;
        }

        ApplyCurrentBackgroundSprite();
    }

    void ApplyCurrentBackgroundSprite()
    {
        if (backgroundImage == null)
            return;

        if (backgroundSprites == null || backgroundSprites.Length == 0)
            return;

        _backgroundSpriteIndex = Mathf.Clamp(_backgroundSpriteIndex, 0, backgroundSprites.Length - 1);

        if (backgroundSprites[_backgroundSpriteIndex] != null)
            backgroundImage.sprite = backgroundSprites[_backgroundSpriteIndex];
    }
}