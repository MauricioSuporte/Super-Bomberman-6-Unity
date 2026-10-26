namespace Assets.Scripts.Netcode
{
    public partial class NetworkBombFx
    {
        public struct MountActionState
        {
            public bool tankReady, tankEligible, blackEligible;
            public float tankCooldown, blackCooldown;
        }

        public MountActionState CaptureMountActionState()
        {
            var state = new MountActionState();
            if (TryGetComponent<TankMountShootAbility>(out var tank))
            { state.tankReady = tank.CanStartShot; state.tankEligible = tank.NetworkShotEligible; state.tankCooldown = tank.CooldownRemainingSeconds; }
            if (TryGetComponent<BlackLouieDashPushAbility>(out var black))
            { state.blackEligible = black.NetworkDashEligible; state.blackCooldown = black.NetworkCooldownRemaining; }
            return state;
        }
    }
}
