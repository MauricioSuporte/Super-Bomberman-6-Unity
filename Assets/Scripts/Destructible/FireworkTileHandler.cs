using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class FireworkTileHandler : MonoBehaviour, IDestructibleTileHandler
{
    [Header("Effect")]
    [SerializeField] private AnimatedSpriteRenderer fireworkPrefab;
    [SerializeField] private float destroyAfterSeconds = 0.6f;
    [SerializeField] private Vector3 worldOffset = Vector3.zero;

    [Header("Animation")]
    [SerializeField] private bool playCycles = true;
    [SerializeField, Min(1)] private int cycles = 1;

    [Header("SFX (optional)")]
    [SerializeField] private AudioClip sfx;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    readonly HashSet<Vector3Int> _triggered = new();
    AudioSource _audio;

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        if (_audio == null)
            _audio = gameObject.AddComponent<AudioSource>();

        _audio.playOnAwake = false;
        _audio.loop = false;
    }

    public bool HandleHit(BombController source, Vector2 worldPos, Vector3Int cell)
    {
        if (source == null)
            return false;

        if (!_triggered.Add(cell))
            return true;

        source.ClearDestructibleForEffect(worldPos, spawnDestructiblePrefab: false, spawnHiddenObject: false);

        Vector2 p = worldPos;
        p.x = Mathf.Round(p.x);
        p.y = Mathf.Round(p.y);

        SpawnFireworkAt((Vector3)p + worldOffset);

        return true;
    }

    void SpawnFireworkAt(Vector3 worldPos)
    {
        if (fireworkPrefab == null)
            return;

        if (sfx != null && _audio != null)
            _audio.PlayOneShot(sfx, sfxVolume);

        AnimatedSpriteRenderer fx = Instantiate(fireworkPrefab, worldPos, Quaternion.identity);

        if (playCycles && cycles > 0)
            StartCoroutine(PlayAndDestroy(fx));
        else if (destroyAfterSeconds > 0f)
            Destroy(fx.gameObject, destroyAfterSeconds);
    }

    IEnumerator PlayAndDestroy(AnimatedSpriteRenderer fx)
    {
        if (fx == null)
            yield break;

        float fallback = Mathf.Max(0.01f, destroyAfterSeconds);

        float duration = EstimateCycleSeconds(fx) * Mathf.Max(1, cycles);
        if (duration <= 0f)
            duration = fallback;

        StartCoroutine(fx.PlayCycles(cycles));

        yield return new WaitForSeconds(duration);

        if (fx != null)
            Destroy(fx.gameObject);
    }

    float EstimateCycleSeconds(AnimatedSpriteRenderer fx)
    {
        if (fx == null)
            return 0f;

        if (fx.useSequenceDuration)
            return Mathf.Max(0f, fx.sequenceDuration);

        if (fx.animationSprite == null || fx.animationSprite.Length == 0)
            return 0f;

        if (fx.pingPong && fx.animationSprite.Length > 1)
            return Mathf.Max(0f, fx.animationTime) * (fx.animationSprite.Length * 2 - 2);

        return Mathf.Max(0f, fx.animationTime) * fx.animationSprite.Length;
    }
}
