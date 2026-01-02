using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] AudioClip moveCursorSfx;
    [SerializeField, Range(0f, 1f)] float moveCursorVolume = 1f;

    [SerializeField] AudioClip confirmSfx;
    [SerializeField, Range(0f, 1f)] float confirmVolume = 1f;

    [SerializeField] AudioClip backSfx;
    [SerializeField, Range(0f, 1f)] float backVolume = 1f;

    [SerializeField] AudioClip resetSfx;
    [SerializeField, Range(0f, 1f)] float resetVolume = 1f;

    [Header("Remap Debug")]
    [SerializeField] bool remapDebugLogs = false;

    enum RowKind { Binding, Reset, Back }

    struct Row
    {
        public RowKind kind;
        public PlayerAction action;

        public Row(RowKind k, PlayerAction a = default)
        {
            kind = k;
            action = a;
        }
    }

    readonly Row[] rows = new[]
    {
        new Row(RowKind.Binding, PlayerAction.MoveUp),
        new Row(RowKind.Binding, PlayerAction.MoveDown),
        new Row(RowKind.Binding, PlayerAction.MoveLeft),
        new Row(RowKind.Binding, PlayerAction.MoveRight),
        new Row(RowKind.Binding, PlayerAction.Start),
        new Row(RowKind.Binding, PlayerAction.ActionA),
        new Row(RowKind.Binding, PlayerAction.ActionB),
        new Row(RowKind.Binding, PlayerAction.ActionC),
        new Row(RowKind.Reset),
        new Row(RowKind.Back),
    };

    int index;
    bool waitingForKey;
    Material runtimeMenuMat;
    Coroutine cursorPulseRoutine;

    bool dpadArmed;
    float[] prevDpadX = new float[12];
    float[] prevDpadY = new float[12];

    struct DpadHit
    {
        public int joy;
        public int dir; // 0..3
    }

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

        index = 0;
        waitingForKey = false;

        if (cursorRenderer != null)
        {
            cursorRenderer.gameObject.SetActive(true);
            cursorRenderer.RefreshFrame();
        }

        var input = PlayerInputManager.Instance;

        while (input.Get(PlayerAction.ActionA) ||
               input.Get(PlayerAction.ActionB) ||
               input.Get(PlayerAction.ActionC) ||
               input.Get(PlayerAction.MoveUp) ||
               input.Get(PlayerAction.MoveDown))
            yield return null;

        yield return null;

        RefreshText();

        bool done = false;
        while (!done)
        {
            if (!waitingForKey)
            {
                if (input.GetDown(PlayerAction.MoveUp))
                {
                    index = Wrap(index - 1, rows.Length);
                    PlaySfx(moveCursorSfx, moveCursorVolume);
                    RefreshText();
                }
                else if (input.GetDown(PlayerAction.MoveDown))
                {
                    index = Wrap(index + 1, rows.Length);
                    PlaySfx(moveCursorSfx, moveCursorVolume);
                    RefreshText();
                }
                else if (input.GetDown(PlayerAction.ActionC))
                {
                    ResetDefaults();
                    PlaySfx(resetSfx, resetVolume);
                    RefreshText();
                    yield return PulseCursor();
                }
                else if (input.GetDown(PlayerAction.ActionB))
                {
                    PlaySfx(backSfx, backVolume);
                    yield return PulseCursor();
                    done = true;
                }
                else if (input.GetDown(PlayerAction.Start) || input.GetDown(PlayerAction.ActionA))
                {
                    var row = rows[index];

                    if (row.kind == RowKind.Binding)
                    {
                        PlaySfx(confirmSfx, confirmVolume);
                        waitingForKey = true;

                        var p1 = PlayerInputManager.Instance.GetPlayer(1);

                        for (int j = 1; j <= 11; j++)
                        {
                            prevDpadX[j] = Input.GetAxisRaw($"joy{j}_6");
                            prevDpadY[j] = Input.GetAxisRaw($"joy{j}_7");
                            if (p1.invertDpadY) prevDpadY[j] = -prevDpadY[j];
                        }

                        dpadArmed = false;

                        RefreshText();
                        yield return PulseCursor();
                    }
                    else if (row.kind == RowKind.Reset)
                    {
                        ResetDefaults();
                        PlaySfx(resetSfx, resetVolume);
                        RefreshText();
                        yield return PulseCursor();
                    }
                    else
                    {
                        PlaySfx(confirmSfx, confirmVolume);
                        yield return PulseCursor();
                        done = true;
                    }
                }
            }
            else
            {
                var p1 = PlayerInputManager.Instance.GetPlayer(1);

                DpadHit? dpad = ReadAnyDpadDownEdgeAnyJoystick(p1, prevDpadX, prevDpadY, ref dpadArmed);
                bool joyBtn = ReadAnyJoystickButtonDown(out int joyIndex, out int button);
                KeyCode? key = ReadAnyKeyDownNoMouse();

                if (remapDebugLogs)
                {
                    Debug.Log($"[REMAP] action={rows[index].action} dpad={(dpad.HasValue ? $"J{dpad.Value.joy} dir{dpad.Value.dir}" : "null")} " +
                              $"joyBtn={(joyBtn ? $"Joystick{joyIndex}Button{button}" : "null")} key={(key.HasValue ? key.Value.ToString() : "null")}");
                }

                if (dpad.HasValue || joyBtn || key.HasValue)
                {
                    var row = rows[index];
                    if (row.kind == RowKind.Binding)
                    {
                        if (dpad.HasValue)
                        {
                            p1.joyIndex = dpad.Value.joy;
                            p1.SetBinding(row.action, Binding.FromDpad(dpad.Value.joy, dpad.Value.dir));
                        }
                        else if (joyBtn)
                        {
                            p1.joyIndex = joyIndex;
                            p1.SetBinding(row.action, Binding.FromJoyButton(joyIndex, button));
                        }
                        else
                        {
                            p1.SetBinding(row.action, Binding.FromKey(key.Value));
                        }
                    }

                    waitingForKey = false;
                    PlaySfx(confirmSfx, confirmVolume);
                    RefreshText();
                    yield return PulseCursor();
                }

                if (input.GetDown(PlayerAction.ActionB))
                {
                    waitingForKey = false;
                    PlaySfx(backSfx, backVolume);
                    RefreshText();
                    yield return PulseCursor();
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

    static DpadHit? ReadAnyDpadDownEdgeAnyJoystick(PlayerInputProfile p, float[] prevX, float[] prevY, ref bool armed)
    {
        float dz = Mathf.Clamp(p.axisDeadzone, 0.05f, 0.95f);

        bool anyNeutralNow = false;

        for (int j = 1; j <= 11; j++)
        {
            float x = Input.GetAxisRaw($"joy{j}_6");
            float y = Input.GetAxisRaw($"joy{j}_7");
            if (p.invertDpadY) y = -y;

            bool nowNeutral = Mathf.Abs(x) <= dz && Mathf.Abs(y) <= dz;
            if (nowNeutral) anyNeutralNow = true;

            bool prevNeutral = Mathf.Abs(prevX[j]) <= dz && Mathf.Abs(prevY[j]) <= dz;

            if (armed && prevNeutral && !nowNeutral)
            {
                prevX[j] = x;
                prevY[j] = y;

                if (y >= dz) return new DpadHit { joy = j, dir = 0 };
                if (y <= -dz) return new DpadHit { joy = j, dir = 1 };
                if (x <= -dz) return new DpadHit { joy = j, dir = 2 };
                if (x >= dz) return new DpadHit { joy = j, dir = 3 };
            }

            prevX[j] = x;
            prevY[j] = y;
        }

        if (!armed && anyNeutralNow)
            armed = true;

        return null;
    }

    static bool ReadAnyJoystickButtonDown(out int joyIndex, out int button)
    {
        joyIndex = 0;
        button = -1;

        for (int j = 1; j <= 11; j++)
        {
            for (int b = 0; b <= 19; b++)
            {
                string name = $"Joystick{j}Button{b}";
                if (Enum.TryParse(name, out KeyCode kc) && Input.GetKeyDown(kc))
                {
                    joyIndex = j;
                    button = b;
                    return true;
                }
            }
        }

        return false;
    }

    static KeyCode? ReadAnyKeyDownNoMouse()
    {
        if (!Input.anyKeyDown)
            return null;

        foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
        {
            if (k >= KeyCode.Mouse0 && k <= KeyCode.Mouse6)
                continue;

            if (Input.GetKeyDown(k))
                return k;
        }

        return null;
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
        const string colorSelected = "#39D1B4";
        const string colorHint = "#FFA621";
        const string colorWhite = "#FFFFFF";

        var p1 = PlayerInputManager.Instance.GetPlayer(1);

        string header =
            RepeatNewLine(headerTopPaddingLines + titleOffsetLines) +
            $"<align=center><size={titleFontSize}><color={colorHint}>CONTROLS</color></size></align>" +
            RepeatNewLine(headerBottomPaddingLines) +
            "\n";

        string body = "<align=left>";

        for (int i = 0; i < rows.Length; i++)
        {
            bool sel = i == index;
            string c = sel ? colorSelected : colorNormal;

            var r = rows[i];
            string linkOpen = $"<link=\"row{i}\">";
            string linkClose = "</link>";

            if (r.kind == RowKind.Binding)
            {
                string left = ActionToLabel(r.action);
                string right = BindingToLabel(p1.GetBinding(r.action));

                string line = $"{left,-12}  :  {right}";
                if (sel && waitingForKey)
                    line = $"{left,-12}  :  <color={colorHint}>PRESS A KEY...</color>";

                body += $"{linkOpen}<size={bodyFontSize}><color={c}>{line}</color></size>{linkClose}\n";
            }
            else if (r.kind == RowKind.Reset)
            {
                body += $"{linkOpen}<size={bodyFontSize}><color={c}>RESET TO DEFAULT</color></size>{linkClose}\n";
            }
            else
            {
                body += $"{linkOpen}<size={bodyFontSize}><color={c}>BACK</color></size>{linkClose}\n";
            }
        }

        body += RepeatNewLine(footerGapLines);

        body +=
            $"<align=center><size={footerFontSize}>" +
            $"<color={colorHint}>A:</color> <color={colorWhite}>CONFIRM / PLANT BOMB </color>\n" +
            $"<color={colorHint}>B:</color> <color={colorWhite}>BACK / EXPLODE CONTROL BOMBS</color>\n" +
            $"<color={colorHint}>C:</color> <color={colorWhite}>ABILITIES</color>\n" +
            $"<color={colorHint}>START:</color> <color={colorWhite}>PAUSE / CONFIRM</color>" +
            $"</size></align>";

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

        menuText.ForceMeshUpdate();
        var ti = menuText.textInfo;
        if (ti == null || ti.linkCount <= 0)
            return;

        string targetId = $"row{index}";
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

    void ResetDefaults()
    {
        var p1 = PlayerInputManager.Instance.GetPlayer(1);
        p1.ResetToDefault();
    }

    static int Wrap(int v, int count)
    {
        if (count <= 0) return 0;
        v %= count;
        if (v < 0) v += count;
        return v;
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
