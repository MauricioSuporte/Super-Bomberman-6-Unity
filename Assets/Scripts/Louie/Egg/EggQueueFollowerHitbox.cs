using UnityEngine;

public sealed class EggQueueFollowerHitbox : MonoBehaviour
{
    [SerializeField] private bool useTag = true;
    [SerializeField] private string explosionTag = "Explosion";
    [SerializeField] private string explosionLayerName = "Explosion";

    LouieEggQueue ownerQueue;
    bool requested;

    public void Bind(LouieEggQueue q) => ownerQueue = q;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (requested) return;
        if (ownerQueue == null) return;
        if (other == null) return;

        bool isExplosion =
            useTag
                ? other.CompareTag(explosionTag)
                : other.gameObject.layer == LayerMask.NameToLayer(explosionLayerName);

        if (!isExplosion) return;

        requested = true;
        ownerQueue.RequestDestroyEgg(transform);
    }
}
