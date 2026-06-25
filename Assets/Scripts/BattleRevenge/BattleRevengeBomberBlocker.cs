public static class BattleRevengeBomberBlocker
{
    public static bool PreventNewRevengeBombers { get; private set; }

    public static void Block()
    {
        PreventNewRevengeBombers = true;
    }

    public static void Unblock()
    {
        PreventNewRevengeBombers = false;
    }
}