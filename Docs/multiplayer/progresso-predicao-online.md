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

- Branch `v0.5.0`, checkout original; preservar fontes e TitleScreen do usuario.
- R02 geometria e A05 publicados em d25c34734 /30/ CI40281389234 sucesso.
- A07 publicado em cb971ee11 /31/ CI40283518599 sucesso (server e updater).
- R02: Unity suite completa 169/169; depois Coop 9/9 com protecao final para
  Sprite de colisao ausente. Relatorios Logs/PredictionCoopTests e
  Logs/PredictionCoopGeometryFinal (.xml/.log). Python 40/40. C# zero warnings/erros.
- A07 salvo/enviado em `cb971ee11`, protocolo31, CI40283518599 sucesso.
  Unity completo171/171, C#0warnings/erros e Python40 passaram nesse lote.
  Relatorios principais Logs/PredictionMoleTests.xml/.log.
- A14 parcial em32 validado: Tank eligibility separada de cooldown com projecao
  pelo timestamp da autoridade. Suite Unity172/172, Python40 e C# zero warnings/
  erros passaram. Relatorios Logs/PredictionTankCooldownTests.xml/.log.
  Pronto para salvar/publicar; nao marcar A14 completo (faltam outras regressoes).
- Runner livre; fontes32 testadas e tracked restaurados antes da CI.
- Nenhuma validacao manual com dois peers nem novas capturas comparaveis feitas.
  Nao declarar zero latencia nem o plano inteiro concluido.
- Proximo: confirmar CI31, testar/salvar Tank32 e seguir A09/A10/A11/A12
  e C/R restantes pela matriz. Nao publicar32 antes da validacao Unity.

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
- [ ] A09: LineBomb/Purple cast e confirmação parcial.
- [ ] A10: ControlBomb feedback.
- [ ] A11: montar/desmontar, posse e fila de ovos.
- [ ] A12: Revenge input e apresentação.
- [x] A13: velocidade/BombPass/DestructiblePass confirmados no replay; manual pendente.
- [ ] A14: regressões dos caminhos existentes.
- [x] C01: fase/curva/velocidade no tick e replay, testes; manual pendente.
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
