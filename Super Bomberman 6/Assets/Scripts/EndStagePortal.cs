using UnityEngine;

public class EndStagePortal : MonoBehaviour
{
    [Header("SFX")]
    public AudioClip enterSfx;

    [Header("Music")]
    public AudioClip endStageMusic;

    [Header("Visual")]
    public AnimatedSpriteRenderer idleRenderer;

    private bool isActivated;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isActivated)
            return;

        if (!other.CompareTag("Player"))
            return;

        isActivated = true;

        // toca som no player
        var audio = other.GetComponent<AudioSource>();
        if (audio != null && enterSfx != null)
        {
            audio.PlayOneShot(enterSfx);
        }

        // PARA a música atual da fase
        if (GameMusicController.Instance != null)
        {
            GameMusicController.Instance.StopMusic();
        }

        // toca música de vitória
        if (endStageMusic != null && GameMusicController.Instance != null)
        {
            GameMusicController.Instance.PlayMusic(endStageMusic, 1f);
        }

        // chama o GameManager para avançar a fase
        var gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            gameManager.EndStage();
        }
    }
}
