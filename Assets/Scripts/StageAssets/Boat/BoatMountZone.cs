using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class BoatMountZone : MonoBehaviour
{
    [Header("Boat Ref (legacy)")]
    [SerializeField] private BoatRideZone boat;

    [Header("Boats (optional - for multi boat)")]
    [SerializeField] private List<BoatRideZone> boats = new();

    [Header("Snap On Mount")]
    [SerializeField] private bool snapPlayerOnMount = true;
    [SerializeField] private bool roundToGrid = true;

    private BoxCollider2D zoneCollider;

    public BoatRideZone Boat => boat;

    public bool ReferencesBoat(BoatRideZone target)
    {
        if (target == null) return false;

        if (boat == target) return true;

        if (boats != null && boats.Count > 0)
        {
            for (int i = 0; i < boats.Count; i++)
                if (boats[i] == target) return true;
        }

        var parentBoat = GetComponentInParent<BoatRideZone>();
        return parentBoat == target;
    }

    private void Awake()
    {
        zoneCollider = GetComponent<BoxCollider2D>();
        zoneCollider.isTrigger = true;

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
        if (other == null) return;

        var mc = other.GetComponentInParent<MovementController>();
        if (mc == null)
            return;

        Vector2 zoneCenter = GetZoneCenterWorld();

        var targetBoat = ResolveBoatForMount(mc, zoneCenter, out _);
        if (targetBoat == null)
            return;

        if (!targetBoat.IsAnchoredAt(zoneCenter, out _))
            return;

        if (!targetBoat.CanMount(mc, out _))
            return;

        if (snapPlayerOnMount)
        {
            float tile = mc.tileSize > 0.0001f ? mc.tileSize : 1f;
            Vector2 target = zoneCenter + Vector2.down * tile;
            mc.SnapToWorldPoint(target, roundToGrid);
        }

        targetBoat.TryMount(mc, out _);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null) return;

        var mc = other.GetComponentInParent<MovementController>();
        if (mc == null) return;

        ClearRemountBlocksForAllCandidates(mc);
    }

    private int ClearRemountBlocksForAllCandidates(MovementController mc)
    {
        var unique = new HashSet<BoatRideZone>();

        if (boat != null) unique.Add(boat);

        var parentBoat = GetComponentInParent<BoatRideZone>();
        if (parentBoat != null) unique.Add(parentBoat);

        if (boats != null && boats.Count > 0)
        {
            for (int i = 0; i < boats.Count; i++)
                if (boats[i] != null) unique.Add(boats[i]);
        }

        if (unique.Count == 0)
        {
            var all = FindObjectsByType<BoatRideZone>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null) unique.Add(all[i]);
        }

        int cleared = 0;
        foreach (var b in unique)
        {
            if (b == null) continue;

            bool wasBlocked = b.IsRemountBlockedFor(mc);
            b.ClearRemountBlock(mc);
            bool stillBlocked = b.IsRemountBlockedFor(mc);

            if (wasBlocked && !stillBlocked) cleared++;
        }

        return cleared;
    }

    private BoatRideZone ResolveBoatForMount(MovementController mc, Vector2 zoneCenter, out string trace)
    {
        var candidates = new List<BoatRideZone>(8);

        if (boat != null) candidates.Add(boat);

        var parentBoat = GetComponentInParent<BoatRideZone>();
        if (parentBoat != null && !candidates.Contains(parentBoat))
            candidates.Add(parentBoat);

        if (boats != null && boats.Count > 0)
        {
            for (int i = 0; i < boats.Count; i++)
            {
                var b = boats[i];
                if (b != null && !candidates.Contains(b))
                    candidates.Add(b);
            }
        }

        if (candidates.Count == 0)
        {
            var all = FindObjectsByType<BoatRideZone>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var b = all[i];
                if (b != null) candidates.Add(b);
            }
        }

        if (candidates.Count == 0)
        {
            trace = "none";
            return null;
        }

        BoatRideZone best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            var b = candidates[i];
            if (b == null) continue;

            if (!b.IsAnchoredAt(zoneCenter, out _))
                continue;

            if (!b.CanMount(mc, out _))
                continue;

            float d = Vector2.Distance(zoneCenter, (Vector2)b.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = b;
            }
        }

        if (best != null)
        {
            trace = $"anchored+can(bestDist={bestDist:0.000})";
            return best;
        }

        best = null;
        bestDist = float.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            var b = candidates[i];
            if (b == null) continue;

            float d = Vector2.Distance(zoneCenter, (Vector2)b.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = b;
            }
        }

        trace = best != null ? $"fallback(bestDist={bestDist:0.000})" : "fallback(null)";
        return best;
    }

    public Vector2 GetZoneCenterWorld()
    {
        return zoneCollider != null ? (Vector2)zoneCollider.bounds.center : (Vector2)transform.position;
    }
}
