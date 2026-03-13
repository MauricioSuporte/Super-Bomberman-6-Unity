using UnityEngine;

public class BomberSkinGlobalUnlockController : MonoBehaviour
{
    private const string LifeUpResourcesPath = "Sounds/LifeUp";

    private static BomberSkinGlobalUnlockController instance;

    private AudioClip lifeUpSfx;
    private int konamiStep;

    private enum KonamiToken
    {
        None = 0,
        Up,
        Down,
        Left,
        Right,
        B,
        A
    }

    private static readonly KonamiToken[] konamiTokens =
    {
        KonamiToken.Up, KonamiToken.Up,
        KonamiToken.Down, KonamiToken.Down,
        KonamiToken.Left, KonamiToken.Right,
        KonamiToken.Left, KonamiToken.Right,
        KonamiToken.B, KonamiToken.A
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject(nameof(BomberSkinGlobalUnlockController));
        instance = go.AddComponent<BomberSkinGlobalUnlockController>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        BomberSkinUnlockProgress.ReloadFromDisk();
        lifeUpSfx = Resources.Load<AudioClip>(LifeUpResourcesPath);
    }

    private void Update()
    {
        var input = PlayerInputManager.Instance;
        if (input == null)
            return;

        bool upDown = false;
        bool downDown = false;
        bool leftDown = false;
        bool rightDown = false;
        bool bDown = false;
        bool aDown = false;

        for (int playerId = 1; playerId <= 4; playerId++)
        {
            upDown |= input.GetDown(playerId, PlayerAction.MoveUp);
            downDown |= input.GetDown(playerId, PlayerAction.MoveDown);
            leftDown |= input.GetDown(playerId, PlayerAction.MoveLeft);
            rightDown |= input.GetDown(playerId, PlayerAction.MoveRight);
            bDown |= input.GetDown(playerId, PlayerAction.ActionB);
            aDown |= input.GetDown(playerId, PlayerAction.ActionA);
        }

        KonamiToken pressed = ReadKonamiTokenThisFrame(upDown, downDown, leftDown, rightDown, bDown, aDown);
        if (pressed == KonamiToken.None)
            return;

        if (AdvanceKonami(pressed))
        {
            bool unlockedNow = BomberSkinUnlockProgress.UnlockGray();
            if (unlockedNow)
            {
                for (int p = 1; p <= 4; p++)
                    PlayerPersistentStats.ClampSelectedSkinIfLocked(p);

                PlayUnlockSfx();
            }
        }
    }

    private KonamiToken ReadKonamiTokenThisFrame(bool upDown, bool downDown, bool leftDown, bool rightDown, bool bDown, bool aDown)
    {
        if (upDown) return KonamiToken.Up;
        if (downDown) return KonamiToken.Down;
        if (leftDown) return KonamiToken.Left;
        if (rightDown) return KonamiToken.Right;
        if (bDown) return KonamiToken.B;
        if (aDown) return KonamiToken.A;
        return KonamiToken.None;
    }

    private bool AdvanceKonami(KonamiToken pressed)
    {
        if (pressed == konamiTokens[konamiStep])
        {
            konamiStep++;

            if (konamiStep >= konamiTokens.Length)
            {
                konamiStep = 0;
                return true;
            }

            return false;
        }

        konamiStep = pressed == konamiTokens[0] ? 1 : 0;
        return false;
    }

    private void PlayUnlockSfx()
    {
        if (lifeUpSfx == null)
            return;

        var music = GameMusicController.Instance;
        if (music != null)
        {
            music.PlaySfx(lifeUpSfx, 1f);
            return;
        }

        GameObject temp = new GameObject("TempUnlockSfx");
        DontDestroyOnLoad(temp);

        var source = temp.AddComponent<AudioSource>();
        source.clip = lifeUpSfx;
        source.volume = 1f;
        source.loop = false;
        source.playOnAwake = false;
        source.Play();

        Destroy(temp, lifeUpSfx.length + 0.1f);
    }
}