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
    [SerializeField] Vector3 selectedScale = new(1.15f, 1.15f, 1f);

    [Header("Cursor")]
    [SerializeField] RectTransform skinCursor;
    [SerializeField] Vector2 cursorPadding = new(18f, 18f);
    [SerializeField] bool cursorMatchSelectedScale = true;

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

    [Header("Input")]
    [SerializeField] KeyCode leftKey = KeyCode.A;
    [SerializeField] KeyCode rightKey = KeyCode.D;
    [SerializeField] KeyCode upKey = KeyCode.W;
    [SerializeField] KeyCode downKey = KeyCode.S;
    [SerializeField] KeyCode confirmKey = KeyCode.Return;
    [SerializeField] KeyCode cancelKey = KeyCode.Escape;

    [Header("Input Source")]
    [SerializeField] bool useMovementControllerBindings = true;
    [SerializeField] string playerTag = "Player";

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
    readonly List<Image> slots = new();

    [SerializeField] Vector2 cursorSizeMultiplier = new(0.9f, 0.9f);
    [SerializeField] float cursorYOffset = 8f;

    int index;
    int selectedIndex = -1;
    BomberSkin selected;

    AudioClip previousClip;
    float previousVolume;
    bool previousLoop;
    bool capturedPreviousMusic;

    bool confirmedSelection;

    static readonly KeyCode[] _konami = new[]
    {
        KeyCode.UpArrow, KeyCode.UpArrow,
        KeyCode.DownArrow, KeyCode.DownArrow,
        KeyCode.LeftArrow, KeyCode.RightArrow,
        KeyCode.LeftArrow, KeyCode.RightArrow,
        KeyCode.B, KeyCode.A
    };

    int konamiStep;

    KeyCode moveLeft;
    KeyCode moveRight;
    KeyCode moveUp;
    KeyCode moveDown;

    Image cursorImage;
    bool cursorDirty;
    bool menuActive;

    float downTimer;
    int downFrameIdx;

    float endTimer;
    int endFrameIdx;

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

        if (!confirmedSelection)
        {
            TickDownHover();
        }
        else
        {
            TickEndStageSelected();
        }
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

    void TickDownHover()
    {
        if (index < 0 || index >= slots.Count)
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
        var img = slots[index];
        if (img != null && img.sprite != sp)
            img.sprite = sp;
    }

    void TickEndStageSelected()
    {
        if (selectedIndex < 0 || selectedIndex >= slots.Count)
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

        float ft = Mathf.Max(0.01f, endStageFrameTime);
        endTimer += Time.unscaledDeltaTime;

        if (endStageFrames == null || endStageFrames.Length == 0)
            return;

        while (endTimer >= ft)
        {
            endTimer -= ft;
            endFrameIdx = (endFrameIdx + 1) % endStageFrames.Length;
        }

        var sp = GetSpriteByFrame(skin, endStageFrames[endFrameIdx]) ?? GetIdleSprite(skin);
        var img = slots[selectedIndex];
        if (img != null && img.sprite != sp)
            img.sprite = sp;
    }

    void ResolveMovementKeys()
    {
        moveLeft = leftKey;
        moveRight = rightKey;
        moveUp = upKey;
        moveDown = downKey;

        if (!useMovementControllerBindings)
        {
            SanitizeMoveKeys();
            return;
        }

        MovementController mc = null;

        var player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
            mc = player.GetComponent<MovementController>();

        if (mc == null)
            mc = Object.FindFirstObjectByType<MovementController>();

        if (mc != null)
        {
            moveUp = mc.inputUp;
            moveDown = mc.inputDown;
            moveLeft = mc.inputLeft;
            moveRight = mc.inputRight;
        }

        SanitizeMoveKeys();
    }

    void SanitizeMoveKeys()
    {
        if (IsArrow(moveLeft)) moveLeft = KeyCode.A;
        if (IsArrow(moveRight)) moveRight = KeyCode.D;
        if (IsArrow(moveUp)) moveUp = KeyCode.W;
        if (IsArrow(moveDown)) moveDown = KeyCode.S;
    }

    static bool IsArrow(KeyCode k) =>
        k == KeyCode.UpArrow || k == KeyCode.DownArrow || k == KeyCode.LeftArrow || k == KeyCode.RightArrow;

    void BuildGrid()
    {
        slots.Clear();

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

        if (skinItemPrefab != null)
            skinItemPrefab.gameObject.SetActive(false);

        for (int i = 0; i < selectableSkins.Count; i++)
        {
            var img = Instantiate(skinItemPrefab, gridRoot);
            img.gameObject.SetActive(true);
            img.preserveAspect = true;
            img.enabled = false;
            slots.Add(img);
        }

        cursorDirty = true;
    }

    public void Hide()
    {
        menuActive = false;

        if (skinCursor != null)
            skinCursor.gameObject.SetActive(false);

        if (root != null) root.SetActive(false);
        else gameObject.SetActive(false);
    }

    public IEnumerator SelectSkinRoutine()
    {
        ResolveMovementKeys();

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

        if (slots.Count != selectableSkins.Count)
            BuildGrid();

        konamiStep = 0;
        confirmedSelection = false;
        selectedIndex = -1;

        downTimer = 0f;
        downFrameIdx = 0;
        endTimer = 0f;
        endFrameIdx = 0;

        StartSelectMusic();

        while (Input.GetKey(confirmKey) || Input.GetKey(moveLeft) || Input.GetKey(moveRight) || Input.GetKey(moveUp) || Input.GetKey(moveDown))
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
            bool didUnlockNow = ProcessKonamiCode();
            if (didUnlockNow)
                RefreshVisuals();

            bool moved = false;
            int nextIndex = index;

            if (Input.GetKeyDown(moveLeft))
            {
                nextIndex = Wrap(index - 1, selectableSkins.Count);
                moved = true;
            }
            else if (Input.GetKeyDown(moveRight))
            {
                nextIndex = Wrap(index + 1, selectableSkins.Count);
                moved = true;
            }
            else if (Input.GetKeyDown(moveUp))
            {
                nextIndex = MoveGrid(index, -columns);
                moved = true;
            }
            else if (Input.GetKeyDown(moveDown))
            {
                nextIndex = MoveGrid(index, +columns);
                moved = true;
            }

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
            else if (Input.GetKeyDown(confirmKey))
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

                    endTimer = 0f;
                    endFrameIdx = 0;

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
            else if (Input.GetKeyDown(cancelKey))
            {
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

    int MoveGrid(int current, int delta)
    {
        int next = current + delta;
        if (next < 0) next = 0;
        if (next >= selectableSkins.Count) next = selectableSkins.Count - 1;
        return next;
    }

    bool ProcessKonamiCode()
    {
        KeyCode pressed = KeyCode.None;

        if (Input.GetKeyDown(KeyCode.UpArrow)) pressed = KeyCode.UpArrow;
        else if (Input.GetKeyDown(KeyCode.DownArrow)) pressed = KeyCode.DownArrow;
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) pressed = KeyCode.LeftArrow;
        else if (Input.GetKeyDown(KeyCode.RightArrow)) pressed = KeyCode.RightArrow;
        else if (Input.GetKeyDown(KeyCode.B)) pressed = KeyCode.B;
        else if (Input.GetKeyDown(KeyCode.A)) pressed = KeyCode.A;
        else return false;

        if (pressed == _konami[konamiStep])
        {
            konamiStep++;
            if (konamiStep >= _konami.Length)
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

        konamiStep = (pressed == _konami[0]) ? 1 : 0;
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

        for (int i = 0; i < slots.Count; i++)
        {
            var img = slots[i];
            if (img == null) continue;

            var s = selectableSkins[i];
            bool isUnlocked = PlayerPersistentStats.IsSkinUnlocked(s);
            bool isSelected = (i == index);

            img.sprite = GetIdleSprite(s);
            img.enabled = img.sprite != null;

            img.color = isUnlocked ? normalTint : lockedTint;
            img.transform.localScale = isSelected ? selectedScale : Vector3.one;

            if (isSelected)
                img.color = isUnlocked ? selectedTint : lockedTint;
        }

        cursorDirty = true;
    }

    void ApplyIdleToAllSlots()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            var img = slots[i];
            if (img == null) continue;

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

        if (index < 0 || index >= slots.Count || slots[index] == null)
        {
            skinCursor.gameObject.SetActive(false);
            return;
        }

        var itemRt = slots[index].rectTransform;

        skinCursor.gameObject.SetActive(true);

        skinCursor.SetParent(itemRt, false);
        skinCursor.SetAsLastSibling();

        skinCursor.anchorMin = new Vector2(0.5f, 0.5f);
        skinCursor.anchorMax = new Vector2(0.5f, 0.5f);
        skinCursor.pivot = new Vector2(0.5f, 0.5f);

        skinCursor.anchoredPosition = new Vector2(0f, cursorYOffset);

        var baseSize = itemRt.rect.size;
        var targetSize = new Vector2(
            baseSize.x * cursorSizeMultiplier.x,
            baseSize.y * cursorSizeMultiplier.y
        ) + cursorPadding;

        skinCursor.sizeDelta = targetSize;

        skinCursor.localScale = cursorMatchSelectedScale ? itemRt.localScale : Vector3.one;
        skinCursor.localRotation = Quaternion.identity;

        if (cursorImage != null)
            cursorImage.color = Color.white;
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

    int Wrap(int v, int count)
    {
        if (count <= 0) return 0;
        v %= count;
        if (v < 0) v += count;
        return v;
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
}
