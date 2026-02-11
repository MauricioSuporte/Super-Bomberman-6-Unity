using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ClownMaskMovement : MonoBehaviour
{
    [Header("Movimento")]
    public float speed = 2.5f;
    public float changeDirectionInterval = 1.5f;
    [Range(0f, 1f)]
    public float chaseWeight = 0.7f;

    [Header("Targeting (Multiplayer)")]
    [Tooltip("How often (seconds) the boss re-evaluates the closest alive player.")]
    public float retargetInterval = 0.25f;

    [Header("Parar ao sofrer dano")]
    public float defaultHitStopDuration = 0.8f;

    [Header("Limites opcionais do mapa")]
    public bool useBounds = false;
    public Vector2 minBounds;
    public Vector2 maxBounds;

    Rigidbody2D rb;
    Transform player;

    Vector2 currentDirection = Vector2.zero;
    float nextDecisionTime;
    float hitStopTimer;

    float retargetTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        PickClosestAlivePlayer();
        retargetTimer = retargetInterval;
    }

    void OnEnable()
    {
        nextDecisionTime = Time.time;
        hitStopTimer = 0f;
        currentDirection = Vector2.zero;

        PickClosestAlivePlayer();
        retargetTimer = retargetInterval;
    }

    void FixedUpdate()
    {
        if (GamePauseController.IsPaused)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (retargetInterval > 0f)
        {
            retargetTimer -= Time.fixedDeltaTime;
            if (retargetTimer <= 0f)
            {
                PickClosestAlivePlayer();
                retargetTimer = retargetInterval;
            }
        }

        if (hitStopTimer > 0f)
        {
            hitStopTimer -= Time.fixedDeltaTime;
            if (hitStopTimer < 0f)
                hitStopTimer = 0f;

            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (Time.time >= nextDecisionTime)
            DecideNewDirection();

        rb.linearVelocity = currentDirection * speed;

        if (useBounds)
        {
            Vector2 pos = rb.position;
            pos.x = Mathf.Clamp(pos.x, minBounds.x, maxBounds.x);
            pos.y = Mathf.Clamp(pos.y, minBounds.y, maxBounds.y);
            rb.position = pos;
        }
    }

    void PickClosestAlivePlayer()
    {
        Vector2 myPos = rb.position;

        Transform best = null;
        float bestDist = float.PositiveInfinity;

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
                if (!m.isActiveAndEnabled || !m.gameObject.activeInHierarchy) continue;
                if (!m.CompareTag("Player")) continue;
                if (m.isDead) continue;

                float dist = ((Vector2)m.transform.position - myPos).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = m.transform;
                }
            }
        }
        else
        {
            var players = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                var m = players[i];
                if (m == null) continue;
                if (!m.CompareTag("Player")) continue;
                if (!m.isActiveAndEnabled || !m.gameObject.activeInHierarchy) continue;
                if (m.isDead) continue;

                float dist = ((Vector2)m.transform.position - myPos).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = m.transform;
                }
            }
        }

        player = best;
    }

    void DecideNewDirection()
    {
        Vector2 toPlayer = Vector2.zero;

        if (player != null)
        {
            toPlayer = (Vector2)player.position - rb.position;
            if (toPlayer.sqrMagnitude > 0.001f)
                toPlayer.Normalize();
        }

        Vector2 randomDir = Random.insideUnitCircle;
        if (randomDir.sqrMagnitude < 0.001f)
            randomDir = Vector2.up;
        randomDir.Normalize();

        float wChase = Mathf.Clamp01(chaseWeight);
        float wRandom = 1f - wChase;

        Vector2 newDir = toPlayer * wChase + randomDir * wRandom;
        if (newDir.sqrMagnitude < 0.001f)
            newDir = randomDir;

        currentDirection = newDir.normalized;

        float randomOffset = Random.Range(-0.4f, 0.4f);
        nextDecisionTime = Time.time + Mathf.Max(0.1f, changeDirectionInterval + randomOffset);
    }

    public void OnHit(float duration = -1f)
    {
        if (duration <= 0f)
            duration = defaultHitStopDuration;

        if (duration <= 0f)
            return;

        hitStopTimer = Mathf.Max(hitStopTimer, duration);
        rb.linearVelocity = Vector2.zero;
    }
}
