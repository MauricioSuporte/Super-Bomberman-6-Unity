using UnityEngine;

public static class PlayerPersistentStats
{
    public const int MaxBombAmount = 9;
    public const int MaxExplosionRadius = 9;
    public const float MaxSpeed = 9f;

    public static int BombAmount = 9;
    public static int ExplosionRadius = 9;
    public static float Speed = 5f;
    public static bool CanKickBombs = false;

    public static void LoadInto(MovementController movement, BombController bomb)
    {
        BombAmount = Mathf.Min(BombAmount, MaxBombAmount);
        ExplosionRadius = Mathf.Min(ExplosionRadius, MaxExplosionRadius);
        Speed = Mathf.Min(Speed, MaxSpeed);

        if (movement != null)
            movement.speed = Speed;

        if (bomb != null)
        {
            bomb.bombAmout = BombAmount;
            bomb.explosionRadius = ExplosionRadius;
        }

        if (movement != null && movement.CompareTag("Player"))
        {
            var kickAbility = movement.GetComponent<BombKickAbility>();

            if (CanKickBombs)
            {
                if (kickAbility == null)
                    kickAbility = movement.gameObject.AddComponent<BombKickAbility>();

                kickAbility.EnableBombKick();
            }
            else
            {
                if (kickAbility != null)
                    kickAbility.DisableBombKick();
            }
        }
    }

    public static void SaveFrom(MovementController movement, BombController bomb)
    {
        if (movement != null && movement.CompareTag("Player"))
        {
            Speed = Mathf.Min(movement.speed, MaxSpeed);

            var kickAbility = movement.GetComponent<BombKickAbility>();
            CanKickBombs = kickAbility != null && kickAbility.CanKickBombs;
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
