using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

    [Header("Cursor")]
    [SerializeField] RectTransform skinCursor;
    [SerializeField] Vector2 cursorPadding = new(18f, 18f);
    [SerializeField] bool cursorMatchSelectedScale = true;
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

    [Header("SFX (Back to Title)")]
    [SerializeField] AudioClip backToTitleSfx;
    [SerializeField, Range(0f, 1f)] float backToTitleSfxVolume = 1f;

    public bool ReturnToTitleRequested { get; private set; }

    [Header("Skins (ordem do menu)")]
    [SerializeField]
    List<BomberSkin> selectableSkins = new()
    {
        BomberSkin.White,
        BomberSkin.Black,
        BomberSkin.Blue,
        BomberSkin.Red,
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

    int index;
    int selectedIndex = -1;
    BomberSkin selected;

    AudioClip previousClip;
    float previousVolume;
    bool previousLoop;
    bool capturedPreviousMusic;

    bool confirmedSelection;

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

    Image cursorImage;
    bool cursorDirty;
    bool menuActive;

    float downTimer;
    int downFrameIdx;

    float endTimer;
    int endFrameIdx;

    int endStageLoopsDone;
    bool endStageStopped;

    bool endStageBaseCaptured;
    RectTransform endStageImgRt;
    Vector2 endStageBaseAnchoredPos;

    float cursorBlinkT;

    void Awake()
    {
        if (root == null)
            root = gameObject;

        if (backgroundImage != null && backgroundSprite != null)
            backgroundImage.sprite = backgroundSprite;

        if (skinCursor != null)
        {
            cursorImage = skinCursor.GetComponent<Image>();
            if (cursorImage != null)
            {
                cursorImage.raycastTarget = false;
                cursorImage.color = Color.white;
            }

            skinCursor.gameObject.SetActive(false);
        }

        BuildGrid();

        if (root != null)
            root.SetActive(false);
    }

    void Update()
    {
        if (!menuActive)
            return;

        TickCursorBlink();

        if (!confirmedSelection)
            TickDownHover();
        else
            TickEndStageSelected();
    }

    void LateUpdate()
    {
        if (!cursorDirty)
            return;

        cursorDirty = false;

        if (skinCursor == null)
            return;

        Canvas.ForceUpdateCanvases();
        UpdateCursorToSelected();
    }

    void TickCursorBlink()
    {
        if (cursorImage == null || skinCursor == null || !skinCursor.gameObject.activeSelf)
            return;

        if (!cursorBlinkWhileNotConfirmed || confirmedSelection)
        {
            var c = cursorImage.color;
            if (c.a != 1f)
            {
                c.a = 1f;
                cursorImage.color = c;
            }
            return;
        }

        cursorBlinkT += Time.unscaledDeltaTime * Mathf.Max(0.01f, cursorBlinkSpeed);
        float s = (Mathf.Sin(cursorBlinkT) + 1f) * 0.5f;
        float a = Mathf.Lerp(cursorBlinkMinAlpha, cursorBlinkMaxAlpha, s);

        var col = cursorImage.color;
        if (!Mathf.Approximately(col.a, a))
        {
            col.a = a;
            cursorImage.color = col;
        }
    }

    void TickDownHover()
    {
        if (index < 0 || index >= slotImages.Count)
            return;

        var skin = selectableSkins[index];
        if (!PlayerPersistentStats.IsSkinUnlocked(skin))
        {
            ApplyIdleToAllSlots();
            return;
        }

        ApplyIdleToAllSlots();

        float ft = Mathf.Max(0.01f, downFrameTime);
        downTimer += Time.unscaledDeltaTime;

        if (downFrames == null || downFrames.Length == 0)
            return;

        while (downTimer >= ft)
        {
            downTimer -= ft;
            downFrameIdx = (downFrameIdx + 1) % downFrames.Length;
        }

        var sp = GetSpriteByFrame(skin, downFrames[downFrameIdx]) ?? GetIdleSprite(skin);
        var img = slotImages[index];
        if (img != null && img.sprite != sp)
            img.sprite = sp;
    }

    void TickEndStageSelected()
    {
        if (selectedIndex < 0 || selectedIndex >= slotImages.Count)
        {
            ApplyIdleToAllSlots();
            return;
        }

        var skin = selectableSkins[selectedIndex];
        if (!PlayerPersistentStats.IsSkinUnlocked(skin))
        {
            ApplyIdleToAllSlots();
            return;
        }

        ApplyIdleToAllSlots();

        var img = slotImages[selectedIndex];
        if (img == null)
            return;

        var rt = img.rectTransform;

        if (!endStageBaseCaptured || endStageImgRt != rt)
        {
            endStageImgRt = rt;
            endStageBaseAnchoredPos = rt.anchoredPosition;
            endStageBaseCaptured = true;
        }

        rt.anchoredPosition = endStageBaseAnchoredPos + new Vector2(0f, endStageYOffset);

        if (endStageFrames == null || endStageFrames.Length == 0)
            return;

        if (endStageStopped)
        {
            int lastFrame = endStageFrames[endStageFrames.Length - 1];
            var lastSp = GetSpriteByFrame(skin, lastFrame) ?? GetIdleSprite(skin);
            if (img.sprite != lastSp)
                img.sprite = lastSp;
            return;
        }

        float ft = Mathf.Max(0.01f, endStageFrameTime);
        endTimer += Time.unscaledDeltaTime;

        while (endTimer >= ft)
        {
            endTimer -= ft;

            int next = endFrameIdx + 1;

            if (next >= endStageFrames.Length)
            {
                endStageLoopsDone++;

                if (endStageLoopsDone >= Mathf.Max(1, endStageLoopsToStop))
                {
                    endStageStopped = true;
                    endFrameIdx = endStageFrames.Length - 1;
                    break;
                }

                next = 0;
            }

            endFrameIdx = next;
        }

        var sp = GetSpriteByFrame(skin, endStageFrames[endFrameIdx]) ?? GetIdleSprite(skin);
        if (img.sprite != sp)
            img.sprite = sp;

        cursorDirty = true;
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
            if (skinCursor != null && child == skinCursor.transform) continue;
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

        cursorDirty = true;
    }

    public void Hide()
    {
        menuActive = false;

        if (endStageBaseCaptured && endStageImgRt != null)
            endStageImgRt.anchoredPosition = endStageBaseAnchoredPos;

        endStageBaseCaptured = false;
        endStageImgRt = null;

        if (skinCursor != null)
            skinCursor.gameObject.SetActive(false);

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

        konamiStep = 0;
        ReturnToTitleRequested = false;
        confirmedSelection = false;
        selectedIndex = -1;

        downTimer = 0f;
        downFrameIdx = 0;

        endTimer = 0f;
        endFrameIdx = 0;
        endStageLoopsDone = 0;
        endStageStopped = false;

        endStageBaseCaptured = false;
        endStageImgRt = null;

        cursorBlinkT = 0f;
        if (cursorImage != null)
        {
            var c = cursorImage.color;
            c.a = 1f;
            cursorImage.color = c;
        }

        StartSelectMusic();

        var input = PlayerInputManager.Instance;

        while (input.Get(PlayerAction.ActionA) ||
               input.Get(PlayerAction.MoveLeft) ||
               input.Get(PlayerAction.MoveRight) ||
               input.Get(PlayerAction.MoveUp) ||
               input.Get(PlayerAction.MoveDown))
            yield return null;

        yield return null;

        index = selectableSkins.IndexOf(PlayerPersistentStats.Skin);
        if (index < 0) index = 0;

        selected = PlayerPersistentStats.Skin;

        PreloadIdleSprites();
        RefreshVisuals();

        Canvas.ForceUpdateCanvases();
        cursorDirty = true;
        yield return null;

        menuActive = true;

        yield return FadeInRoutine();

        bool done = false;

        while (!done)
        {
            bool blockConfirmBack;
            bool didUnlockNow = ProcessKonamiCode(input, out blockConfirmBack);
            if (didUnlockNow)
                RefreshVisuals();

            bool moved = false;
            int nextIndex = index;

            if (input.GetDown(PlayerAction.MoveLeft))
            {
                nextIndex = MoveLeftWrap(index);
                moved = true;
            }
            else if (input.GetDown(PlayerAction.MoveRight))
            {
                nextIndex = MoveRightWrap(index);
                moved = true;
            }
            else if (input.GetDown(PlayerAction.MoveUp))
            {
                nextIndex = MoveUpWrap(index);
                moved = true;
            }
            else if (input.GetDown(PlayerAction.MoveDown))
            {
                nextIndex = MoveDownWrap(index);
                moved = true;
            }

            bool confirmPressed =
                input.GetDown(PlayerAction.Start) ||
                (!blockConfirmBack && input.GetDown(PlayerAction.ActionA));

            if (moved)
            {
                if (nextIndex != index)
                {
                    index = nextIndex;
                    downTimer = 0f;
                    downFrameIdx = 0;
                    PlaySfx(moveCursorSfx, moveCursorSfxVolume);
                    RefreshVisuals();
                }
            }
            else if (!blockConfirmBack && input.GetDown(PlayerAction.ActionB))
            {
                ReturnToTitleRequested = true;
                PlaySfx(backToTitleSfx, backToTitleSfxVolume);

                selected = PlayerPersistentStats.Skin;
                done = true;
            }
            else if (confirmPressed)
            {
                var skin = selectableSkins[index];
                if (PlayerPersistentStats.IsSkinUnlocked(skin))
                {
                    selected = skin;

                    PlayerPersistentStats.Skin = skin;
                    if (skin != BomberSkin.Golden)
                        PlayerPersistentStats.SaveSelectedSkin();

                    selectedIndex = index;
                    confirmedSelection = true;

                    if (cursorImage != null)
                    {
                        var c = cursorImage.color;
                        c.a = 1f;
                        cursorImage.color = c;
                    }

                    endTimer = 0f;
                    endFrameIdx = 0;
                    endStageLoopsDone = 0;
                    endStageStopped = false;

                    endStageBaseCaptured = false;
                    endStageImgRt = null;

                    fadeDuration = fadeOutOnConfirmDuration;

                    PlaySfx(confirmSfx, confirmSfxVolume);
                    RefreshVisuals();

                    done = true;
                }
                else
                {
                    PlaySfx(lockedConfirmSfx, lockedConfirmSfxVolume);
                }
            }
            else if (!blockConfirmBack && input.GetDown(PlayerAction.ActionB))
            {
                ReturnToTitleRequested = true;
                PlaySfx(backToTitleSfx, backToTitleSfxVolume);

                selected = PlayerPersistentStats.Skin;
                done = true;
            }

            yield return null;
        }

        yield return FadeOutRoutine();

        StopSelectMusicAndRestorePrevious(restorePrevious: !confirmedSelection);
        Hide();

        if (fadeImage != null)
            fadeImage.gameObject.SetActive(false);
    }

    public BomberSkin GetSelectedSkin() => selected;

    bool ProcessKonamiCode(PlayerInputManager input, out bool blockConfirmBack)
    {
        blockConfirmBack = false;

        KonamiToken pressed = KonamiToken.None;

        if (input != null)
        {
            if (input.GetDown(PlayerAction.MoveUp)) pressed = KonamiToken.Up;
            else if (input.GetDown(PlayerAction.MoveDown)) pressed = KonamiToken.Down;
            else if (input.GetDown(PlayerAction.MoveLeft)) pressed = KonamiToken.Left;
            else if (input.GetDown(PlayerAction.MoveRight)) pressed = KonamiToken.Right;
            else if (input.GetDown(PlayerAction.ActionB)) pressed = KonamiToken.B;
            else if (input.GetDown(PlayerAction.ActionA)) pressed = KonamiToken.A;
        }

        if (pressed == KonamiToken.None)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow)) pressed = KonamiToken.Up;
            else if (Input.GetKeyDown(KeyCode.DownArrow)) pressed = KonamiToken.Down;
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) pressed = KonamiToken.Left;
            else if (Input.GetKeyDown(KeyCode.RightArrow)) pressed = KonamiToken.Right;
            else if (Input.GetKeyDown(KeyCode.B)) pressed = KonamiToken.B;
            else if (Input.GetKeyDown(KeyCode.A)) pressed = KonamiToken.A;
            else
                return false;
        }

        if (pressed == KonamiToken.A || pressed == KonamiToken.B)
            blockConfirmBack = true;

        if (pressed == _konamiTokens[konamiStep])
        {
            konamiStep++;

            if (konamiStep >= _konamiTokens.Length)
            {
                konamiStep = 0;

                if (!PlayerPersistentStats.IsSkinUnlocked(BomberSkin.Golden))
                {
                    PlayerPersistentStats.UnlockGolden();
                    PlayerPersistentStats.ClampSelectedSkinIfLocked();

                    PlaySfx(konamiUnlockSfx, konamiUnlockSfxVolume);
                    return true;
                }
            }

            return false;
        }

        konamiStep = (pressed == _konamiTokens[0]) ? 1 : 0;
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

    void RefreshVisuals()
    {
        if (selectableSkins == null || selectableSkins.Count == 0)
            return;

        for (int i = 0; i < slotImages.Count; i++)
        {
            var img = slotImages[i];
            if (img == null) continue;

            if (img.rectTransform != null)
                img.rectTransform.anchoredPosition = Vector2.zero;

            var s = selectableSkins[i];
            bool isUnlocked = PlayerPersistentStats.IsSkinUnlocked(s);
            bool isSelected = (i == index);

            img.sprite = GetIdleSprite(s);
            img.enabled = img.sprite != null;

            img.color = isUnlocked ? normalTint : lockedTint;

            if (i < slotRoots.Count && slotRoots[i] != null)
                slotRoots[i].localScale = Vector3.one;

            if (isSelected)
                img.color = isUnlocked ? selectedTint : lockedTint;
        }

        cursorDirty = true;
    }

    void ApplyIdleToAllSlots()
    {
        for (int i = 0; i < slotImages.Count; i++)
        {
            var img = slotImages[i];
            if (img == null) continue;

            if (img.rectTransform != null)
                img.rectTransform.anchoredPosition = Vector2.zero;

            var skin = selectableSkins[i];
            var sp = GetIdleSprite(skin);
            if (img.sprite != sp)
                img.sprite = sp;
        }
    }

    void UpdateCursorToSelected()
    {
        if (skinCursor == null)
            return;

        if (index < 0 || index >= slotRoots.Count || slotRoots[index] == null)
        {
            skinCursor.gameObject.SetActive(false);
            return;
        }

        var slotRt = slotRoots[index];

        skinCursor.gameObject.SetActive(true);
        skinCursor.SetParent(slotRt, false);
        skinCursor.SetAsLastSibling();

        skinCursor.anchorMin = new Vector2(0.5f, 0.5f);
        skinCursor.anchorMax = new Vector2(0.5f, 0.5f);
        skinCursor.pivot = new Vector2(0.5f, 0.5f);

        skinCursor.anchoredPosition = new Vector2(0f, cursorYOffset);

        var baseSize = slotRt.rect.size;
        var targetSize = new Vector2(
            baseSize.x * cursorSizeMultiplier.x,
            baseSize.y * cursorSizeMultiplier.y
        ) + cursorPadding;

        skinCursor.sizeDelta = targetSize;

        skinCursor.localScale = Vector3.one;
        skinCursor.localRotation = Quaternion.identity;

        if (cursorImage != null)
        {
            var c = cursorImage.color;
            c.r = 1f;
            c.g = 1f;
            c.b = 1f;
            cursorImage.color = c;
        }
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
