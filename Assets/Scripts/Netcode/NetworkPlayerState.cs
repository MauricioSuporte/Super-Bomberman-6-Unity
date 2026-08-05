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
        [SyncVar] byte mountType;     // F6: MountedType do Louie montado (0=None)
        [SyncVar] bool diedByExplosion; // morte por explosão? (renderer de morte)
        [SyncVar] bool gloveHolding;  // luva: segurando bomba? (pose no dono)
        [SyncVar(hook = nameof(OnEliminatedChanged))] bool eliminated; // F5a

        const int FxSkull = 1 << 0;
        const int FxInvuln = 1 << 1;

        NetworkPlayerSetup setup;
        CharacterHealth health;
        BombController bomb;
        MovementController move;
        PlayerBomberSkinController skinController;
        PlayerMountCompanion mountCompanion;
        PowerGloveAbility glove;
        AbilitySystem abilitySystem;

        int appliedSkin = -1;
        int appliedTempFx = 0;
        int appliedMountType = -1;
        bool appliedGloveHolding;
        bool playedDeathSfx;
        int appliedAbilityFlags = -1;

        void Awake()
        {
            setup = GetComponent<NetworkPlayerSetup>();
            health = GetComponent<CharacterHealth>();
            bomb = GetComponent<BombController>();
            move = GetComponent<MovementController>();
            skinController = GetComponentInChildren<PlayerBomberSkinController>(true);
            mountCompanion = GetComponent<PlayerMountCompanion>();
            glove = GetComponent<PowerGloveAbility>();
            abilitySystem = GetComponentInChildren<AbilitySystem>(true);
            PlayerPersistentStats.EnsureSessionBooted();
        }

        // Client-auth: liga/desliga as HABILIDADES no abilitySystem do cliente
        // conforme os flags replicados. Sem isto, no cliente as abilities ficam
        // DESLIGADAS (o abilityFlags só ia pro rt/HUD como dado) → o próprio
        // movimento/interação do cliente falha: BombKickAbility.enabledAbility=false
        // (chute nem tenta) e MovementController consulta
        // abilitySystem.IsEnabled(DestructiblePass) (atravessar bloco = false).
        void ApplyAbilityFlagsToClientAbilitySystem()
        {
            if (isServer || abilitySystem == null || abilityFlags == appliedAbilityFlags)
                return;

            SetAbility(BombKickAbility.AbilityId,        (abilityFlags & (1 << 0)) != 0);
            SetAbility(BombPunchAbility.AbilityId,       (abilityFlags & (1 << 1)) != 0);
            SetAbility(PowerGloveAbility.AbilityId,      (abilityFlags & (1 << 2)) != 0);
            SetAbility(BombPassAbility.AbilityId,        (abilityFlags & (1 << 3)) != 0);
            SetAbility(DestructiblePassAbility.AbilityId,(abilityFlags & (1 << 4)) != 0);
            SetAbility(PierceBombAbility.AbilityId,      (abilityFlags & (1 << 5)) != 0);
            SetAbility(ControlBombAbility.AbilityId,     (abilityFlags & (1 << 6)) != 0);
            SetAbility(PowerBombAbility.AbilityId,       (abilityFlags & (1 << 7)) != 0);
            SetAbility(RubberBombAbility.AbilityId,      (abilityFlags & (1 << 8)) != 0);
            SetAbility(MagnetBombAbility.AbilityId,      (abilityFlags & (1 << 9)) != 0);
            SetAbility(FullFireAbility.AbilityId,        (abilityFlags & (1 << 10)) != 0);

            appliedAbilityFlags = abilityFlags;
        }

        void SetAbility(string id, bool on)
        {
            if (on) abilitySystem.Enable(id);
            else    abilitySystem.Disable(id);
        }

        // F5a — replicação da eliminação. Ao fim da sequência de morte o host
        // desativa o GameObject do player; o Mirror NÃO sincroniza SetActive,
        // então marcamos via SyncVar e o cliente desativa o seu clone. SyncVar
        // (e não RPC) para cobrir também quem conecta depois (late-join).
        [Server]
        public void ServerMarkEliminated()
        {
            if (!eliminated)
                eliminated = true;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (eliminated && !isServer && gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        void OnEliminatedChanged(bool _, bool now)
        {
            if (now && !isServer)
            {
                // Predição (Etapa 1): segurança extra — garante que a predição para
                // e o input sintético é limpo ao eliminar.
                if (move != null)
                    move.SetPredictLocally(false);
                PlayerInputManager.Instance?.ClearSyntheticPlayer(
                    setup != null ? setup.PlayerId : GameSession.MinPlayerId);
            }

            if (now && !isServer && gameObject.activeSelf)
                gameObject.SetActive(false);
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

            // F6 — tipo de montaria (Louie) para o cliente mostrar o visual.
            byte mt = (byte)(mountCompanion != null ? (int)mountCompanion.GetMountedLouieType() : 0);
            if (mt != mountType) mountType = mt;

            // Animações host-decididas que o dono replica localmente (client-auth):
            // morte (por explosão?) e a pose de carregar/arremessar da luva.
            if (move != null && diedByExplosion != move.DeathWasByExplosion)
                diedByExplosion = move.DeathWasByExplosion;

            bool gh = glove != null && glove.IsHoldingForNet;
            if (gh != gloveHolding) gloveHolding = gh;
        }

        void ClientApply()
        {
            int playerId = setup != null ? setup.PlayerId : GameSession.MinPlayerId;

            // CharacterHealth local: o HUD lê a vida daqui.
            if (health != null)
                health.life = life;

            // Client-auth: ao morrer (host decide via life==0), desliga a simulação
            // local do dono e limpa o input sintético; e o DONO toca a PRÓPRIA
            // animação de morte localmente (o NetworkPlayerAnimation replica aos
            // demais). Roda uma vez só (PredictLocally vira false em seguida).
            if (!isServer && move != null && move.PredictLocally && life == 0)
            {
                move.SetPredictLocally(false);
                PlayerInputManager.Instance?.ClearSyntheticPlayer(playerId);

                if (glove != null) { glove.NetVisualHardStop(); appliedGloveHolding = false; }
                move.PlayDeathVisualLocal(diedByExplosion);
            }

            // Som da morte: TODOS os clientes (dono e remotos) tocam uma vez na
            // transição para life==0, para todos ouvirem a eliminação.
            if (!isServer && !playedDeathSfx && life == 0 && move != null)
            {
                move.PlayDeathSfxLocal();
                playedDeathSfx = true;
            }

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

            // Client-auth: além do dado no rt (HUD), liga as abilities de verdade
            // no cliente (chute, atravessar bloco, etc. dependem disso).
            ApplyAbilityFlagsToClientAbilitySystem();

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

            // F6 — mostra/esconde o visual do Louie conforme o tipo replicado.
            // (Passo A: o Louie aparece; a animação/facing dele é o Passo B.)
            if (mountType != appliedMountType && mountCompanion != null)
            {
                if (mountType == 0) mountCompanion.HideMountVisualClient();
                else                mountCompanion.ShowMountVisualClient((MountedType)mountType);

                // O DONO troca o PRÓPRIO corpo para o visual "montado" (rider) para
                // não sobrepor o Louie; os remotos recebem isso pelo
                // NetworkPlayerAnimation (client-auth). Só enquanto vivo — a animação
                // de morte tem prioridade sobre o visual de montaria.
                if (isOwned && move != null && life > 0)
                    move.SetMountedOnLouie(mountType != 0);

                appliedMountType = mountType;
            }

            // Luva (client-auth): só o DONO reproduz a pose de carregar/arremessar
            // (os remotos a recebem pelo NetworkPlayerAnimation). A direção do carry
            // vem do próprio facing simulado localmente; o arremesso dispara ao
            // deixar de carregar. Enquanto morto/eliminado não desenha (a morte
            // assume o visual).
            if (glove != null && isOwned)
            {
                bool carryNow = gloveHolding && life > 0 && !eliminated;
                if (carryNow != appliedGloveHolding)
                {
                    glove.NetVisualSetCarrying(carryNow);
                    appliedGloveHolding = carryNow;
                }
                if (carryNow)
                    glove.NetVisualTickCarry();
            }
        }
    }
}
