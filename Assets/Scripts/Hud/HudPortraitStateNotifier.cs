using UnityEngine;

public static class HudPortraitStateNotifier
{
    public static void SetCornered(int playerId, bool active) => Notify(playerId, HudPortraitState.Cornered, active);
    public static void SetInactive(int playerId, bool active) => Notify(playerId, HudPortraitState.Inactive, active);
    public static void SetTimeUp(int playerId, bool active) => Notify(playerId, HudPortraitState.TimeUp, active);
    public static void SetVictory(int playerId, bool active) => Notify(playerId, HudPortraitState.Victory, active);

    static void Notify(int playerId, HudPortraitState state, bool active)
    {
        HudPortraitInGridLayout normalHud = Object.FindAnyObjectByType<HudPortraitInGridLayout>();
        normalHud?.SetPlayerPortraitState(playerId, state, active);

        BattleModeHud battleHud = Object.FindAnyObjectByType<BattleModeHud>();
        battleHud?.SetPlayerPortraitState(playerId, state, active);
    }
}

public enum HudPortraitState
{
    Cornered,
    Inactive,
    TimeUp,
    Victory
}
