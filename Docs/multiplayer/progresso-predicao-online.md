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

Atualizado em27/10/2026. O checklist distingue implementação/testes de validação
manual; o histórico abaixo preserva decisões, resultados e tentativas anteriores.

- **Última publicação confirmada:** protocolo45, commit `14b6f5cac`, pipeline
  [40442915878](https://github.com/MauricioSuporte/Super-Bomberman-6-Unity/actions/runs/40442915878)
  com builds/deploy/updater/artefatos concluídos. O lote C07 abaixo ainda é local.
- **Avanço desta retomada:** C07 parcial: snapshot não suprime mudança de posição
  quando o ack de input se repete; cast do pusher deduplica corpos e mantém a
  física nativa, com gates de client/pausa e diagnóstico de força/snap/reconciliação.
- **Validação final:**359/359 Unity (OnlineRoom267 + COM92), incluindo12 casos
  específicos C07; C#0 erros/avisos novos, Python42/42 (34 CI +8 Directory),
  UTF-8/BOM/meta/diffcheck passaram. Reports preservados em
  Logs/PredictionPusherFinal45Tests.* e PredictionPusherFocused45Tests.*.
- **Limite de C07:** a continuação prevista do contato e seu histórico no replay
  ainda faltam. A centralização nativa na grade e o bounce são transições
  autoritativas separadas; nenhum ganho real de latência foi medido neste lote.
- **Checklist:**13 IDs implementados/testados,13 parciais e7 pendentes. Validação
  manual com dois peers segue pendente inclusive nos IDs marcados.
- **Próximos itens:** concluir contato/continuação owner de C07; R07 sem owner,
  forças R04 e demais C/R descritos no checklist. C10 ainda precisa da timeline,
  commit de terreno e late join; A14 da matriz completa/manual.
- **Workspace:** branch `v0.5.0`, checkout original. Lote C07 ainda não commitado;
  sem novo wire/RPC (protocolo45 permanece). Preservar fontes/TitleScreen do usuário
  e o Editor aberto. O runner encerrou os testes; seus tracked próprios foram restaurados.

## Como acompanhar

- **Este arquivo:** Estado atual e Checklist por ID são os resumos vivos.
- **[Plano completo](plano-aplicacao-predicao-online.md):** escopo, critérios,
  abordagem de cada ID e riscos. O baseline de análise inicial é histórico.
- **[Notas de implementação](prediction-and-reconciliation.md):** comportamento
  técnico e limites de cada solução efetivamente implementada.
- **[Matriz de validação](matriz-validacao-predicao-online.md):** roteiro manual
  por ID, critérios e evidências; cenários ainda não executados entre dois peers.
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

Resumo: **13 de33 IDs concluídos em implementação/testes,13 parciais e7 pendentes**.
Esta contagem não representa percentual de esforço; validação manual com dois peers
continua pendente mesmo em IDs marcados. Consolidação de documentação é item separado.

- [ ] P0: ferramenta/contexto implementados/testados, incluindo classificação de barco45; novas capturas comparáveis pendentes.
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
- [ ] A14: parcial45: descoberta e fixtures COM corrigidas,359 testes combinados aprovados; matriz completa/manual pendentes.
- [x] C01: fase/curva/velocidade no tick e replay, testes; manual pendente.
- [ ] C02: parcial36: interpolação owner aprovada + gates nativos Cannon/Spring/geyser/Battle4; curvas completas/ambientais/manuais pendentes.
- [ ] C03: parcial36: preparação/voo aprovada no owner; deformação ambiental e comparação manual pendentes.
- [ ] C04: parcial37 publicado: transporte de cart/rider/loop/estrelas e gates testados; boarding previsto e manual de entrada/saída/portais pendentes.
- [ ] C05: parcial38 publicado: Portal7 owner/pose/máscara/estrelas e gates; manual e auditoria do swap/blackout C09 pendentes.
- [x] C06: correnteza/plataforma39 e barco45 (água no replay, owner view/cabeça, gates, pausa/cleanup e handoff) implementados/testados; manual pendente.
- [ ] C07: parcial: ack repetido com posição nova, deduplicação de contato, gates e logs;12 testes específicos passaram. Continuação owner/replay do contato e manual pendentes.
- [ ] C08: parcial40 validado: campo magnético aprovado e gates client; continuidade/R07 e manual pendentes.
- [ ] C09: mecanismos e tiles especiais.
- [ ] C10: parcial44: FallingBomb36, teto/powder43 e path/espera de áudio SuddenDeath44 testados (335 casos); timeline/commit de terreno/late join/manual pendentes.
- [ ] R01: parcial36: tempo pose/posição e geração de montaria alinhados/testados; capturas/custo e validação entre peers pendentes.
- [ ] R02: parcial: geometria aprovada segura e clock visual39 alinhados/testados; custo por família e curvas ainda pendentes.
- [ ] R03: apresentação de inimigos.
- [ ] R04: forças impostas por inimigos.
- [ ] R05: projéteis remotos.
- [ ] R06: bosses/bombers.
- [ ] R07: parcial: ovos35/poses38, motion com owner inativo41 e VFX já recebido independente do owner42 testados; bombas sem owner, itens/transferência e manual pendentes.
- [ ] R08: cenário móvel/ambiental.
- [ ] Consolidar skill e notas (atualizadas até45); matriz manual criada, execução e consolidação final pendentes.

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


## Lote37 C04 em andamento

36 salvo/enviado em1b8dbb449, CI40362231252 em andamento. Último deploy confirmado35.
37: novo BattleMode9MinecartController.Network.cs/.meta, MinecartView com cena/path,
revisão/time, riderId usado só para tempo NT, posição/dir, um frame visível e loop.
Server Battle publica20Hz quando muda e heartbeat500ms idle; co-op mantém stream
visual existente. Client buffer64, segue segmentos aprovados/cantos do trilho e
corta wraps/lacunas; só modifica root/sprites/audio do cart, sem boarding/dano.
Native Update/TryHandle/RideRoutine/SuddenDeath client gated; RideRoutine server
usa scope NPI. Loop Battle tem transporte próprio (sem eco do one-shot genérico).
OnDisable envia hidden no server e limpa view. Portal stars e comparações entre
peers ainda pendentes C05/C04. Arquivos ainda não passaram Unity.
NPA37 agora inclui sortingLayer/order na pose/histórico para cabeça do passageiro
sobrepor corretamente o carrinho, sem aplicar a ordenação antes do timestamp.
Protocolos37 alinhados. C# a conferir (exec16128). Próximo: testes C04 view/sampling/
wiring, NPA sorting; aguardar CI36 liberar runner antes de copiar37.


CI36 completed/success confirmado. Fontes37 copiadas ao runner livre para suíte
completa213 testes esperados, PredictionMinecart37Tests.xml/.log (exec a conferir).
C# final0; Python40/40, UTF-8/BOM e diffcheck passaram37. Ainda não declarar Unity.
Novos testes: amostras preservam canto do trilho e cortam grande salto; view não
concede boarding/dano/invulnerabilidade, conserva último frame até hide timestamp,
recusa stale/cena, wire roundtrip, guard RideRoutine, sorting NPA e protocolo36
incompatível. Portal stars/efeitos e manuais continuam pendentes C04/C05.
Timeline do cart conserva o ajuste ao liberar rider (não descarta fim do percurso).


37 suíte cart/sorting213/213 passou (PredictionMinecart37FinalTests). Depois disso
foram adicionadas estrelas aprovadas do portal do carrinho: CartPortalStar com
posição/escala/ângulo/drift/spin/lifetime/frameTime/índice de sprite e clear por
revisão, sem RNG client nem componentes gameplay. Native servidor conserva draws
RNG e defaults da animação; só publica propriedades já escolhidas. Client agenda
no tempo do cart, cancela pendentes em clear/disable e usa rotina de render nativa
com idade/lifetime/frameTime aprovados. Co-op continua no stream existente.
Novo teste visual-only/RNG/wire/clear foi adicionado, ainda sem Unity. C# passou
antes dos últimos guards de cancelamento (agora refazer). Próxima suite214 final
precisa rodar antes de publicar37. Não usar213 como prova dessa adição de FX.
C05 Portal7/swap/masks ainda pendentes; C04 ainda precisa manual de áudio e portais.


37 última alteração ainda sem Unity: NPA aplica apenas sorting confirmado mesmo
quando owner prevê locomoção. Isso evita deixar a cabeça na ordem do carrinho ao
retomar caminhada, sem aplicar frame remoto no owner. Teste OwnerAuthorityMotion
foi estendido para ordenar7->2 conservando frame local2 contra remoto1.
C# exec48766 a conferir. Suite214 com estrelas ainda em runner (exec67248), MAS
não inclui esse último ajuste NPA/fixture. Conferir e depois copiar NPA+testes para
suite final214 consolidada antes de publicar. Não reusar213 como validação de FX.
Somente C04 view e R01 sorting são alterações37; C05 ainda apenas auditado.


37 suíte214 sem último sorting-owner passou; C# último sorting-owner0warnings/erros.
Suite consolidada214 em runner: PredictionMinecart37CompleteTests.xml/.log,
exec87841 a conferir. Inclui todo37, C04 view/FX e sorting-owner final.
Depois disso não houve novas edições C#; apenas docs/skill. Wiring por GUID
conferido BattleMode_9/_12 e 3 Stage_3-2. Sem mudanças de cenas/prefabs.
Próximo: conferir214, preservar relatórios, UTF8/diffcheck, restaurar tracked do
runner e salvar/publicar37. C05 ainda sem código: Portal7 Update faz gameplay no
client, TeleportRoutine interpola raiz oculta em default e tem branch sink/mask.
Reutilizar scope só após gate + preservação de estrelas/máscaras, sem adivinhar
portal de saída. Star7 e Cart12 usam a mesma curva nativa de partícula, mas só
Cart12 recebeu o transporte37 até aqui. C04 boarding ainda não é previsto.


## Lote37 validado para salvar/publicar

Suíte completa214/214 passou (PredictionMinecart37CompleteTests). Depois o payload
MinecartView ganhou scale e interpolação de scale/localPosition no sink; C#0 e
5/5 testes focados finais passaram (PredictionMinecart37ScaleFinalTests). Ambos
relatórios xml/log preservados em Logs do checkout principal. Não declarar que a
suíte214 foi rodada após esse ajuste final; o subset5 valida o trecho alterado.
Python40/40 e UTF-8/BOM/diffcheck passaram. Nenhuma partida manual entre peers.
SourceNativeC04 offline/servidor preserva algoritmo/RNG; gate só no client. C04
continua com comparação manual e possível previsão de entrada em discussão após
capturas; transporte de cart/rider/FX foi implementado. C05 Portal7/swap/masks e
C06-C10 demais, R02-R08/A11/A12/A14/P0 permanecem conforme checklist.
Próximo: enviar37 e acompanhar CI, continuar C05 com eventos aprovados/preservação
de VFX e máscaras (não copiar cegamente corpo de carrinho para portal de player).


## Checkpoint após envio37 / auditoria C05

37 salvo/enviado068d522ca, CI40371185855 em andamento. Último confirmado36
(1b8dbb449 / CI40362231252 sucesso). Runner usado pelo CI37; não copiar fontes.
Diretório só com fontes/TitleScreen do usuário e este checkpoint ainda não salvo.
Relatórios214 +5 preservados na raiz Logs. Próximo implementar C05, ainda sem código.

Auditoria C05: SwapGroundTileBlackoutHandler troca tileA/tileB e blackout; não é um
teleporte de player. Tratar a parte de tile/evento como C09. Portal7 Update/TryHandle
faz gameplay no client sem gate explícito. TeleportRoutine tem default corpo oculto
movido por Lerp e ramo sink; marcar scope aprovado só depois de preservar VFX.
Stars7 têm mesma curva que Cart12, mas ainda não receberam mensagens aprovadas.
Stage_3-4 usa CreateFixedPortalMasks: SpriteMask branco1x1 chamado
Stage34PortalFixedMask, posição/scale mundos, sorting/layers/cutoff e sprites com
VisibleInsideMask. Mask não é SpriteRenderer e não é capturado pelo stream co-op.
Também faltam scale/mask da pose de player/NMA (NPA hoje só runtimeOffset + sorting;
transform.localPosition de sink não é igual ao runtimeOffset). Para C05 completo,
transportar máscara e pose reais sem ativar scripts/física de cena no client.
NativeMaskSprite usa Texture2D.whiteTexture Rect(0,0,1,1), PPU1; pode criar view
nativa sem collider/GameObject de gameplay. Native AnimatePortalSinkRise pode
sair cedo por ending sem passar no cleanup de snapshots/masks: revisar try/finally
sem reposicionar actor após uma transição de ending. TickCoopWorld é chamado por
OnlineRoomSession.Update para Battle também, mas retorna cedo fora de co-op; novo
stream de máscaras precisa ocorrer antes desse return e limpar com ClearCoopWorld
(+OnStopClient) em todos os fluxos. Não marcar C05 completo com só scope/gates.


## Continuação38: Portal7 e poses completas (em validação)

CI37 confirmado sucesso. Lote38 adiciona NetworkVisualPose relativo ao actor,
com proteção do gameplay root e baseline para retorno do owner à predição.
Pose da cabeça/montaria/world mount inclui escala/rotação/offset herdado/máscara.
Portal7 passa a marcar movimento aprovado e recusar gameplay client; estrelas
Battle usam PortalStarView compartilhado com cart, co-op conserva stream existente.
Máscaras Stage34 possuem stream completo limitado/revisionado com empty-clear.
Sink/rise limpa escala/máscara em Dispose e OnDisable sem reposicionar ator
interrompido. Renderer oculto deixa de produzir frame visível de player.

Primeira suíte Unity38:217/219. Duas falhas foram de fixture: Rigidbody de GO
inativo não aceitou o posicionamento utilizado na assertiva de interrupção.
Fixture corrigida para corpo ativo, com assertiva da pré-condição. Revisão também
adicionou limpeza explícita OnDisable e rebind de baseline ao trocar mount.
Rodada final ampliada em andamento: PredictionPortal38FinalTests.xml/.log.
C#0 erros/avisos e Python40/40 passaram. NÃO declarar Unity38/publicação ainda.

Auditoria preliminar C06 para próxima etapa: WaterCycleCurrent trava input e
mantém override externo durante push (não é locomoção livre); apresentar caminho
aprovado, preservar freio pelo input real e cleanup/pausa. FloatingPlatform trava
input durante transporte, mantém múltiplos riders e centraliza Rigidbody em
LateUpdate; precisa scopes por rider, cleanup e alinhamento com world stream.
BoatRideZone deixa player dirigir, altera passTaggedObstacles/água e suprime o
visual do corpo; esse contexto não está em NetworkMovementState. Não aplicar um
scope de voo travado para fingir predição de barco: transportar contexto aprovado
para motor/replay e apresentação do suporte, sem prever embarque/invulnerabilidade.


Validação final38:221/221 Unity passaram na rodada ampliada, incluindo os quatro
casos de interrupção/dispose/disable. C#0 erros/avisos novos, Python40/40 e
UTF-8/BOM/diffcheck passaram. Relatórios preservados em Logs/PredictionPortal38FinalTests.xml
e .log. Próximo: commit/envio/publicação38, confirmar CI e continuar C06.
Nenhuma partida manual com dois peers foi realizada; plano completo segue aberto.


## Após envio38 / início39 correnteza

Commit179b6e185 enviado; CI40377824543 em andamento. Não usar runner enquanto
CI estiver ativo. Última publicação confirmada ainda37.
Implementação39 em andamento: WaterCycleCurrent owner com scope aprovado,
gates pureclient, pausa e cleanup de push em Dispose/OnDisable. Servidor mantém
freio pelo input real; posição Rigidbody/Transform coerente na rede, mantendo
MovePosition offline. Barrier de saída foi incluído no NetworkMovementState:
owner avalia e libera uma cópia histórica durante motor/replay, sem mutar o mapa
estático do servidor. ProtocolVersion/supervisor39 alinhados. Ainda sem testes
novos/Unity39/publicação. C# em execução12696. Boats/plataformas continuam pendentes.


39 também cobre a plataforma flutuante: scope por rider até desembarque/disable,
poses Rigidbody/Transform coerentes e gates de simulação client. Quando a plataforma
centraliza o rider, o servidor suspende a predição de caminhada inclusive na parada;
isso não trava inputs no servidor nem altera o embarque offline. A última posição
de desembarque é aplicada antes do reconcile final. Stream visual co-op existente
continua responsável pelo suporte. Pausa congela a plataforma.
Novos testes de barrier/replay, correnteza pausa/cleanup e plataforma multi-rider
adicionados; C# passou sem erros/avisos novos. Ainda sem Unity39, aguardando CI38
liberar runner. Boat e composição/contexto de água seguem pendentes.


39 R02/C06 alinhamento temporal: o world stream co-op agora usa o mesmo offset
de NetworkTransform do local player para selecionar/interpolar poses e avançar
playback; bombas conservam seu offset próprio. Corrige suporte desenhado adiantado
em relação ao rider. Teste novo cobre multiplier/timelineOffset/fallback sem player.
C# final passou0 erros/avisos. Ainda sem Unity39 (CI38 usa runner).


CI38 confirmado sucesso. Runner liberado, iniciada suíte OnlineRoom completa39
PredictionWaterPlatform39Tests.xml/.log. Novos testes ainda sem resultado.


Auditoria C08/A14 próxima: Bomb.StartKick / TryRedirectKick / StartMagnetPull não
possuem guard de autoridade no próprio entrypoint; dependem dos callers. Handler
MagnetIndestructibleTileHandler inicia ScanRoutine no client, sem gate explícito;
co-op desativa scripts depois do carregamento, mas não confiar só nisso. Analisar
gates e transporte da direção visual antes de mudar handler/tile. Motion Search/
Magnet já usa BindNetworkBombMotion e segmentos aprovados; reutilizar presenter.
Não presumir que esta auditoria identifica um bug reproduzido nos testes do usuário.


C08 handler de ímã ocorre em BattleMode_14 e Stage_2-1 (GUID conferido nas cenas).
Não basta desativar ScanRoutine: direção visual/célula aprovada precisa transporte
Battle14 e baseline/refresh para observadores; co-op já usa tile stream. ResetDirection
atual reescreve tiles no OnEnable; no client deve apenas ler estado aprovado.
Aguardar39 passar antes de ampliar o lote; nenhuma implementação C08 foi feita.


Unity39 primeira rodada223/227: faltavam NetworkIdentity e MovementController
nas fixtures novas que adicionavam somente NetworkPlayerInput (este componente
não os adiciona automaticamente). Identidade corrigida: clock1/1 passou; as três
fixtures restantes ainda precisavam do MovementController/corpo. Corrigido e
executando quatro casos focados em PredictionWaterPlatform39FocusedReadyTests.xml/.log,
exec36726. Nenhum resultado final39 presumido. Uma rodada focada intermediária
usou cópia antiga após erro de caminho; não usar seu relatório como validação.
Depois dos focados, rodar suíte completa39 e copiar relatório/log para Logs raiz.


Focados39: clock e plataforma passaram após fixtures completas. Correnteza
validou pausa/avanço/scope, mas encontrou bug real na restauração: o setter
SetExternalMovementOverride limpa hazardDamage. FinishPush agora restaura override
ANTES de hazardDamage. Executando suíte completa final39 em
PredictionWaterPlatform39FinalTests.xml/.log, exec61224. C# final precisa repetir
após essa troca de ordem. Não publicar39 antes de terminar.

Auditoria R07/C08 adicional: Bomb.BindNetworkBombMotion(null, kind) resolve só o
owner.NetworkBombFx, sem fallback para bomba sem owner. ReportNetworkBombMotion
não emite sem presenter/NI. Se owner-clone fica inativo após morte, RPC pode chegar,
mas LateUpdate desse presenter não roda. Avaliar transporte/apresentador existente
com lifetime independente do owner antes de declarar movimentos ambientais/world
bombs completos. Não transferir tick/posse de input para outro jogador por conveniência.
C05 bomba segurada no portal também precisa auditar visibilidade do carry presenter,
pois ocultar o SR nativo no servidor não implica ocultar um proxy de bomb motion.


Final39:227/227 Unity passaram, incluindo restauração de hazard na ordem correta,
clock/offset, barrier independente no replay e plataforma multi-rider. C#0 erros/
avisos novos, Python40/40, UTF-8/BOM/diffcheck passaram. Relatórios finais copiados
para Logs raiz. Runner restaurado nos arquivos tracked de validação. Próximo:
commit/enviar39, confirmar CI e continuar C08/A14 ou barco com as limitações acima.
Nenhum manual dois peers realizado; plano permanece em andamento.


Após envio39: b9635e4e3 / CI40381560929 em andamento. Último deploy confirmado38.
Início40 C08 ainda não validado: Bomb StartKick/TryRedirectKick/StartMagnetPull
recusam gameplay puro client no próprio entrypoint; redirecionador6 gateado.
Magnet handler client não inicia scan nem muda direção por explosão; startup lê
tiles sem resetá-los. Novo MagnetFieldPhase publica cena/path/revisão/time e
células/direções aprovadas Battle14, limitado256 com heartbeat500ms, buffer32 e
pausa. Client usa só aplicação visual/read-only cache; não reativa coroutine.
Não restaura magnet sobre pedra de SuddenDeath. Co-op conserva tile stream.
Protocolo40 alinhado. C# em execução; nenhum teste Unity40 ainda. Completar testes
wire/fase/rejeição/autoridade e aguardar CI39 liberar runner antes de copiar40.
Bomba ambiental sem presenter/owner inativo continua pendente R07; não declarar
C08 completo nem prever escolha do alvo/efeito magnético.


Retomada após crédito: CI39 confirmado sucesso (b9635e4e3/40381560929). Checklists
atualizados:12 IDs completos em código/testes,13 parciais,8 pendentes. Lote40 ainda
não testado Unity. Runner agora livre; copiar apenas arquivos próprios de40 e metas,
rodar suíte OnlineRoom e testes BattleModeCom que cobrem Magnet, sem tocar Editor raiz.


Unity40 em execução no runner isolado: filtro OnlineRoomEditModeTests +
BattleModeComEditModeTests, PredictionMagnet40Tests.xml/.log, exec74136. Não copiar
mais arquivos enquanto roda. C#0 erros/avisos novos passou. Novos testes cobrem
fase/revisão/tempo/pausa/stone replacement e entrypoints puros client. Nenhum
resultado Unity40 presumido. Preservar fonte/TitleScreen do usuário na raiz.

R07 proposta para próxima etapa (ainda sem código): reutilizar kickPlayback e o
presenter existente com vida de apresentação independente do player. OnDisable
atual apaga motions confirmados e LateUpdate para. Separar cancelamento de ações
previstas do player de motion confirmado de bombas; manager existente pode tickar
presenters inativos explicitamente até handoff/despawn. Sem transferir input/posse.
Bomba sem owner ainda precisa transporte de evento aprovado via manager existente;
resolver junto com lifecycle, com testes morte/scene/timeout antes de marcar R07.


40 OnlineRoom229/229 passou. O filtro combinado NÃO descobriu BattleModeCom,
pois a classe está no assembly runtime (fora de Editor). Não declarar suíte COM
executada com esse XML. Para validá-la sem alterar source layout da raiz, movidos
SOMENTE NO RUNNER os arquivos Assets/Tests/EditMode/BattleModeComEditModeTests.cs
E .cs.meta para Assets/Tests/EditMode/Editor/ (mesmo nome/GUID). Rodando COM separado
PredictionMagnet40ComTests.xml/.log, exec16113. Resultado pendente.
IMPORTANTE ao terminar: copiar relatório/log para Logs raiz; remover somente os
dois arquivos temporários Editor/BattleModeComEditModeTests.cs +.meta no runner,
e git restore nos originais tracked mais demais arquivos de validação. Não iniciar
CI40 com a fixture movida. Não mover nem limpar nada no checkout raiz do usuário.
C#0 erros/avisos novos e Python40/40/UTF-8/BOM/diffcheck passaram.


A14 descoberta COM40: mover a classe antiga para Editor temporariamente revelou
que ela usa helpers internos do assembly runtime, então a execução temporária não
compilou. Arquivos originais restaurados no RUNNER; temporários antigos removidos.
Solução em código (ainda validando): base BattleModeComEditModeTests torna-se
abstrata no mesmo arquivo/assembly; wrapper concreto BattleModeComEditorTests em
Editor herda os testes, sem mover código nem expor helpers de gameplay. Perfil
full do pipeline inclui wrapper; quick permanece rápido/sem executar suíte.
Rodando OnlineRoom + wrapper em PredictionMagnet40CompleteTests.xml/.log,
exec66372; resultado pendente. Não declarar COM executado até conferir XML.


A14 execução real COM: wrapper descobriu92 casos;75 passaram,17 falharam.
OnlineRoom229/229 continuou passando. Falhas antigas majoritariamente fixtures
sem Collider2D concreto (RequireComponent abstrato impedia criar componentes),
Awake/caches não inicializados em Edit Mode, profiles de input vazios e Tilemap
inativo no teste de bounds. Corrigida preparação das fixtures, preservadas todas
as assertivas; nenhum gameplay COM/Search foi alterado para satisfazer testes.
Executando COM92 novamente em PredictionMagnet40ComReadyTests.xml/.log,
exec21942. C# em57497. Não declarar suíte completa321 aprovada ainda.


COM40 corrigido:92/92 passaram; nenhuma alteração de gameplay necessária para
consertar os17 casos antigos. Root C#0 erros/avisos novos passou. Agora rodando
OnlineRoom229 + COM92 juntos: PredictionMagnet40FinalTests.xml/.log. Não presumir
321/321 até conferir resultado. Perfil full seleciona ambos; Python32+8 passou.
Fixture COM original permanece em seu caminho/assembly; wrapper Editor herda casos.


Final40:321/321 Unity passaram juntos (OnlineRoom229 + COM92), C#0 erros/avisos
novos, Python40/40 e UTF-8/BOM/diffcheck passaram. Logs/XML finais preservados em
Logs/PredictionMagnet40FinalTests.*. Runner restaurado nos tracked de validação;
fixture temporária antiga Editor/BattleModeComEditModeTests.* não existe mais.
Commit/enviar40 e confirmar CI; último deploy confirmado39. C08/A14 seguem parciais:
manual, lifecycle de bombas sem presenter e matriz completa ainda pendentes.


Após envio40:3712ec468 / CI40405577413. Último deploy confirmado39 até conferir CI.
Próximo41 R07, escopo inicial: preservar motions de bomba já confirmados quando
owner-clone desativa. Extrair loop de bomb motions de PresentFrame; helper de
apresentação anexado ao manager existente em ordem200 ticka somente FX inativos,
sem rodar input/ability/gameplay. OnDisable cancela previsões/poses do player mas
conserva motion confirmado se NI ainda consta em NetworkClient.spawned. Limpar
em OnStopClient/scene/despawn e evitar duplo tick se desativar após LateUpdate.
Não transferir posse/tick de input nem recriar actor/physics. Bomba sem owner é
etapa posterior de transporte, não ficará resolvida por esse helper. Ainda sem código41.


41 implementado em andamento, não validado: NetworkBombMotionLifetime é helper
render-only no manager existente (ordem200 após NetworkTransform). Owner inativo
com NI ainda spawned conserva só motion confirmado; previsões/poses de player
canceladas. Loop de bombas extraído sem rodar rotinas/input do player. OnStopClient,
scene/NI despawn/destroy limpam e devolvem colliders; reativação retoma LateUpdate
nativo, sem tick duplo no mesmo frame. Suspensão explícita evita pose/bridge local
após desativação, preservando fixtures que invocam apresentação diretamente.
Protocolos41 alinhados. C# em execução; testes lifecycle ainda precisam ser escritos/
executados. Aguardar CI40 liberar runner. Bomba sem owner continua pendente.


41 C# passou0 erros/avisos novos após integrar StartBombMotionLifetime no
OnStartClient já existente da Riding presentation (não duplicar override entre
partials). Novo teste de owner inativo + confirmado/predito/pausa/stop/despawn
escrito, ainda não executado Unity. Bomba sem owner e VFX de explosão com owner
inativo (RpcPlay inicia coroutine no owner) continuam pendentes R07; este lote
preserva somente a continuidade de motion aprovado. Nenhuma nova posse/tick.


CI40 confirmado sucesso. Runner livre, copiados somente arquivos próprios41.
Executando OnlineRoom + COM em PredictionBombLifetime41Tests.xml/.log. Nenhum
resultado Unity41 presumido; não publicar antes de terminar.


41 primeira suíte321/322: único novo teste esperava stop antes do arco terminar;
fixture corrigida para completar arco. Focado seguinte ainda rejeitava stop por
serverTime artificialmente ANTIGO. Corrigida fixture para timestamp crescente e
avanço explícito do relógio Mirror, com restauração no finally. Executando foco
PredictionBombLifetime41ClockTests.xml/.log, exec87770. Runtime não precisou mudar
para essas assertivas. Acrescentados eventos diagnósticos opt-in owner_inactive/
owner_reactivated; copiar helper atualizado ao runner após o foco, antes da suíte final.


Foco41 corrigido passou1/1 com relógio/timestamps coerentes. Suíte final ampliada
agora inclui duas variantes do novo caso (OnStopClient e NI despawn restaurando
collider ainda em voo): PredictionBombLifetime41FinalTests.xml/.log, exec64831.
Helper atualizado com diagnósticos copiado antes dessa rodada. Não presumir
323/323 até conferir XML. A compilação C# e Python/UTF-8 estão sendo concluídos.


Final41:323/323 Unity passaram (OnlineRoom231 + COM92), C#0 erros/avisos novos,
Python40/40, UTF-8/BOM/diffcheck passaram. Novos casos cobrem OnStopClient e NI
removido com collider ainda em voo; confirmado continua, predito é cancelado,
pausa congela, stop ordenado/handoff limpa corretamente. Nenhuma rotina de player
é executada no helper. Relatórios finais copiados em Logs/PredictionBombLifetime41FinalTests.*.
Runner restaurado nos tracked de validação. Próximo: commit/enviar41, confirmar CI,
prosseguir R07 VFX/sem owner ou demais itens. Último deploy confirmado40.


Após envio41:218428e33 / CI40409435598 em andamento. Último deploy confirmado40.
42 R07 em implementação: RpcPlay agenda espera de VFX no helper ativo do manager,
mesmo quando o owner ainda está ativo (não perder coroutine ao desativar depois).
Fallback só usa owner ativo; nunca StartCoroutine em GO inativo. Clear do helper
cancela esperas em scene/client stop; espera também termina se BombController morre.
VFX continuam no método existente PlayNetworkExplosionVisual com collider de dano
desativado, nenhum dano/terreno é previsto. Native host não duplica explosão.
Teste de seleção do host/owner inativo/fallback/role escrito; C# em andamento.
Não validado Unity42. Bomba SEM owner e comparação manual de VFX continuam pendentes.


42 VFX agora captura prefab/dados aprovados antes de agendar, sem manter BombController
ou NetworkBombFx no enumerator (PlayOnTimeline estático). Isso permite que evento
já recebido sobreviva até à destruição do owner, não só à inatividade. O método
PlayApprovedExplosionVisual é comum ao presenter/fluxo existente e mantém collider
sem dano. Acrescentado teste da espera que continua após destruir owner e termina
na desconexão, sem iniciar física/coroutine Unity em Edit Mode. Ainda aguardar
compilação/testes42; nenhum resultado presumido. Não resolve RPC emitido sem owner
ou endpoint já removido antes da recepção, nem escolhe dano/terreno no client.

Auditoria C09/R08: WorldBlackoutRenderer usa MeshRenderer e roomBounds Collider
(que o co-op desativa), além de player/torch circles. Não está no sprite stream nem
whitelist. StageBlackout pode ficar ativo sob Canvas mas não atualizar overlay
inativo; SwapGroundTileBlackoutHandler não transporta estado por si. Precisa
apresentação aprovada de alpha/room bounds/luzes, sem reativar física nem confiar
em bounds de collider desativado. Nenhuma implementação blackout foi feita.


Auditoria C07: PusherPatrolAndPush NÃO trava input nem usa uma coroutine de voo.
Ele faz Collider.Cast, filtra normal/overlap e soma delta via MovePosition ao
mesmo tempo que a caminhada continua; depois notifica PlayerPushedOutOfInvalidTile
para eventual bounce. Não aplicar scope de voo travado. Precisa compor força no
motor/replay, com contato/fase aprovados e sem double-apply no FixedUpdate, mantendo
bounce de recuperação separado. Nenhuma implementação pusher foi feita.


CI41 confirmado sucesso. Runner liberado e copiados só arquivos próprios42.
Rodando OnlineRoom + COM em PredictionBombVisual42Tests.xml/.log; resultado
pendente. Não declarar325/325 nem publicação42 antes de verificar.


42 validado:325/325 Unity (OnlineRoom233 + COM92), C#0 erros/avisos, Python40/40.
Relatórios preservados em Logs/PredictionBombVisual42Tests.*; tracked próprios
do runner restaurados. Espera VFX conserva prefab/dados após destruir owner e
termina em disconnect; helper cancela esperas no cleanup. Nenhum resultado de dano
é previsto. R07 segue parcial para eventos sem owner/endpoint removido e manual.
Próximo: commit/publicação42 e auditoria das demais lacunas do plano.


Retomada43: confirmado325/325 no XML42, Python40/40 e C#0/0; commit42
a10595b74 enviado, CI40413037189 ainda em build/publicação. Fontes/TitleScreen
do usuário preservados. C10 teto Battle8 tinha relógio client independente.
Implementado RoofTileState com snapshots completos limitados512 células, catálogo
local de tiles, revisão/scene/path, buffer32 e timeline offset do player.
Awake/OnEnable/Apply/Update nativos não mudam teto no client; host/offline mantidos.
Inclui null explícito para células abertas/late join e heartbeat500ms.
Cinco novos casos Unity escritos (3 roles, buffer/timeline/pausa/validação e
captura de células abertas), ainda NÃO executados. C# passou sem erros novos;
rebuild exibiu6 avisos preexistentes de dependências Mirror. Aguardar CI42 liberar
runner antes de copiar43. C10 permanece parcial; powder/SuddenDeath/manual pendentes.


43 revisão: catálogo real da arena8 tem8 Tiles simples, sem prefab instanciado
nem collider (m_ColliderType0); não reativa scripts/colisão. Compilação C#
final0 erros/0 avisos, Python40/40. Cenário/prefab não precisaram de mudança.
Ainda aguardar Unity43; CI42 estava compilando cliente após servidor.
Auditoria C10 restante: powder não tem transporte aprovado de ignição/frames;
SuddenDeath tem feedback sem timestamp e Begin também sorteia caminho/voz no
client antes de substituir pelo path recebido. Não implementados neste lote.


43 ampliado para powder Battle3 enquanto CI42 usa runner: PowderTrailFrame
transporta somente frame/fim aprovado, revisão/scene/path/tempo, heartbeat500ms
e buffer32. Client usa tiles visuais gerados com Collider.None, conserva e
restaura piso/matrizes; não chama IgniteRoutine nem SpawnHitboxes. Gates explícitos
nessas entradas. Pausa/timeline comuns ao player, cleanup OnDisable. Dois novos
testes escritos (total esperado332 com roof), ainda NÃO executados Unity.
Novos partials/meta e protocolo43; C# recompilando. Nada43 publicado ainda.


43 final antes de Unity: C#0 erros/0 avisos, Python40/40, UTF-8/BOM/metas
verificados. Runner ainda ocupado por CI42; NÃO copiar enquanto build/deploy ativo.
Total esperado332 casos (325 anteriores +5 roof +2 powder), conferir XML.
Relatório planejado: PredictionRoofPowder43Tests.xml/.log. Nenhum33 ID mudou
de classificação; P0/manual e demais C/R continuam conforme checklist.


CI42 confirmado COMPLETO/sucesso. Copiados10 paths próprios43 ao runner livre.
Suíte OnlineRoom + COM em execução: exec44804,
Build/CI/PredictionRoofPowder43Tests.xml e Logs/CI/PredictionRoofPowder43Tests.log.
Conferir XML (esperado332, não presumir). Nenhum43 commitado/publicado ainda.


43 FINAL validado:332/332 Unity (OnlineRoom240 + COM92), C#0 erros/avisos,
Python40/40, UTF-8/BOM/metas/diffcheck passaram. Relatórios preservados em
Logs/PredictionRoofPowder43Tests.*. Tracked próprios restaurados no runner.
Novos casos validam roles offline/host/client, captura de teto aberto/fechado,
serialização, fase futura/pausa, revisão/scene/path/bounds, fim/cleanup e piso/matriz
da pólvora restaurados sem ignição/hitboxes no client. Referências nativas
confirmadas somente BattleMode_3 e BattleMode_8; nenhum YAML alterado.
Próximo: commit/enviar43, confirmar CI. C10 continua parcial por SuddenDeath/manual.


43 enviado: d993fe122 / CI40415169769 em andamento;42 é último deploy confirmado.
44 C10 em implementação: client copia path aprovado de SuddenDeath sem sortear
caminho/canto/rotação e não inicia cleanup nativo de Revenge. Voz continua sorteada
só na autoridade; duração de espera de música é transmitida em BattleFeedback.
Envio begin movido para a mesma coroutine de áudio após a escolha original da voz,
preservando a ordem nativa dos sorteios. Client não sorteia voz para temporizar.
Validação de begin/path/duração e drop somente após path recebido. Protocolo44.
Três testes novos escritos + wire existente ampliado; ainda não executados Unity.
C# exec61287 em andamento. Aguardar CI43 liberar runner antes de copiar44.
Ainda não corrige timestamp/curva de queda, commit de terreno, late join, etc.
C10 e contagem12 concluídos/13 parciais/8 pendentes permanecem iguais.


44 C# inicial passou sem erros/avisos novos (rebuild mostrou6 avisos antigos
Mirror). Wire de BattleFeedback também testa duration2.75. Testes esperados335
(332 anteriores +3 novos). Resultado Unity44 não presumido; runner ocupado CI43.


