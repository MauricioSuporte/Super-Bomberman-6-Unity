using UnityEngine;

public class GlobalUnlockController : MonoBehaviour
{
    private static readonly bool EnableSurgicalLogs = false;

    private static GlobalUnlockController instance;

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
        {
            SLog("Bootstrap ignored | instance already exists");
            return;
        }

        GameObject go = new(nameof(GlobalUnlockController));
        instance = go.AddComponent<GlobalUnlockController>();
        DontDestroyOnLoad(go);

        SLog("Bootstrap created controller instance");
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            SLog("Awake destroying duplicate instance");
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        UnlockProgress.ReloadFromDisk();

        SLog($"Awake | SaveFileExists={UnlockProgress.SaveFileExists()} | GrayUnlocked={UnlockProgress.IsUnlocked(BomberSkin.Gray)} | OrangeUnlocked={UnlockProgress.IsUnlocked(BomberSkin.Orange)} | PurpleUnlocked={UnlockProgress.IsUnlocked(BomberSkin.Purple)} | BossRushUnlocked={UnlockProgress.IsBossRushUnlocked()}");

        UnlockToastPresenter.EnsureInScene();
    }

    private void OnEnable()
    {
        UnlockProgress.OnSkinUnlocked -= HandleSkinUnlocked;
        UnlockProgress.OnSkinUnlocked += HandleSkinUnlocked;

        UnlockProgress.OnBossRushUnlocked -= HandleBossRushUnlocked;
        UnlockProgress.OnBossRushUnlocked += HandleBossRushUnlocked;

        UnlockProgress.OnHardcoreUnlocked -= HandleHardcoreUnlocked;
        UnlockProgress.OnHardcoreUnlocked += HandleHardcoreUnlocked;

        UnlockProgress.OnBattleModeStage11Unlocked -= HandleBattleModeStage11Unlocked;
        UnlockProgress.OnBattleModeStage11Unlocked += HandleBattleModeStage11Unlocked;
        UnlockProgress.OnBattleModeStageUnlocked -= HandleBattleModeStageUnlocked;
        UnlockProgress.OnBattleModeStageUnlocked += HandleBattleModeStageUnlocked;

        SLog("OnEnable | unlock listeners registered");
    }

    private void OnDisable()
    {
        UnlockProgress.OnSkinUnlocked -= HandleSkinUnlocked;
        UnlockProgress.OnBossRushUnlocked -= HandleBossRushUnlocked;
        UnlockProgress.OnHardcoreUnlocked -= HandleHardcoreUnlocked;
        UnlockProgress.OnBattleModeStage11Unlocked -= HandleBattleModeStage11Unlocked;
        UnlockProgress.OnBattleModeStageUnlocked -= HandleBattleModeStageUnlocked;

        SLog("OnDisable | unlock listeners removed");
    }

    private void Update()
    {
        if (UnlockProgress.IsUnlocked(BomberSkin.Gray))
        {
            if (konamiStep != 0)
            {
                konamiStep = 0;
                SLog("Update | Gray already unlocked, konamiStep reset");
            }

            return;
        }

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

        SLog($"Input token received={pressed} | currentStep={konamiStep}");

        if (AdvanceKonami(pressed))
        {
            SLog("Konami sequence completed | attempting Gray Bomber unlock");

            bool unlockedGray = UnlockProgress.Unlock(BomberSkin.Gray);

            SLog($"Gray unlock result | Gray={unlockedGray}");

            if (unlockedGray)
            {
                for (int p = 1; p <= 4; p++)
                    PlayerPersistentStats.ClampSelectedSkinIfLocked(p);
            }
            else
            {
                SLog("Gray Bomber already unlocked");
            }
        }
    }

    private void HandleSkinUnlocked(BomberSkin skin)
    {
        SLog($"HandleSkinUnlocked | skin={skin}");
    }

    private void HandleBossRushUnlocked()
    {
        SLog("HandleBossRushUnlocked");
    }

    private void HandleHardcoreUnlocked()
    {
        SLog("HandleHardcoreUnlocked");
    }

    private void HandleBattleModeStage11Unlocked()
    {
        SLog("HandleBattleModeStage11Unlocked");
    }

    private void HandleBattleModeStageUnlocked(int stageIndex)
    {
        if (stageIndex == 11)
            return;

        SLog($"HandleBattleModeStageUnlocked | stage={stageIndex}");
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
            SLog($"AdvanceKonami match | nextStep={konamiStep}/{konamiTokens.Length}");

            if (konamiStep >= konamiTokens.Length)
            {
                konamiStep = 0;
                SLog("AdvanceKonami completed full sequence");
                return true;
            }

            return false;
        }

        int previous = konamiStep;
        konamiStep = pressed == konamiTokens[0] ? 1 : 0;

        SLog($"AdvanceKonami mismatch | pressed={pressed} | previousStep={previous} | resetStep={konamiStep}");
        return false;
    }

    private static void SLog(string message)
    {
        if (!EnableSurgicalLogs)
            return;

        Debug.Log($"[GlobalUnlockFlow] {message}");
    }
}
