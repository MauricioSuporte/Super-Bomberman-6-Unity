using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Draws a blue atmospheric fade at the top and bottom of the Stage 3-3 camera.
/// The overlay is fitted to the same final pixel-perfect viewport as the
/// gameplay camera, leaving letterbox bars and HUD elements untouched.
/// </summary>
public sealed class BlueCameraEdgeOverlay : MonoBehaviour
{
    const string OverlayName = "BlueCameraEdgeOverlay";
    const string UnderwaterStageSceneName = "Stage_3-3";
    const float EdgeCoverage = 0.34f;
    const float EdgeOpacity = 0.48f;
    const float PulseHalfCycleSeconds = 3f;
    const float ReferenceScreenHeight = 224f;
    const float NormalGameHudHeight = 23f;
    const float BattleModeHudHeight = 27f;

    static BlueCameraEdgeOverlay instance;

    Canvas canvas;
    CanvasGroup canvasGroup;
    RectTransform safeFrame;
    RectTransform topGradient;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject overlay = new GameObject(OverlayName, typeof(RectTransform), typeof(BlueCameraEdgeOverlay));
        instance = overlay.GetComponent<BlueCameraEdgeOverlay>();
        DontDestroyOnLoad(overlay);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        CreateOverlay();
        RefreshVisibility(SceneManager.GetActiveScene());
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (instance == this)
            instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        RefreshVisibility(scene);
    }

    void CreateOverlay()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = -1000;
        canvasGroup = gameObject.AddComponent<CanvasGroup>();

        GameObject frame = new GameObject("PixelPerfectViewport", typeof(RectTransform), typeof(UICameraViewportFitter));
        safeFrame = frame.GetComponent<RectTransform>();
        safeFrame.SetParent(transform, false);

        topGradient = CreateGradient("Top", true);
        CreateGradient("Bottom", false);
    }

    void Update()
    {
        if (canvasGroup == null || canvas == null || !canvas.enabled)
            return;

        // Begin fully visible, then smoothly fade out and back in over six seconds.
        float pulse = Mathf.PingPong(Time.unscaledTime / PulseHalfCycleSeconds + 1f, 1f);
        canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, pulse);
    }

    RectTransform CreateGradient(string name, bool top)
    {
        GameObject gradientObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(BlueEdgeGradientGraphic));
        RectTransform rectTransform = gradientObject.GetComponent<RectTransform>();
        rectTransform.SetParent(safeFrame, false);
        rectTransform.anchorMin = new Vector2(0f, top ? 0.5f : 0f);
        rectTransform.anchorMax = new Vector2(1f, top ? 1f : EdgeCoverage);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        BlueEdgeGradientGraphic graphic = gradientObject.GetComponent<BlueEdgeGradientGraphic>();
        graphic.Configure(top, new Color(0.04f, 0.25f, 0.78f, EdgeOpacity));
        graphic.raycastTarget = false;
        return rectTransform;
    }

    void RefreshVisibility(Scene scene)
    {
        if (canvas != null)
            canvas.enabled = string.Equals(
                scene.name,
                UnderwaterStageSceneName,
                System.StringComparison.OrdinalIgnoreCase);

        ApplyTopGradientLayout(isBattleModeScene: false);
    }

    void ApplyTopGradientLayout(bool isBattleModeScene)
    {
        if (topGradient == null)
            return;

        float hudHeight = isBattleModeScene ? BattleModeHudHeight : NormalGameHudHeight;
        topGradient.anchorMin = new Vector2(0f, 0.5f);
        topGradient.anchorMax = new Vector2(1f, 1f - hudHeight / ReferenceScreenHeight);
        topGradient.offsetMin = Vector2.zero;
        topGradient.offsetMax = Vector2.zero;
    }
}

/// <summary>UI mesh with opacity at one edge and transparency toward the screen centre.</summary>
public sealed class BlueEdgeGradientGraphic : Graphic
{
    bool fadeFromTop;
    Color edgeColor = Color.blue;

    public void Configure(bool top, Color color)
    {
        fadeFromTop = top;
        edgeColor = color;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = GetPixelAdjustedRect();
        Color transparent = edgeColor;
        transparent.a = 0f;

        Color lowerColor = fadeFromTop ? transparent : edgeColor;
        Color upperColor = fadeFromTop ? edgeColor : transparent;

        UIVertex lowerLeft = UIVertex.simpleVert;
        lowerLeft.position = new Vector3(rect.xMin, rect.yMin);
        lowerLeft.color = lowerColor;

        UIVertex upperLeft = UIVertex.simpleVert;
        upperLeft.position = new Vector3(rect.xMin, rect.yMax);
        upperLeft.color = upperColor;

        UIVertex upperRight = UIVertex.simpleVert;
        upperRight.position = new Vector3(rect.xMax, rect.yMax);
        upperRight.color = upperColor;

        UIVertex lowerRight = UIVertex.simpleVert;
        lowerRight.position = new Vector3(rect.xMax, rect.yMin);
        lowerRight.color = lowerColor;

        vertexHelper.AddUIVertexQuad(new[] { lowerLeft, upperLeft, upperRight, lowerRight });
    }
}
