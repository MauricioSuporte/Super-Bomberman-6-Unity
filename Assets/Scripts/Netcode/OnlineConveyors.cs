using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Netcode
{
    public struct ConveyorPhase : NetworkMessage
    {
        public string scene, path;
        public uint revision;
        public bool clockwise, fast;
    }

    public partial class BombermanNetworkManager
    {
        void ApplyConveyorPhase(ConveyorPhase phase)
        {
            if (NetworkServer.active || NetSync.Mode != NetSync.NetMode.Client || phase.scene != SceneManager.GetActiveScene().name) return;
            foreach (var conveyor in FindObjectsByType<BattleMode5ConveyorController>(FindObjectsInactive.Include))
                if (conveyor.gameObject.scene.name == phase.scene && conveyor.NetworkPath == phase.path) conveyor.ApplyNetworkPhase(phase);
        }
    }
}
