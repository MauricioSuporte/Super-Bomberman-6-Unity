using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BombExplosion : MonoBehaviour
{
    private static readonly Dictionary<BombExplosion, Stack<BombExplosion>> Pools = new();

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

    public void SetOrigin(Vector2 origin) => Origin = origin;
    public void SetSource(BombController owner, int ownerPlayerId, bool isRevengeBomb)
    {
        Owner = owner;
        OwnerPlayerId = ownerPlayerId;
        IsRevengeBomb = isRevengeBomb;
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
        if (start != null) start.enabled = renderer == start;
        if (middle != null) middle.enabled = renderer == middle;
        if (end != null) end.enabled = renderer == end;
        CurrentPart = part;
    }

    private void PrepareForSpawn(Vector2 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
        Origin = position;
        Owner = null;
        OwnerPlayerId = 0;
        IsRevengeBomb = false;
        isReleased = false;
        ResetRenderers();

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
    }

    private void ResetRenderers()
    {
        if (start != null) start.enabled = false;
        if (middle != null) middle.enabled = false;
        if (end != null) end.enabled = false;
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

    public void Play(ExplosionPart part, Vector2 direction, float delay, float duration, Vector2 origin)
    {
        if (playRoutine != null)
            StopCoroutine(playRoutine);

        SetOrigin(origin);
        SetDirection(direction);
        playRoutine = StartCoroutine(PlayRoutine(part, delay, duration));
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
