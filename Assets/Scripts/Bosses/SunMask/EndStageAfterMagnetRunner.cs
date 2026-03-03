using System.Collections;
using UnityEngine;

public class EndStageAfterMagnetRunner : MonoBehaviour
{
    [HideInInspector] public GameObject magnetBomberPrefab;
    [HideInInspector] public bool playMagnetBomberDeathOnSpawn = true;
    [HideInInspector] public float magnetBomberDeathFallbackDuration = 2f;

    [HideInInspector] public float endStageDelayAfterMagnetDeath = 1f;

    private AudioClip endStageMusic;
    private float delayBeforeStart = 1f;
    private float celebrationSeconds = 5f;
    private float fadeDuration = 3f;

    public void CopyEndStageConfigFrom(BossEndStageSequence src)
    {
        if (src == null)
            src = FindFirstObjectByType<BossEndStageSequence>();

        if (src == null)
            return;

        endStageMusic = src.endStageMusic;
        delayBeforeStart = src.delayBeforeStart;
        celebrationSeconds = src.celebrationSeconds;
        fadeDuration = src.fadeDuration;
    }

    void Start()
    {
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        SpawnMagnetBomberDeath(out float magnetDeathSeconds);

        if (magnetDeathSeconds > 0f)
            yield return new WaitForSecondsRealtime(magnetDeathSeconds);

        float d = Mathf.Max(0f, endStageDelayAfterMagnetDeath);
        if (d > 0f)
            yield return new WaitForSecondsRealtime(d);

        var seqGo = new GameObject("BossEndStageSequence_SunMaskRunner");
        var seq = seqGo.AddComponent<BossEndStageSequence>();

        seq.endStageMusic = endStageMusic;
        seq.delayBeforeStart = delayBeforeStart;
        seq.celebrationSeconds = celebrationSeconds;
        seq.fadeDuration = fadeDuration;

        seq.StartBossDefeatedSequence();

        Destroy(gameObject);
    }

    private void SpawnMagnetBomberDeath(out float magnetDeathSeconds)
    {
        magnetDeathSeconds = 0f;

        if (magnetBomberPrefab == null)
            return;

        Vector3 pos = transform.position;
        GameObject go = Instantiate(magnetBomberPrefab, pos, Quaternion.identity);
        if (go == null)
            return;

        if (!playMagnetBomberDeathOnSpawn)
            return;

        var ai = go.GetComponentInChildren<MovementControllerAI>(true);
        if (ai != null)
        {
            ai.SetIntroIdle(false);
            ai.ForceDisableOptionalVisualsNow();
        }

        var mc = go.GetComponentInChildren<MovementController>(true);
        if (mc != null)
        {
            mc.SetExternalMovementOverride(true);
            mc.Kill();

            magnetDeathSeconds = Mathf.Max(0.05f, mc.deathDisableSeconds);
            Destroy(go, magnetDeathSeconds + 0.2f);
            return;
        }

        var ch = go.GetComponentInChildren<CharacterHealth>(true);
        if (ch != null)
        {
            ch.TakeDamage(9999);

            magnetDeathSeconds = Mathf.Max(0.05f, magnetBomberDeathFallbackDuration);
            Destroy(go, magnetDeathSeconds + 0.2f);
            return;
        }

        magnetDeathSeconds = Mathf.Max(0.05f, magnetBomberDeathFallbackDuration);
        Destroy(go, magnetDeathSeconds + 0.2f);
    }
}