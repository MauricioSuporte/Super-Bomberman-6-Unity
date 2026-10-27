using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public struct RevengeReturnState
    {
        public uint revision, inputAck;
        public bool jumping;
        public Vector2 position;
        public byte life, bombs, radius, skin, mount, tempFx;
        public int speed, character, abilities;
    }

    public partial class NetworkPlayerState
    {
        [SyncVar(hook = nameof(OnRevengeReturningChanged))] bool revengeReturning;
        [SyncVar] uint revengeReturnRevision;
        uint receivedRevengeReturn;
        bool receivedRevengePhase;
        public bool HasApprovedRevengeReturn => NetSync.IsOnline && revengeReturning && netIdentity != null && netId != 0 && (isServer || isClient);

        [Server]
        public void ServerBeginRevengeReturn(Vector2 start)
        {
            revengeReturning = true;
            uint ack = GetComponent<NetworkPlayerInput>()?.ResetForConfirmedRevengeReturn() ?? 0;
            RpcRevengeReturn(new RevengeReturnState { revision = ++revengeReturnRevision, inputAck = ack, jumping = true, position = start });
        }

        [Server]
        public void ServerFinishRevengeReturn()
        {
            if (!revengeReturning && !eliminated) return;
            revengeReturning = false; eliminated = false;
            ServerSample();
            var input = GetComponent<NetworkPlayerInput>();
            RpcRevengeReturn(new RevengeReturnState { revision = ++revengeReturnRevision,
                inputAck = input != null ? input.ResetForConfirmedRevengeReturn() : 0,
                position = transform.position, life = life, bombs = bombAmount, radius = explosionRadius,
                skin = skin, mount = mountType, tempFx = tempFx, speed = speedInternal, character = character, abilities = abilityFlags });
            input?.ServerReconcileMovementNow();
        }

        [ClientRpc]
        void RpcRevengeReturn(RevengeReturnState state) => ReceiveConfirmedRevengeReturn(state);

        void OnRevengeReturningChanged(bool previous, bool now)
        {
            if (!NetSync.IsOnline || netIdentity == null || !isClient || isServer) return;
            if (now) ActivateConfirmedReturn();
            else if (eliminated && gameObject.activeSelf) gameObject.SetActive(false);
        }

        void ActivateConfirmedReturn()
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            move?.SetPredictLocally(false);
            move?.SetInputLocked(true, forceIdle: false);
            if (TryGetComponent<Collider2D>(out var collider)) collider.enabled = false;
            if (TryGetComponent<Rigidbody2D>(out var body)) body.simulated = false;
        }

        public void ReceiveConfirmedRevengeReturn(RevengeReturnState state)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || netIdentity == null || !isClient || isServer || netId == 0 ||
                !float.IsFinite(state.position.x) || !float.IsFinite(state.position.y) || (!state.jumping && state.life == 0) ||
                (receivedRevengePhase && !NetworkInputHistory.IsNewer(state.revision, receivedRevengeReturn)) ||
                NetworkInputHistory.IsNewer(revengeReturnRevision, state.revision)) return;
            receivedRevengePhase = true; receivedRevengeReturn = state.revision;
            GetComponent<NetworkPlayerInput>()?.ResetForConfirmedRevengeReturn(state.inputAck);
            if (state.jumping)
            {
                revengeReturning = true; ActivateConfirmedReturn();
                transform.position = state.position;
                return; // The existing approved Riding arc supplies the curve. No revival/gameplay yet.
            }
            revengeReturning = false; eliminated = false; gloveHolding = false; appliedGloveHolding = false; diedByExplosion = false;
            GetComponent<NetworkBombFx>()?.FinishConfirmedRevengeReturnPresentation();
            life = (byte)Mathf.Max(1, state.life); bombAmount = state.bombs; explosionRadius = state.radius;
            skin = state.skin; mountType = state.mount; tempFx = state.tempFx;
            speedInternal = state.speed; character = state.character; abilityFlags = state.abilities;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (health != null) health.life = life;
            if (bomb != null) { bomb.bombAmout = bombAmount; bomb.explosionRadius = explosionRadius; }
            move?.ApplySpeedInternal(speedInternal);
            move?.RespawnFromBattleRevenge(state.position, 0, 0, confirmedNetworkPresentation: true);
            appliedTempFx = 0; appliedMountType = -1; appliedAbilityFlags = -1;
            ClientApply();
            move?.SetMountedOnLouie(mountType != 0);
        }
    }
}
