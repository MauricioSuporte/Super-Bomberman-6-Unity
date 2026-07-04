using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BombExplosion : MonoBehaviour
{
    private static readonly Dictionary<BombExplosion, Stack<BombExplosion>> Pools = new();
    private const string PierceExplosionSpritesPath = "Sprites/BombExplosions/PierceExplosion";
    private const int DefaultSortingOrder = 3;
    private static Sprite[] pierceStartSprites;
    private static Sprite[] pierceMiddleSprites;
    private static Sprite[] pierceEndSprites;
    private static bool pierceSpritesPreloaded;

    public AnimatedSpriteRenderer start;
    public AnimatedSpriteRenderer middle;
    public AnimatedSpriteRenderer end;

    public enum ExplosionPart { Start, Middle, End }

    public ExplosionPart CurrentPart { get; private set; }
    public Vector2 Origin { get; private set; }
    public BombController Owner { get; private set; }
    public int OwnerPlayerId { get; private set; }
    public bool IsRevengeBomb { get; private set; }

    private Coroutine playRoutine;
    private BombExplosion poolPrefab;
    private bool pooledInstance;
    private bool isReleased;
    private bool usePierceSprites;
    private bool defaultSpritesCaptured;
    private Sprite defaultStartIdle;
    private Sprite[] defaultStartAnimation;
    private Sprite defaultMiddleIdle;
    private Sprite[] defaultMiddleAnimation;
    private Sprite defaultEndIdle;
    private Sprite[] defaultEndAnimation;
    private SpriteRenderer[] cachedSpriteRenderers;
    private Collider2D[] cachedColliders;
    private bool[] defaultColliderEnabledStates;

    public static BombExplosion Spawn(BombExplosion prefab, Vector2 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        if (!Pools.TryGetValue(prefab, out var pool))
        {
            pool = new Stack<BombExplosion>(32);
            Pools[prefab] = pool;
        }

        while (pool.Count > 0)
        {
            var instance = pool.Pop();
            if (instance == null)
                continue;

            instance.PrepareForSpawn(position, rotation);
            return instance;
        }

        var created = Instantiate(prefab, position, rotation);
        created.poolPrefab = prefab;
        created.pooledInstance = true;
        created.PrepareForSpawn(position, rotation);
        return created;
    }

    public static void PreloadPierceSprites()
    {
        if (pierceSpritesPreloaded)
            return;

        pierceStartSprites = LoadPierceSprites("Start");
        pierceMiddleSprites = LoadPierceSprites("Middle");
        pierceEndSprites = LoadPierceSprites("End");
        pierceSpritesPreloaded = true;
    }

    public void SetOrigin(Vector2 origin) => Origin = origin;
    public void SetSource(BombController owner, int ownerPlayerId, bool isRevengeBomb)
    {
        Owner = owner;
        OwnerPlayerId = ownerPlayerId;
        IsRevengeBomb = isRevengeBomb;
    }

    void Awake()
    {
        CaptureDefaultSprites();
    }

    public void SetStart() => SetRenderer(start, ExplosionPart.Start);
    public void SetMiddle() => SetRenderer(middle, ExplosionPart.Middle);
    public void SetEnd() => SetRenderer(end, ExplosionPart.End);

    public void UpgradeToMiddleIfNeeded()
    {
        if (CurrentPart == ExplosionPart.End) SetMiddle();
    }

    void SetRenderer(AnimatedSpriteRenderer renderer, ExplosionPart part)
    {
        ApplySpriteSet(renderer, part, usePierceSprites);

        if (start != null) start.enabled = renderer == start;
        if (middle != null) middle.enabled = renderer == middle;
        if (end != null) end.enabled = renderer == end;
        CurrentPart = part;
    }

    private void PrepareForSpawn(Vector2 position, Quaternion rotation)
    {
        transform.SetParent(null, true);
        transform.SetPositionAndRotation(position, rotation);
        Origin = position;
        Owner = null;
        OwnerPlayerId = 0;
        IsRevengeBomb = false;
        usePierceSprites = false;
        isReleased = false;
        RestoreDefaultSortingOrder();
        ResetRenderers();
        RestoreDefaultColliderStates();

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
    }

    private void ResetRenderers()
    {
        if (start != null) start.enabled = false;
        if (middle != null) middle.enabled = false;
        if (end != null) end.enabled = false;
    }

    private void RestoreDefaultSortingOrder()
    {
        if (cachedSpriteRenderers == null)
            cachedSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < cachedSpriteRenderers.Length; i++)
        {
            if (cachedSpriteRenderers[i] != null)
                cachedSpriteRenderers[i].sortingOrder = DefaultSortingOrder;
        }
    }

    private void Release()
    {
        if (isReleased)
            return;

        isReleased = true;
        playRoutine = null;
        ResetRenderers();

        if (!pooledInstance || poolPrefab == null)
        {
            Destroy(gameObject);
            return;
        }

        if (!Pools.TryGetValue(poolPrefab, out var pool))
        {
            pool = new Stack<BombExplosion>(32);
            Pools[poolPrefab] = pool;
        }

        gameObject.SetActive(false);
        pool.Push(this);
    }

    public void SetDirection(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x);
        transform.rotation = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.forward);
    }

    public void DestroyAfter(float seconds)
    {
        if (seconds <= 0f)
        {
            Release();
            return;
        }

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        playRoutine = StartCoroutine(DestroyRoutine(seconds));
    }

    public void Play(ExplosionPart part, Vector2 direction, float delay, float duration, Vector2 origin, bool usePierceSprites = false)
    {
        if (playRoutine != null)
            StopCoroutine(playRoutine);

        this.usePierceSprites = usePierceSprites;
        SetOrigin(origin);
        SetDirection(direction);
        playRoutine = StartCoroutine(PlayRoutine(part, delay, duration));
    }

    public void SetCollisionEnabled(bool enabled)
    {
        EnsureColliderCache();

        if (cachedColliders == null)
            return;

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null)
                cachedColliders[i].enabled = enabled && defaultColliderEnabledStates[i];
        }
    }

    public void PlayDamageOnly(float duration, Vector2 origin)
    {
        if (playRoutine != null)
            StopCoroutine(playRoutine);

        SetOrigin(origin);
        CurrentPart = ExplosionPart.Middle;
        ResetRenderers();

        if (duration <= 0f)
        {
            playRoutine = null;
            Release();
            return;
        }

        playRoutine = StartCoroutine(DestroyRoutine(duration));
    }

    private void CaptureDefaultSprites()
    {
        if (defaultSpritesCaptured)
            return;

        defaultSpritesCaptured = true;
        CaptureRendererSprites(start, out defaultStartIdle, out defaultStartAnimation);
        CaptureRendererSprites(middle, out defaultMiddleIdle, out defaultMiddleAnimation);
        CaptureRendererSprites(end, out defaultEndIdle, out defaultEndAnimation);
    }

    private void EnsureColliderCache()
    {
        if (cachedColliders != null && defaultColliderEnabledStates != null)
            return;

        cachedColliders = GetComponentsInChildren<Collider2D>(true);
        defaultColliderEnabledStates = new bool[cachedColliders.Length];

        for (int i = 0; i < cachedColliders.Length; i++)
            defaultColliderEnabledStates[i] = cachedColliders[i] != null && cachedColliders[i].enabled;
    }

    private void RestoreDefaultColliderStates()
    {
        EnsureColliderCache();

        if (cachedColliders == null)
            return;

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null)
                cachedColliders[i].enabled = defaultColliderEnabledStates[i];
        }
    }

    private static void CaptureRendererSprites(AnimatedSpriteRenderer renderer, out Sprite idle, out Sprite[] animation)
    {
        idle = renderer != null ? renderer.idleSprite : null;
        animation = renderer != null ? renderer.animationSprite : null;
    }

    private void ApplySpriteSet(AnimatedSpriteRenderer renderer, ExplosionPart part, bool pierce)
    {
        if (renderer == null)
            return;

        CaptureDefaultSprites();

        Sprite idle;
        Sprite[] animation;

        if (pierce && TryGetPierceSprites(part, out var pierceSprites))
        {
            idle = pierceSprites[0];
            animation = pierceSprites;
        }
        else
        {
            GetDefaultSprites(part, out idle, out animation);
        }

        renderer.idleSprite = idle;
        renderer.animationSprite = animation;
        renderer.RefreshFrame();
    }

    private void GetDefaultSprites(ExplosionPart part, out Sprite idle, out Sprite[] animation)
    {
        switch (part)
        {
            case ExplosionPart.Start:
                idle = defaultStartIdle;
                animation = defaultStartAnimation;
                break;
            case ExplosionPart.Middle:
                idle = defaultMiddleIdle;
                animation = defaultMiddleAnimation;
                break;
            default:
                idle = defaultEndIdle;
                animation = defaultEndAnimation;
                break;
        }
    }

    private static bool TryGetPierceSprites(ExplosionPart part, out Sprite[] sprites)
    {
        PreloadPierceSprites();

        sprites = part switch
        {
            ExplosionPart.Start => pierceStartSprites,
            ExplosionPart.Middle => pierceMiddleSprites,
            ExplosionPart.End => pierceEndSprites,
            _ => null
        };

        return sprites != null && sprites.Length > 0;
    }

    private static Sprite[] LoadPierceSprites(string partName)
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>($"{PierceExplosionSpritesPath}{partName}");
        if (sprites == null || sprites.Length == 0)
            return null;

        Array.Sort(sprites, (a, b) => string.CompareOrdinal(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty));
        return sprites;
    }

    IEnumerator PlayRoutine(ExplosionPart part, float delay, float duration)
    {
        ResetRenderers();

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        switch (part)
        {
            case ExplosionPart.Start: SetStart(); break;
            case ExplosionPart.Middle: SetMiddle(); break;
            case ExplosionPart.End: SetEnd(); break;
        }

        if (duration <= 0f)
        {
            playRoutine = null;
            Release();
            yield break;
        }

        yield return new WaitForSeconds(duration);

        playRoutine = null;
        Release();
    }

    private IEnumerator DestroyRoutine(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        playRoutine = null;
        Release();
    }
}
