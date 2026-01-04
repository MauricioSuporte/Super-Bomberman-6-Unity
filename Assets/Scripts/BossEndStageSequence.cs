using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossEndStageSequence : MonoBehaviour
{
    [Header("Audio")]
    public AudioClip bossCheeringMusic;

    [Header("Timing")]
    public float delayAfterBossDeath = 1f;
    public float cheeringDuration = 4f;
    public float fadeDuration = 1f;

    GameManager gameManager;
    bool sequenceStarted;

    readonly List<Collider2D> cachedPlayerColliders = new();
    readonly List<bool> cachedColliderEnabled = new();

    void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
    }

    public void StartBossDefeatedSequence()
    {
        if (sequenceStarted)
            return;

        sequenceStarted = true;

        var runnerGo = new GameObject("BossEndStageSequenceRunner");
        var runner = runnerGo.AddComponent<BossEndStageSequence>();

        runner.bossCheeringMusic = bossCheeringMusic;
        runner.delayAfterBossDeath = delayAfterBossDeath;
        runner.cheeringDuration = cheeringDuration;
        runner.fadeDuration = fadeDuration;
        runner.gameManager = gameManager;

        runner.StartCoroutine(runner.BossDefeatedRoutine());

        enabled = false;
    }

    IEnumerator BossDefeatedRoutine()
    {
        if (delayAfterBossDeath > 0f)
            yield return new WaitForSeconds(delayAfterBossDeath);

        var alivePlayers = FindAlivePlayers();

        if (alivePlayers.Count == 0)
        {
            Destroy(gameObject);
            yield break;
        }

        for (int i = 0; i < alivePlayers.Count; i++)
        {
            var p = alivePlayers[i];
            if (p == null || p.isDead)
                continue;

            Vector2 center = new(
                Mathf.Round(p.transform.position.x),
                Mathf.Round(p.transform.position.y)
            );

            p.PlayEndStageSequence(center, snapToPortalCenter: false);

            MakePlayerSafeForCelebration(p);
        }

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

        Destroy(gameObject);
    }

    List<MovementController> FindAlivePlayers()
    {
        var list = new List<MovementController>(4);

        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (ids != null && ids.Length > 0)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                if (id == null) continue;

                if (!id.TryGetComponent(out MovementController m))
                    m = id.GetComponentInChildren<MovementController>(true);

                if (m == null) continue;
                if (!m.gameObject.activeInHierarchy) continue;
                if (!m.CompareTag("Player")) continue;
                if (m.isDead) continue;

                list.Add(m);
            }

            return list;
        }

        var players = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            var m = players[i];
            if (m == null) continue;
            if (!m.gameObject.activeInHierarchy) continue;
            if (!m.CompareTag("Player")) continue;
            if (m.isDead) continue;

            list.Add(m);
        }

        return list;
    }

    void MakePlayerSafeForCelebration(MovementController player)
    {
        if (player == null)
            return;

        player.SetExplosionInvulnerable(true);
        player.SetInputLocked(true, false);

        var col = player.GetComponent<Collider2D>();
        if (col != null)
        {
            cachedPlayerColliders.Add(col);
            cachedColliderEnabled.Add(col.enabled);
            col.enabled = false;
        }

        if (player.TryGetComponent<CharacterHealth>(out var health) && health != null)
            health.StopInvulnerability();
    }
}
