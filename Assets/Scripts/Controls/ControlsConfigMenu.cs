using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ControlsConfigMenu : MonoBehaviour
{
    [Header("Menu Owner (who navigates UI)")]
    [SerializeField, Range(1, 4)] int ownerPlayerId = 1;

    [Header("UI")]
    [SerializeField] GameObject root;
    [SerializeField] Image backgroundImage;

    [Header("Layout Root (moves menu as a whole)")]
    [SerializeField] RectTransform menuLayoutRoot;
    [SerializeField] float menuGlobalYOffset = 140;

    [Header("Fade")]
    [SerializeField] Image fadeImage;
    [SerializeField] float fadeInDuration = 0.25f;
    [SerializeField] float fadeOutDuration = 0.2f;

    [Header("Music")]
    [SerializeField] AudioClip controlsMusic;
    [SerializeField, Range(0f, 1f)] float controlsMusicVolume = 1f;

    [Header("Text (TMP)")]
    [SerializeField] TMP_Text menuText;

    [Header("Title")]
    [SerializeField] int titleFontSize = 46;
    [SerializeField] float titleVOffset = 26f;
    [SerializeField] int headerTopPaddingLines = 0;
    [SerializeField] int headerBottomPaddingLines = 0;

    [Header("Body")]
    [SerializeField] int bodyFontSize = 30;

    [Header("Footer")]
    [SerializeField] int footerGapLines = 0;
    [SerializeField] int footerFontSize = 22;
    [SerializeField, Range(0, 10)] int footerExtraNewLines = 0;

    [Header("Select Player Blocks")]
    [SerializeField] int selectGridFontSize = 24;
    [SerializeField, Range(0, 6)] int playerBlockGapLines = 1;

    [Header("Text Style (SB5-like)")]
    [SerializeField] bool forceBold = true;

    [Header("Outline (TMP SDF)")]
    [SerializeField] Color outlineColor = Color.black;
    [SerializeField, Range(0f, 1f)] float outlineWidth = 0.5f;
    [SerializeField, Range(0f, 1f)] float outlineSoftness = 0.0f;

    [Header("Cursor (AnimatedSpriteRenderer)")]
    [SerializeField] AnimatedSpriteRenderer cursorRenderer;
    [SerializeField] Vector2 cursorOffset = new(-100f, 0f);
    [SerializeField, Range(0f, 200f)] float cursorGapLeft = 18f;
    [SerializeField, Range(-40f, 40f)] float cursorLineCenterAdjustY = 6f;

    [Header("SFX")]
    [SerializeField] AudioClip confirmSfx;
    [SerializeField, Range(0f, 1f)] float confirmVolume = 1f;

    [SerializeField] AudioClip moveOptionSfx;
    [SerializeField, Range(0f, 1f)] float moveOptionVolume = 1f;

    [SerializeField] AudioClip backSfx;
    [SerializeField, Range(0f, 1f)] float backVolume = 1f;

    [SerializeField] AudioClip resetSfx;
    [SerializeField, Range(0f, 1f)] float resetVolume = 1f;

    [Header("Players Block - Global Indent")]
    [SerializeField] float playersBlockIndentX = 400f;

    const string colorNormal = "#FFFFE7";
    const string colorHint = "#FFA621";
    const string colorWhite = "#FFFFFF";

    const string colorBlueSoft = "#8FD3FF";
    const string colorPlayerGreen = "#8CFFB3";
    const string colorPlayerSelectedRed = "#FF5A5A";

    const float COLUMN_LEFT_LABEL_BASE = -370f;
    const float COLUMN_LEFT_VALUE_BASE = -210f;
    const float COLUMN_RIGHT_LABEL_BASE = 100f;
    const float COLUMN_RIGHT_VALUE_BASE = 260f;

    const string LINK_WAIT_PREFIX = "wait_";
    const string LINK_RESET_YES = "reset_yes";
    const string LINK_RESET_NO = "reset_no";

    enum MenuState
    {
        SelectPlayer,
        ConfirmReset,
        BulkRemap
    }

    MenuState state;

    int playerSelectIndex;
    int targetPlayerId;
    int bulkStep;

    int confirmResetIndex;
    int confirmResetPlayerId;

    Material runtimeMenuMat;
    Coroutine cursorPulseRoutine;
    Dictionary<PlayerAction, Binding> bulkSnapshot;

    Vector2 menuLayoutRootBasePos;
    bool menuLayoutRootCached;

    struct DpadHit { public int dir; public int joyIndex; }
    struct JoyBtnHit { public int btn; public int joyIndex; }

    static readonly PlayerAction[] BulkActions = new[]
    {
        PlayerAction.MoveUp,
        PlayerAction.MoveDown,
        PlayerAction.MoveLeft,
        PlayerAction.MoveRight,
        PlayerAction.Start,
        PlayerAction.ActionA,
        PlayerAction.ActionB,
        PlayerAction.ActionC
    };

    void Awake()
    {
        if (root == null)
            root = gameObject;

        SetupMenuTextMaterial();
        CacheMenuLayoutRootBasePos();

        if (root != null)
            root.SetActive(false);

        if (cursorRenderer != null)
            cursorRenderer.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (runtimeMenuMat != null)
            Destroy(runtimeMenuMat);
    }

    void CacheMenuLayoutRootBasePos()
    {
        if (menuLayoutRootCached)
            return;

        if (menuLayoutRoot != null)
        {
            menuLayoutRootBasePos = menuLayoutRoot.anchoredPosition;
            menuLayoutRootCached = true;
        }
    }

    void SetupMenuTextMaterial()
    {
        if (menuText == null)
            return;

        menuText.textWrappingMode = TextWrappingModes.NoWrap;
        menuText.overflowMode = TextOverflowModes.Overflow;
        menuText.extraPadding = false;

        if (forceBold)
            menuText.fontStyle |= FontStyles.Bold;

        Material baseMat = menuText.fontSharedMaterial;
        if (baseMat == null) baseMat = menuText.fontMaterial;
        if (baseMat == null && menuText.font != null) baseMat = menuText.font.material;
        if (baseMat == null) return;

        if (runtimeMenuMat != null)
            Destroy(runtimeMenuMat);

        runtimeMenuMat = new Material(baseMat);

        TrySetColor(runtimeMenuMat, "_OutlineColor", outlineColor);
        TrySetFloat(runtimeMenuMat, "_OutlineWidth", outlineWidth);
        TrySetFloat(runtimeMenuMat, "_OutlineSoftness", outlineSoftness);

        TrySetFloat(runtimeMenuMat, "_FaceDilate", 0f);
        TrySetFloat(runtimeMenuMat, "_FaceSoftness", 0f);

        TrySetFloat(runtimeMenuMat, "_UnderlayDilate", 0f);
        TrySetFloat(runtimeMenuMat, "_UnderlaySoftness", 0f);
        TrySetFloat(runtimeMenuMat, "_UnderlayOffsetX", 0f);
        TrySetFloat(runtimeMenuMat, "_UnderlayOffsetY", 0f);
        TrySetColor(runtimeMenuMat, "_UnderlayColor", new Color(0f, 0f, 0f, 0f));
        runtimeMenuMat.DisableKeyword("UNDERLAY_ON");
        runtimeMenuMat.DisableKeyword("UNDERLAY_INNER");

        menuText.fontMaterial = runtimeMenuMat;
        menuText.havePropertiesChanged = true;
        menuText.UpdateMeshPadding();
        menuText.SetAllDirty();
    }

    static void TrySetFloat(Material m, string prop, float value)
    {
        if (m != null && m.HasProperty(prop))
            m.SetFloat(prop, value);
    }

    static void TrySetColor(Material m, string prop, Color value)
    {
        if (m != null && m.HasProperty(prop))
            m.SetColor(prop, value);
    }

    bool TryGetAnyPlayerDown(PlayerAction action, out int pid)
    {
        pid = 1;

        var input = PlayerInputManager.Instance;
        if (input == null) return false;

        for (int p = 1; p <= 4; p++)
        {
            if (input.GetDown(p, action))
            {
                pid = p;
                return true;
            }
        }

        return false;
    }

    bool TryGetAnyPlayerDownEither(PlayerAction a, PlayerAction b, out int pid)
    {
        if (TryGetAnyPlayerDown(a, out pid)) return true;
        if (TryGetAnyPlayerDown(b, out pid)) return true;
        pid = 1;
        return false;
    }

    bool AnyPlayerHeld(PlayerAction action)
    {
        var input = PlayerInputManager.Instance;
        if (input == null) return false;

        for (int p = 1; p <= 4; p++)
        {
            if (input.Get(p, action))
                return true;
        }

        return false;
    }

    bool AnyPlayerHeldAnyMenuKey()
    {
        return AnyPlayerHeld(PlayerAction.ActionA) ||
               AnyPlayerHeld(PlayerAction.ActionB) ||
               AnyPlayerHeld(PlayerAction.ActionC) ||
               AnyPlayerHeld(PlayerAction.Start) ||
               AnyPlayerHeld(PlayerAction.MoveUp) ||
               AnyPlayerHeld(PlayerAction.MoveDown) ||
               AnyPlayerHeld(PlayerAction.MoveLeft) ||
               AnyPlayerHeld(PlayerAction.MoveRight);
    }

    PlayerAction CurrentBulkAction()
    {
        return BulkActions[Mathf.Clamp(bulkStep, 0, BulkActions.Length - 1)];
    }

    string CurrentWaitLinkId()
    {
        return LINK_WAIT_PREFIX + ActionToLabel(CurrentBulkAction());
    }

    public IEnumerator OpenRoutine(int openerPlayerId, AudioClip restoreMusic, float restoreMusicVolume = 1f)
    {
        ownerPlayerId = Mathf.Clamp(openerPlayerId, 1, 4);

        if (root == null) root = gameObject;

        root.transform.SetAsLastSibling();
        root.SetActive(true);

        SetupMenuTextMaterial();

        CacheMenuLayoutRootBasePos();
        if (menuLayoutRoot != null)
            menuLayoutRoot.anchoredPosition = menuLayoutRootBasePos + new Vector2(0f, menuGlobalYOffset);

        if (controlsMusic != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlayMusic(controlsMusic, controlsMusicVolume, true);

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.transform.SetAsLastSibling();
            SetFadeAlpha(1f);
            yield return FadeTo(0f, fadeInDuration);
            fadeImage.gameObject.SetActive(false);
        }

        state = MenuState.SelectPlayer;
        playerSelectIndex = Mathf.Clamp(ownerPlayerId - 1, 0, 3);
        targetPlayerId = playerSelectIndex + 1;
        bulkStep = 0;
        bulkSnapshot = null;

        confirmResetIndex = 1;
        confirmResetPlayerId = 1;

        if (cursorRenderer != null)
        {
            cursorRenderer.gameObject.SetActive(true);
            cursorRenderer.RefreshFrame();
        }

        while (AnyPlayerHeldAnyMenuKey())
            yield return null;

        yield return null;

        RefreshText();

        bool done = false;
        while (!done)
        {
            if (state == MenuState.SelectPlayer)
            {
                int prev = playerSelectIndex;

                if (TryGetAnyPlayerDown(PlayerAction.MoveUp, out int pidUp))
                {
                    ownerPlayerId = pidUp;
                    playerSelectIndex = Mathf.Clamp(playerSelectIndex - 1, 0, 3);
                }
                else if (TryGetAnyPlayerDown(PlayerAction.MoveDown, out int pidDown))
                {
                    ownerPlayerId = pidDown;
                    playerSelectIndex = Mathf.Clamp(playerSelectIndex + 1, 0, 3);
                }

                if (playerSelectIndex != prev)
                {
                    targetPlayerId = playerSelectIndex + 1;
                    PlaySfx(moveOptionSfx, moveOptionVolume);
                    RefreshText();
                }

                if (TryGetAnyPlayerDown(PlayerAction.ActionB, out int pidBack))
                {
                    ownerPlayerId = pidBack;
                    PlaySfx(backSfx, backVolume);
                    yield return PulseCursor();
                    done = true;
                    yield return null;
                    continue;
                }

                if (TryGetAnyPlayerDown(PlayerAction.ActionC, out int pidAskReset))
                {
                    ownerPlayerId = pidAskReset;

                    confirmResetPlayerId = playerSelectIndex + 1;
                    confirmResetIndex = 1;
                    state = MenuState.ConfirmReset;

                    PlaySfx(confirmSfx, confirmVolume);
                    RefreshText();
                    yield return PulseCursor();
                    yield return null;
                    continue;
                }

                if (TryGetAnyPlayerDownEither(PlayerAction.Start, PlayerAction.ActionA, out int pidConfirm))
                {
                    ownerPlayerId = pidConfirm;

                    targetPlayerId = playerSelectIndex + 1;
                    var p = PlayerInputManager.Instance.GetPlayer(targetPlayerId);

                    bulkSnapshot = p.CloneBindings();
                    bulkStep = 0;
                    state = MenuState.BulkRemap;

                    PlaySfx(confirmSfx, confirmVolume);
                    RefreshText();
                    yield return PulseCursor();
                    yield return null;
                    continue;
                }

                yield return null;
                continue;
            }

            if (state == MenuState.ConfirmReset)
            {
                int prev = confirmResetIndex;

                if (TryGetAnyPlayerDown(PlayerAction.MoveUp, out int pidUp))
                {
                    ownerPlayerId = pidUp;
                    confirmResetIndex = Mathf.Clamp(confirmResetIndex - 1, 0, 1);
                }
                else if (TryGetAnyPlayerDown(PlayerAction.MoveDown, out int pidDown))
                {
                    ownerPlayerId = pidDown;
                    confirmResetIndex = Mathf.Clamp(confirmResetIndex + 1, 0, 1);
                }
                else if (TryGetAnyPlayerDown(PlayerAction.MoveLeft, out int pidLeft))
                {
                    ownerPlayerId = pidLeft;
                    confirmResetIndex = 0;
                }
                else if (TryGetAnyPlayerDown(PlayerAction.MoveRight, out int pidRight))
                {
                    ownerPlayerId = pidRight;
                    confirmResetIndex = 1;
                }

                if (confirmResetIndex != prev)
                {
                    PlaySfx(moveOptionSfx, moveOptionVolume);
                    RefreshText();
                }

                if (TryGetAnyPlayerDown(PlayerAction.ActionB, out int pidCancel))
                {
                    ownerPlayerId = pidCancel;
                    state = MenuState.SelectPlayer;
                    PlaySfx(backSfx, backVolume);
                    RefreshText();
                    yield return PulseCursor();
                    yield return null;
                    continue;
                }

                if (TryGetAnyPlayerDownEither(PlayerAction.Start, PlayerAction.ActionA, out int pidYesNo))
                {
                    ownerPlayerId = pidYesNo;

                    if (confirmResetIndex == 0)
                    {
                        var pReset = PlayerInputManager.Instance.GetPlayer(confirmResetPlayerId);
                        pReset.ResetToDefault();
                        pReset.SaveToPrefs();

                        PlaySfx(resetSfx, resetVolume);
                        state = MenuState.SelectPlayer;
                        RefreshText();
                        yield return PulseCursor();
                        yield return null;
                        continue;
                    }

                    state = MenuState.SelectPlayer;
                    PlaySfx(backSfx, backVolume);
                    RefreshText();
                    yield return PulseCursor();
                    yield return null;
                    continue;
                }

                yield return null;
                continue;
            }

            if (state == MenuState.BulkRemap)
            {
                bool escCancel = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
                var p = PlayerInputManager.Instance.GetPlayer(targetPlayerId);

                if (escCancel)
                {
                    if (bulkSnapshot != null)
                        p.ApplyBindings(bulkSnapshot);

                    bulkSnapshot = null;
                    bulkStep = 0;
                    state = MenuState.SelectPlayer;

                    PlaySfx(backSfx, backVolume);
                    RefreshText();
                    yield return null;
                    continue;
                }

                var dpad = ReadAnyDpadDownThisFrame();
                var joyBtn = ReadAnyGamepadButtonDownThisFrame();
                KeyCode? key = ReadAnyKeyboardKeyDownNoMouse();

                if (key.HasValue)
                {
                    var bBind = p.GetBinding(PlayerAction.ActionB);
                    var cBind = p.GetBinding(PlayerAction.ActionC);

                    if (bBind.kind == BindKind.Key && bBind.key == key.Value) key = null;
                    if (cBind.kind == BindKind.Key && cBind.key == key.Value) key = null;
                }

                if (dpad.HasValue || joyBtn.HasValue || key.HasValue)
                {
                    var action = CurrentBulkAction();

                    if (dpad.HasValue)
                    {
                        p.joyIndex = dpad.Value.joyIndex;
                        p.SetBinding(action, Binding.FromDpad(p.joyIndex, dpad.Value.dir));
                    }
                    else if (joyBtn.HasValue)
                    {
                        p.joyIndex = joyBtn.Value.joyIndex;
                        p.SetBinding(action, Binding.FromJoyButton(p.joyIndex, joyBtn.Value.btn));
                    }
                    else
                    {
                        p.SetBinding(action, Binding.FromKey(key.Value));
                    }

                    PlaySfx(confirmSfx, confirmVolume);
                    yield return PulseCursor();

                    bulkStep++;

                    if (bulkStep >= BulkActions.Length)
                    {
                        p.SaveToPrefs();
                        bulkSnapshot = null;
                        bulkStep = 0;
                        state = MenuState.SelectPlayer;
                        RefreshText();
                        yield return null;
                        continue;
                    }

                    RefreshText();
                }

                yield return null;
                continue;
            }

            yield return null;
        }

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.transform.SetAsLastSibling();
            SetFadeAlpha(0f);
            yield return FadeTo(1f, fadeOutDuration);
        }

        if (cursorRenderer != null)
            cursorRenderer.gameObject.SetActive(false);

        if (menuLayoutRoot != null && menuLayoutRootCached)
            menuLayoutRoot.anchoredPosition = menuLayoutRootBasePos;

        if (root != null)
            root.SetActive(false);

        if (fadeImage != null)
            fadeImage.gameObject.SetActive(false);

        if (restoreMusic != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlayMusic(restoreMusic, Mathf.Clamp01(restoreMusicVolume), true);
    }

    static DpadHit? ReadAnyDpadDownThisFrame()
    {
        var all = Gamepad.all;
        for (int i = 0; i < all.Count; i++)
        {
            var pad = all[i];
            if (pad == null) continue;

            int joyIndex = i + 1;

            if (pad.dpad.up.wasPressedThisFrame) return new DpadHit { dir = 0, joyIndex = joyIndex };
            if (pad.dpad.down.wasPressedThisFrame) return new DpadHit { dir = 1, joyIndex = joyIndex };
            if (pad.dpad.left.wasPressedThisFrame) return new DpadHit { dir = 2, joyIndex = joyIndex };
            if (pad.dpad.right.wasPressedThisFrame) return new DpadHit { dir = 3, joyIndex = joyIndex };
        }

        return null;
    }

    static JoyBtnHit? ReadAnyGamepadButtonDownThisFrame()
    {
        var all = Gamepad.all;
        for (int i = 0; i < all.Count; i++)
        {
            var pad = all[i];
            if (pad == null) continue;

            int joyIndex = i + 1;

            if (pad.buttonSouth.wasPressedThisFrame) return new JoyBtnHit { btn = 0, joyIndex = joyIndex };
            if (pad.buttonEast.wasPressedThisFrame) return new JoyBtnHit { btn = 1, joyIndex = joyIndex };
            if (pad.buttonWest.wasPressedThisFrame) return new JoyBtnHit { btn = 2, joyIndex = joyIndex };
            if (pad.buttonNorth.wasPressedThisFrame) return new JoyBtnHit { btn = 3, joyIndex = joyIndex };

            if (pad.leftShoulder.wasPressedThisFrame) return new JoyBtnHit { btn = 4, joyIndex = joyIndex };
            if (pad.rightShoulder.wasPressedThisFrame) return new JoyBtnHit { btn = 5, joyIndex = joyIndex };

            if (pad.leftTrigger.wasPressedThisFrame) return new JoyBtnHit { btn = 6, joyIndex = joyIndex };
            if (pad.rightTrigger.wasPressedThisFrame) return new JoyBtnHit { btn = 7, joyIndex = joyIndex };

            if (pad.startButton.wasPressedThisFrame) return new JoyBtnHit { btn = 8, joyIndex = joyIndex };
            if (pad.selectButton.wasPressedThisFrame) return new JoyBtnHit { btn = 9, joyIndex = joyIndex };
        }

        return null;
    }

    static KeyCode? ReadAnyKeyboardKeyDownNoMouse()
    {
        var kb = Keyboard.current;
        if (kb == null) return null;

        if (!kb.anyKey.wasPressedThisFrame)
            return null;

        foreach (var k in kb.allKeys)
        {
            if (k == null) continue;
            if (!k.wasPressedThisFrame) continue;

            if (TryMapInputSystemKeyToUnityKeyCode(k.keyCode, out var kc))
                return kc;
        }

        return null;
    }

    static bool TryMapInputSystemKeyToUnityKeyCode(Key key, out KeyCode kc)
    {
        kc = KeyCode.None;
        string name = key.ToString();

        switch (key)
        {
            case Key.Enter: kc = KeyCode.Return; return true;
            case Key.Escape: kc = KeyCode.Escape; return true;
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

        if (Enum.TryParse(name, true, out KeyCode parsed))
        {
            kc = parsed;
            return kc != KeyCode.None;
        }

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

        return false;
    }

    IEnumerator PulseCursor()
    {
        if (cursorRenderer == null)
            yield break;

        if (cursorPulseRoutine != null)
            StopCoroutine(cursorPulseRoutine);

        cursorPulseRoutine = StartCoroutine(cursorRenderer.PlayCycles(1));
        yield return cursorPulseRoutine;
        cursorPulseRoutine = null;
    }

    void RefreshText()
    {
        if (menuText == null)
            return;

        string header =
            RepeatNewLine(Mathf.Max(0, headerTopPaddingLines)) +
            $"<align=center><size={titleFontSize}><color={colorHint}><voffset={titleVOffset}>CONTROLS</voffset></color></size></align>" +
            RepeatNewLine(Mathf.Max(0, headerBottomPaddingLines)) +
            "\n";

        string body = "<align=center>";
        body += $"<size={bodyFontSize}><color={colorBlueSoft}>CHOOSE A PLAYER TO EDIT CONTROLS</color></size>\n\n";
        body += "</align>";

        body += "<align=left>";
        AppendPlayerBlock(ref body, 0, colorNormal, colorHint, colorWhite);
        body += RepeatNewLine(playerBlockGapLines);
        AppendPlayerBlock(ref body, 1, colorNormal, colorHint, colorWhite);
        body += RepeatNewLine(playerBlockGapLines);
        AppendPlayerBlock(ref body, 2, colorNormal, colorHint, colorWhite);
        body += RepeatNewLine(playerBlockGapLines);
        AppendPlayerBlock(ref body, 3, colorNormal, colorHint, colorWhite);

        int footerLift = Mathf.Max(0, footerGapLines + footerExtraNewLines);
        body += RepeatNewLine(footerLift);

        if (state == MenuState.SelectPlayer)
        {
            body +=
                $"<align=center><size={footerFontSize}>" +
                $"<color={colorHint}>A / START:</color> <color={colorWhite}>CONFIRM / PLACE BOMB</color>\n" +
                $"<color={colorHint}>B:</color> <color={colorWhite}>RETURN / EXPLODE CONTROL BOMB</color>\n" +
                $"<color={colorHint}>C:</color> <color={colorWhite}>RESTORE DEFAULT KEYS / ABILITIES</color>" +
                $"</size></align>";
        }
        else if (state == MenuState.ConfirmReset)
        {
            string yesText = confirmResetIndex == 0 ? $"<color={colorHint}>YES</color>" : $"<color={colorWhite}>YES</color>";
            string noText = confirmResetIndex == 1 ? $"<color={colorHint}>NO</color>" : $"<color={colorWhite}>NO</color>";

            body +=
                $"<align=center><size={footerFontSize}>" +
                $"<color={colorHint}>RESTORE DEFAULT KEYS?</color>\n" +
                $"<color={colorWhite}>PLAYER {confirmResetPlayerId}</color>\n\n" +
                $"<link=\"{LINK_RESET_YES}\">{yesText}</link>    <link=\"{LINK_RESET_NO}\">{noText}</link>\n\n" +
                $"<color={colorHint}>A / START</color><color={colorWhite}>: CONFIRM</color>    <color={colorHint}>B</color><color={colorWhite}>: CANCEL</color>" +
                $"</size></align>";
        }
        else
        {
            var a = CurrentBulkAction();
            body +=
                $"<align=center><size={footerFontSize}>" +
                $"<color={colorHint}>REMAPPING PLAYER {targetPlayerId}:</color> <color={colorWhite}>{ActionToLabel(a)}</color>\n" +
                $"<color={colorHint}>PRESS A KEY...</color>\n" +
                $"<color={colorHint}>ESC</color><color={colorWhite}>: CANCEL</color>" +
                $"</size></align>";
        }

        body += "</align>";

        menuText.text = header + body;

        if (state == MenuState.SelectPlayer)
        {
            UpdateCursorPosition_ByLinkId($"sel{playerSelectIndex}");
        }
        else if (state == MenuState.ConfirmReset)
        {
            UpdateCursorPosition_ByLinkId(confirmResetIndex == 0 ? LINK_RESET_YES : LINK_RESET_NO);
        }
        else
        {
            UpdateCursorPosition_ByLinkId(CurrentWaitLinkId());
        }
    }

    void AppendPlayerBlock(ref string body, int index, string cn, string ch, string cw)
    {
        for (int line = 0; line < 5; line++)
        {
            string l = PlayerLine(index + 1, line, cn, ch, cw, index);
            if (state == MenuState.SelectPlayer)
                body += $"<link=\"sel{index}\">{l}</link>\n";
            else
                body += $"{l}\n";
        }
    }

    string LabelMaybeWait(PlayerAction action, string label, string ch)
    {
        if (state == MenuState.BulkRemap && CurrentBulkAction() == action)
            return $"<link=\"{CurrentWaitLinkId()}\"><color={ch}>{label}</color></link>";

        return $"<color={ch}>{label}</color>";
    }

    string PlayerLine(int pid, int lineIndex, string cn, string ch, string cw, int selIndex)
    {
        var p = PlayerInputManager.Instance.GetPlayer(pid);

        bool selected = (playerSelectIndex == selIndex);
        string playerColor = selected ? colorPlayerSelectedRed : colorPlayerGreen;
        string tag = $"<color={playerColor}>PLAYER {pid}</color>";

        string u = BindingToShort(p.GetBinding(PlayerAction.MoveUp));
        string d = BindingToShort(p.GetBinding(PlayerAction.MoveDown));
        string l = BindingToShort(p.GetBinding(PlayerAction.MoveLeft));
        string r = BindingToShort(p.GetBinding(PlayerAction.MoveRight));

        string st = BindingToShort(p.GetBinding(PlayerAction.Start));
        string a = BindingToShort(p.GetBinding(PlayerAction.ActionA));
        string b = BindingToShort(p.GetBinding(PlayerAction.ActionB));
        string c = BindingToShort(p.GetBinding(PlayerAction.ActionC));

        float ll = COLUMN_LEFT_LABEL_BASE + playersBlockIndentX;
        float lv = COLUMN_LEFT_VALUE_BASE + playersBlockIndentX;
        float rl = COLUMN_RIGHT_LABEL_BASE + playersBlockIndentX;
        float rv = COLUMN_RIGHT_VALUE_BASE + playersBlockIndentX;

        bool isTarget = (state == MenuState.BulkRemap && pid == targetPlayerId);

        string Lbl(PlayerAction act, string s)
        {
            if (isTarget) return LabelMaybeWait(act, s, ch);
            return $"<color={ch}>{s}</color>";
        }

        string txt = lineIndex switch
        {
            0 => $"<align=center>{tag}</align>",

            1 => $"<pos={ll}>{Lbl(PlayerAction.MoveUp, "UP:")}</pos><pos={lv}>{u}</pos><pos={rl}>{Lbl(PlayerAction.Start, "START:")}</pos><pos={rv}>{st}</pos>",
            2 => $"<pos={ll}>{Lbl(PlayerAction.MoveDown, "DOWN:")}</pos><pos={lv}>{d}</pos><pos={rl}>{Lbl(PlayerAction.ActionA, "A:")}</pos><pos={rv}>{a}</pos>",
            3 => $"<pos={ll}>{Lbl(PlayerAction.MoveLeft, "LEFT:")}</pos><pos={lv}>{l}</pos><pos={rl}>{Lbl(PlayerAction.ActionB, "B:")}</pos><pos={rv}>{b}</pos>",
            4 => $"<pos={ll}>{Lbl(PlayerAction.MoveRight, "RIGHT:")}</pos><pos={lv}>{r}</pos><pos={rl}>{Lbl(PlayerAction.ActionC, "C:")}</pos><pos={rv}>{c}</pos>",

            _ => string.Empty
        };

        return $"<size={selectGridFontSize}><color={cn}>{txt}</color></size>";
    }

    static string BindingToShort(Binding b)
    {
        if (b.kind == BindKind.Key)
            return PrettyKeyName(b.key);

        if (b.kind == BindKind.DPad)
        {
            return b.dpadDir switch
            {
                0 => $"JOY {b.joyIndex} UP",
                1 => $"JOY {b.joyIndex} DOWN",
                2 => $"JOY {b.joyIndex} LEFT",
                3 => $"JOY {b.joyIndex} RIGHT",
                _ => $"JOY {b.joyIndex} DPAD"
            };
        }

        if (b.kind == BindKind.JoyButton)
        {
            string btn = b.joyButton switch
            {
                0 => "A",
                1 => "B",
                2 => "X",
                3 => "Y",
                4 => "L",
                5 => "R",
                6 => "LT",
                7 => "RT",
                8 => "START",
                9 => "SELECT",
                _ => $"B{b.joyButton}"
            };

            return $"JOY {b.joyIndex} {btn}";
        }

        return "UNK";
    }

    void UpdateCursorPosition_ByLinkId(string linkId)
    {
        if (menuText == null || cursorRenderer == null)
            return;

        cursorRenderer.gameObject.SetActive(true);

        menuText.ForceMeshUpdate();
        var ti = menuText.textInfo;
        if (ti == null || ti.linkCount <= 0)
            return;

        bool foundAny = false;
        float bestY = float.NegativeInfinity;
        Vector3 bestLocalPos = default;

        for (int i = 0; i < ti.linkCount; i++)
        {
            var li = ti.linkInfo[i];
            if (li.GetLinkID() != linkId)
                continue;

            int first = li.linkTextfirstCharacterIndex;
            int last = first + li.linkTextLength - 1;
            if (first < 0 || first >= ti.characterCount)
                continue;

            last = Mathf.Min(last, ti.characterCount - 1);

            int anchorChar = -1;
            for (int c = first; c <= last; c++)
            {
                var ch = ti.characterInfo[c];
                if (!ch.isVisible) continue;

                char cc = ch.character;
                if (cc != ' ' && cc != '\u00A0' && cc != '\n' && cc != '\r' && cc != '\t')
                {
                    anchorChar = c;
                    break;
                }
            }

            if (anchorChar < 0) anchorChar = first;
            if (anchorChar < 0 || anchorChar >= ti.characterCount) continue;

            var ci = ti.characterInfo[anchorChar];

            float x = ci.bottomLeft.x - cursorGapLeft;
            float y = (ci.ascender + ci.descender) * 0.5f + cursorLineCenterAdjustY;

            if (y > bestY)
            {
                bestY = y;
                bestLocalPos = new Vector3(x + cursorOffset.x, y + cursorOffset.y, 0f);
                foundAny = true;
            }
        }

        if (!foundAny)
            return;

        cursorRenderer.SetExternalBaseLocalPosition(bestLocalPos);
    }

    void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null) return;

        var music = GameMusicController.Instance;
        if (music == null) return;

        music.PlaySfx(clip, volume);
    }

    void SetFadeAlpha(float a)
    {
        if (fadeImage == null) return;
        var c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;
    }

    IEnumerator FadeTo(float targetA, float duration)
    {
        if (fadeImage == null)
            yield break;

        float d = Mathf.Max(0.001f, duration);
        float start = fadeImage.color.a;
        float t = 0f;

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(start, targetA, Mathf.Clamp01(t / d));
            SetFadeAlpha(a);
            yield return null;
        }

        SetFadeAlpha(targetA);
    }

    static string RepeatNewLine(int count)
    {
        if (count <= 0) return string.Empty;
        return new string('\n', count);
    }

    static string ActionToLabel(PlayerAction a)
    {
        return a switch
        {
            PlayerAction.MoveUp => "UP",
            PlayerAction.MoveDown => "DOWN",
            PlayerAction.MoveLeft => "LEFT",
            PlayerAction.MoveRight => "RIGHT",
            PlayerAction.Start => "START",
            PlayerAction.ActionA => "A",
            PlayerAction.ActionB => "B",
            PlayerAction.ActionC => "C",
            _ => a.ToString().ToUpperInvariant(),
        };
    }

    static string PrettyKeyName(KeyCode key)
    {
        return key switch
        {
            KeyCode.UpArrow => "UP ARROW",
            KeyCode.DownArrow => "DOWN ARROW",
            KeyCode.LeftArrow => "LEFT ARROW",
            KeyCode.RightArrow => "RIGHT ARROW",

            KeyCode.LeftShift => "LEFT SHIFT",
            KeyCode.RightShift => "RIGHT SHIFT",

            KeyCode.LeftControl => "LEFT CTRL",
            KeyCode.RightControl => "RIGHT CTRL",

            KeyCode.LeftAlt => "LEFT ALT",
            KeyCode.RightAlt => "RIGHT ALT",

            KeyCode.Return => "ENTER",
            KeyCode.Escape => "ESC",

            KeyCode.Backspace => "BACK SPACE",
            KeyCode.Delete => "DELETE",

            KeyCode.Space => "SPACE",

            KeyCode.Keypad0 => "KEYPAD 0",
            KeyCode.Keypad1 => "KEYPAD 1",
            KeyCode.Keypad2 => "KEYPAD 2",
            KeyCode.Keypad3 => "KEYPAD 3",
            KeyCode.Keypad4 => "KEYPAD 4",
            KeyCode.Keypad5 => "KEYPAD 5",
            KeyCode.Keypad6 => "KEYPAD 6",
            KeyCode.Keypad7 => "KEYPAD 7",
            KeyCode.Keypad8 => "KEYPAD 8",
            KeyCode.Keypad9 => "KEYPAD 9",

            KeyCode.KeypadEnter => "KEYPAD ENTER",

            KeyCode.Comma => ",",
            KeyCode.Period => ".",
            KeyCode.Slash => "/",

            _ => SplitCamelCase(key.ToString()).ToUpperInvariant()
        };
    }

    static string SplitCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sb = new System.Text.StringBuilder(input.Length * 2);
        sb.Append(input[0]);

        for (int i = 1; i < input.Length; i++)
        {
            char c = input[i];
            if (char.IsUpper(c) && !char.IsUpper(input[i - 1]))
                sb.Append(' ');

            sb.Append(c);
        }

        return sb.ToString();
    }
}
