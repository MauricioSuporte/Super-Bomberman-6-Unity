using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class RedBoatMountZone : MonoBehaviour
{
    [SerializeField] private RedBoatRideZone boat;

    [Header("Snap On Mount")]
    [SerializeField] private bool snapPlayerOnMount = true;
    [SerializeField] private bool roundToGrid = true;

    private BoxCollider2D zoneCollider;

    public RedBoatRideZone Boat => boat;

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
        if (boat == null || other == null)
            return;

        var mc = other.GetComponentInParent<MovementController>();
        if (mc == null)
            return;

        Vector2 zoneCenter = GetZoneCenterWorld();

        if (!boat.IsAnchoredAt(zoneCenter, out _))
            return;

        if (!boat.CanMount(mc, out _))
            return;

        if (snapPlayerOnMount)
        {
            float tile = mc.tileSize > 0.0001f ? mc.tileSize : 1f;
            Vector2 target = zoneCenter + Vector2.down * tile;
            mc.SnapToWorldPoint(target, roundToGrid);
        }

        boat.TryMount(mc, out _);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (boat == null || other == null) return;

        var mc = other.GetComponentInParent<MovementController>();
        if (mc == null) return;

        boat.ClearRemountBlock(mc);
    }

    public Vector2 GetZoneCenterWorld()
    {
        return zoneCollider != null ? (Vector2)zoneCollider.bounds.center : (Vector2)transform.position;
    }
}
