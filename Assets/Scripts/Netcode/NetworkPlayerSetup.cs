using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    /// <summary>
    /// Replica o playerId (1..6) do Player e o aplica aos componentes de
    /// gameplay já existentes em TODAS as instâncias (host e clients), para
    /// que o input sintético/hardware acerte o slot certo.
    ///
    /// Colocar no Player.prefab (junto com NetworkIdentity, NetworkTransform
    /// e NetworkPlayerInput).
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public class NetworkPlayerSetup : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnPlayerIdChanged))]
        int netPlayerId = 1;

        public int PlayerId => netPlayerId;

        [Server]
        public void ServerAssignPlayerId(int playerId)
        {
            netPlayerId = Mathf.Clamp(playerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);
            ApplyPlayerId(netPlayerId);
        }

        public override void OnStartClient()
        {
            ApplyPlayerId(netPlayerId);

            // Cliente puro não simula física: deixa o Rigidbody2D kinematic
            // para o NetworkTransform posicionar sem conflito.
            if (!isServer && TryGetComponent<Rigidbody2D>(out var rb))
                rb.bodyType = RigidbodyType2D.Kinematic;
        }

        void OnPlayerIdChanged(int _, int newValue)
        {
            ApplyPlayerId(newValue);
        }

        void ApplyPlayerId(int playerId)
        {
            if (TryGetComponent<PlayerIdentity>(out var identity))
                identity.playerId = playerId;

            if (TryGetComponent<MovementController>(out var move))
                move.SetPlayerId(playerId);

            if (TryGetComponent<BombController>(out var bomb))
                bomb.SetPlayerId(playerId);
        }
    }
}
