using UnityEngine;
using UnityEngine.SceneManagement;

public static class BattleModeComStageAbilityLoader
{
    private const string BattleMode2SceneName = "BattleMode_2";
    private const string BattleMode3SceneName = "BattleMode_3";
    private const string BattleMode4SceneName = "BattleMode_4";
    private const string BattleMode5SceneName = "BattleMode_5";
    private const string BattleMode6SceneName = "BattleMode_6";
    private const string BattleMode7SceneName = "BattleMode_7";
    private const string BattleMode8SceneName = "BattleMode_8";
    private const string BattleMode9SceneName = "BattleMode_9";

    public static bool EnsureForActiveStage(GameObject playerObject)
    {
        if (playerObject == null)
            return false;

        return SceneManager.GetActiveScene().name switch
        {
            BattleMode2SceneName =>
                EnsureAbility<BattleModeComStage2FallingBombAbility>(playerObject),
            BattleMode3SceneName =>
                EnsureAbility<BattleModeComStage3PowderTrailAbility>(playerObject),
            BattleMode4SceneName =>
                EnsureAbility<BattleModeComStage4SpringEscapeAbility>(playerObject),
            BattleMode5SceneName =>
                EnsureAbility<BattleModeComStage5ConveyorAwarenessAbility>(playerObject),
            BattleMode6SceneName =>
                EnsureAbility<BattleModeComStage6RedirectionKickAbility>(playerObject),
            BattleMode7SceneName =>
                EnsureAbility<BattleModeComStage7PortalEscapeAbility>(playerObject),
            BattleMode8SceneName =>
                EnsureAbility<BattleModeComStage8PowerGroundAwarenessAbility>(playerObject),
            BattleMode9SceneName =>
                EnsureAbility<BattleModeComStage9MinecartAbility>(playerObject),
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
