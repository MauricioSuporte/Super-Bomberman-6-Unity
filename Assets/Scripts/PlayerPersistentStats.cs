using UnityEngine;

public static class PlayerPersistentStats
{
    public const int MaxBombAmount = 9;
    public const int MaxExplosionRadius = 9;
    public const float MaxSpeed = 9f;

    public static int BombAmount = 9;
    public static int ExplosionRadius = 9;
    public static float Speed = 5f;
    public static bool CanKickBombs = true;

    public static void LoadInto(MovementController movement, BombController bomb)
    {
        BombAmount = Mathf.Min(BombAmount, MaxBombAmount);
        ExplosionRadius = Mathf.Min(ExplosionRadius, MaxExplosionRadius);
        Speed = Mathf.Min(Speed, MaxSpeed);

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
            Speed = Mathf.Min(movement.speed, MaxSpeed);
            CanKickBombs = movement.canKickBombs;
        }

        if (bomb != null && bomb.CompareTag("Player"))
        {
            BombAmount = Mathf.Min(bomb.bombAmout, MaxBombAmount);
            ExplosionRadius = Mathf.Min(bomb.explosionRadius, MaxExplosionRadius);
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
