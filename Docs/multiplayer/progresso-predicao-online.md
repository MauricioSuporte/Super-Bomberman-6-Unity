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

## Retomada em 28/10/2026 — correções reportadas em Battle Mode

Os ajustes de Rule Config/embaralhamento, alpha80%, direção inicial para baixo e
cursor até o fim da contagem estão no commit3267ab200 (protocolo52). O checkout
retomado está em1c590671e, sem alterações pendentes do usuário. Último Actions
consultado40596044890: completed/success. O histórico51 abaixo é a validação
anterior de predição, não a última versão do repositório.

Lote atual em validação (ainda não publicado):
- A12: corrigida conversão de hardware para máscara compacta do cart. PlayerAction
  inclui Start antes de ActionA; deslocar seu ordinal gerava A32/C128, mas o motor
  espera A16/C64. Host/offline e cliente agora usam o mesmo mapeamento explícito.
  A carrega, soltar A dispara, C acelera. O indicador confirmado acompanha a carga.
- A12: atividade de sala renovada somente por input válido/aceito/não vazio do cart;
  heartbeat/duplicata/overflow não renovam o prazo.
- Montaria: o arco Riding apresenta o jogador, não a montaria no chão. Sua presença
  não pode interromper a aplicação dos frames aprovados de NetworkMountAnimation.
- Drops: TryDropBattleItemsForDeadPlayer chama NetSpawn.Server após Instantiate,
  como outros spawns de itens; criação/coleta continuam autoritativas.
- Bombas/Kick: preservar progresso já exibido dentro de um trecho aprovado evita
  convertê-lo em offset que desacelera ao decair. Limite150ms e fim do trecho;
  não prevê outra célula, mudança de direção, colisão ou novo lançamento.

Testes/regressões e publicação serão registrados ao concluir. Não houve captura
manual com dois peers neste lote. Checklist continua15 implementados/18 parciais;
A12 ainda parcial (preview imediato de carga/recoil/muzzle e métricas pendentes).
Protocolo52 mantido: nenhuma alteração de wire/RPC/SyncVar neste lote.

## Estado atual

Atualizado em29/10/2026. O checklist distingue implementação/testes de validação
manual; o histórico preserva decisões, resultados e tentativas anteriores.