44 checagens locais finais: C#0/0 no incremental, Python40/40, UTF-8/BOM e
diffcheck passaram; novo wire duration também testado por roundtrip existente.
Ainda não executado Unity44. Auditoria C10: fall-start aprovado não comprova por
si toda decisão de terreno no pouso; precisa evento de commit e timeline alinhada,
sem deixar colisão/visual divergirem. Não implementar isso por inferência nem
declarar o problema resolvido pela remoção do RNG client. CI43 ainda compilava.


44 diagnósticos opt-in acrescentados: sudden_death_begin inclui path_count,
hash estável de coordenadas (sem RNG/instanceId/hash de plataforma) e music_wait;
sudden_death_drop_begin/finished incluem célula, posição e duração inicial.
Role/build/protocolo/timeline já vêm do logger comum. Permitem futuras capturas;
ainda não há comparação real de peers nem ganho de latência medido. Debug flow
client indica path aprovado em vez de imprimir um padrão local inexistente.
CI43 já concluiu builds e estava publicando servidor; aguardar job todo terminar.


CI43 COMPLETO/sucesso confirmado. Copiados5 paths próprios44 ao runner livre.
Executando OnlineRoom + COM: exec63361,
Build/CI/PredictionSuddenDeath44Tests.xml e Logs/CI/PredictionSuddenDeath44Tests.log.
Esperado335 (OnlineRoom243 + COM92), conferir XML; não presumir sucesso.
44 ainda sem commit,43 é último deploy confirmado. Fontes/TitleScreen preservados.


