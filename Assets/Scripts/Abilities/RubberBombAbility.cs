using UnityEngine;

[DisallowMultipleComponent]
public class RubberBombAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "RubberBombBomb";

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
            ab.Disable(PowerBombAbility.AbilityId);
        }
    }

    public void Disable()
    {
        enabledAbility = false;
    }
}