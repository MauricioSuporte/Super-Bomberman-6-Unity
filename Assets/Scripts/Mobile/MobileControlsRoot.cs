using UnityEngine;

public class MobileControlsRoot : MonoBehaviour
{
    public static MobileControlsRoot Instance { get; private set; }

    [SerializeField] private bool showOnlyOnMobile = true;

    private MobileButton actionAButton;
    private MobileButton actionBButton;
    private MobileButton actionCButton;
    private MobileButton selectButton;
    private MobileButton actionRButton;
    private Sprite placeBombSprite;
    private Sprite powerGloveSprite;
    private Sprite punchBombSprite;
    private Sprite louieAbilitySprite;
    private Sprite detonateControlBombSprite;
    private Sprite dismountSprite;

    public static bool Editing { get; private set; }
    public int SelectedControl { get; set; }
    private CanvasGroup visibility;
    private MobileDynamicJoystick joystick;
    private readonly RectTransform[] targets = new RectTransform[8];
    private readonly Vector2[] originalSizes = new Vector2[8];
    private readonly Vector2[] originalAnchors = new Vector2[8];
    private readonly Vector2[] originalPositions = new Vector2[8];
    private int screenWidth;
    private int screenHeight;
    private float lastTouch;
    private bool settingsOpen;
    public bool Portrait => Screen.height > Screen.width;

    public void NotifyTouch() => lastTouch = Time.unscaledTime;

    public void SetSettingsOpen(bool value)
    {
        settingsOpen = value;
        if (!value) SetEditing(false);
        ApplyPlatformVisibility();
    }

    public void SetEditing(bool value)
    {
        Editing = value;
        joystick?.Release();
        foreach (var b in GetComponentsInChildren<MobileButton>(true)) b.Release();
        ApplyPlatformVisibility();
        ApplyLayout();
    }

