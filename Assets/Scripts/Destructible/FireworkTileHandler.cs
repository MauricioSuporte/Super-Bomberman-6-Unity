using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;

public sealed class FireworkTileHandler : MonoBehaviour, IDestructibleTileHandler
{
    public static event Action AllFireworksDestroyedAtPhase2Start;

    [Header("Stage Fireworks Counter")]
    [SerializeField, Min(0)] private int totalFireworksInStage = 0;
    [SerializeField] private TileBase fireworkTile;

    static int _total;
    static int _destroyed;
    static int _sceneBuildIndex = -1;
    static bool _pendingInvokeAtPhase2Start;

    [Header("Prefabs (3 phases)")]
    [SerializeField] private AnimatedSpriteRenderer phase1RisePrefab;
    [SerializeField] private AnimatedSpriteRenderer phase2ExplosionPrefab;
    [SerializeField] private AnimatedSpriteRenderer phase3SparksPrefab;

    [Header("Phases Timing")]
    [SerializeField, Min(0.01f)] private float phase1DurationSeconds = 0.5f;
    [SerializeField, Min(0.01f)] private float phase2DurationSeconds = 1f;
    [SerializeField, Min(0.01f)] private float phase3DurationSeconds = 1f;

    [Header("Spawn Offset")]
    [SerializeField] private Vector3 worldOffset = Vector3.zero;

    [Header("Phase 2 Start SFX")]
    [SerializeField] private AudioClip phase2StartSfx;
    [SerializeField, Range(0f, 1f)] private float phase2StartSfxVolume = 1f;

    [Header("Phase 2/3 Anchor")]
    [SerializeField] private float areaBottomUpTiles = 3f;

    [Header("Phase 2 Area (Width x Height)")]
    [SerializeField, Min(1)] private int phase2WidthTiles = 5;
    [SerializeField, Min(1)] private int phase2HeightTiles = 2;

    [Header("Phase 3 Area (Width x Height)")]
    [SerializeField, Min(1)] private int phase3WidthTiles = 3;
    [SerializeField, Min(1)] private int phase3HeightTiles = 2;

    [Header("Phase 2 Spawning")]
    [SerializeField, Min(0.01f)] private float phase2SpawnLifetimeSeconds = 0.1f;
    [SerializeField, Min(0f)] private float phase2MinIntervalSeconds = 0.01f;
    [SerializeField, Min(0f)] private float phase2MaxIntervalSeconds = 0.08f;
    [SerializeField, Min(1)] private int phase2MinSpawnPerBurst = 1;
    [SerializeField, Min(1)] private int phase2MaxSpawnPerBurst = 3;

    [Header("Phase 3 Spawning")]
    [SerializeField, Min(0.01f)] private float phase3SpawnLifetimeSeconds = 0.25f;
    [SerializeField, Min(0f)] private float phase3MinIntervalSeconds = 0.10f;
    [SerializeField, Min(0f)] private float phase3MaxIntervalSeconds = 0.22f;
    [SerializeField, Min(1)] private int phase3MinSpawnPerBurst = 1;
    [SerializeField, Min(1)] private int phase3MaxSpawnPerBurst = 1;

    [Header("Phase 3 Drift Down")]
    [SerializeField, Min(0f)] private float phase3DriftSpeedUnitsPerSecond = 0.75f;
    [SerializeField, Min(0f)] private float phase3DriftMaxDownDistance = 0.6f;

    [Header("Grid")]
    [SerializeField] private bool roundToPixelGrid = true;
    [SerializeField] private bool snapYOnPhase3 = false;

    readonly HashSet<Vector3Int> _triggered = new();
    AudioSource _audio;

    void Awake()
    {
        int currentScene = SceneManager.GetActiveScene().buildIndex;
        if (_sceneBuildIndex != currentScene)
        {
            _sceneBuildIndex = currentScene;
            _total = 0;
            _destroyed = 0;
            _pendingInvokeAtPhase2Start = false;
        }

        _audio = GetComponent<AudioSource>();
        if (_audio == null)
            _audio = gameObject.AddComponent<AudioSource>();

        _audio.playOnAwake = false;
        _audio.loop = false;
    }

    void Start()
    {
        if (_total > 0)
            return;

        if (totalFireworksInStage > 0)
        {
            _total = totalFireworksInStage;
            return;
        }

        int counted = TryAutoCountFireworks();
        if (counted > 0)
            _total = counted;
    }

    int TryAutoCountFireworks()
    {
        var gm = FindFirstObjectByType<GameManager>();
        if (gm == null || gm.destructibleTilemap == null)
            return 0;

        if (fireworkTile == null)
            return 0;

        int c = 0;
        var bounds = gm.destructibleTilemap.cellBounds;

        foreach (var pos in bounds.allPositionsWithin)
        {
            var t = gm.destructibleTilemap.GetTile(pos);
            if (t == fireworkTile)
                c++;
        }

        return c;
    }

    public bool HandleHit(BombController source, Vector2 worldPos, Vector3Int cell)
    {
        if (source == null)
            return false;

        if (!_triggered.Add(cell))
            return true;

        bool isLast = RegisterFireworkDestroyedAndCheckIfLast();

        source.ClearDestructibleForEffect(worldPos, false, false);

        Vector2 p = worldPos;
        p.x = Mathf.Round(p.x);
        p.y = Mathf.Round(p.y);

        Vector3 origin = (Vector3)p + worldOffset;
        StartCoroutine(FireworkSequence(origin, isLast));

        return true;
    }

