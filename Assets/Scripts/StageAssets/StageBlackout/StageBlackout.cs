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

    [SerializeField, Min(0f)]
    private float fadeInSeconds = 0.25f;

    Material _originalMat;
    Material _matInstance;
    float _currentA;
    float _targetA;
    bool _active;

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
        if (!_active) return;
        if (_matInstance == null) return;
        if (fadeInSeconds <= 0f) return;
        if (_currentA >= _targetA) return;

        _currentA = Mathf.MoveTowards(_currentA, _targetA, Time.unscaledDeltaTime / Mathf.Max(0.001f, fadeInSeconds));
        ApplyFullBlackout(_currentA);
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

            return;
        }

        _active = false;
        _currentA = 0f;

        if (_matInstance != null)
            ApplyFullBlackout(0f);

        blackoutImage.material = null;
        blackoutImage.gameObject.SetActive(false);
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
        if (_matInstance == null) return;

        _matInstance.SetFloat("_EllipseX", 1f);
        _matInstance.SetFloat("_EllipseY", 1f);

        _matInstance.SetVector("_Center", new Vector4(-10f, -10f, 0f, 0f));
        _matInstance.SetFloat("_Radius", 0.001f);
        _matInstance.SetFloat("_Softness", 0.001f);

        _matInstance.SetColor("_Color", new Color(0f, 0f, 0f, Mathf.Clamp01(a)));
    }
}