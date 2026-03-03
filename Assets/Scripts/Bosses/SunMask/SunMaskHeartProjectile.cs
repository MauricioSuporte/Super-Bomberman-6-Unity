using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class SunMaskHeartProjectile : MonoBehaviour
{
    [Header("Collision / Damage")]
    [SerializeField] private bool ignorePlayerLayer = true;
    [SerializeField] private bool ignoreBombLayer = false;

    [Header("Renderers")]
    public AnimatedSpriteRenderer floatRenderer;
    public AnimatedSpriteRenderer destroyRenderer;

    [Header("Movement")]
    [Min(0f)] public float speed = 2.25f;
    [Min(0f)] public float floatAmplitude = 0.08f;
    [Min(0f)] public float floatFrequency = 3.5f;

    [Header("Arrival Behavior")]
    [SerializeField, Min(0f)] private float stopNearTargetDistance = 0.15f;

    [Header("Lifetime")]
    [Min(0f)] public float lifeTime = 6f;

    [Header("Targeting")]
    [SerializeField, Min(0.02f)] private float retargetInterval = 0.25f;

    [Header("Destroy Animation (Explosion only)")]
    public bool playDestroyAnimation = true;
    [Min(0.05f)] public float destroyFallbackDuration = 0.5f;

    private Rigidbody2D rb;
    private Transform target;
    private bool initialized;
    private bool dying;
    private float startTime;

    private float nextRetargetTime;
    private MovementController[] cachedPlayers = new MovementController[0];
    private Vector2 lastFloatOffset;

    private SunMaskBoss boss;

    public void SetBoss(SunMaskBoss b) => boss = b;

    public void Initialize(float speed, float lifeTime, float floatAmplitude, float floatFrequency)
    {
        if (speed > 0f) this.speed = speed;
        if (lifeTime > 0f) this.lifeTime = lifeTime;
        if (floatAmplitude >= 0f) this.floatAmplitude = floatAmplitude;
        if (floatFrequency >= 0f) this.floatFrequency = floatFrequency;

        initialized = true;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
        }

        var col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;

        ApplyLayerIgnores();
    }

    void ApplyLayerIgnores()
    {
        int myLayer = gameObject.layer;

        if (ignorePlayerLayer)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0 && myLayer >= 0)
                Physics2D.IgnoreLayerCollision(myLayer, playerLayer, true);
        }

        if (ignoreBombLayer)
        {
            int bombLayer = LayerMask.NameToLayer("Bomb");
            if (bombLayer >= 0 && myLayer >= 0)
                Physics2D.IgnoreLayerCollision(myLayer, bombLayer, true);
        }
    }

    void OnEnable()
    {
        dying = false;
        startTime = Time.time;
        nextRetargetTime = 0f;
        lastFloatOffset = Vector2.zero;

        if (destroyRenderer != null)
            destroyRenderer.enabled = false;

        if (floatRenderer != null)
        {
            floatRenderer.enabled = true;
            floatRenderer.idle = false;
            floatRenderer.loop = true;
            floatRenderer.CurrentFrame = 0;
            floatRenderer.RefreshFrame();
        }

        RefreshPlayersCache();
        AcquireNearestTarget();

        if (lifeTime > 0f)
            StartCoroutine(LifeTimer());
    }

    IEnumerator LifeTimer()
    {
        float t = Mathf.Max(0f, lifeTime);
        if (t > 0f)
            yield return new WaitForSeconds(t);

        DestroyByLifetime();
    }

    void DestroyByLifetime()
    {
        if (dying) return;
        dying = true;
        Destroy(gameObject);
    }

    void Update()
    {
        if (dying || !initialized)
            return;

        if (Time.time >= nextRetargetTime || TargetInvalid(target))
        {
            nextRetargetTime = Time.time + Mathf.Max(0.02f, retargetInterval);
            RefreshPlayersCache();
            AcquireNearestTarget();
        }

        Vector2 pos = transform.position;

        Vector2 desired;
        Vector2 to = Vector2.zero;

        if (target != null)
        {
            to = (Vector2)target.position - pos;
            float dist = to.magnitude;

            if (dist <= Mathf.Max(0f, stopNearTargetDistance))
                desired = Vector2.zero;
            else
                desired = to.sqrMagnitude > 0.0001f ? to.normalized : Vector2.zero;
        }
        else
        {
            desired = Vector2.up;
        }

        float t = Time.time - startTime;
        Vector2 perp = desired.sqrMagnitude > 0.0001f ? new Vector2(-desired.y, desired.x) : Vector2.right;

        Vector2 floatOffsetNow = perp * (Mathf.Sin(t * floatFrequency) * floatAmplitude);
        Vector2 floatOffsetDelta = floatOffsetNow - lastFloatOffset;
        lastFloatOffset = floatOffsetNow;

        Vector2 step = desired * (speed * Time.deltaTime);
        Vector2 newPos = pos + step + floatOffsetDelta;

        if (rb != null)
            rb.MovePosition(newPos);
        else
            transform.position = newPos;
    }

    bool TargetInvalid(Transform t)
    {
        if (t == null) return true;

        var m = t.GetComponent<MovementController>();
        if (m == null) return true;

        return m.isDead || m.IsEndingStage;
    }

    void RefreshPlayersCache()
    {
        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        if (ids != null && ids.Length > 0)
        {
            var temp = new List<MovementController>(ids.Length);

            for (int i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                if (id == null) continue;

                MovementController m = null;
                if (!id.TryGetComponent(out m))
                    m = id.GetComponentInChildren<MovementController>(true);

                if (m == null) continue;
                if (!m.CompareTag("Player")) continue;

                temp.Add(m);
            }

            cachedPlayers = temp.ToArray();
            return;
        }

        var go = GameObject.FindGameObjectsWithTag("Player");
        if (go != null && go.Length > 0)
        {
            var temp = new List<MovementController>(go.Length);

            for (int i = 0; i < go.Length; i++)
            {
                if (go[i] == null) continue;

                var m = go[i].GetComponent<MovementController>();
                if (m == null)
                    m = go[i].GetComponentInChildren<MovementController>(true);

                if (m == null) continue;

                temp.Add(m);
            }

            cachedPlayers = temp.ToArray();
            return;
        }

        cachedPlayers = new MovementController[0];
    }

    void AcquireNearestTarget()
    {
        MovementController best = null;
        float bestDist = float.PositiveInfinity;

        Vector2 myPos = transform.position;

        for (int i = 0; i < cachedPlayers.Length; i++)
        {
            var p = cachedPlayers[i];
            if (p == null) continue;
            if (p.isDead || p.IsEndingStage) continue;

            float d = ((Vector2)p.transform.position - myPos).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = p;
            }
        }

        target = best != null ? best.transform : null;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (dying) return;
        if (other == null) return;

        int explosionLayer = LayerMask.NameToLayer("Explosion");
        if (explosionLayer >= 0 && other.gameObject.layer == explosionLayer)
            TriggerDestroy(other);
    }

    public void TriggerDestroy(Collider2D explosionCollider = null)
    {
        if (dying) return;
        dying = true;

        MovementController whoDestroyed = ResolvePlayerFromExplosion(explosionCollider);

        if (boss != null && whoDestroyed != null)
            boss.NotifyHeartDestroyedByPlayer(whoDestroyed);

        if (!playDestroyAnimation || destroyRenderer == null)
        {
            Destroy(gameObject);
            return;
        }

        StartCoroutine(DestroyRoutine());
    }

    MovementController ResolvePlayerFromExplosion(Collider2D explosionCollider)
    {
        if (explosionCollider != null)
        {
            var bc = explosionCollider.GetComponentInParent<BombController>();
            if (bc != null)
            {
                var mc = bc.GetComponent<MovementController>();
                if (mc != null && !mc.isDead && !mc.IsEndingStage)
                    return mc;
            }

            Vector2 origin = explosionCollider.transform.position;
            return FindNearestAlivePlayer(origin);
        }

        return FindNearestAlivePlayer(transform.position);
    }

    MovementController FindNearestAlivePlayer(Vector2 origin)
    {
        RefreshPlayersCache();

        MovementController best = null;
        float bestDist = float.PositiveInfinity;

        for (int i = 0; i < cachedPlayers.Length; i++)
        {
            var p = cachedPlayers[i];
            if (p == null) continue;
            if (p.isDead || p.IsEndingStage) continue;

            float d = ((Vector2)p.transform.position - origin).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = p;
            }
        }

        return best;
    }

    IEnumerator DestroyRoutine()
    {
        if (floatRenderer != null)
            floatRenderer.enabled = false;

        if (destroyRenderer != null)
        {
            destroyRenderer.enabled = true;
            destroyRenderer.idle = false;
            destroyRenderer.loop = false;
            destroyRenderer.CurrentFrame = 0;
            destroyRenderer.RefreshFrame();
        }

        float dur = destroyFallbackDuration;

        if (destroyRenderer != null)
        {
            if (destroyRenderer.useSequenceDuration && destroyRenderer.sequenceDuration > 0f)
                dur = destroyRenderer.sequenceDuration;
            else if (destroyRenderer.animationSprite != null && destroyRenderer.animationSprite.Length > 0)
                dur = destroyRenderer.animationTime * destroyRenderer.animationSprite.Length;
        }

        dur = Mathf.Max(0.05f, dur);
        yield return new WaitForSeconds(dur);

        Destroy(gameObject);
    }
}