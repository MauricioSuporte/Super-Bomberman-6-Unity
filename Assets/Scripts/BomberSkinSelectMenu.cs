using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BomberSkinSelectMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] GameObject root;
    [SerializeField] Image backgroundImage;

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

    [Header("Background Sprite")]
    [SerializeField] Sprite backgroundSprite;

    [Header("Resources")]
    [SerializeField] string spritesResourcesPath = "Sprites/Bombers/Bomberman";
    [SerializeField] int idleFrameIndex = 16;

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

    readonly Dictionary<BomberSkin, Sprite> spriteCache = new();
    readonly List<Image> slots = new();

    int index;
    BomberSkin selected;

    AudioClip previousClip;
    float previousVolume;
    bool previousLoop;
    bool capturedPreviousMusic;

    static readonly KeyCode[] _konami = new[]
    {
        KeyCode.UpArrow, KeyCode.UpArrow,
        KeyCode.DownArrow, KeyCode.DownArrow,
        KeyCode.LeftArrow, KeyCode.RightArrow,
        KeyCode.LeftArrow, KeyCode.RightArrow,
        KeyCode.B, KeyCode.A
    };

    int konamiStep;

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

    void BuildGrid()
    {
        slots.Clear();

        if (gridRoot == null || skinItemPrefab == null)
            return;

        var grid = gridRoot.GetComponent<GridLayoutGroup>();
        if (grid != null)
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
            Destroy(child.gameObject);
        }

        if (skinItemPrefab != null)
            skinItemPrefab.gameObject.SetActive(false);

        for (int i = 0; i < selectableSkins.Count; i++)
        {
            var img = Instantiate(skinItemPrefab, gridRoot);
            img.gameObject.SetActive(true);
            img.preserveAspect = true;
            slots.Add(img);
        }
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
        else gameObject.SetActive(false);
    }

    public IEnumerator SelectSkinRoutine()
    {
        if (root == null)
            root = gameObject;

        root.transform.SetAsLastSibling();
        root.SetActive(true);

        if (backgroundImage != null && backgroundSprite != null)
            backgroundImage.sprite = backgroundSprite;

        if (slots.Count != selectableSkins.Count)
            BuildGrid();

        konamiStep = 0;

        StartSelectMusic();

        while (Input.GetKey(confirmKey) || Input.GetKey(leftKey) || Input.GetKey(rightKey) || Input.GetKey(upKey) || Input.GetKey(downKey))
            yield return null;

        yield return null;

        index = selectableSkins.IndexOf(PlayerPersistentStats.Skin);
        if (index < 0) index = 0;

        selected = PlayerPersistentStats.Skin;

        Refresh();

        bool done = false;
        while (!done)
        {
            bool didUnlockNow = ProcessKonamiCode();
            if (didUnlockNow)
                Refresh();

            bool moved = false;
            int nextIndex = index;

            if (Input.GetKeyDown(leftKey))
            {
                nextIndex = Wrap(index - 1, selectableSkins.Count);
                moved = true;
            }
            else if (Input.GetKeyDown(rightKey))
            {
                nextIndex = Wrap(index + 1, selectableSkins.Count);
                moved = true;
            }
            else if (Input.GetKeyDown(upKey))
            {
                nextIndex = MoveGrid(index, -columns);
                moved = true;
            }
            else if (Input.GetKeyDown(downKey))
            {
                nextIndex = MoveGrid(index, +columns);
                moved = true;
            }

            if (moved)
            {
                if (nextIndex != index)
                {
                    index = nextIndex;
                    PlaySfx(moveCursorSfx, moveCursorSfxVolume);
                    Refresh();
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

                    PlaySfx(confirmSfx, confirmSfxVolume);
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

        StopSelectMusicAndRestorePrevious();
        Hide();
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
        if (!Input.anyKeyDown)
            return false;

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

    void StopSelectMusicAndRestorePrevious()
    {
        var music = GameMusicController.Instance;
        if (music == null)
            return;

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

    void Refresh()
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
    }

    Sprite GetIdleSprite(BomberSkin skin)
    {
        if (spriteCache.TryGetValue(skin, out var cached))
            return cached;

        string sheetName = skin + "Bomber";
        string sheetPath = $"{spritesResourcesPath}/{sheetName}";
        var sprites = Resources.LoadAll<Sprite>(sheetPath);

        string wantedName = $"{sheetName}_{idleFrameIndex}";

        Sprite chosen = null;

        if (sprites != null && sprites.Length > 0)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                var s = sprites[i];
                if (s != null && s.name == wantedName)
                {
                    chosen = s;
                    break;
                }
            }

            if (chosen == null)
            {
                for (int i = 0; i < sprites.Length; i++)
                {
                    if (sprites[i] != null)
                    {
                        chosen = sprites[i];
                        break;
                    }
                }
            }
        }

        spriteCache[skin] = chosen;
        return chosen;
    }

    int Wrap(int v, int count)
    {
        if (count <= 0) return 0;
        v %= count;
        if (v < 0) v += count;
        return v;
    }
}
