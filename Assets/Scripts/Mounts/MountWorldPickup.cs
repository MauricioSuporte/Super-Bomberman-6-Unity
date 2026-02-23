using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class MountWorldPickup : MonoBehaviour
{
    [Header("Pickup")]
    [SerializeField] string playerTag = "Player";

    [Header("Type")]
    [SerializeField] MountedType type = MountedType.None;

    bool consumed;
    Collider2D _col;

    public void Init(MountedType t) => type = t;

    void Awake()
    {
        _col = GetComponent<Collider2D>();
        if (_col != null)
            _col.enabled = true;
    }

    static bool PlayerHoldingBombWithPowerGlove(GameObject player)
    {
        if (player == null)
            return false;

        if (!player.TryGetComponent<AbilitySystem>(out var ab) || ab == null)
            return false;

        ab.RebuildCache();

        var glove = ab.Get<PowerGloveAbility>(PowerGloveAbility.AbilityId);
        return glove != null && glove.IsEnabled && glove.IsHoldingBomb;
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

        if (PlayerHoldingBombWithPowerGlove(player))
            return;

        if (!player.TryGetComponent<PlayerMountCompanion>(out var comp) || comp == null)
            return;

        bool alreadyMounted =
            (player.TryGetComponent<MovementController>(out var m2) && m2 != null && m2.IsMountedOnLouie) ||
            (player.TryGetComponent<PlayerMountCompanion>(out var c2) && c2 != null && c2.HasMountedLouie());

        if (alreadyMounted)
            return;

        if (type == MountedType.None)
            type = ResolveTypeFromNameFallback(gameObject.name);

        if (type == MountedType.None)
            return;

        if (_col != null)
            mv.SnapToColliderCenter(_col, roundToGrid: false);
        else
            mv.SnapToWorldPoint(transform.position, roundToGrid: false);

        consumed = true;

        var worldQueue = GetComponent<MountEggQueue>();

        comp.TryMountExistingLouieFromWorld(
            louieWorldInstance: gameObject,
            louieType: type,
            worldQueueToAdopt: worldQueue
        );
    }

    static MountedType ResolveTypeFromNameFallback(string n)
    {
        if (string.IsNullOrEmpty(n)) return MountedType.None;

        n = n.ToLowerInvariant();

        if (n.Contains("bluelouie")) return MountedType.Blue;
        if (n.Contains("blacklouie")) return MountedType.Black;
        if (n.Contains("purplelouie")) return MountedType.Purple;
        if (n.Contains("greenlouie")) return MountedType.Green;
        if (n.Contains("yellowlouie")) return MountedType.Yellow;
        if (n.Contains("pinklouie")) return MountedType.Pink;
        if (n.Contains("redlouie")) return MountedType.Red;

        return MountedType.None;
    }
}