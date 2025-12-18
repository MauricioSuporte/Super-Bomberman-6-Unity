using UnityEngine;

[DisallowMultipleComponent]
public class ControlBombAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "ControlBomb";

    [SerializeField] private bool enabledAbility;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    public void Enable()
    {
        enabledAbility = true;

        if (TryGetComponent<AbilitySystem>(out var abilitySystem))
            abilitySystem.Disable(PierceBombAbility.AbilityId);
    }

    public void Disable()
    {
        enabledAbility = false;
    }
}
