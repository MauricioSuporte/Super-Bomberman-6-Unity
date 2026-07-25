using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    /// <summary>
    /// F0 — Estado por-player replicado (host-autoritativo).
    ///
    /// PlayerPersistentStats é uma classe ESTÁTICA alimentada pelo save da
    /// máquina local; no cliente puro ela devolve valores errados, então o HUD
    /// (vidas/bombas/fogo/velocidade) e a skin divergem. Aqui o host lê os
    /// valores autoritativos dos componentes de simulação e os replica; o
    /// cliente os escreve de volta no runtime local (PlayerPersistentStats /
    /// CharacterHealth) — que é de onde o BattleModeHud lê — e aplica a skin.
    ///
    /// Primeira fatia: vida, nº de bombas, raio, velocidade, skin. As flags de
    /// powerup (ícones do HUD) e mounts entram numa fatia seguinte.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(NetworkPlayerSetup))]
    public class NetworkPlayerState : NetworkBehaviour
    {
        [SyncVar] byte life = 1;
        [SyncVar] byte bombAmount = 1;
        [SyncVar] byte explosionRadius = 2;
        [SyncVar] int speedInternal;
        [SyncVar] byte skin;
        [SyncVar] int abilityFlags;   // bitmask de powerups (ver Pack/Unpack)
        [SyncVar] byte tempFx;        // F4: bit0 = skull ativo, bit1 = invencível

        const int FxSkull = 1 << 0;
        const int FxInvuln = 1 << 1;

        NetworkPlayerSetup setup;
        CharacterHealth health;
        BombController bomb;
        MovementController move;
        PlayerBomberSkinController skinController;

        int appliedSkin = -1;
        int appliedTempFx = 0;

        void Awake()
        {
            setup = GetComponent<NetworkPlayerSetup>();
            health = GetComponent<CharacterHealth>();
            bomb = GetComponent<BombController>();
            move = GetComponent<MovementController>();
            skinController = GetComponentInChildren<PlayerBomberSkinController>(true);
            PlayerPersistentStats.EnsureSessionBooted();
        }

        void LateUpdate()
        {
            if (isServer)
                ServerSample();
            else
                ClientApply();
        }

        [Server]
        void ServerSample()
        {
            if (health != null)
            {
                byte l = (byte)Mathf.Clamp(health.life, 0, 255);
                if (l != life) life = l;
            }
            if (bomb != null)
            {
                byte b = (byte)Mathf.Clamp(bomb.bombAmout, 0, 255);
                if (b != bombAmount) bombAmount = b;
                byte r = (byte)Mathf.Clamp(bomb.explosionRadius, 0, 255);
                if (r != explosionRadius) explosionRadius = r;
            }
            if (move != null)
            {
                if (move.SpeedInternal != speedInternal) speedInternal = move.SpeedInternal;
            }

            int pid = setup != null ? setup.PlayerId : GameSession.MinPlayerId;

            byte sk = (byte)PlayerPersistentStats.Get(pid).Skin;
            if (sk != skin) skin = sk;

            // Flags de powerup (fonte viva no host = runtime state, que o HUD lê).
            var rt = PlayerPersistentStats.GetRuntime(pid);
            int flags = 0;
            if (rt != null)
            {
                if (rt.CanKickBombs)        flags |= 1 << 0;
                if (rt.CanPunchBombs)       flags |= 1 << 1;
                if (rt.HasPowerGlove)       flags |= 1 << 2;
                if (rt.CanPassBombs)        flags |= 1 << 3;
                if (rt.CanPassDestructibles) flags |= 1 << 4;
                if (rt.HasPierceBombs)      flags |= 1 << 5;
                if (rt.HasControlBombs)     flags |= 1 << 6;
                if (rt.HasPowerBomb)        flags |= 1 << 7;
                if (rt.HasRubberBombs)      flags |= 1 << 8;
                if (rt.HasMagnetBomb)       flags |= 1 << 9;
                if (rt.HasFullFire)         flags |= 1 << 10;
            }
            if (flags != abilityFlags) abilityFlags = flags;

            // F4 — efeitos temporários (só o VISUAL precisa replicar; o gameplay
            // já é host-autoritativo). Skull ativo e (in)vulnerabilidade — inclui
            // a invencibilidade do InvincibleSuit, o blink pós-dano e o de spawn.
            int fx = 0;
            if (TryGetComponent<SkullDebuffController>(out var skull) && skull.HasActiveEffect)
                fx |= FxSkull;
            if (health != null && health.IsInvulnerable)
                fx |= FxInvuln;
            byte fxb = (byte)fx;
            if (fxb != tempFx) tempFx = fxb;
        }

        void ClientApply()
        {
            int playerId = setup != null ? setup.PlayerId : GameSession.MinPlayerId;

            // CharacterHealth local: o HUD lê a vida daqui.
            if (health != null)
                health.life = life;

            // Runtime state local: o HUD lê bombas/fogo/velocidade daqui.
            var rt = PlayerPersistentStats.GetRuntime(playerId);
            if (rt != null)
            {
                rt.Life = life;
                rt.BombAmount = bombAmount;
                rt.ExplosionRadius = explosionRadius;
                rt.SpeedInternal = speedInternal;
                rt.Skin = (BomberSkin)skin;

                rt.CanKickBombs        = (abilityFlags & (1 << 0)) != 0;
                rt.CanPunchBombs       = (abilityFlags & (1 << 1)) != 0;
                rt.HasPowerGlove       = (abilityFlags & (1 << 2)) != 0;
                rt.CanPassBombs        = (abilityFlags & (1 << 3)) != 0;
                rt.CanPassDestructibles = (abilityFlags & (1 << 4)) != 0;
                rt.HasPierceBombs      = (abilityFlags & (1 << 5)) != 0;
                rt.HasControlBombs     = (abilityFlags & (1 << 6)) != 0;
                rt.HasPowerBomb        = (abilityFlags & (1 << 7)) != 0;
                rt.HasRubberBombs      = (abilityFlags & (1 << 8)) != 0;
                rt.HasMagnetBomb       = (abilityFlags & (1 << 9)) != 0;
                rt.HasFullFire         = (abilityFlags & (1 << 10)) != 0;
            }

            // Skin: aplica só quando muda (Apply carrega sprites; tem cache).
            if (skin != appliedSkin && skinController != null)
            {
                skinController.Apply((BomberSkin)skin);
                appliedSkin = skin;
            }

            // F4 — reproduz o VISUAL dos efeitos temporários (pisca-pisca) só nas
            // transições. Os blinks mexem em cor/alpha do sprite (ortogonal ao
            // sprite/frame que o NetworkPlayerAnimation replica). Duração longa: o
            // host controla início/fim pelas transições da SyncVar tempFx.
            bool skullNow  = (tempFx & FxSkull) != 0;
            bool invulnNow = (tempFx & FxInvuln) != 0;
            bool skullWas  = (appliedTempFx & FxSkull) != 0;
            bool invulnWas = (appliedTempFx & FxInvuln) != 0;

            if (skullNow != skullWas && move != null)
            {
                if (skullNow) move.ApplyTemporarySkullVisual(3600f);
                else          move.ClearTemporarySkullVisual();
            }
            if (invulnNow != invulnWas && health != null)
            {
                if (invulnNow) health.StartTemporaryInvulnerability(3600f, true);
                else           health.StopInvulnerability();
            }
            appliedTempFx = tempFx;
        }
    }
}
