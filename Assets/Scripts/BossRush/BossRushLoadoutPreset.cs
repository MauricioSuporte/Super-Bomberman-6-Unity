using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BossRushLoadoutPreset
{
    public BossRushDifficulty difficulty;

    [Header("Base Stats")]
    [Min(1)] public int life = 1;
    [Min(1)] public int bombAmount = 1;
    [Min(1)] public int explosionRadius = 1;
    [Range(0, PlayerPersistentStats.MaxSpeedUps)] public int speedUps = 0;

    [Header("Abilities")]
    public bool canKickBombs;
    public bool canPunchBombs;
    public bool hasPowerGlove;
    public bool canPassBombs;
    public bool canPassDestructibles;
    public bool hasPierceBombs;
    public bool hasControlBombs;
    public bool hasPowerBomb;
    public bool hasRubberBombs;
    public bool hasFullFire;

    [Header("Mount")]
    public MountedType mountedLouie = MountedType.None;

    [Header("Queued Eggs (Optional)")]
    public List<ItemType> queuedEggs = new();

    public void ApplyTo(PlayerPersistentStats.PlayerState state)
    {
        if (state == null)
            return;

        state.Life = Mathf.Max(1, life);
        state.BombAmount = Mathf.Clamp(bombAmount, 1, PlayerPersistentStats.MaxBombAmount);
        state.ExplosionRadius = Mathf.Clamp(explosionRadius, 1, PlayerPersistentStats.MaxExplosionRadius);
        state.SpeedInternal = PlayerPersistentStats.ClampSpeedInternal(
            PlayerPersistentStats.BaseSpeedNormal + (Mathf.Clamp(speedUps, 0, PlayerPersistentStats.MaxSpeedUps) * PlayerPersistentStats.SpeedStep)
        );

        state.CanKickBombs = canKickBombs;
        state.CanPunchBombs = canPunchBombs;
        state.HasPowerGlove = hasPowerGlove;
        state.CanPassBombs = canPassBombs;
        state.CanPassDestructibles = canPassDestructibles;
        state.HasPierceBombs = hasPierceBombs;
        state.HasControlBombs = hasControlBombs;
        state.HasPowerBomb = hasPowerBomb;
        state.HasRubberBombs = hasRubberBombs;
        state.HasFullFire = hasFullFire;

        if (state.HasControlBombs)
        {
            state.HasPierceBombs = false;
            state.HasPowerBomb = false;
            state.HasRubberBombs = false;
        }
        else if (state.HasPierceBombs)
        {
            state.HasControlBombs = false;
            state.HasPowerBomb = false;
            state.HasRubberBombs = false;
        }
        else if (state.HasPowerBomb)
        {
            state.HasControlBombs = false;
            state.HasPierceBombs = false;
            state.HasRubberBombs = false;
        }
        else if (state.HasRubberBombs)
        {
            state.HasControlBombs = false;
            state.HasPierceBombs = false;
            state.HasPowerBomb = false;
        }

        state.MountedLouie = mountedLouie;

        state.QueuedEggs.Clear();
        if (queuedEggs != null && queuedEggs.Count > 0)
            state.QueuedEggs.AddRange(queuedEggs);
    }
}
