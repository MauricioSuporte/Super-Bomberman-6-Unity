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

Atualizado em 26/10/2026. Este é o resumo para acompanhar o trabalho; o histórico
abaixo preserva decisões e resultados anteriores. O checklist por ID distingue
implementação concluída de trabalho parcial. Validação manual fica separada.

- **Publicado e confirmado:** protocolo35, commit `a70d3cd22`, pipeline
  [40355636832](https://github.com/MauricioSuporte/Super-Bomberman-6-Unity/actions/runs/40355636832) com sucesso.
- **Último lote publicado:** apresentação dos ovos, visibilidade, direção/idle,
  sorting, preservação de hitboxes e identidade oculta para novos observadores.
  Validado com191 testes Unity,40 Python e compilação C# sem erros/avisos novos.
- **Validado para publicação:** protocolo36 ainda no diretório, não publicado.
  C02/C03: apresentação contínua de movimentos aprovados em canhão, mola/geyser
  e corda; gates impedem executar gameplay desses lançadores no client.
  C10: FallingBomb usa alvo/tempo aprovados e spawn real em rede.
  R01: histórico alinha pose da montaria/cabeça com posição, com geração por montaria.
- **Validação36:** suíte final206/206 passou, incluindo a coerência
  Rigidbody/Transform dos lançadores. C# final0 erros/avisos, Python40/40,
  UTF-8/BOM e diffcheck passaram. Logs/PredictionPresentation36FinalTests.xml/.log.
  Não declarar deploy36 até confirmar CI.
- **Próximos itens:** concluir validação/publicação36; C04 transporte do visual do
  carrinho e passageiro, C05 portais/swap, demais C/R e regressões A14.
- **Pendências de verificação:** nenhuma partida manual com dois peers nem novas
  capturas comparáveis. P0, A11/R07, A14, R01/R02 e mecanismos C continuam com
  pendências descritas no checklist. Não declarar zero latência ou plano completo.
- **Workspace:** branch `v0.5.0`, checkout original; preservar fontes e TitleScreen
  do usuário e o Unity aberto. Novos scripts e metas36 ainda não salvos em commit.

## Como acompanhar

- **Este arquivo:** Estado atual e Checklist por ID são os resumos vivos.
- **[Plano completo](plano-aplicacao-predicao-online.md):** escopo, critérios,
  abordagem de cada ID e riscos. O baseline de análise inicial é histórico.
- **[Notas de implementação](prediction-and-reconciliation.md):** comportamento
  técnico e limites de cada solução efetivamente implementada.
- **[Pipelines](https://github.com/MauricioSuporte/Super-Bomberman-6-Unity/actions):**
  status de build/publicação. Código enviado não implica servidor já atualizado.

## Marcos concluídos e commits

1. `1ac51b037`: A01/A13 e infraestrutura P0. Montaria/contexto/pass confirmados,
   motor e replay, diagnóstico com contexto, comparador e testes. 146 Unity.
2. `818c209de`: A02 proxy de plantio sem gameplay, reserva de estoque, confirmação
   por tick A, rejeição/timeout e footprint virtual. 147 Unity.
3. `9aa13fe06`: A03 Green dash, geometria readonly, apresentação do owner com
   ponte limitada de 150 ms, confirmação/reconciliação e áudio. 148 Unity.
   Marcos 1-3 publicados no protocolo 25 (CI 40251770360 sucesso).
4. `ffed44e3f`: A04 Pink e A06 Red. Pink separa chão/arco/sombra, owner e observer,
   sem coroutine/invulnerabilidade client. Red prevê pose, confirma cooldown e
   som de acerto; stun/item drop continuam no servidor. 151 Unity, 37 Python.
   Publicado no protocolo 26 (CI 40254308964 sucesso).
5. `b18c25d43`: C01 esteiras nas cenas BattleMode_5, Stage_3-4 e Stage_3-6.
   Push composto uma vez no tick do motor, lock de curva e fase históricos,
   velocidade/modificador/duração por tick, fases aprovadas para observadores.
   Client não simula bombas/toggles/movimento de terceiros. Offline/COM conservam
   loop anterior. Mais de um controller suspende predição conservadoramente;
   cenas atuais têm um cada. 154 Unity, 40 Python. Publicado no protocolo 27.

## Marco 7: A08 Tank (validado e enviado)

- `TankMountShootAbility` tem preview readonly e gate de Update em client puro.
  Spawn recebe owner/tick antes de NetSpawn. RPC confirma com áudio deduplicado;
  ready/cooldown são confirmados por MountActionState no TargetReconcile.
- `NetworkTankPrediction.cs` prevê pose/som e proxy somente SpriteRenderer.
  Sem TankShot/collider/Rigidbody/NetworkIdentity no proxy. Avanço até 150 ms sem
  spawn; confirmação conserva fase, rejeição/timeout/morte restauram apresentação.
- `NetworkTankShot` transfere a apresentação ao filho visual; o root fica sob
  NetworkTransform e não ganha simulação client. Snapshots limitam extrapolação
  a 150 ms. Sweep readonly consulta geometria conhecida; impacto/explosão/dano e
  cadeia seguem no servidor. Pausa congela posição visual; retomada longa espera
  amostra nova por até um segundo, com fallback autoritativo.
- Tint de cooldown tem API client de apresentação. Confirmação tardia após morte
  não ressuscita a pose. OnStopClient limpa o presenter do projétil.
- P0 final: MotionDiagnosticProbe.ClassifyPredictionBlock distingue pose prevista,
  movimento de habilidade e pausa; comparador exclui esses bloqueios. Comparador
  também separa captura, player, plataforma e fase da esteira, e não herda build
  entre arquivos sem cabeçalho. Não há novas capturas de ganho medido.
- Testes corrigidos: fixture de handoff precisava do collider existente no prefab
  real; posição do filho após mover pai exige tolerância de ponto flutuante.
  Ambos corrigidos, suíte completa passou. Nenhum teste pendente executando agora.
- Manual pendente: Tank em dois peers, quatro direções, cooldown/tint, obstáculo
  próximo/móvel, posse/transição, pausa, morte, perdas/jitter e impacto/chain.
- Commit `9a8dbca7c` enviado; próximo: A05 Black, A07 Mole, A09-A12 e famílias C/R.
  Protocolo ainda de trabalho 28; ao adicionar RPCs A05, incrementar para 29.
- A05 já começou; ver seção seguinte. Linha cp1252 confirmada foi convertida
  isoladamente para UTF-8; arquivo completo agora decodifica estritamente.

## Marco 8: A05 Black validado e salvo em a9c426883

- `NetworkBlackDash.cs` prevê owner dash/pose/som; máximo 150ms sem estado novo e
  range finito. Confirmação, hold de impacto, parada, rejeição e timeout/morte/
  montaria/bounce conservam autoridade. Não prevê push/stun/kick/invulnerabilidade.
- `BlackLouieDashPushAbility` compartilha cast readonly com pass acknowledged;
  gate bloqueia coroutine em client. Dash de rede escreve body/transform de forma
  imediata e zera velocidades; offline usa MovePosition como antes.
- End/Cancel liberam seu lock antes de reportar parada; NPI envia snapshot imediato
  forçado. Client segura posição final até o snapshot liberar motor, evitando NT
  antigo. Report só força snapshot se havia dash Black ativo no servidor.
- Cooldown usa elegibilidade separada de duração; projeta remanescente para
  NetworkTime.predictedTime, sem esperar ida/volta extra na borda do cooldown.
- MountActionState/Capture extraídos para partial comum; Tank preservado. Flags
  acknowledged estão disponíveis fora do tick para previews. NMA cobre pose,
  rebind e afterimage Black; Pink/bounce cancelam e FX limpa na saída.
- Só o comentário cp1252 confirmado de Black foi convertido; arquivo agora UTF-8.
- Validação: 162 Unity, 40 Python, C# 0 warnings/erros e UTF-8/BOM corretos.
  Nenhum teste em execução agora. Relatórios copiados para Logs/ do checkout.
- Manual pendente: dash/contato/hold/variações de pass/cancel em dois peers,
  pausas, diagonal, cooldown e rede degradada. Pushes externos seguem para R04.
- A05 foi salvo com skip CI e incluido na publicacao 30 com R02.

## R02: pré-requisito de colisão do mundo co-op (validado e enviado em d25c34734)

- Confirmado por código e assets: CoopSceneLoaded desabilita colliders e bodies
  de objetos de cena sem NetworkIdentity. Stage_1-1, Stage_3-4 e Stage_3-6 têm
  TilemapCollider2D não trigger em Ground/Destructibles/Indestructibles (layer 6),
  sem NetworkIdentity nos YAMLs. Motor, Green/Black e Tank consultam Physics2D;
  sem geometria client, podem prever atravessando parede até correção do servidor.
- Preferir geometria query-only separada, sem reativar scripts/corpos originais:
  replicar pose/tiles aprovados em Tilemap + TilemapCollider2D sem renderer, Mono
  de gameplay, NetworkIdentity ou Rigidbody dinâmico. Preservar tags para pass.
- Não copiar TileBase original diretamente para o clone (poderia executar Startup
  ou instanciar gameObjects). Gerar Tiles simples a partir de GetSprite/
  GetColliderType e matrizes computadas, com gameObject=null; destruir cache
  desses assets e proxies ao limpar/trocar cena.
- Integrar criação após loop que desabilita mundo, atualizar em ApplyCoopTiles e
  limpar em ClearCoopWorld. Proxy Tilemap deve se chamar CollisionShape (não
  Ground/Destructibles) para não ser escolhido pelos resolvers por nome.
- CoopTiles precisa informar collisionEnabled (estado server de collider/body/
  trigger/active), talvez offset; wire exige protocolo 30 ao implementar.
- Verificar composites/figuras não Tilemap e evitar extrapolar sobre geometria
  não suportada. Busca por CompositeCollider2D nos Scenes atuais não retornou.
- Novos helpers `CoopCollisionViews.cs` e `OnlineCoopCollision.cs`, com .meta.
  Tilemaps de query usam Tiles simples com dados de colisão aprovados pelo servidor;
  originais continuam desativados, sem renderer/Mono/gameObject Startup nos clones.
- Wire 30: CoopTileFrame inclui tipo/sprite de colisão; CoopTiles inclui complete,
  collisionEnabled e offset. Novo CoopCollisions transmite boxes/circles/capsules
  de cenário (PilarBlock/Door encontrados em Stage_3-4/3-6), com deltas/remoção.
- Predição aguarda mapas e snapshot completo de objetos; formas ativas não
  suportadas/composites suspendem predição, mas inputs seguem para o servidor.
- Server escuta Tilemap.tilemapTileChanged para enviar mudanças no tick de mundo
  (20Hz) em vez de esperar a varredura periódica (4Hz). Pose/collider da map também
  é verificada por tick. Inicial sem cache lê tiles reais, em vez de reset vazio.
- Box props têm dados aprovados de pose/escala/offset/enabled; nenhum script
  original reativado. Collider sem suporte ativo é fallback autoritativo.
- Tile motions aprovados da Yellow atualizam a query imediatamente; blockers
  de origem/destino existentes continuam responsáveis pelas reservas.
- ClearCoopWorld remove views/tiles gerados e inscrição estática. Helpers sem
  singleton/manager adicional. Manager existente coordena prontidão/stream.
- Validacao retomada: suite Unity 169/169; depois Coop 9/9 com a protecao
  final de readiness para Sprite de colisao ausente. C# zero warnings/erros;
  Python40 e UTF-8/BOM passaram. Teste wire inicialmente usava Quaternion zero
  invalido, corrigido para identity. Relatorios preservados no checkout principal.
- R02 agora cobre geometria de consulta aprovada; trajetorias dinamicas e atores
  continuam nos C/R correspondentes. Dois peers/manual ainda pendentes.

## Checklist por ID

- [ ] P0: ferramenta e contexto implementados/testados; novas capturas comparáveis pendentes.
- [x] A01: implementação e testes de locomoção montada; manual pendente.
- [x] A02: proxy, capacidade, confirmação/rejeição e testes; manual pendente.
- [x] A03: predição/autoridade de dash e testes; manual pendente.
- [x] A04: Pink Louie jump e teste; manual e ovos A11/R07 pendentes.
- [x] A05: owner dash/pose e testes; efeitos de contato autoritativos, manual/R04 pendentes.
- [x] A06: Red Louie pose e áudio confirmado; stun autoritativo; manual pendente.
- [x] A07: preparacao/pose Mole prevista; RNG/teleporte/protecao autoritativos; 171 testes, manual pendente.
- [x] A08: pose/proxy/handoff/cooldown e testes; manual pendente.
- [x] A09: grupo/tile/estoque/variantes e Purple pose, 176+5 testes; manual pendente.
- [x] A10: auditado feedback existente, logs Btick/selecao e guards client; 182 testes; capturas/manual pendentes.
- [ ] A11: parcial: riding/dismount34 e fila visual35 testados; continuidade/transferência e dois peers ainda requerem auditoria R07.
- [ ] A12: Revenge input e apresentação.
- [x] A13: velocidade/BombPass/DestructiblePass confirmados no replay; manual pendente.
- [ ] A14: regressões dos caminhos existentes.
- [x] C01: fase/curva/velocidade no tick e replay, testes; manual pendente.
- [ ] C02: parcial36: interpolação owner aprovada + gates nativos Cannon/Spring/geyser/Battle4; curvas completas/ambientais/manuais pendentes.
- [ ] C03: parcial36: preparação/voo aprovada no owner; deformação ambiental e comparação manual pendentes.
- [ ] C04: carrinho.
- [ ] C05: portais/swap.
- [ ] C06: correnteza/barco/plataforma.
- [ ] C07: pusher.
- [ ] C08: ímãs/redirecionamento.
- [ ] C09: mecanismos e tiles especiais.
- [ ] C10: parcial36 FallingBomb aviso/RNG/spawn corrigidos; powder/roof/SuddenDeath pendentes.
- [ ] R01: parcial36: tempo pose/posição e geração de montaria alinhados/testados; capturas/custo e validação entre peers pendentes.
- [ ] R02: parcial: geometria aprovada segura implementada (169 + 9 testes); ainda auditar custo por familia e curvas no stream visual.
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

## A07: preparacao da Mole validada

NetworkMoleStartup prevê somente a preparação inicial, pose/som por tick C;
confirmação conserva fase; ack sem início rejeita; revisões descartam eventos
antigos. Timeout/pausa/morte/troca de mount/disable limpam apresentação.
MoleMountDrillAbility no client não executa Update de gameplay; API de pose
reutiliza suppression/head offset/animador sem rotina, RNG ou invulnerabilidade.
Server publica início e passagem para fase 2, deduplicando áudio co-op. Demais
fases continuam via NetworkMountAnimation e teleporte continua inteiramente
autoritativo, pois o destino depende de RNG/alvos vivos. Não extrapolar saída.
NetworkPlayerAnimation e NetworkMountAnimation respeitam pose local ativa.
Teste novo cobre preparo, RNG preservado, rejeicao, confirmacao sem reinicio,
pausa, stale/end, rebind, timeout e morte. Suite completa 171/171 passou.
A07 foi validado (171/171 Unity, Python40, C# zero warnings/erros e UTF-8),
salvo/enviado em cb971ee11. CI40283518599 em andamento. Quando terminar,
registrar conclusao e validar o ajuste Tank em32 usando o runner livre.

A07 teste direcionado: primeiras falhas estavam na fixture EditMode, que nao
executa Awake da habilidade adicionada em runtime. Fixture agora inicializa
Awake explicitamente antes de validar o presenter. Nao remover o teste de
rebind: ele protege active.enabled no presenter manual apos NetworkMountAnimation.
PredictionMoleFocused3 passou 1/1 e PredictionMoleTests passou 171/171.
Relatorios preservados em Logs/PredictionMoleTests.xml e .log.

Proxima implementacao A09: placements atuais sao Dictionary<uint,Prediction>,
um unico tile por tick A. Linha requer grupo identificado por tick+acao A/C
e celula, reserva por tile e confirmacao parcial. TryPlaceBombAtIgnoringInputLock
nao chama ServerConfirmPlacement; nao reutilizar CurrentActionATick para Purple
(C), nem inventar ticks por tile. Extrair preview de geometria readonly de
BombController, preservar variantes/water/hole/ground e snapshot do estoque.
Nao iniciar bombas/fuses ou coroutines Purple no client.

A14 Tank em32: MountActionState adiciona tankEligible, Capture usa
NetworkShotEligible (mesmos guards de CanStartShot menos cooldown). Owner
reconcilia deadline pela idade do snapshot; confirmacao nao soma novamente RTT.
Teste Tank existente estendido para cooldown0.3/snapshot0.2, limite vencido
sem ready novo e elegibilidade falsa com cooldown0. Protocolos32 alinhados.
C# em verificacao; Unity/Python32 ainda pendentes. Aguardar CI31 para testar.

Precisao da matriz: R02 originalmente inclui tambem custo por familia e perda
de curvas por interpolacao linear (OnlineCoopSprites). Geometria esta concluida,
mas isso e somente um pre-requisito; manter R02 parcial ate o restante da auditoria.
Nao marcar R03/R05/R06/R08 completos apenas por existir o stream generico: ainda
conferir arcos curtos, teleports, discontinuidades e projeteis de cada familia.

A14 Tank32: C# incremental passou sem warnings/erros; Python40/40 passou.
Unity ainda NAO executado. Aguardar CI40283518599 terminar; copiar os 6 C#
modificados, testes e supervisor para runner. Rodar OnlineRoomEditModeTests.Tank
(e depois suite completa, esperado172 com caso de protocolo31). Preservar
Logs/PredictionTankCooldownTests.xml/.log no checkout principal. Conferir que
snapshot antigo do cooldown nao adia deadline e que tankEligible=false bloqueia
preview mesmo com cooldown0. Depois salvar32; nao publicar sem validacao Unity.

Desenho A09 adicional (nao implementado): identificar cada proxy por
(tick, acao A/C, celula), sem ticks sinteticos. Reserva conta todos os tiles,
inclusive confirmados aguardando ack para nao subestimar estoque antigo.
Reconciliacao remove rejeicoes parciais; RPC por tile confirma netId/posicao.
Purple precisa confirmar pose mesmo quando nenhuma bomba cabe; linha A sem
bombas extras nao cria efeito que nao existe offline. Som uma vez por grupo,
com audio co-op genereco deduplicado. Preview readonly deve rejeitar agua/hole,
explosao e blockers e usar a mesma origem/direcao de LineBombAbility.PlaceLine.
Resolver variante por ordinal/estado aprovado: nao repetir Power/Rubber/etc
de primeiro spawn em todos os proxies se specialBombsOnEveryPlacement=false.
Purple animator precisa API visual manual; Update real no client fica gated.
Nao consumir estoque real nem executar TryPlaceBombAtIgnoringInputLock client.

Tank32: testes direcionados 4/4 passaram. Fixture ampliada para testar
CaptureMountActionState durante cooldown e apos Disable (antes somente estado
recebido simulado). Suite completa172 em andamento, sessao exec15831.
C# final sessao11421 deve ser conferido; nenhum resultado Unity presumido.

Tank32 validado: suite completa172/172 passou incluindo Captura/Disable,
projecao de cooldown, wire32 e regressao dos presenters anteriores. C# final
zero warnings/erros, Python40/40 e UTF-8/BOM passaram. Publicar este lote e
seguir A09 pela arquitetura registrada. Manual dois peers ainda pendente.

Checkpoint apos envio32: commit874b38956, pipeline40285499924 em andamento.
Ultima publicacao integral confirmada: cb971ee11 / protocolo31 / CI40283518599.
Nenhum arquivo de usuario foi editado. Nao declarar32 publicado antes do CI.

A09 fundacao iniciada (nao validada Unity/nao publicada): BombController agora
expoe TryPreviewNetworkPlacementAt para geometria readonly compartilhada e
GetNetworkPlacementContinuationPrefab para resolver variante apos reserva de
outra bomba sem spawn/estoque. Resolver existente ganhou assumeActiveBomb=false
opcional preservando chamadas server/offline. Search/Magnet respeitam flag
every-placement; Power permanece first-only. Teste novo de variantes/RNG/estoque
foi adicionado mas NAO rodou. Aguardar CI32 antes de usar runner. Ainda falta
o grupo (tick,acao,celula), confirmacao parcial e pose Purple; nao marcar A09.

A09 continuacao em33 (NAO completa): NetworkBombPlacement dictionary agora usa
(tick, byte action, Vector2 cell), onde0=plantio simples,1=linha A,2=Purple C.
BeginPlacementPrediction aceita action/playSound opcionais; reserva continua
compartilhada; Single impede duas celulas no mesmo tick. Confirmacao RPC agora
leva action e procura tile correspondente para grupo, mantendo Single com
correcao de posicao. ServerConfirmPlacement escolhe tickC para action2.
Ack/timeout/footprint atualizados para key.tick. Teste antigo lookup de proxy
atualizado para tuple. Protocolos33 alinhados; testcase protocolo32 rejeitado.
C# em exec71141, conferir resultado. NAO houve Unity dessa refatoracao.
Runner ainda executa teste de fundacao32 com helper/teste de variantes SOMENTE
(exec36760, PredictionLineFoundation.xml/.log). Nao copiar33 enquanto Unity roda.
Ainda falta implementar campos PlacementState: continuationPrefabAssetId,
lineAllowed/purpleAllowed/purpleDuration, capturados do servidor. Novo partial
NetworkBombLinePlacement deve usar helpers readonly, estoque-reservas, blockers
Stage/Enemy e mesma origem/cardinal de LineBombAbility; compartilhar origem via
metodo puro para evitar desvio. action1 pede seed existente (ou proxy anterior),
action2 inicia pose mesmo se estoque0, sem coroutine. Limite de grupo conservador.
Confirmar cada tile depois de TryPlaceBombAtIgnoringInputLock no server, sem usar
tickA para C. RPC confirma cast Purple mesmo se0bombas, com duracao/direcao/tempo.
Som apenas1 por grupo, com helper direto+RPC e dedup; nao duplicar broadcast coop.
Purple animator precisa Begin/Pose manual + ForceStop, sem coroutine client;
rebind deve reaplicar progresso. Update real de Purple gated em client puro.
Integrar C edge, AbilityPredictionLocksMovement/HasLocalMountAbilityPose, cleanup
e ack/reject no existing FX/NPI. Nao marcar A09 completo antes de Unity e testes
de grupo parcial, estoque, variantes, stale/morte/pausa/rebind e A02 regressao.

A09 progresso adicional em33: PlacementState agora carrega next-prefab,
lineAllowed/purpleAllowed/duracao do cast. NetworkBombLinePlacement novo
(.meta criado) faz preview readonly da linha com reservas comuns e limite64,
mesma origem/seed compartilhadas em LineBombAbility. Server PlaceLine confirma
cada tile com action1/2. Som1/grupo via RPC deduplicado (sem broadcast co-op).
Purple tem pose manual, C edge, RPC confirmacao preservando fase, ack/reject,
pausa/morte/timeout/mountloss/cleanup e NMArebind via ReapplyMountAbilityPose;
Update original client gated. AnimatorPlay extraiu BeginCast compartilhado.
C# exec44108 a conferir. Nenhum Unity desse lote completo33 executado ainda.
Teste de fundacao2 passou (exec21021), mas seu XML deve ser conferido/preservado.
Ele cobria apenas helpers+refatoracao inicial, nao pose/linha completa.
Falta testar grupo parcial/estoque/variantes/wire e Purplepose0bombas/rebind,
confirm/reject/stale/pause/morte. Revisar canto de linha, tiles/water/hole e
rollback; depois copiar33 para runner livre e rodar testes e suite completa.

A09 lote final em33 validado: suite176/176 passou. Depois foram adicionados
somente5 testes de entrada A real/seed previsto com agua/hole/wall/enemy/explosion;
todos5 passaram. Nao declarar suite181/181 completa: esse total nao foi rodado
junto. C# zero warnings/erros e Python40 passaram. Sem validacao manual2peers.
Salvar/publicar33 e seguir A10/A11/A12, C02-C10, R01-R08, A14/P0 parciais.
R02 geometria concluida, mas custo/curvas por familia ainda pendentes.

A14 ponto a investigar (nao corrigido/reproduzido): NetworkPlayerInput agrega
actionEdges com OR e sobrescreve CurrentActionATick/CTick a cada edge no lote
antes de entregar uma unica Update de gameplay. Multiplas bordas iguais em
inputs agrupados podem casar a primeira previsao com o ultimo tick; investigar
sem reintroduzir dupla execucao de A/Glove. Nao presumir defeito medido.

A09 salvo/enviado b18e91f08 / protocolo33 / CI40315969165 sucesso (server e updater).
176 full +5 geometry passaram; nao afirmar CI33 concluido antes de consultar.
A10 auditoria iniciada: ControlBomb offline nao tem gesto/lock de cast; botao
de touch responde no input existente. Nao prever explosao/dano. Logs novos
control_detonation_validate incluem Btick real, modo oldest/all/empty, bombNetId
e accepted; fontes client input_send ja registram pressed32/tick. NPI adicionou
CurrentActionBTick, sem wire novo. Guard em TryExplodeOldest/All impede gameplay
em client puro. Novo teste ControlledBombDetonationCannotRunGameplayOnPureClient
ainda nao executado Unity. C# em verificacao. Aguardar CI33 liberar runner.

A10 fixture corrigida previamente: Bomb requer Collider2D concreto; adicionar
BoxCollider2D antes de AddComponent<Bomb>. Limpar lista planted da fixture antes
de destruir objetos. C# full so6warnings conhecidosMirror; incremental final
a conferir. Proximo teste, quando CI33 terminar: suite completa OnlineRoomEditModeTests
(expected182) com A10 e os5casos geometry da A09 juntos, sem copiar enquanto CIativa.
Logs A10 sao selecao validada, nao confirmacao visual da explosao; nao inferir
input-to-explosion apenas desses eventos. MobileButton.OnPointerDown ja chama
ApplyPressedVisual sem depender de servidor; nao adicionar cast/lock inexistente.

A11 proxima investigacao: PlayerManualDismount Select128 permite previsao de
preparacao/arco no mesmo lugar, com elegibilidade confirmada. PlayerRidingController
usa parabola quantizada + alturas inicial/final e sprites ascend/descend (nao
curva seno de bomba). Montar por pickup/ovo deve aguardar alvo/posse aprobados.
Cogitar descritor autoritativo de RidingArcType,start,target,duracao,alturas,peak,
ppu,quantize,direcao,tempo/revisao e SelectTick. API visual no controller sem
coroutine/isPlaying/inputLock/invulnerabilidade; FX controla root/presenter e
bridge ate snapshot. NPA/NMA devem respeitar presenter owner e observer.
ServerStart/Stop em TryPlayMountArcInternal/routine/Cancel; Stop antes de
onComplete para permitir handoff ao bounce de recovery existente. Posse,
invulnerabilidade, detachWorldPickup e filaEggQueue continuam servidor.
Ainda nao implementado A11. EggFollower tem somente NetworkTransform/facing,
sem ownerId/indice; precisa auditoria separada de fila e fase/visibilidade.

A11 detalhes de desenho ainda nao implementados: mounted visual pode permanecer
no chao durante a previa local de desmontar (root flat nao muda), sem criar
MountWorldPickup nem trocar posse. Confirmacao entrega renderer ao objeto de
mundo aprovado; dano/kill cancela pose e restore. Arcos aprovados de pickup
precisam de bloqueio do NPA e NMA em owner/observer e root restored apos NPI
e depois do NetworkTransform; manter NT decodificando snapshots. Preparar
bridge de termino ate snapshot pos-arc e handoff explicito para bounce de
recuperacao (nao cancelar apex de arco recebido tarde). Nao usar rotina real
PlayerRidingController no client: ela desbloqueia input e chama onComplete.
FilaEggQueue/NetworkEggFollower permanece pendente A11/R07, nao declarar A11
inteiro concluido ao terminar apenas riding/dismount.

CI40315969165 concluiu com sucesso; ultima publicacao b18e91f08 / protocolo33.
A10 agora em teste Unity completo182 no runner (PredictionControlTests).
Nao presumir resultado antes de XML. C# incremental final0warnings/erros.

A11 fundacao iniciada no diretorio (NAO usada em runtime ainda/NAO testada Unity):
PlayerRidingController ganhou NetworkArcState/CaptureNetworkArcState, funcao pura
SampleNetworkArcHeight (mesma parabola/pixel quantization do offline) e
PresentNetworkArc/StopNetworkArcVisual. API somente sprites/offset; isPlaying,
rotina, callbacks, inputLock e invulnerabilidade nao alterados. Reset limpa
networkArcVisual quando usado. Loop offline ainda intacto. Faltam RPC/Select
prediction/eligibilidade/handoff/root/queue. Nao adicionar fields mortos: flag
networkArcVisual lida por reset e presenter. Nao marcar A11 concluido.
A10 ainda em Unity (exec94975); A11 NAO foi copiado ao runner nessa suite.

A10 validado: PredictionControlTests182/182 passou (inclui A09+5geometry juntos).
C# final0warnings/erros e UTF-8/BOM passaram. Sem wire novo. Salvar com skip CI
para evitar publicar builds completos apenas por instrumentacao; incluir no
proximo lote. Ultima publicacao b18e91f08 /33/ CI40315969165 sucesso.
PlayerRidingController.cs (A11helper) permanece fora desse commit e nao foi
copiado ao runner182. Proximo: testes A11API/root/curve e implementar RPC+Select.

A10 salvo/enviado ce7e27ada com skip CI (incluido no proximo lote); publicacao
ativa continua b18e91f08/protocolo33. SuiteControl182 e C#0warnings passaram.
A11 helper teste adicionado RidingArcPresentationPreservesPixelCurveWithoutStartingGameplay:
parabola/quantizacao, ascend/descend, origem/Z/duracao, sem worldmove/inputlock/
IsPlaying/invulnerabilidade, cleanup e host guard. Ainda nao rodou Unity.
Runner livre; helper/teste copiados para PredictionRidingFoundation.
Nao ha RPC ou Select prediction de A11 ainda. Proximo implementa-los apos helper.

A11 em implementacao34: NetworkRidingPresentation.cs/.meta novos. RidingMotion
com revisao/inputTick/arc/tempo/stop; ownerSelect128 preve somente gesto usando
shape aprovado no MountActionState.dismountArc e dismountEligible. SenderNative
TryPlayMountArcInternal confirma no mesmo Update antes do primeiro yield (evita
ack rejeitar uma transicao aceita); fim enviado antes de onComplete para bounce.
Server root Rigidbody/Transform sincronizados durante arco e snapshot forca
no fim/cancel depois de unlock. TryPlayDismount ganhou networkInputTick opcional
passado pelo PlayerManualDismount; CurrentSelectTick emNPI, sem ticks sinteticos.
FX root/pose domina NPA/NMA/NT sem desligar decoder. Pending/reject/confirm phase,
stop bridge ate snapshot, pause/timeout/death, queue de bounce landing32slots
(somente apos stop completed; contato externo cancela), cleanup presentes.
Guards de abilidades/carry enquanto riding; Black confirmado toma controle.
Falta revisar takeover Green/Pink (nao feito ainda), teste owner/observer/queue
rollback/death/pause/wire e snapshots; validar UTF8/C#/Python/Unity.
Protocolos34 alinhados; NAO publicado. A11/R07 filaEgg ainda pendente.
HelperRidingFoundation passou1/1, mas nao inclui esse novo presenter/glue34.

A11 ainda em34, C# exec88786 a conferir. Retained RidingMotion virou SyncVar com
hook+OnStartClient (late join/visibilidade); progresso aprovado e sampleTime
atualizados a20Hz somente durante arco e em troca de pausa (sem spam parado),
sem reiniciar presenter a cada sample pois revisao nao muda. PaiNative reporta
progress/paused no loop, mantendo matematica offline intacta. Start confirma
mesmoUpdate antes do yield para ack nao rejeitar gesto aceito. End revision/
paused atualizados, nativeclock freezing honrado inclusive snapshot inicial
recebido durante pausa. Pose/gravity/ownership ainda nunca simulados client.
Novo TraceRiding inclui hold_ms para medir espera no endpoint. A ponte de fim
aguarda estado aprovado; nao antecipar unlock de gameplay por supor uma
completionFrame exata (coroutine libera input num frame posterior ao arco).
Faltam testes end/handoff/owner/observer/late-join/paused/wire/nativePosition
coerencia e takeover. Green/Pink confirmados agora limpam riding antigo.

A14 novo ajuste junto de34: NPI guardava frame.held mascarado no pending durante
pose; agora RecordLocalFrame guarda input original, retorna copia mascarada
para motor e aplica mascara no replay somente se presenter ainda o exige.
ReceiveMovementState extraido do corpo TargetRpc existente para testar fluxo
real sem Command ficticio. Teste RejectedAbilityReplaysOriginalWalkingIntentInsteadOfStoredPoseZeros
verifica que pre-ack conserva lock, rejeicao C recupera3ticks de movimento e
nao apaga held original. Ainda NAO executado Unity. C# em verificacao.
A11 testes owner/observer-paused/nativeCoherent foram adicionados (tambem
ainda sem Unity), alem de RidingFoundation que ja passou1/1 antes do glue34.

Lote34 copiado ao runner para primeira suite completa com Riding+RawHistory
(expected188). C# incremental0warnings/erros passou. Testes owner/observer/
paused-retained/bounce queue/bridge/native-position e rejeicaoRed-replayRaw
adicionados; ainda NAO rodaram. PredictionRidingTests em exec a conferir.
Nao declarar A11 completo: filaEggQueue/R07 ainda pendentes apos riding.


## Retomada de 26/10: lote34 validado

NetworkRidingPresentation e integração aprovados na suíte completa188/188.
Predição manual Select preserva o tick, confirma sem reiniciar fase e rejeita
restaurando snapshot. Montar depende do evento aprovado. Pausa/fase retida para
observadores, propriedade/dano/invulnerabilidade e callbacks no servidor.
Landing bounce/stop esperam a curva principal; root fica coerente no servidor e
na reconciliação client. Diagnóstico hold_ms mede a espera na extremidade.
Histórico de input agora preserva direção original durante poses; teste verifica
rejeição de gesto Red seguida de replay real dos comandos pendentes.
C# final zero warnings/erros; Python32+8, UTF-8/BOM e diffcheck passaram.
Fontes e TitleScreen do usuário preservados. Protocolos34 alinhados; publicação
pendente no momento deste registro. A11/R07 e A14 não estão completos.


## Lote35 em andamento: ovos aprovados (ainda não validado Unity/publicado)

34 salvo/enviado em24b471911, CI40328660226 em andamento; não afirmar deploy.
35 está no diretório: NetworkEggFollower recebe QueuePresentation com timestamp,
facing/idle/visibilidade/sorting. Buffer32 no tempo interpolado; sem mover root,
conceder posse, criar fila ou prever captura. MountEggQueue publica após mover,
freeze, sorting/visibilidade; transporte fica ativo para late observer com sprites
ocultos e hitboxes preservados durante forcedHidden. Offline usa SetActive nativo.
Direction helper é só visual; VFX de destruição deixa de receber overrides.
Protocolos35 alinhados, testes novos adicionados (ainda não rodaram); C# final
zero warnings/erros. Build completa anterior mostrou6 avisos já existentes Mirror.
Runner ocupado pelo CI34: não copiar fontes35 até CI terminar. Próximo: validar
EggQueue testes + suíte completa, Python/UTF-8; só depois salvar/publicar35.
WorldMount auditado: pickup estático e emote alternativo (RNG só no servidor),
clone sem collider/simulação; não justifica extrapolação de posição sem evidência.
A11/R07 ainda parciais; sem medir atraso real de fila nem fazer dois peers.


CI34 confirmado completed/success. Runner livre; fontes35 copiadas e suíte
OnlineRoomEditModeTests iniciada: PredictionEggQueueTests.xml/.log. Esperados191
(188 anteriores +2 testes de ovo + rejeição protocolo34). Ainda conferir resultado.
C#0, Python40 e UTF-8/BOM passaram em35; sem publicação35 até Unity passar.


## Lote35 final validado

PredictionEggQueueFinalTests:191/191 passou incluindo ajuste de timelineOffset
igual ao NetworkTransform. C# final0warnings/erros; Python40/40 e UTF-8/BOM/
diffcheck passaram. Logs/xml preservados no checkout principal antes de liberar
runner para CI. Publicar35 com cliente+servidor compatíveis; depois conferir CI.
Fontes/TitleScreen preservados. A11/R07 continuam parciais (nenhuma medição real
ou validação de transferência entre dois peers). C02 auditoria preliminar:
Cannon usa parábola no root e sprites de fase, SpringLauncher pode wrap/clamp por
sala (helper SetFlightPosition já mantém body/transform), BattleMode4 usa chão
linear+altura visual subida/hold/descida. Não copiar uma curva única para todos.
A12 ainda pendente; BattleRevengeController não tem presenter/spawn de rede próprio
identificado na auditoria inicial: rastrear elegibilidade e transporte antes de
prever input em um carrinho local sem contraparte aprovada.


## Checkpoint após envio35

Commit `a70d3cd22` enviado; CI40355636832 em andamento no último check, ainda na
etapa Compile, test and build requested players. Não afirmar servidor35 instalado
até completed/success. Último deploy confirmado continua34 (24b471911).
Diretório principal só contém alterações originais do usuário em fontes/TitleScreen;
nenhuma alteração C# pendente. Runner limpo dos tracked usados na validação antes
do push, agora ocupado pelo CI35. Não copiar fontes até liberar.
Relatório final191/191 preservado em Logs/PredictionEggQueueFinalTests.xml/.log.
Próximos candidatos já investigados: C02 curvas distintas; A12 depende de
transporte para carrinho e input sobrevivendo à eliminação (MovementController
OnDeathSequenceEnded + NetworkPlayerState desativam o player/NPI). FallingBomb C10
BattleMode2FallingBombController sorteia target em coroutine sem gate explícito
NetSync; antes de prever qualquer telegraph, conferir/gatear origem autoritativa e
replicar aviso alvo/tempo. Não tratar alvo local aleatório como confirmação.
Plano permanece em andamento, nenhuma validação manual de dois peers realizada.


## Lote36 em andamento (não publicado)

CI35 completed/success confirmado. Novo NetworkAuthorityMotion.cs/.meta partial
NPI: lease marca trajetória nativa autoritativa, continuousPresentation no snapshot
somente enquanto canPredict=false. Owner usa snapshots NT já recebidos (componente
continua disabled, preservando baseline delta), sem RPC snap a cada pacote, no
mesmo NetworkTime.time da pose NPA. Pausa congela, descontinuidades >1.5tile ou
gap>.25s cortam em vez de interpolar pelo mapa; não extrapola. Rollback/replay
normal retoma ao encerrar; presenters especializados têm prioridade. Logs
bem delimitados authority_motion_begin/end. Sem input/posse/dano speculative.
Wiring: Cannon, SpringLauncher/geyser, Battle4Spring, preparação+voo da corda11.
Novo gate pureclient impede coroutines de gameplay duplicadas nesses mecanismos.
198 Unity passaram ANTES dos gates+FallingBomb; não usar esse resultado para lote
final. C# incremental final0. Suite final204 em runner: PredictionExternalAndTarget
(exec64105 a conferir). Precisa Python/UTF-8/diffcheck e preservar relatórios.

Carrinho9/12 foi retirado desse wiring: visual próprio não tinha transporte aprovado
identificado e desativar rotina local esconderia seu trajeto. C04 continua pendente
até apresentar posição/frame/visibilidade do carrinho sem atropelamento client.
C02/C03 continuam parciais: não há descritor da curva inteira e faltam efeitos
ambientais de mola/corda em Battle; montaria NMA ainda usa frame atual, requer R01.
Não declarar todos lançadores/carrinhos prontos nem latência zero.

C10 FallingBomb36: nativeclient não sorteia target/instancia bomba. Mensagem
FallingBombTarget aprovada com revisão/time/remaining/fuse/radius/paused, envio100ms
para late observer e eventos de start/stop, sem prever timer gameplay. Cliente só
apresenta aviso; deadline não cria bomba. Bomba nativa agora NetSpawn antes do
StartPunch; falha usa NetSpawn.Despawn. Novo partial BattleMode2...Network.cs/meta;
handler no manager existente (InitializeCoopClient também usado em Battle).
Testes de guards e alvo/RNG/pausa/stale/scene/wire adicionados (ainda conferir204).
Demais C10 roof/powder/SuddenDeath pendentes. Protocolos36 alinhados.


36 avançou: R01 montaria agora usa history64 de frame sets alterados, timestamp no
tempo NT, tipo+geração para troca de montaria, interpolação de offsets locais com
frames/flip/visible/cor discretos. NPA também interpola offset compatível e pausa
congela views. C# final0. Suite completa206/206 passou incluindo fixtures corrigidas
(pré-requisitos Collider no mecanismo e cena preview isolada no teste do aviso).
Após isso, helper server de coerência Transform/Rigidbody Cannon/Battle4 foi
adicionado e compilou0. Suite final206 em andamento PredictionPresentation36Final
(exec23143 a conferir), não declarar versão final testada até conferir. Python40
já passou36; UTF8/diffcheck passam mas refazer após últimas edições.
NMA/R01 testes cobrem midpoint/tipo/geração/historycap e offset de cabeça sem
adiantar frame. Capturas/manuais ainda pendentes. C02/C03/C10 continuam parciais.
Próximo depois de validar36: salvar/publicar lote e continuar C04 (visual carrinho
aprovado antes de gatear gameplay local), C05 e demais IDs. Nenhuma fonte/Title
modificada por este trabalho. Arquivos novos têm metas; csproj de CLI é ignorado.


## Lote36 pronto para publicação

Suíte final206/206 passou; C# final0 warnings/erros, Python40/40, UTF-8/BOM e
checagem do diff passaram. Relatórios preservados no checkout principal:
Logs/PredictionPresentation36FinalTests.xml/.log. Runner liberado antes de push.
Lote contém C02/C03 apresentação aprovada + gates, C10 FallingBomb e R01 poses.
Todos os IDs continuam com limites e manuais registrados; não é o plano todo.
Próximo: salvar/enviar36, conferir CI e continuar C04/C05 e outros pendentes.
