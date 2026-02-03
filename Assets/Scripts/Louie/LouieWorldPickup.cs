using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class LouieWorldPickup : MonoBehaviour
{
    [Header("Pickup")]
    [SerializeField] string playerTag = "Player";

    [Header("Type")]
    [SerializeField] MountedLouieType type = MountedLouieType.None;

    bool consumed;
    Collider2D _col;

    public void Init(MountedLouieType t) => type = t;

    void Awake()
    {
        _col = GetComponent<Collider2D>();
        if (_col != null)
            _col.enabled = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || other == null)
            return;

        if (!other.CompareTag(playerTag))
            return;

        var player = other.gameObject;

        if (!player.TryGetComponent<MovementController>(out var mv) || mv == null)
            return;

        if (mv.isDead || GamePauseController.IsPaused)
            return;

        if (player.TryGetComponent<PlayerRidingController>(out var rider) && rider != null && rider.IsPlaying)
            return;

        if (!player.TryGetComponent<PlayerLouieCompanion>(out var comp) || comp == null)
            return;

        bool alreadyMounted =
            (player.TryGetComponent<MovementController>(out var m2) && m2 != null && m2.IsMountedOnLouie) ||
            (player.TryGetComponent<PlayerLouieCompanion>(out var c2) && c2 != null && c2.HasMountedLouie());

        if (alreadyMounted)
            return;

        if (type == MountedLouieType.None)
            type = ResolveTypeFromNameFallback(gameObject.name);

        if (type == MountedLouieType.None)
            return;

        if (_col != null)
            mv.SnapToColliderCenter(_col, roundToGrid: false);
        else
            mv.SnapToWorldPoint(transform.position, roundToGrid: false);

        consumed = true;

        var worldQueue = GetComponent<LouieEggQueue>();

        comp.TryMountExistingLouieFromWorld(
            louieWorldInstance: gameObject,
            louieType: type,
            worldQueueToAdopt: worldQueue
        );
    }

    static MountedLouieType ResolveTypeFromNameFallback(string n)
    {
        if (string.IsNullOrEmpty(n)) return MountedLouieType.None;

        n = n.ToLowerInvariant();

        if (n.Contains("bluelouie")) return MountedLouieType.Blue;
        if (n.Contains("blacklouie")) return MountedLouieType.Black;
        if (n.Contains("purplelouie")) return MountedLouieType.Purple;
        if (n.Contains("greenlouie")) return MountedLouieType.Green;
        if (n.Contains("yellowlouie")) return MountedLouieType.Yellow;
        if (n.Contains("pinklouie")) return MountedLouieType.Pink;
        if (n.Contains("redlouie")) return MountedLouieType.Red;

        return MountedLouieType.None;
    }
}
