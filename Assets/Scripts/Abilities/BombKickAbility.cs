using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(BombController))]
public class BombKickAbility : MonoBehaviour, IMovementAbility
{
    public const string AbilityId = "BombKick";

    private const string KickClipResourcesPath = "Sounds/KickBomb";
    private static AudioClip cachedKickClip;

    [SerializeField] private bool enabledAbility;

    private AudioSource audioSource;
    private BombController bombController;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        bombController = GetComponent<BombController>();

        if (cachedKickClip == null)
            cachedKickClip = Resources.Load<AudioClip>(KickClipResourcesPath);
    }

    public void Enable()
    {
        enabledAbility = true;

        if (TryGetComponent<AbilitySystem>(out var abilitySystem))
            abilitySystem.Disable(BombPassAbility.AbilityId);
    }

    public void Disable()
    {
        enabledAbility = false;
    }

    public bool TryHandleBlockedHit(Collider2D hit, Vector2 direction, float tileSize, LayerMask obstacleMask)
    {
        if (!enabledAbility)
            return false;

        if (hit == null)
            return false;

        if (hit.gameObject.layer != LayerMask.NameToLayer("Bomb"))
            return false;

        var bomb = hit.GetComponent<Bomb>();
        if (bomb == null || bomb.IsBeingKicked)
            return false;

        if (!bomb.CanBeKicked)
            return false;

        LayerMask bombObstacles = obstacleMask | LayerMask.GetMask("Enemy");

        bool kicked = bomb.StartKick(
            direction,
            tileSize,
            bombObstacles,
            bombController != null ? bombController.destructibleTiles : null
        );

        if (!kicked)
            return false;

        if (audioSource != null && cachedKickClip != null)
            audioSource.PlayOneShot(cachedKickClip);

        return true;
    }
}
