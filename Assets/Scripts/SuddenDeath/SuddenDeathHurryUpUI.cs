using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class SuddenDeathHurryUpUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Image image;
    [SerializeField] private RectTransform rectTransform;

    [Header("Timing")]
    [SerializeField] private float duration = 2f;
    [SerializeField] private float blinkSpeed = 0.08f;

    [Header("Position")]
    [SerializeField] private Vector2 anchoredPosition = new Vector2(0f, 80f);

    [Header("Scale")]
    [SerializeField] private float baseScale = 1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool autoPlayOnStartForDebug = false;
    [SerializeField] private bool disableBlinkForDebug = false;

    Coroutine routine;

    void Awake()
    {
        if (image == null)
            image = GetComponent<Image>();

        if (rectTransform == null && image != null)
            rectTransform = image.rectTransform;

        if (image != null)
            image.enabled = false;

        Log(
            $"Awake | image={(image != null ? image.name : "NULL")} " +
            $"rectTransform={(rectTransform != null ? rectTransform.name : "NULL")} " +
            $"gameObjectActive={gameObject.activeInHierarchy}");
    }

    void Start()
    {
        Log(
            $"Start | image={(image != null ? "OK" : "NULL")} " +
            $"sprite={(image != null && image.sprite != null ? image.sprite.name : "NULL")} " +
            $"canvas={GetComponentInParent<Canvas>()?.name ?? "NULL"} " +
            $"screen={Screen.width}x{Screen.height}");

        if (autoPlayOnStartForDebug)
        {
            Log("Start | autoPlayOnStartForDebug = true, chamando Play()");
            Play();
        }
    }

    [ContextMenu("TESTAR HURRY UP UI")]
    public void Play()
    {
        Log("Play() chamado");

        if (image == null)
        {
            Debug.LogWarning("[SuddenDeathHurryUpUI] Image não configurada.", this);
            return;
        }

        if (rectTransform == null)
            rectTransform = image.rectTransform;

        if (rectTransform == null)
        {
            Debug.LogWarning("[SuddenDeathHurryUpUI] RectTransform não encontrado.", this);
            return;
        }

        if (image.sprite == null)
        {
            Debug.LogWarning("[SuddenDeathHurryUpUI] Sprite da Image está NULL.", this);
            return;
        }

        if (routine != null)
        {
            Log("Play() | parando rotina anterior");
            StopCoroutine(routine);
        }

        routine = StartCoroutine(PlayRoutine());
    }

    IEnumerator PlayRoutine()
    {
        Log("PlayRoutine() iniciou");

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;

        image.SetNativeSize();

        rectTransform.localScale = Vector3.one * baseScale;

        image.enabled = true;
        image.color = Color.white;

        Canvas.ForceUpdateCanvases();

        Log(
            $"PlayRoutine() | sprite={image.sprite.name} " +
            $"nativeSize={image.sprite.rect.width}x{image.sprite.rect.height} " +
            $"anchoredPosition={rectTransform.anchoredPosition} " +
            $"sizeDelta={rectTransform.sizeDelta} " +
            $"localScale={rectTransform.localScale} " +
            $"enabled={image.enabled}");

        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        Log(
            $"PlayRoutine() | worldCorners BL={corners[0]} TL={corners[1]} TR={corners[2]} BR={corners[3]}");

        if (disableBlinkForDebug)
        {
            Log($"PlayRoutine() | disableBlinkForDebug=true, exibindo fixo por {duration:0.000}s");
            yield return new WaitForSeconds(duration);
            image.enabled = false;
            routine = null;
            Log("PlayRoutine() finalizou sem blink");
            yield break;
        }

        float elapsed = 0f;
        bool visible = true;

        while (elapsed < duration)
        {
            yield return new WaitForSeconds(blinkSpeed);

            elapsed += blinkSpeed;
            visible = !visible;
            image.enabled = visible;

            Log($"Blink | elapsed={elapsed:0.000} visible={visible}");
        }

        image.enabled = false;
        routine = null;

        Log("PlayRoutine() finalizou");
    }

    float GetPixelPerfectScale()
    {
        float scale = Mathf.Max(1, Mathf.RoundToInt((float)Screen.height / 224f));
        Log($"GetPixelPerfectScale() = {scale}");
        return scale;
    }

    void Log(string msg)
    {
        if (!enableDebugLogs)
            return;

        Debug.Log($"[SuddenDeathHurryUpUI] {msg}", this);
    }
}