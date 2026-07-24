using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    /// <summary>
    /// Encaminha o input do jogador dono (client) para o servidor, que o
    /// injeta no PlayerInputManager como input sintético do playerId
    /// correspondente. Com isso, o MovementController/BombController
    /// EXISTENTES simulam o jogador remoto no host sem qualquer alteração
    /// na lógica de gameplay.
    ///
    /// Fluxo:
    ///   Client dono  -> lê hardware local (esquema do player 1 da máquina)
    ///                -> empacota um bitmask de ações
    ///                -> Command para o servidor (só quando muda)
    ///   Servidor     -> aplica o último bitmask de cada player todo frame
    ///                   via SetSyntheticHeld. A detecção de "GetDown"
    ///                   (bombas) é derivada pelo próprio PlayerInputManager.
    ///
    /// O player LOCAL do host é dirigido pelo hardware normalmente; como
    /// Get() = sintético || hardware, aplicar máscara 0 nele é inofensivo.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(NetworkPlayerSetup))]
    public class NetworkPlayerInput : NetworkBehaviour
    {
        // Ordem dos bits do bitmask de input.
        static readonly PlayerAction[] SyncedActions =
        {
            PlayerAction.MoveUp,    // bit 0
            PlayerAction.MoveDown,  // bit 1
            PlayerAction.MoveLeft,  // bit 2
            PlayerAction.MoveRight, // bit 3
            PlayerAction.ActionA,   // bit 4 (colocar bomba)
            PlayerAction.ActionB,   // bit 5 (detonar / kick)
            PlayerAction.ActionC,   // bit 6
        };

        // Índices (em SyncedActions) dos botões de ação (edge-triggered / GetDown):
        // ActionA(4)=colocar bomba, ActionB(5)=detonar/kick, ActionC(6).
        static readonly int[] TapActionIndices = { 4, 5, 6 };

        // Esquema de controle local usado por quem está nesta máquina.
        const int LocalInputSlot = 1;

        NetworkPlayerSetup setup;
        int lastSentMask = -1;
        int serverHeldMask;

        void Awake()
        {
            setup = GetComponent<NetworkPlayerSetup>();
        }

        void Update()
        {
            if (isLocalPlayer && !isServer)
            {
                SampleAndSendLocalInput();
                SampleAndSendTaps();
            }

            if (isServer)
                ApplyServerHeldMask();
        }

        // Botões de ação são lidos por GetDown (borda). Derivar a borda no host
        // a partir do held mask é frágil (depende da ordem de Update). Então
        // detectamos o GetDown aqui no cliente (hardware confiável) e mandamos
        // um tap explícito -> TapSynthetic no host (imune à ordem, usa frame).
        void SampleAndSendTaps()
        {
            var input = PlayerInputManager.Instance;
            if (input == null)
                return;

            for (int i = 0; i < TapActionIndices.Length; i++)
            {
                int idx = TapActionIndices[i];
                if (input.GetDown(LocalInputSlot, SyncedActions[idx]))
                    CmdTap(idx);
            }
        }

        [Command]
        void CmdTap(int actionIndex)
        {
            var input = PlayerInputManager.Instance;
            if (input == null || setup == null)
                return;
            if (actionIndex < 0 || actionIndex >= SyncedActions.Length)
                return;

            input.TapSynthetic(setup.PlayerId, SyncedActions[actionIndex]);
        }

        void SampleAndSendLocalInput()
        {
            var input = PlayerInputManager.Instance;
            if (input == null)
                return;

            int mask = 0;
            for (int i = 0; i < SyncedActions.Length; i++)
            {
                if (input.Get(LocalInputSlot, SyncedActions[i]))
                    mask |= 1 << i;
            }

            if (mask != lastSentMask)
            {
                lastSentMask = mask;
                CmdSendInput(mask);
            }
        }

        [Command]
        void CmdSendInput(int mask)
        {
            serverHeldMask = mask;
        }

        void ApplyServerHeldMask()
        {
            var input = PlayerInputManager.Instance;
            if (input == null || setup == null)
                return;

            int playerId = setup.PlayerId;
            for (int i = 0; i < SyncedActions.Length; i++)
            {
                bool held = (serverHeldMask & (1 << i)) != 0;
                input.SetSyntheticHeld(playerId, SyncedActions[i], held);
            }
        }
    }
}
