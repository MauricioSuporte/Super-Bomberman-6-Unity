using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[RequireComponent(typeof(RawImage))]
[RequireComponent(typeof(VideoPlayer))]
public class TitleScreenController : MonoBehaviour
{
    [Header("UI / Video")]
    public RawImage titleScreenRawImage;
    public VideoPlayer titleVideoPlayer;

    [Header("Audio")]
    public AudioClip titleMusic;

    [Header("Input")]
    public KeyCode startKey = KeyCode.Return;

    public bool Running { get; private set; }

    bool ignoreStartKeyUntilRelease;

    void Awake()
    {
        if (titleScreenRawImage == null)
            titleScreenRawImage = GetComponent<RawImage>();

        if (titleVideoPlayer == null)
            titleVideoPlayer = GetComponent<VideoPlayer>();

        ForceHide();
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

        if (ignoreStartKeyUntilRelease || Input.GetKey(startKey))
        {
            ignoreStartKeyUntilRelease = false;
            while (Input.GetKey(startKey))
                yield return null;
            yield return null;
        }
        else
        {
            yield return null;
        }

        while (!Input.GetKeyDown(startKey))
            yield return null;

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        titleVideoPlayer.Stop();
        titleScreenRawImage.gameObject.SetActive(false);

        Running = false;
    }
}
