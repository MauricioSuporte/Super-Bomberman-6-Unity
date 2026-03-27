using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class StageBlackout : MonoBehaviour
{
    public static StageBlackout Instance { get; private set; }

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
    [SerializeField, Min(1)] private int maxExplosionSpotlights = 16;

    [Header("Debug")]
    [SerializeField] private bool enableSurgicalLogs = true;

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

        _blackoutRect = blackoutImage.rectTransform;
        _canvas = blackoutImage.canvas;

        if (_canvas != null)
        {
            _uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _canvas.worldCamera;
        }

        _originalMat = blackoutImage.material;

        if (_originalMat != null)
        {
            _matInstance = Instantiate(_originalMat);
            blackoutImage.material = _matInstance;
        }

        blackoutImage.raycastTarget = false;
        blackoutImage.gameObject.SetActive(false);

        _currentA = 0f;
        _targetA = Mathf.Clamp01(blackoutAlpha);

        int cacheSize = Mathf.Max(1, maxExplosionSpotlights);
        _spotlightCentersCache = new Vector4[cacheSize];
        _spotlightHalfSizeCache = new Vector4[cacheSize];
        _spotlightSoftnessCache = new float[cacheSize];
        _spotlightIntensityCache = new float[cacheSize];

        if (_matInstance != null)
            ApplyFullBlackout(0f);

        _active = false;
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

        if (fadeInSeconds > 0f && _currentA < _targetA)
        {
            _currentA = Mathf.MoveTowards(
                _currentA,
                _targetA,
                Time.unscaledDeltaTime / Mathf.Max(0.001f, fadeInSeconds));

            ApplyFullBlackout(_currentA);
        }

        ApplyExplosionSpotlights();
    }

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

            ApplyExplosionSpotlights();
            LogSurgical($"Blackout ativado. alpha={_targetA:F3}");
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

        blackoutImage.material = null;
        blackoutImage.gameObject.SetActive(false);

        LogSurgical("Blackout desativado.");
    }

    public void RegisterExplosionSpotlight(int id, Vector2 worldPosition)
    {
        if (!_active || _matInstance == null)
            return;

        _activeExplosionSpotlights[id] = new ExplosionSpotlightData
        {
            Id = id,
            Transform = FindExplosionTransformByInstanceId(id),
            LastKnownWorldPosition = worldPosition,
            Intensity = 0f
        };

        if (enableSurgicalLogs)
        {
            Vector2 uv = WorldToBlackoutUV(worldPosition);
            Vector2 oneTileUv = GetBlackoutUvDeltaForOneTile(worldPosition);
            Vector2 halfSize = GetExplosionHalfSizeInBlackoutUV(worldPosition);

            LogSurgical(
                $"RegisterExplosionSpotlight -> id={id}, " +
                $"world=({worldPosition.x:F3}, {worldPosition.y:F3}), " +
                $"uv=({uv.x:F4}, {uv.y:F4}), " +
                $"oneTileUv=({oneTileUv.x:F4}, {oneTileUv.y:F4}), " +
                $"halfSize=({halfSize.x:F4}, {halfSize.y:F4}), " +
                $"extraTiles={extraTilesAroundExplosion:F2}, " +
                $"activeCount={_activeExplosionSpotlights.Count}");
        }

        ApplyExplosionSpotlights();
    }

    public void UpdateExplosionSpotlight(int id, float intensity)
    {
        if (_activeExplosionSpotlights.TryGetValue(id, out var data))
            data.Intensity = Mathf.Clamp01(intensity);
    }

    public void UnregisterExplosionSpotlight(int id)
    {
        if (_activeExplosionSpotlights.Remove(id))
        {
            LogSurgical($"UnregisterExplosionSpotlight -> id={id}, remaining={_activeExplosionSpotlights.Count}");
            ApplyExplosionSpotlights();
        }
    }

    void LogSurgical(string message)
    {
        if (!enableSurgicalLogs)
            return;

        Debug.Log($"[StageBlackout][{gameObject.name}] {message}", this);
    }

    Transform FindExplosionTransformByInstanceId(int instanceId)
    {
        BombExplosion[] explosions = FindObjectsByType<BombExplosion>(FindObjectsSortMode.None);
        for (int i = 0; i < explosions.Length; i++)
        {
            if (explosions[i] != null && explosions[i].GetInstanceID() == instanceId)
                return explosions[i].transform;
        }

        return null;
    }

    Vector2 GetTrackedWorldPosition(ExplosionSpotlightData data)
    {
        if (data != null && data.Transform != null)
            data.LastKnownWorldPosition = data.Transform.position;

        return data != null ? data.LastKnownWorldPosition : Vector2.zero;
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
        float tilesHalfExtent = 0.5f + extraTilesAroundExplosion;

        return new Vector2(
            oneTileUv.x * tilesHalfExtent,
            oneTileUv.y * tilesHalfExtent);
    }

    void ApplyExplosionSpotlights()
    {
        if (_matInstance == null)
            return;

        int count = 0;

        foreach (var kv in _activeExplosionSpotlights)
        {
            if (count >= maxExplosionSpotlights)
                break;

            ExplosionSpotlightData data = kv.Value;
            Vector2 worldPos = GetTrackedWorldPosition(data);
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