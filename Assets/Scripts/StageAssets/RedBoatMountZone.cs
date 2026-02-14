using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class RedBoatMountZone : MonoBehaviour
{
    [Header("Boat Ref (legacy)")]
    [SerializeField] private RedBoatRideZone boat;

    [Header("Boats (optional - for multi boat)")]
    [SerializeField] private List<RedBoatRideZone> boats = new();

    [Header("Snap On Mount")]
    [SerializeField] private bool snapPlayerOnMount = true;
    [SerializeField] private bool roundToGrid = true;

    [Header("Debug")]
    [SerializeField] private bool debugMountZone = true;

    private BoxCollider2D zoneCollider;

    // Legacy-only (mantido)
    public RedBoatRideZone Boat => boat;

    // Usado pelo RideZone para cachear anchors mesmo sem legacy
    public bool ReferencesBoat(RedBoatRideZone target)
    {
        if (target == null) return false;

        if (boat == target) return true;

        if (boats != null && boats.Count > 0)
        {
            for (int i = 0; i < boats.Count; i++)
                if (boats[i] == target) return true;
        }

        var parentBoat = GetComponentInParent<RedBoatRideZone>();
        return parentBoat == target;
    }

    private void MLog(string msg)
    {
        if (!debugMountZone) return;
        Debug.Log($"[BoatMountDbg] zone='{name}' t={Time.time:0.00} f={Time.frameCount} {msg}", this);
    }

    private static string BName(Object o) => o == null ? "NULL" : o.name;

    private void Awake()
    {
        zoneCollider = GetComponent<BoxCollider2D>();
        zoneCollider.isTrigger = true;

        if (boat == null)
        {
            boat = GetComponentInParent<RedBoatRideZone>();
            MLog($"Awake: legacy boat was NULL, parentBoat={BName(boat)}");
        }
        else
        {
            MLog($"Awake: legacy boat set: {BName(boat)}");
        }

        MLog($"Awake: boats list count={(boats == null ? -1 : boats.Count)}");
        if (boats != null)
        {
            for (int i = 0; i < boats.Count; i++)
                MLog($"Awake: boats[{i}]={BName(boats[i])}");
        }
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
        {
            MLog($"Enter: other='{other.name}' -> MovementController NULL (GetComponentInParent).");
            return;
        }

        Vector2 zoneCenter = GetZoneCenterWorld();
        MLog($"Enter: other='{other.name}' mc='{mc.name}' zoneCenter={zoneCenter} legacyBoat={BName(boat)}");

        var targetBoat = ResolveBoatForMount(mc, zoneCenter, out var resolveTrace);
        MLog($"ResolveBoatForMount => target={BName(targetBoat)} trace={resolveTrace}");

        if (targetBoat == null)
        {
            MLog("ABORT: targetBoat NULL");
            return;
        }

        if (!targetBoat.IsAnchoredAt(zoneCenter, out var anchorReason))
        {
            float dist = Vector2.Distance(zoneCenter, targetBoat.transform.position);
            MLog($"ABORT: IsAnchoredAt=false reason='{anchorReason}' dist(zoneCenter, boat.pos)={dist:0.000} boat.pos={(Vector2)targetBoat.transform.position}");
            return;
        }

        if (!targetBoat.CanMount(mc, out var canReason))
        {
            MLog($"ABORT: CanMount=false reason='{canReason}'");
            return;
        }

        if (snapPlayerOnMount)
        {
            float tile = mc.tileSize > 0.0001f ? mc.tileSize : 1f;
            Vector2 target = zoneCenter + Vector2.down * tile;
            MLog($"SnapPlayerOnMount: tile={tile:0.###} target={target} roundToGrid={roundToGrid}");
            mc.SnapToWorldPoint(target, roundToGrid);
        }

        bool mounted = targetBoat.TryMount(mc, out var mountReason);
        MLog($"TryMount => {mounted} reason='{mountReason}' targetBoat={BName(targetBoat)}");
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null) return;

        var mc = other.GetComponentInParent<MovementController>();
        if (mc == null) return;

        // IMPORTANT: não dá pra “resolver 1 barco” com segurança (você pode ter montado no (1))
        // então limpamos o remount block em TODOS os candidatos.
        int clearedCount = ClearRemountBlocksForAllCandidates(mc);
        MLog($"Exit: mc='{mc.name}' ClearRemountBlocks candidatesCleared={clearedCount}");
    }

    private int ClearRemountBlocksForAllCandidates(MovementController mc)
    {
        var unique = new HashSet<RedBoatRideZone>();

        // 1) legacy
        if (boat != null) unique.Add(boat);

        // 2) parent
        var parentBoat = GetComponentInParent<RedBoatRideZone>();
        if (parentBoat != null) unique.Add(parentBoat);

        // 3) lista
        if (boats != null && boats.Count > 0)
        {
            for (int i = 0; i < boats.Count; i++)
                if (boats[i] != null) unique.Add(boats[i]);
        }

        // 4) fallback: scene
        if (unique.Count == 0)
        {
            var all = FindObjectsByType<RedBoatRideZone>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null) unique.Add(all[i]);
        }

        int cleared = 0;
        foreach (var b in unique)
        {
            if (b == null) continue;

            // log antes/depois (pra você enxergar exatamente quem estava bloqueando)
            bool wasBlocked = b.IsRemountBlockedFor(mc);
            b.ClearRemountBlock(mc);
            bool stillBlocked = b.IsRemountBlockedFor(mc);

            if (wasBlocked && !stillBlocked) cleared++;

            if (wasBlocked || debugMountZone)
                MLog($"Exit: ClearRemountBlock boat='{b.name}' wasBlocked={wasBlocked} nowBlocked={stillBlocked}");
        }

        return cleared;
    }

    private RedBoatRideZone ResolveBoatForMount(MovementController mc, Vector2 zoneCenter, out string trace)
    {
        if (boat != null)
        {
            trace = "legacy";
            return boat;
        }

        var parentBoat = GetComponentInParent<RedBoatRideZone>();
        if (parentBoat != null)
        {
            trace = "parent";
            return parentBoat;
        }

        if (boats != null && boats.Count > 0)
        {
            RedBoatRideZone best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < boats.Count; i++)
            {
                var b = boats[i];
                if (b == null) continue;

                float d = Vector2.Distance(zoneCenter, b.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = b;
                }
            }

            trace = best != null ? $"list(bestDist={bestDist:0.000})" : "list(all null)";
            return best;
        }

        var allBoats = FindObjectsByType<RedBoatRideZone>(FindObjectsSortMode.None);
        if (allBoats == null || allBoats.Length == 0)
        {
            trace = "scene(no boats found)";
            return null;
        }

        {
            RedBoatRideZone best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < allBoats.Length; i++)
            {
                var b = allBoats[i];
                if (b == null) continue;

                float d = Vector2.Distance(zoneCenter, b.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = b;
                }
            }

            trace = best != null ? $"scene(bestDist={bestDist:0.000})" : "scene(all null)";
            return best;
        }
    }

    public Vector2 GetZoneCenterWorld()
    {
        return zoneCollider != null ? (Vector2)zoneCollider.bounds.center : (Vector2)transform.position;
    }
}