    bool RegisterFireworkDestroyedAndCheckIfLast()
    {
        if (_total <= 0)
            return false;

        _destroyed++;

        if (_destroyed >= _total)
        {
            _destroyed = _total;
            _pendingInvokeAtPhase2Start = true;
            return true;
        }

        return false;
    }

    IEnumerator FireworkSequence(Vector3 origin, bool isLastFirework)
    {
        if (phase1RisePrefab != null)
            SpawnOneShot(phase1RisePrefab, Snap(origin, true, true), phase1DurationSeconds, false);

        yield return new WaitForSeconds(phase1DurationSeconds);

        Vector3 areaBottomCenter = origin + Vector3.up * areaBottomUpTiles;
        areaBottomCenter = Snap(areaBottomCenter, true, true);

        PlayPhase2StartSfx();

        if (isLastFirework && _pendingInvokeAtPhase2Start)
        {
            _pendingInvokeAtPhase2Start = false;
            AllFireworksDestroyedAtPhase2Start?.Invoke();
        }

        if (phase2ExplosionPrefab != null)
        {
            yield return StartCoroutine(SpawnBurstPhaseInArea(
                areaBottomCenter,
                phase2ExplosionPrefab,
                phase2DurationSeconds,
                phase2WidthTiles,
                phase2HeightTiles,
                phase2MinIntervalSeconds,
                phase2MaxIntervalSeconds,
                phase2MinSpawnPerBurst,
                phase2MaxSpawnPerBurst,
                phase2SpawnLifetimeSeconds,
                false,
                true
            ));
        }
        else
        {
            yield return new WaitForSeconds(phase2DurationSeconds);
        }

        if (phase3SparksPrefab != null)
        {
            yield return StartCoroutine(SpawnBurstPhaseInArea(
                areaBottomCenter,
                phase3SparksPrefab,
                phase3DurationSeconds,
                phase3WidthTiles,
                phase3HeightTiles,
                phase3MinIntervalSeconds,
                phase3MaxIntervalSeconds,
                phase3MinSpawnPerBurst,
                phase3MaxSpawnPerBurst,
                phase3SpawnLifetimeSeconds,
                true,
                snapYOnPhase3
            ));
        }
        else
        {
            yield return new WaitForSeconds(phase3DurationSeconds);
        }
    }

    void PlayPhase2StartSfx()
    {
        if (phase2StartSfx == null || _audio == null)
            return;

        _audio.PlayOneShot(phase2StartSfx, phase2StartSfxVolume);
    }

    IEnumerator SpawnBurstPhaseInArea(
        Vector3 areaBottomCenter,
        AnimatedSpriteRenderer prefab,
        float phaseSeconds,
        int widthTiles,
        int heightTiles,
        float minInterval,
        float maxInterval,
        int minPerBurst,
        int maxPerBurst,
        float spawnLifetime,
        bool driftDown,
        bool snapY)
    {
        float endTime = Time.time + Mathf.Max(0.01f, phaseSeconds);
        int halfW = widthTiles / 2;

        while (Time.time < endTime)
        {
            int count = Random.Range(minPerBurst, maxPerBurst + 1);

            for (int i = 0; i < count; i++)
            {
                int dx = (widthTiles == 1) ? 0 : Random.Range(-halfW, halfW + 1);
                int dy = (heightTiles == 1) ? 0 : Random.Range(0, heightTiles);

                Vector3 pos = new(
                    areaBottomCenter.x + dx,
                    areaBottomCenter.y + dy,
                    0f
                );

                pos = Snap(pos, true, snapY);
                SpawnOneShot(prefab, pos, spawnLifetime, driftDown);
            }

            float wait = Random.Range(minInterval, maxInterval);
            yield return wait > 0f ? new WaitForSeconds(wait) : null;
        }
    }

    void SpawnOneShot(AnimatedSpriteRenderer prefab, Vector3 worldPos, float durationSeconds, bool driftDown)
    {
        AnimatedSpriteRenderer fx = Instantiate(prefab, worldPos, Quaternion.identity);

        fx.idle = false;
        fx.loop = false;
        fx.useSequenceDuration = true;
        fx.sequenceDuration = Mathf.Max(0.01f, durationSeconds);
        fx.CurrentFrame = 0;

        fx.enabled = false;
        fx.enabled = true;

        if (driftDown)
        {
            var drift = fx.gameObject.AddComponent<DriftDownDuringLifetime>();
            drift.speedUnitsPerSecond = phase3DriftSpeedUnitsPerSecond;
            drift.maxDownDistance = phase3DriftMaxDownDistance;
        }

        Destroy(fx.gameObject, durationSeconds + 0.05f);
    }

    Vector3 Snap(Vector3 p, bool snapX, bool snapY)
    {
        if (!roundToPixelGrid)
            return p;

        if (snapX) p.x = Mathf.Round(p.x);
        if (snapY) p.y = Mathf.Round(p.y);
        p.z = 0f;
        return p;
    }

    private sealed class DriftDownDuringLifetime : MonoBehaviour
    {
        public float speedUnitsPerSecond;
        public float maxDownDistance;

        Vector3 startPos;
        float accumulatedDown;

        void OnEnable()
        {
            startPos = transform.position;
            accumulatedDown = 0f;
        }

        void Update()
        {
            float step = speedUnitsPerSecond * Time.deltaTime;
            accumulatedDown = Mathf.Min(maxDownDistance, accumulatedDown + step);

            Vector3 pos = startPos;
            pos.y -= accumulatedDown;
            transform.position = pos;
        }
    }
}
