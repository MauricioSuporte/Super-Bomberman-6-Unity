using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class RedBoatMountZone : MonoBehaviour
{
    [SerializeField] private RedBoatRideZone boat;

    [Header("Snap On Mount")]
    [SerializeField] private bool snapPlayerOnMount = true;
    [SerializeField] private bool roundToGrid = true;

    private BoxCollider2D zoneCollider;

    private void Awake()
    {
        zoneCollider = GetComponent<BoxCollider2D>();
        zoneCollider.isTrigger = true;
    }

    private void Reset()
    {
        var c = GetComponent<BoxCollider2D>();
        c.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (boat == null) return;

        var mc = other.GetComponentInParent<MovementController>();
        if (mc == null) return;

        if (!boat.CanMount(mc))
            return;

        if (snapPlayerOnMount)
        {
            float tile = mc.tileSize > 0.0001f ? mc.tileSize : 1f;
            Vector2 center = zoneCollider != null ? (Vector2)zoneCollider.bounds.center : (Vector2)transform.position;
            Vector2 target = center + Vector2.down * tile;

            mc.SnapToWorldPoint(target, roundToGrid);
        }

        boat.TryMount(mc);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (boat == null) return;

        var mc = other.GetComponentInParent<MovementController>();
        if (mc == null) return;

        boat.ClearRemountBlock(mc);
    }
}