44 FINAL:335/335 Unity (OnlineRoom243 + COM92), C#0 erros/avisos novos,
Python40/40 e UTF-8/BOM/diffcheck passaram. Três casos novos validam path sem
RNG, wait de música aprovado sem sorteio client e begin inválido/drop sem path;
wire existente preserva duration e campos de expulsão. Relatórios copiados em
Logs/PredictionSuddenDeath44Tests.*;5 tracked próprios do runner restaurados.
Matriz manual por33 IDs criada e ligada ao checkpoint/skill; nenhum cenário
manual executado. Sem alteração de scene/prefab. Próximo: commit/enviar44 e CI.


44 enviado:9c6372abb / CI40417871378 em andamento.43 é último deploy confirmado.
Nenhuma alteração runtime45 iniciada. Consolidação de notas/skill e matriz manual
incluída em44; checklist permanece12 implementados/testados,13 parciais,8 pendentes.

Auditoria C06 barco para próxima implementação (NÃO implementado):
- BoatRideZone permite caminhar livremente e usa SetPassTaggedObstacles(Water),
  HidePlayer/HeadOnly e Physics2D.IgnoreCollision na autoridade. Não usar scope
  de voo travado nem conceder invulnerabilidade/embarque no client.
- NetworkMovementState ainda não contém canPassTaggedObstacles/passObstacleTag;
  quatro queries do MovementController leem flags atuais. Primeiro transportar
  contexto aprovado e usá-lo no motor/replay, sem mutar a permissão nativa client.
  Só BoatRideZone chama o setter atualmente; Water existe no TagManager.
