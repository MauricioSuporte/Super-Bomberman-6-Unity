using System;
using System.Collections.Generic;

public static class AbilityRegistry
{
    private static readonly Dictionary<string, Type> map = new()
    {
        { BombKickAbility.AbilityId, typeof(BombKickAbility) },
        { BombPunchAbility.AbilityId, typeof(BombPunchAbility) },
        { PierceBombAbility.AbilityId, typeof(PierceBombAbility) },
        { ControlBombAbility.AbilityId, typeof(ControlBombAbility) },
        { FullFireAbility.AbilityId, typeof(FullFireAbility) },
        { BombPassAbility.AbilityId, typeof(BombPassAbility) },
        { DestructiblePassAbility.AbilityId, typeof(DestructiblePassAbility) },
        { InvincibleSuitAbility.AbilityId, typeof(InvincibleSuitAbility) },
        { PurpleLouieBombLineAbility.AbilityId, typeof(PurpleLouieBombLineAbility) },
        { GreenLouieDashAbility.AbilityId, typeof(GreenLouieDashAbility) },
        { YellowLouieDestructibleKickAbility.AbilityId, typeof(YellowLouieDestructibleKickAbility) },
        { PinkLouieJumpAbility.AbilityId, typeof(PinkLouieJumpAbility) },
        { RedLouiePunchStunAbility.AbilityId, typeof(RedLouiePunchStunAbility) },
        { BlackLouieDashPushAbility.AbilityId, typeof(BlackLouieDashPushAbility) },
        { MoleMountDrillAbility.AbilityId, typeof(MoleMountDrillAbility) },
        { TankMountShootAbility.AbilityId, typeof(TankMountShootAbility) },
    };

    public static bool TryGetType(string id, out Type type) => map.TryGetValue(id, out type);
    public static void Register(string id, Type type) => map[id] = type;
}
