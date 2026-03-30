using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class StageBlackout : MonoBehaviour
{
    public static StageBlackout Instance { get; private set; }

    private const int ShaderMaxSpotlights = 128;

    [Header("UI")]
    [SerializeField] private Image blackoutImage;

    [Header("Target Stage")]
    [SerializeField] private bool onlyForWorldStage = true;
    [SerializeField] private int targetWorld = 2;
    [SerializeField] private int targetStage = 5;

    [Header("Blackout")]
    [Range(0f, 1f)]
    [SerializeField] private float blackoutAlpha = 0.92f;
    [SerializeField, Min(0f)] private float fadeInSeconds = 0.25f;

    [Header("Explosion Spotlight")]
    [SerializeField, Min(0.01f)] private float tileWorldSize = 1f;
    [SerializeField, Min(0f)] private float extraTilesAroundExplosion = 1f;
    [SerializeField, Min(0f)] private float explosionSpotlightSoftness = 0.01f;
    [SerializeField, Min(1)] private int maxExplosionSpotlights = 128;

    private static readonly int IdEllipseX = Shader.PropertyToID("_EllipseX");
    private static readonly int IdEllipseY = Shader.PropertyToID("_EllipseY");
    private static readonly int IdCenter = Shader.PropertyToID("_Center");
    private static readonly int IdRadius = Shader.PropertyToID("_Radius");
    private static readonly int IdSoftness = Shader.PropertyToID("_Softness");
    private static readonly int IdColor = Shader.PropertyToID("_Color");

    private static readonly int IdSpotlightCount = Shader.PropertyToID("_SpotlightCount");
    private static readonly int IdSpotlightCenters = Shader.PropertyToID("_SpotlightCenters");
    private static readonly int IdSpotlightHalfSize = Shader.PropertyToID("_SpotlightHalfSize");
    private static readonly int IdSpotlightSoftness = Shader.PropertyToID("_SpotlightSoftness");
    private static readonly int IdSpotlightIntensity = Shader.PropertyToID("_SpotlightIntensity");

    // ─── PERF: Transform is passed directly at registration — no FindObjectsByType ───
    private sealed class ExplosionSpotlightData
    {
        public int Id;
        public Transform Transform;
        public Vector2 LastKnownWorldPosition;
        public float Intensity;
    }

    Material _originalMat;
    Material _matInstance;
    float _currentA;
    float _targetA;
    bool _active;

    readonly Dictionary<int, ExplosionSpotlightData> _activeExplosionSpotlights = new();

    Vector4[] _spotlightCentersCache;
    Vector4[] _spotlightHalfSizeCache;
    float[] _spotlightSoftnessCache;
    float[] _spotlightIntensityCache;

    RectTransform _blackoutRect;
    Canvas _canvas;
    Camera _uiCamera;

    // ─── PERF: dirty flag — shader arrays are only uploaded when something changed ───
    bool _spotlightsDirty;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (!blackoutImage)
        {
            enabled = false;
            return;
        }

        maxExplosionSpotlights = Mathf.Clamp(maxExplosionSpotlights, 1, ShaderMaxSpotlights);

        _blackoutRect = blackoutImage.rectTransform;
        _canvas = blackoutImage.canvas;

        if (_canvas != null)
        {
            _uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _canvas.worldCamera;
        }

        _originalMat = blackoutImage.material;

        // ─── PERF: material is created once and reused forever ───
        if (_originalMat != null)
        {
            _matInstance = Instantiate(_originalMat);
            blackoutImage.material = _matInstance;
        }

        blackoutImage.raycastTarget = false;
        blackoutImage.gameObject.SetActive(false);

        _currentA = 0f;
        _targetA = Mathf.Clamp01(blackoutAlpha);

        _spotlightCentersCache = new Vector4[ShaderMaxSpotlights];
        _spotlightHalfSizeCache = new Vector4[ShaderMaxSpotlights];
        _spotlightSoftnessCache = new float[ShaderMaxSpotlights];
        _spotlightIntensityCache = new float[ShaderMaxSpotlights];

        if (_matInstance != null)
            ApplyFullBlackout(0f);

        _active = false;
        _spotlightsDirty = false;
    }

    void OnValidate()
    {
        maxExplosionSpotlights = Mathf.Clamp(maxExplosionSpotlights, 1, ShaderMaxSpotlights);
    }

    void Start()
    {
        if (onlyForWorldStage && !IsTargetStage())
        {
            if (blackoutImage != null)
                blackoutImage.gameObject.SetActive(false);

            enabled = false;
            return;
        }

        SetBlackoutActive(true);
    }

    void Update()
    {
        if (!_active || _matInstance == null)
            return;

        // ─── Alpha fade ───
        if (fadeInSeconds > 0f && _currentA < _targetA)
        {
            _currentA = Mathf.MoveTowards(
                _currentA,
                _targetA,
                Time.unscaledDeltaTime / Mathf.Max(0.001f, fadeInSeconds));

            ApplyFullBlackout(_currentA);
        }

        // ─── PERF: track live transforms every frame (cheap), but only upload to
        //           the shader when something actually changed (_spotlightsDirty). ───
        UpdateTrackedPositions();

        if (_spotlightsDirty)
        {
            FlushSpotlightsToShader();
            _spotlightsDirty = false;
        }
    }

    // ─────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────

    public void SetBlackoutActive(bool active)
    {
        if (blackoutImage == null)
            return;

        if (onlyForWorldStage && !IsTargetStage())
        {
            blackoutImage.gameObject.SetActive(false);
            _active = false;
            return;
        }

        if (active)
        {
            // ─── PERF: never recreate _matInstance if it already exists ───
            if (_matInstance == null && _originalMat != null)
                _matInstance = Instantiate(_originalMat);

            blackoutImage.material = _matInstance;
            blackoutImage.gameObject.SetActive(true);
            _active = true;
            _targetA = Mathf.Clamp01(blackoutAlpha);

            if (fadeInSeconds <= 0f)
            {
                _currentA = _targetA;
                ApplyFullBlackout(_currentA);
            }
            else
            {
                _currentA = 0f;
                ApplyFullBlackout(0f);
            }

            _spotlightsDirty = true;
            return;
        }

        _active = false;
        _currentA = 0f;
        _activeExplosionSpotlights.Clear();

        if (_matInstance != null)
        {
            ApplyFullBlackout(0f);
            ClearExplosionSpotlights();
        }

        // ─── PERF: keep _matInstance alive — only unassign from the image ───
        blackoutImage.material = null;
        blackoutImage.gameObject.SetActive(false);
    }

    // ─── PERF: caller passes the Transform directly — no scene search needed ───
    public void RegisterExplosionSpotlight(int id, Transform explosionTransform, Vector2 worldPosition)
    {
        if (!_active || _matInstance == null)
            return;

        _activeExplosionSpotlights[id] = new ExplosionSpotlightData
        {
            Id = id,
            Transform = explosionTransform,
            LastKnownWorldPosition = worldPosition,
            Intensity = 0f
        };

        _spotlightsDirty = true;
    }

    // ─── Legacy overload (kept for backward compatibility) ───
    public void RegisterExplosionSpotlight(int id, Vector2 worldPosition)
        => RegisterExplosionSpotlight(id, null, worldPosition);

    public void UpdateExplosionSpotlight(int id, float intensity)
    {
        if (!_activeExplosionSpotlights.TryGetValue(id, out var data))
            return;

        float clamped = Mathf.Clamp01(intensity);
        if (Mathf.Approximately(data.Intensity, clamped))
            return;                 // ─── PERF: skip if unchanged

        data.Intensity = clamped;
        _spotlightsDirty = true;
    }

    public void UnregisterExplosionSpotlight(int id)
    {
        if (_activeExplosionSpotlights.Remove(id))
            _spotlightsDirty = true;
    }

    // ─────────────────────────────────────────────────────────
    //  Internal helpers
    // ─────────────────────────────────────────────────────────

    // ─── PERF: called every Update() — only reads Transform.position, no alloc ───
    void UpdateTrackedPositions()
    {
        foreach (var kv in _activeExplosionSpotlights)
        {
            var data = kv.Value;
            if (data.Transform == null)
                continue;

            Vector2 current = data.Transform.position;
            if (current == data.LastKnownWorldPosition)
                continue;

            data.LastKnownWorldPosition = current;
            _spotlightsDirty = true;
        }
    }

    // ─── PERF: separated from Update — only runs when _spotlightsDirty is true ───
    void FlushSpotlightsToShader()
    {
        if (_matInstance == null)
            return;

        int count = 0;

        foreach (var kv in _activeExplosionSpotlights)
        {
            if (count >= maxExplosionSpotlights)
                break;

            var data = kv.Value;
            Vector2 worldPos = data.LastKnownWorldPosition;
            Vector2 uv = WorldToBlackoutUV(worldPos);
            Vector2 halfSize = GetExplosionHalfSizeInBlackoutUV(worldPos);

            _spotlightCentersCache[count] = new Vector4(uv.x, uv.y, 0f, 0f);
            _spotlightHalfSizeCache[count] = new Vector4(halfSize.x, halfSize.y, 0f, 0f);
            _spotlightSoftnessCache[count] = explosionSpotlightSoftness;
            _spotlightIntensityCache[count] = data.Intensity;
            count++;
        }

        _matInstance.SetInt(IdSpotlightCount, count);
        _matInstance.SetVectorArray(IdSpotlightCenters, _spotlightCentersCache);
        _matInstance.SetVectorArray(IdSpotlightHalfSize, _spotlightHalfSizeCache);
        _matInstance.SetFloatArray(IdSpotlightSoftness, _spotlightSoftnessCache);
        _matInstance.SetFloatArray(IdSpotlightIntensity, _spotlightIntensityCache);
    }

    void ClearExplosionSpotlights()
    {
        if (_matInstance == null)
            return;

        _matInstance.SetInt(IdSpotlightCount, 0);
    }

    bool IsTargetStage()
    {
        if (StageIntroTransition.Instance != null)
        {
            return StageIntroTransition.Instance.world == targetWorld &&
                   StageIntroTransition.Instance.stageNumber == targetStage;
        }

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return false;

        string n = scene.name;
        return n.Contains("2-5") || n.Contains("2_5");
    }

    Vector2 WorldToBlackoutUV(Vector2 worldPos)
    {
        Camera worldCamera = Camera.main;
        if (worldCamera == null || _blackoutRect == null)
            return new Vector2(0.5f, 0.5f);

        Vector3 screen = worldCamera.WorldToScreenPoint(worldPos);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _blackoutRect,
                screen,
                _uiCamera,
                out Vector2 localPoint))
        {
            return new Vector2(0.5f, 0.5f);
        }

        Rect rect = _blackoutRect.rect;

        float u = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        float v = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);

        return new Vector2(u, v);
    }

    Vector2 GetBlackoutUvDeltaForOneTile(Vector2 worldCenter)
    {
        Vector2 uvCenter = WorldToBlackoutUV(worldCenter);
        Vector2 uvRight = WorldToBlackoutUV(worldCenter + new Vector2(tileWorldSize, 0f));
        Vector2 uvUp = WorldToBlackoutUV(worldCenter + new Vector2(0f, tileWorldSize));

        return new Vector2(
            Mathf.Abs(uvRight.x - uvCenter.x),
            Mathf.Abs(uvUp.y - uvCenter.y));
    }

    Vector2 GetExplosionHalfSizeInBlackoutUV(Vector2 worldCenter)
    {
        Vector2 oneTileUv = GetBlackoutUvDeltaForOneTile(worldCenter);
        float tilesHalfExt = 0.5f + extraTilesAroundExplosion;

        return new Vector2(
            oneTileUv.x * tilesHalfExt,
            oneTileUv.y * tilesHalfExt);
    }

    void ApplyFullBlackout(float a)
    {
        if (_matInstance == null)
            return;

        _matInstance.SetFloat(IdEllipseX, 1f);
        _matInstance.SetFloat(IdEllipseY, 1f);
        _matInstance.SetVector(IdCenter, new Vector4(-10f, -10f, 0f, 0f));
        _matInstance.SetFloat(IdRadius, 0.001f);
        _matInstance.SetFloat(IdSoftness, 0.001f);
        _matInstance.SetColor(IdColor, new Color(0f, 0f, 0f, Mathf.Clamp01(a)));
    }
}