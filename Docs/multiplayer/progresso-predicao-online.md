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

Marcos 1-3 publicados com protocolo 25 (CI 40251770360 concluído com sucesso).
A04/A06 validados automaticamente no lote de protocolo 26, aguardando publicação:
- A01: snapshot confirma montaria e o motor permite locomoção montada estável;
  presenter local da montaria usa pose/fase do proprietário, sem habilitar gameplay.
- A13: replay usa velocidade confirmada e flags confirmadas de BombPass e
  DestructiblePass, independentemente de atualizações mais recentes da habilidade.
- P0: diagnóstico registra contexto no input e na resposta; comparador em
  `Tools/Diagnostics/analyze_prediction.py` separa builds/contextos e exclui inputs
  bloqueados/sem classificação dos percentis. Capturas antigas não comprovam ganho.
- Validação mais recente: 151/151 Unity EditMode para A04/A06; 37/37 Python.
  C# sem warnings/erros; UTF-8/BOM dos C# alterados verificados.
  Relatórios: `Logs/PredictionPinkRedTests.xml` e `.log`.
- Protocolo de trabalho: 26 (Pink/Red com novas RPCs). Servidor publicado: 25,
  commit `9aa13fe06`; novos clientes 26 serão publicados após validação do lote.
- Próxima ação: A04 Pink jump; depois C01 e demais IDs na ordem do plano.
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

### Marco 3: A03 validado

- `NetworkGreenDash.cs`: predição de início do proprietário, janela de 150 ms
  sem autoridade, confirmação sem voltar ao início, snapshots com correção e
  parada por tick de C/revisão. Não executa coroutine de gameplay no client.
- Green respeita geometria conhecida, cancela em morte/interrupção e preserva
  áudio/afterimages. FX arbitra dash vs bounce; motor comum não disputa o corpo.
- Validação: 148/148 Unity EditMode; `Logs/PredictionGreenTests.xml` e `.log`.
- Próximo: A04 Pink (posição de chão separada do arco visual), depois C01.
- Manual pendente: input C em dois peers, alvo dinâmico, pausa/desmontagem/morte,
  plantio durante dash, speed debuff, áudio/afterimages e condições de rede.

### Marco 4: A04 validado, aguardando commit do lote

- Novo `NetworkPinkJump.cs`: gesto/arco previsto do proprietário, confirmação de
  geometria por C/revisão, rejeição, cooldown, parada/interrupção e apresentação
  remota no relógio de interpolação. Posição de chão e altura visual separadas.
- `PinkLouieJumpAbility` compartilha a resolução de destino entre server/offline
  e preview. Client puro não inicia rotina nem invulnerabilidade; APIs de pose
  preservam/restauram collider e offsets. Shadow e animator têm apresentação manual.
- Protocolo de trabalho passou a 26 para impedir Editor com RPCs novos de conectar
  ao build 25 em publicação. Pipeline 25: run 40251770360, commit 9aa13fe06.
- C# passou sem warnings/erros; Unity passou 150/150. Relatórios preservados em
  `Logs/PredictionPinkTests.xml` e `.log`. Teste de arco/chão/sombra, confirmação
  sem reinício, parada antecipada, rejeição/restore e bloqueio da coroutine client.
- Manual pendente: pausa, desmontagem/morte, perdas/latência, observador e ovos.
  Sincronismo dos ovos será tratado em A11/R07, ainda pendente.

### Marco 5: A06 validado, aguardando publicação com A04

- `NetworkRedPunch.cs`: pose do soco prevista apenas no proprietário; confirmação
  por tick C preserva fase, cooldown maior confirmado, rejeição por ack, pausa e
  interrupção restauram a pose. Sem hit test, stun ou item drop no client.
- Som é de acerto no offline; por isso só é emitido nos clients após confirmação
  do servidor. Observadores continuam com frames de montaria autoritativos.
- `RedLouiePunchStunAbility.Update` bloqueia execução de gameplay em client puro.
- Novo teste cobre fase, confirmação/rejeição, pausa, cooldown, morte e host guard.
- C#/Unity aprovados: 151/151, incluindo observador Pink. 37/37 Python passaram.
  O teste de observador inicialmente usava o relógio do proprietário; foi corrigido
  para NetworkTime.time, correspondente à interpolação, e a suíte repetida passou.
- Próximo: commit/publicação do lote 26; depois A08 Tank/A05/C01 e demais IDs.
  A validação manual de ambos os peers continua pendente.
- Skill e notas atualizadas com separação chão/arco, relógios e áudio de acerto.

## Checklist por ID

- [ ] P0: ferramenta e contexto implementados/testados; novas capturas comparáveis pendentes.
- [x] A01: implementação e testes de locomoção montada; manual pendente.
- [x] A02: proxy, capacidade, confirmação/rejeição e testes; manual pendente.
- [x] A03: predição/autoridade de dash e testes; manual pendente.
- [x] A04: Pink Louie jump e teste; manual e ovos A11/R07 pendentes.
- [ ] A05: Black Louie dash/push.
- [x] A06: Red Louie pose e áudio confirmado; stun autoritativo; manual pendente.
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
