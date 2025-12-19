using UnityEngine;

[RequireComponent(typeof(AbilitySystem))]
public class EnableBombKickOnAwake : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<AbilitySystem>().Enable(BombKickAbility.AbilityId);
    }
}
