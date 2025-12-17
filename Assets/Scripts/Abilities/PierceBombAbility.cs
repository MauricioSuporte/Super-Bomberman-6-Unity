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
    }

    public void Disable()
    {
        enabledAbility = false;
    }
}
