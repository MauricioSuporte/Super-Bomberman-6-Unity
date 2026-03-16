using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TitleScreenDropIntro : MonoBehaviour
{
    const string LOG = "[TitleScreenLogoDropIntro]";

    [Serializable]
    public class CharacterDropSlot
    {
        public string name;
        public Image image;

        [Header("Sprites")]
        public Sprite sprite;
        public Sprite fallingSprite;
        public Sprite landedSprite;

        [Header("Layout")]
        public Vector2 baseSize = new(80f, 80f);
        public bool useSpriteNativeSizeAsCharacterSize = false;
        public Vector2 positionOffset = Vector2.zero;
    }

    enum IntroVisualState
    {
        Hidden = 0,
        CharactersAboveTop = 1,
        CharactersLanded = 2,
        Completed = 3
    }

    [Header("References")]
    [SerializeField] Image logoImage;
    [SerializeField] Sprite logoSprite;

    [Header("Characters (left -> right)")]
    [SerializeField] CharacterDropSlot[] characterSlots = new CharacterDropSlot[7];

    [Header("Audio")]
    [SerializeField] AudioClip characterDropStartSfx;
    [SerializeField, Range(0f, 1f)] float characterDropStartSfxVolume = 1f;

    [Header("Layout")]
    [SerializeField] RectTransform layoutRoot;
    [SerializeField] bool useLayoutRootAsParent = true;

    [Header("Reference Frame")]
    [SerializeField, Min(1)] int referenceWidth = 256;
    [SerializeField, Min(1)] int referenceHeight = 224;

    [Header("Logo Size (base reference pixels 256x224)")]
    [SerializeField] Vector2 logoSize = new(233f, 69f);
    [SerializeField] bool useSpriteNativeSizeAsLogoSize = false;

    [Header("Logo Final Position (base reference pixels, top-center based)")]
    [SerializeField] Vector2 finalAnchoredPosition = new(0f, -8f);

    [Header("Logo Start Offset")]
    [SerializeField] float startOffsetAboveScreen = 72f;

    [Header("Logo Timing")]
    [SerializeField, Min(0.01f)] float dropDuration = 0.25f;
    [SerializeField] bool useUnscaledTime = true;
    [SerializeField] AnimationCurve dropCurve = null;

    [Header("Characters Final Position (base reference pixels, bottom-center based)")]
    [SerializeField] float charactersBottomMargin = 0f;

    [Header("Characters Start Offset")]
    [SerializeField] float charactersStartOffsetAboveScreen = 32f;

    [Header("Characters Timing")]
    [SerializeField, Min(0.01f)] float charactersDropDuration = 0.30f;
    [SerializeField] float charactersDropStagger = 0.035f;
    [SerializeField] AnimationCurve charactersDropCurve = null;
    [SerializeField] float delayAfterCharactersBeforeLogo = 0.03f;

    [Header("Pixel Perfect")]
    [SerializeField] bool roundAnchoredPosition = true;
    [SerializeField] bool applyPointFilter = true;

    [Header("Debug (Surgical Logs)")]
    [SerializeField] bool enableSurgicalLogs = true;

    Coroutine currentRoutine;

    RectTransform _logoRect;
    readonly List<RectTransform> _characterRects = new();

    float _pixelFrameScale = 1f;
    IntroVisualState _visualState = IntroVisualState.Hidden;

    Vector2[] _cachedCharacterFinalBasePositions = Array.Empty<Vector2>();
    Vector2[] _cachedCharacterAboveTopBasePositions = Array.Empty<Vector2>();
    bool[] _characterHasLanded = Array.Empty<bool>();

    Vector2 _cachedLogoFinalBasePosition;
    Vector2 _cachedLogoAboveTopBasePosition;

    bool skipRequested;

    public bool IsPlaying => currentRoutine != null;
    public bool Running => currentRoutine != null;
    public bool Skipped { get; private set; }

    public void SetPixelFrameScale(float scale)
    {
        _pixelFrameScale = Mathf.Max(0.01f, scale);
        RebuildCachedBasePositionsIfPossible();
        ReapplyCurrentResolvedLayout();
        DumpAllState("SetPixelFrameScale");
    }

    void Awake()
    {
        if (dropCurve == null || dropCurve.length == 0)
            dropCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        if (charactersDropCurve == null || charactersDropCurve.length == 0)
            charactersDropCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        SyncThisRectToLayoutRoot();
        EnsureLogo();
        EnsureCharacters();
        EnsureStateCaches();
        RebuildCachedBasePositionsIfPossible();

        HideImmediate();
        DumpAllState("Awake");
    }

    public void SetLayoutRoot(RectTransform root)
    {
        layoutRoot = root;

        SyncThisRectToLayoutRoot();
        EnsureLogo();
        EnsureCharacters();
        EnsureStateCaches();
        RebuildCachedBasePositionsIfPossible();
        ReapplyCurrentResolvedLayout();

        DumpAllState("SetLayoutRoot");
        DumpCharacterVisibilityState("SetLayoutRoot");
    }

    public void HideImmediate()
    {
        StopIntro();

        Skipped = false;
        skipRequested = false;
        _visualState = IntroVisualState.Hidden;

        ResetCharacterLandedState();
        RebuildCachedBasePositionsIfPossible();
        ReapplyCurrentResolvedLayout();

        DumpAllState("HideImmediate");
    }

    public void PrepareAboveTop()
    {
        EnsureLogo();
        EnsureCharacters();
        EnsureStateCaches();

        Skipped = false;
        skipRequested = false;

        ApplyLogoSprite();
        ApplyCharacterSprites(false);
        ResetCharacterLandedState();

        RebuildCachedBasePositionsIfPossible();
        UpdateCharacterSiblingOrder();

        _visualState = IntroVisualState.CharactersAboveTop;
        ReapplyCurrentResolvedLayout();

        DumpAllState("PrepareAboveTop");
        DumpCharacterVisibilityState("PrepareAboveTop");
    }

    public void Skip()
    {
        if (!Running)
            return;

        skipRequested = true;
        Skipped = true;

        if (enableSurgicalLogs)
            Debug.Log($"{LOG} Skip requested", this);
    }

    public IEnumerator PlayIntro()
    {
        StopIntro();

        EnsureLogo();
        EnsureCharacters();
        EnsureStateCaches();

        Skipped = false;
        skipRequested = false;

        ApplyLogoSprite();
        ApplyCharacterSprites(false);
        ResetCharacterLandedState();

        RebuildCachedBasePositionsIfPossible();
        UpdateCharacterSiblingOrder();

        currentRoutine = StartCoroutine(PlayMasterRoutine());
        yield return currentRoutine;
    }

    public void StopIntro()
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }

        skipRequested = false;
    }

    IEnumerator PlayMasterRoutine()
    {
        ResetCharacterLandedState();
        RebuildCachedBasePositionsIfPossible();
        UpdateCharacterSiblingOrder();

        _visualState = IntroVisualState.CharactersAboveTop;
        ReapplyCurrentResolvedLayout();

        if (enableSurgicalLogs)
            Debug.Log($"{LOG} PlayMasterRoutine | dropping characters first, then logo", this);

        if (HandleSkipIfRequested("PlayMasterRoutine-Begin"))
            yield break;

        yield return PlayCharactersRoutine();

        if (HandleSkipIfRequested("AfterCharacters"))
            yield break;

        if (delayAfterCharactersBeforeLogo > 0f)
        {
            yield return WaitWithSkip(delayAfterCharactersBeforeLogo);

            if (HandleSkipIfRequested("AfterDelayBeforeLogo"))
                yield break;
        }

        PrepareLogoAboveTop(true);

        if (HandleSkipIfRequested("BeforeLogo"))
            yield break;

        yield return PlayLogoRoutine();

        if (HandleSkipIfRequested("AfterLogo"))
            yield break;

        _visualState = IntroVisualState.Completed;
        ReapplyCurrentResolvedLayout();

        currentRoutine = null;
        DumpAllState("PlayMasterRoutine-End");
        DumpCharacterVisibilityState("PlayMasterRoutine-End");
    }

    IEnumerator PlayCharactersRoutine()
    {
        int count = characterSlots != null ? characterSlots.Length : 0;
        if (count == 0)
            yield break;

        EnsureStateCaches();
        RebuildCachedBasePositionsIfPossible();

        float duration = Mathf.Max(0.01f, charactersDropDuration);
        List<List<int>> waveOrder = BuildCenterOutWaveOrder();

        for (int i = 0; i < count; i++)
            ApplyCharacterSprite(i, false);

        _visualState = IntroVisualState.CharactersAboveTop;
        ReapplyCurrentResolvedLayout();

        for (int waveIndex = 0; waveIndex < waveOrder.Count; waveIndex++)
        {
            if (HandleSkipIfRequested($"PlayCharactersRoutine-Wave{waveIndex}-Begin"))
                yield break;

            List<int> wave = waveOrder[waveIndex];
            float t = 0f;

            PlayCharacterDropStartSfx();

            if (enableSurgicalLogs)
            {
                string waveText = "";
                for (int j = 0; j < wave.Count; j++)
                {
                    if (j > 0) waveText += ", ";
                    waveText += wave[j].ToString();
                }

                Debug.Log($"{LOG} PlayCharactersRoutine | wave={waveIndex} | characters=[{waveText}] | playedDropSfx={(characterDropStartSfx != null)}", this);
            }

            while (t < duration)
            {
                if (PollSkipPressed())
                    Skip();

                if (HandleSkipIfRequested($"PlayCharactersRoutine-Wave{waveIndex}-Loop"))
                    yield break;

                float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                t += dt;

                float localT = Mathf.Clamp01(t / duration);
                float eased = charactersDropCurve != null ? charactersDropCurve.Evaluate(localT) : localT;

                for (int j = 0; j < wave.Count; j++)
                {
                    int i = wave[j];
                    CharacterDropSlot slot = characterSlots[i];
                    if (slot == null || slot.image == null)
                        continue;

                    RectTransform rt = slot.image.rectTransform;
                    Vector2 startBase = _cachedCharacterAboveTopBasePositions[i];
                    Vector2 endBase = _cachedCharacterFinalBasePositions[i];
                    Vector2 currentBase = Vector2.LerpUnclamped(startBase, endBase, eased);

                    ApplyAnchoredPositionScaled(rt, currentBase);

                    if (!slot.image.gameObject.activeSelf)
                        slot.image.gameObject.SetActive(true);
                }

                yield return null;
            }

            for (int j = 0; j < wave.Count; j++)
            {
                int i = wave[j];
                CharacterDropSlot slot = characterSlots[i];
                if (slot == null || slot.image == null)
                    continue;

                _characterHasLanded[i] = true;
                ApplyAnchoredPositionScaled(slot.image.rectTransform, _cachedCharacterFinalBasePositions[i]);
                slot.image.gameObject.SetActive(true);
                ApplyCharacterSprite(i, true);

                if (enableSurgicalLogs)
                    Debug.Log($"{LOG} Character[{i}] landed -> switched to landed sprite", this);
            }
        }

        for (int i = 0; i < count; i++)
        {
            CharacterDropSlot slot = characterSlots[i];
            if (slot == null || slot.image == null)
                continue;

            _characterHasLanded[i] = true;
            ApplyAnchoredPositionScaled(slot.image.rectTransform, _cachedCharacterFinalBasePositions[i]);
            slot.image.gameObject.SetActive(true);
            ApplyCharacterSprite(i, true);
        }

        _visualState = IntroVisualState.CharactersLanded;
        ReapplyCurrentResolvedLayout();

        DumpAllState("PlayCharactersRoutine-End");
        DumpCharacterVisibilityState("PlayCharactersRoutine-End");
    }

    IEnumerator PlayLogoRoutine()
    {
        if (_logoRect == null || logoImage == null)
            yield break;

        float duration = Mathf.Max(0.01f, dropDuration);
        float t = 0f;

        Vector2 startBase = _cachedLogoAboveTopBasePosition;
        Vector2 endBase = _cachedLogoFinalBasePosition;

        while (t < duration)
        {
            if (PollSkipPressed())
                Skip();

            if (HandleSkipIfRequested("PlayLogoRoutine-Loop"))
                yield break;

            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt;

            float linear = Mathf.Clamp01(t / duration);
            float eased = dropCurve != null ? dropCurve.Evaluate(linear) : linear;

            Vector2 posBase = Vector2.LerpUnclamped(startBase, endBase, eased);
            ApplyAnchoredPositionScaled(_logoRect, posBase);

            if (!logoImage.gameObject.activeSelf)
                logoImage.gameObject.SetActive(true);

            yield return null;
        }

        ApplyAnchoredPositionScaled(_logoRect, endBase);
        logoImage.gameObject.SetActive(true);

        DumpAllState("PlayLogoRoutine-End");
    }

    bool PollSkipPressed()
    {
        var input = PlayerInputManager.Instance;
        if (input == null)
            return false;

        return input.AnyGetDown(PlayerAction.Start) || input.AnyGetDown(PlayerAction.ActionA);
    }

    bool HandleSkipIfRequested(string context)
    {
        if (PollSkipPressed())
            Skip();

        if (!skipRequested)
            return false;

        CompleteImmediateToEndState(context);
        return true;
    }

    void CompleteImmediateToEndState(string context)
    {
        EnsureLogo();
        EnsureCharacters();
        EnsureStateCaches();
        RebuildCachedBasePositionsIfPossible();

        for (int i = 0; i < characterSlots.Length; i++)
        {
            CharacterDropSlot slot = characterSlots[i];
            if (slot == null || slot.image == null)
                continue;

            _characterHasLanded[i] = true;
            slot.image.gameObject.SetActive(true);
            ApplyAnchoredPositionScaled(slot.image.rectTransform, _cachedCharacterFinalBasePositions[i]);
            ApplyCharacterSprite(i, true);
        }

        if (logoImage != null && _logoRect != null)
        {
            logoImage.gameObject.SetActive(true);
            ApplyLogoSprite();
            ApplyAnchoredPositionScaled(_logoRect, _cachedLogoFinalBasePosition);
        }

        _visualState = IntroVisualState.Completed;
        currentRoutine = null;
        skipRequested = false;

        if (enableSurgicalLogs)
            Debug.Log($"{LOG} CompleteImmediateToEndState | context={context}", this);

        DumpAllState($"CompleteImmediateToEndState-{context}");
        DumpCharacterVisibilityState($"CompleteImmediateToEndState-{context}");
    }

    IEnumerator WaitWithSkip(float seconds)
    {
        float duration = Mathf.Max(0f, seconds);
        float t = 0f;

        while (t < duration)
        {
            if (PollSkipPressed())
                Skip();

            if (skipRequested)
                yield break;

            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }
    }

    void EnsureLogo()
    {
        if (logoImage == null)
        {
            Transform found = transform.Find("TitleLogo");
            if (found != null)
                logoImage = found.GetComponent<Image>();
        }

        if (logoImage == null)
        {
            GameObject go = new("TitleLogo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            logoImage = go.GetComponent<Image>();
        }

        _logoRect = logoImage.rectTransform;

        Transform desiredParent = GetDesiredParent();
        if (_logoRect.parent != desiredParent)
            _logoRect.SetParent(desiredParent, false);

        _logoRect.anchorMin = new Vector2(0.5f, 1f);
        _logoRect.anchorMax = new Vector2(0.5f, 1f);
        _logoRect.pivot = new Vector2(0.5f, 1f);
        _logoRect.localScale = Vector3.one;
        _logoRect.localRotation = Quaternion.identity;

        logoImage.preserveAspect = true;
        logoImage.raycastTarget = false;
        logoImage.enabled = true;
    }

    void EnsureCharacters()
    {
        _characterRects.Clear();

        if (characterSlots == null)
            characterSlots = Array.Empty<CharacterDropSlot>();

        Transform desiredParent = GetDesiredParent();

        for (int i = 0; i < characterSlots.Length; i++)
        {
            CharacterDropSlot slot = characterSlots[i];
            if (slot == null)
                continue;

            if (slot.image == null)
            {
                string objName = string.IsNullOrWhiteSpace(slot.name)
                    ? $"TitleCharacter_{i}"
                    : slot.name;

                Transform found = transform.Find(objName);
                if (found != null)
                    slot.image = found.GetComponent<Image>();

                if (slot.image == null)
                {
                    GameObject go = new(objName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    slot.image = go.GetComponent<Image>();
                }
            }

            RectTransform rt = slot.image.rectTransform;

            if (rt.parent != desiredParent)
                rt.SetParent(desiredParent, false);

            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            slot.image.preserveAspect = true;
            slot.image.raycastTarget = false;
            slot.image.enabled = true;

            _characterRects.Add(rt);
        }
    }

    Transform GetDesiredParent()
    {
        if (useLayoutRootAsParent && layoutRoot != null)
            return transform;

        return transform;
    }

    void ApplyLogoSprite()
    {
        if (logoImage == null || logoSprite == null)
            return;

        Texture tex = logoSprite.texture;
        if (tex != null && applyPointFilter)
            tex.filterMode = FilterMode.Point;

        logoImage.sprite = logoSprite;
    }

    void ApplyCharacterSprites(bool landed)
    {
        for (int i = 0; i < characterSlots.Length; i++)
            ApplyCharacterSprite(i, landed);
    }

    void ApplyCharacterSprite(int index, bool landed)
    {
        if (characterSlots == null || index < 0 || index >= characterSlots.Length)
            return;

        CharacterDropSlot slot = characterSlots[index];
        if (slot == null || slot.image == null)
            return;

        Sprite chosen = landed ? GetLandedSprite(slot) : GetFallingSprite(slot);
        if (chosen == null)
            return;

        Texture tex = chosen.texture;
        if (tex != null && applyPointFilter)
            tex.filterMode = FilterMode.Point;

        if (slot.image.sprite != chosen)
            slot.image.sprite = chosen;
    }

    void PlayCharacterDropStartSfx()
    {
        if (characterDropStartSfx == null)
            return;

        AudioSource.PlayClipAtPoint(characterDropStartSfx, Vector3.zero, characterDropStartSfxVolume);
    }

    Sprite GetFallingSprite(CharacterDropSlot slot)
    {
        if (slot == null)
            return null;

        if (slot.fallingSprite != null)
            return slot.fallingSprite;

        return slot.sprite;
    }

    Sprite GetLandedSprite(CharacterDropSlot slot)
    {
        if (slot == null)
            return null;

        if (slot.landedSprite != null)
            return slot.landedSprite;

        return slot.sprite;
    }

    void EnsureStateCaches()
    {
        int count = characterSlots != null ? characterSlots.Length : 0;

        if (_cachedCharacterFinalBasePositions == null || _cachedCharacterFinalBasePositions.Length != count)
            _cachedCharacterFinalBasePositions = new Vector2[count];

        if (_cachedCharacterAboveTopBasePositions == null || _cachedCharacterAboveTopBasePositions.Length != count)
            _cachedCharacterAboveTopBasePositions = new Vector2[count];

        if (_characterHasLanded == null || _characterHasLanded.Length != count)
            _characterHasLanded = new bool[count];
    }

    void ResetCharacterLandedState()
    {
        EnsureStateCaches();

        for (int i = 0; i < _characterHasLanded.Length; i++)
            _characterHasLanded[i] = false;
    }

    void RebuildCachedBasePositionsIfPossible()
    {
        EnsureStateCaches();

        _cachedLogoFinalBasePosition = finalAnchoredPosition;
        _cachedLogoAboveTopBasePosition = GetLogoStartBasePosition();

        Vector2[] finalPositions = ComputeCharacterFinalBasePositions();
        for (int i = 0; i < finalPositions.Length; i++)
        {
            _cachedCharacterFinalBasePositions[i] = finalPositions[i];
            _cachedCharacterAboveTopBasePositions[i] = GetCharacterStartBasePosition(i, finalPositions);
        }
    }

    void ReapplyCurrentResolvedLayout()
    {
        SyncThisRectToLayoutRoot();
        EnsureLogo();
        EnsureCharacters();

        ApplyCurrentScaledLayout();
        UpdateCharacterSiblingOrder();

        switch (_visualState)
        {
            case IntroVisualState.Hidden:
                ApplyHiddenVisualState();
                break;

            case IntroVisualState.CharactersAboveTop:
                ApplyCharactersAboveTopVisualState();
                break;

            case IntroVisualState.CharactersLanded:
                ApplyCharactersLandedVisualState();
                break;

            case IntroVisualState.Completed:
                ApplyCompletedVisualState();
                break;
        }
    }

    void ApplyHiddenVisualState()
    {
        if (logoImage != null)
            logoImage.gameObject.SetActive(false);

        if (characterSlots == null)
            return;

        for (int i = 0; i < characterSlots.Length; i++)
        {
            CharacterDropSlot slot = characterSlots[i];
            if (slot == null || slot.image == null)
                continue;

            slot.image.gameObject.SetActive(false);
        }
    }

    void ApplyCharactersAboveTopVisualState()
    {
        if (characterSlots != null)
        {
            for (int i = 0; i < characterSlots.Length; i++)
            {
                CharacterDropSlot slot = characterSlots[i];
                if (slot == null || slot.image == null)
                    continue;

                ApplyCharacterSprite(i, false);
                slot.image.gameObject.SetActive(true);
                ApplyAnchoredPositionScaled(slot.image.rectTransform, _cachedCharacterAboveTopBasePositions[i]);
            }
        }

        if (logoImage != null && _logoRect != null)
        {
            logoImage.gameObject.SetActive(false);
            ApplyAnchoredPositionScaled(_logoRect, _cachedLogoAboveTopBasePosition);
        }
    }

    void ApplyCharactersLandedVisualState()
    {
        if (characterSlots != null)
        {
            for (int i = 0; i < characterSlots.Length; i++)
            {
                CharacterDropSlot slot = characterSlots[i];
                if (slot == null || slot.image == null)
                    continue;

                slot.image.gameObject.SetActive(true);
                ApplyAnchoredPositionScaled(slot.image.rectTransform, _cachedCharacterFinalBasePositions[i]);

                if (_characterHasLanded != null && i < _characterHasLanded.Length && _characterHasLanded[i])
                    ApplyCharacterSprite(i, true);
                else
                    ApplyCharacterSprite(i, false);
            }
        }

        if (logoImage != null && _logoRect != null)
        {
            logoImage.gameObject.SetActive(false);
            ApplyAnchoredPositionScaled(_logoRect, _cachedLogoAboveTopBasePosition);
        }
    }

    void ApplyCompletedVisualState()
    {
        if (characterSlots != null)
        {
            for (int i = 0; i < characterSlots.Length; i++)
            {
                CharacterDropSlot slot = characterSlots[i];
                if (slot == null || slot.image == null)
                    continue;

                slot.image.gameObject.SetActive(true);
                ApplyAnchoredPositionScaled(slot.image.rectTransform, _cachedCharacterFinalBasePositions[i]);
                ApplyCharacterSprite(i, true);
            }
        }

        if (logoImage != null && _logoRect != null)
        {
            logoImage.gameObject.SetActive(true);
            ApplyAnchoredPositionScaled(_logoRect, _cachedLogoFinalBasePosition);
        }
    }

    void ApplyCurrentScaledLayout()
    {
        if (_logoRect != null)
        {
            Vector2 baseSize = GetEffectiveBaseLogoSize();
            _logoRect.sizeDelta = RoundVec(baseSize * _pixelFrameScale);
        }

        for (int i = 0; i < characterSlots.Length; i++)
        {
            CharacterDropSlot slot = characterSlots[i];
            if (slot == null || slot.image == null)
                continue;

            RectTransform rt = slot.image.rectTransform;
            Vector2 baseSize = GetEffectiveBaseCharacterSize(slot);
            rt.sizeDelta = RoundVec(baseSize * _pixelFrameScale);
        }
    }

    void PrepareCharactersAboveTop()
    {
        RebuildCachedBasePositionsIfPossible();

        for (int i = 0; i < characterSlots.Length; i++)
        {
            CharacterDropSlot slot = characterSlots[i];
            if (slot == null || slot.image == null)
                continue;

            ApplyCharacterSprite(i, false);
            slot.image.gameObject.SetActive(true);
            ApplyAnchoredPositionScaled(slot.image.rectTransform, _cachedCharacterAboveTopBasePositions[i]);
        }

        if (enableSurgicalLogs)
        {
            Debug.Log($"{LOG} PrepareCharactersAboveTop | count={characterSlots.Length}", this);
            for (int i = 0; i < _cachedCharacterFinalBasePositions.Length; i++)
            {
                Vector2 offset = characterSlots[i] != null ? characterSlots[i].positionOffset : Vector2.zero;
                Debug.Log($"{LOG} Character[{i}] finalBase={_cachedCharacterFinalBasePositions[i]} offset={offset}", this);
            }
        }
    }

    void PrepareLogoAboveTop(bool visible)
    {
        if (logoImage == null || _logoRect == null)
            return;

        logoImage.gameObject.SetActive(visible);
        ApplyAnchoredPositionScaled(_logoRect, _cachedLogoAboveTopBasePosition);
    }

    Vector2 GetLogoStartBasePosition()
    {
        return new Vector2(finalAnchoredPosition.x, finalAnchoredPosition.y + startOffsetAboveScreen);
    }

    Vector2[] ComputeCharacterFinalBasePositions()
    {
        int count = characterSlots != null ? characterSlots.Length : 0;
        if (count == 0)
            return Array.Empty<Vector2>();

        Vector2[] sizes = new Vector2[count];
        for (int i = 0; i < count; i++)
            sizes[i] = GetEffectiveBaseCharacterSize(characterSlots[i]);

        Vector2[] positions = new Vector2[count];

        float leftHalf = sizes[0].x * 0.5f;
        float rightHalf = sizes[count - 1].x * 0.5f;

        float minCenterX = -referenceWidth * 0.5f + leftHalf;
        float maxCenterX = referenceWidth * 0.5f - rightHalf;

        if (count == 1)
        {
            positions[0] = new Vector2(0f, charactersBottomMargin) + GetCharacterPositionOffset(0);
            return positions;
        }

        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            float x = Mathf.Lerp(minCenterX, maxCenterX, t);
            Vector2 autoPosition = new Vector2(x, charactersBottomMargin);
            positions[i] = autoPosition + GetCharacterPositionOffset(i);
        }

        return positions;
    }

    Vector2 GetCharacterStartBasePosition(int index, Vector2[] finalBasePositions)
    {
        Vector2 endBase = finalBasePositions[index];
        return new Vector2(endBase.x, endBase.y + referenceHeight + charactersStartOffsetAboveScreen);
    }

    Vector2 GetCharacterPositionOffset(int index)
    {
        if (characterSlots == null || index < 0 || index >= characterSlots.Length)
            return Vector2.zero;

        CharacterDropSlot slot = characterSlots[index];
        if (slot == null)
            return Vector2.zero;

        return slot.positionOffset;
    }

    void UpdateCharacterSiblingOrder()
    {
        if (characterSlots == null || characterSlots.Length == 0)
            return;

        Transform parent = GetDesiredParent();
        if (parent == null)
            return;

        int count = characterSlots.Length;
        int center = count / 2;

        List<int> drawOrder = new();

        drawOrder.Add(center);

        for (int dist = 1; dist <= center; dist++)
        {
            int left = center - dist;
            int right = center + dist;

            if (left >= 0)
                drawOrder.Add(left);

            if (right < count)
                drawOrder.Add(right);
        }

        int sibling = 0;

        for (int i = 0; i < drawOrder.Count; i++)
        {
            int idx = drawOrder[i];
            CharacterDropSlot slot = characterSlots[idx];
            if (slot == null || slot.image == null)
                continue;

            slot.image.transform.SetSiblingIndex(sibling++);
        }

        if (logoImage != null)
            logoImage.transform.SetAsLastSibling();

        if (enableSurgicalLogs)
        {
            string msg = "";
            for (int i = 0; i < drawOrder.Count; i++)
            {
                if (i > 0) msg += " -> ";
                msg += drawOrder[i];
            }

            Debug.Log($"{LOG} UpdateCharacterSiblingOrder | backToFront={msg}", this);
        }
    }

    Vector2 GetEffectiveBaseLogoSize()
    {
        if (useSpriteNativeSizeAsLogoSize && logoSprite != null)
        {
            if (logoSprite.texture != null)
                return new Vector2(logoSprite.texture.width, logoSprite.texture.height);

            Rect r = logoSprite.rect;
            return new Vector2(r.width, r.height);
        }

        return logoSize;
    }

    Vector2 GetEffectiveBaseCharacterSize(CharacterDropSlot slot)
    {
        if (slot == null)
            return new Vector2(80f, 80f);

        Sprite sizeSprite = GetLandedSprite(slot) ?? GetFallingSprite(slot);

        if (slot.useSpriteNativeSizeAsCharacterSize && sizeSprite != null)
        {
            if (sizeSprite.texture != null)
                return new Vector2(sizeSprite.texture.width, sizeSprite.texture.height);

            Rect r = sizeSprite.rect;
            return new Vector2(r.width, r.height);
        }

        return slot.baseSize;
    }

    void ApplyAnchoredPositionScaled(RectTransform rt, Vector2 basePos)
    {
        if (rt == null)
            return;

        Vector2 scaled = basePos * _pixelFrameScale;

        if (roundAnchoredPosition)
            scaled = RoundVec(scaled);

        rt.anchoredPosition = scaled;
    }

    static Vector2 RoundVec(Vector2 v)
    {
        return new Vector2(Mathf.Round(v.x), Mathf.Round(v.y));
    }

    object Wait(float seconds)
    {
        float t = Mathf.Max(0f, seconds);
        return useUnscaledTime ? new WaitForSecondsRealtime(t) : new WaitForSeconds(t);
    }

    List<int> BuildCenterOutOrder()
    {
        List<int> order = new();

        if (characterSlots == null || characterSlots.Length == 0)
            return order;

        int count = characterSlots.Length;
        int center = count / 2;

        order.Add(center);

        for (int dist = 1; dist <= center; dist++)
        {
            int left = center - dist;
            int right = center + dist;

            if (left >= 0)
                order.Add(left);

            if (right < count)
                order.Add(right);
        }

        return order;
    }

    List<List<int>> BuildCenterOutWaveOrder()
    {
        List<List<int>> waves = new();

        if (characterSlots == null || characterSlots.Length == 0)
            return waves;

        int count = characterSlots.Length;
        int center = count / 2;

        waves.Add(new List<int> { center });

        for (int dist = 1; dist <= center; dist++)
        {
            List<int> wave = new();

            int left = center - dist;
            int right = center + dist;

            if (left >= 0)
                wave.Add(left);

            if (right < count)
                wave.Add(right);

            if (wave.Count > 0)
                waves.Add(wave);
        }

        return waves;
    }

    void SyncThisRectToLayoutRoot()
    {
        if (layoutRoot == null)
            return;

        RectTransform self = transform as RectTransform;
        if (self == null)
            return;

        if (self.parent != layoutRoot)
            self.SetParent(layoutRoot, false);

        self.anchorMin = Vector2.zero;
        self.anchorMax = Vector2.one;
        self.pivot = new Vector2(0.5f, 0.5f);
        self.offsetMin = Vector2.zero;
        self.offsetMax = Vector2.zero;
        self.anchoredPosition = Vector2.zero;
        self.localScale = Vector3.one;
        self.localRotation = Quaternion.identity;

        if (enableSurgicalLogs)
        {
            Debug.Log(
                $"{LOG} SyncThisRectToLayoutRoot | self={self.name} parent={self.parent.name} " +
                $"anchorMin={self.anchorMin} anchorMax={self.anchorMax} " +
                $"offsetMin={self.offsetMin} offsetMax={self.offsetMax} " +
                $"rect=({self.rect.width:0.###}x{self.rect.height:0.###})",
                this
            );
        }
    }

    public void CompleteImmediate()
    {
        EnsureLogo();
        EnsureCharacters();
        EnsureStateCaches();
        RebuildCachedBasePositionsIfPossible();

        for (int i = 0; i < characterSlots.Length; i++)
        {
            CharacterDropSlot slot = characterSlots[i];
            if (slot == null || slot.image == null)
                continue;

            _characterHasLanded[i] = true;
            slot.image.gameObject.SetActive(true);
            ApplyAnchoredPositionScaled(slot.image.rectTransform, _cachedCharacterFinalBasePositions[i]);
            ApplyCharacterSprite(i, true);
        }

        if (logoImage != null && _logoRect != null)
        {
            logoImage.gameObject.SetActive(true);
            ApplyLogoSprite();
            ApplyAnchoredPositionScaled(_logoRect, _cachedLogoFinalBasePosition);
        }

        _visualState = IntroVisualState.Completed;
        StopIntro();

        if (enableSurgicalLogs)
            Debug.Log($"{LOG} CompleteImmediate", this);

        DumpAllState("CompleteImmediate");
        DumpCharacterVisibilityState("CompleteImmediate");
    }

    void DumpAllState(string context)
    {
        if (!enableSurgicalLogs)
            return;

        string rootInfo = "layoutRoot=NULL";
        if (layoutRoot != null)
            rootInfo = $"layoutRoot={layoutRoot.name} rootRect=({layoutRoot.rect.width:0.###}x{layoutRoot.rect.height:0.###})";

        string logoInfo = "logo=NULL";
        if (_logoRect != null)
        {
            logoInfo =
                $"logoRect=({_logoRect.rect.width:0.###}x{_logoRect.rect.height:0.###}) " +
                $"logoSizeDelta=({_logoRect.sizeDelta.x:0.###}x{_logoRect.sizeDelta.y:0.###}) " +
                $"logoAnchored=({_logoRect.anchoredPosition.x:0.###},{_logoRect.anchoredPosition.y:0.###})";
        }

        Debug.Log(
            $"{LOG} {context} | pixelFrameScale={_pixelFrameScale:0.###} | visualState={_visualState} | skipped={Skipped} | {rootInfo} | {logoInfo} | characterCount={(characterSlots != null ? characterSlots.Length : 0)}",
            this
        );

        if (characterSlots == null)
            return;

        for (int i = 0; i < characterSlots.Length; i++)
        {
            CharacterDropSlot slot = characterSlots[i];
            if (slot == null || slot.image == null)
                continue;

            RectTransform rt = slot.image.rectTransform;
            Debug.Log(
                $"{LOG} Character[{i}] name={slot.name} baseSize={GetEffectiveBaseCharacterSize(slot)} offset={slot.positionOffset} " +
                $"rect=({rt.rect.width:0.###}x{rt.rect.height:0.###}) " +
                $"sizeDelta=({rt.sizeDelta.x:0.###}x{rt.sizeDelta.y:0.###}) " +
                $"anchored=({rt.anchoredPosition.x:0.###},{rt.anchoredPosition.y:0.###}) " +
                $"sibling={rt.GetSiblingIndex()}",
                this
            );
        }
    }

    void DumpCharacterVisibilityState(string context)
    {
        if (!enableSurgicalLogs || characterSlots == null)
            return;

        Canvas rootCanvas = GetComponentInParent<Canvas>();

        Debug.Log($"{LOG} {context} | rootCanvas={(rootCanvas != null ? rootCanvas.name : "NULL")} | selfActive={gameObject.activeInHierarchy}", this);

        for (int i = 0; i < characterSlots.Length; i++)
        {
            CharacterDropSlot slot = characterSlots[i];
            if (slot == null || slot.image == null)
                continue;

            RectTransform rt = slot.image.rectTransform;
            CanvasRenderer cr = slot.image.canvasRenderer;
            Transform parent = rt.parent;

            string spriteInfo = slot.image.sprite != null
                ? $"{slot.image.sprite.name} rect=({slot.image.sprite.rect.width}x{slot.image.sprite.rect.height}) tex=({slot.image.sprite.texture.width}x{slot.image.sprite.texture.height})"
                : "NULL";

            Debug.Log(
                $"{LOG} Visibility[{i}] " +
                $"name={slot.name} " +
                $"activeSelf={slot.image.gameObject.activeSelf} " +
                $"activeInHierarchy={slot.image.gameObject.activeInHierarchy} " +
                $"enabled={slot.image.enabled} " +
                $"color={slot.image.color} " +
                $"alpha={slot.image.color.a:0.###} " +
                $"cull={cr.cull} " +
                $"depth={cr.absoluteDepth} " +
                $"sprite={spriteInfo} " +
                $"offset={slot.positionOffset} " +
                $"parent={(parent != null ? parent.name : "NULL")} " +
                $"sibling={rt.GetSiblingIndex()} " +
                $"anchored={rt.anchoredPosition} " +
                $"sizeDelta={rt.sizeDelta} " +
                $"scale={rt.lossyScale}",
                this
            );
        }

        if (logoImage != null)
        {
            CanvasRenderer cr = logoImage.canvasRenderer;
            Debug.Log(
                $"{LOG} LogoVisibility " +
                $"activeSelf={logoImage.gameObject.activeSelf} " +
                $"activeInHierarchy={logoImage.gameObject.activeInHierarchy} " +
                $"enabled={logoImage.enabled} " +
                $"alpha={logoImage.color.a:0.###} " +
                $"cull={cr.cull} " +
                $"depth={cr.absoluteDepth} " +
                $"parent={(logoImage.transform.parent != null ? logoImage.transform.parent.name : "NULL")} " +
                $"sibling={logoImage.rectTransform.GetSiblingIndex()}",
                this
            );
        }
    }
}