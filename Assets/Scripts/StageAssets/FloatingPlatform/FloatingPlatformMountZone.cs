using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class FloatingPlatformMountZone : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool dbg = true;

    [Header("Platform Ref (legacy)")]
    [SerializeField] private FloatingPlatform platform;

    [Header("Platforms (optional - multi)")]
    [SerializeField] private List<FloatingPlatform> platforms = new();

    private BoxCollider2D _zone;

    private string DbgPrefix => $"[FloatingPlatformMountZone#{GetInstanceID()} '{name}']";

    private void Dbg(string msg)
    {
        if (!dbg) return;
        Debug.Log($"{DbgPrefix} {msg}", this);
    }

    private void Awake()
    {
        _zone = GetComponent<BoxCollider2D>();
        _zone.isTrigger = true;

        if (platform == null)
            platform = GetComponentInParent<FloatingPlatform>();

        Dbg($"Awake. platform={(platform != null ? platform.name : "null")} platformsCount={(platforms != null ? platforms.Count : 0)}");
    }

    private void Reset()
    {
        var c = GetComponent<BoxCollider2D>();
        c.isTrigger = true;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other == null) return;

        var mc = other.GetComponentInParent<MovementController>();
        if (mc == null) return;

        var target = ResolvePlatformForInput(mc);
        if (target == null) return;

        target.TryHandlePlatformInput(mc);
    }

    private FloatingPlatform ResolvePlatformForInput(MovementController mc)
    {
        if (platform != null)
            return platform;

        if (FloatingPlatform.TryGetPlatformForRider(mc, out var riding) && riding != null)
            return riding;

        var parent = GetComponentInParent<FloatingPlatform>();
        if (parent != null)
            return parent;

        if (platforms != null && platforms.Count > 0)
        {
            for (int i = 0; i < platforms.Count; i++)
            {
                var p = platforms[i];
                if (p == null) continue;
                return p;
            }
        }

        var all = FindObjectsByType<FloatingPlatform>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null) return all[i];

        return null;
    }
}