- **Publicado:** protocolo55, commit `76f2a948c`, pipeline
  [40868952214](https://github.com/MauricioSuporte/Super-Bomberman-6-Unity/actions/runs/40868952214)
  completed/success: servidor, cliente e atualizador; inclui R07/continuidade de
  trajetórias após destruição do presenter e as correções anteriores de co-op.
- **Validação local mais recente:**901/901 Unity, sem skips, C02/lifetime do canhão;
  C#0/0 e Python53/53. Report PredictionCannonLifetime55Full. A12 validado com2clientes
  e6disparos em PredictionRevengeInputs55Run2. R07 em PredictionPresenterClients55Run2
  manteve8amostras de movimento porpeer após transferência, sem novo evento.
- **Integração55:**2clientes Mirror reais em Battle por210s com2cargas de arena,
  itens em movimento e desmontagem Mole/Tank; co-op Stage_1-1 por90s com captura.
  Ambos sem erros/avisos. Headless; não comprova renderização nem input lag zero.
  Sorting/reconexão posteriores a esses binários validados na suíte881.
- **R07/A11:** trajetórias de bombas sem endpoint original, rolagem/push de itens,
  recuperação de peers, poses de montaria no mesmo histórico e continuidade do
  dismount. **A12:** handoff espera fim da fase apresentada; métricas já testadas.
- **R02/R03/R05–R08:** custos CPU/bytes por família instrumentados; primeira captura
  EnemyMovementController registrada sem baseline comparativo; guardas nativas e
  pausa de apresentação ampliadas. Curvas/ambientes/telegraphs ainda em auditoria.
- **Checklist:**17 IDs implementados/testados e16 parciais; todos os33 iniciados.
  Validação visual/manual continua pendente, inclusive em IDs marcados como feitos.
- **Próximo:** continuar C02/C03 (curvas e entrega ao motor) e demais parciais.
  A12 já teve integração de morte nativa, C e3cargas/releases porcliente. ClownMask/SunMask/
  Stage_3-6 já tiveram integração real documentada abaixo; manual visual pendente.
- **Workspace:** branch `v0.5.0`, checkout original. Nenhuma cena/prefab editada neste
  lote. Não fechar nem sobrescrever o Editor ou cenas/fontes do usuário.

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

Resumo: **17 de33 IDs concluídos em implementação/testes e16 parciais**.
Esta contagem não representa percentual de esforço; validação manual com dois peers
continua pendente mesmo em IDs marcados. Consolidação de documentação é item separado.

- [ ] P0: ferramenta/contexto implementados/testados, incluindo barco45 e logs de força/snap/reconciliação C07; novas capturas comparáveis pendentes.
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
- [ ] A11: parcial55 local: riding/dismount, fila visual e defer de remoção da montaria no arco vivo testados; smoke2peers Mole/Tank detectou teardown, corrigido em suíte878. Repetição e manual visual pendentes.
- [x] A12: motor/replay, carga/mira, recoil/muzzle, release tick, retorno confirmado e handoff testados; integração55 com2clientes, C e3cargas/releases porowner,6confirmações sem rejeição/timeout. Comparação visual/offline, rede adversa e hardware físico pendentes na matriz manual.
- [x] A13: velocidade/BombPass/DestructiblePass confirmados no replay; manual pendente.
- [ ] A14: parcial46: descoberta/fixtures COM e regressões ampliadas,423 testes combinados aprovados; matriz completa/manual pendentes.
- [x] C01: fase/curva/velocidade no tick e replay, testes; manual pendente.
- [ ] C02: parcial47 publicado: base36 + limite visual aprovado para salto alto Battle4; suíte conjunta439/439 passou. Curvas completas/ambientais/manuais pendentes.
- [ ] C03: parcial47: preparação/voo36 e deformação ambiental47 aprovadas/testadas; auditoria final do voo/devolução ao motor e comparação manual pendentes.
- [ ] C04: parcial37 publicado: transporte de cart/rider/loop/estrelas e gates testados; boarding previsto e manual de entrada/saída/portais pendentes.
- [ ] C05: parcial38 publicado: Portal7 owner/pose/máscara/estrelas e gates; manual e auditoria do swap/blackout C09 pendentes.
- [x] C06: correnteza/plataforma39 e barco45 (água no replay, owner view/cabeça, gates, pausa/cleanup e handoff) implementados/testados; manual pendente.
- [x] C07: continuação owner46 limitada a contato/célula/fase aprovados, caminhada/replay e interrupções testados (378 combinados); manual pendente.
- [ ] C08: parcial40 validado: campo magnético aprovado e gates client; continuidade/R07 e manual pendentes.
- [ ] C09: parcial48: caldeira/pull ordenado sem previsão/lead, gates de swap/dinamite/foguete, blackout/room lights e materiais de máscara aprovados,445 testes combinados; fases/tile Startup/capturas/render gráfico ainda pendentes.
- [ ] C10: parcial44: FallingBomb36, teto/powder43 e path/espera de áudio SuddenDeath44 testados (335 casos); timeline/commit de terreno/late join/manual pendentes.
- [ ] R01: parcial36: tempo pose/posição e geração de montaria alinhados/testados; capturas/custo e validação entre peers pendentes.
- [ ] R02: parcial55 local: geometria/clock aprovados, instrumentação CPU e bytes de frame por família e analyzer testados. Primeira captura co-op2peers Stage_1-1 analisada; curvas e comparação de custo pendentes.
- [ ] R03: parcial55 local: perfil de corte/pausa ampliado para todas EnemyMovementController, guardas nativas auditadas com regressões parametrizadas; suíte878. Curvas/capturas por família e manual pendentes.
- [x] R04: Hurricane/Magnet/bolhas Submarine46 com receita aprovada, caminhada/replay, fase/prazo/clear e gates (423 combinados); manual pendente.
- [ ] R05: parcial47 publicado: registro de renderers plain e gates de Heli/Icicle/FishMan/Matango/FreezerVenus/Queen; focados8/8 passaram; suíte conjunta439/439 passou; demais famílias/curvas/capturas pendentes.
- [ ] R06: parcial50 validado: gates de SunMask/ClownMask/FreezerVenus/Mecha, Heart/Star e colisão global testados;19 focados R06/R08 passaram. Intro/telegraph/curvas/áudio/demais bosses e manual pendentes.
- [x] R07: itens/rolagem/push, ovos e poses de montaria alinhados; transporte independente de ator, migração de arco/fila aprovada após stop/disable/destroy e cleanup sem tomar pose/input de outro player. Suíte894 e probe2clients com8samples móveis por client após remoção,0erros/avisos. Manual gráfico/offline e casos ambientais continuam na matriz; publicação deste último lote ainda pendente.
- [ ] R08: parcial50 validado: gates de Penguin/SnowBall/barril/pilares testados em19 focados R06/R08; stream visual existente conservado. Curvas completas/áudio/ambientação/capturas e demais famílias pendentes.
- [ ] Consolidar skill e notas (atualizadas até55: motion independente do ator, itens/montaria, custos, reconexão e teardown); matriz atualizada com integração automática2peers. Manual gráfico e consolidação dos IDs parciais pendentes.

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


C07 enviado:commit ecc3caa4f (protocolo45, sem mudança de wire), pipeline
40448262003 em andamento.359/359 Unity e42/42 Python foram executados ANTES do
push. Último deploy confirmado ainda14b6f5cac; confirmar jobs de servidor e
updater antes de dizer que o lote C07 já está disponível. Runtime local restante
é só o user dirt (duas fontes e TitleScreen); checkpoint é o único diff próprio.


Publicação C07 confirmada em40448262003: builds Linux/Windows, deploy do servidor
Linux e canal do updater concluíram com sucesso. Upload dos artefatos em andamento
na última verificação; aguardar conclusão geral antes de registrar CI completo.
Skill de predição recebeu a distinção entre relógio de mundo e lote de inputs,
MovePosition pendente/contato de outro pusher e prerequisitos de testes de cast.
Validador quick_validate da skill passou; dependência PyYAML ficou isolada em
Logs/SkillValidationDeps (ignorado), sem alterar dependências do projeto/sistema.
C07 segue parcial; próxima etapa é contato/continuação owner com histórico de
replay e handoff ao snap/bounce. Nenhum teste manual dois peers foi realizado.


Fechamento C07 parcial45:CI40448262003 COMPLETO/sucesso confirmado, incluindo
servidor, updater, cliente Windows e reports/manifestos/artefatos. Runtime
publicado ecc3caa4f; protocolo45 permanece.359/359 Unity (267+92),42/42 Python,
C#0 avisos/erros novos e skill validada. Checklist13 implementados/testados,
13 parciais,7 pendentes; manual segue pendente. Nenhum processo próprio de teste
ficou em execução. Nenhuma scene/prefab foi alterada, Editor principal preservado.
Próxima retomada: implementar a continuação owner de C07 sem duplicar a força de
mundo em cada input do servidor, incluindo contato histórico, endpoint/reversão,
saída de contato e entrega ao snap/bounce autoritativo. Não presumir que os logs
ou o envio de posição com ack repetido já implementaram essa predição.


Retomada autorizada27/10: usuário pediu seguir ATÉ TERMINAR checklist, sem parar
só ao terminar um lote. NÃO marcar o plano completo enquanto IDs/validação real
faltarem. Trabalhar serialmente: não há autorização de delegação nesta sessão.

C07 continuação owner46 em implementação, ainda NÃO publicada:
- Novos parciais PusherPatrolAndPush.Network.cs e MovementController.NetworkPusher.cs
  com metas; snapshots levam geometria/velocidade/filtros/célula segura/remaining.
- Só contato confirmado no passo atual do mundo, box alinhado sem edgeRadius;
  endpoint/reversão, multi-source e perda de contato falham conservadoramente.
- Owner compõe após motor/esteira por no máximo150ms e dentro da célula confirmada;
  restore/replay não move a fonte global, não chama resolvedor nem concede dano.
  Servidor conserva MovePosition uma vez no mundo, NUNCA aplica de novo no motor.
- DTO/wire mudou: Authenticator e Directory locais agora46 (último deploy45).
  NPI publica contato/clear mesmo com ack e posição repetidos.
- Logs/contexto pusher=True; comparador separa pusher_walking/mounted e usa entrada.
- C#0 erros/avisos novos passou (6 avisos Mirror existentes na primeira compilação).
  Teste novo OnlineRoomPusherContinuationTests.cs parcial da mesma fixture; Unity
  ainda NÃO executado para46. Python ainda falta rodar após novo caso do comparador.
- Runner estava livre, CI40448262003 concluído/sucesso. Fontes próprias copiadas.
  Próximo: rodada focada Pusher46, corrigir casos, suite completa e seguir IDs.
  Nenhum código46 foi commitado/enviado. User dirt3arquivos intacto; não fechar Editor.


C07 implementação46 VALIDADA:378/378 Unity (286+92), incluindo novos casos de
contato/filtros/circle/Box, replay/budget, caminhada relativa ao mesmo motor,
limite de célula, muro/hole, pausa, stop/bounce-lock, fonte ambígua/expirada,
endpoint, heartbeat/clear com ack/posição iguais e pacotes antigos/futuros.
A falha de caminhada era expectativa incorreta: o motor centraliza a linha
perpendicular. Comparar força com o MESMO passo sem força; SyncTransforms global
extra foi removido por não corrigir nem ser necessário.374/374 intermediários
preservados,378 finais em Logs/PredictionPusher46FinalTests.*. Python43/43 e
C#final0/0 passaram; integridade UTF-8/BOM/metas/diffcheck conferida.
Próximo: commit LOCAL de C07, depois R04 no mesmo protocolo46 ainda não publicado;
agrupar a publicação para evitar duas builds completas seguidas. Runner contém
fontes próprias C07 copiadas/dirty e novos arquivos/metas; NÃO restaurar para45
antes de copiar também a base C07 na próxima validação. Nenhum CI46 disparado.
User dirt3arquivos preservado. Continuar até checklist, não parar neste lote.


C07 commit LOCAL04189e84d (378/378 Unity,43 Python) AINDA NÃO enviado. Usuário pediu
continuar até checklist, portanto seguir R04 antes de agrupar publicação46.

R04 em implementação46, NÃO validado no Unity ainda:
- Novos NetworkEnemyForceState + interface e3 source partials, motor partial.
  NativeHurricane/Magnet/Bubble registram recibo; só o owner projeta força após
  motor, sem rodar IA/captura/dano. NativeMovePosition continua uma vez no mundo.
- Hurricane flee usa regra compartilhada e setter histórico de velocidade já
  existente; receipt só de slow NÃO concede pull não confirmado ao soltar input.
- Budget150ms/mesma célula/LOS/alinhamento/próximo wall/hole, fase dos frames de
  Hurricane, bubble remainingDistance; rejeitar outra cena ou confirmação velha.
  Scene também foi adicionada ao DTO de pusher46 (46 nunca publicado ainda).
- Recibo só vira continuidade quando posição física concorda com target esperado;
  fonte desativada/morta, contato/prazo expirado, fonte ambígua e pusher+enemy
  descartam forecast. Nenhuma força é somada duas vezes nos inputs servercatch-up.
- Pause/client gates Native; Hurricane eMagnet usam listas reaproveitadas sem
  hardcap de collider, teste cobre22shapes/2bodies e limpeza ao sair da visão.
- C#0/0 passou antes do último ajuste de Clear em listas. Novos testes em
  OnlineRoomEnemyForcePredictionTests.cs (mesma fixture parcial). Python comparador
  novo distingue enemy_force por espécie; ainda precisa rodar após novo teste.
- Runner mantém baseC07dirty própria+R04 copiado; não copiar fonte45 sobre esse
  conjunto. Próximo: rodada focadaEnemyForce, suite completa, checks, reports,
  commit R04 e publicar46 agrupado (sem interromper plano depois desse lote).
- Último deploy confirmado45/ecc3caa4f/CI40448262003sucesso. MainEditor/userdirt3
  arquivos preservados. Nenhum trabalho R03/R05/R06/R08 ou C09 foi implementado;
  C09 auditoria achou blackout MeshRenderer e instanced eye shader fora do stream.


R04 validação em andamento: primeira rodada38=4/34 falhou no guard de cena porque
UnityEditMode sem cena salva tem nome vazio válido. DTO rejeita null, mas aceita
stringvazia SOMENTE se igual à cena ativa; nada de renomear/salvar a cena do usuário.
Rodada isolada4/4 passou após isso. Suite422=421/1: apenas teste de caminhada
perpendicular Hurricane assumia pull; regra offline dot>0.01 considera fuga e
aplica slow0.5. Teste corrigido para a regra compartilhada, sem mudar gameplay.
Addedcaso de fase12→13ativa vsfim13→0 para não estender outro ciclo. Próxima rodada
final423 esperados (331OnlineRoom+92COM), relatório PredictionEnemyForce46FinalTests.*
no runner. C#final0/0 e Python44/44 (36+8) passaram no código antes do último teste.
Persistir reports finais, atualizar checklist/skill/docs e seguir publicação46
agrupada comC07local04189e84d, depois continuar os demais IDs como usuário pediu.


R04 FINAL:423/423 Unity(331OnlineRoom+92COM),44/44 Python(36+8), C#final0/0,
UTF-8/BOM/metas/diffcheck passaram. Reports finais em
Logs/PredictionEnemyForce46FinalTests.*. C07base04189e84d local, não enviado;
R04 ainda no workspace, protocolo46 nunca publicado. Source scene permite nome
vazio de cenaUnitynão salva, mas null/outra cena são rejeitados; mesma proteção
foi aplicada ao pusher. Nada foi renomeado/salvo no Editor principal.
Hurricane: preview usa a mesma fuga radial dot>0.01 do offline, inclusive lateral;
slow só no contexto histórico do owner. Slow confirmado não autoriza pull novo.
Fase12→13 pode continuar se ambas ativas; não projeta outro ciclo. Magnet mantém
raio aprovado; Bubble mantém distância restante. Registry de fonte só lê estado,
nenhum alvo/captura/RNG/gameplay nativo roda no client.
Listas das consultasHurricane/Magnet reutilizadas sem truncar:22shapes/2bodies e
saída da visão testados; não declarar zeroGC ou ganho medido sem profile real.
C07+enemy simultâneos e duas fontes diferentes descartam forecast; servidor mantém
seu último MovePosition nativo, sem somar por pacote. Stops/heartbeats revogam
contexto com posição/ack repetidos. Fontes desaparecidas e phase packets velhos
voltam à autoridade; dano continua servidor.
Próximo: restaurar APENAS tracked próprios do runner após preservar reports,
commit R04, enviarC07+R04juntos, confirmar publicação46 e continuar próximosIDs.
Contagem15implementados/testados,12parciais,6pendentes; manual não realizado.


## Continuação autorizada: C03 / protocolo 47 (27/10/2026)

CI46/40485513422 concluiu com sucesso em 509cb70d2 (C07 + R04). Artefatos de
validação e Windows disponíveis. Novo trabalho C03 ainda local: frames de corda
aprovados em overlay sem collider, gates de deformação/contato, revisão/histórico,
heartbeat/clear e restauração. Auditoria dos 32 tiles da cena não encontrou prefab;
todos usam collider de sprite, preservado na tilemap original. Sem cena alterada.
Focados Unity 4/4 passaram; C# incremental 0 erros/avisos e Python 44/44 passaram.
Suite combinada PredictionRope47FinalTests passou 427/427 (OnlineRoom335 +
COM92), XML e log preservados em Logs/. C03 permanece parcial: comparações manuais e auditoria
final do voo/devolução ao motor ainda pendentes. Novo message requer protocolo47
no Authenticator e Directory, ambos locais. C02 permanece só em auditoria.


## R05 e continuação C02 locais / protocolo47

CoopTrackRenderer reutiliza o registro pendente de renderers da sincronização
existente (20 Hz), evitando aguardar a descoberta de 250 ms em Heli, Icicle e suas
partículas, FishMan, fallback Matango, FreezerVenus e suas trilhas, Queen e trilha
pixel. Não garante efeito que nasça e morra entre dois ticks de 50 ms. Não há
extrapolação de alvo/homing, impacto ou dano no cliente. Factories e loops/contatos
nativos dessas famílias têm gates; clientes recebem apenas a apresentação.
Primeira suíte434:430 passaram,4 falharam (2 fixtures sem collider concreto e2
callbacks expression-bodied que delegavam contatos sem gate). Corrigidos; focados
PredictionProjectile47FocusedTests8/8 passaram, incluindo registro/dedup/cleanup.
Tentativa e resultado focado preservados em Logs/. Não declarar as demais famílias
Star/Heart/rockets/spear variantes ou todas as curvas R05 auditadas.

C02 auditou Battle4: lift20 tiles/0.1s excede o corte de4 unidades na interpolação
visual. AuthorityMotionScope agora aprova o limite apenas dos filhos visuais e o
transporta em NetworkVisualPose; player/mount usam a mesma aprovação histórica.
Default4, cap128, validação finita, fronteiras de início/fim e geração preservadas.
Cortes do transform root, renderer/frame discreto e gap>250 ms continuam separados.
Não é curva completa nem previsão de pouso. Testes de história de player/mount
foram ampliados para20 unidades com/sem aprovação e scope stale/nan/cap/cleanup.

Suite conjunta PredictionPresentation47FinalTests passou439/439 (OnlineRoom347 +
COM92); C# incremental final0 erros/avisos, Python44/44 e skill validator passaram.
Última publicação continua46/509cb70d2;47 pronto para envio. C03 está salvo em
927c4bac7; salvar R05/C02 no próximo commit. Preservar fontes/TitleScreen do usuário.


## Em trabalho: C09 / protocolo48 (não publicar ainda)

47 foi enviado em927c4bac7 +026d88302, pipeline40489646611 em andamento
(build/test Linux/Windows). Último deploy confirmado ainda46/509cb70d2.
439/439 Unity47 e44/44 Python estão preservados; não confundir com48.

C09 local48: enum Environment em BombMotionKind, início em elapsed0, stop e
continuação aprovados enfileirados como segmentos curtos, sem extrapolar próxima
célula nem antecipar pose de habilidade ou bounce Rubber. Boiler publica só o
movimento nativo80ms via NetworkBombFx existente; finally + OnDisable emitem stop.
Não muda fuse/dano/ignição, nem resolve o endpoint RPC de owner destruído (R07).
Gates nativos em Boiler/Swap/SwapBlackout/Dynamite/Rocket; query de raio PowerGround
continua readonly. DestructibleRocket registra rocket/target no stream e envia
SFX de explosão via GameAudioSettings. Ainda NÃO testado no Unity; runner ocupado
pelo CI47, nenhuma fonte48 copiada para lá. C#48 + novos testes compilaram0/0.
Novos testes em OnlineRoomEnvironmentBombTests.cs + meta: pull/stop tardio, pausa,
sem pose/collider alterado/extrapolação/Rubber, gates de mutação/RNG e query Power.

IMPORTANTE: não publicar48 só com esses gates. Blackout precisa transporte
aprovado antes: StageBlackout/WorldBlackoutRenderer mesh e materiais instanciados
BlackoutVisibleParts estão fora do stream atual. Primeiro implementar/testar a
apresentação dessas partes. Main renderer é puramente visual mas usa Collider.bounds
/OverlapPoint: no client o collider fica desabilitado, calcular bounds/contém a
partir de BoxCollider.transform/offset/size sem reativá-lo. StageBlackout pode
estar inativo no próprio Image GO; coordenador precisa referência IncludeInactive
e aplicar estado sem coroutine nativa. Fade usa unscaledDelta, não congelar só
por pausa. Enviar active/alpha e spotlights lógicos36 + room lights16; não replicar
valores dependentes de câmera headless. Olhos precisam material isolado no proxy
com região/paleta8/tolerância/bounds e barrel occlusion aprovada; fallback ao shader
padrão revela pixels indevidos. Não reativar enemy gameplay para conseguir olhos.

Progresso por ID não mudou com trabalho48 ainda não validado:15/13/5.
Preservar fontes e TitleScreen do usuário, Editor aberto e runner em CI.


## Continuação C09 blackout48 (implementado localmente, aguardando suíte final)

CI47/40489646611 concluiu com sucesso. Native48 já foi copiado ao runner somente
após esse término. Primeira tentativa Environment48:1/2 (pull passou; fixture
BombController sem BoxCollider ficou null); corrigido pré-requisito. Reports em
Logs/PredictionEnvironment48AttemptTests.*. Focados completos seguintes passaram
5/5 (PredictionBlackout48FocusedTests.* preservados): pull e stop juntos/pausa/sem
bounce, gates de tiles e RNG, bounds math no client com collider desabilitado,
receita de material wire/isolamento/capacidade8/barrel desconhecido, UI inativa
retomada, fade aprovado durante pausa, sem timer local e revisões inválidas.

Runtime48 agora inclui:
- BlackoutView: cena/caminho/revisão/histórico64, active/alpha,36 spotlights lógicos
  (sem depender de câmera dedicada),16 room lights, vision e roomActive aprovados.
  Coordenador mantém refs IncludeInactive, publica20Hz por mudança/heartbeat500ms,
  apresenta na timeline dos sprites. Fade acompanha unscaled do servidor. Awake
  visual idempotente permite reativar o próprio Image GO; timers/spotlights nativos
  client são gated. Clear de destroy retém o último contexto para tolerar ordem de
  destruição dos filhos. Nenhuma coroutine nativa é iniciada no client.
- WorldBlackoutRenderer permanece render-only na whitelist. Client usa Box offset/
  size/transform matemáticos sem reativar collider; offline/host preservam bounds
  e OverlapPoint nativos inclusive quando desabilitados. RoomActive é aprovado:
  impedir que desligar o collider da sala no servidor conceda visão local extra.
  Luz do próprio jogador acompanha seu root/facing apresentado, com raio aprovado.
- CoopSpriteFrame.blackout opcional (null compacto) transporta template, região,
  paleta8, tolerância, sprite/room bounds, barrel sprite/UV/flip/alpha/matriz. Material
  clonado por view; nunca altera template. Se máscara/occluder inválido ou ausente,
  esconde em vez de usar shader padrão que revelaria pixels. Cleanup em remoção/
  troca/clear de cena. Captura snapshots imutáveis; buffer8 de aplicação reutilizado.
  BlackoutVisibleParts só simula no servidor/offline e registra overlay prontamente.
  Template está nos catálogos Stage_3-1..3-6 (não em2-5, que usa UI legado).

Últimas revisões48 (roomActive/offline/clear) já na suíte combinada
PredictionEnvironment48FinalTests em execução; esperar XML. C# e Python44/44 já
passaram, mas rodar/confirmar o incremental final após os últimos C#.
Não afirmar imagem final ou ganho medido: render visual/manual ainda pendente.
Próximo após sucesso: docs/skill, UTF8/BOM/metas, commit/publicar48, confirmarCI,
seguir restante. C09 ainda parcial até auditoria de fases/tile Startup/capturas;
não marcar checklist completo só porque a suíte passa.


##48 pronto para publicação / correção final de herança de lead

PredictionEnvironment48FinalTests passou445/445 (OnlineRoom353 + COM92).
445 inclui duas variantes do pull: sem comando anterior e após Search previsto
pendente. Environment agora rejeita BeginAbilityPrediction e nunca herda o lead
do histórico anterior: a trajetória80ms começa em0 mesmo na substituição de ação.
Início/stop juntos e Rubber continuam cobertos. Suite444 anterior preservada em
Logs/PredictionEnvironment48Full444Tests.*; final445 em Logs/PredictionEnvironment48FinalTests.*.
C# incremental0/0, Python44/44, skill validator e diffcheck passaram; conferirUTF8/
BOM e metas antes do commit.48 ainda não enviado neste ponto do histórico.
C09 parcial, checklist15/14/4. C05/C02/C03/R05 demais auditorias seguem pendentes;
não marcar um deles concluído só com as melhorias parciais atuais.
Próximos auditados sem novas alterações: R03 Frog/Hop/SnowMan/Korobokkuru usam
stream20Hz mas sombras plain podem esperar descoberta250ms; falta gate nas overrides
Start/Fixed (base não basta). A12 exige input sobrevivendo ao player inativo e
transporte/predição próprios do cart; smoke_public_client cobre só lobby, sem partida.


## R03/49 validado localmente

48/4d364cc85/40508017471 foi publicado com sucesso.49 local, sem commit neste
ponto: proteção de Start/Fixed em base/Junction/Frog/Hop/Snow/Korobokkuru, callbacks
base de dano/fim/Kill e Snow bomb reaction não simulam client. Isso não audita as
125 overrides de todas as especializações; co-op continua desabilitando gameplay
de cena como barreira principal. Offline/host retêm caminho nativo.

CoopSpriteFrame.enemyStepLimit identifica as quatro famílias prioritárias e seus
visuais auxiliares registrados. Corpo e sombra usam a mesma timeline; gap>250ms
ou salto>1.5tile faz cut aprovado, sem cruzar a sala por interpolação. Durante
pausa, mantém pose/frame já apresentados; mensagens/remoções continuam recebidas.
O campo não é autorização de extrapolação/IA/dano. Não aplica esse limite a
projéteis/bosses desconhecidos. Prefabs atuais: tile1; Hop jump0.5s/height1;
Korobokkuru jump1s; Frog/Snow usam constantes. Configurações novas com arcos muito
maiores precisam revisar o limite e/ou fornecer curva explícita.

Sombras Frog/Hop e ondas/gotas Frog registram renderer logo após configurar;
Korobokkuru registra sombras existentes. Sprite dinâmico Frog é16x16 legível;
ondas32x32 também cabem no transporte. Máscaras de olhos dessas famílias recebem
mesmo perfil de movimento. Player/blackout alpha unscaled não foi congelado.
Focados7/7 e suíte PredictionEnemy49FinalTests452/452 (OnlineRoom360 + COM92)
passaram; reports preservados em Logs. C#0/0; concluir Python/skill/UTF8 antescommit.
Checklist15/15/3. R03 parcial, não afirmar validação gráfica nem ganho medido.

A12 auditado (ainda SEM alterações): NPI para quando player GO é eliminado; cart
não tem transporte dedicado; BombController.LaunchRevengeBomb não faz NetSpawn
antes de StartPunch; NetworkPlayerState.eliminated só vai true e não há retorno
sincronizado de Revenge. Precisa tratar conjuntamente input sobrevivente, cart
aprovado/predição de perímetro, bomba real, geração/ack e respawn confirmado. Native
cart move/charge/cooldown/recoil/enter/exit usam unscaled; input para em pausa, mas
coroutines enter/exit/recoil continuam. Não assumir clock de Time.deltaTime.

## Retomada em27/10:49 publicado e A12/bomba em50

CI40514393076 confirmado success para d1b2b3ef5/protocolo49. Workspace continha
somente as três alterações do usuário preservadas. Nenhum novo agente/worktree.

A12: MadBomberBomb.prefab já possui NetworkIdentity/NetworkTransform ServerToClient
e está nos spawnPrefabs de OnlineLobby; Player aponta para ele. O lançamento
nativo não chamava NetSpawn.Server, portanto o primeiro arco saía com netId0 e não
era enviado. Agora o spawn ocorre antes de StartPunch; falha usa NetSpawn.Despawn.
Client puro retorna antes de resolver tilemaps/instanciar/fuse. Novo kind Revenge
é arco aprovado: não faz previsão de elegibilidade/retorno, pose de soco/luva,
continuação de corredor, bounce Rubber nem herda lead de ação anterior.
O mesmo presenter ordena seus segmentos/stop e funciona com owner inativo.
Autenticador/Directory passam a50 por compatibilidade desse novo valor de wire.

6/6 focados passaram: cliente não cria bomba nem consome RNG; wiring real do
prefab/registro em preview scene sem salvar a cena aberta; Punch/Revenge com
owner inativo, pausa, stop enfileirado, cancelamento de previsão e cleanup por
OnStopClient/despawn. C# incremental0/0, Python44/44. Relatórios focados copiados
para Logs/PredictionRevenge50FocusedTests.*. Suíte completa50 passou456/456 (OnlineRoom364+COM92); reports preservados em
Logs/PredictionRevenge50FinalTests.*. UTF-8/BOM/metas/diff/skill passaram.

A12 continua parcial. Não há transporte novo de carrinho ou input após morte,
nem reativação confirmada do player; ServerMarkEliminated continua só true.
Não aplicar gates na criação client do carrinho até existir seu visual substituto.
Seu movimento usa unscaledDeltaTime e cooldown/charge usam unscaledTime; entrada,
saída e recoil continuam em pausa offline. Um timeout de input deve CANCELAR a
carga, nunca fabricar release que lança bomba. Novas mensagens devem autenticar
owner via conexão, separar geração/seq do cart do tick vivo e limitar fila/budget.
Amostragem local usa slot1, independentemente do P1-P6 atribuído. Não conceder
swap/retorno/eliminação por previsão. See roteiro detalhado na auditoria anterior.


## Trabalho local seguinte: R06/R08 (ainda sem Unity/sem commit)

50/bomba Revenge foi salvo em1677fb50b e enviado. CI40517686471 está construindo;
não copiar fontes para o checkout do runner enquanto ele executa.

R06: gates de OnEnable/FixedUpdate/callbacks/kill de SunMask e ClownMask;
FreezerVenus não inicia combate/ataque/movimento/dano/morte no client puro;
MechaBossSequence não inicia sequência nativa pelo Start/OnEnable. Heart/Star
não iniciam lifetime/alvo/orbit/contact no client. Star registra renderers plain
na criação e Freezer registra corpo/sombra, reutilizando CoopTrackRenderer.
Não foi criado predictor de IA ou escolhido alvo no client. A cena co-op já
suprime simulação; estes gates protegem entradas e callbacks antes/fora desse
passo. Não é auditoria de todos os bosses/intro/curvas/sons/eventos de ataque.

R08: gates em RocketPenguin (OnEnable/Update/LateUpdate/BeginShot/Schedule),
SnowBallTrap (Update/ApplySweep), BarrelPillarTrap (NotifyPillarDestroyed) e
BarrelPillarTrapPillar.TryDestroyByExplosion. Os renderers continuam pelo stream
aprovado existente; não foi adicionado forecast de colisão/terreno. Loops de áudio,
ambientação por câmera, demais mecanismos e qualidade visual continuam pendentes.

Novo OnlineRoomBossAuthorityTests.cs +meta contém19 casos planejados para entradas
nativas/RNG/contatos/ambiente e regra RNG offline. A contagem é de casos adicionados,
NÃO executados. C# incremental0/0 passou. Arquivos locais ainda sem commit.
Próximo: aguardar CI50 terminar, copiar somente Assets/Scripts e Assets/Tests
alterados ao runner, rodar focados e suíte completa, guardar XML/log em Logs,
validar UTF-8/metas/diff, atualizar contagem/estado e salvar/publicar lote.
Checklist mantém15/16/2 até validação efetiva; R06/R08 ainda não marcados parciais.


CI40517686471/1677fb50b finalizado success: build client/server, publicação do
servidor, canal do updater e artefatos confirmados. Runner liberado e fontes
R06/R08 copiadas. Focados em execução: PredictionBossEnvironment50FocusedTests;
conferir XML e total19 antes de declarar sucesso. Depois suíte completa esperada475.
Nenhum teste executado no Editor original, nenhuma cena/fonte do usuário alterada.


R06/R08 primeira execução focada19:9 passaram/10 falharam. Todas as10 falhas
vinham da pré-condição da fixture: um Rigidbody inativo ignorou position20,21 e
continuou0,0. Não alterar gameplay para compensar isso. Fixture corrigida usa um
Rigidbody ativo separado, ligado ao cache rb/bossRb nativo, e ASSERTA posição/
velocidade iniciais antes de invocar a entrada. Tentativa preservada em
Logs/PredictionBossEnvironment50AttemptTests.*. Segunda execução focada em
andamento (PredictionBossEnvironment50FocusedTests), ainda sem resultado confirmado.


R06/R08 corrigido:19/19 focados passaram. Report em
Logs/PredictionBossEnvironment50FocusedTests.*. Suíte completa em execução
PredictionBossEnvironment50FinalTests (esperado475=OnlineRoom383+COM92).
Checklist agora15 implementados/testados +18 parciais=33 IDs iniciados, sem
confundir início com conclusão. R06/R08 ainda sem commit/publicação. Nenhum novo
wire após o enum Revenge já publicado: compatibilidade permanece protocolo50.


R06/R08 lote validado:475/475 Unity completos (OnlineRoom383+COM92), incluindo
os19 focados novos, offline RNG e global collision regression. XML/log em
Logs/PredictionBossEnvironment50FinalTests.*. C# final0 erros/0 avisos novos;
UTF-8/BOM/metas/diff e skill passaram; Python44/44 já executados neste protocolo
(arquivos Python não mudaram neste lote). Runner restaurado somente nos fontes
rastreados próprios; não limpar fontes/cenas/settings gerados alheios. Próximo:
salvar/enviar lote; protocolo permanece50, sem novo wire. Manual/capturas não feitos.


Lote R06/R08 salvo/enviado em913518a53. CI40520719676 na fila na última consulta;
não copiar nem testar no runner enquanto esse job estiver ativo. Último servidor/
updater integralmente confirmado continua1677fb50b/protocolo50/CI40517686471.
Workspace voltou a conter somente os três arquivos alterados pelo usuário.
Próxima implementação prioritária é A12/carrinho + input após eliminação + retorno
confirmado, conforme auditoria registrada; faltam também partes dos demais18
parciais e execução manual/capturas. Não declarar o checklist completo.


## A12 carrinho/retorno em51: implementação local, ainda sem Unity

CI40520719676/913518a53 confirmado completed/success nesta retomada. Baseline
publicado50 contém R06/R08 e475 testes. Preservadas as três alterações do usuário.

Código local51 (ainda sem commit/publicação/testes Unity): OnlineRevengeCarts
transporta roster scene/revision/time/id/generation, frames/poses/indicadores e
input autenticado pela conexão/slot/identity. Manager recebe input mesmo com o
player inativo. Queue8 só usa créditos do FixedUpdate, suporta uint wrap e cancela
carga por timeout/overflow sem inventar release. Slot de hardware sempre1.
Controller nativo foi separado em motor/ação; motor previsto/replay não dispara
bombas/carga/som. Predição limita lacuna a250ms; história128 retém dt de cada tick;
observadores interpolam perímetro pelos8segmentos, sem diagonal entre paredes.
Frames/sorting/recoil/HUD são visuais; clone não executa gameplay/physics.
Native entry/exit continuam em unscaled durante pausa, como offline. Pausa do
motor retém pose já apresentada. Novo valor51 no autenticador e Directory.

NetworkRevengeReturn: início confirmado reativa apenas a apresentação do player
eliminado e bloqueia input/collider; Riding pode mostrar o arco aprovado apesar de
life0/isDead. Fim confirmado transporta stats/posição/ack, limpa histórico/ações,
restaura movimento e aplica ClientApply sem LoadInto do loadout base da máquina.
ServerMarkEliminated reseta fase/revisão; hooks e RPCs têm guards de papel/serial.
Callback final é publicado antes do bounce seguinte. Gameplay de swap/eligibilidade
continua nativo do servidor. Novas SyncVars/RPCs exigem builds51 compatíveis.

24 casos focados adicionados em OnlineRoomRevengeCartTests (ainda NÃO executados):
queue/budget/overflow/quicktap/wrap; política de sender; paridade de motor nativo
com previsão nos cantos/C; replay com dt histórico; wire/pose/clone visual;
retorno confirmado com stats e guards offline/unspawned. C# anterior0/0 passou;
C# final com testes ainda em execução. Próximo: focados, corrigir falhas reais de
fixture/implementação, suite completa esperada499, Python44, UTF8/metas/diff/skill;
revisar pausas/roster/limites/late join antes de publicar. Não marcar A12 completo.


A12/51: focados25/25 passaram (Logs/PredictionRevengeCart51FocusedTests.*).
Tentativas iniciais preservadas (Attempt/SecondAttempt/Core24). Fixtures corrigidas:
Health Awake para renderer cache; cart ativo em preview scene para capturar poses;
ASRs permitidos somente em atualização manual; base e stage distintos no teste
para comprovar que LoadInto não traz o loadout antigo ao respawn. Guard real
netIdentity!=null foi adicionado às entradas de retorno/unspawned.

Revisão detectou e corrigiu o início do Riding: só um arco CONFIRMADO pode
bypassar life0/isDead quando HasApprovedRevengeReturn está ativo. O teste25 prova
que um gesto não confirmado continua rejeitado e que a apresentação não dá vida.
Também foi adicionado ServerMarkEliminated ao victim removido pelo swap (o fluxo
antecipado não passava pelo OnDeathSequenceEnded normal). Cliente não autoriza
TryHandleLethalRevengeHit. Cursor de pouso permanece na célula mundial aprovada,
sem ser arrastado pelo root previsto. Diagnostics incluem revenge_cart_authority
no servidor e revenge_cart_present após apresentar, com cart/gen/tick/ack/pending.

Suíte completa500:499 passaram/1 falhou no UnityTest real
EarlyDeathIsEvaluatedWithoutAnotherDeathAfterRoundReload. Causa: host não registra
handlers de views remotas por usar o caminho nativo, mas o novo SendToReady incluía
localConnection (Unknown message59196). Corrigido para enviar somente a peers
remotos ready/autenticados. Logs/PredictionRevengeCart51FullAttemptTests.* guardados.
Teste host em execução: PredictionRevengeCart51HostTests. Depois repetir suite500.
Ainda SEM commit/publicação51; último servidor confirmado é913518a53/protocolo50.


Host51 corrigido passou1/1, cobrindo duas rodadas reais no Unity PlayMode
(Logs/PredictionRevengeCart51HostTests.*). Suíte completa500 em repetição no runner.
Recoil aprovado agora pode terminar unscaled durante pausa sem mover root/frame;
regressão de pose também verifica cursor mundial e offset. Código congelado para
validação final. C# final ainda em execução40797; Python44/44 já passaram.
Manual entre peers não foi realizado; A12 permanece parcial (preview de carga,
recoil/aim/muzzle e handoff/capturas ainda não concluídos). Sem publicação51.


A12/51 lote FINAL validado:500/500 Unity (OnlineRoom408 + COM92),25 focados e
host de duas rodadas. Reports finais em Logs/PredictionRevengeCart51FinalTests.*;
Initial500/Speed500 preservam as duas suítes aprovadas intermediárias. A última
execução inclui velocidade aprovada restaurada antes da predição, reset de luva,
uint inicial por ack, recoil em pausa e budget pausado sem crédito acumulado.
Pause preserva A held sem gerar release, mas descarta fila/créditos ignorados.
C# final0 erros/0 avisos, Python44/44; byte/BOM/metas/diff e skill passaram.
Runner restaurado somente nos caminhos rastreados próprios antes do envio.
Nenhuma cena/prefab editada, nenhum Editor do usuário fechado/salvo. Próximo:
salvar/enviar e confirmar CI51. A12 parcial e checklist15 implementados/18 parciais.


Commit b6a886569 salvo e enviado. CI40558343289 em execução; não usar/copiar fontes
no runner enquanto o job estiver ativo. Workspace contém somente os três arquivos
modificados pelo usuário (duas fontes e TitleScreen). Sem Unity/test/.NET próprios
rodando fora do CI. Próximo ponto A12: antecipação visual de carga/recoil/aim/muzzle,
conservando elegibilidade/cooldown/célula/tick confirmados, e handoff/manual.
Auditoria de input detectou ainda uma pendência de ciclo de vida para o próximo
lote: registrar atividade de sala em input de cart remoto aceito (o NPI do morto
não executa CmdControls). TickRoomTimeout já cobre hardware do host, mas o novo
ReceiveRevengeInput não chama RegisterRoomActivity; isso deve ser alinhado antes
de homologar cenários longos com todos os humanos eliminados e só COM na arena.
Não misturar essa pendência com confirmação do build/deploy deste lote.


CI40558343289 confirmado completed/success para b6a886569/protocolo51. Builds
Linux235MB/Windows972MB sem erros, deploy do servidor, canal do updater e artefatos
concluídos. Documentação de fechamento salva separadamente com skip ci. Última
suíte final500/500 e C#0/0 correspondem ao código publicado, com correção de host,
velocidade confirmada e pause budget incluídos. Não há processos próprios Unity/
testes/.NET pendentes; apenas a cópia de trabalho do usuário permaneceu alterada.

Retomar pelo RegisterRoomActivity dos comandos remotos válidos do carrinho e depois
pela antecipação visual de A12. Antes de alterar wire/RPC/SyncVars, subir protocolo
para52 em authenticator e Directory e compilar ambos os players. Se só alterar
atividade de sala/diagnóstico sem wire, protocolo51 continua compatível, mas builds
podem exigir identidade de commit igual. Não reabrir testes já aprovados sem mudança.


28/10 — primeira execução deste lote:505/510 (Logs/PredictionBattlePolish52Attempt.*).
As cinco falhas: teste de shuffle anterior usava SceneManager.CreateScene fora de
Play Mode (corrigido para EditorSceneManager.NewScene Additive); quatro cenários
COM habilitavam DestructiblePass num ator inativo. Ability agora conserva a flag
sem iniciar coroutine inativa, inicia/reaplica em OnEnable e restaura colisões/
encerra refresh em OnDisable. Sem suprimir erros. Nova repetição inclui regressão
real do presenter de montaria durante Riding e guarda contra herdar progresso de
um lançamento novo não previsto. C#0/0 e Python44/44 já passaram antes desta última
correção; execução final e publicação ainda pendentes.


28/10 — lote corrigido e validado:511/511 Unity (OnlineRoom419 + COM92), C#0 erros/
0 avisos, Python44/44 e UTF8/BOM/diff aprovados. Reports finais:
Logs/PredictionBattlePolish52Verified.xml/.log. A segunda tentativa509/511 está
preservada em Logs/PredictionBattlePolish52SecondAttempt.*. Shuffle agora testa a
inicialização da permutação diretamente, sem criar/salvar cenas. Regressão de
montaria simula o recebimento da SyncList do servidor (não escrita pelo cliente).
Runner restaurado somente nos caminhos C# próprios antes da publicação.
Nenhuma cena/prefab foi editada. Sem validação manual entre dois peers nem nova
medição de atraso neste lote. A12 e os18 IDs parciais continuam em andamento.
Próximo: publicar este lote52, validar carga/indicador/C/visibilidade entre peers,
continuar preview imediato de A12 com elegibilidade/cooldown/tick aprovados.


Publicação acionada: commit4822c318e, protocolo52. Actions40801957603 confirmado
in_progress: https://github.com/MauricioSuporte/Super-Bomberman-6-Unity/actions/runs/40801957603
Ainda não confirmar deploy/updater até completed/success. Código congelado e
workspace limpo após envio. Ao retomar, consultar esse run antes de usar o runner.


## Continuação A12 — prévia de carga53 (em validação)

Actions40801957603 do commit4822c318e confirmado completed/success antes de usar
novamente o runner. Cliente/servidor/updater52 publicados. Próximo lote53 adiciona
somente apresentação imediata de carga/mira do owner, sem prever spawn/disparo.
O servidor envia até5 destinos validados (3..7tiles), limites/intervalo nativos e
prontidão de cooldown. Owner usa hardware A local, mantém alvo em coordenada mundial
e apaga indicador ao soltar mesmo com snapshot antigo. Uma atualização longa não
compra vários passos de carga; pausa congela o relógio. Amostra>250ms, cart em
animação ou distância>0.125 da posição aprovada cancelam a prévia. O motor/replay
não executa carga. A versão53 é necessária pelo novo conteúdo de RevengeCartState.

A12 continua parcial: recoil/muzzle antecipados, correlação de release tick e
validação manual/métricas ainda pendentes. Checklist15 implementados/18 parciais.
Suíte Unity completa em execução no runner; não iniciar CI nem copiar fontes nele
até terminar. Registrar resultado de PredictionCharge53 e restaurar só os caminhos
próprios antes de publicar. Python44/44 já passaram. Sem cenas/prefabs alterados.


A12/53 validado:512/512 Unity (OnlineRoom420+COM92), C#0/0, Python44/44,
UTF8/BOM/diff aprovados. Reports em Logs/PredictionCharge53.xml/.log. Runner
restaurado só nos caminhos próprios. Prévia de carga/mira pronta para homologação;
não há prova de sensação/latência renderizada nem recoil/muzzle previsto. Próximo:
publicar53 e conferir indicador durante carga, pausa, movimento e soltura com dois
peers; depois release tick/recoil/muzzle. Checklist permanece15/18 parciais.


## A12/54 — release tick, recoil e muzzle (em desenvolvimento)

Pedido: concluir próximas pendências de A12. Wire54 acrescenta tick/hora do último
lançamento e parâmetros aprovados de recoil/muzzle. O servidor carimba o tick
consumido do cart. Owner prevê só apresentação ao soltar A após carga válida e um
frame held enviado; ack sem lançamento correspondente cancela. Timeout500ms.
Direção/offset do tiro são congelados no lançamento, distintos da mira atual.
Prévia usa curva senoidal unscaled nativa; som somente confirmado. Muzzle é um
SpriteRenderer usando os frames End do prefab de explosão, sem scripts/colisores.
Pausa mantém atualização unscaled desse efeito; release não cria bomba/dano.
Ainda NÃO validado/publicado54. Pipeline53 b614ef48e/40803559380 ainda em execução;
não alterar runner até terminar. C# inicial passou; após ajuste de offset houve
2 erros de conversão ambígua Vector2/3, corrigidos com cast explícito. Recompilar.
Próximo: validar C#, testes Unity, revisão de confirmação/rejeição/cleanup e
publicar só após aprovado. A12 permanece parcial, manual entre peers pendente.


A12/54 lote validado:522/522 Unity (OnlineRoom430+COM92), C#0 erros/0 avisos,
Python44/44, UTF8/BOM/diff aprovados. Reports Logs/PredictionRevengeLaunch54.*.
Inclui testes de uint wrap/ack, curva de recoil e muzzle realmente renderer-only
com frames carregados por Resources (Player.prefab não referencia a explosão).
Protocolo53/Actions40803559380 confirmado completed/success antes dos testes.
Runner restaurado só nos caminhos próprios; nenhuma cena/prefab alterada.

Implementados: release tick no servidor, preview imediato de recoil/muzzle do
owner, cancelamento por ack sem lançamento/timeout, confirmação sem reiniciar
prévia, muzzle confirmado para observadores com relógio do servidor. Som permanece
confirmado. Sem spawn/dano/eligibilidade especulativos. Recoil/muzzle unscaled
podem terminar durante pausa como nativo. Direção/offset congelados por tiro.
A12 permanece parcial: validar manualmente2peers (owner+observador), rejeição,
pausa, canto/cooldown, retorno/remoção do cart e efeito em latência alta; obter
capturas com métricas de input/render e verificar handoff/entrada/saída visual.
Não há medição de zero lag nem simulação/rollback completo da partida.
Próximo: confirmar publicação54; depois capturas/manuais de A12 e demais IDs.
Checklist15 implementados/18 parciais. Código congelado para publicação.


## P0/A12 — métricas de resposta do lançamento (em validação)

Protocolo54 d89ea1e94 / Actions40805386795 confirmado completed/success. Lote
atual não muda wire nem protocolo. Novos eventos revenge_launch_input,
first_visual, confirm, reject e timeout carregam cart/geração/tick e duração desde
A release amostrado. First_visual só é registrado depois de aplicar recoil não
nulo a sprites visíveis ou muzzle com sprite/branch ativo; não representa GPU/
monitor nem hardware bruto. Confirmação e apresentação são métricas diferentes.
Analisador mantém contexto revenge_cart separado, exige input correspondente,
deduplica e mantém captura/build/role/player separados. p95 exige20 amostras.
Sem novos logs reais capturados; não inferir números de latência deste trabalho.
Unity PredictionRevengeMetrics54 em execução. Registrar resultado antes de
publicar/restaurar runner. A12 continua parcial e checklist15/18 parciais.


P0/A12 métricas54 validadas:522/522 Unity, C#0/0, Python46/46 (CI38+Directory8),
UTF8/BOM/diff aprovados. Reports Logs/PredictionRevengeMetrics54.xml/.log.
Nenhum valor de latência de partida foi coletado. Primeira apresentação, confirmação,
rejeição e timeout possuem estatísticas separadas; input ausente/duplicado/outra
geração/captura não vira amostra. p95 somente20+. Checklist15/18 parciais mantido.
Runner restaurado somente nos C# próprios. Sem cenas/prefabs alterados.
Próximo: confirmar publicação deste lote54, coletar logs2peers e continuar auditoria
R07 de apresentação independente de owner/transferência, seguida de demais IDs.


## Execução contínua solicitada — R07/55 e auditoria de famílias

O usuário pediu implementar o plano completo sem encerrar a cada lote. Não publicar
cada alteração pequena nem dar manual como concluído sem prova. Protocolo54 de
métricas / Actions40808356205: job completed (conferir conclusion no fechamento).
R07/55 implementado: ItemPickup recebe segmentos/poses revisionados pelo netId,
independentes de owner, guarda client nas entradas de expulsão/bounce/push,
paradas e continuidades em ordem sem frame extra, wrap como corte, pausa e estado
para observador tardio. Cliente não escolhe terreno/landing nem altera collider.
Removido feedback6/7 que executava expulsão nativa client-side. Mundo/montarias:
pose e root no mesmo histórico32, corte4units/250ms, pose congelada durante pausa,
buffer reutilizado e limpeza no stop. Snapshot temporal altera wire: protocolo55.
Validação R07: focados6/7 inicialmente (fixture SyncList sem inicialização de NI),
fixado fixture; suíte completa530/530 passou incluindo recuperação tardia. Reports
Logs/PredictionItem55Focused.* e PredictionItem55Full.*; C#0/0 no último build.
Sem validação manual entre peers. R07 continua parcial enquanto auditoria/transfer/
ownerless e capturas não terminarem. Não concluir manual de nenhuma família.

Em paralelo de leitura foi feita auditoria de overrides nativos de inimigos,
Bosses e Bombers: guards inseridos em258 entry points antes de Start/Update/Fixed,
contato/morte/Init/Expire etc. Guardar base não protege código após base retorna.
Essas alterações amplas ainda NÃO têm validação Unity; manter separadas do lote
R07 e executar regressão parametrizada antes de commit/publicação. JellyFishShot
não agenda Expire no client; Speeder não sorteia preferLeftTurn em Awake client.
Próximo: regressões parametrizadas, suíte completa e seguir demais IDs. O runner
está livre após a suíte530, mas fontes R07 nele ainda diferem de HEAD.


Auditoria de famílias55:857/857 Unity aprovados em
Logs/PredictionNativeAudit55.xml/.log (326 entry points parametrizados +531
regressões existentes). Não houve skips. Guarda externa adicionada a258 métodos
nativos, mais ScheduleAttack/ScheduleAbility para não sortear IA em OnDisable;
JellyFishShot não agenda expiração no client e Speeder não sorteia em Awake.
Perfil de corte/pausa se aplica a todos EnemyMovementController, não somente
Frog/Hop/SnowMan/Korobokkuru. pauseWithGameplay em CoopSpriteFrame congela
apresentação de projéteis/visuais físicos e animações que respeitam pausa; blackout
unscaled continua fora. Cache de classificação evita consulta RB a cada frame.
Wire55 já inclui esse campo. Cliente continua sem executar IA/colisão/ataques.
UTF8/BOM passou para os C# deste lote. Sem medição de render/curvas de todas as
famílias em partida real; estes testes não concluem manual R03/R05/R06/R08.

A11/R07 adicional em desenvolvimento após a suíte857: adiar remoção visual de
montaria enquanto o rider vivo ainda apresenta arco de Riding. Não adiar estado
real nem morte/troca para outra montaria. Cinco testes puros novos preparados;
repetir suíte com essa mudança antes de commit/publicação. NPS/método de teste
adicional ainda não fazem parte do lote validado857. Nenhum push55 realizado.


## Retomada29/10 — trabalho contínuo, sem publicação55 ainda

Commits locais:000410840 (R07 itens/montaria) e80afbd484 (auditoria nativa).
Origin/publicado continua0b2f10852/protocolo54; não confundir commit local com deploy.
Lote seguinte em workspace: transporte WorldBombMotion pelo manager para motion
sem endpoint do ator, cache256 por netId/revisão/scene, exclusão do owner do kick
remoto (mantém Target/predição), roteamento a FX válido já spawnado no client e
retry limitado para ordem spawn/evento. worldMotion não concede habilidade nem
herda input lead/histórico de outra ação. Cleanup e recuperação de late observer
não dependem de manter o jogador original. Também há guard de entrada/saída visual
de Revenge antes do motor e defer de remoção de montaria durante Riding vivo.
Validação:15/15 focados e872/872 suíte completa. Reports
Logs/PredictionWorldBomb55Focused.* e PredictionWorldBomb55Full.*. Esses resultados
correspondem a antes das adições de métricas/harness abaixo.

R02/P0 em validação: OnlinePresentationMetrics agrega frames amostrados/alterados,
bytes serializados dos frames e CPU do bloco de captura por família, somente com
diagnóstico ligado. Não inclui envelopes, definições de assets, multiplicação por
peers, transporte nem overhead do próprio diagnóstico. Analyzer separa o contexto,
valida números e agrega famílias; PythonCI39/39 passou. Não são medições de uma
partida nova. Additive JSON de log, sem novo wire além do protocolo55 já local.

Próximo fluxo já preparado mas NÃO executado: BuildPerformancePlayer no runner,
3-process smoke com2clientes Mirror reais e flag --prediction-items, além de
--mount-input para Mole/Tank. LocalNetworkInputSmoke observa item em fase de
movimento aprovado. Fixture server cria Skull/LineBomb e expulsa via gameplay
nativo. Nomes exclusivos passados só por flag Development/Editor, sem PlayerPrefs;
release continua usando nome salvo. Smoke usa CREATE_NO_WINDOW e só encerra seus
próprios processos. Headless NÃO comprova aspecto gráfico/sensação nem latência.
Nova suíte final deve incluir diagnóstico de família e helper de nome (874 casos
esperados). Depois compilar Development e executar smoke antes de fechar este lote.
Não iniciar CI com runner em uso. Há fontes não commitadas e arquivos novos nele.


## Retomada29/10 — falha de encerramento reproduzida e correção em validação

Suíte consolidada antes do smoke:874/874 passou, C#0 avisos/0 erros e Python47/47.
Development player compilou (PredictionDevelopment55Build.log). O smoke real
Logs/PredictionFullClients55Run1 confirmou nos2clientes a apresentação aprovada de
Skull/LineBomb e desmontagem Mole/Tank, mas FALHOU no encerramento por NullReference
em NetworkBombMotionLifetime.Forget. O singleton destruído passa no pattern C#;
adicionado check Unity-null antes do acesso. Três avisos de publish após shutdown
(Mole/Pink/Green) corrigidos consultando NetworkServer.active também após yields.
Novo smoke agora falha explicitamente nesses avisos. Não classificar Run1 como
sucesso; tampouco comprova gráficos, sensação de input ou custo co-op.

Próximo: suíte PredictionLifecycle55 (878 casos esperados) já iniciada no runner;
se aprovada, rebuild Development e repetir smoke em pasta Run2. C# desta correção
já compilou0/0. Skill e notas atualizadas com transporte independente de ator,
continuidade de item/montaria e teardown. Nenhum push/deploy55 realizado.


Atualização da retomada: PredictionLifecycle55 final878/878 passou, sem skips;
relatórios copiados para Logs/. Build Development corrigido em andamento.
Pipeline anterior40808356205 confirmado completed/success (0b2f10852, protocolo54).
Smoke ganhou opção co-op/Stage e cópia explícita dos três motion logs para medir
R02 por família com2clientes reais. Essa modalidade ainda não foi executada.


Enquanto os smokes55 corrigidos rodam: auditoria R07 identificou diferença de
sorting dos itens em roll/push. Client agora captura/eleva/restaura a mesma ordem
visual do caminho offline, preservando flip e ordens já altas; restaura também
em OnDisable. Uma regressão nova preparada (879 esperados). Esta mudança AINDA
não foi sincronizada no runner nem está no binário dos smokes em andamento.
Repetir Unity/C# antes de commit/publicação; não atribuir validação gráfica ao smoke.


Smoke55 Run2 APROVADO: Logs/PredictionFullClients55Run2/result.json.2clientes
Mirror reais,210s de gameplay solicitado,2cargas de arena, roll de Skull/LineBomb
e dismount Mole/Tank confirmados;0erros e0avisos de servidor. Headless/rendered=false:
não comprova aspecto gráfico nem sensação de latência. Corrige a falha de Run1.
Co-op NormalGame Stage_1-1 com2clientes e diagnósticos está em execução em
Logs/PredictionCoopClients55Run1; não iniciado outro Unity enquanto usa o binário.

Revisão adicional R07: caches de revisão de item e world bomb agora identificam a
conexão real, removem peers desconectados e não excluem um novo cliente por herdar
connectionId de um owner anterior. Duas regressões com envio Mirror e reconexão
preparadas. Junto ao sorting, são881 casos esperados na próxima suíte; mudanças
posteriores ao build dos smokes, ainda exigem sincronização/Unity antes de publicar.


Co-op55 APROVADO: Logs/PredictionCoopClients55Run1/result.json.2clientes reais,
NormalGame/Stage_1-1,90s,0erros/avisos. Logs dos3processos copiados e analisados em
analysis.json.82reports válidos de EnemyMovementController:23025 amostras,
22305 frames alterados,2788125bytes de payload de frame e121,3574ms de captura
agregada (~5,27us/amostra). Inclui aquecimento, não tem baseline offline/anterior,
exclui envelopes/definições/fanout/transporte/diagnóstico/GPU. Uma família/cena só;
não concluir custo de todos inimigos/bosses nem melhoria comparativa.
Build stamp do Development ainda0b2f10852 do runner, protocolo55 com fontes locais
modificadas: não identifica deploy. binary-provenance.json registra SHA256 da DLL
de ambos os smokes antes de outro build; script agora registra hashes no início.

Suíte PredictionRecovery55 iniciada após os smokes com sorting/reconexão (881
esperados). C#0/0 passou. Aguardar relatório, corrigir falhas se houver e salvar
lote somente após a validação. Nenhum processo de smoke continua rodando.


## Lote55 validado para salvar e publicar

PredictionRecovery55:881/881 Unity,0falhas,0skips (29/10 08:38:39–08:39:00Z).
Inclui sorting/restauração do item e recuperação de peers reconectados com o mesmo
connectionId, além das regressões de teardown. C# final0/0; Python47/47; py_compile
smoke passou. Sem mudanças de cenas/prefabs. Reports copiados para Logs/.
Os smokes Battle Run2 e Co-op Run1 usam o Development anterior ao sorting/reconnect,
registrado por hash da DLL; os últimos3casos cobrem essas alterações na suíte881.
Não inferir aspecto gráfico/fluidez/manual de logs headless. Checklist15/18 mantido.
Runner liberado após a suíte; restaurar somente fontes próprias antes de Actions.
Próximo: commit55 e envio conjunto client/server, acompanhar build/deploy, então
retomar C02/C03 e outros IDs parciais. Publicação ainda não confirmada neste ponto.


##55 enviado / continuação A14–R07

Commit11c1bdf55 enviado junto com000410840 e80afbd484. Actions40843901315 iniciado,
preflight aprovado, builds em execução; não confirmar deploy antes do resultado.
Runner pertence ao CI; não copiar fontes nem iniciar outro Unity nele agora.

Próximo defeito identificado no review: LastKickForTick também elegia motion.worldMotion
como último chute adjacente do owner quando o manager roteava a apresentação pelo
seu FX. Isso poderia trocar a referência usada pelo replay por um chute de COM/outro
ator. Correção local exclui somente motion compartilhado dessa referência, mantendo
IsKickMovingForTick para a consulta de obstáculo móvel. Duas regressões novas
preparadas (owner/observer, stop, comando próprio preservado); ainda NÃO validadas
Unity, NÃO commitadas, NÃO no CI11c1bdf55. Aguardar runner, executar focados e compilar.


Publicação55 CONFIRMADA: Actions40843901315 completed/success em todos os passos,
commit11c1bdf55, builds Linux/Windows, deploy e updater. Não inclui a nova correção
local LastKickForTick. Após liberar o runner, fontes desse ajuste sincronizadas;
focados4casos (owner/observer e arco/stop) em execução em PredictionSharedKick55.
Se aprovados, script já encadeia BuildPerformancePlayer para validar fases com
chefes em dois clientes reais. Nenhum processo do usuário encerrado/cena editada.


Expansão preparada da integração após os focados: Stage_1-7 contém ClownMask;
Stage_2-7 contém SunMask; Stage_3-6 contém16prefabs de inimigos (inclui Heli,
MechaBombSB3, BirdRobotRed, Penguin não está nessa cena). Prefabs conferidos por
GUID do m_SourcePrefab, não por suposição de nomes. Rodar smokes sequenciais para
não exceder memória, usando2clientes reais e --diagnostics; não concluir curvas,
input ou renderização somente por smoke. Fases ainda não executadas nesta etapa.


Fix LastKickForTick VALIDADO: Logs/PredictionSharedKick55.xml/.log,4/4focados
passaram (2novos +2regressões existentes). C# sem erros/avisos novos;6avisos já
existentes no Mirror após rebuild de dependências. A suíte completa881 precede
esse fix; não afirmar883/883. Build Development seguinte em andamento. Ainda não
publicado. Não copiar/restaurar runner até terminar build e smokes próprios.


## Continuação R07 — pausa dos ovos, após commit local26fdda131

Development do fix de referência compilou com resultado Succeeded/errors0. Houve
uma mensagem interna do allocator Unity durante build (Failed to free block),
sem falha no relatório/exit; conservar log PredictionSharedKick55DevelopmentBuild
para verificar recorrência. Não é aviso C# e não foi suprimido. Smokes sequenciais
ClownMask/NormalGame Stage_1-7, SunMask/BossRush Stage_2-7 e World3/NormalGame
Stage_3-6 iniciados em Logs/Prediction{Clown,Sun,World3}Clients55Run1.

Nova lacuna encontrada em NetworkEggFollower: enquanto pausado, NT e histórico
visual ainda podiam avançar. Client agora retém root/rotação e fase já apresentados,
sem drenar a fila até retomar; limpa estado no OnStopClient. Host/offline continuam
nativos, destruição aprovada mantém posse dos renderers. Um teste novo preparado;
ainda NÃO está no Development dos smokes, NÃO validado Unity e NÃO commitado.
Após smokes, sincronizar EggFollower/OnlineRoomItemMotionTests e executar suíte
completa (884 esperados). LastKickForTick já foi validado em4focados e salvo local.
Não publicar enquanto runner estiver em uso.


## Integração ampliada55 revelou falhas sem Exception

Smokes Clown/Sun/World3 Run1 completaram conexão/cena/encerramento. Sun0avisos;
World3 teve3avisos de catálogo de tiles Runtime. NÃO classificar Clown como
combate aprovado: análise das capturas mostrou players input_locked até o fim,
7930 amostras de visual e só5frames alterados. IntroSequence aguarda playersReady,
mas depois ResetBossState parava introRoutine já atribuída pelo primeiro yield:
a própria coroutine era cancelada. Corrigido com cancelIntro:false só dentro da
intro, preservando cancelamento no Awake/OnDisable e limpando BossIntroRunning.
UnityTest verifica que a coroutine segue viva e que OnDisable ainda a cancela.

R02 classificação também escondia ClownMaskMovement sob CharacterHealth; o wrapper
de saúde agora é ignorado ao encontrar família de gameplay. Teste ampliado.
C01/R02: tiles AnimatedTile de esteira gerados em runtime agora resolvem para seu
original no catálogo via controller/mapa de origem; índice cacheado no catálogo
existente. Client reaplica apenas a fase já aprovada após snapshot geral do mundo,
com cor/matriz autoritativas restauradas depois da troca visual. Sem novo wire55,
sem novo gerenciador, Startup/força/dano continuam nas regras já existentes.
Duas regressões de Tilemap verificam identidade/colisão, fase e preservação offline.

Suíte PredictionCoopCorrections55:887/887 passou,0falhas/0skips. Inclui pausa de ovos,
fix de intro Clown e wrapper de custo. Relatórios copiados a Logs/. Depois dessa
suíte foi reforçada a preservação de cor/matriz no helper ApplyCoopTileCells; dois
focados PredictionConveyorMetadata55 estão rodando e, se aprovarem, BuildPerformance
já encadeado. Teste novo compara cor/matriz do Tilemap além da fase. Não sobrescrever
runner enquanto esse script estiver em execução.

smoke_full_clients ganhou --require-unlocked: exige movimento livre no servidor
e no owner de cada cliente após a intro, via diagnóstico. Falta de asset de catálogo
agora também falha o smoke. Repetir Clown e World3 em Run2 com --diagnostics e
--require-unlocked após novo Development. Sun pode repetir para comparação de
classificação. Os Run1 não comprovam gameplay nem imagens. Código desse lote ainda
não commitado/publicado; origin continua11c1bdf55, fix26fdda131 está só local.


PredictionConveyorMetadata55:2/2 focados aprovados, cor e matriz comparadas no
Tilemap real após reaplicar variante. C# final0/0. Build Performance com o lote
completo em andamento (session58924), log PredictionCoopCorrections55DevelopmentBuild.
Após exit0, repetir ClownRun2/World3Run2/SunRun2 comdiagnostics+require_unlocked=True,
90s cada. Script já exige que owner de cada cliente e2players no servidor tenham
prediction_block=none depois da intro. Run1 Sun e World3 possuem esse desbloqueio;
ClownRun1 não. Não escrever sucesso de combate para ClownRun1.
Próximo lote tem887full +2focados posteriores; não anunciar889testes únicos.


## Lote co-op55 validado para publicação após Run2

ClownRun2, World3Run2 e SunRun2 aprovaram os checkpoints estritos:2clientes reais,
90s/cena,2players livres no servidor e owner livre em cada client,0erros/avisos.
Reports completos e SHA256 em cada pasta Logs/Prediction{Clown,World3,Sun}Clients55Run2.
Clown agora tem6717frames alterados em7750 amostras (antes5/7930 com intro presa).
Classificação correta ClownMaskMovement, não CharacterHealth. World3 teve11famílias
observadas (6de inimigos +props), não afirmar que todos16prefabs inventariados
simularam. Sun/World3 analysis.json criados; dados não são comparação de performance.
Último Development não repetiu a mensagem interna de allocator do build anterior.

887full +2focados de metadados, C#0/0, Python47/47, smokes estritos aprovados.
Sem novo wire: protocolo55 mantido. Código de Clown/esteira/ovos/custo ainda precisa
commit e envio junto com26fdda131 local. Restaurar só fontes próprias no runner
antes de push. Próximo após publicar: R07 transferência de apresentação já em curso
com destruição do FX (teste atual cobre novo evento após remoção, não esse intervalo),
C02/C03 curvas/handoff e demais IDs parciais. Checklist15/18 continua honesto;
smoke headless não encerra validação visual/manual de nenhum ID.


## Lote co-op enviado / próxima prova R07

Commits26fdda131 +a1e0e9e48 enviados, protocolo55 mantido; CI da nova publicação em
execução. Não confundir envio com deploy. Runner pertence ao CI até terminar.

R07 continuação:5testes novos preparados em OnlineRoomWorldBombMotionTests.
3exigem que arco e stop já recebidos continuem sem NOVO evento de rede quando o
FX original passa por OnStopClient/OnDisable/OnDestroy;2garantem que comando ainda
não confirmado e Carry dependente de ator não migrem. Ainda NÃO rodaram; runtime
continua sem migração explícita de KickPlayback entre FX. Primeiro reproduzir
comportamento atual quando runner liberar, depois corrigir mantendo curva/progresso,
fila aprovada e collider/sorting; não herdar input/pose/lead do outro ator.
OnDisable perde o estado antes do OnDestroy quando CanRetain falha: tratar também
essa entrada, além do stop client. Evitar migrar durante disconnect/scene loading.
Nenhum código de produção novo desse próximo passo foi escrito ou publicado ainda.


## R07: baseline reproduzido / migração em validação

Publicaçãoa1e0e9e48 confirmada: Actions40850586608 completed/success, todos os passos.
Novo lote local tem6testes. Baseline inicial deixou o manager escolher o presenter
por ordem de Dictionary e2casos não testavam a origem desejada; preservado relatório
PredictionPresenterTransfer55Baseline. Fixture corrigida para entregar RPC à origem
e afirmar seu ownership antes da remoção. BaselineFixedFixture:6casos,2passaram,
4falharam como esperado (stop/disable/destroy +kick confirmado). Cancelamento de
comando não confirmado e Carry já funcionava. Reports em Logs/PredictionPresenterTransfer55BaselineFixedFixture.*.

Runtime local agora migra só trajetórias confirmadas em andamento para FX realmente
client/spawnado da mesma cena, preferindo o local. Preserva elapsed/posição/fila e
captura de collider/sorting; remove histórico/tick/pose do ator anterior, marca
worldMotion e approvedOnly para não inventar próximo segmento depois da perda da
origem. Queues já aprovadas seguem essa restrição. Disable sem retain, stop client
e destroy tratados; disconnect/scene loading, Carry/Lift e comandos pendentes não
migram. Sem candidato, segue cleanup/fallback existente; não criar ator fictício.
Mudanças ainda NÃO validadas Unity ou publicadas.

Dev harness ganhou -load-test-presenter-removal, só Development/local server.
Na Stage_1-1 lança Punch nativo de1,2s e remove o jogador que o apresentava após
250ms, com os2clientes conectados. Smoke flag --presenter-removal exige evento de
transferência e samples movendo antes do próximo apply no MESMO relógio de cada
client. Sem teclado sintético nem mensagem remota habilitando fixture; actors são
duráveis de teste. Novo harness ainda NÃO foi executado.

Suíte PredictionPresenterTransfer55Full em execução,893esperados, session60572
(identificador efêmero do tool). Runner está com
fontes desse lote. Após aprovação: compilar Development e rodar smoke NormalGame,
Stage_1-1,diagnostics+require_unlocked+presenter_removal,90s em pasta nova. C# e
py_compile já executados; conferir conclusão do C# na ferramenta. Não publicar antes
pelos resultados. Nenhum wire mudou; protocolo55 permanece.


C# do lote R07 compilou0erros/0avisos novos;6avisos preexistentes do Mirror no rebuild
de dependências. BaselineFixedFixture confirmou4falhas/2passes em6casos antes da
correção. Nova migração usa approvedOnly nos estados transferidos, sem extrapolar
novo kick/bounce além dos segmentos já aprovados; novo evento direto pode renovar
o caminho normal. Callback da fila conserva essa restrição. State preserva sample
cadence, progresso e metadados, sem transferir inputTick/history/gesto de player.
Novo probe usa Punch NATIVO, direção para dentro do mapa, fuse10s e um único arco
longo; exige arc ainda ativo antes de remover a origem, aguarda pouso e limpa a
bomba. A flag só fica no arquivo Development já protegido por UNITY_EDITOR||DEBUG.
Suíte893 ainda deve ser conferida; não considerar o código aprovado antes do XML.


## R07 transferência:893 passaram / primeiro probe e reforço894

PredictionPresenterTransfer55Full:893/893 aprovados,0skips; build Development passou.
PredictionPresenterClients55Run1 confirmou9samples com deslocamento entre transferência
e próximo apply em CADA client, sem erros. Teve1aviso Mirror de comando atrasado
para o ator deliberadamente removido. O campo seconds do resultado ficou INVÁLIDO
porque o parser reutilizou started para o relógio do evento; não foi teste de9h.
Corrigido para transfer_started; não usar seconds de Run1 como métrica. Raw preservado.

Para eliminar o aviso causado pela fixture, o probe agora suspende apenas o sender
local nos clients de teste, envia PresenterSmokeReady e o servidor aguarda ambos
mais1s de drenagem antes de remover o ator. Mensagem/hook só existem sob
UNITY_EDITOR||DEBUG e flag local explícita. CI proíbe PresenterSmokeReady na DLL
release junto dos harnesses já excluídos. runRealInput agora exige sua própriaflag,
para um probe de observação não disparar Select por possuir Mole/Tank.

Revisão adicional: ForgetClientBomb de estado worldMotion não pode cancelar gesto
local de outro Punch. Guard adicionado e teste cria duas bombas/gesto aprovado
real via ReceiveKickMotion. Suíte PredictionPresenterPose55Full:894/894 passou,
0falhas/0skips. Relatórios copiados para Logs/. Novo Development +probeRun2 estão
encadeados em session92037; aguardar exit/result, não copiar fontes para runner.
C# incremental session69884 deve ser conferido. Não afirmar que o Run2 passou antes
de existir report. Código R07 continua local sem commit/publicação.


## R07 concluído em implementação/testes — publicação pendente

PredictionPresenterPose55Full:894/894,0falhas/0skips; C#0erros/0avisos. Python47/47.
PredictionPresenterClients55Run2 aprovado:2clients Mirror reais,8samples móveis em
cada um depois da transferência e antes do próximo apply,0erros/0avisos. Input da
fixture suspenso/confirmado antes da remoção e período de drenagem eliminaram o
comando tardio observado emRun1, sem suprimir avisos. seconds agora96,656 (contador
corrigido);90é orçamento solicitado do servidor, inclui lobby/cena, não90s exatos
de gameplay. Build Development aprovado; não comprova pixels/áudio/latência humana.
SHA256 e reports em Logs/PredictionPresenterClients55Run2; análise agregada separada.

R07 marcado[x] só em implementação/testes, levando o checklist a16/33 completos e
17parciais. Itens/ovos/montarias são autoritativos; FX migrado não recebe habilidade,
pose ou histórico do ator anterior. Sem presenter válido, cleanup/NT existentes;
sem inventar endpoint/continuar Carry desconhecido. Validação manual permanece.
Próximo imediato: salvar/push do lote validado (ainda local), liberar runner paraCI,
acompanhar publicação; continuar A12 (prova de input/carga/boost em2clients) e
C02/C03. Não repetir a suíte894 sem nova alteração ou falha que justifique.


## Retomada A12 em29/10 — integração de input do Revenge Kart (em desenvolvimento)

R07 salvo/enviado em76f2a948c; Actions40868952214 em compilação, sem confirmação
de deploy nesta consulta.894/894 Unity e smoke real Run2 preservados acima.

Nova fixture somente Development em LocalServerLoadTest: habilita a regra Revenge,
executa morte nativa dos2owners, envia direção/C e3cargas/releases A pelo hardware
sintético local1 e transporte normal. Exige indicador de carga, avanço do cart comC
no servidor e3disparos nativos porowner; não chama RPC de disparo para simular input.
Parser exige correlação cart/generation/tick nos3inputs/first_visual/confirm, sem
duplicatas/reject/timeout e presença dos2carts em cada cliente.6regressões do parser;
Python53/53 passou (45CI+8Directory). C# passou:0erros,6avisos preexistentes de Mirror.
Ainda NÃO executado no Unity/binário: runner reservado ao pipeline76f2. Não marcar
A12 concluído nem inferir renderização/latência física; checklist permanece16/17.

A12: pipeline76f2 concluído/success; runner liberado. Suíte completa
PredictionRevengeInputs55Full em execução, seguida do Development build e smoke
BattleMode_15/90s com2clientes reais. Não há resultado desse teste ainda.


## A12 — integração aprovada em29/10, protocolo55

Lote runtime publicado continua76f2a948c/Actions40868952214 completed/success.
Este lote adiciona somente fixture DEBUG/Editor, parser, testes e documentação;
nenhuma alteração de wire, cena/prefab ou regra de produção. Ainda sem envio.

-894/894 Unity,0falhas/0skips, reports PredictionRevengeInputs55Full. Posteriormente
  o encerramento da fixture DEBUG foi corrigido e compilado/testado no binário Run2.
- C# final0erros/0avisos; Python53/53 (45CI+8Directory), incluindo6casos de validação
  de correlação/duplicação/ausência de observador/rejeição/NaN/boost de dois owners.
- Development Run2 build aprovado; Logs/PredictionRevengeInputs55Run2DevelopmentBuild.log.
- Logs/PredictionRevengeInputs55Run2/result.json,config.json,analysis.json e3capturas:
  servidor+2clientes Mirror reais, BattleMode_15,90s solicitados/113,219s totais
  incluindo startup/shutdown; atores duráveis, headless, inputs sintéticos local1.
  Os2owners morreram nativamente, entraram no cart, tiveram C aceito com avanço,
  carregaram A até7tiles e3disparos confirmados cada. Ambos apresentaram os2carts.
  Zero erros/avisos, reject/timeout/duplicatas. SHA256 Assembly-CSharp.dll:
  7947243e092a1ff5c937b091d67ab3dc292ed57ea964d9a7b4aa2452cb7ca37e.
- Primeira resposta visual CPU: medianas0,4232ms/P1 e0,5697ms/P2 após o release
  amostrado; confirmação132,8546ms/P1 e132,8911ms/P2. São3amostras porowner,
  insuficientes para p95; não medem GPU, hardware físico nem input-to-photon.
- Run1 preservado: checkpoints de gameplay passaram, mas a cópia da captura ainda
  aberta cortou uma linha ao final. Não é uma execução aprovada. Nova flag local
  -load-test-client encerra Development após disconnect e Application.quitting
  fecha o StreamWriter; Python aguarda os processos antes de copiar os logs.

A12 marcado concluído em implementação/testes, como os demais IDs[x]; comparação
manual/offline, cantos/pausa em rede adversa e troca/retorno visual continuam na
matriz manual e não foram executados neste smoke. Checklist17concluídos/16parciais.
Próximo: C02/C03 curvas e devolução ao motor. Runner deve ser restaurado somente
no tracked próprio LocalServerLoadTest.cs antes de eventual push; sem processo
próprio ativo após o Run2. Não publicar debug harness como conteúdo do release.


## Continuação C02 — lifetime do canhão (em validação, sem commit)

A12 salvo no commit LOCAL c0cf7232e, ainda não enviado; runtime publicado76f2a948c.
Checklist17concluídos/16parciais. Não presumir deploy de alterações locais.

CannonLauncher tem cleanup explícito no OnDisable de seu IEnumerator rastreado,
finally da preparação e do voo e IDisposable para o voo aninhado. Preserva os
estados anteriores de input/collider/BombController/proteção e não restaura flags
sobre morte/end-stage. A scope de autoridade cobre agora FireRoutine inteira e
termina depois da restauração de controle; antes terminava dentro do arco com
input ainda bloqueado. Curva/duração/distância/colisões continuam nativas.

6casos novos em OnlineRoomLauncherLifetimeTests.cs/.meta. Baseline inicial falhou
na fixture (faltava BoxCollider antes do RequireComponent abstrato). BaselineFixedFixture:
4casos,1pass/3fail; warmup e collider não restaurados. O caso de GameObject disable
em EditMode foi posteriormente convertido para UnityTest em PlayMode porque
MonoBehaviour sem ExecuteInEditMode não recebe OnDisable nesse ambiente.
Focused com fix:5/6pass, esse único caso ainda com fixture EditMode incorreta.
Novo PredictionCannonLifetime55Lifecycle em execução no runner, incluindo disable
nativo em preparação/voo. Depois: corrigir falhas, suíte completa900esperados,
C#final/UTF8/diff, docs/skill, restaurar tracked próprios no runner antes de push.

Runner nesta etapa contém fontes próprias CannonLauncher.cs, LocalServerLoadTest.cs
(c0cf7232e) e teste/metas novos. Nenhuma mudança do usuário removida. Não copiar fontes
nem iniciar outra instância Unity enquanto a execução atual estiver ativa.
Mola SpringLauncher e corda também precisam auditoria de cancelamento; ainda SEM
alterações nesses arquivos. C02/C03 permanecem parciais; não foram enviadas curvas.


C02/canhão validado: PredictionCannonLifetime55Lifecycle6/6 passou, incluindo
UnityTest em PlayMode com desativação real durante preparação e voo. Posterior
review preservou a ordem nativa: cancelar dash antes de capturar InputLocked.
Regressão específica GreenLouie acrescentada. Suíte FINAL901/901,0falhas/0skips,
Logs/PredictionCannonLifetime55Full.*; C#final0erros/0avisos. UTF8/BOM/diff OK.
Nenhuma cena/prefab alterada. Native offline e scope servidor cobertos nos testes;
comparação gráfica offline/2peers para o canhão ainda não executada. C02 parcial;
não é a implementação final das curvas. Pronto para commit/envio com c0cf7232e.
Runner restaurar somente CannonLauncher.cs e LocalServerLoadTest.cs antes de push.
