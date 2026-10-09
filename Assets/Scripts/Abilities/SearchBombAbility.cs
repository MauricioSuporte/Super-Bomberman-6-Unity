using UnityEngine;

[DisallowMultipleComponent]
public sealed class SearchBombAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "SearchBomb";
    [SerializeField] private bool enabledAbility;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    public void Enable() => enabledAbility = true;
    public void Disable() => enabledAbility = false;
}
