namespace Assets.Scripts.Netcode
{
    public partial class NetworkBombFx
    {
        public struct MountActionState
        {
            public bool tankReady, tankEligible, blackEligible, dismountEligible;
            public PlayerRidingController.NetworkArcState dismountArc;
            public float tankCooldown, blackCooldown;
        }

        public MountActionState CaptureMountActionState()
        {
            var state = new MountActionState();
            if (TryGetComponent<TankMountShootAbility>(out var tank))
            { state.tankReady = tank.CanStartShot; state.tankEligible = tank.NetworkShotEligible; state.tankCooldown = tank.CooldownRemainingSeconds; }
            if (TryGetComponent<BlackLouieDashPushAbility>(out var black))
            { state.blackEligible = black.NetworkDashEligible; state.blackCooldown = black.NetworkCooldownRemaining; }
            if (TryGetComponent<PlayerManualDismount>(out var dismount) && TryGetComponent<PlayerRidingController>(out var rider))
            {
                state.dismountEligible = dismount.NetworkDismountEligible;
                state.dismountArc = rider.CaptureNetworkArcState(PlayerRidingController.RidingArcType.Dismount,
                    UnityEngine.Vector2.down, transform.position, transform.position, dismount.NetworkDismountDuration);
            }
            return state;
        }
    }
}
