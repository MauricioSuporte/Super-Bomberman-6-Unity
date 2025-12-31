using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[RequireComponent(typeof(RawImage))]
[RequireComponent(typeof(VideoPlayer))]
public class TitleScreenController : MonoBehaviour
{
    [Header("SFX")]
    public AudioClip startSfx;
    [Range(0f, 1f)]
    public float startSfxVolume = 1f;

    private static readonly WaitForSecondsRealtime _waitForSecondsRealtime1 = new(1f);

    [Header("UI / Video")]
    public RawImage titleScreenRawImage;
    public VideoPlayer titleVideoPlayer;

    [Header("Audio")]
    public AudioClip titleMusic;

    [Header("Input")]
    public KeyCode startKey = KeyCode.Return;
    [SerializeField] KeyCode startKeyAlt = KeyCode.M;

    public bool Running { get; private set; }

    bool ignoreStartKeyUntilRelease;
    bool bootedSession;

    void Awake()
    {
        if (titleScreenRawImage == null)
            titleScreenRawImage = GetComponent<RawImage>();

        if (titleVideoPlayer == null)
            titleVideoPlayer = GetComponent<VideoPlayer>();

        ForceHide();
    }

    void EnsureBootSession()
    {
        if (bootedSession)
            return;

        PlayerPersistentStats.EnsureSessionBooted();
        bootedSession = true;
    }

    public void SetIgnoreStartKeyUntilRelease()
    {
        ignoreStartKeyUntilRelease = true;
    }

    public void ForceHide()
    {
        Running = false;

        if (titleVideoPlayer != null)
            titleVideoPlayer.Stop();

        if (titleScreenRawImage != null)
            titleScreenRawImage.gameObject.SetActive(false);
    }

    public IEnumerator Play(Image fadeToHideOptional)
    {
        EnsureBootSession();

        Running = true;

        if (titleScreenRawImage == null || titleVideoPlayer == null)
        {
            Running = false;
            yield break;
        }

        if (fadeToHideOptional != null)
            fadeToHideOptional.gameObject.SetActive(false);

        titleScreenRawImage.gameObject.SetActive(true);

        if (titleMusic != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlayMusic(titleMusic, 1f, true);

        titleVideoPlayer.isLooping = true;
        titleVideoPlayer.playOnAwake = false;
        titleVideoPlayer.Stop();
        titleVideoPlayer.Play();

        if (ignoreStartKeyUntilRelease || Input.GetKey(startKey) || Input.GetKey(startKeyAlt))
        {
            ignoreStartKeyUntilRelease = false;
            while (Input.GetKey(startKey) || Input.GetKey(startKeyAlt))
                yield return null;
            yield return null;
        }
        else
        {
            yield return null;
        }

        while (!Input.GetKeyDown(startKey) && !Input.GetKeyDown(startKeyAlt))
            yield return null;

        if (startSfx != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlaySfx(startSfx, startSfxVolume);

        var transition = StageIntroTransition.Instance;
        if (transition != null)
            transition.StartFadeOut(1f, false);

        yield return _waitForSecondsRealtime1;

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        titleVideoPlayer.Stop();
        titleScreenRawImage.gameObject.SetActive(false);

        Running = false;
    }
}
