using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public sealed class TouchControlsMenu : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    private Canvas canvas;
    private TMP_FontAsset font;
    private Material textMaterial;
    private FontStyles textStyle;
    private RawImage sourceBackground;
    private RawImage animatedBackground;
    private TMP_Text upArrow;
    private TMP_Text downArrow;
    private int dragPointer = int.MinValue;
    private Vector2 dragOrigin;
    private int dragFirst;
    private const float RowHeight = 0.108f;
    private const float TextInset = 0.048f;
    private CanvasGroup transition;
    private System.Action confirmSound;
    private System.Action returnSound;
    private System.Action resetSound;
    private System.Action moveSound;
    private static readonly Color NormalText = new Color32(255, 255, 231, 255);
    private static readonly Color HintText = new Color32(255, 166, 33, 255);
    private RectTransform panel;
    private int selected = 1;
    private int first = 1;
    private bool done;
    private bool editing;
    private bool confirmReset;
    private bool resetAccepted;
    private float acceptInputAfter;
    private int lastSelectedControl = -1;
    private int width;
    private int height;
    private readonly System.Collections.Generic.List<GameObject> widgets = new();
    private SavedTouchControls Settings => SaveSystem.GetTouchControls();
    private static string T(int i) => GameTextDatabase.Touch(i).ToUpperInvariant();

    public static IEnumerator Open(TMP_Text sourceText, RawImage background,
        System.Action onConfirm, System.Action onReturn, System.Action onReset, System.Action onMove)
    {
        var go = new GameObject("TouchControlsMenu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var menu = go.AddComponent<TouchControlsMenu>();
        menu.font = sourceText.font;
        menu.textMaterial = sourceText.fontSharedMaterial;
        menu.textStyle = sourceText.fontStyle;
        menu.sourceBackground = background;
        menu.confirmSound = onConfirm;
        menu.returnSound = onReturn;
        menu.resetSound = onReset;
        menu.moveSound = onMove;
        menu.transition = go.AddComponent<CanvasGroup>();
        menu.transition.alpha = 0f;
        menu.transition.interactable = false;
        menu.acceptInputAfter = float.PositiveInfinity;
        menu.canvas = go.GetComponent<Canvas>();
        menu.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        menu.canvas.sortingOrder = 1100;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        if (MobileControlsRoot.Instance == null)
        {
            var prefab = Resources.Load<GameObject>("UI/MobileControlsPrefab");
            if (prefab != null) Instantiate(prefab);
        }
        yield return null;
        MobileControlsRoot.Instance?.SetSettingsOpen(true);
        menu.Draw();
        yield return menu.Fade(1f);
        menu.acceptInputAfter = Time.unscaledTime;
        menu.transition.interactable = true;
        while (!menu.done) yield return null;
        menu.acceptInputAfter = float.PositiveInfinity;
        menu.transition.interactable = false;
        yield return menu.Fade(0f);
        SaveSystem.SaveTouchControls();
        MobileControlsRoot.Instance?.SetSettingsOpen(false);
        Destroy(go);
    }

    IEnumerator Fade(float target)
    {
        float start = transition.alpha;
        const float duration = 0.18f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            transition.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        transition.alpha = target;
    }

    void OnEnable() => Canvas.willRenderCanvases += UpdateAnimatedVisuals;
    void OnDisable()
    {
        Canvas.willRenderCanvases -= UpdateAnimatedVisuals;
        dragPointer = int.MinValue;
    }

    // Copy after the source's LateUpdate scroller, without rebuilding the menu.
    void UpdateAnimatedVisuals()
    {
        if (animatedBackground != null && sourceBackground != null)
        {
            animatedBackground.texture = sourceBackground.texture;
            animatedBackground.uvRect = sourceBackground.uvRect;
            animatedBackground.color = sourceBackground.color;
        }
        float alpha = Mathf.Repeat(Time.unscaledTime, 0.9f) < 0.45f ? 1f : 0f;
        if (upArrow != null) upArrow.alpha = alpha;
        if (downArrow != null) downArrow.alpha = alpha;
    }

    bool CanScroll => !done && !editing && !confirmReset && Time.unscaledTime >= acceptInputAfter;

    bool InList(Vector2 screenPoint, Camera eventCamera)
    {
        if (panel == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
            panel, screenPoint, eventCamera, out var point)) return false;
        Rect bounds = panel.rect;
        float y = Mathf.InverseLerp(bounds.yMin, bounds.yMax, point.y);
        return bounds.Contains(point) && y >= 0.16f && y <= 0.82f;
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (!CanScroll || dragPointer != int.MinValue || !InList(e.pressPosition, e.pressEventCamera)) return;
        dragPointer = e.pointerId;
        dragFirst = first;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, e.pressPosition, e.pressEventCamera, out dragOrigin);
        e.eligibleForClick = false;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!CanScroll || e.pointerId != dragPointer) return;
        e.eligibleForClick = false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, e.position, e.pressEventCamera, out var point)) return;
        int rows = Mathf.RoundToInt((point.y - dragOrigin.y) / Mathf.Max(1f, panel.rect.height * RowHeight));
        ScrollTo(dragFirst + rows);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (e.pointerId != dragPointer) return;
        e.eligibleForClick = false;
        dragPointer = int.MinValue;
    }

    public void OnScroll(PointerEventData e)
    {
        if (CanScroll && InList(e.position, e.enterEventCamera) && Mathf.Abs(e.scrollDelta.y) > 0.01f)
            ScrollTo(first - (int)Mathf.Sign(e.scrollDelta.y));
    }

    void ScrollTo(int target)
    {
        int next = Mathf.Clamp(target, 1, 10);
        if (next == first) return;
        first = next;
        selected = Mathf.Clamp(selected, first, first + 5);
        moveSound?.Invoke();
        Draw();
    }

    void OnDestroy() => MobileControlsRoot.Instance?.SetSettingsOpen(false);

    void Update()
    {
        if (canvas == null) return;
        if (width != Screen.width || height != Screen.height ||
            (editing && lastSelectedControl != MobileControlsRoot.Instance.SelectedControl)) Draw();
        var input = PlayerInputManager.Instance;
        if (input == null || Time.unscaledTime < acceptInputAfter) return;
        if (input.GetDown(PlayerAction.ActionB)) { Back(); return; }
        if (editing) return;
        if (confirmReset)
        {
            if (input.GetDown(PlayerAction.MoveLeft) || input.GetDown(PlayerAction.MoveRight))
            { resetAccepted = !resetAccepted; moveSound?.Invoke(); Draw(); }
            if (input.GetDown(PlayerAction.ActionA) || input.GetDown(PlayerAction.Start))
            { if (resetAccepted) ResetAll(); else Back(); }
            return;
        }
        if (input.GetDown(PlayerAction.MoveDown)) { MoveSelection(1); }
        if (input.GetDown(PlayerAction.MoveUp)) { MoveSelection(-1); }
        if (input.GetDown(PlayerAction.MoveLeft)) Change(selected, -1);
        else if (input.GetDown(PlayerAction.MoveRight) || input.GetDown(PlayerAction.ActionA) || input.GetDown(PlayerAction.Start)) Change(selected, 1);
    }

    void MoveSelection(int direction)
    {
        int next = Mathf.Clamp(selected + direction, 1, 15);
        if (next == selected) return;
        selected = next;
        moveSound?.Invoke();
        EnsureVisible();
        Draw();
    }

    void Page(int direction)
    {
        first = Mathf.Clamp(first + direction * 6, 1, 10);
        selected = first;
        moveSound?.Invoke();
        Draw();
    }

    void EnsureVisible()
    {
        if (selected < first) first = selected;
        if (selected > first + 5) first = selected - 5;
        first = Mathf.Clamp(first, 1, 10);
    }

    void Back()
    {
        returnSound?.Invoke();
        if (confirmReset) confirmReset = false;
        else if (editing) { editing = false; MobileControlsRoot.Instance.SetEditing(false); SaveSystem.SaveTouchControls(); }
        else done = true;
        Draw();
    }

    string Value(int row) => row switch
    {
        1 => T(Settings.analog ? 18 : 19), 2 => T(Settings.dynamicAnalog ? 16 : 17),
        3 => T(Settings.blockDiagonals ? 16 : 17), 4 => Mathf.RoundToInt(Settings.deadzone * 100) + "%",
        5 => T(SaveSystem.GetMobileTouchButtonsVisible() ? 16 : 17),
        6 => T(Settings.autoHide ? 16 : 17), 7 => Settings.hideDelay.ToString("0") + " s",
        8 => T(Settings.actionIcons ? 16 : 17), 9 => T(Settings.hideUnused ? 16 : 17),
        11 => Mathf.RoundToInt(Settings.opacity * 100) + "%", _ => ""
    };

    void Change(int row, int direction)
    {
        if (row == 2 && !Settings.analog) return;
        selected = row;
        switch (row)
        {
            case 1: Settings.analog = !Settings.analog; break;
            case 2: if (Settings.analog) Settings.dynamicAnalog = !Settings.dynamicAnalog; break;
            case 3: Settings.blockDiagonals = !Settings.blockDiagonals; break;
            case 4: Settings.deadzone = Mathf.Clamp(Settings.deadzone + direction * 0.05f, 0, 0.8f); break;
            case 5: SaveSystem.SetMobileTouchButtonsVisible(!SaveSystem.GetMobileTouchButtonsVisible()); break;
            case 6: Settings.autoHide = !Settings.autoHide; break;
            case 7: Settings.hideDelay = Mathf.Clamp(Settings.hideDelay + direction, 1, 60); break;
            case 8: Settings.actionIcons = !Settings.actionIcons; break;
            case 9: Settings.hideUnused = !Settings.hideUnused; break;
            case 10: editing = true; MobileControlsRoot.Instance.SetEditing(true); break;
            case 11: Settings.opacity = Mathf.Clamp(Settings.opacity + direction * 0.05f, 0.1f, 1); break;
            case 12: ResetLayouts(true, false); break;
            case 13: ResetLayouts(false, true); break;
            case 14: confirmReset = true; resetAccepted = false; break;
            case 15: Back(); return;
        }
        if (row == 12 || row == 13) resetSound?.Invoke();
        else confirmSound?.Invoke();
        SaveSystem.SaveTouchControls();
        Draw();
    }

    void ResetLayouts(bool positions, bool sizes)
    {
        foreach (var layout in new[] { Settings.portrait, Settings.landscape })
            foreach (var item in layout)
            {
                if (positions) item.positioned = false;
                if (sizes) item.scale = 1;
            }
    }

    void ResetAll()
    {
        resetSound?.Invoke();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new SavedTouchControls()), Settings);
        SaveSystem.SetMobileTouchButtonsVisible(true);
        SaveSystem.SaveTouchControls();
        confirmReset = false;
        Draw();
    }

    void Draw()
    {
        width = Screen.width; height = Screen.height;
        animatedBackground = null;
        upArrow = downArrow = null;
        foreach (var widget in widgets) { widget.SetActive(false); Destroy(widget); }
        widgets.Clear();
        var go = new GameObject("SafeArea", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        widgets.Add(go);
        panel = (RectTransform)go.transform;
        Rect safe = Screen.safeArea;
        panel.anchorMin = new Vector2(safe.xMin / width, safe.yMin / height);
        panel.anchorMax = new Vector2(safe.xMax / width, safe.yMax / height);
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        if (!editing)
        {
            var fitter = go.AddComponent<UICameraViewportFitter>();
            fitter.ForceApplyNow();
        }
        var bg = go.GetComponent<Image>();
        bg.color = new Color(0, 0, 0, editing ? 0 : 0.9f);
        if (!editing && sourceBackground != null && sourceBackground.texture != null)
        {
            var backdrop = Rect("ControlsBackground", 0, 0, 1, 1).gameObject.AddComponent<RawImage>();
            animatedBackground = backdrop;
            backdrop.texture = sourceBackground.texture;
            backdrop.uvRect = sourceBackground.uvRect;
            backdrop.color = sourceBackground.color;
            backdrop.raycastTarget = false;
        }
        bg.raycastTarget = !editing;
        if (editing)
        {
            var root = MobileControlsRoot.Instance;
            lastSelectedControl = root.SelectedControl;
            var layout = Settings.Layout(root.Portrait)[lastSelectedControl];
            Label(T(20), .02f, .82f, .98f, .99f, 20);
            Button(T(24) + " " + ControlName(lastSelectedControl) + " / " + T(root.Portrait ? 22 : 23), .02f, .73f, .50f, .82f,
                () => { root.SelectedControl = (root.SelectedControl + 1) % 8; moveSound?.Invoke(); Draw(); });
            Button("−", .51f, .73f, .61f, .82f, () => Resize(-0.1f));
            Label(T(25) + " " + layout.scale.ToString("0.0"), .62f, .73f, .78f, .82f, 18);
            Button("+", .79f, .73f, .89f, .82f, () => Resize(0.1f));
            Button(T(21), .75f, .62f, .98f, .72f, Back);
            return;
        }
        Label(T(0), .04f, .89f, .96f, .99f, 30).color = HintText;
        if (confirmReset)
        {
            Label(T(26), .05f, .5f, .95f, .75f, 26);
            Button(T(27), .1f, .3f, .45f, .45f, ResetAll).fontStyle |= resetAccepted ? FontStyles.Underline : FontStyles.Normal;
            Button(T(28), .55f, .3f, .9f, .45f, Back).fontStyle |= resetAccepted ? FontStyles.Normal : FontStyles.Underline;
            return;
        }
        if (first > 1) upArrow = Button("▲", .4f, .82f, .6f, .89f, () => Page(-1));
        for (int i = 0; i < 6 && first + i <= 15; i++)
        {
            int row = first + i;
            float top = .81f - i * RowHeight;
            bool available = row != 2 || Settings.analog;
            bool numeric = row == 4 || row == 7 || row == 11;
            var label = Button(T(row), .04f, top - .095f, .96f, top, () => Change(row, 1), available);
            label.color = available ? NormalText : Color.gray;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.rectTransform.anchorMin = new Vector2(TextInset, top - .089f);
            label.rectTransform.anchorMax = new Vector2(.60f, top - .006f);
            var value = Label(Value(row), numeric ? .81f : .62f, top - .089f, 1f - TextInset, top - .006f, 23);
            value.color = available ? HintText : Color.gray;
            value.alignment = TextAlignmentOptions.MidlineRight;
            if (numeric)
            {
                Button("−", .62f, top - .095f, .70f, top, () => Change(row, -1));
                Button("+", .71f, top - .095f, .79f, top, () => Change(row, 1));
            }
        }
        if (first + 5 < 15) downArrow = Button("▼", .4f, .09f, .6f, .16f, () => Page(1));
        Button(T(15), .65f, .01f, .96f, .09f, Back);
    }

    static string ControlName(int index) => index switch { 0 => T(1), 1 => "A", 2 => "B", 3 => "C", 4 => "START", 5 => "SELECT", 6 => "L", _ => "R" };

    void Resize(float delta)
    {
        var root = MobileControlsRoot.Instance;
        var item = Settings.Layout(root.Portrait)[root.SelectedControl];
        item.scale = Mathf.Clamp(item.scale + delta, 0.5f, 2f);
        confirmSound?.Invoke();
        SaveSystem.SaveTouchControls(); Draw();
    }

    RectTransform Rect(string name, float x0, float y0, float x1, float y1)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(panel, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    TMP_Text Label(string value, float x0, float y0, float x1, float y1, float size)
    {
        var rt = Rect("Label", x0, y0, x1, y1);
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSharedMaterial = textMaterial;
        text.fontStyle = textStyle | FontStyles.UpperCase;
        text.color = NormalText;
        text.text = value;
        text.fontSize = size;
        text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = size;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        LocalizedTmpFontFallback.Apply(text);
        return text;
    }

    TMP_Text Button(string title, float x0, float y0, float x1, float y1, UnityEngine.Events.UnityAction action, bool available = true)
    {
        var rt = Rect("Button", x0, y0, x1, y1);
        var image = rt.gameObject.AddComponent<Image>();
        image.color = editing ? new Color(0, 0, 0, .75f) : Color.clear;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image; button.interactable = available;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() =>
        {
            if (!done && Time.unscaledTime >= acceptInputAfter) action();
        });
        return Label(title, x0 + .008f, y0 + .006f, x1 - .008f, y1 - .006f, 24);
    }
}