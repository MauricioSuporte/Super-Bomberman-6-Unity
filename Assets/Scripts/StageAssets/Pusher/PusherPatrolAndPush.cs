using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AnimatedSpriteRenderer))]
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

    [Header("Push Axis")]
    [Tooltip("When true, pushes horizontally (X). When false, pushes vertically (Y).")]
    [SerializeField] private bool pushHorizontallyInsteadOfVertically = false;

    [Header("Vertical Push Filter")]
    [SerializeField, Range(0f, 1f)] private float minVerticalNormal = 0.75f;
    [SerializeField, Range(0f, 1f)] private float maxSideNormal = 0.25f;
    [SerializeField, Range(0f, 1f)] private float minXOverlapPercent = 0.6f;

    [Header("Horizontal Push Filter")]
    [SerializeField, Range(0f, 1f)] private float minHorizontalNormal = 0.75f;
    [SerializeField, Range(0f, 1f)] private float maxUpDownNormal = 0.25f;
    [SerializeField, Range(0f, 1f)] private float minYOverlapPercent = 0.6f;

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

        PushPlayersIfValidContact(delta);

        _rb.MovePosition(pos + delta);
    }

    private void PushPlayersIfValidContact(Vector2 delta)
    {
        float moveDist = delta.magnitude;
        if (moveDist <= 0.00001f)
            return;

        bool horizontal = pushHorizontallyInsteadOfVertically;

        // Só considera push se o delta for majoritário no eixo escolhido
        if (horizontal)
        {
            if (Mathf.Abs(delta.x) <= Mathf.Abs(delta.y))
                return;
        }
        else
        {
            if (Mathf.Abs(delta.y) <= Mathf.Abs(delta.x))
                return;
        }

        Vector2 castDir = delta / moveDist;

        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = playerLayerMask,
            useTriggers = true
        };

        int count = _col.Cast(castDir, filter, _hits, moveDist + castSkin);
        if (count <= 0)
            return;

        Bounds pusherBounds = _col.bounds;

        float pushAmount = horizontal ? delta.x : delta.y;
        if (Mathf.Abs(pushAmount) <= 0.00001f)
            return;

        for (int i = 0; i < count; i++)
        {
            var hit = _hits[i];
            if (hit.collider == null)
                continue;

            Vector2 n = hit.normal;

            if (!IsValidNormalForAxis(horizontal, n, pushAmount))
                continue;

            Bounds playerBounds = hit.collider.bounds;

            if (!HasEnoughOverlapForAxis(horizontal, pusherBounds, playerBounds))
                continue;

            Rigidbody2D playerRb = hit.rigidbody;
            if (playerRb == null)
                playerRb = hit.collider.GetComponentInParent<Rigidbody2D>();

            if (playerRb == null)
                continue;

            Vector2 pushDelta = horizontal ? new Vector2(pushAmount, 0f) : new Vector2(0f, pushAmount);
            playerRb.MovePosition(playerRb.position + pushDelta);

            var resolver = hit.collider.GetComponentInParent<PlayerPushedOutOfInvalidTile>();
            if (resolver != null)
            {
                Vector2 pushDir =
                    horizontal
                        ? (pushAmount > 0f ? Vector2.right : Vector2.left)
                        : (pushAmount > 0f ? Vector2.up : Vector2.down);

                resolver.NotifyExternalPushed(pushDir);
            }
        }
    }

    private bool IsValidNormalForAxis(bool horizontal, Vector2 n, float pushAmount)
    {
        if (horizontal)
        {
            // Normal deve ser majoritariamente horizontal (colisão "de lado")
            if (Mathf.Abs(n.y) > maxUpDownNormal)
                return false;

            // Empurrando pra direita: esperamos normal apontando pra esquerda (-x) no hit
            if (pushAmount > 0f)
            {
                if (n.x > -minHorizontalNormal)
                    return false;
            }
            else
            {
                if (n.x < minHorizontalNormal)
                    return false;
            }

            return true;
        }

        // Vertical (seu comportamento original)
        if (Mathf.Abs(n.x) > maxSideNormal)
            return false;

        if (pushAmount > 0f)
        {
            if (n.y > -minVerticalNormal)
                return false;
        }
        else
        {
            if (n.y < minVerticalNormal)
                return false;
        }

        return true;
    }

    private bool HasEnoughOverlapForAxis(bool horizontal, Bounds pusherBounds, Bounds playerBounds)
    {
        if (horizontal)
        {
            float overlapY = Mathf.Min(pusherBounds.max.y, playerBounds.max.y) - Mathf.Max(pusherBounds.min.y, playerBounds.min.y);
            if (overlapY <= 0f)
                return false;

            float denom = Mathf.Min(pusherBounds.size.y, playerBounds.size.y);
            if (denom <= 0.00001f)
                return false;

            float overlapPercent = overlapY / denom;
            return overlapPercent >= minYOverlapPercent;
        }

        float overlapX = Mathf.Min(pusherBounds.max.x, playerBounds.max.x) - Mathf.Max(pusherBounds.min.x, playerBounds.min.x);
        if (overlapX <= 0f)
            return false;

        float denom2 = Mathf.Min(pusherBounds.size.x, playerBounds.size.x);
        if (denom2 <= 0.00001f)
            return false;

        float overlapPercent2 = overlapX / denom2;
        return overlapPercent2 >= minXOverlapPercent;
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