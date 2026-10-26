# Progresso da implementação de predição online

## Retomada

Pedido autorizado: implementar o plano completo em
`Docs/multiplayer/plano-aplicacao-predicao-online.md`, preservando os limites de
autoridade e salvando progresso se a sessão/limite interromper o trabalho.
Ler este arquivo, o plano e a skill `super-bomberman-6-online-prediction` antes
de continuar. Conferir `git status` e os diffs: trabalho incompleto pode estar
no diretório sem commit. Não presumir que pendências foram implementadas.

Baseline inicial: `2e41c0c47`, protocolo 24, branch `v0.5.0` no checkout original.
Não criar worktree nem fechar o Editor com alterações não salvas. Preservar as
alterações do usuário em TitleScreen e nos dois assets de fonte.

## Estado atual

Marco 1 implementado e validado automaticamente (publicação ainda pendente):
- A01: snapshot confirma montaria e o motor permite locomoção montada estável;
  presenter local da montaria usa pose/fase do proprietário, sem habilitar gameplay.
- A13: replay usa velocidade confirmada e flags confirmadas de BombPass e
  DestructiblePass, independentemente de atualizações mais recentes da habilidade.
- P0: diagnóstico registra contexto no input e na resposta; comparador em
  `Tools/Diagnostics/analyze_prediction.py` separa builds/contextos e exclui inputs
  bloqueados/sem classificação dos percentis. Capturas antigas não comprovam ganho.
- Validação: 146/146 Unity EditMode, 37/37 Python; compilação C# incremental sem
  warnings/erros. Relatórios: `Logs/PredictionMountedTests.xml` e `.log`.
- Protocolo de trabalho: 25 (wire de contexto alterado); servidor publicado ainda
  permanece na versão anterior enquanto este lote não for enviado ao pipeline.
- Próxima ação: A02, proxy visual de colocação com confirmação/rejeição por tick,
  capacidade confirmada e substituição pelo spawn autoritativo.
- Partidas manuais com dois peers, contextos das nove montarias, redes degradadas
  e demais modos ainda não executadas; não declarar ganho percebido medido.

### Marco 2: A02 validado, ainda sem publicação

- `NetworkBombPlacement.cs` (partial do NetworkBombFx) cria apenas SpriteRenderer;
  reserva por tick/capacidade, confirma após spawn, rejeita por ack/timeout e usa
  footprint virtual para sair/reentrar na célula prevista sem collider de gameplay.
- `BombController` usa um resolvedor único de variantes nos dois caminhos de
  plantio; geometria de preview não altera fuse, estoque ou cenário.
- `NetworkPlayerInput` replica stock/prefab permitido e conserva o tick específico
  do press A mesmo quando processa vários ticks. Áudio dedicado evita duplicação.
- Teste: `PlacementProxyIsVisualOnlyAndReservesCapacityUntilAcknowledged`.
- Validação: 147/147 Unity EditMode; C# compilou sem warnings/erros introduzidos.
  Relatórios copiados para `Logs/PredictionPlacementTests.xml` e `.log`.
- Protocolo de trabalho permanece 25 (não publicado).
- Próxima ação: A03 Green Louie (pose + movimento limitado do proprietário,
  confirmação e snapshots do dash); depois A04 Pink e C01.
- Manual pendente: spawn/áudio em dois peers, press rápido com lote de inputs,
  variantes de bomba, glove/line/dash, água/buracos e disputa da célula.

## Checklist por ID

- [ ] P0: ferramenta e contexto implementados/testados; novas capturas comparáveis pendentes.
- [x] A01: implementação e testes de locomoção montada; manual pendente.
- [x] A02: proxy, capacidade, confirmação/rejeição e testes; manual pendente.
- [ ] A03: Green Louie dash.
- [ ] A04: Pink Louie jump.
- [ ] A05: Black Louie dash/push.
- [ ] A06: Red Louie pose/áudio; stun autoritativo.
- [ ] A07: Mole drill.
- [ ] A08: Tank shot visual previsto.
- [ ] A09: LineBomb/Purple cast e confirmação parcial.
- [ ] A10: ControlBomb feedback.
- [ ] A11: montar/desmontar, posse e fila de ovos.
- [ ] A12: Revenge input e apresentação.
- [x] A13: velocidade/BombPass/DestructiblePass confirmados no replay; manual pendente.
- [ ] A14: regressões dos caminhos existentes.
- [ ] C01: esteiras e composição com o motor.
- [ ] C02: molas/canhões/geysers.
- [ ] C03: corda.
- [ ] C04: carrinho.
- [ ] C05: portais/swap.
- [ ] C06: correnteza/barco/plataforma.
- [ ] C07: pusher.
- [ ] C08: ímãs/redirecionamento.
- [ ] C09: mecanismos e tiles especiais.
- [ ] C10: eventos FallingBomb/powder/roof/Sudden Death.
- [ ] R01: observadores.
- [ ] R02: mundo co-op.
- [ ] R03: apresentação de inimigos.
- [ ] R04: forças impostas por inimigos.
- [ ] R05: projéteis remotos.
- [ ] R06: bosses/bombers.
- [ ] R07: itens/ovos/montarias remotos.
- [ ] R08: cenário móvel/ambiental.
- [ ] Consolidar skill, notas e matriz de validação.

Um ID só deve ser marcado concluído com implementação/testes identificados;
registrar separadamente validação manual ainda não realizada. Hipóteses de
ganho devem continuar identificadas como hipóteses enquanto não houver captura.

## Validação e publicação

Baseline anterior: 135 testes Unity EditMode e 32 testes Python passaram.
Isso não valida alterações futuras. Executar C# e Unity para cada lote de código,
verificar UTF-8/BOM dos C# alterados e não introduzir warnings.

O Editor principal pode estar aberto. A validação anterior utilizou o checkout
isolado do runner, apenas quando nenhum job de CI o estava usando:
`C:/Users/mauri/.sb6-actions-runner/_work/Super-Bomberman-6-Unity/Super-Bomberman-6-Unity`.
Conferir CI antes de copiar fontes. Copiar os relatórios para `Logs/` do checkout
principal antes de disparar publicação, pois o pipeline limpa saídas do runner.

Não prometer retomada automática baseada em créditos. Este registro permite
continuar em uma próxima mensagem/sessão quando o acesso estiver disponível.