- Bloqueio visual_suppressed impede predição de barco. Manter até ter presenter
  owner de barco/cabeça e descriptor aprovado; liberar agora causaria divergência.
- Original BoatRideZone/zonas são desabilitados no co-op; não reativar scripts,
  colliders, IgnoreCollision ou coroutines nativos para obter o visual.
- Projection precisa seguir raiz prevista, preservar quatro direções e offsets,
  material/sorting/escala, pele da cabeça, pausa e cleanup/rebind. NPA tem caminho
  próprio para owner e FX LateUpdate100; evitar disputar visibilidade/frame.
- Stream co-op usa IDs de SpriteRenderer DO SERVIDOR. Não recomputar GetEntityId
  no client para esconder duplicatas. Descriptor deve identificar os renderers
  aprovados para suprimir só o barco projetado do owner; observers mantêm stream.
- ASR oferece PlaybackState/PresentPlayback e campos visuais públicos; proxy pode
  conter só SpriteRenderer/ASR com dados visuais, jamais BoatRideZone/NI/collider.
- Pausa deve congelar pose em world space apesar de reconciliações; desfazer
  projeção precisa restaurar baseline sem esconder/ressuscitar pose de outra ação.
- BoatUnmountZone faz TryUnmount ANTES do SnapToWorldPoint final. Um reconcile
  forçado tem de ocorrer depois da posição final, não no meio do desembarque.
