using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossEndStageSequence : MonoBehaviour
{
    [Header("Audio")]
    public AudioClip endStageMusic;

    [Header("Timing")]
    [Min(0f)] public float delayBeforeStart = 1f;
    [Min(0f)] public float celebrationSeconds = 5f;
    [Min(0f)] public float fadeDuration = 3f;

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

        runner.endStageMusic = endStageMusic;
        runner.delayBeforeStart = delayBeforeStart;
        runner.celebrationSeconds = celebrationSeconds;
        runner.fadeDuration = fadeDuration;
        runner.gameManager = gameManager;

        runner.StartCoroutine(runner.RunEndStageRoutine());

        enabled = false;
    }

    IEnumerator RunEndStageRoutine()
    {
        if (delayBeforeStart > 0f)
            yield return new WaitForSeconds(delayBeforeStart);

        var alivePlayers = FindAlivePlayers();
        if (alivePlayers.Count == 0)
        {
            Destroy(gameObject);
            yield break;
        }

        for (int i = 0; i < alivePlayers.Count; i++)
        {
            var m = alivePlayers[i];
            if (m == null || m.isDead || m.IsEndingStage)
                continue;

            var bombController = m.GetComponent<BombController>();

            PlayerPersistentStats.SaveFrom(m, bombController);

            if (bombController != null)
                bombController.ClearPlantedBombsOnStageEnd(false);

            Vector2 center = new(
                Mathf.Round(m.transform.position.x),
                Mathf.Round(m.transform.position.y)
            );

            m.PlayEndStageSequence(center, snapToPortalCenter: false);

            MakePlayerSafeForEnding(m);
        }

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        if (endStageMusic != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlayMusic(endStageMusic, 1f, false);

        if (celebrationSeconds > 0f)
        {
            float elapsed = 0f;
            while (elapsed < celebrationSeconds)
            {
                if (!GamePauseController.IsPaused)
                    elapsed += Time.deltaTime;

                yield return null;
            }
        }

        if (StageIntroTransition.Instance != null)
            StageIntroTransition.Instance.StartFadeOut(fadeDuration);

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

    void MakePlayerSafeForEnding(MovementController player)
    {
        if (player == null)
            return;

        player.SetExplosionInvulnerable(true);
        player.SetInputLocked(true, false);

        if (player.TryGetComponent<Collider2D>(out var col))
        {
            cachedPlayerColliders.Add(col);
            cachedColliderEnabled.Add(col.enabled);
            col.enabled = false;
        }

        if (player.TryGetComponent<CharacterHealth>(out var health) && health != null)
            health.StopInvulnerability();
    }
}
