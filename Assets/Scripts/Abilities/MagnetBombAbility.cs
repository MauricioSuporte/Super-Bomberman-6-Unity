using UnityEngine;

[DisallowMultipleComponent]
public class MagnetBombAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "MagnetBomb";

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

    public void ApplyToBomb(GameObject bombObject)
    {
        if (!enabledAbility || bombObject == null)
            return;

        if (!bombObject.TryGetComponent<MagnetBomb>(out var magnetBomb) || magnetBomb == null)
            magnetBomb = bombObject.AddComponent<MagnetBomb>();

        magnetBomb.SetTargetLayer("Enemy");
        magnetBomb.enabled = true;
    }
}