- Também auditar gates de TryMount/TryUnmount/CanMount e zonas ANTES do snap,
  reentrada, troca de mount, morte/despawn e supressão de inatividade.
Manual/curvas/contexto de barco permanecem C06 parcial; não há ganho medido novo.


Fechamento desta retomada: CI40417871378 COMPLETO/sucesso. Protocolo44/9c6372abb
publicado no servidor e updater, artefatos enviados.42 e43 também tiveram CI
concluída com sucesso. Conferidos33 IDs únicos:12 implementados/testados,
13 parciais,8 pendentes; consolidação/documentação é item separado. Sem nova
captura real de peers ou teste manual. O plano completo segue em andamento.
Próxima retomada: ler Estado atual/Checklist, notas44 e auditoria C06 acima;
não presumir barco previsto nem C10/R07 completos. Nenhum teste/processo de
validação próprio permanece em execução. Só fontes/TitleScreen do usuário
ficam como alterações runtime locais após salvar este checkpoint.


Retomada45 C06 em implementação: NetworkMovementState inclui taggedPass/passTag
históricos e descriptor aprovado do barco. Quatro queries do motor usam contexto
acknowledged durante tick/replay sem alterar flags nativas client. BoatRideZone
cacheia chave por hierarquia + posição local authored antes do autoanchor/follow,
para distinguir múltiplos barcos com mesmo nome. Captura IDs de SR do servidor,
flags/offsets/escala/rotação/follow para owner, sem conceder proteção/ocupação.
Native client Awake/Late/TryMount/TryUnmount e triggers de zonas têm gates;
reconcile do desembarque ocorre após snap final. Guard Server/NI evita chamada
[Server] em prefab offline não inicializado. FX owner cria só SR/ASR de barco,
usa cabeças atuais da skin e suprime somente views co-op dos IDs aprovados.
NPA/NMA respeitam posse de pose do barco; pausa restaura raiz antes da renderização.
C# inicial passou0 erros/avisos novos (6 avisos antigos de dependências no rebuild).
Revisão/testes ainda em andamento, nada45 publicado.44 é último deploy confirmado.
Contagem permanece12 implementados/testados/13 parciais/8 pendentes. Próximo:
finalizar fixtures de motor/pass, gates, owner view/pausa/cleanup e wire; executar
OnlineRoom+COM no runner somente se nenhum CI estiver ativo. Nenhum teste45 rodou.


