using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class RedBoatUnmountZone : MonoBehaviour
{
    [SerializeField] private RedBoatRideZone boat;

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
        if (boat == null || !boat.HasRider)
            return;

        var mc = other.GetComponentInParent<MovementController>();
        if (mc == null)
            return;

        if (!boat.IsRider(mc))
            return;

        Vector2 center = zoneCollider != null ? (Vector2)zoneCollider.bounds.center : (Vector2)transform.position;

        if (!boat.TryUnmount(mc))
            return;

        mc.SnapToWorldPoint(center, roundToGrid: true);
    }
}
