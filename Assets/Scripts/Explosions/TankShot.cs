using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class TankShot : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float speed = 8f;
    [SerializeField] private LayerMask hitMask;

    [Header("Impact -> Explosion (BombController logic)")]
    [SerializeField, Min(0)] private int explosionRadius = 1;
    [SerializeField] private bool pierceExplosion = false;
    [SerializeField] private bool chainBombOnHit = true;

    private Rigidbody2D _rb;
    private Collider2D _col;
    private Vector2 _dir;
    private GameObject _owner;

    private AnimatedSpriteRenderer _anim;
    private ContactFilter2D _filter;
    private readonly RaycastHit2D[] _castHits = new RaycastHit2D[8];

    private BombController _cachedBombController;
    private AudioSource _cachedSfxSource;

    private bool _impactHandled;

    public void Init(Vector2 dir, float projectileSpeed, LayerMask mask, GameObject owner)
    {
        _dir = dir == Vector2.zero ? Vector2.down : dir.normalized;
        speed = Mathf.Max(0.1f, projectileSpeed);
        hitMask = mask;
        _owner = owner;

        _filter = new ContactFilter2D { useLayerMask = true, useTriggers = true };
        _filter.SetLayerMask(hitMask);

        CacheBombControllerAndSfxSource();

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
        if (_impactHandled)
            return;

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

            HandleImpact(hit.collider, hit.centroid);
            return;
        }

        _rb.MovePosition(_rb.position + _dir * moveDist);
    }

    private void HandleImpact(Collider2D other, Vector2 impactPos)
    {
        if (_impactHandled)
            return;

        _impactHandled = true;

        if (_rb != null)
            _rb.MovePosition(impactPos);

        if (chainBombOnHit && other != null && other.gameObject.layer == LayerMask.NameToLayer("Bomb"))
            TryChainBomb(other);

        TrySpawnExplosion(impactPos);

        Destroy(gameObject);
    }

    private void TryChainBomb(Collider2D other)
    {
        var bombGo = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
        if (bombGo == null)
            return;

        if (bombGo.TryGetComponent<Bomb>(out var bomb) && bomb != null && bomb.Owner != null)
        {
            bomb.Owner.ExplodeBombChained(bombGo);
            return;
        }

        CacheBombControllerAndSfxSource();
        if (_cachedBombController != null)
            _cachedBombController.ExplodeBombChained(bombGo);
    }

    private void TrySpawnExplosion(Vector2 worldPos)
    {
        CacheBombControllerAndSfxSource();
        if (_cachedBombController == null)
            return;

        worldPos.x = Mathf.Round(worldPos.x);
        worldPos.y = Mathf.Round(worldPos.y);

        _cachedBombController.SpawnExplosionCrossForEffectWithTileEffects(
            origin: worldPos,
            radius: Mathf.Max(0, explosionRadius),
            pierce: pierceExplosion,
            sfxSource: _cachedSfxSource
        );
    }

    private void CacheBombControllerAndSfxSource()
    {
        if (_cachedBombController == null)
        {
            if (_owner != null && _owner.TryGetComponent(out _cachedBombController) && _cachedBombController != null)
            {
            }
            else
            {
                var all = FindObjectsByType<BombController>(FindObjectsSortMode.None);
                for (int i = 0; i < all.Length; i++)
                {
                    var bc = all[i];
                    if (bc == null)
                        continue;

                    if (!bc.CompareTag("Player"))
                        continue;

                    _cachedBombController = bc;
                    break;
                }
            }
        }

        if (_cachedSfxSource == null)
        {
            if (_owner != null && _owner.TryGetComponent(out _cachedSfxSource) && _cachedSfxSource != null)
            {
                _cachedSfxSource.playOnAwake = false;
                _cachedSfxSource.spatialBlend = 0f;
            }
            else if (_cachedBombController != null && _cachedBombController.playerAudioSource != null)
            {
                _cachedSfxSource = _cachedBombController.playerAudioSource;
            }
        }
    }
}