45 testes em execução no runner livre: exec31556, PredictionBoat45Tests.xml/.log
(Build/CI e Logs/CI). Esperado342 casos (335 anteriores +7 barco), conferir XML.
Novas fixtures foram separadas em partial da MESMA OnlineRoomEditModeTests, sem
alterar descoberta/filtro CI. C# inicial e final intermediário passaram0 erros/
avisos novos; compilação final/Python em execução. Client view NÃO altera flag
externalVisualSuppressed, nem proteção/ocupação; usa só SR/ASR e cabeças atuais.
Pausa preserva direction/idle/frame e root antes da renderização. Scope contínuo
autoritativo (portal/transporte) desativa projeção para os presenters existentes.
Views do stream são restauradas imediatamente com a última visibilidade aprovada,
com dono por ID para cleanup antigo não liberar a claim nova. Source morto/view
removido também limpam. Testes ainda não tiveram resultado; não publicar45 agora.


45 primeira rodada342/342 PASSOU (250 OnlineRoom +92 COM), relatórios iniciais
preservados em Logs/PredictionBoat45Tests.*. Após essa rodada, acrescentado codec
opcional do descriptor (fora do barco só1 byte) e testes de handoff de claim,
cedência a transporte aprovado e NPA/NMA com mount. C# final0/0 passou; Python40/40.
Rodada FINAL agora em execução: exec90776, PredictionBoat45FinalTests.xml/.log;
esperado346 (OnlineRoom254 +COM92), conferir XML. Não extrapolar342 ao código novo.
NativeAssets verificados: RedBoat.prefab, Submarine.prefab e componentes em cena
Stage_2-3; Source também usado como prefab nas demais fases. Nenhuma scene/prefab
precisou ser alterada. Código45 ainda sem commit/publicação. Último deploy44.


