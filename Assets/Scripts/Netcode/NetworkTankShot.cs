using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    [RequireComponent(typeof(NetworkIdentity), typeof(TankShot))]
    public sealed class NetworkTankShot : NetworkBehaviour
    {
        [SyncVar] Vector2 direction;

        public override void OnStartServer()
        {
            direction = GetComponent<TankShot>().Direction;
        }

        public override void OnStartClient()
        {
            if (isServer) return;
            var shot = GetComponent<TankShot>();
            shot.Init(direction, shot.Speed, 0, null);
            foreach (var collider in GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
            foreach (var body in GetComponentsInChildren<Rigidbody2D>(true)) body.simulated = false;
        }
    }
}
