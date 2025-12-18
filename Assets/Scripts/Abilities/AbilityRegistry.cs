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
        { DestructiblePassAbility.AbilityId, typeof(DestructiblePassAbility) }
    };

    public static bool TryGetType(string id, out Type type)
    {
        return map.TryGetValue(id, out type);
    }

    public static void Register(string id, Type type)
    {
        map[id] = type;
    }
}