45 segunda rodada346/346 PASSOU (OnlineRoom254 +COM92), incluindo codec1 byte,
claim transfer, retorno de scope aprovado e NPA/NMA sem reaparecer Louie/sombra.
Relatórios preservados em Logs/PredictionBoat45FinalTests.*. Acréscimo P0 necessário:
MotionDiagnosticProbe agora informa boat=True/False via contexto aprovado, sem
registrar ocupação nativa client. Comparador separa boat/boat_mounted de walking/
mounted e usa contexto NA ENTRADA, excluindo boat_context_pending da latência.
Dois testes Python novos e1 Unity de formatter/contexto; código do barco não mudou
após346. Rodada completa atual exec55137, PredictionBoat45DiagnosticsTests.xml/.log,
esperado347 (255 OnlineRoom +92 COM). Ainda conferir XML, não presumir resultado.


45 FINAL:347/347 Unity (255 OnlineRoom +92 COM), C#0 erros/avisos novos,
Python42/42 (34 CI +8 Directory), UTF-8/BOM/metas/diffcheck passaram. Reports em
Logs/PredictionBoat45DiagnosticsTests.*;342/346 intermediários também preservados.
15 tracked próprios do runner restaurados; novos arquivos/metas ficarão para
checkout normal do CI. Nenhuma scene/prefab alterada. C06 marcado implementado/
testado, com manual explícito pendente como os demais IDs concluídos. Contagem
agora13 implementados/testados,12 parciais,8 pendentes. A classificação P0 de barco
foi validada sem nova captura real. Próximo: commit/enviar45 e confirmar CI.


