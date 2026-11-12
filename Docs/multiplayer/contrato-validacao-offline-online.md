# Contrato permanente de validação offline e online

Regra definida pelo usuário em 11/11/2026, após fechar os 33 IDs de predição/rollback: qualquer ajuste ou recurso do jogo deve preservar offline e online. Uma implementação que só funciona offline não está entregue. A conclusão do checklist é a baseline de regressão, não uma garantia de que recursos futuros se sincronizem automaticamente.

## Antes de alterar

Consultar AGENTS.md, as skills Unity/Online/Prediction e os owners atuais. Registrar os modos afetados (Normal Game, Boss Rush, Battle), offline/local2P, servidor dedicado, owner, observador, host e COM quando aplicáveis. UI, conteúdo, animação, som, câmera, montarias e prefabs também têm contrato online; ausência de edição em Netcode não isenta a tarefa.

Traçar o fluxo nativo offline e o fluxo de rede: input/trigger → autoridade → estado/segmento aprovado → transporte → snapshot inicial/observador tardio → apresentação → término/interrupção/cleanup. Explicar um papel não aplicável em vez de ignorá-lo. Não exigir rollback em um menu ou simulação especulativa onde não cabe.

## Conhecimento que deve ser preservado

| Causa/área | Contrato |
| --- | --- |
| Input do owner | Feedback previsto e reversível quando seguro; validar no servidor e reconciliar/reexecutar os inputs originais sem duplicar habilidade, som, spawn ou dano. Hardware local é slot1, independentemente de P1–P6. |
| Movimento de outro ator ou decisão desconhecida | Apresentar trajetos/fases aprovados, com histórico limitado e continuação ordenada; não adivinhar RNG, alvo, colisão, wrap, terreno, acerto ou morte. |
| Continuação determinística | Algoritmo compartilhado, seed/ordinal estáveis e limites explícitos. Direção determinística não torna colisão ou resultado autoritativo previsível. |
| Contexto de replay | Restaurar capacidades, montaria, modificadores e duração restante no tick histórico; contexto atual não pode alterar retroativamente todos os inputs pendentes. Geometria aprovada deve estar pronta antes da previsão. |
| Timeline e arcos curtos | Root, pose e montaria usam o mesmo instante do papel. Preservar curvas nativas, endpoints e segmentos curtos; congelar em pausa, lidar com recuos do relógio e evitar espera extra por tile. Não somar lead repetidamente. |
| Visual/áudio | Replicar edges de sprite, visibilidade, flip, sorting, alpha, mask, shader/propriedades e estados manuais; incluir repouso. Clocks nativos escalados/não escalados, clip/gain/fase e tails precisam de ownership claro. Headless pode exigir clock CPU em vez de AudioSource.isPlaying. |
| Montaria/posição | Origem real de coleta/desmontagem e assento específico, inclusive HeadOnly/Tank/Mole. Preservar sombra, escala, colisão e locks anteriores. Não substituir montaria por um clone concorrente. |
| Autoridade | Dano, morte, RNG, item/posse, terreno, placar, save e resultado continuam no servidor. Proxy previsto é apresentação; não invoca gameplay nativo no client. Offline não é identificado por !isServer. |
| Cancelamento/reuso | Serial/generation/scene/path e conexão efetiva impedem estados antigos. Dispor iteradores aninhados, preservar proteções anteriores e restaurar em disable/morte/troca/teardown. FX aprovado deve sobreviver à remoção da origem pelo owner apropriado. |
| Diagnóstico | Tick recebido é frame.tick, não o último CurrentInputTick processado. Polling cedo, observação após os presenters e antes da captura global. Resolver release após observar o frame: FixedUpdate pode ter movido antes de Update soltar o input. |
| Evidência | Capturas completas, flush/quit antes de copiar, build/DLL/source/protocolo identificados. Limites/dropped/overflow tornam ausência de evento inconclusiva. Separar bloqueio, colisão, cancelamento, hitch, RTT e correção interna. Percentis exigem amostras; máximo não pode desaparecer. |
| Publicação | ProtocolVersion, supervisor.PROTOCOL, manifestos, cliente/servidor e diretório precisam concordar. Alteração de wire exige bump coerente; não remover o gate de mismatch para liberar um deploy. |

Os detalhes e limites específicos ficam nas [notas mantidas](prediction-and-reconciliation.md) e na [skill de predição](../../.codex/skills/super-bomberman-6-online-prediction/SKILL.md). O [plano](plano-aplicacao-predicao-online.md), [checklist/checkpoint](progresso-predicao-online.md), [matriz](matriz-validacao-predicao-online.md) e [fechamento](fechamento-predicao-online.md) continuam sendo os mesmos arquivos canônicos, com o histórico e os aceites por cenário.

