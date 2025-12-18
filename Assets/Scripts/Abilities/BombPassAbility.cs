using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MovementController))]
public class BombPassAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "BombPass";

    [SerializeField] private bool enabledAbility;

    private MovementController movement;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    private void Awake()
    {
        movement = GetComponent<MovementController>();
    }

    public void Enable()
    {
        enabledAbility = true;

        if (movement != null)
            movement.obstacleMask &= ~LayerMask.GetMask("Bomb");

        if (TryGetComponent<AbilitySystem>(out var abilitySystem))
            abilitySystem.Disable(BombKickAbility.AbilityId);

        if (CompareTag("Player"))
            PlayerPersistentStats.CanPassBombs = true;
    }

    public void Disable()
    {
        enabledAbility = false;

        if (movement != null)
            movement.obstacleMask |= LayerMask.GetMask("Bomb");

        if (CompareTag("Player"))
            PlayerPersistentStats.CanPassBombs = false;
    }
}
