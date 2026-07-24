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

        NetworkPlayerSetup setup;
        CharacterHealth health;
        BombController bomb;
        MovementController move;
        PlayerBomberSkinController skinController;

        int appliedSkin = -1;

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

            byte sk = (byte)PlayerPersistentStats.Get(setup != null ? setup.PlayerId : GameSession.MinPlayerId).Skin;
            if (sk != skin) skin = sk;
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
            }

            // Skin: aplica só quando muda (Apply carrega sprites; tem cache).
            if (skin != appliedSkin && skinController != null)
            {
                skinController.Apply((BomberSkin)skin);
                appliedSkin = skin;
            }
        }
    }
}
