// ============================================================================
//  NormalGameAIManager
// ----------------------------------------------------------------------------
//  Coordenador (dev-only) que anexa o NormalGameAIController aos players
//  escolhidos (por padrão P2/P3/P4) durante o Normal Game, para gravar
//  gameplays de 4 jogadores estando sozinho.
//
//  CARREGAMENTO AUTOMÁTICO
//    Não é preciso adicionar este componente a nenhuma cena. Ele se cria
//    sozinho (RuntimeInitializeOnLoadMethod) quando o jogo inicia, persiste
//    entre cenas (DontDestroyOnLoad) e anexa a IA aos players de qualquer cena.
//
//  LIGAR / DESLIGAR (fácil)
//    Basta alterar o bool estático EnableAI abaixo, no código. Ele NÃO é
//    serializado (não aparece no Inspector) e pode ser mudado a qualquer
//    momento, inclusive em runtime por outro script/console.
//
//  REMOVER DO BUILD FINAL
//    Tudo está sob o Scripting Define Symbol ENABLE_NORMAL_GAME_AI. Sem esse
//    define a IA não é compilada nem carregada, e nada é distribuído.
//    (Player Settings > Other Settings > Scripting Define Symbols.)
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NormalGameAIManager : MonoBehaviour
{
#if ENABLE_NORMAL_GAME_AI
    // ======================= CONFIGURAÇÃO (edite aqui) =======================

    /// <summary>
    /// Bool NÃO serializado: ligue/desligue a IA mudando este valor no código.
    /// Pode ser alterado a qualquer momento, inclusive em runtime.
    /// </summary>
    public static bool EnableAI = true;

    /// <summary>Players que a IA controla (P1 normalmente fica com você).</summary>
    private static readonly int[] AiPlayerIds = { 2, 3, 4 };

    // Comportamentos da IA.
    private const bool FarmDestructibles = true;
    private const bool HuntEnemies = true;
    private const bool CollectItems = true;

    // Frequência (s) de procurar players novos para anexar a IA.
    private const float RescanInterval = 0.5f;

    // =========================================================================

    private static NormalGameAIManager instance;
    private static readonly List<PlayerIdentity> scratch = new(8);

    private float rescanTimer;
    private bool lastEnableAI;

    /// <summary>
    /// Cria o manager automaticamente em qualquer cena assim que o jogo inicia,
    /// sem precisar adicioná-lo manualmente. Persiste entre cenas.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        var go = new GameObject("[NormalGameAIManager]");
        go.AddComponent<NormalGameAIManager>();
        DontDestroyOnLoad(go);

    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        rescanTimer = 0f;
        lastEnableAI = EnableAI;
    }

    private void OnDisable()
    {
        if (instance == this)
            DetachAll();
    }

    private void Update()
    {
        // Transição liga -> desliga: remove a IA dos players uma vez.
        if (lastEnableAI && !EnableAI)
            DetachAll();
        lastEnableAI = EnableAI;

        if (!EnableAI)
            return;

        rescanTimer -= Time.deltaTime;
        if (rescanTimer > 0f)
            return;
        rescanTimer = Mathf.Max(0.1f, RescanInterval);

        AttachToPlayers();
    }

    private void AttachToPlayers()
    {
        scratch.Clear();
        PlayerIdentity.GetActivePlayers(scratch);

        for (int i = 0; i < scratch.Count; i++)
        {
            PlayerIdentity id = scratch[i];
            if (id == null)
                continue;
            if (System.Array.IndexOf(AiPlayerIds, id.playerId) < 0)
                continue;

            // Segurança: em Battle Mode o BattleModeComController é quem controla.
            if (id.TryGetComponent<BattleModeComController>(out var com) && com != null && com.enabled)
                continue;

            // Precisa ter os componentes esperados de um player jogável.
            if (!id.TryGetComponent<MovementController>(out _) || !id.TryGetComponent<BombController>(out _))
                continue;

            if (!id.TryGetComponent<NormalGameAIController>(out var ctrl) || ctrl == null)
            {
                ctrl = id.gameObject.AddComponent<NormalGameAIController>();
                ctrl.Configure(id.playerId);
            }

            // Aplica/atualiza tuning.
            ctrl.farmDestructibles = FarmDestructibles;
            ctrl.huntEnemies = HuntEnemies;
            ctrl.collectItems = CollectItems;
            ctrl.debugLogs = false;
            ctrl.enabled = true;
        }
    }

    private void DetachAll()
    {
        scratch.Clear();
        PlayerIdentity.GetActivePlayers(scratch);

        for (int i = 0; i < scratch.Count; i++)
        {
            PlayerIdentity id = scratch[i];
            if (id == null)
                continue;
            if (id.TryGetComponent<NormalGameAIController>(out var ctrl) && ctrl != null)
            {
                Destroy(ctrl);
            }
        }
    }
#endif
}
