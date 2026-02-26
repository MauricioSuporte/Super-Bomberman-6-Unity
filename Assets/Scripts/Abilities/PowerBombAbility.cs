using UnityEngine;

[DisallowMultipleComponent]
public class PowerBombAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "PowerBomb";

    [SerializeField] private bool enabledAbility;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    public void Enable()
    {
        enabledAbility = true;

        if (TryGetComponent<AbilitySystem>(out var ab) && ab != null)
        {
            ab.Disable(PierceBombAbility.AbilityId);
            ab.Disable(ControlBombAbility.AbilityId);
        }
    }

    public void Disable()
    {
        enabledAbility = false;
    }
}