## Gates obrigatórios de cada alteração do jogo

1. Rodar compilação Unity/C# disponível e corrigir erros/avisos introduzidos, além do check UTF8/meta. Usar a versão de ProjectVersion.txt. Não fechar o Editor do usuário nem disputar o runner com Actions/outro teste.
2. Rodar a suíte Unity EditMode completa do baseline: `OnlineRoomEditModeTests;BattleModeComEditorTests;OnlineChatEditModeTests;OnlineLobbyInputEditModeTests`. Conferir XML com contagem positiva, zero failed e resultado Passed; filtro vazio não é sucesso. Adicionar regressão significativa no owner existente quando necessário.
3. Rodar ambas as suítes Python, inclusive em alterações de conteúdo/runtime: `python -m unittest discover -s Tools/RoomDirectory -p "test_*.py"` e `python -m unittest discover -s Tools/Ci -p "test_*.py"`. Os testes incluem o alinhamento real dos protocolos do repositório.
4. Validar o recurso tocado offline e com servidor dedicado + dois clientes reais, mostrando owner e observador. Cobrir início/continuação/fim, pausa/retomada e os eventos relevantes de morte, disputa, mudança de posse, cena/rodada e desconexão. Conteúdo de cena exige referências/prefabs/Resources corretos nos peers. Testar o host e COM quando o caminho é diferente e afetado.
5. Conferir captura e apresentação real, e registrar build/sha/protocolo, cenário, papéis, comandos, XML/logs, resultados e limites. Não chamar delta interno de replay de snap renderizado, idade de aprovação de duração de travada, compilação Linux de teste de runtime Linux ou amostra CPU de latência GPU/hardware.

A documentação pura deve ao menos passar pela avaliação de impacto, integridade e links; não inventar execução de gameplay. Alterações em código, conteúdo, ferramentas ou pipeline passam pelos gates automatizados acima. Não reexecutar suítes já aprovadas sem mudança/falha que justifique, mas uma nova alteração relevante ou correção de falha precisa de nova validação. Se ferramenta/acesso estiver indisponível, registrar precisamente o bloqueio e o que não foi testado; isso não é autorização para declarar o recurso inteiro correto.

O perfil quick do CI continua uma ferramenta de iteração que compila e roda Python; ele não substitui a suíte Unity completa executada na tarefa. Para release com evidência no próprio Actions, selecionar full. Corrigir uma falha sem desativar testes, baixar tolerância ou deslocar critério obrigatório para outro ID.

## Execução e ferramentas reutilizáveis

- Unity: `Tools/Ci/release.py:unity` com ROOT apontando para checkout isolado conferido; `OnlineClientBuild.BuildPerformancePlayer` produz Development para smokes. Nunca executar deploy/publicação como efeito colateral de uma validação local.
- Integração: `Tools/RoomDirectory/smoke_full_clients.py --help`; fixtures de montaria/abilities/launchers/mechanisms/coop-world, argumentos compatíveis e saída nova por tentativa. Cópia imutável fora do runner evita sobrescrita por CI. Usar SB6_SAVE_DIRECTORY isolado e aguardar quit/flush.
- Diagnóstico: `Tools/Diagnostics/analyze_prediction.py`, `prediction_cohorts.py`, `correlate_prediction_commands.py`, `analyze_remote_player.py`, `analyze_bomb_abilities.py`, `analyze_environment.py`, `analyze_bosses.py`, `analyze_projectiles.py`, `hitch_report.py` e relatórios R01/R02/R03.
- Rede degradada: `udp_test_relay.py` somente em loopback/teste; registrar condições e RTT efetivo. UDP Windows10054 no encerramento é contado sem matar outros peers; readiness/disconnect do jogo continuam sendo gates reais.
- P0: LocalPredictionSmoke é opt-in Development, Stage_1-1, inputs horizontais/Blue Louie e pausa; não representa toda combinação de gameplay nem autoriza uso numa sala de jogador.

## Encerrar sem perder o conhecimento

Atualizar o teste de regressão e a seção relevante das notas/skills quando houver aprendizado reutilizável. Evidências finais compactas e hashes ficam preservados antes de apagar builds, PNGs, capturas redundantes e tentativas temporárias. Registrar remoções e o novo local de arquivo, mantendo as referências históricas auditáveis. Não apagar código de predição, testes, skills, saves, recovery scenes, configuração de ferramentas, histórico global Codex/Claude ou cache ativo do runner para limpar artefatos deste trabalho.
