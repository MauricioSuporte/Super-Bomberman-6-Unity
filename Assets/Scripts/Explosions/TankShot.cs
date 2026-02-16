using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class TankShot : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float speed = 8f;
    [SerializeField] private LayerMask hitMask;

    private Rigidbody2D _rb;
    private Collider2D _col;
    private Vector2 _dir;
    private GameObject _owner;

    private AnimatedSpriteRenderer _anim;
    private ContactFilter2D _filter;
    private readonly RaycastHit2D[] _castHits = new RaycastHit2D[8];

    public void Init(Vector2 dir, float projectileSpeed, LayerMask mask, GameObject owner)
    {
        _dir = dir == Vector2.zero ? Vector2.down : dir.normalized;
        speed = Mathf.Max(0.1f, projectileSpeed);
        hitMask = mask;
        _owner = owner;

        _filter = new ContactFilter2D { useLayerMask = true, useTriggers = true };
        _filter.SetLayerMask(hitMask);

        if (_anim != null)
        {
            _anim.enabled = true;
            _anim.idle = false;
            _anim.loop = true;
            _anim.CurrentFrame = 0;
            _anim.RefreshFrame();

            if (_anim.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = (_dir == Vector2.right);
        }
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();

        if (_rb != null)
        {
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        if (_col != null)
            _col.isTrigger = true;

        _anim = GetComponentInChildren<AnimatedSpriteRenderer>(true);
    }

    private void FixedUpdate()
    {
        if (_rb == null || _col == null)
            return;

        float moveDist = speed * Time.fixedDeltaTime;

        int hitCount = _col.Cast(_dir, _filter, _castHits, moveDist);

        for (int i = 0; i < hitCount; i++)
        {
            var hit = _castHits[i];
            if (hit.collider == null)
                continue;

            if (_owner != null && hit.collider.gameObject == _owner)
                continue;

            _rb.MovePosition(hit.centroid);

            int layer = hit.collider.gameObject.layer;

            if (layer == LayerMask.NameToLayer("Player"))
            {
                if (hit.collider.TryGetComponent<CharacterHealth>(out var h))
                    h.TakeDamage(1);
            }

            Destroy(gameObject);
            return;
        }

        _rb.MovePosition(_rb.position + _dir * moveDist);
    }
}
