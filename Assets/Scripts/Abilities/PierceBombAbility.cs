using UnityEngine;

[DisallowMultipleComponent]
public class PierceBombAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "PierceBomb";

    [SerializeField] private bool enabledAbility;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    public void Enable()
    {
        enabledAbility = true;

        if (TryGetComponent<AbilitySystem>(out var abilitySystem))
        {
            abilitySystem.Disable(ControlBombAbility.AbilityId);
            abilitySystem.Disable(PowerBombAbility.AbilityId);
        }
    }

    public void Disable()
    {
        enabledAbility = false;
    }
}
