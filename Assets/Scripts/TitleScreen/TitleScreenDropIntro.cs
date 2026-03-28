using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TitleScreenDropIntro : MonoBehaviour
{
    [Serializable]
    public class CharacterDropSlot
    {
        public string name;
        public Image image;

        [Header("Sprites")]
        public Sprite sprite;
        public Sprite fallingSprite;
        public Sprite landedSprite;

        [Header("Idle Alternate Sprite")]
        public Sprite alternateSprite;

        [Header("Idle Alternate Timing")]
        [Min(0.1f)] public float idleMinInterval = 2.0f;
        [Min(0.1f)] public float idleMaxInterval = 4.0f;
        [Min(0.05f)] public float alternateDuration = 0.5f;
        [Min(0f)] public float idleStartDelay = 0f;

        [Header("Layout")]
        public Vector2 baseSize = new(80f, 80f);
        public bool useSpriteNativeSizeAsCharacterSize = false;
        public Vector2 positionOffset = Vector2.zero;

        [Header("Idle Alternate Offset")]
        public Vector2 alternatePositionOffset = Vector2.zero;

        [Header("Idle Alternate Size Offset")]
        public Vector2 alternateSizeOffset = Vector2.zero;
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

    [Header("Logo Frames")]
    [SerializeField] Sprite[] logoFrames = Array.Empty<Sprite>();
    [SerializeField, Min(0.01f)] float logoFrameDuration = 0.08f;
    [SerializeField] bool animateLogo = true;
    [SerializeField] bool loopLogoAnimation = true;
    [SerializeField] bool resetLogoAnimationWhenShown = true;

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

    Coroutine currentRoutine;

    readonly List<Coroutine> _idleRoutines = new();

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

    int _currentLogoFrameIndex;
    float _logoAnimTimer;
    bool[] _characterUsingAlternatePose = Array.Empty<bool>();

    public bool IsPlaying => currentRoutine != null;
    public bool Running => currentRoutine != null;
    public bool Skipped { get; private set; }

    public void SetPixelFrameScale(float scale)
    {
        _pixelFrameScale = Mathf.Max(0.01f, scale);
        RebuildCachedBasePositionsIfPossible();
        ReapplyCurrentResolvedLayout();
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

        ResetLogoAnimation();
        HideImmediate();
    }

    void Update()
    {
        TickLogoAnimation();
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
    }

    public void HideImmediate()
    {
        StopIntro();
        StopAllIdleRoutines();

        Skipped = false;
        skipRequested = false;
        _visualState = IntroVisualState.Hidden;

        ResetCharacterLandedState();
        ResetLogoAnimation();
        RebuildCachedBasePositionsIfPossible();
        ReapplyCurrentResolvedLayout();
    }

    public void PrepareAboveTop()
    {
        EnsureLogo();
        EnsureCharacters();
        EnsureStateCaches();

        Skipped = false;
        skipRequested = false;

        ResetLogoAnimation();
        ApplyLogoSprite();
        ApplyCharacterSprites(false);
        ResetCharacterLandedState();
        StopAllIdleRoutines();

        RebuildCachedBasePositionsIfPossible();
        UpdateCharacterSiblingOrder();

        _visualState = IntroVisualState.CharactersAboveTop;
        ReapplyCurrentResolvedLayout();
    }

    public void Skip()
    {
        if (!Running)
            return;

        skipRequested = true;
        Skipped = true;
    }

    public IEnumerator PlayIntro()
    {
        StopIntro();
        StopAllIdleRoutines();

        EnsureLogo();
        EnsureCharacters();
        EnsureStateCaches();

        Skipped = false;
        skipRequested = false;

        ResetLogoAnimation();
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

    void StopAllIdleRoutines()
    {
        for (int i = 0; i < _idleRoutines.Count; i++)
        {
            if (_idleRoutines[i] != null)
                StopCoroutine(_idleRoutines[i]);
        }
        _idleRoutines.Clear();
    }

    void StartIdleRoutinesForAllLanded()
    {
        StopAllIdleRoutines();

        if (characterSlots == null)
            return;

        for (int i = 0; i < characterSlots.Length; i++)
        {
            CharacterDropSlot slot = characterSlots[i];
            if (slot == null || slot.image == null)
            {
                _idleRoutines.Add(null);
                continue;
            }

            if (slot.alternateSprite == null)
            {
                _idleRoutines.Add(null);
                continue;
            }

            Coroutine c = StartCoroutine(IdleAlternateRoutine(i, slot));
            _idleRoutines.Add(c);
        }
    }

    void StartIdleRoutineForCharacter(int index)
    {
        if (characterSlots == null || index < 0 || index >= characterSlots.Length)
            return;

        CharacterDropSlot slot = characterSlots[index];
        if (slot == null || slot.image == null || slot.alternateSprite == null)
            return;

        while (_idleRoutines.Count <= index)
            _idleRoutines.Add(null);

        if (_idleRoutines[index] != null)
            StopCoroutine(_idleRoutines[index]);

        _idleRoutines[index] = StartCoroutine(IdleAlternateRoutine(index, slot));
    }

    IEnumerator IdleAlternateRoutine(int index, CharacterDropSlot slot)
    {
        float startDelay = slot.idleStartDelay > 0f
            ? slot.idleStartDelay
            : UnityEngine.Random.Range(0f, Mathf.Max(0f, slot.idleMaxInterval));

        if (startDelay > 0f)
        {
            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(startDelay);
            else
                yield return new WaitForSeconds(startDelay);
        }

        while (true)
        {
            ApplyCharacterSpriteOverride(index, slot.alternateSprite);

            bool isBlackLouie =
                slot != null &&
                !string.IsNullOrWhiteSpace(slot.name) &&
                string.Equals(slot.name.Trim(), "Black", StringComparison.OrdinalIgnoreCase);

            bool isPinkLouie =
                slot != null &&
                !string.IsNullOrWhiteSpace(slot.name) &&
                string.Equals(slot.name.Trim(), "Pink", StringComparison.OrdinalIgnoreCase);

            Vector2 originalOffset = slot.alternatePositionOffset;

            if (isBlackLouie)
            {
                float totalDuration = Mathf.Max(0.05f, slot.alternateDuration);
                float stepTime = 0.1f;

                float elapsed = 0f;
                int stepsApplied = 0;

                while (elapsed < totalDuration)
                {
                    float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    elapsed += dt;

                    int targetSteps = Mathf.FloorToInt(elapsed / stepTime);

                    if (targetSteps > stepsApplied)
                    {
                        stepsApplied = targetSteps;
                        slot.alternatePositionOffset = originalOffset + new Vector2(-stepsApplied, 0f);
                        ApplyCharacterPoseLayout(index);
                    }

                    yield return null;
                }

                slot.alternatePositionOffset = originalOffset;
            }
            else if (isPinkLouie)
            {
                float stepTime = 0.1f;

                float elapsedUp = 0f;
                int upStepsApplied = 0;

                while (elapsedUp < 0.5f)
                {
                    float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    elapsedUp += dt;

                    int targetSteps = Mathf.FloorToInt(elapsedUp / stepTime);

                    if (targetSteps > upStepsApplied)
                    {
                        upStepsApplied = Mathf.Min(targetSteps, 5);
                        slot.alternatePositionOffset = originalOffset + new Vector2(0f, upStepsApplied);
                        ApplyCharacterPoseLayout(index);
                    }

                    yield return null;
                }

                float elapsedDown = 0f;
                int downStepsApplied = 0;

                while (elapsedDown < 0.5f)
                {
                    float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    elapsedDown += dt;

                    int targetSteps = Mathf.FloorToInt(elapsedDown / stepTime);

                    if (targetSteps > downStepsApplied)
                    {
                        downStepsApplied = Mathf.Min(targetSteps, 5);
                        slot.alternatePositionOffset = originalOffset + new Vector2(0f, 5 - downStepsApplied);
                        ApplyCharacterPoseLayout(index);
                    }

                    yield return null;
                }

                slot.alternatePositionOffset = originalOffset;
            }
            else
            {
                float altDur = Mathf.Max(0.05f, slot.alternateDuration);

                if (useUnscaledTime)
                    yield return new WaitForSecondsRealtime(altDur);
                else
                    yield return new WaitForSeconds(altDur);
            }

            ApplyCharacterSprite(index, true);

            float interval = UnityEngine.Random.Range(
                Mathf.Max(0.1f, slot.idleMinInterval),
                Mathf.Max(0.1f, slot.idleMaxInterval));

            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(interval);
            else
                yield return new WaitForSeconds(interval);
        }
    }

    void ApplyCharacterSpriteOverride(int index, Sprite overrideSprite)
    {
        if (characterSlots == null || index < 0 || index >= characterSlots.Length)
            return;

        CharacterDropSlot slot = characterSlots[index];
        if (slot == null || slot.image == null || overrideSprite == null)
            return;

        Texture tex = overrideSprite.texture;
        if (tex != null && applyPointFilter)
            tex.filterMode = FilterMode.Point;

        if (slot.image.sprite != overrideSprite)
            slot.image.sprite = overrideSprite;

        if (_characterUsingAlternatePose != null && index < _characterUsingAlternatePose.Length)
            _characterUsingAlternatePose[index] = true;

        ApplyCharacterPoseLayout(index);
    }

    IEnumerator PlayMasterRoutine()
    {
        ResetCharacterLandedState();
        ResetLogoAnimation();
        RebuildCachedBasePositionsIfPossible();
        UpdateCharacterSiblingOrder();

        _visualState = IntroVisualState.CharactersAboveTop;
        ReapplyCurrentResolvedLayout();

        if (HandleSkipIfRequested())
            yield break;

        yield return PlayCharactersRoutine();

        if (HandleSkipIfRequested())
            yield break;

        if (delayAfterCharactersBeforeLogo > 0f)
        {
            yield return WaitWithSkip(delayAfterCharactersBeforeLogo);

            if (HandleSkipIfRequested())
                yield break;
        }

        PrepareLogoAboveTop(true);

        if (HandleSkipIfRequested())
            yield break;

        yield return PlayLogoRoutine();

        if (HandleSkipIfRequested())
            yield break;

        _visualState = IntroVisualState.Completed;
        ReapplyCurrentResolvedLayout();

        StartIdleRoutinesForAllLanded();

        currentRoutine = null;
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
            if (HandleSkipIfRequested())
                yield break;

            List<int> wave = waveOrder[waveIndex];
            float t = 0f;

            PlayCharacterDropStartSfx();

            while (t < duration)
            {
                if (PollSkipPressed())
                    Skip();

                if (HandleSkipIfRequested())
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

                StartIdleRoutineForCharacter(i);
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
    }

    IEnumerator PlayLogoRoutine()
    {
        if (_logoRect == null || logoImage == null)
            yield break;

        if (resetLogoAnimationWhenShown)
            ResetLogoAnimation();

        float duration = Mathf.Max(0.01f, dropDuration);
        float t = 0f;

        Vector2 startBase = _cachedLogoAboveTopBasePosition;
        Vector2 endBase = _cachedLogoFinalBasePosition;

        while (t < duration)
        {
            if (PollSkipPressed())
                Skip();

            if (HandleSkipIfRequested())
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
    }

    void TickLogoAnimation()
    {
        if (!animateLogo)
            return;

        if (logoImage == null || !logoImage.gameObject.activeInHierarchy)
            return;

        int frameCount = GetLogoFrameCount();
        if (frameCount <= 1)
            return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f)
            return;

        _logoAnimTimer += dt;
        float frameDuration = Mathf.Max(0.01f, logoFrameDuration);

        while (_logoAnimTimer >= frameDuration)
        {
            _logoAnimTimer -= frameDuration;

            if (loopLogoAnimation)
            {
                _currentLogoFrameIndex = (_currentLogoFrameIndex + 1) % frameCount;
            }
            else
            {
                if (_currentLogoFrameIndex < frameCount - 1)
                    _currentLogoFrameIndex++;
            }

            ApplyLogoSprite();
        }
    }

    void ResetLogoAnimation()
    {
        _currentLogoFrameIndex = 0;
        _logoAnimTimer = 0f;
        ApplyLogoSprite();
    }

    int GetLogoFrameCount()
    {
        return logoFrames != null ? logoFrames.Length : 0;
    }

    Sprite GetCurrentLogoSprite()
    {
        int count = GetLogoFrameCount();
        if (count <= 0)
            return null;

        int index = Mathf.Clamp(_currentLogoFrameIndex, 0, count - 1);
        return logoFrames[index];
    }

    bool PollSkipPressed()
    {
        var input = PlayerInputManager.Instance;
        if (input == null)
            return false;

        return input.AnyGetDown(PlayerAction.Start) || input.AnyGetDown(PlayerAction.ActionA);
    }

    bool HandleSkipIfRequested()
    {
        if (PollSkipPressed())
            Skip();

        if (!skipRequested)
            return false;

        CompleteImmediateToEndState();
        return true;
    }

    void CompleteImmediateToEndState()
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
            if (resetLogoAnimationWhenShown)
                ResetLogoAnimation();

            logoImage.gameObject.SetActive(true);
            ApplyLogoSprite();
            ApplyAnchoredPositionScaled(_logoRect, _cachedLogoFinalBasePosition);
        }

        _visualState = IntroVisualState.Completed;
        currentRoutine = null;
        skipRequested = false;

        StartIdleRoutinesForAllLanded();
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
        if (logoImage == null)
            return;

        Sprite sprite = GetCurrentLogoSprite();
        if (sprite == null)
            return;

        Texture tex = sprite.texture;
        if (tex != null && applyPointFilter)
            tex.filterMode = FilterMode.Point;

        if (logoImage.sprite != sprite)
            logoImage.sprite = sprite;
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

        if (_characterUsingAlternatePose != null && index < _characterUsingAlternatePose.Length)
            _characterUsingAlternatePose[index] = false;

        if (_characterHasLanded != null &&
            index < _characterHasLanded.Length &&
            _characterHasLanded[index])
        {
            ApplyCharacterPoseLayout(index);
        }
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

        if (_characterUsingAlternatePose == null || _characterUsingAlternatePose.Length != count)
            _characterUsingAlternatePose = new bool[count];
    }

    void ResetCharacterLandedState()
    {
        EnsureStateCaches();

        for (int i = 0; i < _characterHasLanded.Length; i++)
            _characterHasLanded[i] = false;

        for (int i = 0; i < _characterUsingAlternatePose.Length; i++)
            _characterUsingAlternatePose[i] = false;
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
            ApplyLogoSprite();
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

            bool landed =
                _characterHasLanded != null &&
                i < _characterHasLanded.Length &&
                _characterHasLanded[i];

            if (landed)
            {
                ApplyCharacterPoseLayout(i);
            }
            else
            {
                RectTransform rt = slot.image.rectTransform;
                Vector2 baseSize = GetEffectiveBaseCharacterSize(slot);
                rt.sizeDelta = RoundVec(baseSize * _pixelFrameScale);
            }
        }
    }

    void PrepareLogoAboveTop(bool visible)
    {
        if (logoImage == null || _logoRect == null)
            return;

        if (visible && resetLogoAnimationWhenShown)
            ResetLogoAnimation();

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
    }

    Vector2 GetEffectiveBaseLogoSize()
    {
        Sprite sizeSprite = GetCurrentLogoSprite();

        if (useSpriteNativeSizeAsLogoSize && sizeSprite != null)
        {
            if (sizeSprite.texture != null)
                return new Vector2(sizeSprite.texture.width, sizeSprite.texture.height);

            Rect r = sizeSprite.rect;
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
            if (resetLogoAnimationWhenShown)
                ResetLogoAnimation();

            logoImage.gameObject.SetActive(true);
            ApplyLogoSprite();
            ApplyAnchoredPositionScaled(_logoRect, _cachedLogoFinalBasePosition);
        }

        _visualState = IntroVisualState.Completed;
        StopIntro();

        StartIdleRoutinesForAllLanded();
    }

    void ApplyCharacterPoseLayout(int index)
    {
        if (characterSlots == null || index < 0 || index >= characterSlots.Length)
            return;

        CharacterDropSlot slot = characterSlots[index];
        if (slot == null || slot.image == null)
            return;

        RectTransform rt = slot.image.rectTransform;

        bool usingAlternate =
            _characterUsingAlternatePose != null &&
            index < _characterUsingAlternatePose.Length &&
            _characterUsingAlternatePose[index];

        Vector2 basePos = _cachedCharacterFinalBasePositions[index];
        Vector2 baseSize = GetEffectiveBaseCharacterSize(slot);

        if (usingAlternate)
        {
            basePos += slot.alternatePositionOffset;
            baseSize += slot.alternateSizeOffset;
        }

        ApplyAnchoredPositionScaled(rt, basePos);
        rt.sizeDelta = RoundVec(baseSize * _pixelFrameScale);
    }
}