using UnityEngine;

public sealed class InvisibleLandMineBehavior : MonoBehaviour, IItemPickupBehavior
{
    [Header("Detection")]
    [SerializeField, Min(0.1f)] private float tileSize = 1f;
    [SerializeField, Min(0.5f)] private float triggerDistanceInTiles = 3f;
    [SerializeField, Min(0.02f)] private float scanEverySeconds = 0.1f;

    [Header("Renderers (usually use ItemPickup.idleRenderer)")]
    [SerializeField] private AnimatedSpriteRenderer targetRenderer;

    [Header("Animation When Near")]
    [SerializeField] private bool loopWhenNear = true;

    [Header("Damage")]
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField] private bool playDestroyAnimationOnPickup = true;

    private float _nextScanTime;
    private bool _isNear;

    private void Awake()
    {
        // Se não setar manualmente, tenta puxar do ItemPickup
        if (targetRenderer == null)
        {
            var p = GetComponent<ItemPickup>();
            if (p != null && p.idleRenderer != null)
                targetRenderer = p.idleRenderer;
        }

        ApplyState(force: true);
    }

    private void Update()
    {
        if (Time.time < _nextScanTime)
            return;

        _nextScanTime = Time.time + scanEverySeconds;

        bool near = IsAnyPlayerWithinTiles(triggerDistanceInTiles);
        if (near != _isNear)
        {
            _isNear = near;
            ApplyState(force: false);
        }
    }

    private bool IsAnyPlayerWithinTiles(float tiles)
    {
        float ts = Mathf.Max(0.0001f, tileSize);
        float maxDist = tiles * ts;
        float maxSqr = maxDist * maxDist;

        // Busca players pelo MovementController (você já usa nele como “Player”)
#if UNITY_2023_1_OR_NEWER
        var players = FindObjectsByType<MovementController>(FindObjectsSortMode.None);
#else
        var players = FindObjectsOfType<MovementController>();
#endif
        if (players == null || players.Length == 0)
            return false;

        Vector2 minePos = transform.position;

        for (int i = 0; i < players.Length; i++)
        {
            var mv = players[i];
            if (mv == null) continue;
            if (!mv.CompareTag("Player")) continue;
            if (mv.isDead) continue; // seu MovementController tem isDead público

            Vector2 p = mv.Rigidbody != null ? mv.Rigidbody.position : (Vector2)mv.transform.position;
            if ((p - minePos).sqrMagnitude <= maxSqr)
                return true;
        }

        return false;
    }

    private void ApplyState(bool force)
    {
        if (targetRenderer == null)
            return;

        // “sprite permanece idle” quando ninguém está perto
        if (!_isNear)
        {
            targetRenderer.loop = false;   // tanto faz, idle = true não avança frames
            targetRenderer.idle = true;
            targetRenderer.CurrentFrame = 0;
            targetRenderer.RefreshFrame();
            return;
        }

        // quando alguém chega perto -> anima em loop
        targetRenderer.idle = false;
        targetRenderer.loop = loopWhenNear;
        targetRenderer.pingPong = false;
        // não reseta frame toda hora; só garante que tá rodando
        targetRenderer.RefreshFrame();
    }

    public bool OnPickedUp(ItemPickup pickup, GameObject player)
    {
        if (pickup == null || player == null)
            return true;

        pickup.TryApplyDamageLikeEnemyContact(player, damage);
        pickup.Consume(playDestroyAnimationOnPickup);

        return true;
    }
}