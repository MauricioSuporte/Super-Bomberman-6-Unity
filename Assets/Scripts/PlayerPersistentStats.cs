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
    public static bool CanPunchBombs = true;

    public static bool HasPierceBombs = false;
    public static bool HasControlBombs = true;

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
            if (!movement.TryGetComponent<AbilitySystem>(out var abilitySystem))
                abilitySystem = movement.gameObject.AddComponent<AbilitySystem>();

            if (CanKickBombs) abilitySystem.Enable(BombKickAbility.AbilityId);
            else abilitySystem.Disable(BombKickAbility.AbilityId);

            if (CanPunchBombs) abilitySystem.Enable(BombPunchAbility.AbilityId);
            else abilitySystem.Disable(BombPunchAbility.AbilityId);

            if (HasControlBombs)
            {
                abilitySystem.Enable(ControlBombAbility.AbilityId);
                abilitySystem.Disable(PierceBombAbility.AbilityId);
            }
            else if (HasPierceBombs)
            {
                abilitySystem.Enable(PierceBombAbility.AbilityId);
                abilitySystem.Disable(ControlBombAbility.AbilityId);
            }
            else
            {
                abilitySystem.Disable(PierceBombAbility.AbilityId);
                abilitySystem.Disable(ControlBombAbility.AbilityId);
            }
        }
    }

    public static void SaveFrom(MovementController movement, BombController bomb)
    {
        if (movement != null && movement.CompareTag("Player"))
        {
            Speed = Mathf.Min(movement.speed, MaxSpeed);

            var abilitySystem = movement.GetComponent<AbilitySystem>();

            var kick = abilitySystem != null ? abilitySystem.Get<BombKickAbility>(BombKickAbility.AbilityId) : null;
            CanKickBombs = kick != null && kick.IsEnabled;

            var punch = abilitySystem != null ? abilitySystem.Get<BombPunchAbility>(BombPunchAbility.AbilityId) : null;
            CanPunchBombs = punch != null && punch.IsEnabled;

            var pierce = abilitySystem != null ? abilitySystem.Get<PierceBombAbility>(PierceBombAbility.AbilityId) : null;
            HasPierceBombs = pierce != null && pierce.IsEnabled;

            var control = abilitySystem != null ? abilitySystem.Get<ControlBombAbility>(ControlBombAbility.AbilityId) : null;
            HasControlBombs = control != null && control.IsEnabled;

            if (HasControlBombs)
                HasPierceBombs = false;
            else if (HasPierceBombs)
                HasControlBombs = false;
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
        ExplosionRadius = 1;
        Speed = 3f;

        CanKickBombs = false;
        CanPunchBombs = false;

        HasPierceBombs = false;
        HasControlBombs = false;
    }
}
