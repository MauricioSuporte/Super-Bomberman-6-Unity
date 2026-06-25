using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[DefaultExecutionOrder(-200)]
public class PlayerInputManager : MonoBehaviour
{
    private const int MinSupportedPlayerId = 1;
    private const int MaxSupportedPlayerId = 6;
    private const int PlayerActionCount = (int)PlayerAction.ActionR + 1;

    public static PlayerInputManager Instance { get; private set; }

    [Header("Players (fallback if GameSession is missing)")]
    [Tooltip("How many player profiles to create (1-6). Used only if GameSession.Instance is null.")]
    [Range(MinSupportedPlayerId, MaxSupportedPlayerId)]
    [SerializeField] int maxPlayers = 4;

    [Header("Analog As Dpad Fallback")]
    private readonly float analogThreshold = 0.35f;
    [SerializeField] bool includeRightStickAsDpad = false;

    [Header("Boat Input Gate")]
    [Tooltip("If true: while the player is riding a Boat, only MoveUp/Down/Left/Right and Start are accepted. Everything else returns false.")]
    [SerializeField] private bool blockNonDirectionalInputsWhileRidingBoat = true;

    [Header("Spring Launcher Input Gate")]
    [Tooltip("If true: while the player is using a SpringLauncher, only MoveUp/Down/Left/Right and Start are accepted. Everything else returns false.")]
    [SerializeField] private bool blockNonDirectionalInputsWhileUsingSpringLauncher = true;

    [Tooltip("How often to refresh playerId -> MovementController mapping (seconds).")]
    [SerializeField, Min(0.05f)] private float refreshPlayersMapEverySeconds = 0.5f;

    readonly Dictionary<int, PlayerInputProfile> players = new();

    readonly Dictionary<int, bool> prevUp = new();
    readonly Dictionary<int, bool> prevDown = new();
    readonly Dictionary<int, bool> prevLeft = new();
    readonly Dictionary<int, bool> prevRight = new();

    readonly Dictionary<int, bool> curUp = new();
    readonly Dictionary<int, bool> curDown = new();
    readonly Dictionary<int, bool> curLeft = new();
    readonly Dictionary<int, bool> curRight = new();

    private readonly Dictionary<int, MovementController> playerControllers = new();
    private readonly HashSet<int> playersUsingSpringLauncher = new();
    private readonly bool[] playersRidingBoat = new bool[MaxSupportedPlayerId + 1];
    private readonly bool[] playersUsingSpringLauncherState = new bool[MaxSupportedPlayerId + 1];
    private readonly bool[,] rawHeldCache = new bool[MaxSupportedPlayerId + 1, PlayerActionCount];
    private readonly bool[,] rawDownCache = new bool[MaxSupportedPlayerId + 1, PlayerActionCount];
    private readonly int[,] rawHeldCacheStamp = new int[MaxSupportedPlayerId + 1, PlayerActionCount];
    private readonly int[,] rawDownCacheStamp = new int[MaxSupportedPlayerId + 1, PlayerActionCount];
    private readonly bool[,] syntheticHeld = new bool[MaxSupportedPlayerId + 1, PlayerActionCount];
    private readonly bool[,] syntheticPreviousHeld = new bool[MaxSupportedPlayerId + 1, PlayerActionCount];
    private readonly int[,] syntheticTapFrame = new int[MaxSupportedPlayerId + 1, PlayerActionCount];
    private readonly bool[] anyHeldInputCache = new bool[MaxSupportedPlayerId + 1];
    private readonly int[] anyHeldInputCacheStamp = new int[MaxSupportedPlayerId + 1];

    private static readonly Dictionary<KeyCode, KeyControl> keyboardControlsByKeyCode = new();
    private static Keyboard cachedKeyboard;
    private static bool keyboardControlCacheReady;

    private float nextPlayersMapRefreshTime;
    private int playerStateCacheStamp;
    private int lastForcedPlayersMapRefreshStamp;

    int PlayerCount
    {
        get
        {
            var gs = GameSession.Instance;
            if (gs != null)
                return Mathf.Clamp(gs.ActivePlayerCount, MinSupportedPlayerId, MaxSupportedPlayerId);

            return Mathf.Clamp(maxPlayers, MinSupportedPlayerId, MaxSupportedPlayerId);
        }
    }

