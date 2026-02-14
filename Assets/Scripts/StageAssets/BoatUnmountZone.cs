using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class BoatUnmountZone : MonoBehaviour
{
    [Header("Boat Ref (legacy)")]
    [SerializeField] private BoatRideZone boat;

    [Header("Boats (optional - for multi boat)")]
    [SerializeField] private List<BoatRideZone> boats = new();

    private BoxCollider2D zoneCollider;

    private void Awake()
    {
        zoneCollider = GetComponent<BoxCollider2D>();
        zoneCollider.isTrigger = true;

        // Auto-resolve se não setou nada no Inspector
        if (boat == null)
            boat = GetComponentInParent<BoatRideZone>();
    }

    private void Reset()
    {
        var c = GetComponent<BoxCollider2D>();
        c.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
            return;

        var mc = other.GetComponentInParent<MovementController>();
        if (mc == null)
            return;

        var targetBoat = ResolveBoatForUnmount(mc);
        if (targetBoat == null || !targetBoat.HasRider)
            return;

        if (!targetBoat.IsRider(mc))
            return;

        Vector2 center = zoneCollider != null ? (Vector2)zoneCollider.bounds.center : (Vector2)transform.position;

        if (!targetBoat.TryUnmount(mc))
            return;

        mc.SnapToWorldPoint(center, roundToGrid: true);
    }

    private BoatRideZone ResolveBoatForUnmount(MovementController mc)
    {
        // 1) se veio setado (legacy)
        if (boat != null)
            return boat;

        // 2) se o player está montado, pega pelo mapa rider->boat (multi-boat perfeito)
        if (BoatRideZone.TryGetBoatForRider(mc, out var ridingBoat) && ridingBoat != null)
            return ridingBoat;

        // 3) tenta parent
        var parentBoat = GetComponentInParent<BoatRideZone>();
        if (parentBoat != null)
            return parentBoat;

        // 4) se tem lista, tenta achar qual contém esse rider
        if (boats != null && boats.Count > 0)
        {
            for (int i = 0; i < boats.Count; i++)
            {
                var b = boats[i];
                if (b == null) continue;
                if (b.HasRider && b.IsRider(mc))
                    return b;
            }

            // fallback: primeiro não nulo
            for (int i = 0; i < boats.Count; i++)
                if (boats[i] != null) return boats[i];
        }

        // 5) último fallback: procurar no scene quem está com esse rider
        var all = FindObjectsByType<BoatRideZone>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var b = all[i];
            if (b == null) continue;
            if (b.HasRider && b.IsRider(mc))
                return b;
        }

        return null;
    }
}
