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
        MovementController move;
        BombController bomb;
        int lastSentMask = -1;
        int serverHeldMask;

        void Awake()
        {
            setup = GetComponent<NetworkPlayerSetup>();
            move = GetComponent<MovementController>();
            bomb = GetComponent<BombController>();
        }

        // Client-autoritativo: o DONO deste player (host OU cliente puro) simula-o
        // localmente e é a autoridade da própria posição. Roda no spawn, ANTES do 1º
        // Update — o flag já está pronto quando os gates do MovementController rodam.
        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            if (move != null)
                move.SetPredictLocally(true);
        }

        void Update()
        {
            if (isLocalPlayer && !isServer)
            {
                SampleAndSendLocalInput();
                SampleAndSendTaps();
                InjectLocalMovementSynthetic();
            }

            if (isServer)
                ApplyServerHeldMask();
        }

        // Predição de cliente (Etapa 1): além de mandar o input pro host, injeta o
        // input de MOVIMENTO (bits 0-3) localmente como sintético no próprio playerId,
        // para o MovementController local (que lê Get(playerId,...)) enxergar o teclado
        // imediatamente. Só movimento — bomba/dano seguem host-autoritativos.
        void InjectLocalMovementSynthetic()
        {
            if (move == null || !move.PredictLocally || setup == null)
                return;

            int playerId = setup.PlayerId;
            // Se o playerId do dono JÁ é o slot de hardware local (1), o
            // MovementController lê o teclado direto por Get(playerId); injetar
            // aqui causaria latch (synthetic = synthetic||hardware). Só injeta quando difere.
            if (playerId == LocalInputSlot)
                return;

            var input = PlayerInputManager.Instance;
            if (input == null)
                return;

            for (int i = 0; i < 4; i++) // MoveUp/Down/Left/Right
                input.SetSyntheticHeld(playerId, SyncedActions[i], input.Get(LocalInputSlot, SyncedActions[i]));
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
                if (!input.GetDown(LocalInputSlot, SyncedActions[idx]))
                    continue;

                // ActionA (colocar bomba): reporta a posição do dono para o host
                // colocar a bomba no tile CERTO (a transform do host está atrasada).
                if (SyncedActions[idx] == PlayerAction.ActionA)
                    CmdBombTap((Vector2)transform.position);
                else
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

        // Tap de ActionA com a posição do dono (client-auth): o host aplica a
        // posição-reportada no BombController e dispara o tap. A colocação usa esse
        // tile; o pickup da luva (mesmo GetDown) segue usando a posição da bomba.
        [Command]
        void CmdBombTap(Vector2 ownerPos)
        {
            var input = PlayerInputManager.Instance;
            if (input == null || setup == null)
                return;

            if (bomb != null)
                bomb.SetNetworkedPlacementOverride(ownerPos);

            input.TapSynthetic(setup.PlayerId, PlayerAction.ActionA);
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

        // ---- Chute de bomba (client-auth) --------------------------------------
        // No cliente-dono o movimento roda localmente e o BombKickAbility detecta o
        // chute, mas o host não simula esse movimento → o dono PEDE ao host pra
        // executar o StartKick autoritativo na bomba networkada. O deslize volta
        // replicado pela NetworkTransform da bomba.
        public bool TryClientKickBomb(Bomb bomb, Vector2 dir, float tileSize, int obstacleMask)
        {
            if (bomb == null || !isLocalPlayer || isServer)
                return false;

            var id = bomb.GetComponent<NetworkIdentity>();
            if (id == null)
                return false;

            CmdKickBomb(id.netId, dir, tileSize, obstacleMask);
            return true;
        }

        [Command]
        void CmdKickBomb(uint bombNetId, Vector2 dir, float tileSize, int obstacleMask)
        {
            if (!NetworkServer.spawned.TryGetValue(bombNetId, out var id) || id == null)
                return;

            var bomb = id.GetComponent<Bomb>();
            if (bomb == null)
                return;

            var kick = GetComponent<BombKickAbility>();
            if (kick != null)
                kick.ServerExecuteKick(bomb, dir, tileSize, obstacleMask);
        }
    }
}
