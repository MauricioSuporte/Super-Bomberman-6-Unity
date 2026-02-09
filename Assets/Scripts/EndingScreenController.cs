using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EndingScreenController : MonoBehaviour
{
    private static readonly WaitForSecondsRealtime _waitFrame = new(0.01f);

    [Header("UI")]
    public Image endingImage;
    public TMP_Text messageText;

    [Header("Audio")]
    public AudioClip endingMusic;
    [Range(0f, 1f)] public float musicVolume = 1f;

    [Header("Text Outline")]
    [SerializeField] Color outlineColor = Color.black;
    [SerializeField, Range(0f, 1f)] float outlineWidth = 0.4f;

    public static EndingScreenController Instance { get; private set; }

    [Header("Return To Title")]
    [SerializeField] string titleSceneName = "Stage_1-1";

    [Header("Message")]
    [TextArea(6, 14)]
    public string message =
        "<size=52><color=#1ABC00>WORLD 2</color>  <color=#E8E8E8>DEMO COMPLETE!</color></size>\n\n\n\n" +
        "<size=34><color=#E8E8E8>Thank you for playing!</color></size>\n" +
        "<size=34><color=#E8E8E8>More stages are coming soon!</color></size>\n\n\n\n" +
        "<size=32><color=#3392FF>OPEN SOURCE PROJECT</color></size>\n" +
        "<size=28><color=#E8E8E8>github.com/MauricioSuporte/</color></size>\n" +
        "<size=28><color=#E8E8E8>Super-Bomberman-6-Unity</color></size>\n\n\n\n" +
        "<size=34><color=#FF6F31>PRESS ENTER</color></size>\n" +
        "<size=30><color=#E8E8E8>TO RETURN TO TITLE SCREEN</color></size>";

    public bool Running { get; private set; }

    Material runtimeMsgMat;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
        }

        SetupMessageMaterial();
        ForceHide();
    }

    void OnDestroy()
    {
        if (runtimeMsgMat != null)
            Destroy(runtimeMsgMat);
    }

    void SetupMessageMaterial()
    {
        if (messageText == null)
            return;

        Material baseMat = messageText.fontSharedMaterial;

        if (baseMat == null && messageText.font != null)
            baseMat = messageText.font.material;

        if (baseMat == null)
            return;

        if (runtimeMsgMat != null)
            Destroy(runtimeMsgMat);

        runtimeMsgMat = new Material(baseMat);

        if (runtimeMsgMat.HasProperty("_OutlineWidth"))
            runtimeMsgMat.SetFloat("_OutlineWidth", outlineWidth);

        if (runtimeMsgMat.HasProperty("_OutlineColor"))
            runtimeMsgMat.SetColor("_OutlineColor", outlineColor);

        messageText.fontSharedMaterial = runtimeMsgMat;
    }

    public void ForceHide()
    {
        Running = false;

        if (endingImage != null)
            endingImage.gameObject.SetActive(false);

        if (messageText != null)
            messageText.gameObject.SetActive(false);

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    public IEnumerator Play(Image fadeImageOptional)
    {
        Running = true;

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        SetupMessageMaterial();

        if (endingImage != null)
        {
            endingImage.gameObject.SetActive(true);
            endingImage.enabled = true;

            var c = endingImage.color;
            c.a = 0f;
            endingImage.color = c;

            endingImage.transform.SetAsLastSibling();
        }

        if (messageText != null)
        {
            messageText.text = message;
            messageText.gameObject.SetActive(true);
            messageText.alpha = 0f;
            messageText.transform.SetAsLastSibling();
        }

        if (fadeImageOptional != null)
        {
            fadeImageOptional.gameObject.SetActive(true);
            fadeImageOptional.transform.SetAsLastSibling();
        }

        if (endingMusic != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlayMusic(endingMusic, musicVolume, true);

        float duration = 2f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);

            if (endingImage != null)
            {
                var c = endingImage.color;
                c.a = p;
                endingImage.color = c;
            }

            if (messageText != null)
                messageText.alpha = p;

            if (fadeImageOptional != null)
            {
                var fc = fadeImageOptional.color;
                fc.a = 1f - p;
                fadeImageOptional.color = fc;
            }

            yield return null;
        }

        if (fadeImageOptional != null)
            fadeImageOptional.gameObject.SetActive(false);

        var input = PlayerInputManager.Instance;

        if (input != null && input.AnyGet(PlayerAction.Start))
        {
            while (input != null && input.AnyGet(PlayerAction.Start))
                yield return _waitFrame;

            yield return null;
        }

        while (true)
        {
            input = PlayerInputManager.Instance;
            if (input != null && input.AnyGetDown(PlayerAction.Start))
                break;

            yield return null;
        }

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        GamePauseController.ForceUnpause();
        ForceHide();
        Running = false;

        PlayerPersistentStats.ResetSessionForReturnToTitle();
        SceneManager.LoadScene(titleSceneName, LoadSceneMode.Single);
    }
}
