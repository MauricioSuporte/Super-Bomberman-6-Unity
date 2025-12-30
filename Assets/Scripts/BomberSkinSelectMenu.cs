using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BomberSkinSelectMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] GameObject root;
    [SerializeField] Image backgroundImage;
    [SerializeField] Image bomberImage;
    [SerializeField] Text nameText;
    [SerializeField] Text hintText;

    [Header("Background Sprite")]
    [SerializeField] Sprite backgroundSprite;

    [Header("Resources")]
    [SerializeField] string spritesResourcesPath = "Sprites/Bombers/Bomberman";
    [SerializeField] int idleFrameIndex = 16;

    [Header("Music")]
    [SerializeField] AudioClip selectMusic;
    [SerializeField, Range(0f, 1f)] float selectMusicVolume = 1f;
    [SerializeField] bool loopSelectMusic = true;

    [Header("Input")]
    [SerializeField] KeyCode leftKey = KeyCode.LeftArrow;
    [SerializeField] KeyCode rightKey = KeyCode.RightArrow;
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

    int index;
    BomberSkin selected;

    AudioClip previousClip;
    float previousVolume;
    bool previousLoop;
    bool capturedPreviousMusic;

    void Awake()
    {
        if (root == null)
            root = gameObject;

        if (backgroundImage != null && backgroundSprite != null)
            backgroundImage.sprite = backgroundSprite;

        if (root != null)
            root.SetActive(false);
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

        StartSelectMusic();

        while (Input.GetKey(confirmKey) || Input.GetKey(leftKey) || Input.GetKey(rightKey))
            yield return null;

        yield return null;

        index = selectableSkins.IndexOf(PlayerPersistentStats.Skin);
        if (index < 0) index = 0;

        selected = PlayerPersistentStats.Skin;

        Refresh();

        bool done = false;
        while (!done)
        {
            if (Input.GetKeyDown(leftKey))
            {
                index = Wrap(index - 1, selectableSkins.Count);
                Refresh();
            }
            else if (Input.GetKeyDown(rightKey))
            {
                index = Wrap(index + 1, selectableSkins.Count);
                Refresh();
            }
            else if (Input.GetKeyDown(confirmKey))
            {
                var skin = selectableSkins[index];
                if (PlayerPersistentStats.IsSkinUnlocked(skin))
                {
                    selected = skin;
                    done = true;
                }
                else
                {
                    if (hintText != null) hintText.text = "Bloqueada";
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
        var skin = selectableSkins[index];
        bool unlocked = PlayerPersistentStats.IsSkinUnlocked(skin);

        if (nameText != null)
            nameText.text = skin.ToString();

        if (hintText != null)
            hintText.text = unlocked ? "← → escolher  |  Enter confirmar" : "Bloqueada (Golden)";

        if (bomberImage != null)
        {
            bomberImage.sprite = GetIdleSprite(skin);
            bomberImage.enabled = bomberImage.sprite != null;
            bomberImage.preserveAspect = true;
            bomberImage.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.35f);
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
