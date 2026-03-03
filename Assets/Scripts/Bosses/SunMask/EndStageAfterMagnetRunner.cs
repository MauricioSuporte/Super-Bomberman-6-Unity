using System.Collections;
using UnityEngine;

public class EndStageAfterMagnetRunner : MonoBehaviour
{
    private const string RLOG = "[SunMask_EndStageRunner]";
    [HideInInspector] public bool enableSurgicalLogs = true;

    [HideInInspector] public GameObject magnetBomberPrefab;
    [HideInInspector] public bool playMagnetBomberDeathOnSpawn = true;
    [HideInInspector] public float magnetBomberDeathFallbackDuration = 2f;

    [HideInInspector] public float endStageDelayAfterMagnetDeath = 1f;

    // snapshot config
    private AudioClip _endStageMusic;
    private bool _playRandomGoodSfx = true;
    private float _goodSfxVolume = 1f;
    private float _delayBeforeStart = 1f;
    private float _celebrationSeconds = 5f;
    private float _fadeDuration = 3f;

    public void CopyEndStageConfigFrom(BossEndStageSequence src)
    {
        // se não foi setado no inspector, tenta achar agora (antes do boss morrer destruir tudo)
        if (src == null)
            src = FindFirstObjectByType<BossEndStageSequence>();

        if (src == null)
        {
            SLog("CopyEndStageConfigFrom: src=NULL (will use defaults)");
            return;
        }

        _endStageMusic = src.endStageMusic;

        // reflection-free: precisamos de campos públicos/serialize. Como playRandomGoodSfx e goodSfxVolume são private,
        // a forma segura é você tornar esses dois "public" OU expor getters.
        // Enquanto isso, mantemos os defaults e logamos.
        _delayBeforeStart = src.delayBeforeStart;
        _celebrationSeconds = src.celebrationSeconds;
        _fadeDuration = src.fadeDuration;

        SLog($"CopyEndStageConfigFrom: OK | src={src.name} | delay={_delayBeforeStart} celeb={_celebrationSeconds} fade={_fadeDuration} music={(_endStageMusic != null ? _endStageMusic.name : "NULL")}");
        SLog("NOTE: playRandomGoodSfx/goodSfxVolume são private no BossEndStageSequence; usando defaults do runner.");
    }

    void Start()
    {
        SLog($"Start | timeScale={Time.timeScale:0.###}");
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        SLog($"Run BEGIN | magnetPrefab={(magnetBomberPrefab != null ? magnetBomberPrefab.name : "NULL")}");

        SpawnMagnetBomberDeath(out float magnetDeathSeconds);

        if (magnetDeathSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(magnetDeathSeconds);
            SLog("Magnet death wait complete (Realtime)");
        }

        float d = Mathf.Max(0f, endStageDelayAfterMagnetDeath);
        if (d > 0f)
        {
            SLog($"EndStageDelay={d:0.###} (Realtime)");
            yield return new WaitForSecondsRealtime(d);
        }

        // Cria um BossEndStageSequence independente (não será destruído junto do boss original)
        var seqGo = new GameObject("BossEndStageSequence_SunMaskRunner");
        var seq = seqGo.AddComponent<BossEndStageSequence>();

        // copia config mínima (campos públicos)
        seq.endStageMusic = _endStageMusic;
        seq.delayBeforeStart = _delayBeforeStart;
        seq.celebrationSeconds = _celebrationSeconds;
        seq.fadeDuration = _fadeDuration;

        SLog("Calling StartBossDefeatedSequence() on NEW sequence instance");
        seq.StartBossDefeatedSequence();

        Destroy(gameObject);
    }

    private void SpawnMagnetBomberDeath(out float magnetDeathSeconds)
    {
        magnetDeathSeconds = 0f;

        if (magnetBomberPrefab == null)
        {
            SLog("SpawnMagnet: magnetBomberPrefab=NULL (skip)");
            return;
        }

        Vector3 pos = transform.position;
        GameObject go = Instantiate(magnetBomberPrefab, pos, Quaternion.identity);
        if (go == null)
        {
            SLog("SpawnMagnet: Instantiate returned NULL (unexpected)");
            return;
        }

        SLog($"SpawnMagnet: spawned go={go.name} id={go.GetInstanceID()} at {pos}");

        if (!playMagnetBomberDeathOnSpawn)
        {
            SLog("SpawnMagnet: playMagnetBomberDeathOnSpawn=false (no forced death)");
            return;
        }

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

    private void SLog(string msg)
    {
        if (!enableSurgicalLogs) return;
        Debug.Log($"{RLOG} [t={Time.unscaledTime:0.00}] {msg}", this);
    }
}