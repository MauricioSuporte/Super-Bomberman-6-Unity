using UnityEngine;
using UnityEngine.SceneManagement;

public static class BattleModeComStageAbilityLoader
{
    private const string BattleMode2SceneName = "BattleMode_2";

    public static bool EnsureForActiveStage(GameObject playerObject)
    {
        if (playerObject == null)
            return false;

        return SceneManager.GetActiveScene().name switch
        {
            BattleMode2SceneName =>
                EnsureAbility<BattleModeComStage2FallingBombAbility>(playerObject),
            _ => false
        };
    }

    private static bool EnsureAbility<T>(GameObject playerObject)
        where T : MonoBehaviour
    {
        if (playerObject.TryGetComponent<T>(out _))
            return false;

        playerObject.AddComponent<T>();
        return true;
    }
}