    bool IsConfiguredActivePlayer(int playerId)
    {
        playerId = Mathf.Clamp(playerId, MinSupportedPlayerId, MaxSupportedPlayerId);

        var gs = GameSession.Instance;
        if (gs != null)
            return gs.IsPlayerActive(playerId);

        return playerId <= PlayerCount;
    }

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureProfilesForPlayerCount();
        RefreshPlayersMap(force: true);
    }

    void Update()
    {
        using var performanceSample = BattleModePerformanceMarkers.InputUpdate.Auto();

        EnsureProfilesForPlayerCount();
        RefreshPlayerStateCache(forcePlayersMapRefresh: false);

        for (int id = MinSupportedPlayerId; id <= MaxSupportedPlayerId; id++)
        {
            var p = GetPlayer(id);
            ReadDirectionalDigital(p, id, out bool up, out bool down, out bool left, out bool right);

            curUp[id] = up;
            curDown[id] = down;
            curLeft[id] = left;
            curRight[id] = right;
        }
    }

    void LateUpdate()
    {
        using var performanceSample = BattleModePerformanceMarkers.InputUpdate.Auto();

        for (int id = MinSupportedPlayerId; id <= MaxSupportedPlayerId; id++)
        {
            prevUp[id] = curUp[id];
            prevDown[id] = curDown[id];
            prevLeft[id] = curLeft[id];
            prevRight[id] = curRight[id];

            for (int actionIndex = 0; actionIndex < PlayerActionCount; actionIndex++)
                syntheticPreviousHeld[id, actionIndex] = syntheticHeld[id, actionIndex];
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void EnsureProfilesForPlayerCount()
    {
        for (int id = MinSupportedPlayerId; id <= MaxSupportedPlayerId; id++)
        {
            if (!players.TryGetValue(id, out var p) || p == null)
                players[id] = new PlayerInputProfile(id);

            if (!prevUp.ContainsKey(id))
                prevUp[id] = prevDown[id] = prevLeft[id] = prevRight[id] = false;

            if (!curUp.ContainsKey(id))
                curUp[id] = curDown[id] = curLeft[id] = curRight[id] = false;
        }
    }

    private void RefreshPlayersMap(bool force)
    {
        if (!force && Time.time < nextPlayersMapRefreshTime)
            return;

        nextPlayersMapRefreshTime = Time.time + Mathf.Max(0.05f, refreshPlayersMapEverySeconds);

        playerControllers.Clear();

        var all = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude);
        if (all == null)
            return;

        for (int i = 0; i < all.Length; i++)
        {
            var mc = all[i];
            if (mc == null)
                continue;

            if (!mc.CompareTag("Player"))
                continue;

            if (!mc.TryGetComponent<PlayerIdentity>(out var identity))
                identity = mc.GetComponentInParent<PlayerIdentity>(true);

            if (identity == null)
                continue;

            int pid = Mathf.Clamp(identity.playerId, 1, 6);
            playerControllers[pid] = mc;
        }
    }

    private static int GetFrameStamp()
    {
        return Time.frameCount + 1;
    }

    private void InvalidatePlayerStateCache()
    {
        playerStateCacheStamp = 0;
    }

    private void RefreshPlayerStateCache(bool forcePlayersMapRefresh)
    {
        int frameStamp = GetFrameStamp();
        if (!forcePlayersMapRefresh && playerStateCacheStamp == frameStamp)
            return;

        RefreshPlayersMap(force: forcePlayersMapRefresh);

        for (int id = MinSupportedPlayerId; id <= MaxSupportedPlayerId; id++)
        {
            bool isActivePlayer = IsConfiguredActivePlayer(id);
            playersRidingBoat[id] = false;
            playersUsingSpringLauncherState[id] = isActivePlayer && playersUsingSpringLauncher.Contains(id);

            if (!isActivePlayer)
                continue;

            if (playerControllers.TryGetValue(id, out var mc) && mc != null)
                playersRidingBoat[id] = BoatRideZone.IsRidingBoat(mc);
        }

        playerStateCacheStamp = frameStamp;

        if (forcePlayersMapRefresh)
            lastForcedPlayersMapRefreshStamp = frameStamp;
    }

    private static bool TryGetActionIndex(PlayerAction action, out int actionIndex)
    {
        actionIndex = (int)action;
        return actionIndex >= 0 && actionIndex < PlayerActionCount;
    }

    public void SetSpringLauncherInputGate(int playerId, bool enabled)
    {
        playerId = Mathf.Clamp(playerId, MinSupportedPlayerId, MaxSupportedPlayerId);

        if (enabled)
            playersUsingSpringLauncher.Add(playerId);
        else
            playersUsingSpringLauncher.Remove(playerId);

        InvalidatePlayerStateCache();
    }

    public void SetSyntheticHeld(int playerId, PlayerAction action, bool held)
    {
        playerId = Mathf.Clamp(playerId, MinSupportedPlayerId, MaxSupportedPlayerId);

        if (!TryGetActionIndex(action, out int actionIndex))
            return;

        syntheticHeld[playerId, actionIndex] = held;
        anyHeldInputCacheStamp[playerId] = 0;
    }

    public void TapSynthetic(int playerId, PlayerAction action)
    {
        playerId = Mathf.Clamp(playerId, MinSupportedPlayerId, MaxSupportedPlayerId);

        if (!TryGetActionIndex(action, out int actionIndex))
            return;

        syntheticTapFrame[playerId, actionIndex] = Time.frameCount;
        anyHeldInputCacheStamp[playerId] = 0;
    }

    public void ClearSyntheticPlayer(int playerId)
    {
        playerId = Mathf.Clamp(playerId, MinSupportedPlayerId, MaxSupportedPlayerId);

        for (int actionIndex = 0; actionIndex < PlayerActionCount; actionIndex++)
        {
            syntheticHeld[playerId, actionIndex] = false;
            syntheticPreviousHeld[playerId, actionIndex] = false;
            syntheticTapFrame[playerId, actionIndex] = 0;
        }

        anyHeldInputCacheStamp[playerId] = 0;
    }

    public void ClearAllSyntheticInputs()
    {
        for (int id = MinSupportedPlayerId; id <= MaxSupportedPlayerId; id++)
            ClearSyntheticPlayer(id);
    }

    private bool IsActionBlockedWhileRidingBoat(PlayerAction action)
    {
        return action != PlayerAction.MoveUp &&
               action != PlayerAction.MoveDown &&
               action != PlayerAction.MoveLeft &&
               action != PlayerAction.MoveRight &&
               action != PlayerAction.Start;
    }

    private bool IsActionBlockedWhileUsingSpringLauncher(PlayerAction action)
    {
        return action != PlayerAction.MoveUp &&
               action != PlayerAction.MoveDown &&
               action != PlayerAction.MoveLeft &&
               action != PlayerAction.MoveRight &&
               action != PlayerAction.Start;
    }

    private bool ShouldBlockActionBecauseRidingBoat(int playerId, PlayerAction action)
    {
        if (!blockNonDirectionalInputsWhileRidingBoat)
            return false;

        if (!IsActionBlockedWhileRidingBoat(action))
            return false;

        RefreshPlayerStateCache(forcePlayersMapRefresh: false);

        if (!playersRidingBoat[playerId] &&
            (!playerControllers.TryGetValue(playerId, out var mc) || mc == null) &&
            lastForcedPlayersMapRefreshStamp != GetFrameStamp())
        {
            RefreshPlayerStateCache(forcePlayersMapRefresh: true);
        }

        return playersRidingBoat[playerId];
    }

    private bool ShouldBlockActionBecauseUsingSpringLauncher(int playerId, PlayerAction action)
    {
        if (!blockNonDirectionalInputsWhileUsingSpringLauncher)
            return false;

        if (!IsActionBlockedWhileUsingSpringLauncher(action))
            return false;

        RefreshPlayerStateCache(forcePlayersMapRefresh: false);
        return playersUsingSpringLauncherState[playerId];
    }

    public PlayerInputProfile GetPlayer(int playerId)
    {
        playerId = Mathf.Clamp(playerId, MinSupportedPlayerId, MaxSupportedPlayerId);

        if (!players.TryGetValue(playerId, out var p) || p == null)
        {
            p = new PlayerInputProfile(playerId);
            players[playerId] = p;

            prevUp[playerId] = prevDown[playerId] = prevLeft[playerId] = prevRight[playerId] = false;
            curUp[playerId] = curDown[playerId] = curLeft[playerId] = curRight[playerId] = false;
        }

        return p;
    }

    public bool Get(int playerId, PlayerAction action) => Get(action, playerId);
    public bool GetDown(int playerId, PlayerAction action) => GetDown(action, playerId);

    public bool Get(PlayerAction action, int playerId = 1)
    {
        playerId = Mathf.Clamp(playerId, MinSupportedPlayerId, MaxSupportedPlayerId);

        if (ShouldBlockActionBecauseRidingBoat(playerId, action))
            return false;

        if (ShouldBlockActionBecauseUsingSpringLauncher(playerId, action))
            return false;

        if (TryGetMobileDirectionalHeld(action, playerId, out bool mobileDirectionalHeld) &&
            mobileDirectionalHeld)
        {
            return true;
        }

        return IsSyntheticHeld(playerId, action) || ReadHeldRawCached(playerId, action);
    }

    public bool GetDown(PlayerAction action, int playerId = 1)
    {
        playerId = Mathf.Clamp(playerId, MinSupportedPlayerId, MaxSupportedPlayerId);

        if (ShouldBlockActionBecauseRidingBoat(playerId, action))
            return false;

        if (ShouldBlockActionBecauseUsingSpringLauncher(playerId, action))
            return false;

        return IsSyntheticDown(playerId, action) || ReadDownRawCached(playerId, action);
    }

    bool TryGetMobileDirectionalHeld(PlayerAction action, int playerId, out bool held)
    {
        held = false;

        if (playerId != 1 || !IsDirectionalAction(action))
            return false;

        var bridge = MobileInputBridge.Instance;
        if (bridge == null)
            return false;

        Vector2 mobile = bridge.MoveVector;
        held = action switch
        {
            PlayerAction.MoveUp => mobile.y >= analogThreshold,
            PlayerAction.MoveDown => mobile.y <= -analogThreshold,
            PlayerAction.MoveLeft => mobile.x <= -analogThreshold,
            PlayerAction.MoveRight => mobile.x >= analogThreshold,
            _ => false
        };

        return true;
    }

    bool TryGetMobileDirectionalDown(PlayerAction action, int playerId, out bool down)
    {
        down = false;

        if (playerId != 1 || !IsDirectionalAction(action) || MobileInputBridge.Instance == null)
            return false;

        bool was = action switch
        {
            PlayerAction.MoveUp => prevUp.TryGetValue(playerId, out bool up) && up,
            PlayerAction.MoveDown => prevDown.TryGetValue(playerId, out bool heldDown) && heldDown,
            PlayerAction.MoveLeft => prevLeft.TryGetValue(playerId, out bool left) && left,
            PlayerAction.MoveRight => prevRight.TryGetValue(playerId, out bool right) && right,
            _ => false
        };

        bool now = action switch
        {
            PlayerAction.MoveUp => curUp.TryGetValue(playerId, out bool up) && up,
            PlayerAction.MoveDown => curDown.TryGetValue(playerId, out bool heldDown) && heldDown,
            PlayerAction.MoveLeft => curLeft.TryGetValue(playerId, out bool left) && left,
            PlayerAction.MoveRight => curRight.TryGetValue(playerId, out bool right) && right,
            _ => false
        };

        down = now && !was;

        return true;
    }

    static bool IsDirectionalAction(PlayerAction action)
    {
        return action == PlayerAction.MoveUp ||
               action == PlayerAction.MoveDown ||
               action == PlayerAction.MoveLeft ||
               action == PlayerAction.MoveRight;
    }

    public bool AnyGet(PlayerAction action) => AnyGet(action, out _);

    public bool AnyGet(PlayerAction action, out int playerId)
    {
        for (int id = MinSupportedPlayerId; id <= MaxSupportedPlayerId; id++)
        {
            if (!IsConfiguredActivePlayer(id))
                continue;

            if (Get(action, id))
            {
                playerId = id;
                return true;
            }
        }

        playerId = 0;
        return false;
    }

    public bool AnyGetDown(PlayerAction action) => AnyGetDown(action, out _);

    public bool AnyGetDown(PlayerAction action, out int playerId)
    {
        for (int id = MinSupportedPlayerId; id <= MaxSupportedPlayerId; id++)
        {
            if (!IsConfiguredActivePlayer(id))
                continue;

            if (GetDown(action, id))
            {
                playerId = id;
                return true;
            }
        }

        playerId = 0;
        return false;
    }

    public bool HasAnyHeldInput(int playerId)
    {
        playerId = Mathf.Clamp(playerId, MinSupportedPlayerId, MaxSupportedPlayerId);

        int frameStamp = GetFrameStamp();
        if (anyHeldInputCacheStamp[playerId] == frameStamp)
            return anyHeldInputCache[playerId];

        bool hasInput =
            (curUp.TryGetValue(playerId, out bool up) && up) ||
            (curDown.TryGetValue(playerId, out bool down) && down) ||
            (curLeft.TryGetValue(playerId, out bool left) && left) ||
            (curRight.TryGetValue(playerId, out bool right) && right) ||
            ReadHeldRawCached(playerId, PlayerAction.MoveUp) ||
            ReadHeldRawCached(playerId, PlayerAction.MoveDown) ||
            ReadHeldRawCached(playerId, PlayerAction.MoveLeft) ||
            ReadHeldRawCached(playerId, PlayerAction.MoveRight) ||
            ReadHeldRawCached(playerId, PlayerAction.Start) ||
            ReadHeldRawCached(playerId, PlayerAction.ActionA) ||
            ReadHeldRawCached(playerId, PlayerAction.ActionB) ||
            ReadHeldRawCached(playerId, PlayerAction.ActionC) ||
            ReadHeldRawCached(playerId, PlayerAction.ActionL) ||
            ReadHeldRawCached(playerId, PlayerAction.ActionR) ||
            HasAnySyntheticHeld(playerId);

        anyHeldInputCache[playerId] = hasInput;
        anyHeldInputCacheStamp[playerId] = frameStamp;
        return hasInput;
    }

    private bool IsSyntheticHeld(int playerId, PlayerAction action)
    {
        if (!TryGetActionIndex(action, out int actionIndex))
            return false;

        return syntheticHeld[playerId, actionIndex] ||
               syntheticTapFrame[playerId, actionIndex] == Time.frameCount;
    }

    private bool IsSyntheticDown(int playerId, PlayerAction action)
    {
        if (!TryGetActionIndex(action, out int actionIndex))
            return false;

        return syntheticTapFrame[playerId, actionIndex] == Time.frameCount ||
               (syntheticHeld[playerId, actionIndex] && !syntheticPreviousHeld[playerId, actionIndex]);
    }

    private bool HasAnySyntheticHeld(int playerId)
    {
        for (int actionIndex = 0; actionIndex < PlayerActionCount; actionIndex++)
        {
            if (syntheticHeld[playerId, actionIndex] ||
                syntheticTapFrame[playerId, actionIndex] == Time.frameCount)
            {
                return true;
            }
        }

        return false;
    }

    private bool ReadHeldRawCached(int playerId, PlayerAction action)
    {
        if (!TryGetActionIndex(action, out int actionIndex))
            return ReadHeldRawUncached(playerId, action);

        int frameStamp = GetFrameStamp();
        if (rawHeldCacheStamp[playerId, actionIndex] == frameStamp)
            return rawHeldCache[playerId, actionIndex];

        bool value = ReadHeldRawUncached(playerId, action);
        rawHeldCache[playerId, actionIndex] = value;
        rawHeldCacheStamp[playerId, actionIndex] = frameStamp;
        return value;
    }

    private bool ReadHeldRawUncached(int playerId, PlayerAction action)
    {
        if (playerId == 1 && MobileInputBridge.Instance != null && MobileInputBridge.Instance.Get(action))
            return true;

        var p = GetPlayer(playerId);
        var b = p.GetBinding(action);

        if (b.kind == BindKind.Key)
            return ReadKeyHeld(b.key);

        if (b.kind == BindKind.DPad)
        {
            return b.dpadDir switch
            {
                0 => curUp.TryGetValue(playerId, out bool up) && up,
                1 => curDown.TryGetValue(playerId, out bool down) && down,
                2 => curLeft.TryGetValue(playerId, out bool left) && left,
                3 => curRight.TryGetValue(playerId, out bool right) && right,
                _ => false
            };
        }

        if (b.kind == BindKind.JoyButton)
            return ReadGamepadButtonHeld(p, b.joyButton);

        return false;
    }

    private bool ReadDownRawCached(int playerId, PlayerAction action)
    {
        if (!TryGetActionIndex(action, out int actionIndex))
            return ReadDownRawUncached(playerId, action);

        int frameStamp = GetFrameStamp();
        if (rawDownCacheStamp[playerId, actionIndex] == frameStamp)
            return rawDownCache[playerId, actionIndex];

        bool value = ReadDownRawUncached(playerId, action);
        rawDownCache[playerId, actionIndex] = value;
        rawDownCacheStamp[playerId, actionIndex] = frameStamp;
        return value;
    }

    private bool ReadDownRawUncached(int playerId, PlayerAction action)
    {
        if (playerId == 1 && MobileInputBridge.Instance != null && MobileInputBridge.Instance.GetDown(action))
            return true;

        if (TryGetMobileDirectionalDown(action, playerId, out bool mobileDirectionalDown) && mobileDirectionalDown)
            return true;

        var p = GetPlayer(playerId);
        var b = p.GetBinding(action);

        if (b.kind == BindKind.Key)
            return ReadKeyDown(b.key);

        if (b.kind == BindKind.DPad)
        {
            bool now = b.dpadDir switch
            {
                0 => curUp[playerId],
                1 => curDown[playerId],
                2 => curLeft[playerId],
                3 => curRight[playerId],
                _ => false
            };

            bool was = b.dpadDir switch
            {
                0 => prevUp[playerId],
                1 => prevDown[playerId],
                2 => prevLeft[playerId],
                3 => prevRight[playerId],
                _ => false
            };

            return now && !was;
        }

        if (b.kind == BindKind.JoyButton)
            return ReadGamepadButtonDown(p, b.joyButton);

        return false;
    }

    void ReadDirectionalDigital(PlayerInputProfile p, int playerId, out bool up, out bool down, out bool left, out bool right)
    {
        up = down = left = right = false;

        ReadControllerDirectionalDigital(p, out bool padUp, out bool padDown, out bool padLeft, out bool padRight);

        up |= padUp;
        down |= padDown;
        left |= padLeft;
        right |= padRight;

        Vector2 mobile = Vector2.zero;
        bool hasBridge = MobileInputBridge.Instance != null;

        if (playerId == 1 && hasBridge)
        {
            mobile = MobileInputBridge.Instance.MoveVector;

            up |= mobile.y >= analogThreshold;
            down |= mobile.y <= -analogThreshold;
            right |= mobile.x >= analogThreshold;
            left |= mobile.x <= -analogThreshold;
        }

    }

    void ReadControllerDirectionalDigital(PlayerInputProfile p, out bool up, out bool down, out bool left, out bool right)
    {
        var device = ResolvePlayerInputDevice(p);

        if (device == null)
        {
            up = down = left = right = false;
            return;
        }

        UniversalControllerInput.ReadDirectionalDigital(
            device,
            analogThreshold,
            includeRightStickAsDpad,
            out up,
            out down,
            out left,
            out right);
    }

    static InputDevice ResolvePlayerInputDevice(PlayerInputProfile p)
    {
        return UniversalControllerInput.ResolveProfileDevice(p);
    }

    static bool ReadKeyHeld(KeyCode k)
    {
        var kb = Keyboard.current;
        if (kb == null)
            return false;

        return TryGetKeyControl(k, kb, out var key) && key.isPressed;
    }

    static bool ReadKeyDown(KeyCode k)
    {
        var kb = Keyboard.current;
        if (kb == null)
            return false;

        return TryGetKeyControl(k, kb, out var key) && key.wasPressedThisFrame;
    }

    static bool TryGetKeyControl(KeyCode desiredKeyCode, Keyboard kb, out KeyControl key)
    {
        key = null;
        if (kb == null)
            return false;

        EnsureKeyboardControlCache(kb);
        return keyboardControlsByKeyCode.TryGetValue(desiredKeyCode, out key) && key != null;
    }

    static void EnsureKeyboardControlCache(Keyboard kb)
    {
        if (keyboardControlCacheReady && cachedKeyboard == kb)
            return;

        keyboardControlsByKeyCode.Clear();
        cachedKeyboard = kb;
        keyboardControlCacheReady = true;

        foreach (var k in kb.allKeys)
        {
            if (k == null)
                continue;

            if (TryMapInputSystemKeyToUnityKeyCode(k.keyCode, out var kc))
                keyboardControlsByKeyCode[kc] = k;
        }
    }

    static bool TryMapInputSystemKeyToUnityKeyCode(Key key, out KeyCode kc)
    {
        kc = KeyCode.None;
        string name = key.ToString();

        switch (key)
        {
            case Key.Enter: kc = KeyCode.Return; return true;
            case Key.Escape: kc = KeyCode.Escape; return true;
            case Key.Backspace: kc = KeyCode.Backspace; return true;
            case Key.Space: kc = KeyCode.Space; return true;
            case Key.Tab: kc = KeyCode.Tab; return true;
        }

        if (name.StartsWith("Digit", StringComparison.OrdinalIgnoreCase) && name.Length == 6)
        {
            char d = name[5];
            if (d >= '0' && d <= '9')
            {
                kc = (KeyCode)Enum.Parse(typeof(KeyCode), "Alpha" + d);
                return true;
            }
        }

        if (name.StartsWith("Numpad", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Length == 7)
            {
                char d = name[6];
                if (d >= '0' && d <= '9')
                {
                    kc = (KeyCode)Enum.Parse(typeof(KeyCode), "Keypad" + d);
                    return true;
                }
            }

            if (name.Equals("NumpadEnter", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.KeypadEnter; return true; }
            if (name.Equals("NumpadPlus", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.KeypadPlus; return true; }
            if (name.Equals("NumpadMinus", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.KeypadMinus; return true; }
            if (name.Equals("NumpadMultiply", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.KeypadMultiply; return true; }
            if (name.Equals("NumpadDivide", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.KeypadDivide; return true; }
            if (name.Equals("NumpadPeriod", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.KeypadPeriod; return true; }
        }

        if (name.Equals("LeftCtrl", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.LeftControl; return true; }
        if (name.Equals("RightCtrl", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.RightControl; return true; }
        if (name.Equals("LeftAlt", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.LeftAlt; return true; }
        if (name.Equals("RightAlt", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.RightAlt; return true; }
        if (name.Equals("Backquote", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.BackQuote; return true; }

        if (name.Equals("Minus", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Minus; return true; }
        if (name.Equals("Equals", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Equals; return true; }
        if (name.Equals("LeftBracket", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.LeftBracket; return true; }
        if (name.Equals("RightBracket", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.RightBracket; return true; }
        if (name.Equals("Semicolon", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Semicolon; return true; }
        if (name.Equals("Quote", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Quote; return true; }
        if (name.Equals("Backslash", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Backslash; return true; }
        if (name.Equals("Slash", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Slash; return true; }
        if (name.Equals("Comma", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Comma; return true; }
        if (name.Equals("Period", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Period; return true; }

        if (Enum.TryParse(name, true, out KeyCode parsed))
        {
            kc = parsed;
            return kc != KeyCode.None;
        }

        return false;
    }

    static bool ReadGamepadButtonHeld(PlayerInputProfile p, int btn)
    {
        var device = ResolvePlayerInputDevice(p);
        if (device == null)
            return false;

        return UniversalControllerInput.ReadButtonHeld(device, btn);
    }

    static bool ReadGamepadButtonDown(PlayerInputProfile p, int btn)
    {
        var device = ResolvePlayerInputDevice(p);
        if (device == null)
            return false;

        return UniversalControllerInput.ReadButtonDown(device, btn);
    }
}