    void Start()
    {
        joystick = GetComponentInChildren<MobileDynamicJoystick>(true);
        targets[0] = joystick.Visual;
        foreach (var b in GetComponentsInChildren<MobileButton>(true))
        {
            int index = b.Action switch
            {
                PlayerAction.ActionA => 1, PlayerAction.ActionB => 2, PlayerAction.ActionC => 3,
                PlayerAction.Start => 4, PlayerAction.Select => 5, PlayerAction.ActionL => 6,
                PlayerAction.ActionR => 7, _ => -1
            };
            if (index >= 0) targets[index] = (RectTransform)b.transform;
        }
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null) continue;
            originalSizes[i] = targets[i].sizeDelta;
            originalAnchors[i] = targets[i].anchorMin;
            originalPositions[i] = targets[i].anchoredPosition;
            var handle = targets[i].gameObject.AddComponent<MobileControlLayoutHandle>();
            handle.Index = i;
            handle.Target = targets[i];
        }
        // The directional hit area follows the visual; dynamic mode expands it below.
        ApplyLayout();
    }

    public void StorePosition(int index)
    {
        var target = targets[index];
        var parent = (RectTransform)target.parent;
        Vector2 point = parent.InverseTransformPoint(target.position);
        var entry = SaveSystem.GetTouchControls().Layout(Portrait)[index];
        entry.positioned = true;
        entry.x = Mathf.InverseLerp(parent.rect.xMin, parent.rect.xMax, point.x);
        entry.y = Mathf.InverseLerp(parent.rect.yMin, parent.rect.yMax, point.y);
        ApplyLayout();
    }

    public void ApplyLayout()
    {
        if (joystick == null) return;
        joystick.Release();
        var area = (RectTransform)joystick.transform;
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = area.offsetMax = Vector2.zero;
        var settings = SaveSystem.GetTouchControls();
        settings.Normalize();
        var layout = settings.Layout(Portrait);
        for (int i = 0; i < targets.Length; i++)
        {
            var target = targets[i];
            if (target == null) continue;
            var parent = (RectTransform)target.parent;
            target.anchorMin = target.anchorMax = originalAnchors[i];
            target.sizeDelta = originalSizes[i] * layout[i].scale;
            target.anchoredPosition = originalPositions[i];
            if (layout[i].positioned)
                target.position = parent.TransformPoint(new Vector2(
                    Mathf.Lerp(parent.rect.xMin, parent.rect.xMax, layout[i].x),
                    Mathf.Lerp(parent.rect.yMin, parent.rect.yMax, layout[i].y)));
            ClampToSafeArea(target);
        }
        joystick.RefreshVisual();
        // A full-screen transparent hit area supports the dynamic stick. It is first
        // in sibling order so all action buttons retain priority.
        var image = area.GetComponent<UnityEngine.UI.Image>();
        image.raycastTarget = !Editing && settings.analog && settings.dynamicAnalog;
        joystick.Visual.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
    }

    static void ClampToSafeArea(RectTransform target)
    {
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Rect safe = Screen.safeArea;
        float dx = Mathf.Max(0, safe.xMin - corners[0].x) - Mathf.Max(0, corners[2].x - safe.xMax);
        float dy = Mathf.Max(0, safe.yMin - corners[0].y) - Mathf.Max(0, corners[2].y - safe.yMax);
        target.position += new Vector3(dx, dy, 0);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
        DontDestroyOnLoad(gameObject);

        var canvas = GetComponentInChildren<Canvas>(true);
        visibility = canvas.gameObject.AddComponent<CanvasGroup>();
        NotifyTouch();
        CacheContextButtonsAndSprites();
        ApplyPlatformVisibility();
    }

    public void RefreshVisibilityFromSavedPreference()
    {
        ApplyPlatformVisibility();
        ApplyLayout();
    }

    public void SetTouchButtonsVisible(bool visible)
    {
        SaveSystem.SetMobileTouchButtonsVisible(visible);
        ApplyPlatformVisibility();
    }

    void ApplyPlatformVisibility()
    {
        if (visibility == null) return;
        bool show = Editing || ((!showOnlyOnMobile || Application.isMobilePlatform) &&
            SaveSystem.GetMobileTouchButtonsVisible() && !settingsOpen);
        bool idle = SaveSystem.GetTouchControls().autoHide &&
            Time.unscaledTime - lastTouch > SaveSystem.GetTouchControls().hideDelay;
        visibility.alpha = show && (!idle || Editing) ? SaveSystem.GetTouchControls().opacity : 0;
        visibility.blocksRaycasts = show;
        if (!show) MobileInputBridge.Instance?.ClearAll();
    }

    void OnSceneChanged(UnityEngine.SceneManagement.Scene oldScene, UnityEngine.SceneManagement.Scene newScene)
    {
        joystick?.Release();
        foreach (var button in GetComponentsInChildren<MobileButton>(true)) button.Release();
        MobileInputBridge.Instance?.ClearAll();
        NotifyTouch();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (screenWidth != Screen.width || screenHeight != Screen.height)
        {
            screenWidth = Screen.width;
            screenHeight = Screen.height;
            ApplyLayout();
        }
        var touch = UnityEngine.InputSystem.Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.isPressed) NotifyTouch();
        ApplyPlatformVisibility();
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
                case PlayerAction.Select: selectButton = button; break;
                case PlayerAction.ActionR: actionRButton = button; break;
            }
        }

        placeBombSprite = Resources.Load<Sprite>("UI/Place_Bomb");
        powerGloveSprite = Resources.Load<Sprite>("UI/Use_Power_Glove");
        punchBombSprite = Resources.Load<Sprite>("UI/Use_Box_Glove");
        louieAbilitySprite = Resources.Load<Sprite>("UI/Louie_hability");
        detonateControlBombSprite = Resources.Load<Sprite>("UI/Acionate_Bomb");
        dismountSprite = Resources.Load<Sprite>("UI/Dismount");
    }

    void RefreshContextIcons()
    {
        if (Editing || !IsGameplayStage() || GamePauseController.IsPaused || Time.timeScale == 0f)
        {
            actionRButton?.gameObject.SetActive(true);
            SetContextIcons(null, null, null);
            selectButton?.gameObject.SetActive(true);
            selectButton?.SetContextVisual(null);
            return;
        }

        GameObject player = FindPlayerOne();
        if (player == null)
        {
            actionRButton?.gameObject.SetActive(true);
            SetContextIcons(null, null, null);
            selectButton?.gameObject.SetActive(true);
            selectButton?.SetContextVisual(null);
            return;
        }

        var movement = player.GetComponent<MovementController>();
        var abilities = player.GetComponent<AbilitySystem>();
        var bombs = player.GetComponent<BombController>();
        var powerGlove = player.GetComponent<PowerGloveAbility>();
        var companion = player.GetComponent<PlayerMountCompanion>();

        bool canStopKick = abilities != null && (abilities.IsEnabled(BombKickAbility.AbilityId) ||
            abilities.IsEnabled(YellowLouieKickAbility.AbilityId));
        actionRButton?.gameObject.SetActive(!SaveSystem.GetTouchControls().hideUnused || canStopKick);
        bool canUsePowerGlove = powerGlove != null && powerGlove.CanPickupBombAtCurrentPosition();
        bool canUsePunch = abilities != null && abilities.IsEnabled(BombPunchAbility.AbilityId);
        bool canDetonateControlBomb = abilities != null &&
                                      abilities.IsEnabled(ControlBombAbility.AbilityId) &&
                                      bombs != null &&
                                      bombs.PeekOldestControlledBomb() != null;
        bool isMounted = movement != null && movement.IsMounted;
        bool canDismount = isMounted && companion != null && companion.HasMountedLouie();

        SetContextIcons(
            canUsePowerGlove ? powerGloveSprite : placeBombSprite,
            canDetonateControlBomb ? detonateControlBombSprite : null,
            isMounted ? louieAbilitySprite : canUsePunch ? punchBombSprite : null);
        selectButton?.gameObject.SetActive(!SaveSystem.GetTouchControls().hideUnused || canDismount);
        selectButton?.SetContextVisual(SaveSystem.GetTouchControls().actionIcons && canDismount ? dismountSprite : null);
    }

    void SetContextIcons(Sprite actionA, Sprite actionB, Sprite actionC)
    {
        var settings = SaveSystem.GetTouchControls();
        bool hide = settings.hideUnused && IsGameplayStage() && !Editing && Time.timeScale > 0f;
        actionAButton?.gameObject.SetActive(!hide || actionA != null);
        actionBButton?.gameObject.SetActive(!hide || actionB != null);
        actionCButton?.gameObject.SetActive(!hide || actionC != null);
        actionAButton?.SetContextIcon(settings.actionIcons ? actionA : null);
        actionBButton?.SetContextIcon(settings.actionIcons ? actionB : null);
        actionCButton?.SetContextIcon(settings.actionIcons ? actionC : null);
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
