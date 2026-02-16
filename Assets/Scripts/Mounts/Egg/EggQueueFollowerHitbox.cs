using UnityEngine;

public sealed class EggQueueFollowerHitbox : MonoBehaviour
{
    [Header("Explosion Destroy")]
    [SerializeField] private bool useTag = true;
    [SerializeField] private string explosionTag = "Explosion";
    [SerializeField] private string explosionLayerName = "Explosion";

    [Header("Consume Egg On Player Collision")]
    [SerializeField] private bool enableConsumeByPlayer = true;
    [SerializeField] private string playerTag = "Player";

    MountEggQueue ownerQueue;
    bool requestedExplosion;
    bool requestedConsume;

    public void Bind(MountEggQueue q) => ownerQueue = q;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (ownerQueue == null || other == null)
            return;

        if (!requestedExplosion && IsExplosion(other))
        {
            if (ownerQueue.OwnerIsInvulnerable)
                return;

            requestedExplosion = true;
            ownerQueue.RequestDestroyEgg(transform);
            return;
        }

        if (!enableConsumeByPlayer || requestedConsume)
            return;

        if (!other.CompareTag(playerTag))
            return;

        bool consumed = ownerQueue.RequestConsumeEggForMountCollision(transform, other.gameObject);
        requestedConsume = consumed;
    }

    bool IsExplosion(Collider2D other)
    {
        return useTag
            ? other.CompareTag(explosionTag)
            : other.gameObject.layer == LayerMask.NameToLayer(explosionLayerName);
    }
}
