using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class BomberSkinSelectMenu : MonoBehaviour
{
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
    [SerializeField] Sprite backgroundSprite;

    [Header("Resources")]
    [SerializeField] string spritesResourcesPath = "Sprites/Bombers/Bomberman";
    [SerializeField] int idleFrameIndex = 16;

    [Header("Preview Animations")]
    [SerializeField] float downFrameTime = 0.22f;
    [SerializeField] float endStageFrameTime = 0.14f;

    [SerializeField] int[] downFrames = new[] { 14, 16, 18, 16 };
    [SerializeField] int[] endStageFrames = new[] { 148, 148, 146, 148, 147, 148, 146, 148, 147, 147 };

    [Header("EndStage Offset + Stop")]
    [SerializeField] float endStageYOffset = 10f;
    [SerializeField] int endStageLoopsToStop = 2;

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
    [SerializeField] AudioClip konamiUnlockSfx;
    [SerializeField, Range(0f, 1f)] float konamiUnlockSfxVolume = 1f;

    [Header("SFX (Return)")]
    [FormerlySerializedAs("backToTitleSfx")]
    [SerializeField] AudioClip returnSfx;
    [FormerlySerializedAs("backToTitleSfxVolume")]
    [SerializeField, Range(0f, 1f)] float returnSfxVolume = 1f;

    public bool ReturnToTitleRequested { get; private set; }

    [Header("Skins (menu order)")]
    [SerializeField]
    List<BomberSkin> selectableSkins = new()
    {
        BomberSkin.White,
        BomberSkin.Black,
        BomberSkin.Red,
        BomberSkin.Blue,
        BomberSkin.Green,
        BomberSkin.Yellow,
        BomberSkin.Pink,
        BomberSkin.Aqua,
        BomberSkin.Golden
    };

    readonly Dictionary<BomberSkin, Sprite> idleCache = new();
    readonly Dictionary<BomberSkin, Dictionary<int, Sprite>> sheetFrameCache = new();

    readonly List<RectTransform> slotRoots = new();
    readonly List<Image> slotImages = new();

    AudioClip previousClip;
    float previousVolume;
    bool previousLoop;
    bool capturedPreviousMusic;

    enum KonamiToken
    {
        None = 0,
        Up,
        Down,
        Left,
        Right,
        B,
        A
    }

    static readonly KonamiToken[] _konamiTokens = new[]
    {
        KonamiToken.Up, KonamiToken.Up,
        KonamiToken.Down, KonamiToken.Down,
        KonamiToken.Left, KonamiToken.Right,
        KonamiToken.Left, KonamiToken.Right,
        KonamiToken.B, KonamiToken.A
    };

    int konamiStep;
    bool menuActive;

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

        if (backgroundImage != null && backgroundSprite != null)
            backgroundImage.sprite = backgroundSprite;

        BuildGrid();

        if (root != null)
            root.SetActive(false);
    }

    void Update()
    {
        if (!menuActive)
            return;

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

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.transform.SetAsLastSibling();
            SetFadeAlpha(1f);
        }

        if (backgroundImage != null && backgroundSprite != null)
            backgroundImage.sprite = backgroundSprite;

        if (slotImages.Count != selectableSkins.Count)
            BuildGrid();

        PlayerPersistentStats.EnsureSessionBooted();

        konamiStep = 0;
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

        yield return FadeInRoutine();

        bool done = false;

        while (!done)
        {
            bool anyReturnToTitle = false;

            bool globalUp = false, globalDown = false, globalLeft = false, globalRight = false, globalB = false, globalA = false;

            for (int i = 0; i < players.Count; i++)
            {
                int pid = players[i].playerId;

                bool upDown = input.GetDown(pid, PlayerAction.MoveUp);
                bool downDown = input.GetDown(pid, PlayerAction.MoveDown);
                bool leftDown = input.GetDown(pid, PlayerAction.MoveLeft);
                bool rightDown = input.GetDown(pid, PlayerAction.MoveRight);
                bool aDown = input.GetDown(pid, PlayerAction.ActionA);
                bool bDown = input.GetDown(pid, PlayerAction.ActionB);

                globalUp |= upDown;
                globalDown |= downDown;
                globalLeft |= leftDown;
                globalRight |= rightDown;
                globalB |= bDown;
                globalA |= aDown;
            }

            bool blockMenuThisFrame;
            bool didUnlockNow = ProcessKonamiCode(globalUp, globalDown, globalLeft, globalRight, globalB, globalA, out blockMenuThisFrame);

            if (didUnlockNow)
            {
                UpdateSlotVisuals();
                yield return null;
                continue;
            }

            if (blockMenuThisFrame)
            {
                yield return null;
                continue;
            }

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

        if (!PlayerPersistentStats.IsSkinUnlocked(skin))
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
                {
                    img.rectTransform.anchoredPosition = st.baseCaptured ? st.baseAnchoredPos : Vector2.zero;
                }
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
            if (st == null) continue;

            if (st.stopped)
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
            bool unlocked = PlayerPersistentStats.IsSkinUnlocked(skin);

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
                        spriteIdx >= 0 && spriteIdx < cursorSpriteByPlayer.Length &&
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

    bool ProcessKonamiCode(bool upDown, bool downDown, bool leftDown, bool rightDown, bool bDown, bool aDown, out bool blockMenuThisFrame)
    {
        KonamiToken pressed = ReadKonamiTokenThisFrame(upDown, downDown, leftDown, rightDown, bDown, aDown);

        bool unlocked = AdvanceKonami(pressed, out bool consumed);

        blockMenuThisFrame = consumed && (pressed == KonamiToken.A || pressed == KonamiToken.B);

        if (!unlocked)
            return false;

        if (!PlayerPersistentStats.IsSkinUnlocked(BomberSkin.Golden))
        {
            PlayerPersistentStats.UnlockGolden();

            for (int p = 1; p <= 4; p++)
                PlayerPersistentStats.ClampSelectedSkinIfLocked(p);

            PlaySfx(konamiUnlockSfx, konamiUnlockSfxVolume);
            return true;
        }

        return false;
    }

    KonamiToken ReadKonamiTokenThisFrame(bool upDown, bool downDown, bool leftDown, bool rightDown, bool bDown, bool aDown)
    {
        if (upDown) return KonamiToken.Up;
        if (downDown) return KonamiToken.Down;
        if (leftDown) return KonamiToken.Left;
        if (rightDown) return KonamiToken.Right;
        if (bDown) return KonamiToken.B;
        if (aDown) return KonamiToken.A;
        return KonamiToken.None;
    }

    bool AdvanceKonami(KonamiToken pressed, out bool consumedThisFrame)
    {
        consumedThisFrame = false;

        if (pressed == KonamiToken.None)
            return false;

        bool isKonamiToken =
            pressed == KonamiToken.Up ||
            pressed == KonamiToken.Down ||
            pressed == KonamiToken.Left ||
            pressed == KonamiToken.Right ||
            pressed == KonamiToken.B ||
            pressed == KonamiToken.A;

        if (isKonamiToken && (konamiStep > 0 || pressed == _konamiTokens[0]))
            consumedThisFrame = true;

        if (pressed == _konamiTokens[konamiStep])
        {
            konamiStep++;

            if (konamiStep >= _konamiTokens.Length)
            {
                konamiStep = 0;
                consumedThisFrame = true;
                return true;
            }

            return false;
        }

        konamiStep = (pressed == _konamiTokens[0]) ? 1 : 0;

        if (pressed == _konamiTokens[0])
            consumedThisFrame = true;

        return false;
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
}
