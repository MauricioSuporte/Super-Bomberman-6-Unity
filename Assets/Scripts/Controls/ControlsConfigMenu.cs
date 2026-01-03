using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class ControlsConfigMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] GameObject root;
    [SerializeField] Image backgroundImage;

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
    [SerializeField] int titleOffsetLines = -1;

    [Header("Body")]
    [SerializeField] int bodyFontSize = 32;

    [Header("Layout")]
    [SerializeField] int headerTopPaddingLines = 1;
    [SerializeField] int headerBottomPaddingLines = 1;
    [SerializeField] int footerGapLines = 1;
    [SerializeField] int footerFontSize = 28;

    [Header("Text Style (SB5-like)")]
    [SerializeField] bool forceBold = true;

    [Header("Outline (TMP SDF)")]
    [SerializeField] Color outlineColor = Color.black;
    [SerializeField, Range(0f, 1f)] float outlineWidth = 0.42f;
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

    enum MenuState { Home, ConfirmRemapAll, BulkRemap, ConfirmReset }

    enum HomeOption { RemapAll = 0, Reset = 1, Back = 2 }

    MenuState state;
    int bulkStep;

    HomeOption homeOption = HomeOption.RemapAll;

    Material runtimeMenuMat;
    Coroutine cursorPulseRoutine;

    Dictionary<PlayerAction, Binding> bulkSnapshot;

    struct DpadHit { public int dir; }

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

    public IEnumerator OpenRoutine(AudioClip restoreMusic, float restoreMusicVolume = 1f)
    {
        if (root == null) root = gameObject;

        root.transform.SetAsLastSibling();
        root.SetActive(true);

        SetupMenuTextMaterial();

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

        state = MenuState.Home;
        bulkStep = 0;
        bulkSnapshot = null;
        homeOption = HomeOption.RemapAll;

        if (cursorRenderer != null)
        {
            cursorRenderer.gameObject.SetActive(true);
            cursorRenderer.RefreshFrame();
        }

        var input = PlayerInputManager.Instance;

        while (input.Get(PlayerAction.ActionA) ||
               input.Get(PlayerAction.ActionB) ||
               input.Get(PlayerAction.ActionC) ||
               input.Get(PlayerAction.Start) ||
               input.Get(PlayerAction.MoveUp) ||
               input.Get(PlayerAction.MoveDown))
            yield return null;

        yield return null;

        RefreshText();

        bool done = false;
        while (!done)
        {
            var p1 = PlayerInputManager.Instance.GetPlayer(1);

            if (state == MenuState.BulkRemap)
            {
                bool escCancel = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

                if (escCancel)
                {
                    if (bulkSnapshot != null)
                        p1.ApplyBindings(bulkSnapshot);

                    bulkSnapshot = null;
                    bulkStep = 0;
                    state = MenuState.Home;

                    PlaySfx(backSfx, backVolume);
                    RefreshText();
                    yield return null;
                    continue;
                }

                DpadHit? dpad = ReadAnyDpadDownThisFrame();
                bool joyBtn = ReadAnyGamepadButtonDownThisFrame(out int legacyBtn);
                KeyCode? key = ReadAnyKeyboardKeyDownNoMouse();

                if (dpad.HasValue || joyBtn || key.HasValue)
                {
                    var action = BulkActions[Mathf.Clamp(bulkStep, 0, BulkActions.Length - 1)];

                    if (dpad.HasValue) p1.SetBinding(action, Binding.FromDpad(1, dpad.Value.dir));
                    else if (joyBtn) p1.SetBinding(action, Binding.FromJoyButton(1, legacyBtn));
                    else p1.SetBinding(action, Binding.FromKey(key.Value));

                    PlaySfx(confirmSfx, confirmVolume);
                    yield return PulseCursor();

                    bulkStep++;

                    if (bulkStep >= BulkActions.Length)
                    {
                        p1.SaveToPrefs();

                        bulkSnapshot = null;
                        bulkStep = 0;
                        state = MenuState.Home;

                        RefreshText();
                        yield return null;
                        continue;
                    }

                    RefreshText();
                }

                yield return null;
                continue;
            }

            if (state == MenuState.ConfirmRemapAll)
            {
                if (input.GetDown(PlayerAction.ActionB))
                {
                    PlaySfx(backSfx, backVolume);
                    state = MenuState.Home;
                    RefreshText();
                }
                else if (input.GetDown(PlayerAction.Start) || input.GetDown(PlayerAction.ActionA))
                {
                    bulkSnapshot = p1.CloneBindings();
                    bulkStep = 0;
                    state = MenuState.BulkRemap;

                    PlaySfx(confirmSfx, confirmVolume);
                    RefreshText();
                    yield return PulseCursor();
                }

                yield return null;
                continue;
            }

            if (state == MenuState.ConfirmReset)
            {
                if (input.GetDown(PlayerAction.ActionB))
                {
                    PlaySfx(backSfx, backVolume);
                    state = MenuState.Home;
                    RefreshText();
                }
                else if (input.GetDown(PlayerAction.Start) || input.GetDown(PlayerAction.ActionA))
                {
                    p1.ResetToDefault();
                    p1.SaveToPrefs();

                    PlaySfx(resetSfx, resetVolume);
                    state = MenuState.Home;
                    RefreshText();
                    yield return PulseCursor();
                }

                yield return null;
                continue;
            }

            if (state == MenuState.Home)
            {
                if (input.GetDown(PlayerAction.ActionC))
                {
                    homeOption = HomeOption.Reset;
                    PlaySfx(confirmSfx, confirmVolume);
                    state = MenuState.ConfirmReset;
                    RefreshText();
                    yield return PulseCursor();
                    yield return null;
                    continue;
                }

                if (input.GetDown(PlayerAction.MoveUp))
                {
                    var prev = homeOption;
                    homeOption = (HomeOption)Mathf.Clamp(((int)homeOption) - 1, 0, 2);

                    if (homeOption != prev)
                        PlaySfx(moveOptionSfx, moveOptionVolume);

                    RefreshText();
                }
                else if (input.GetDown(PlayerAction.MoveDown))
                {
                    var prev = homeOption;
                    homeOption = (HomeOption)Mathf.Clamp(((int)homeOption) + 1, 0, 2);

                    if (homeOption != prev)
                        PlaySfx(moveOptionSfx, moveOptionVolume);

                    RefreshText();
                }

                if (input.GetDown(PlayerAction.ActionB))
                {
                    PlaySfx(backSfx, backVolume);
                    yield return PulseCursor();
                    done = true;
                }
                else if (input.GetDown(PlayerAction.Start) || input.GetDown(PlayerAction.ActionA))
                {
                    switch (homeOption)
                    {
                        case HomeOption.RemapAll:
                            PlaySfx(confirmSfx, confirmVolume);
                            state = MenuState.ConfirmRemapAll;
                            RefreshText();
                            yield return PulseCursor();
                            break;

                        case HomeOption.Reset:
                            PlaySfx(confirmSfx, confirmVolume);
                            state = MenuState.ConfirmReset;
                            RefreshText();
                            yield return PulseCursor();
                            break;

                        case HomeOption.Back:
                            PlaySfx(backSfx, backVolume);
                            yield return PulseCursor();
                            done = true;
                            break;
                    }
                }
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

        if (root != null)
            root.SetActive(false);

        if (fadeImage != null)
            fadeImage.gameObject.SetActive(false);

        if (restoreMusic != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlayMusic(restoreMusic, Mathf.Clamp01(restoreMusicVolume), true);
    }

    static DpadHit? ReadAnyDpadDownThisFrame()
    {
        foreach (var pad in Gamepad.all)
        {
            if (pad == null) continue;

            if (pad.dpad.up.wasPressedThisFrame) return new DpadHit { dir = 0 };
            if (pad.dpad.down.wasPressedThisFrame) return new DpadHit { dir = 1 };
            if (pad.dpad.left.wasPressedThisFrame) return new DpadHit { dir = 2 };
            if (pad.dpad.right.wasPressedThisFrame) return new DpadHit { dir = 3 };
        }

        return null;
    }

    static bool ReadAnyGamepadButtonDownThisFrame(out int legacyBtn)
    {
        legacyBtn = -1;

        foreach (var pad in Gamepad.all)
        {
            if (pad == null) continue;

            if (pad.buttonSouth.wasPressedThisFrame) { legacyBtn = 0; return true; }
            if (pad.buttonEast.wasPressedThisFrame) { legacyBtn = 1; return true; }
            if (pad.buttonWest.wasPressedThisFrame) { legacyBtn = 2; return true; }
            if (pad.buttonNorth.wasPressedThisFrame) { legacyBtn = 3; return true; }

            if (pad.leftShoulder.wasPressedThisFrame) { legacyBtn = 4; return true; }
            if (pad.rightShoulder.wasPressedThisFrame) { legacyBtn = 5; return true; }

            if (pad.leftTrigger.wasPressedThisFrame) { legacyBtn = 6; return true; }
            if (pad.rightTrigger.wasPressedThisFrame) { legacyBtn = 7; return true; }

            if (pad.startButton.wasPressedThisFrame) { legacyBtn = 8; return true; }
            if (pad.selectButton.wasPressedThisFrame) { legacyBtn = 9; return true; }
        }

        return false;
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

        switch (key)
        {
            case Key.W: kc = KeyCode.W; return true;
            case Key.A: kc = KeyCode.A; return true;
            case Key.S: kc = KeyCode.S; return true;
            case Key.D: kc = KeyCode.D; return true;

            case Key.UpArrow: kc = KeyCode.UpArrow; return true;
            case Key.DownArrow: kc = KeyCode.DownArrow; return true;
            case Key.LeftArrow: kc = KeyCode.LeftArrow; return true;
            case Key.RightArrow: kc = KeyCode.RightArrow; return true;

            case Key.Enter: kc = KeyCode.Return; return true;
            case Key.Escape: kc = KeyCode.Escape; return true;
            case Key.Space: kc = KeyCode.Space; return true;

            case Key.M: kc = KeyCode.M; return true;
            case Key.N: kc = KeyCode.N; return true;
            case Key.B: kc = KeyCode.B; return true;
            case Key.C: kc = KeyCode.C; return true;

            default:
                return false;
        }
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

        const string colorNormal = "#FFFFE7";
        const string colorHint = "#FFA621";
        const string colorWhite = "#FFFFFF";

        var p1 = PlayerInputManager.Instance.GetPlayer(1);

        string header =
            RepeatNewLine(headerTopPaddingLines + titleOffsetLines) +
            $"<align=center><size={titleFontSize}><color={colorHint}>CONTROLS</color></size></align>" +
            RepeatNewLine(headerBottomPaddingLines) +
            "\n";

        string body = "<align=left>";

        for (int i = 0; i < BulkActions.Length; i++)
        {
            var a = BulkActions[i];
            string left = ActionToLabel(a);
            string right = BindingToLabel(p1.GetBinding(a));

            string line = $"{left,-12}  :  {right}";

            if (state == MenuState.BulkRemap && i == Mathf.Clamp(bulkStep, 0, BulkActions.Length - 1))
                line = $"{left,-12}  :  <color={colorHint}>PRESS A KEY...</color>";

            body += $"<link=\"bind{i}\"><size={bodyFontSize}><color={colorNormal}>{line}</color></size></link>\n";
        }

        body += RepeatNewLine(footerGapLines);

        if (state == MenuState.ConfirmRemapAll)
        {
            body +=
                $"<align=center><size={footerFontSize}>" +
                $"<color={colorHint}>REMAP ALL?</color>\n" +
                $"<color={colorHint}>A / START:</color> <color={colorWhite}>YES</color>    " +
                $"<color={colorHint}>B:</color> <color={colorWhite}>NO</color>" +
                $"</size></align>";
        }
        else if (state == MenuState.ConfirmReset)
        {
            body +=
                $"<align=center><size={footerFontSize}>" +
                $"<color={colorHint}>RESET TO DEFAULT?</color>\n" +
                $"<color={colorHint}>A / START:</color> <color={colorWhite}>YES</color>    " +
                $"<color={colorHint}>B:</color> <color={colorWhite}>NO</color>" +
                $"</size></align>";
        }
        else if (state == MenuState.BulkRemap)
        {
            var a = BulkActions[Mathf.Clamp(bulkStep, 0, BulkActions.Length - 1)];
            body +=
                $"<align=center><size={footerFontSize}>" +
                $"<color={colorHint}>REMAP:</color> <color={colorWhite}>{ActionToLabel(a)}</color>\n" +
                $"<color={colorHint}>ESC:</color> <color={colorWhite}>CANCEL REMAPPING</color>" +
                $"</size></align>";
        }
        else
        {
            string remapLine = $"<link=\"home0\"><color={colorHint}>A / START:</color> <color={colorWhite}>REMAP ALL</color></link>";
            string resetLine = $"<link=\"home1\"><color={colorHint}>C:</color> <color={colorWhite}>RESET TO DEFAULT</color></link>";
            string backLine = $"<link=\"home2\"><color={colorHint}>B:</color> <color={colorWhite}>BACK</color></link>";

            body +=
                $"<align=center><size={footerFontSize}>" +
                remapLine + "\n" +
                resetLine + "\n" +
                backLine +
                $"</size></align>";
        }

        body += "</align>";

        menuText.text = header + body;

        UpdateCursorPosition();
    }

    static string BindingToLabel(Binding b)
    {
        if (b.kind == BindKind.Key)
            return b.key.ToString().ToUpperInvariant();

        if (b.kind == BindKind.DPad)
        {
            string dir = b.dpadDir switch
            {
                0 => "DPAD UP",
                1 => "DPAD DOWN",
                2 => "DPAD LEFT",
                3 => "DPAD RIGHT",
                _ => "DPAD"
            };

            return $"JOY{b.joyIndex} {dir}";
        }

        if (b.kind == BindKind.JoyButton)
            return $"JOY{b.joyIndex} BTN{b.joyButton}";

        return "UNKNOWN";
    }

    void UpdateCursorPosition()
    {
        if (menuText == null || cursorRenderer == null)
            return;

        bool show =
            state == MenuState.BulkRemap ||
            state == MenuState.Home;

        cursorRenderer.gameObject.SetActive(show);

        if (!show)
            return;

        menuText.ForceMeshUpdate();
        var ti = menuText.textInfo;
        if (ti == null || ti.linkCount <= 0)
            return;

        string targetId;

        if (state == MenuState.BulkRemap)
        {
            int target = Mathf.Clamp(bulkStep, 0, BulkActions.Length - 1);
            targetId = $"bind{target}";
        }
        else
        {
            targetId = $"home{(int)homeOption}";
        }

        TMP_LinkInfo? link = null;
        for (int i = 0; i < ti.linkCount; i++)
        {
            var li = ti.linkInfo[i];
            if (li.GetLinkID() == targetId)
            {
                link = li;
                break;
            }
        }

        if (!link.HasValue)
            return;

        int first = link.Value.linkTextfirstCharacterIndex;
        int last = first + link.Value.linkTextLength - 1;
        if (first < 0 || last < 0 || first >= ti.characterCount)
            return;

        last = Mathf.Min(last, ti.characterCount - 1);

        int anchorChar = first;
        for (int i = first; i <= last; i++)
        {
            var ch = ti.characterInfo[i];
            if (!ch.isVisible)
                continue;

            char cc = ch.character;
            if (cc != ' ' && cc != '\u00A0' && cc != '\n' && cc != '\r' && cc != '\t')
            {
                anchorChar = i;
                break;
            }
        }

        var ci = ti.characterInfo[anchorChar];

        float x = ci.bottomLeft.x - cursorGapLeft;
        float y = (ci.ascender + ci.descender) * 0.5f + cursorLineCenterAdjustY;

        Vector3 localPos = new(x + cursorOffset.x, y + cursorOffset.y, 0f);
        cursorRenderer.SetExternalBaseLocalPosition(localPos);
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
        if (count == 0) return string.Empty;
        if (count > 0) return new string('\n', count);
        return string.Empty;
    }

    static string ActionToLabel(PlayerAction a)
    {
        return a switch
        {
            PlayerAction.MoveUp => "MOVE UP",
            PlayerAction.MoveDown => "MOVE DOWN",
            PlayerAction.MoveLeft => "MOVE LEFT",
            PlayerAction.MoveRight => "MOVE RIGHT",
            PlayerAction.Start => "START",
            PlayerAction.ActionA => "A",
            PlayerAction.ActionB => "B",
            PlayerAction.ActionC => "C",
            _ => a.ToString().ToUpperInvariant(),
        };
    }
}
