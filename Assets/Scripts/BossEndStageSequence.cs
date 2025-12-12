using System.Collections;
using UnityEngine;

public class BossEndStageSequence : MonoBehaviour
{
    public MovementController player;
    public AudioClip bossCheeringMusic;
    public float delayAfterBossDeath = 1f;
    public float cheeringDuration = 4f;
    public float fadeDuration = 1f;

    GameManager gameManager;
    bool sequenceStarted;

    void Awake()
    {
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
                player = go.GetComponent<MovementController>();
        }

        gameManager = FindFirstObjectByType<GameManager>();
    }

    public void StartBossDefeatedSequence()
    {
        if (sequenceStarted)
            return;

        sequenceStarted = true;

        if (isActiveAndEnabled)
            StartCoroutine(BossDefeatedRoutine());
    }

    IEnumerator BossDefeatedRoutine()
    {
        if (delayAfterBossDeath > 0f)
            yield return new WaitForSeconds(delayAfterBossDeath);

        if (player == null || player.isDead)
            yield break;

        player.StartCheering();

        if (GameMusicController.Instance != null && bossCheeringMusic != null)
            GameMusicController.Instance.PlayMusic(bossCheeringMusic, 1f, false);

        float timeBeforeFade = Mathf.Max(0f, cheeringDuration - fadeDuration);

        if (timeBeforeFade > 0f)
            yield return new WaitForSeconds(timeBeforeFade);

        if (StageIntroTransition.Instance != null && fadeDuration > 0f)
            StageIntroTransition.Instance.StartFadeOut(fadeDuration);

        if (fadeDuration > 0f)
            yield return new WaitForSeconds(fadeDuration);

        if (gameManager != null)
            gameManager.EndStage();
    }
}
