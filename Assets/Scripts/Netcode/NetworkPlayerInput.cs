using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    /// <summary>Clients submit controls; only the server simulates the player.</summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(NetworkIdentity), typeof(NetworkPlayerSetup))]
    public class NetworkPlayerInput : NetworkBehaviour
    {
        static readonly PlayerAction[] Actions =
        {
            PlayerAction.MoveUp, PlayerAction.MoveDown, PlayerAction.MoveLeft,
            PlayerAction.MoveRight, PlayerAction.ActionA, PlayerAction.ActionB, PlayerAction.ActionC, PlayerAction.Select
        };
        NetworkPlayerSetup setup;
        int serverHeldMask, lastSentHeld;
        float nextSend, lastReceived;
        static bool Blocked => GamePauseController.IsPaused ||
            (BombermanNetworkManager.IsCoopStage && StageIntroTransition.Instance != null && StageIntroTransition.Instance.IntroRunning) ||
            (GameManager.Instance != null && GameManager.Instance.OnlineRoundPresentationActive);

        void Awake() => setup = GetComponent<NetworkPlayerSetup>();

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            if (TryGetComponent<MovementController>(out var movement)) movement.SetPredictLocally(false);
        }

        void Update()
        {
            var input = PlayerInputManager.Instance;
            if (input == null || setup == null) return;
            if (isLocalPlayer && !isServer && NetworkClient.ready && !Blocked)
            {
                int held = 0, pressed = 0;
                for (int i = 0; i < Actions.Length; i++)
                {
                    if (input.Get(1, Actions[i])) held |= 1 << i;
                    if (i >= 4 && input.GetDown(1, Actions[i])) pressed |= 1 << i;
                }
                if (pressed != 0 || held != lastSentHeld || Time.unscaledTime >= nextSend)
                {
                    nextSend = Time.unscaledTime + 1f / 60f;
                    lastSentHeld = held;
                    CmdControls(held, pressed);
                }
            }
            if (!isServer || connectionToClient == null) return;
            // The room owner uses P1 synthetic controls for the pause menu.
            // Do not overwrite those controls with gameplay zeros while paused.
            if (Blocked) { serverHeldMask = 0; return; }
            int mask = Time.unscaledTime - lastReceived > 0.5f ? 0 : serverHeldMask;
            for (int i = 0; i < Actions.Length; i++)
                input.SetSyntheticHeld(setup.PlayerId, Actions[i], (mask & (1 << i)) != 0);
        }

        [Command]
        void CmdControls(int held, int pressed)
        {
            if (((held | pressed) & ((1 << Actions.Length) - 1)) != 0) BombermanNetworkManager.RegisterRoomActivity();
            lastReceived = Time.unscaledTime;
            serverHeldMask = Blocked ? 0 : held & ((1 << Actions.Length) - 1);
            if (Blocked || setup == null || PlayerInputManager.Instance == null) return;
            for (int i = 4; i < Actions.Length; i++)
                if ((pressed & (1 << i)) != 0)
                    PlayerInputManager.Instance.TapSynthetic(setup.PlayerId, Actions[i]);
        }

        public override void OnStopServer()
        {
            if (setup != null) PlayerInputManager.Instance?.ClearSyntheticPlayer(setup.PlayerId);
            base.OnStopServer();
        }

        // Collision and kick decisions run on the server's movement path.
        public bool TryClientKickBomb(Bomb bomb, Vector2 dir, float tileSize, int obstacleMask) => false;
    }
}
