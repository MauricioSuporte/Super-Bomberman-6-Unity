using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class PusherPatrolAndPush : MonoBehaviour
{
    [Header("Path (A <-> B)")]
    [SerializeField] private Vector2 pointA;
    [SerializeField] private Vector2 pointB;

    [Header("Movement")]
    [SerializeField, Min(0.01f)] private float speed = 2f;
    [SerializeField] private bool startGoingToB = true;
    [SerializeField, Min(0f)] private float arriveDistance = 0.02f;

    [Header("Push")]
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField, Min(0f)] private float castSkin = 0.02f;

    [Header("Vertical Push Filter")]
    [SerializeField, Range(0f, 1f)] private float minVerticalNormal = 0.75f;
    [SerializeField, Range(0f, 1f)] private float maxSideNormal = 0.25f;
    [SerializeField, Range(0f, 1f)] private float minXOverlapPercent = 0.6f;

    [Header("Animation")]
    [SerializeField] private AnimatedSpriteRenderer animatedSprite;
    [SerializeField] private bool forceAnimatedLoopAlways = true;

    private Rigidbody2D _rb;
    private Collider2D _col;
    private bool _toB;

    private readonly RaycastHit2D[] _hits = new RaycastHit2D[8];

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();

        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;

        _toB = startGoingToB;

        if (animatedSprite == null)
            animatedSprite = GetComponent<AnimatedSpriteRenderer>();

        ApplyAnimationPolicy();
    }

    private void OnEnable()
    {
        ApplyAnimationPolicy();
    }

    private void FixedUpdate()
    {
        Vector2 pos = _rb.position;

        Vector2 target = _toB ? pointB : pointA;
        Vector2 toTarget = target - pos;

        float dist = toTarget.magnitude;
        if (dist <= arriveDistance)
        {
            _toB = !_toB;
            return;
        }

        Vector2 dir = toTarget / dist;

        float step = speed * Time.fixedDeltaTime;
        if (step > dist) step = dist;

        Vector2 delta = dir * step;

        PushPlayersIfValidVerticalContact(delta);

        _rb.MovePosition(pos + delta);
    }

    private void PushPlayersIfValidVerticalContact(Vector2 delta)
    {
        float moveDist = delta.magnitude;
        if (moveDist <= 0.00001f)
            return;

        if (Mathf.Abs(delta.y) <= Mathf.Abs(delta.x))
            return;

        Vector2 dir = delta / moveDist;

        var filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = playerLayerMask;
        filter.useTriggers = true;

        int count = _col.Cast(dir, filter, _hits, moveDist + castSkin);
        if (count <= 0)
            return;

        float pushY = delta.y;
        if (Mathf.Abs(pushY) <= 0.00001f)
            return;

        Bounds pusherBounds = _col.bounds;

        for (int i = 0; i < count; i++)
        {
            var hit = _hits[i];
            if (hit.collider == null)
                continue;

            Vector2 n = hit.normal;
            if (Mathf.Abs(n.x) > maxSideNormal)
                continue;

            if (pushY > 0f)
            {
                if (n.y > -minVerticalNormal)
                    continue;
            }
            else
            {
                if (n.y < minVerticalNormal)
                    continue;
            }

            Bounds playerBounds = hit.collider.bounds;

            float overlapX = Mathf.Min(pusherBounds.max.x, playerBounds.max.x) - Mathf.Max(pusherBounds.min.x, playerBounds.min.x);
            if (overlapX <= 0f)
                continue;

            float denom = Mathf.Min(pusherBounds.size.x, playerBounds.size.x);
            if (denom <= 0.00001f)
                continue;

            float overlapPercent = overlapX / denom;
            if (overlapPercent < minXOverlapPercent)
                continue;

            Rigidbody2D playerRb = hit.rigidbody;
            if (playerRb == null)
                playerRb = hit.collider.GetComponentInParent<Rigidbody2D>();

            if (playerRb == null)
                continue;

            playerRb.MovePosition(playerRb.position + new Vector2(0f, pushY));
        }
    }

    private void ApplyAnimationPolicy()
    {
        if (!forceAnimatedLoopAlways || animatedSprite == null)
            return;

        animatedSprite.loop = true;
        animatedSprite.idle = false;
        animatedSprite.RefreshFrame();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawLine(pointA, pointB);
        Gizmos.DrawWireSphere(pointA, 0.08f);
        Gizmos.DrawWireSphere(pointB, 0.08f);
    }
#endif
}