45 commitado/enviado:14b6f5cac, CI40442915878 em andamento;44 último deploy
confirmado. Tracked runtime locais só fontes/TitleScreen do usuário. Aguardar
CI antes de usar runner para novo teste/copiar arquivos. Não presumir deploy45.

Auditoria adicional C07/R04 (nenhum código46 implementado):
- PusherPatrolAndPush.FixedUpdate calcula delta até pointA/B (speed*fixedDelta),
  inverte direção com uma parada ao alcançar arriveDistance, faz Collider.Cast
  com useTriggers=true e filtra normal/overlap antes de MovePosition no player.
- A lista de hits pode conter mais de um collider do mesmo Rigidbody. No código
  nativo o MovePosition repetido usa a mesma posição física; uma migração para
  posição imediata/tick precisa deduplicar ator antes de aplicar o delta, para
  não introduzir push duplo. Contato inicial permanece validado pelo servidor.
- NotifyExternalPushed reinicia uma coroutine e só no frame seguinte avalia tile
  inválido/hole/SuddenDeath/bounce; não prever essa decisão de dano/recuperação.
- Não usar scope de voo travado: input continua permitido durante push. Compor
  uma vez no tick/replay e impedir aplicação paralela no FixedUpdate nativo para
  esse ator; checar catch-up de inputs versus fase de mundo/turno do pusher.
- R02 já captura shapes Box/Circle/Capsule com Rigidbody simulado e atualiza views
  a20Hz. A pose de query é aplicada diretamente; isso não é história temporal de
  força. Não inventar que todos os corpos móveis são unsupported no código atual.
- Hurricane quando o player foge já aplica multiplicador0.5 com prazo curto,
  coberto pela infraestrutura histórica A13; no outro ramo ainda usa MovePosition.
  Magnet e Bubble também têm MovePosition direto; Bubble tem distância finita de
  um tile, mas não trava input. Nenhum desses ramos foi alterado nesta retomada.
- Próximos testes C07: caminhada simultânea/contato parcial, atores com múltiplos
  colliders, troca de sentido/endpoint, pause, muro/hole e bounce autoritativo;
  validar offline/COM e owner/observador sem reativar o mundo client.


Fechamento45: CI40442915878 COMPLETO/sucesso confirmado, incluindo servidor,
updater e artefatos. Runtime14b6f5cac/protocolo45 publicado; nenhum código46
iniciado. Nenhum processo próprio de teste ficou em execução. Checklist conferido:
33 IDs únicos,13 implementados/testados,12 parciais,8 pendentes; manual não realizado.
Próxima retomada: C07, ler auditoria acima e preservar offline/COM/inputs simultâneos;
não presumir que force composition ou R04 foi implementado. Tracked runtime locais
restantes são só as três alterações do usuário (duas fontes e TitleScreen).


Retomada27/10 — C07 em implementação (ainda NÃO publicado):
- Baseline publicado segue14b6f5cac/protocolo45/CI40442915878 sucesso.
- NetworkPlayerInput agora distingue ack repetido com posição inalterada de um
  deslocamento autoritativo sem novo input. Forças/resolvedor não ficam esperando
  outro comando para enviar a posição; mesma cadência periódica30Hz.
- Pusher deduplica Rigidbody entre hits do mesmo cast e explicita gates de client
  puro/pausa. Mantém MovePosition pendente e queries no relógio de mundo original.
  Não mover posição imediatamente: isso altera os contatos do próximo pusher.
- Logs opcionais: pusher_step, pusher_resolve_grid_snap, movement_world_snapshot
  e movement_reconcile; correção depois de replay distinta de erro bruto aprovado.
  Gates/rate limit existentes; novos eventos limitados por ator/tempo.
- A primeira exploração com escrita imediata foi descartada na revisão. Seu XML
 360=359/1 está preservado em Logs/PredictionPusherExploratory45Tests.* e NÃO
  valida o código final. Cast sem contato também ocorreu na rodada nativa com
  Dynamic (358=349/9). Player.prefab real usa Kinematic/Circle radius0.2 offset
  Y-0.1; não presumir Dynamic. Rodada focada seguinte volta a Kinematic, avança
  física uma vez na montagem e verifica bounds/count/normal antes do push.
- Sem novo wire/RPC: protocolo permanece45. C#0/0 passou antes do último evento
  diagnóstico; Python42/42 passou. A rodada nativa falhou358=349/9, reports em
  Logs/PredictionPusherNativeAttempt45Tests.*. Agora session77025: filtro
  PusherCastWithTwoPlayerShapesNotifiesTheActorOnlyOnce, relatório
  PredictionPusherContact45Tests.xml/.log; conferir falha de prerequisito da física.
  NÃO copiar sobre runner enquanto esse processo estiver executando.
- Código próprio não commitado. User dirt permanece só fontes/TitleScreen além
  dos arquivos próprios. C07 será PARCIAL: predição/continuação owner, replay do
  contato e handoff ao snap/bounce nativo ainda não implementados. Nenhum ganho
  de latência/manual foi medido. Próximo: resolver testes, compilar/checks, salvar
  reports, restaurar APENAS tracked próprios no runner, commit/push e confirmar CI.


C07 atualização da fixture: a falha não demonstrava diferença entre Dynamic e
Kinematic. A montagem a71000 unidades não produzia hits; a mesma montagem perto
(710 unidades), com corpo Kinematic real e caso Circle(radius0.2,offsetY-0.1),
passou12/12. Não atribuir a causa só ao bodyType. Física nativa é avançada
explicitamente na fixture e o contato/count/normal são pré-condições verificadas.
Nenhuma alteração de bodyType/collider foi aplicada ao Player.prefab ou ao jogo.
Suíte combinada atual session67093, esperado359=267 OnlineRoom+92 COM; C#final0/0.


C07 FINAL desta etapa:359/359 Unity (267 OnlineRoom +92 COM),12/12 específicos,
C#final0/0, Python42/42; UTF-8/BOM/meta/diffcheck passaram. Reports finais
preservados em Logs/PredictionPusherFinal45Tests.* e PredictionPusherFocused45Tests.*.
Tentativas anteriores com falhas foram preservadas e não são a validação final.
Restaurados somente os4 tracked próprios do runner; novos testes/metas ficam para
checkout normal do CI. Nenhuma scene/prefab foi alterada. C07 permanece parcial,
contagem13 implementados/testados,13 parciais,7 pendentes. Sem wire novo, mantém45.
Próximo: commit/push deste lote e confirmação do pipeline; não presumir deploy.
