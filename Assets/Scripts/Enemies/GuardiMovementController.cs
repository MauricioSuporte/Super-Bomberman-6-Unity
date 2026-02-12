using UnityEngine;

public sealed class GuardiMovementController : JunctionTurningEnemyMovementController
{
    [Header("Firework Death Rule")]
    [SerializeField] private bool dieWhenFireworksCleared = true;

    void OnEnable()
    {
        FireworkTileHandler.AllFireworksDestroyedAtPhase2Start += HandleAllFireworksDestroyedAtPhase2Start;
    }

    void OnDisable()
    {
        FireworkTileHandler.AllFireworksDestroyedAtPhase2Start -= HandleAllFireworksDestroyedAtPhase2Start;
    }

    void HandleAllFireworksDestroyedAtPhase2Start()
    {
        if (!dieWhenFireworksCleared)
            return;

        if (isDead)
            return;

        Kill();
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead)
            return;

        int explosionLayer = LayerMask.NameToLayer("Explosion");
        if (explosionLayer >= 0 && other.gameObject.layer == explosionLayer)
            return;

        base.OnTriggerEnter2D(other);
    }
}
