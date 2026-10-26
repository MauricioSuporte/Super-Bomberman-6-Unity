using Assets.Scripts.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class MovementController
{
    BattleMode5ConveyorController networkConveyor;
    BattleMode5ConveyorController.NetworkMotorState networkConveyorState;
    Scene conveyorScene;
    bool hasConveyorScene;
    bool multipleNetworkConveyors;

    BattleMode5ConveyorController ResolveNetworkConveyor()
    {
        var scene = SceneManager.GetActiveScene();
        if (hasConveyorScene && conveyorScene == scene) return networkConveyor;
        conveyorScene = scene; hasConveyorScene = true; networkConveyor = null; multipleNetworkConveyors = false;
        foreach (var conveyor in FindObjectsByType<BattleMode5ConveyorController>(FindObjectsInactive.Include))
        {
            if (conveyor.gameObject.scene != scene || !conveyor.HasNetworkMotor) continue;
            if (networkConveyor != null) { multipleNetworkConveyors = true; break; }
            networkConveyor = conveyor;
        }
        return networkConveyor;
    }

    bool NetworkConveyorContextReady
    {
        get
        {
            if (!networkTickDriven) return true;
            var conveyor = ResolveNetworkConveyor();
            return !multipleNetworkConveyors && (NetSync.ShouldSimulateLocally || conveyor == null || networkConveyorState.path == conveyor.NetworkPath);
        }
    }

    public bool ComposesConveyorInNetworkTick(BattleMode5ConveyorController conveyor)
        => networkTickDriven && ResolveNetworkConveyor() == conveyor && !multipleNetworkConveyors;

    public string NetworkConveyorDiagnosticState
    {
        get
        {
            var state = CaptureConveyorState();
            return state.path == null ? "none" : state.path + ":" + (state.clockwise ? "cw" : "ccw") + ":" +
                (state.fast ? "fast" : "slow") + ":" + (state.hasTarget ? state.target.ToString() : "free");
        }
    }

    BattleMode5ConveyorController.NetworkMotorState CaptureConveyorState()
    {
        var conveyor = ResolveNetworkConveyor();
        return conveyor != null && !multipleNetworkConveyors ? conveyor.CaptureNetworkMotor(this, networkConveyorState) : default;
    }

    void RestoreConveyorState(BattleMode5ConveyorController.NetworkMotorState state)
    {
        networkConveyorState = state;
        ResolveNetworkConveyor()?.RestoreNetworkMotor(this, state);
    }

    void SimulateConveyorTick()
    {
        var conveyor = ResolveNetworkConveyor();
        if (conveyor == null || multipleNetworkConveyors) return;
        conveyor.SimulateNetworkMotor(this, networkConveyorState);
        networkConveyorState = conveyor.CaptureNetworkMotor(this, networkConveyorState);
    }
}
