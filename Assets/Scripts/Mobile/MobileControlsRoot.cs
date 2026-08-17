using UnityEngine;

public class MobileControlsRoot : MonoBehaviour
{
    public static MobileControlsRoot Instance { get; private set; }

    [SerializeField] private bool showOnlyOnMobile = true;

    private MobileButton actionAButton;
    private MobileButton actionBButton;
    private MobileButton actionCButton;
    private Sprite placeBombSprite;
    private Sprite powerGloveSprite;
    private Sprite punchBombSprite;
    private Sprite louieAbilitySprite;
    private Sprite detonateControlBombSprite;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CacheContextButtonsAndSprites();
        ApplyPlatformVisibility();
    }

    public void RefreshVisibilityFromSavedPreference()
    {
        ApplyPlatformVisibility();
    }

    public void SetTouchButtonsVisible(bool visible)
    {
        SaveSystem.SetMobileTouchButtonsVisible(visible);
        ApplyPlatformVisibility();
    }

    void ApplyPlatformVisibility()
    {
        bool shouldShow = true;

        if (showOnlyOnMobile)
            shouldShow = Application.isMobilePlatform;

        if (shouldShow)
            shouldShow = SaveSystem.GetMobileTouchButtonsVisible();

        gameObject.SetActive(shouldShow);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        RefreshContextIcons();
    }

    void CacheContextButtonsAndSprites()
    {
        foreach (var button in GetComponentsInChildren<MobileButton>(true))
        {
            switch (button.Action)
            {
                case PlayerAction.ActionA: actionAButton = button; break;
                case PlayerAction.ActionB: actionBButton = button; break;
                case PlayerAction.ActionC: actionCButton = button; break;
            }
        }

        placeBombSprite = Resources.Load<Sprite>("UI/Place_Bomb");
        powerGloveSprite = Resources.Load<Sprite>("UI/Use_Power_Glove");
        punchBombSprite = Resources.Load<Sprite>("UI/Use_Box_Glove");
        louieAbilitySprite = Resources.Load<Sprite>("UI/Louie_hability");
        detonateControlBombSprite = Resources.Load<Sprite>("UI/Acionate_Bomb");
    }

    void RefreshContextIcons()
    {
        if (!IsGameplayStage())
        {
            SetContextIcons(null, null, null);
            return;
        }

        GameObject player = FindPlayerOne();
        if (player == null)
        {
            SetContextIcons(null, null, null);
            return;
        }

        var movement = player.GetComponent<MovementController>();
        var abilities = player.GetComponent<AbilitySystem>();
        var bombs = player.GetComponent<BombController>();
        var powerGlove = player.GetComponent<PowerGloveAbility>();

        bool canUsePowerGlove = powerGlove != null && powerGlove.CanPickupBombAtCurrentPosition();
        bool canUsePunch = abilities != null && abilities.IsEnabled(BombPunchAbility.AbilityId);
        bool canDetonateControlBomb = abilities != null &&
                                      abilities.IsEnabled(ControlBombAbility.AbilityId) &&
                                      bombs != null &&
                                      bombs.PeekOldestControlledBomb() != null;
        bool isMounted = movement != null && movement.IsMounted;

        SetContextIcons(
            canUsePowerGlove ? powerGloveSprite : placeBombSprite,
            canDetonateControlBomb ? detonateControlBombSprite : null,
            isMounted ? louieAbilitySprite : canUsePunch ? punchBombSprite : null);
    }

    void SetContextIcons(Sprite actionA, Sprite actionB, Sprite actionC)
    {
        actionAButton?.SetContextIcon(actionA);
        actionBButton?.SetContextIcon(actionB);
        actionCButton?.SetContextIcon(actionC);
    }

    static bool IsGameplayStage()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        return sceneName.StartsWith("Stage_", System.StringComparison.OrdinalIgnoreCase) ||
               sceneName.StartsWith("BattleMode_", System.StringComparison.OrdinalIgnoreCase);
    }

    static GameObject FindPlayerOne()
    {
        foreach (var identity in PlayerIdentity.ActivePlayers)
        {
            if (identity != null && identity.playerId == GameSession.MinPlayerId)
                return identity.gameObject;
        }

        return null;
    }
}
