using UnityEngine;

[DisallowMultipleComponent]
public class FullFireAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "FullFire";

    [SerializeField] private bool enabledAbility;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    public void Enable()
    {
        enabledAbility = true;
    }

    public void Disable()
    {
        enabledAbility = false;
    }
}
