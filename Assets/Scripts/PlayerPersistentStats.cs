public static class PlayerPersistentStats
{
    public static int BombAmount = 9;
    public static int ExplosionRadius = 9;
    public static float Speed = 5f;
    public static bool CanKickBombs = true;

    public static void LoadInto(MovementController movement, BombController bomb)
    {
        if (movement != null)
        {
            movement.speed = Speed;
            movement.canKickBombs = CanKickBombs;
        }

        if (bomb != null)
        {
            bomb.bombAmout = BombAmount;
            bomb.explosionRadius = ExplosionRadius;
        }
    }

    public static void SaveFrom(MovementController movement, BombController bomb)
    {
        if (movement != null && movement.CompareTag("Player"))
        {
            Speed = movement.speed;
            CanKickBombs = movement.canKickBombs;
        }

        if (bomb != null && bomb.CompareTag("Player"))
        {
            BombAmount = bomb.bombAmout;
            ExplosionRadius = bomb.explosionRadius;
        }
    }

    public static void ResetToDefaults()
    {
        BombAmount = 1;
        ExplosionRadius = 2;
        Speed = 5f;
        CanKickBombs = false;
    }
}
