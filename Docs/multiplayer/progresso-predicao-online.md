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

Atualizado em 05/11/2026. O checklist distingue implementação/testes de validação
manual; o histórico preserva decisões, resultados e tentativas anteriores.

- **C08 implementado e validado:** Magnet/Search preservam os segmentos aprovados;
  curvas de chute passam pelo canto, e cancelamento descarta a rota pendente.
  Bombas autônomas não herdam extrapolação do chute local. Full2: 1028/1028,
  sem skips; 18 casos focados finais; C# 0 erros/0 avisos; Python 86/86.
  Peers3 em Stage_2-1, com dois
  clientes Mirror reais headless, confirmou os três trajetos e transferência do
  presenter, sem erros/avisos do servidor. QA visual/hardware segue separado.
  Protocolo 63 mantido; publicação concluída no Actions42110831096.
- **C05 implementado e validado:** cleanup dos portais de jogador/bomba preserva
  estados anteriores e encerra a apresentação autoritativa após liberar o controle.
  Full4: 1013/1013, sem skips; três testes focados finais e Python78/78 aprovados.
  Peers8: Battle7 e Stage3-4 com dois clientes reais renderizados, origem/destino
  aprovados e caminhada após saída nos dois papéis; zero erros/avisos do servidor.
  Corrigidos também catálogoBigWorm e ciclo de vida/transmissão do grão de areia.
  Protocolo60 mantido; lote publicado com sucesso no Actions42010967595.
- **Publicado:** protocolo 63, commit `3ca94c369`,
  [Actions42110831096](https://github.com/MauricioSuporte/Super-Bomberman-6-Unity/actions/runs/42110831096).
  Servidor Linux, cliente Windows e canal GameUpdater 0.5.0 publicados com sucesso;
  os três manifestos confirmam o mesmo commit e build limpa. Baseline anterior:
  `16f96722f`/Actions42077315561 (chat e ajustes posteriores ao C05).
- **C04/protocolo60 concluído e publicado:** cart/passageiro/poses na mesma
  trajetória aprovada; curvas, salto/wrap, portais, pausa/retomada, cancelamento
  nativo e handoff ao motor/bounce. Entrada/saída e rota permanecem autoritativas.
  Commit `6c2b92cae` publicado com sucesso para cliente e servidor.
- **Validação anterior (C04/protocolo60):** Full7 1010/1010, sem skips;
  C#0erros,6avisos legados do pacoteMirror; nenhum aviso introduzido. Python76/76.
  Development8 compilou; Peers7 com2clientes reais renderizados por110s em Battle9,
  Battle12 e Stage3-2Room1: fases/curva/salto/portal e caminhada após desembarque
  apresentados em cada cliente,0erros/avisos do servidor. ImagensURP inspecionadas.
  Full7 valida produção; depois dele só helper Development de teleporte, compilado
  e exercitado no Development8/Peers7. Aceite humano/offline segue na matriz geral.
- **Validação anterior (C03/protocolo59):** Full3 995/995, sem skips;
  C#0erros/0avisos; Peers3 com2clientes e endpoints750/20/240ms.
- **Validação anterior (visuais/protocolo58):**984/984 Unity, sem skips;
  C# 0 erros/0 avisos, Python 72/72, Peers4 renderizado aprovado com 2 clientes.
- **Validação anterior (C02/protocolo57):**971/971Unity,0skips,
  incluindo43casosLaunch; C#runtime/Editor0erros/0avisos; Python68/68(60CI+8Directory).
  Cannon2(Stage_2-3),Geyser2(Stage_3-3) eArena2(Battle4):2clientesMirror reais,
  90s porcenário, owner/observer com fase contínua sem recuo, begin/end e controle
  desbloqueado após o salto;0erros/avisosservidor. Voos a pé/montados e cancelamento
  montado emCannon/Geyser. CâmeraURP capturada e imagens inspecionadas; não medem
  latência humana nem equivalem a comparação manual completa com offline.
  A11 mantém928/928 +3remount/4Select porowner e câmeraURP; A12 mantém aceite humano.
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
- **Checklist:** 25 IDs implementados/testados e 8 parciais; todos os 33 iniciados.
  Validação visual/manual continua pendente, inclusive em IDs marcados como feitos.
- **Próximo:** continuar R01, mantendo a comparação
  perceptual/manual no roteiro geral. C06 e C07 já estão implementados/testados.
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

Resumo: **23 de33 IDs concluídos em implementação/testes e10 parciais**.
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
- [x] A11: implementado/testado56: arcos X/Y por assento/HeadOnly e origem pré-snap; contato conhecido e Select previstos, confirmação/rollback e fila visual. Unity928/928; dois peers com3Mount+4Dismount previstos/confirmados porowner, ovos intactos. Capturas URP inspecionadas; comparação perceptual humana permanece no QA global.
- [x] A12: concluído. Motor/replay, carga/mira, recoil/muzzle, release tick, retorno confirmado e handoff testados; integração55 com2clientes, C e3cargas/releases porowner,6confirmações. Usuário confirmou validação com2clientes reais e conclusão em29/10/2026.
- [x] A13: velocidade/BombPass/DestructiblePass confirmados no replay; manual pendente.
- [x] A14: concluído e aprovado pelo usuário em10/11/2026 nos cenários testados do pacote SB6-QA-A14. Protocolo68 local; coreUnity1078/1078, dois owners/observadores normal e stall, Search/Magnet e transferência validados. Tick agrupado, hold A, pausa da luva, primeiro frame e Carry→Throw corrigidos. Evidências/limitações mantidas; publicação pendente.
- [x] C01: fase/curva/velocidade no tick e replay, testes; manual pendente.
- [x] C02: protocolo57: curvas nativas compartilhadas de mola/Battle4/canhão/geyser, pausa longa/sem recuo, lift/sombras, cancelamento e handoff ao motor/bounce.971/971Unity;3cenários com2clientes reais e câmeraURP,0erros/avisos. Aceite humano de sensação/offline segue na matriz geral; publicação57 concluída com sucesso no Actions40964582861.
- [x] C03: protocolo 59: curva nativa e pixel grid compartilhados, prefixo aprovado sem extrapolar obstáculos, início/fim confiáveis, relógio e poses alinhados, pausa/timeout/cancelamento e retorno ao motor. Full3 995/995; Peers3 com 2 clientes reais renderizados apresentou endpoints de 750/20/240 ms em ambos, sem erros/avisos. Validação perceptual manual segue no QA global.
- [x] C04: protocolo60: cart/rider/poses com clock aprovado, fases/segmentos/progresso/velocidade, curvas e cortes de portal, pausa/retomada, cancelamento nativo e motor/bounce após saída. Full7 1010/1010; Peers7 com2clientes renderizados em Battle9/12 e Stage3-2Room1,0erros/avisos e caminhada real após desembarque. Comparação perceptual/offline permanece no QA global; publicação60 concluída no Actions41948608681.
- [x] C05: protocolo60: portais hidden/sink com apresentação aprovada, corte de destino e cleanup nativo de player/bomba; proteção anterior e controle preservados. Full4 1013/1013 e três testes focados finais; Peers8 Battle7/Stage3-4 com2clientes renderizados,0erros/avisos. Hall/retorno e gates de swap/blackout auditados; QA perceptual/held-bomb/saída ocupada segue na matriz geral. Publicação concluída no Actions42010967595.
- [x] C06: correnteza/plataforma39 e barco45 (água no replay, owner view/cabeça, gates, pausa/cleanup e handoff) implementados/testados; manual pendente.
- [x] C07: continuação owner46 limitada a contato/célula/fase aprovados, caminhada/replay e interrupções testados (378 combinados); manual pendente.
- [x] C08: protocolo 63: campo e alvo autoritativos; Magnet/Search e curvas de Kick em ordem, stop/cancelamento, pausa/revisões e bombas sem dono/presenter. Full2 1028/1028 + Final18 18/18; Peers3 com 2 clientes reais headless e 3 trajetos nativos, transferência sem interrupção, 0 erros/avisos do servidor. QA visual/offline permanece na matriz geral; publicado no Actions42110831096.
- [x] C09: protocolo64: caldeira com transporte WorldBombMotion e cancelamento; tiles com frames aprovados/clock headless sem RNG; prefab/Startup seguro; câmera aprovada em transições; foguete com pausa; dinamite Battle com Fx confirmados. Full2 1037/1037; Full3 1037/1038 corrigido e Final2 5/5. Cinco cenários com2clientes renderizados (Peers5/6/7), 0erros/avisos servidor e imagens inspecionadas. QA perceptual/offline segue global; publicação64 concluída no Actions42152138685.
- [x] C10: protocolo65: timeline aprovada de SuddenDeath com pausa, snapshots tardios, curva/sombra e commit explícito de terreno/colisão; cancelamento nativo e reuso offline. FallingBomb com alvo/blink aprovados; teto/powder auditados. Full1041/1041 + Final4/4; quatro cenários com2clientes renderizados e repetição finalSD,0erros/avisos servidor.104células servidor=104porcliente; QA perceptual/manual segue global. Publicação65 concluída no Actions42204673208.
- [x] R01: concluído e publicado em homologação (07/11/2026; protocolo 65 inalterado; runtime `b01817c73`, Actions 42403939455). Implementação, validação automatizada (EditMode 1046/1046; smokes com 2 clientes renderizados, 0 passos para trás reais, 0 lacunas, 0 poses duplas) e aceite manual (aprovado pelo usuário nos cenários testados) concluídos. **Pendências obrigatórias do R01:** (1) custo CPU/bytes do player remoto (baseline comparável); (2) comparação online × offline com `analyze_prediction.py` (Logs/R01-comparacao.json). **Atualização 07/11:** (2) demonstrada; (1) medida, revelou custo de diagnósticos (+56 KiB/s de alocação no servidor) e +5–6% de ciclos no observador; a correção Development-only ainda precisa de compilação/EditMode e da medição sem captura. **Atualização 07/11 (tarde):** EditMode 1046/1046; sem captura não há diferença resolvida (Δ frame ≤ 0,05 ms); R01 concluído nos critérios obrigatórios. **Publicação 07/11:** servidor, cliente e atualizador conferidos em `b01817c73`/65. Limitações: borderline de amostragem em locomotion, cenários parados, morte inferida, poucas repetições só na CPU nova, teardown antigo não reproduzido (não corrigido).
- [x] R02: aprovado pelo usuário em 09/11/2026, publicado em homologação (08/11; protocolo 67; runtime `b2d90fb7b`, Actions 42473009178; validado no binário r02c `020208c8…`). Parte 1: amostras de posição por frame + âncoras de repouso; forma das famílias de gameplay corrigida (p95 0,1 px). Parte 2: reposicionamento explícito no wrap; reconexão 3/3; teto de 60 Hz (`frame`); fome real separada. Parte 3: resíduo das sobreposições isolado (captura do servidor antes do LateUpdate da areia/câmera) e corrigido com captura após todos os LateUpdate; reprodução controlada com travadas de 120 ms: fase antiga 10,5 px, r02c 1,06–1,16 px (p95 0,8) em 6/6 execuções. Campo removido sem redução de alocação demonstrada (alocador em blocos de 4 KiB: ≈ 0 no servidor); custo total r02c − baseline +25/+23/+22 KiB/s, sem regressão contra r02b. Picos de localhost: 41/48 janelas são travadas do servidor; o restante é atraso observado pelo Mirror ou travada de cliente. EditMode 1055/1055. Aceite perceptual aprovado pelo usuário em 09/11/2026 nos cenários testados; custos e limitações anteriores preservados.
- [x] R03: concluído e aprovado visualmente pelo usuário em 10/11/2026 nos cenários do pacote SB6-QA-R03. Protocolo68 local; Unity1068/1068, dois clientes reais, comparação offline, interrupções e três pares CPU/bytes concluídos. Evidências e limitações preservadas no checkpoint. Publicação pendente.
- [x] R04: Hurricane/Magnet/bolhas Submarine46 com receita aprovada, caminhada/replay, fase/prazo/clear e gates (423 combinados); manual pendente.
- [x] R05: concluído em 11/11/2026, aceite visual do usuário: "Está aprovado R05 pode marcar como concluido." Entrega candidate14/protocolo68: 17 famílias, core1088/1088, 9 cenários com 2 clientes, referências offline e 24 medições de custo qualificadas. Pacote SB6-QA-R05 aprovado; limitações preservadas. Sem publicação; próximos R06/R08.
- [x] R06: CONCLUÍDO em 11/11/2026; usuário confirmou "R06 está aprovado", nos cenários testados com o pacote C:/Users/mauri/SB6-QA-R06. Mechas1-6 com3entradas corrigidas offline/online; Pretty/Venus com subida gradual, máscara, absorção e áudio aprovados.1097/1097 Unity,195 CI;2clientes reais,4cenas offline,2portais e BossRush passaram;8custos baseline/final e comparação online/offline registrados;servidor Linux Release compilou (runtime Linux não testado). Protocolo69. Sem publicação/R08.
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


## Próxima retomada — mola C02 candidata local, pipeline do canhão ativo

5619659d9 (inclui c0cf7232e) enviado ao origin/v0.5.0. Actions40875720038 em andamento,
ainda sem confirmação de publicação. Último deploy totalmente confirmado76f2a948c.
901/901 Unity+C#0/0 do canhão e A12 dois peers preservados no checkout principal.

SpringLauncher.cs agora tem alterações LOCAIS ainda sem Unity/teste/commit:
HashSet virou mapa actor→IEnumerator; OnDisable para/descarta rotinas rastreadas;
finally restaura collider/BombController/proteção anterior e limpa offsets/sprites/
shadow/mount jump próprios antes de devolver controle. Flags de morte/end-stage
não são reabilitadas. Geometria/duração/room wrap permanecem nativos. Gate de
isActiveAndEnabled impede começar outra rotina num launcher desabilitado.
C# compilou0erros,6avisos preexistentes de Mirror; conferir final incremental.

Novo OnlineRoomSpringLifetimeTests.cs/.meta ainda NÃO executado: UnityTest real
PlayMode/TitleScreen, preparação10s e voo10s com proteção inicialfalse/true.
Actor ativa Awake antes da tagPlayer para não carregar o loadout persistente;
Movement fica disabled; evento de trigger nativo por reflection; verifica cleanup
após desativar mecanismo. Fixture pode precisar ajuste; não presumir aprovação.

Runner reservado ao CI. Depois de confirmar completed/success:
1. Copiar SOMENTE teste/metas novos para rodar baseline SEM copiar Spring modificado.
2. Preservar resultados na raiz Logs, corrigir fixture se preciso.
3. Copiar Spring modificado e validar focado, depois suíte completa esperada902.
4. C#final/UTF8/BOM/diff/flags/sprites; atualizar docs/skill e publicar só com checks.
5. Auditar corda C03: PlayerPreparingImpulseRoutine/PlayerImpulseRoutine também não
   têm finally; OnDisable atual só restaura tiles. Ainda sem alteração nesses arquivos.

Nenhum processo próprio de teste/build ativo nesta etapa; somente pipeline.
Não criar worktree, não fechar Editor, não limpar cenas/fontes/recovery do usuário.
Checklist17concluídos/16parciais; C02/C03 continuam parciais e curva completa pendente.

Pipeline5619659d9 confirmado completed/success. Baseline da mola em execução
PredictionSpringLifetime55Baseline; ainda SEM copiar Spring modificado ao runner.
Cannon candidato seguinte ganhou gate isActiveAndEnabled e teste PlayMode ampliado
para desativar GO ou só componente e impedir reinício via trigger desativado.
Essas últimas mudanças também ainda NÃO foram executadas no Unity. C# anterior
da mola passou0/0 no incremental; não há publicação de Spring neste checkpoint.


Mola: baseline PredictionSpringLifetime55Baseline falhou em PlayMode no primeiro
cenário, InputLocked permaneceu true após desativar o mecanismo. Com fix, dois
UnityTests focados passaram em PredictionSpringLifetime55Focused (mola3cenários,
canhão4cenários: preparação/voo, GO/componente, trigger repetido desativado).
Spring mantém proteção inicialfalse/true e devolve collider/BombController/input.
Suíte completa PredictionSpringLifetime55Full em execução,902casos esperados.
C# final em execução85130. Nada deste lote de mola foi publicado ainda.


## Mola C02 validada — lote pronto para envio

PredictionSpringLifetime55Full:902/902 aprovados,0falhas/0skips. C#final0erros/0avisos;
Python53/53 já aprovado, sem alteração posterior nas ferramentas Python. UTF8/BOM,
metas e diff check aprovados. Testes nativos de desativação GO/componente em PlayMode
passaram para mola/canhão; cola anterior de collider/BombController/input/proteção
foi removida sem mudar a curva, duração ou room wrap. Nenhuma cena/prefab alterada.
Sem nova comparação gráfica entre peers para o launcher; A12 real está documentado.
Protocolo55 mantido; nenhum wire/RPC/SyncVar novo. Checklist17concluídos/16parciais.

Próximo: auditar C03 PlayerPreparingImpulseRoutine/PlayerImpulseRoutine e integração
OnDisable da corda (hoje só restaura tiles); ainda NÃO há alterações nessas rotinas.
C02/C03 trajetórias completas/handoff, P0 comparativos e demais IDs continuam abertos.
Runner restaurado só nos fontes próprios antes de push; relatórios na raiz Logs.


## Checkpoint após envio da mola

8b3841929 enviado ao origin/v0.5.0. Actions40878816406 iniciado/em andamento;
NÃO anunciar deploy concluído antes do sucesso. Último deploy confirmado5619659d9.
Todos os fontes/testes desta etapa foram salvos;902/902 Unity, C#0/0, Python53/53.
Relatórios preservados na raiz Logs; fontes rastreados próprios do runner restaurados.
Nenhum processo próprio de teste/build continua ativo. Runner pertence ao pipeline.

Próxima retomada: conferir essa publicação; seguir C03/cancelamento da preparação e
voo da corda. Auditoria feita, ainda sem mudanças em BattleMode11ImpulseRopeController:
Awake requer cena BattleMode_11; fixture pode usar SceneManager.CreateScene com esse
nome para isolar sem carregar uma arena completa. OnDisable na partial Network só
restaura tiles; faltam tracking/dispose de outer+nested e restauração de flags/poses.
C02 curvas completas/handoff e os demais parciais continuam abertos. Checklist17/33
concluídos em implementação/testes,16parciais; matriz visual/manual permanece pendente.


## Retomada em29/10 — updater e C03 (em desenvolvimento)

Usuário confirmou A12 validado com2clientes reais e pediu marcar concluído. A12 já
estava[x]; registrado aceite humano na linha atual. Checklist permanece17/33 e16parciais.
Pipeline40878816406 de8b3841929 confirmado completed/success nesta retomada.

Updater agora mostra percentual independente por etapa,100% apenas após sucesso,
ETA da própria etapa e velocidade média de download emB/s/KiB/s/MiB/s/GiB/s.
Tamanho desconhecido fica indeterminado até EOF; download truncado/hash inválido/
estrutura inválida/extraction manifest errado/rollback não exibem100%. Checking e
etapas rápidas usam estimativa indeterminada quando não há dados suficientes.
Native updater compilado com warnings-as-errors;13testes updater passaram, conjunto
Python58/58 (50CI+8Directory). Binário local Build/CI/GameUpdater.exe gerado; sem deploy.

C03: nova fixture OnlineRoomRopeLifetimeTests.cs/.meta, PlayMode com cena isolada
BattleMode_11, preparação/voo, GO/componente, pose/proteção e reutilização. Primeira
baseline falhou na fixture por SpriteRenderer ausente; corrigido explicitamente.
BaselineFixedFixture em execução no runner SEM copiar runtime C03 alterado.

Código C03 LOCAL candidato: PlayerImpulseScope registra recursos/flags e guarda
IEnumerator core; rotina externa usa scope, Core descarta flight aninhado. OnDisable
interrompe e libera scopes, incluindo input/override/hazard/supressão/poses/proteção
anterior e token de autoridade, sem completar o voo/teleportar ao destino. Estados
nativos de morte/fim continuam com seus helpers e não recebem unlock. Mantidos
curva/ease/grid/colisão e timers nativos de CharacterHealth, que não são cancelados
para evitar apagar proteção de outras fontes. Ainda NÃO validado em Unity.

Root ganhou alterações alheias do Editor em3fonts: PressStart2P-Regular SDF,
Retro Gaming SDF e Resources/Fonts/PressStart2P-Regular SDF. Preservar e excluir dos
commits próprios. C# raiz precisou restore dos assets NuGet Temp/obj regenerados;
dotnet build em execução60926. Runner tem só teste/metas da baseline nesta etapa.
Depois: confirmar falha de runtime, copiar C03 e rodar focado/full903esperados;
C#final/UTF8/diff, docs/skill, restaurar tracked próprios e publicar com updater.


C03: baseline corrigida falhou por InputLocked true após desativar a corda. Com fix,
PredictionRopeLifetime55Focused1/1 e suíte PredictionRopeLifetime55Full903/903 passaram.
Depois da revisão, bindings visuais são reutilizados e o wrapper avança o core na
chamada, sem scheduler yield extra; teste Immediate1/1 passou com assert imediato.
Suíte final PredictionRopeLifetime55Final em execução (95989),903esperados. C#final0/0.
Depois: confirmar full final, validar UTF8/BOM/diffs, atualizar estado/checklist C03,
restaurar APENAS tracked C03 do runner e publicar junto ao updater validado58Python.
Preservar3fonts sujos do usuário; não incluí-los no commit. Nenhuma cena/prefab alterada.


## C03 e updater validados — pronto para publicação

PredictionRopeLifetime55Final:903/903,0falhas/0skips. Immediate1/1 confirma preparação
no frame da chamada, cancelamento de preparação/voo em GO/componente, restauração
de flags/poses/proteção e reutilização sem completar trajeto. C#final0erros/0avisos.
Python58/58 (50CI+8Directory), updater13/13; binário Build/CI/GameUpdater.exe compilado.
UTF8/BOM, metas, campos privados lidos e diff próprios revisados. Sem cena/prefab.

A12[x] recebeu o aceite expresso do usuário com2clientes reais; checklist17/33 e
16parciais permanece. C03 não foi marcado completo: curvas/handoff/manuais faltam.
Timers CharacterHealth e gameplay/ease/collision/quantização continuam nativos.
Protocolo55 mantido, sem wire/RPC/SyncVar novo. Próximo: publicação, conferir CI e
seguir curvas/handoff C02/C03 e demais parciais. Runner deve ser restaurado apenas
nos2sources C03 após preservar reports; fonts sujos do usuário não entram no commit.


## Checkpoint após envio do updater/C03 em29/10

Commit0ddcf872d enviado; Actions40910624716 em compilação. Último deploy concluído
confirmado8b3841929. Não anunciar publicação de0dd antes de completed/success.
Updater local pronto em Build/CI/GameUpdater.exe; no pacote público passa a valer
após o sucesso do CI. Um updater antigo já em execução usa sua cópia antiga emTemp;
para ver o novo progresso nesse primeiro update, usar o executável novo na pasta
do jogo antes de iniciar, ou aguardar a próxima execução do updater instalado.

903/903 Unity FINAL (PredictionRopeLifetime55Final),0falhas/0skips; C#0/0 e Python58/58.
Sources/metas próprios salvos e tracked C03 do runner restaurados; reports em Logs.
Só continuam sujos os3assets de fonte do usuário. Não limpar nem incluí-los emcommits.
Nenhum processo próprio de teste/build ativo; runner pertence ao pipeline.
A12 concluído por aceite expresso2clientes; checklist17/33 e16parciais.

Próximo: conferir CI e continuar descritores de curva/phase e handoff C02/C03;
C03 já tem cleanup/reuso/first-frame cobertos, mas descritores/visual2peers ainda
não foram implementados/validados por este lote. Não repetir suíte903 sem nova
mudança/falha. Continuar demais IDs a partir do checklist vivo, preservando offline.


## A11 retomado em29/10 — arcos por assento, em validação

Pedido atual: concluir A11 e ligar o arco à posição visual real de coleta/assento,
incluindo HeadOnly e direção, e fazer o caminho inverso na desmontagem.
Alterações locais ainda NÃO publicadas: pontos X/Y do assento e pé no mesmo sampler
nativo/rede; captura da origem antes do detach/snap; apresentação da montaria
pendente/destacada durante o arco; guardas autoritativas nos pickups/detach/remount.
Protocolo56 nos dois lados, pois NetworkArcState ganhou dois floats X.
Previsão Select usa destino de desembarque e assento da direção atual. Montagem
continua dependendo do evento aprovado; NÃO foi implementada previsão de coleta.

PredictionRidingAnchors56Focused passou14/14 (nove prefabs x quatro direções,
três repetições de amostragem/wire mais regressões prévias). Suíte completa
PredictionRidingAnchors56Full ainda em execução. C#0/0; Python58/58.
Harness Development ganhou mount-cycles: três remontagens pelo trigger nativo,
quatro Select por cliente, conferência da transferência de um ovo por player.
Capturas renderizadas opcionais preparadas; ainda NÃO executadas/inspecionadas.
Não declarar repetição2peers ou validação visual com base somente nos testes.

Próximo: conferir full; copiar últimos ajustes de direção/harness ao runner livre;
compilar Development e rodar smoke2peers mount_input=True,mount_cycles=True,
rendered=True,diagnostics=True. Revisar capturas/falhas; validar compilação final,
UTF8/BOM e atualizar A11 com a evidência realmente obtida. Lista de paths próprios
copiados ao runner em Logs/A11-56-owned-paths.json. Não restaurar fonts/settings
alheios do runner e não incluir os três fonts sujos do usuário no commit.
Actions40910624716 confirmado completed/success; nenhum novo publish56 iniciado.


A11 complemento em andamento após Full912/912 e FinalFocused14/14: root agora tem
previsão limitada de contato com NetworkWorldMount já aprovado, parado, visível,
spawnado e recente (250ms); clone permanece sem gameplay/colisores. Só Battle,
alvo conhecido e owner elegível. Origem/destino/assento compartilhados; confirmação
aceita até8ticks posteriores (batch de inputs + trigger), mesmo alvo125mm; sem
aprovação retorna ao estado confirmado por ack/timeout. Ainda NÃO testado Unity.
Novos testes de contato9montarias e matching6casos incluídos, mas não executados.
O build Development atualmente no runner (exec42946) ainda é anterior a esse
complemento: valida apenas arcos/frames/harness/repetição. Não publicar/commitar
sem validar complemento. Próximo após smoke é copiar novos paths, rodar foco/full
927esperados e reconstruir player para integração com contato previsto. Capturas
renderizadas A11 ainda não foram produzidas neste ponto.


## A11 — retomada após foco de contato, estado em29/10 13h05 local

PredictionMountContact56Focused2:30/30 passou sem skips. Inclui9montarias com
contato aprovado parado/recente, bloqueio de collider indisponível/estado antigo/
remoção/movimento, seis matching de tick/alvo incluindo wrap, rollback por ack,
confirmação preservando fase e cancelamento, além dos14testes de arco anteriores.
O primeiro foco falhou9fixtures por escrita direta em SyncList do client; corrigida
a fixture para desserializar o pacote de frames, mantendo as guardas de autoridade.

Primeiro smoke real PredictionRiding56CyclesRendered1 completou3remounts e4Select
por cliente, transferência de1ovo em cada ciclo, sem erros/warnings de rede. Esse
binário ainda NÃO tinha o novo contato previsto. Os PNG eram pretos; visual-review.json
registra que NÃO houve aceite visual, apesar de result.rendered=true indicar GPU
ativa. Capturador corrigido para request da câmera URP/RT; ainda não validado.

Sourcefinal adiciona contactAvailable aprovado ao world proxy (wire56), exige input
e movimento efetivo em direção ao pickup, não ao sair dele; não há remontagem
especulativa estando parado. O harness agora caminha para sair/retornar ao trigger.
PredictionMountContact56Full está emexec57152 (928esperados), seguido por build
PredictionMountContact56DevBuild no mesmoexec apenas se full passar. NÃO copiar
nem restaurar runner enquanto esseexec não terminar. Depois rodar novo smoke120s
com mount_cycles/rendered/diagnostics, confirmar previsões Mount/phase e imagens
variadas. Protocolo56 ainda não publicado/commitado; C#último0/0 e Python58/58.
Lista paths próprios no Logs/A11-56-owned-paths.json. Preservar fonts alheios.


A11 atualização: PredictionMountContact56Full passou928/928,0skips. Build emexec57152
PredictionMountContact56DevBuild ainda em andamento. Não interromper nem copiar
runner até ele terminar. ÚNICA diferença atual de C# raiz/runner é atributo
DefaultExecutionOrder(-150) em LocalNetworkInputSmoke: precisa anteceder
NetworkPlayerInput(-100), depois de PlayerInputManager(-200), para GetDown sintético
não desaparecer no LateUpdate do input. Primeiro smoke tinha apenas riding authority,
não prediction_begin gesture_only; esse limite está identificado, não declarar
predição comprovada naquele lote. Após build atual, copiar SOMENTE harness final
e reconstruir Development (sem repetir928, é só mudança de ordem da fixture).
Então executar smoke90s com2clients/rendered/mount-cycles/diagnostics e exigir
Select previsto/confirmado nos dois, contato Mount quando caminhada reentrar,
ovos intactos e PNG distintos reais. Demais fontes root/runner byte a byte iguais.


A11 integração2 (PredictionMountContact56CyclesRendered2):3ciclos e1ovo porowner
novamente passaram;0erros/warnings no servidor. Capturas de ambos confirmam4gestos
Select previstos,3confirmados preservando fase; o primeiro sofreu warmupGPU do
capturador (new_sequence/timeout), não serve como medida de atraso de gameplay.
Nenhum Mount gesture_only nesse lote: fixture escolhia direção porPlayerId, mas
spawn estava embaralhado e ambos andavam para fora da arena/parede. Não declarar
previsão de contato comprovada com essa captura. Render URP funciona, mas RT1024x896
cortava os players nas bordas pois viewport usa Screen.width/height reais.

Correções SÓ na fixture Development: sentido pela posiçãoX do spawn, aquecer a
pipeline antes do primeiro arco e RT nas dimensões reais da tela. C#final0/0.
Novoexec90018: PredictionMountContact56CaptureDevBuild e, se compilar, smoke90s
PredictionMountContact56CyclesRendered3, já com script atualizado. Não usar runner
até término. Fontes de runtime/testes continuam exatamente as que passaram928/928;
a ordem-150 e essas correções da fixture compilaram em dotnet e serão compiladas
no Unity pelo build. Próximo: confirmar contato previsto comfase, ovos e imagens
complayersvisíveis; se falhar, corrigir causa e repetir só teste afetado. Ainda56
local não publicado. A11 continua parcial até fechar essas evidências.


## A11 concluído em implementação e validação — protocolo56 pronto

PredictionMountContact56Full:928/928,0falhas/0skips; foco30/30. C#final0erros/0avisos.
PredictionMountContact56CyclesRendered3: duas instâncias completas Mirror/Direct3D11,
90s,3remounts nativos via trigger e4Select porowner. Cada cliente registrou3previsões
Mount e4Dismount, todas confirmadas preservando fase, sem rejeição/timeout/new_sequence.
Arquivo riding-review.json valida esses pares. Transferência de1ovo porplayer
conferida no servidor em cada saída/entrada, sem perda/duplicação.0erros/warnings
nos eventos de servidor e0erros de replicação nos três processos.

Frames reais da câmera gameplay URP foram inspecionados para início/subida/descida/
fim em Mole/Tank. A11 marcado[x] no critério implementação/testes,18/33 e15parciais.
Não confundir request offscreen da câmera com a saída final de HUD/barras, nem com
aceite humano de sensação/input lag. Os9prefabs/quatro direções e HeadOnly/fullbody
estão cobertos pelos testes de pontos de assento/pé e amostragem/wire compartilhada.
As capturas renderizadas registram avisos de shaders LensFlare opcionais removidos
no build URP da baseline; sem alteração de pipeline/settings/cena/prefab neste lote.

O smoke agora exige previsões locais confirmadas (além dos checkpoints do servidor),
e3PNGs distintos porclient quando rendered. validate_mount_smoke.py também passou
nos dois captures reais, e seis regressões rejeitam aprovação sem previsão, falta
de arcos, rollback/timeout e contam só oowner. Python64/64. Novas opções:
--mount-input --mount-cycles --diagnostics --rendered; usar player Development.

Próximo: UTF8/diff finais; preservar reports; restaurar só os17tracked próprios do
runner listados em Logs/A11-56-owned-paths.json; salvar/publicar todos os22paths
próprios, sem os3fonts do usuário. Protocolo56 alinhado no jogo/directory; builds
cliente e servidor devem ser publicados juntos. Ainda NÃO houve push56 neste ponto.


## A11 salvo e enviado — publicação56 iniciada

Commit `bf111acca` enviado a origin/v0.5.0. Actions40922400063 em andamento:
https://github.com/MauricioSuporte/Super-Bomberman-6-Unity/actions/runs/40922400063
Build/publicação conjunto cliente+servidor (sem server-only), perfilquick; testes
já executados localmente928/928Unity,30/30foco,64/64Python. Não usar o runner enquanto
esse job estiver ativo. Seus17tracked próprios foram restaurados antes do push,
com reports preservados em Logs; root só tem os3fonts do usuário fora do commit.
A11[x],18/33implementados/testados e15parciais. Não presumir deploy56 até consultar
conclusão completed/success; último deploy confirmado permanece0ddcf872d/protocolo55.
Continuar C02/C03 e demais parciais pelo checklist após conferir a publicação.


## Próximo C02 — auditoria retomada durante o CI56 (sem código novo)

AuthorityMotionScope em NetworkAuthorityMotion.cs cobre o lifetime nativo e owner
interpola apenas snapshots aprovados. Isso NÃO é um descritor de trajetória inteira.
CannonLauncher.LaunchPlayerArc põe a parábola no Rigidbody/root, com end opcionalmente
arredondado; SpringLauncher alterna3caminhos: sem riding (altura no root), montado
(altura no root + jump/shadow da montaria) e desmontado com riding (root plano e
altura somente no renderer). Spring ainda faz wrap horizontal contínuo, vertical
para tile interno e clamp do arco pelos bounds de sala em World3. Não generalizar
bounce de player para esses caminhos: ele assume altura no root e pose de bounce.

Próxima implementação deve extrair sampler nativo compartilhado (root/ground/
altura separados) sem alterar regras offline; enviar receita aprovada e fase/pausa
pelo proprietário existente. Observer e owner devem usar o mesmo tempo aprovado
com ajuste de NetworkTransform/NetworkPlayerAnimation/NetworkMountAnimation.
Priorizar Riding/bounce e cancelar por morte/disable/cena. Final da curva arredondado
ou wrap precisa stop/handoff aprovado; não liberar input por extrapolação. Retenção
SyncVar para late observers e cleanup com token da AuthorityMotionScope. Há testes
existentes de geometria Spring e lifetime Cannon/Spring em
OnlineRoomLauncherLifetimeTests.cs; manter assinaturas privadas refletidas por eles.
Nenhum sampler/receita C02 novo foi escrito neste lote. Não marcar C02 completo.
CI40922400063 ainda em build na última consulta; runner reservado a ele.


## C02 em implementação — protocolo57 local, NÃO validado

Pedido explícito atual: implementar C02. Confirmado Actions40922400063 completed/
success para bf111acca/A11/protocolo56; runner livre na última consulta.
Três fonts sujos do usuário continuam preservados. Nenhum commit57/push novo.

Novos arquivos PlayerLaunchArc.cs e NetworkLaunchPresentation.cs (+metas), ainda
sem testes Unity: sampler nativo compartilhado para parábola/platô, root vs visual,
wrap/clamp de sala; receita SyncVar retida no NetworkPlayerInput com revision/seq,
phase/time/paused, owner timeline previsto e observer interpolado. Root restaurado
após reconciliação/LateUpdate via FX existente. Handoff usa replay só de movimento;
nenhuma proteção/input/posse concedida no client. RecipeLastStatecancel/timeout/pose
precisam revisão/testes. NPA/NMA agora consultam o clock de receita para o owner.

NetworkVisualPose carrega metadata condicional de lift porlaunch (sequence/mode/
lift), correção da altura entre poses aprovadas; sombra Pink ancorada ao solo tem
lift negativo quando altura no root. Serializador custom economiza:1byte extra
sem receita,9mais com receita; precisa testar Weaver/roundtrip real. Novo getter
PinkShadow.HasGroundedJump só lê apresentação. ProtocolVersion/supervisor57 alinhados.

Cannon e3variantes Spring/geyser usam sampler/LaunchScope; flightProgress paused
explícito. Spring outer guarda/dispose nested flight e cancela dash antes do lock.
Battle4 usa platô compartilhado; está sendo corrigido teardown: dictionary de
iterators, nested disposal, manter posição no cancel, preservar proteção prévia e
não gerar bounce em cancel/death/end-stage. Getter CharacterHealth.HasExternalInvulnerability
novo para restaurar o flag externo prévio; revisar finally e warnings.

Exec70639 dotnet build em andamento/resultado a conferir. csproj gerado foi ajustado
só para incluir os2sources novos no check terminal; não commitar csproj.
Próximo: conferir compilação, adicionar testes geometry/phase/pausa/handback/late
observer/cancel/Weaver/lifetime Battle4, copiar sources/metas ao runner livre e rodar
foco/full antes de build+smoke2peers. Código57 ainda não comprova C02 concluído.


## C02 — checkpoint de validação57

PredictionLaunch57Focused4:38/38 Unity passaram,0skips. Inclui geometria nativa,
serialização condicional47/56bytes, owner/observer, pausa, late state/revisions,
correção de lift, sombra gerada sem collider, cancel/death, replay só de caminhada,
bounce+stop aguardando o salto e disable real de GO/componente Battle4 em PlayMode.
TentativaFocused3 teve36/37 porfixture com motor desabilitado; corrigida habilitando
o motor e repetida noFocused4. C# incremental0erros/0avisos.
PredictionLaunch57Full em execução; buildDevelopment e smokes ainda não executados.

Harness Development ganhou --launchers arena|spring|cannon: duas conexões reais,
primeiro player sem montaria e segundoPink; salto nativo completo e cancelamento
no segundo spring/cannon, com restauração do controle. Câmera URP captura após os
presenters. Validator exige begin/sample/end de owner e observer emcada cliente,
além de movimento desbloqueado posterior. Ainda não há evidência dos smokes57.
Novo helperTools/RoomDirectory/validate_launcher_smoke.py precisa commit.
Próximo: conferir suíte completa, buildDevelopment57 e3smokes nas cenas Battle4,
Stage_2-3/cannon eStage_3-3/geyser. Não marcar C02 concluído antes da validação.


Checkpoint seguinte: PredictionLaunch57Full966/966 passou,0skips. Python56CI+8Directory
passaram. Integridade strictUTF8/BOM e metas dos19C# alterados/novos verificada;
gitdiff--check limpo. BuildPredictionLaunch57Development em execução no runner;
ainda não restaurar paths do runner ou publicar até concluir os smokes.


Revisão encontrou timeout de receita pausada: heartbeat de pausa é intencionalmente
suprimido, mas timeout antigo invalidava a geração após pausa longa. Corrigido
localmente para aplicar timeout somente emestado não pausado; dois testes owner/
observer novos. Ainda não copiado ao runner enquanto a primeira buildDevelopment
estiver executando. Repetir foco40 e full968 com esta correção antes da buildfinal.


Focused5 passou40/40 (inclui pausa prolongada); C#0/0 novamente. Arena1 executou
2clientes reais e ambos begin/end/amostras/unlock,0erros/avisosservidor, câmeraURP.
Inspeção adicional detectou recuo11.5ms na fase do observer após stall: resultado
antigo não deve contar como aceite final. Presenter local agora retém o maior
elapsed jáexibido apenas dentro da mesma geração (root e lift); nova geração não
herda fase. Dois testes novos (42focados/970full esperados) ainda nãoexecutados.
Validator agora rejeita recuo de fase superior1ms. Cannon1 nãoexecutou: reserva
mínima3GiB insuficiente com suíteUnity em paralelo. Não diminuir a reserva; aguardar
Editor/testes terminarem e rodar os smokes sequencialmente. Full2+Development2
emexecução usam apenas a correção de pausa, não a monotonicidade local posterior.
Depois copiar estes2sources novamente, rodar full970 e buildDevelopment3 final.


Final de compilação/testes57: PredictionLaunch57Full3 passou970/970,0skips,
incluindo os42casosLaunch. C#final0erros/0avisos; Python60CI+8Directory passaram.
Correção de monotonicidade e logs launch_recipe_publish no servidor estão nesta
suíte. BuildPredictionLaunch57Development3 emexecução; será a base dos smokes
finais comvalidatorque rejeita recuo. Ainda não há commit/push57. PreflightSSH passou.


Cannon2/Development3 passou:2clientes reais Stage_2-3,90s,owner/observer contínuos
sem recuo e desbloqueados, voo completo + cancelamento montado,0erros/avisos.
Geyser1/Stage_3-3: critérios de curva/phase/unlock passaram nos2clientes, mas smoke
completo REPROVOU por47warnings de sprites de bolha ausentes no CoopSceneCatalog.
Não ocultar os warnings nem descrever Geyser1 como aceite limpo. Causa: catálogo
Stage33BubbleAnimations carregado viaResources, fora das dependências serializadas.

CoopSceneCatalogBuilder agora inclui explicitamente esse recurso só naStage_3-3;
novo testegera catálogos e exige todos osframes(>40), sem mudar gameplay/RNG dasbolhas.
Full4/971esperados +Development4 emexecução. Ao finalizar Full4, scriptcopia somente
Assets/Resources/CoopScenes/Stage_3-3.asset gerado do runner ao checkout original.
Owned-paths JSON inclui builder e esseasset para restauração antes deCI.
Próximo: conferir971, build4 e repetir Geyser2, Arena2 eCannon3 semUnity emparalelo.
C02 ainda parcial até concluir estes aceites e atualizar checklist/publicação57.


Full4 passou971/971,0skips;43casosLaunch incluindo assetcatalog. EditorC#também
0/0 após restoreNuGet do csprojgerado (tentativa --no-restore falhou só porassets
NuGet ausente). Stage_3-3.asset gerado copiado ao root:48refsSprite acrescentadas
mais contentHash, nenhuma mudança na lógica de bolhas. Development4 concluído.
Cannon2 continua válido: Assembly-CSharp.dll deDevelopment4 é exatamente o mesmo
SHA256(2a2ef52acb90c633004cf0ab2398e69625d5498d110b6393b9fa5a8ab740562e).
Não repetir Cannon3 sem mudança relevante: só catálogoStage_3-3/Editor mudaram.
Geyser2 eArena2 estão emexecução sequencial(90s cada) no exec65508; nenhumaUnity
batch/teste emparalelo. Conferir resultado/failure antes de marcarC02 e publicar.


## C02 concluído e validado localmente — pronto para publicação57

Cannon2/Geyser2/Arena2 passaram90s cada,2clientesMirror reais porcenário, ambos
owner eobserver com begin/amostras/end/unlock e fase monotônica.0erros e0avisos
emcada resultado. Assemblyruntime igual nos3cenários; catálogoStage_3-3 corrigido
naDevelopment4. Arena2 passou também a remoção dos atoresduráveis e encerramento.
CapturasURP inspecionadas(incluindo bolhas após correção); não são medição de
inputlag humano ou validação exaustiva de HUD, áudio ou conexãoWAN.

Principais correções: receitaSyncVar noNPI existente; sampler nativo/online;
curvasParábola/Platô com wrap/clamp existentes; clock único root/player/mount porrole;
liftcondicional preserva pose e sombraPink; sombraBattle4 decorativa; pausa longa
não expira a receita; fase não recua com updates da mesma geração. Morte/disable/
scene/cancel limpam a apresentação; cancelnativo não teleporta ao fim. Fila de
bounce/stop aguarda arco principal. Handoff reproduz caminhada sem executar açõesA.
Destino, disputa, colisões, invulnerabilidade e consequências continuam autoritativos.

Sources ecatálogo do root conferidos byte-a-byte com runner testado;20C# strictUTF8/
BOM/meta passaram, diff--check limpo. Fonts do usuário preservados. Checklist19/33
implementados/testados,14parciais. Próximo: restaurar SOMENTE paths tracked em
Logs/C02-57-owned-paths.json no runner,commit/push conjunto cliente+servidor57 e
conferir Actions; não executar build/teste local no runner durante CI.


## Checkpoint de publicação C02 — 29/10/2026

Commit runtime/assets/testes/skill/docs:97d88c4f7c2573cae37a136758416a6e14389760,
branchv0.5.0, push confirmado. Actions40964582861(build-and-deploy123765680748)
Homologation both/quick emandamento; checkout emcurso na última consulta pelo
connectorGitHub. Runner devolvido: somente20trackedpaths próprios restaurados
antes dopush; fontes/settings anteriores preservados. NenhumaUnity/SB6 própria
ativa na conferência anterior ao push. NÃO iniciar teste/build local nesse runner
atéCI concluir. Root ficou somente comas3fontes do usuário sujas.

C02 marcado[x], checklist19/33 e14parciais. Últimas evidências finais:
- Logs/PredictionLaunch57Full4.xml:971/971,0skips;43Launch.
- C#runtime/Editor0/0;Python68/68;20C# strictUTF8/BOM/meta e diffcheck limpos.
- Cannon2,Geyser2,Arena2/result.json:2clientesreais,90s cada,0erros/avisos,
  owner/observer semrecuo econtroledevolvido. Cannon2mesmoassembly daDevelopment4.
- Catálogo Stage_3-3com48sprites adicionais resolve47warnings doGeyser1.
- CapturasURP inspecionadas; não mede WAN/inputlaghumano, HUD/áudio completo ou
  todas as montarias. Aceite manual continua namatriz geral.

Próxima retomada: consultar Actions40964582861; emfalha inspecionar logs e corrigir,
emsuccess registrar publicação57/commit97d88c4f7 e seguir IDs ainda parciais do plano.
Não refazer estes testes sem nova mudança/falha relevante. Não reverter fontes.


## Visuais de montaria e player — protocolo 58 em validação local

Baseline publicado: protocolo 57, commit `97d88c4f7`; Actions `40964582861`
concluído com sucesso. O checklist geral permanece 19/33; esta revisão corrige
regressões visuais reportadas pelo usuário, sem encerrar outros IDs.

Implementação local:
- Montaria pendente: apresentar os frames aprovados durante a montagem antes de
  conceder o estado lógico de montado; colliders e scripts nativos continuam desligados.
- Riding: distinguir arco visual recebido da coroutine nativa para que setters de
  cabeça/estado não escondam o player durante a montagem/desmontagem.
- Tank/Mole: restaurar os offsets HeadOnly autorais após pose aprovada e reconciliação.
- Pink Louie: desligar o lifecycle nativo no client após o salto e limpar os outros
  ramos visuais ao voltar à locomoção, mesmo sem mudança da direção em cache.
- Emote/AFK: reforçar exclusividade do sprite aprovado e devolver a locomoção ao
  owner quando a pose termina. Enviar explicitamente o toque direcional junto com L,
  mantendo a proteção contra gerar um segundo A a partir de botão segurado.
- Morte: encaminhar a causa para CharacterHealth, não guardar causa de um golpe
  ignorado por invulnerabilidade, remover a antiga montaria do player e transmitir
  a animação destacada pelo transporte WorldMount existente no Battle. Co-op usa
  seu registro de renderers. O client não executa morte/dano localmente.
- Diagnósticos de renderer/frame/head e fixture Development com dois clientes,
  ovo Blue real, emotes/AFK, morte normal/explosão, salto Pink e Tank/Mole.
- Authenticator e supervisor alinhados no protocolo 58 (mudança de campos na rede).

Validação e evidências locais:
- `VisualRepair58Full`: 983/983, sem skips. Depois foi ampliado o caso de morte
  com explosão ignorada seguida de morte normal: `VisualRepair58IgnoredBlast`, 1/1.
- `VisualRepair58Full2`: 983/984; o novo teste de input falhou por identidade Mirror
  não inicializada na fixture. Inicialização corrigida; `VisualRepair58Full3` passou 984/984, sem skips.
- C# final: 0 erros, 0 avisos. Python: 64 testes CI + 8 Directory aprovados.
- `VisualRepair58Peers1`: falhou ao iniciar emote nativo. Logs fechados comprovam
  L+Up segurado, mas somente L pressionado; o transporte descartava a borda direcional.
  A correção de bordas explícitas está no Full3. Não considerar este smoke aprovado.
- Próximo: terminar Full3, gerar Development com `OnlineClientBuild.BuildPerformancePlayer`,
  repetir smoke renderizado em `Logs/VisualRepair58Peers2`, revisar capturas e corrigir
  falhas reais. Só depois restaurar os arquivos próprios do runner e publicar cliente
  e servidor juntos, conforme autorização persistente da sessão.

Retomada operacional: checkout principal `v0.5.0`, HEAD `db07900ac` antes desta revisão.
Fontes do usuário permanecem sujas e não devem entrar no commit. Runner isolado em
`C:/Users/mauri/.sb6-actions-runner/_work/Super-Bomberman-6-Unity/Super-Bomberman-6-Unity`.
Arquivos copiados estão em `Logs/VisualRepair58-owned-paths.json`: restaurar somente
`tracked`, nunca as fontes/settings do usuário. Não executar smoke de três processos
junto com Unity e preservar reserva de 3 GiB de RAM. Finalizar writers antes de ler
ou copiar JSONL. Nenhuma correção 58 publicada até esta etapa.


Atualização da validação: `VisualRepair58Full3` passou 984/984, C# 0/0 e Python
72/72. `VisualRepair58Development4` compilou com sucesso. A tentativa Development3
usou nome incorreto do método de build e foi substituída pelo entrypoint existente
BuildPerformancePlayer; não houve erro no código do jogo.

`VisualRepair58Peers2` confirmou emote/AFK a pé e montado, ovo Blue coletado e morte
normal animada nos dois clientes (frames 0–5, sem renderer extra na caminhada).
A captura URP arc-01-00 mostra a montaria visível sob o player no arco. O teste
parou ao checar Pink Jump em 200 ms: os logs fechados comprovam que o comando C
chegou depois dessa checagem e iniciou JumpUp normalmente. A fixture agora espera
o início nativo real, com prazo de 2 s, antes de mandar o movimento lateral.
Development5 em execução com essa única alteração de harness; a fonte funcional
continua sendo a validada no Full3. Próximo smoke: VisualRepair58Peers3.


Development5 compilou; Peers3 revelou que a captura URP durante o botão segurado
pode atrasar o cliente por mais de 100 ms. O servidor libera held input após esse
limite deliberadamente: os logs mostram emote correto seguido da liberação. A
fixture esperava que ele continuasse ativo aos 700 ms e falhava nesse caso válido.
Agora a fixture cancela AFK anterior com movimento confirmado, observa a entrada
real na pose (prazo 2 s) e só depois envia movimento para sair. O prazo de held
input do jogo permanece inalterado. Development6 em execução; próximo Peers4.
Nenhuma alteração funcional após Full3, somente sincronização do dev harness.


### Resultado final local — 30/10/2026

Development6 compilou com sucesso. `Logs/VisualRepair58Peers4/result.json` aprovado:
120 s solicitados, 141,25 s totais, servidor e dois clientes Mirror reais renderizados,
mesmo Assembly-CSharp SHA256 `16c97d4c5e3653ad06e4f4ac1d94b921a6beab37d52e1a4fd7f56406c78771ab`.
Todos os checkpoints nativos dos dois jogadores concluíram; sem erros de replicação
e sem eventos de warning do servidor. O fixture usa atores duráveis; não é uma
medição humana de latência nem uma nova validação manual em produção.

Cada client registrou 300 amostras de caminhada prevista sem renderer extra; morte
Blue normal e Pink explosão com frames 0–5; montagem visível no owner/observador
(15/22 amostras no client1, 17/19 no client2). Conferência adicional dos JSONLs
fechados: 15 amostras laterais Pink por owner, somente Left + corpo MoutedRigth ou
MoutedLeft, sem JumpUp residual. HeadOnly Mole = 1,3 em todas as direções; Tank =
1,4 vertical / 1,5 horizontal, iguais aos offsets autorais. Capturas URP de montagem,
lateral Pink e cabeça Tank/Mole inspecionadas. A arena 15 possui blackout próprio.

Validação funcional Full3: 984/984 sem skips; após isso só mudou a sincronização
do harness Development, recompilado na Development6 e exercitado no Peers4. C#
final 0 erros/0 avisos; Python 64 CI + 8 Directory aprovados. 14 C# com UTF-8
estrito, meta e fonte idêntica ao runner. Nenhuma cena/prefab/texture alterada.
Próximo passo: restaurar somente arquivos próprios do runner e commit/push both58.


### Publicação iniciada

Commit `605f9d662a9557b7c74b757ec050c01036c7008b`, push v0.5.0 confirmado.
Actions `41054228040`, job `124057578682`, target both / quick iniciado.
Última consulta: checkout em andamento; pré-flight/build/deploy ainda pendentes.
Consultar esse run antes de declarar publicação concluída. O runner teve somente
os arquivos próprios restaurados antes do push e o Actions assumiu seu checkout.
Os três assets de fonte sujos do usuário continuam preservados no root.
Progresso do plano permanece 19/33: este lote corrige os visuais reportados, sem
marcar outros IDs parciais como concluídos. Não há smoke/Unity local ainda rodando.


## Retomada C03 — protocolo 59 local

Aceite humano dos visuais 58 recebido e publicação 58 confirmada success.
PlayerLaunchArc inclui Rope com ease-out quadrático e pixel grid nativos. A
corda publica esse descritor e fase após cada passo aceito, sem mudar checagem
de obstáculos. O presenter usa timeline buffered nos dois papéis e limita a
continuação à maior fase já aprovada, pois blockers móveis só o servidor decide.
Scope da corda retém heartbeat/pausa e encerra receita depois de restaurar flags,
antes do snapshot de handoff. Preparação/tiles mantêm transporte aprovado existente.

Testes novos no OnlineRoomLaunchCurveTests: geometria nativa/pixel grid, prefixo
confirmado, bloqueio antecipado, owner/observer e pausa longa. Fixture Development
launcher `rope`: BattleMode_11, dois owners a pé/Pink, input real Up para segurar
corda, Right durante voo/handoff e cancelamento da segunda instância.

Exec 5179 roda PredictionRope59Full (992 esperados) e, se passar, Development
sequencialmente no runner isolado. Owned paths em Logs/PredictionRope59-owned-paths.json.
Não sobrescrever runner enquanto Unity estiver ativo. Próximo: verificar suíte,
smoke renderizado --launchers rope com diagnostics, closed JSONL e capturas.
Não executar Unity junto com smoke de três processos (reserva 3 GiB). Fontes do
usuário preservadas. Ainda sem commit/publicação 59.


C03 checkpoint seguinte: Full1 passou 992/992, sem skips; Development1 compilou.
Revisão encontrou voo truncado em menos de um intervalo SyncVar: protocolo 59
agora envia também begin/stop confiáveis somente para Rope, com revisão única.
A recepção conserva a duração relativa de begin/stop tardios e aplica o mesmo
deslocamento de relógio à geração, sem extrapolar prefixo desconhecido. Dois
testes adicionais de entrega tardia/stop curto (owner/observer) levam a 994.
Fixture final usa corredores livres x=-7 e x=5 para voo completo/cancelamento,
e repete x=-6 sob pilar para testar prefixo curto. Não usar Development1 como
artefato final: ele contém harness anterior à escolha dos corredores e não tem
RPC de bordas. Exec28588: Full2+Development2 finais em andamento; fontes copiadas.
Python65CI+8Directory73/73; C# final em exec67118 aguardando resultado.
Ainda sem commit59 nem smoke59. Preservar as fontes do usuário e não tocar runner.


C03 Peers1 falhou com evidência útil: servidor concluiu os três casos, ambos
clientes receberam begin/stop da geração curta, mas só apresentaram dois voos.
Os JSONLs fechados mostram também batch inicial de fases 0–540 ms no mesmo
frame após shader/render stall. Retime no recebimento não bastava: o relógio
da rede podia avançar antes do frame de apresentação e pular a janela curta.

Correção atual: relógio local de reprodução por ator/generation, iniciado no
frame efetivo, passo limitado a 50 ms e ao prefixo aprovado; stop recebido fica
retido até apresentar o endpoint. Corpo e montaria usam a mesma fase mapeada ao
clock nativo, sem repetir uma pausa já observada. Pausa conserva fase; timeout
sem aprovação cancela a receita sem conceder predição e não revive a geração
expirada. Ack de movimento livre não encerra cedo uma corda ainda apresentada.
Novo launcher de outra família preempta o clock antigo. Nenhum input, física,
dano ou invulnerabilidade nativa roda no client.

Full2 passou 994/994 e Development2 compilou; ambos anteriores ao novo clock.
Exec16595 roda Full3 (995 esperados) e Development3 finais. Não copiar runner
ativo. C# mais recente passou antes da guarda extra de timeout, repetir final.
Próximo smoke: PredictionRope59Peers2 (rope/rendered/diagnostics, 90 s). Exigir
três gerações em cada client, begin/end, fases sem recuo e motor desbloqueado.
Manter Peers1 como falha; não flexibilizar o validador para escondê-la.

Auditoria preliminar C04: MinecartView já interpola cantos de trilho e offset/
escala; não criar outro presenter. Battle9/12 têm transporte próprio, Stage3-2
usa stream co-op. Escopo do plano mantém entrada/saída/ocupação autoritativas;
a pendência histórica de "boarding previsto" precisa ser confrontada com esse
limite antes de implementar. Nenhuma alteração C04 feita neste lote.


Full3 passou 995/995 (0 skips), C# 0 erros/0 avisos. Development3 compilou com
o clock final. Foi acrescentado somente um evento diagnóstico após aplicar o
endpoint: launch_recipe_endpoint. O validador agora exige os endpoints das três
gerações e um prefixo apresentado de 0–50 ms em cada cliente. Cinco testes do
validador passaram. Exec36903 compila Development4 com esse log; exec10048 C#
final. Fontes finais funcionais são as do Full3, fora esse registro diagnóstico.
Próximo: PredictionRope59Peers2 via smoke_full_clients.run(... launcher_kind='rope',
diagnostics=True, rendered=True, seconds=90). Aguardar saída de todos os players
antes de ler JSONL. Não há smoke ativo agora. Ainda sem commit/publicação59.


Peers2 falhou na precondição nativa do prefixo curto: o input Right anterior
ainda chegava quando a fixture reposicionava para x=-6; o ator mudava para um
corredor livre antes do Up, produzindo voo completo. Os JSONLs foram copiados
após fechar todos os processos. Confirmam o clock corrigido: endpoints de
750 ms apresentados nas gerações 1–5 pelos dois clientes, sem encurtar o voo.
Não tratar esse smoke como aprovado, pois o cenário de bloqueio não ocorreu.

Harness final agora envia passo neutro e espera RopeSmokeAck com tick depois de
dois FixedUpdates do client, seguido de processamento desse tick e controles
zerados no servidor, antes de cada reposicionamento. Evita fabricar cenário com
input antigo em trânsito. Registro e mensagens são somente Development.
Exec30716 compila Development5; exec97764 C# final. Próximo Peers3, mesmas
exigências do validador, sem afrouxar endpoints/curva/bloqueio/cancelamento.
Código funcional continua o do Full3/995; após ele só diagnóstico e harness.


### C03 aprovado localmente — 30/10/2026

Development5 compilou. `Logs/PredictionRope59Peers3/result.json` aprovado: 90 s
solicitados, 108,625 s totais, dois clientes Mirror reais com câmera URP e input
sintético no caminho de hardware/NPI. Mesmo Assembly-CSharp nos três players:
`1ef76e58be6f36bbbdfb711746cfaf9f4cc87a74570849ec9d03fa7329062f7c`.

Ambos apresentaram e registraram os endpoints completos antes do handoff:
- Player1/seq1: 750 ms, posição (-7,-4), voo completo a pé.
- Player1/seq2: 20 ms, posição (-6,3.5625), prefixo bloqueado pelo pilar.
- Player2/seq1: 240 ms, posição (5,-0.3125), cancelamento com Pink montada.
Client1: 2begins/2ends/17samples owner e1/1/6 observer. Client2:1/1/7 owner e
2/2/19 observer. Fases sem recuo e movimento desbloqueado após end; zero erros
de replicação/avisos do servidor. Capturas URP distintas de voo a pé e montado
foram inspecionadas. A fixture usa atores duráveis; não é teste humano de latência
nem comparação manual completa com offline. Poses especiais nativas montadas e
qualidade perceptual permanecem no roteiro global, sem alterar arte offline.

Full3 995/995 passou (sem skips). Depois dele somente log de endpoint e handshake
de input da fixture; ambos compilaram no Development5 e foram exercitados no
Peers3. C# final0erros/0avisos; Python65CI+8Directory73/73, mais5 testes focados
do validador ampliado. UTF8/meta e fontes do runner idênticas ao root conferidos.
Nenhuma cena/prefab/texture modificada. Checklist atualizado:20/33 feitos,13parciais.
Próximo: restaurar só owned paths do runner, commitar/publicar cliente+servidor59.


### Publicação C03 iniciada

Commit runtime/testes/docs/skill `cef9598d77e9fbd86c14dfe7aa936d978b05a0ce`,
push v0.5.0 confirmado. Actions41150289348, target both / quick, última consulta
queued. Conferir esse run na retomada antes de declarar servidor59 publicado.
Runner restaurado somente nos owned paths; nenhum Unity ou smoke ficou ativo.
Três assets de fonte do usuário preservados no root. Checkpoint, notas e matriz
foram atualizados; C03 marcado feito por implementação/testes, manual global
permanece separado. Próximo ID de implementação: C04 (ver auditoria preliminar
acima); os outros 13 IDs parciais não foram marcados concluídos.


### C04 em implementação — protocolo60 local

Runtime compila C# com0erros/0avisos. Focused6/6 passou em Unity, incluindo
owner/observer na curva e pausa, corte de geração/portal e cancelamento nativo
em Enter/Rail com reuso e preservação de proteção preexistente. Relatórios em
Logs/PredictionCart60Focused.xml/.log. Runner livre após publicação59; copiados
somente caminhos próprios de Logs/PredictionCart60-owned-paths.json. Fontes do
usuário preservadas.

Exec56886: suíte completa seguida de Development, no runner isolado. Próximo:
conferir XML/compilação e rodar smoke_full_clients com launcher_kind cart9/cart12
(Battle) e cart32 (NormalGame/Stage_3-2), diagnostics/rendered=True. A fixture
exige embarque/saída nativos de dois jogadores, um Pink, proteção conservada;
validador exige Enter/Rail/Exit + portal ou Jump e retorno ao motor livre nos
capturados de cada cliente. Ainda NÃO executado. Não marcar C04 concluído nem
publicado até fechar evidências. Boarding especulativo não se aplica: o plano
C04 mantém disputa/entrada/saída e rota aleatória autoritativas. Carrinho não tem
comando de direção do ocupante para prever.


Full passou1001/1001,0skips; XML/log copiados para Logs/PredictionCart60Full.*.
Python67CI+8Directory=75/75. Development está compilando na mesma exec56886.
Fonte funcional final é a da suíte completa; depois dela somente documentação.
Próximo executar cart9/cart12/cart32 quando Unity encerrar, preservando reserva
RAM3GiB; ler/copiar motionJSONL somente depois de todos os writers encerrarem.


Development1 compilou (205s). Auditoria posterior achou que o snapshot inicial
capturava só a direção visível do cart: cancelamento no portal poderia deixar
outra direção achatada. CaptureAndApply agora captura todos os renderers do corpo,
e o teste nativo altera uma branch inativa e exige sua escala original no cancel.
Também impede snap de saída/continuação do portal após morte.

Exec32298: Full2 das fontes finais, seguido de Development2 e três smokes
renderizados95s: cart9(28160),cart12(28161),cart32(28162/NormalGame Stage_3-2).
O script para ao primeiro erro; verificar relatórios antes de retomar. C#final
exec46633. Nenhuma publicação60 ou commit ainda. C04 permanece parcial até essa
validação. Primeiro Full1001 e Development1 são anteriores ao último ajuste de
limpeza, não devem ser descritos como binário final.


Auditoria final co-op: prefab Minecart não tem Rigidbody2D. Classificador
CoopPauseWithGameplay agora inclui o controller de carrinho, para congelar
corpo/animação junto ao passageiro; teste focado novo. Alteração ainda só no root,
posterior à cópia da exec32298. Essa exec valida Full2/Development2/smokes ANTES
dessa correção de pausa. Depois copiar fontes finais, rodar Full3(esperado1002),
Development3 e ao menos repetir cart32; não publicar fontes finais sem compilação.


Continuação local: bounce de recuperação pode chegar antes do fim apresentado
Exit. NetworkPlayerBounce agora delega ao NPI a fila aprovada de cart (máx32),
incluindo stop, e libera apenas ao Idle/cleanup. Dois testes owner/observer exigem
não truncar Exit, completar endpoint do bounce e restaurar collider. Alterações
apenas no root depois da cópiaFull2; executar Full3(esperado1004)/Development3 e
smokes das fontes finais. Owned paths atualizado com NetworkPlayerBounce.cs.
Full2/Development2/primeiros smokes são anteriores a pausa e fila de bounce.


Exec32298 encerrou: Full2 passou1001, Development2 compilou. Peers-cart9 falhou
antes do embarque por AFK do ator durante warmup (motion server confirma
visual_override no ponto da estação); nenhum trajeto aprovado, sem marcar aceito.
Capturas JSONL copiadas somente após fechar os3processos, tentativa preservada.
Harness agora manda Right0.4s pelo input real, espera ack neutro e exige saída da
pose AFK antes de reposicionar. Sem mudar a regra de entrada em produção.

Exec29139 é a retomada ATIVA: fontes finais copiadas (inclui pausa co-op e fila
de bounce), Full3 esperado1004 → Development3 → smokes95s renderizados
Logs/PredictionCart60Peers2-cart9/cart12/cart32 (ports28160–28162). Para no primeiro
erro. C#exec99767. Ainda sem commit/push/publicação60. Ao concluir conferirXML,
result.json de cada smoke, inspecionar PNGs, byteUTF8/meta, atualizar checklist,
restaurar somente ownedpaths do runner e publicar ambos clientes/servidor.
Skill recebeu notas do C04 (ainda sem commit). Um campo legado nunca lido
PortalVisualSnapshot.rendererEnabled foi removido (sem YAML por não serializado).


### Retomada31/10 — Full3 e reprodução das fases curtas

Exec29139 encerrou: Full3 passou1004/1004,0skips; Development3 compilou.
Peers2-cart9 aprovado (2clientes,95s, renderizado,0erros/avisos, Enter/Rail/Exit e
handoff em ambos). Peers2-cart12 completou nativo2owners mas falhou no validador:
primeiro Enter ausente em ambos os clientes; Rail/PortalSink/Rise/Exit presentes.
Capturas fechadas preservadas, inclusive client2 copiado após saída. Não marcar
aceito nem enfraquecer validador. cart32 não executou porque script parou.

Correção LOCAL posterior ao Full3: relógio limitado a50ms/frame inicia no primeiro
prefixo entregue de uma viagem, sem pular o embarque para pegar o estado mais
recente. Retém pausa e monotonicidade; bounded history continua64. NPI's
AuthorityPresentationTime usa o clock do cart para corpo/montaria. Co-op registra
ids dos renderers no manager existente e usa esse clock no stream já existente,
incluindo animação, mantendo máscaras e materiais. Enter/Exit publicam também
edge forçado. Teste novo de stall/pausa/rewind (total esperado1005).
C#exec59156 começou antes dos2Publishforce, conferir/rodar final após isso.
Próximo Full4/Development4 e Peers3-cart9/cart12/cart32 das fontes finais.
Ainda sem commit/push/publicação60; três fontes do usuário intocadas.


Exec65828 ATIVA: Full4(esperado1005) → Development4 → Peers3-cart9/cart12/cart32,
95s cada,2clientes renderizados. Fontes copiadas iguais ao root neste instante;
nenhuma mudança runtime posterior. C#do clock passou0/0; os2Publishforce vieram
logo depois. Conferir final C# depois dos testes. Publicação60 ainda NÃO feita.


### Fontes finais C04 — retomada31/10

Full4 concluiu1005/1005,0skips. OrquestradorPython4388 interrompido após identificar
sua associação ao Unity24664 de Full4; o Editor de teste continuou e encerrou
normalmente. Não houve Development4/Peers3 (evitado build de fontes ultrapassadas).
XML/log Full4 copiados ao root.

Ajuste final no mesmo clock: ClaimApprovedCart no recebimento protege o histórico
de animação antes de ClientApply poder podar o Enter pelo relógio global, sem
aplicar root nesse recebimento. Clock não avança além do último tempo aprovado;
testes exigem claim imediato sem mudar root e hold após acabar o prefixo recebido.
Nenhuma outra mudança runtime após a cópia abaixo; manter congeladas até resultados.

Exec48960 ATIVA: Full5(1005 esperado) → Development5 → Peers4-cart9/cart12/cart32,
95s renderizados cada, dois clientes Mirror reais, portas28160–28162. Para ao
primeiro erro. C#final exec55191. No root/runner mesmas fontes próprias, conforme
Logs/PredictionCart60-owned-paths.json; três assets de fonte do usuário preservados.
Próximo: conferir resultados, corrigir somente falha demonstrada, inspecionar PNGs,
UTF8/meta e checklist, restaurar owned paths do runner e publicar cliente+servidor60.
Ainda sem commit/push60. C03/protocolo59 segue última publicação confirmada.


### Retomada final do clock — exec45657

Full4 passou1005/1005 e foi preservado. Full5 roda/encerra separadamente (Unity5488)
sem seu orquestradorPython26812, que foi interrompido para evitar build ultrapassado.
Exec45657 aguarda esse Unity encerrar naturalmente antes de copiar qualquer fonte
para o runner; depois copia/loga Full5, executa Full6 esperado1006, Development6 e
Peers5-cart9/cart12/cart32,95s renderizados por cenário,2clientes,ports28160–28162.

Última correção local: MinecartView.paused confirma o intervalo parado. Relógio
conserva o último tempo recebido e, na retomada, pula somente o intervalo de pausa
já confirmado da mesma geração. Evita atraso de vários segundos após pausa longa.
Teste novo exige sem continuação inventada antes do resume e salto dos40s parados
após aprovação. Claim no recebimento, limite temporal e replay do bounce seguem.
Fontes congeladas para Full6/Development6; não marcar C04 concluído nem publicar
antes dos3resultados e revisão das imagens. Nenhum commit/push60 ainda.


Full5 passou1005/1005,0skips, preservado em Logs/. Exec45657 já iniciou Full6 das
fontes finais (1006 esperado); depois Development6/Peers5. C#final exec53293 passou
0erros/0avisos. UTF8/meta12arquivosC# e git diff --check aprovados. Depois da cópia
para Full6 somente documentação/skill alteradas; não há runtime posterior.


Full6 APROVADO1006/1006,0skips (Logs/PredictionCart60Full6.xml/.log). Exec45657
segue em Development6; depois Peers5 das3cenas. Nenhuma mudança runtime posterior
ao Full6. C#final0/0, UTF8/meta12C# e diffcheck aprovados. Ainda sem publicar60.


### Retomada04/11 — C04 pendência Stage3-2

HEAD agora cef2e5d86 (Pomori sprites, commit do usuário). Preservar também novos
diffs do usuário: DestructibleRocketController.cs/prefab, Rocket.asset,
StageHeatHaze.mat, Rocket 2.png/.meta, além das3fontes. Manifesto de hashes dos
tracked alheios em Logs/PredictionCart60-user-files-20261104.json. Não adicionar
esses diffs ao commit C04. Runner limpo em cef2e5d8 antes da cópia própria.
MCP Unity não consta nos tools expostos; Editor do usuário permanece aberto.

Resultados recuperados: Development6 compilou; Peers5-cart9 e-cart12 aprovados,
2clientes renderizados,95s,0erros/avisos. cart32 nativo completo nos2players, mas
validador falhou no handoff. JSONLs completos conferidos após todos os processos
encerrados: servidor liberou P2 por5s, então AFK normal; client só terminou sua
reprodução após esse intervalo. Limite50ms/frame durante rail longo acumulava
atraso no FPS reduzido pela captura PNG.

Correção local: Rail/Jump/PortalHidden permitem100ms/frame, transições curtas50ms;
nenhuma fase/geração/rider é pulada no avanço. Quatro testes de FPS10/transição
adicionados (suíte esperada1010). Harness agora aguarda ack3 após o client soltar
ApprovedCartActive e envia caminhada real+ack neutro, exigindo motor nativo livre
antes de registrar input restored. Evita confundir AFK legítimo após viagem com
lock de gameplay, mantendo validador estrito e input pela produção.

Exec58571 ATIVA: Full7 → Development7 → Peers6-cart32/cart9/cart12,110s/cenário,
2clientes renderizados, portas28160–28162. Inicia pelo cenário que falhou. Copiados
somente ownedpaths ao runner; nenhuma alteração runtime após essa cópia. Guardas
RAM continuam ativas (3GiB para smoke); não fechar Editor nem apps do usuário.
Ainda sem commit/push/publicação60. Última publicação confirmada é protocolo59.


04/11: Full7 APROVADO1010/1010,0skips. OrquestradorPython21776 foi interrompido
antes do build para evitar fixture ultrapassada; Unity26500 finalizou normalmente,
XML/log preservados. Após Full7 só mudou helper Development de teleporte: sincroniza
transform a partir do Rigidbody antes do reconcile, evitando screenshot inicial
com parent interpolado no spawn antigo. Produção C04 é a fonte validada em Full7.
Exec46497 ATIVA: Development8 compila helper final → Peers7-cart32/cart9/cart12,
110s/2clientes/renderizados,ports28160–28162. Nenhum runtime posterior à cópia.

C#root restaurado (Temp project.assets inexistente após regeneração do Editor),
compilou0erros/6avisos conhecidos de Mirror/package, nenhum do código alterado.
Preflight SSH de homologação passou,protocol60alinhado,disk11GiBlivre. GH CLI sem
auth; API pública lista Actions. Último commit cef2e5d86 temrun41228579688failed,
anterior41150289348success59; investigar motivo antes de publicação60. Não reenviar
run antigo enquanto o runner está ocupado pelo teste local.


04/11 continuação: Full7 passou1010/1010,0skips. Exec46497 compila Development8
e rodará Peers7-cart32/cart9/cart12,110s cada. Apenas helper Development de
teleporte mudou após Full7 (SyncAuthorityMotionTransform antes do reconcile);
produção é a validada em1010. Evita root visual antigo nos primeiros frames da
fixture, sem mudar contatos/embarque em produção. Ainda sem alteração runtime
posterior à cópia para Development8. C#root passou0erros/6avisos legadosMirror.
Run cef2e5d86 falhou por shutdown signal do runner durante testes Python (logs
confirmados), não por erro do jogo. Listener atual ativo; não reenviar esse run
antigo. Novo commit C04 deverá publicar ambos. SSHpreflight60 passou.


### C04 aprovado localmente —04/11/2026

Development8 compilou. Peers7-cart32/cart9/cart12 aprovados:110s solicitados por
cenário,129.391s/115.234s/115.047s totais. Dois clientesMirror reais, renderização
URP, input sintético no caminho P1/NPI e caminhada confirmada após saída apresentada.
Mesmo Assembly-CSharp nos3players:5e57a431256696dd55ec478b51decf3c6fd1a3045d4da9724c4dcea72dc3823f.
Cada cliente registrou owner e observador, Enter/Rail/Exit, mais Jump no shuttle
Room1 e PortalSink/Rise emBattle12.0erros/avisos do servidor. Imagens de entrada,
trilho/jump e rise inspecionadas; não substituem teste humano de sensação/offline.

Full7 passou1010/1010,0skips. Após essa suíte só o helper Development de snap foi
sincronizado antes do reconcile; compilou noDevelopment8 e foi exercitado nos3smokes.
Python68CI+8Directory76/76; C#0erros/6avisos já conhecidos emMirror. ByteUTF8/meta,
diffcheck e paridade owned sources root/runner aprovados. Nenhum prefab/cena/texture
alterado pelo C04. Os7arquivos alheios tracked mantêm hashes do início da retomada;
Rocket2.png/.meta também preservados sem stage. Checklist21/33feitos,12parciais.
Próximo restaurar APENAS ownedpaths do runner, commitar/pushC04 e conferirCIboth60.


Runner restaurado em2listas exatas:14ownedpaths de fontes, depois14arquivos
reescritos por import/build/teste (manifesto emLogs/PredictionCart60-generated-runner-paths.json).
A segunda lista foi verificada contra allowlist explícita e checkout limpo inicial;
só no runnerisolado, nenhum revert/clean no root. Runner gitstatus limpo agora,
nenhum player/Unity de teste ativo. Editor do usuário continua aberto.
Build reemitirá seus catálogos gerados normalmente. Fontes, Rocket e demais diffs
alheios no root mantidos; índiceGit estava vazio antes do stage C04.


### Publicação C04 iniciada —04/11/2026

Commit6c2b92cae6b740d1b96b793523bbb69bc68c647e,pushv0.5.0confirmado.
Actions41948608681 in_progress, targetboth/quick (testesUnity já locais1010).
Link:https://github.com/MauricioSuporte/Super-Bomberman-6-Unity/actions/runs/41948608681.
Conferir build/deployLinux e updaterWindows antes de declarar servidor60 publicado.
Workspace só com7diffstracked alheios +Rocket2.png/.meta; nada deC04solto. Runner
foi limpo apenas nos caminhos próprios/generatedverificados; nenhum smoke ficou
ativo. Editor do usuário preservado. Checklist21/33 feitos,12parciais; próximoC05
após confirmar esta publicação. Não reenviar run antigo cef2e5d86/protocolo59.


### Publicação C04 concluída —04/11/2026

Actions41948608681 COMPLETO/sucesso confirmado pela API do GitHub: buildLinux e
Windows sem erros, deployLinux/KCP aprovado, canalGameUpdaterWindows publicado,
artefatos/relatórios enviados. Manifesto confirma sourceCommit6c2b92cae6b740d1b96b793523bbb69bc68c647e,
protocol60,targetboth,workingTreeDirtyBeforeBuild=false,benchmarkHarness=false.
C04 concluído em implementação/testes/publicação. PróximoC05. Checklist21/33feitos,
12parciais, mais QAmanual/consolidação fora da contagem. Não ficou nenhum smoke
ouUnityde teste ativo; Editor e todos os arquivos alheios do usuário preservados.


## C05 iniciado — auditoria de portais

Escopo confirmado: Battle7, swap e World3; destino/ocupação/reserva autoritativos,
transição aprovada e corte do teleporte. Primeira correção LOCAL em
BattleMode7PortalController.cs: guardar/restaurar proteção externa anterior por
CharacterHealth; não reativar controle/bombas/collider após morte ou fim de fase.
UTF8 conferido. Compilação C#exec95258 a consultar. Ainda sem testes Unity desta
alteração, sem commit/publicação; C05 NÃO concluído. Próximo adicionar regressão
nativa de cancelamento/proteção/morte, auditar iteradores aninhados e validar
portal/saída/blackout com dois clients. Preservar os diffs alheios Rocket/fontes/
material já listados no checkpoint anterior. Protocolo publicado continua60.


### Continuação C05 —04/11

C#exec95258 passou0erros/6avisosMirrorlegados. NativePortal: pilha de iteradores
rastreada e explicitamente descartada no disable; scope de autoridade liberado
após restauração. Preserva flags externos/visuais e proteção prévia; morte/fim
não reativam controle/collider/bombcontroller. Focused1/2 falharam em drift1415u
porMovePosition/interpolação pendente; limpeza agora fixa Rigidbody na última
posição apresentada e sobrescreve movimento pendente (mesmo contratoC04).
Focused3 passou1UnityTest com2variantes (hidden/sink), preservação de flags,
proteção true/false, cancelamento e reuso. Relatórios fechados emLogs/.

Auditoria incluiu bombas: rotina escondia render/collider/notifier sem finally.
Agora tem lifetime rastreado e restaura só bomba viva em cancelamento, sem punch
ou detonação; transferência normal aoStartPunch mantém sua própria colisão.
Esse caso foi adicionado ao mesmoUnityTest, ainda aguardandoFull2.

HarnessDevelopment: portal7(Battle7) eportal34(NormalGame Stage3-4),2owners a pé/
Pink. Confere origem/destino nativos, proteção e unlock; aguarda owner largar
AuthorityPresentation antes de caminhar cominputreal. ValidadorPython exige os
endpoints em cada cliente (owner/observer) e movimento livre após chegada.
9testes focados do validador passaram. C#exec73284 passou0erros/0avisos (antes do
último finally da bomba). Sem wirechange: protocolo continua60; "61" nos nomes
Logs é loteC05, não alteração do protocolo. Não publicar antes das evidências.

Exec70381 orquestradorPython28864 interrompido após vincular aoUnity27468 Full1,
que continua até encerramento natural. Exec38704 ATIVA espera esseEditor sair,
copia apenas ownedpaths e rodaFull2 esperado1011 → Development2 →
PredictionPortal61Peers2-portal7/portal34,110s renderizados,ports28170/28171.
Para no primeiro erro. Nenhuma alteração runtime posterior à cópia planejada.
Runner já tinha13dirt gerados porCI antes desta cópia; não descartar como se
fossem fontesC05. Preservar rootRocket/fontes/material/newPNG. C05 ainda parcial;
nenhum commit/push/publicação deste lote. Editor do usuário permanece aberto.


Full2 passou1011/1011,0skips (inclui bomba em cancelamento). Development2 foi
interrompido explicitamente no processo19488 após conferir log/projectPath do
runnerisolado e parent26336; nenhum Editor do usuário foi tocado. Evitado build
ultrapassado após último ajuste de cleanup daGlove.

Última correção: limpar supressão transitória daPowerGlove mesmo após morte,
sem reativar sprites de carry no jogador morto/finalizando. Setter agora verifica
mover vivo antes de refresh visual. MesmoUnityTest adiciona estado morto e exige
input/collider/bombcontroller bloqueados e flagdeGlove limpa. Código atual está
em Full3(esperado1011), seguido de Development3 e Peers3-portal7/portal34,
110s/2clientes/renderizados/ports28170–28171. Exec15845 ATIVA. Sourcefrozen após
cópia; próxima mudança só se houver falha demonstrada. Não publicar parcialmente.

Auditoria World3Hall: entrada e retornoWorldMap passam porRouteCoopScene, mantendo
roteamento do servidor. SwapGroundTileHandler eSwapGroundTileBlackoutHandler
possuem gates nos pontos de simulação/ignição; sem escolher alteração de terreno
no client. Não houve nova predição de destino/rota/reserva. QAmanual de todas as
variações de sala/heldbomb ainda deve ser mantido separado do aceite automático.


Full3 APROVADO1011/1011,0skips. C#finalexec73888 passou0erros/0avisos. Exec15845
agora em Development3; depois2smokesPeers3. Sem alteração runtime após Full3.
GloveDeath, Collider/InputDead e bomba em cancelamento foram realmente executados
no mesmoUnityTest dePlayMode, além de hidden/sink e reuso. Só comparar render e
handoff entre dois peers quando os writersJSONL encerrarem. Protocolo ainda60;
nenhum commit/publicaçãoC05. Não marcarC05feito antes desses resultados.

C05 continuação: Full3 aprovado1011/1011. Peers4 Battle7 passou; Stage3-4
completou os dois portais nativos, mas o validador não encontrou a origem de um
owner em motion_sample (Update). Novo portal_pose_sample coleta após presenters
no LateUpdate1500; validação estrita de origem/destino e controle preservada.
Development5 e Peers5 dos dois cenários em execução; não publicado.

Peers6: Battle7 aprovado; Stage3-4 endpoints/controle aprovados em ambos,
mas reprovado por catálogo (BigWorm e sprite sólido gerado) e NullReference
em Stage34SandstormEffect.OnDisable antes de Start. Corrigidos recursos explícitos
no catalog builder, pixels conhecidos de Texture2D.whiteTexture sem readback
e cleanup nulo/idempotente dos grãos. Dois testes novos. Full4 + Development6
+ Peers7 nos dois cenários em execução (exec39061); não publicar antes do aceite.

Diagnóstico refinado: sprite sem nome é o grão da tempestade (textura1x1
descartada no CPU), não Texture2D.whiteTexture. Removido fallback branco
genérico; manter apenas esse pixel legível resolve a origem sem alterar protocolo.
Teste de cleanup também exige pixels legíveis. Orquestrador27004 encerrado
antes do build para evitar smokes sabidamente obsoletos; Unity Full4 PID25564
continua até gravar XML. Não copiar fontes no runner até ele terminar.
Próximo: copiar grain/test final, Full5 e Development7, Peers8 de ambos.

Full4 aprovado1013/1013; C#finalexec81778 aprovado0erros/0avisos.
FinalFocused primeiro:2/3 (teste criou textura e chamou Destroy adiado emEditMode).
Corrigido fixture para cleanup antes deStart duas vezes e inspeção do pixel
com descarte explícito dos próprios assets. Runtime não alterado por essa falha.
FinalFocused2/Development7/Peers8 emexec29276; fontes finais copiadas ao runner.
Nenhuma publicaçãoC05 até validar ambos os cenários finais sem erros.

### Retomada final em 05/11/2026 — C05 aprovado

FinalFocused2: 3/3; Development7 compilado. Peers8 Battle7 (130,672s) e
Stage3-4 (115,640s): dois clientes reais, câmeraURP, origem confirmada e destino
após presenters, retorno ao motor nos dois papéis. Zero erros/avisos servidor.
Assembly igual no servidor e clientes: bdc29a145ab52758a9dc6777e0ab2b944383142ccd48ee06849f548eb8c900ab.
Imagens finais de chegada inspecionadas. A captura não mede latência de hardware
nem substitui comparação humana completa com offline. Checklist22/33 e11parciais.

Durante a pausa, HEAD avançou para c7c9e7a8c (sprites do foguete, trabalho do
usuário). Runner foi limpo pelo CI; nenhum cleanup adicional é necessário.
Actions41962406679 desse commit recebeu shutdown do runner durante a build;
servidor e updater não foram publicados. Listener está ativo na retomada.
Preservar também PlayerPersistentStats, DestructibleRocketController, fonte e
StageHeatHaze atualmente alterados pelo usuário. Publicar somente arquivos C05.

C05 commit ba9be59ee1de049fdedf215bdafec53550e1e66b enviado a origin/v0.5.0.
Publicação both/quick iniciada no Actions42010967595 em05/11/2026:
https://github.com/MauricioSuporte/Super-Bomberman-6-Unity/actions/runs/42010967595
Aguardar servidorLinux e canalGameUpdater antes de marcar publicado.
Fontes, PlayerPersistentStats, DestructibleRocketController e StageHeatHaze
do usuário ficaram fora do commit. Não modificar o runner enquanto CI trabalha.

### Publicação C05 concluída em 05/11/2026

Actions42010967595 finalizou success. Linux dedicado e Windows compilaram sem
erros; servidor, updater, artefato Windows e resumo de deploy passaram.
Build limpa, profilequick/targetboth, protocolo60, commit de ambos:
ba9be59ee1de049fdedf215bdafec53550e1e66b. deploy-status.success=true;
canalGameUpdater0.5.0 aponta SB6-Windows-2026-11-05_06-28-22.zip.
Checklist permanece22/33 implementados/testados,11parciais. PróximoC08.
Nenhum arquivo do usuário entrou nos commits C05 ou de checkpoint.

### C08 retomado em05/11/2026 — continuidade de ímã/redirecionamento

Baseline16f96722f. Preservar Packages/manifest.json e packages-lock.json do usuário
(hashes emLogs/PredictionC08-user-packages.json). Runner semEditor/CI ativo, mas
com12arquivos gerados de build anteriores; não limpar esses arquivos. Editor
do usuárioPID24924 intocado. Fontes próprias: NetworkBombFx.cs e
OnlineRoomWorldBombMotionTests.cs. Nenhum protocolo novo neste lote.

Encontrado: Magnet/Search não preservavam sequência de segmentos; recebimento
podia pular ao próximo tile e até usar extrapolação deKick além do aprovado.
Agora mantêm fila comoEnvironment, sem extrapolar novo passo magnético/Search.
Kick mantém caminho reto responsivo, mas curva aprovada termina primeiro no
canto antes de aplicar nova direção; fila de revisões descarta duplicatas antigas.
Seis casos owner/observer Magnet/Search/Kick exercitam canto,stop,sem gameplay;
fixture final também inclui pausa e mensagem velha após término.

C#exec76626:0erros,6avisos legadosMirror. Focused inicialexec40055 está no runner
(apenas seis casos). Após terminar, copiar teste final com pausa/stale e rodar
suíte combinada. Integração real dos trajetos C08 ainda pendente; não marcarC08
concluído nem publicar até validar os resultados. Checklist segue22/33.

C08 Focused aprovado6/6. Full combinado emexec33910 com pausa/stale adicionado.
Revisão adicional encontrou cancelamento nativo que retorna ao tile anterior:
stop divergente do último endpoint deve cortar a fila imediatamente, não exibir
rota descartada. Correção e3regressões adicionais estão na raiz, ainda não
copiadas ao runner ocupado. Rodar nove casos finais após oFull.

FixtureDev preparada emLocalServerLoadTest.cs: -load-test-field-motion junto
a presenter-removal executa nativeMagnet/Search eKick+TryRedirectKick num
corredor isolado(30,30), com paredeColliderStage para stop. Sem cena/prefab novo.
Python smoke_full_clients.run(field_motion=True,presenter_removal=True,
diagnostics=True) valida origem/canto/destino dos3netIds em cada client e
mantém regressão de remoção do presenter. Não exige GPU (rendered=False).
11testes do validador passaram. BuildDev/integracao ainda pendentes.

C08 Full aprovado1019/1019,0skips. FinalFocused aprovado9/9 com cancelamento
e pausa/stale. C#finalexec61707 aprovado0erros/0avisos. Python71CI+15Directory
=86/86. UTF8 estrito e hashesPackages do usuário preservados. Development em
exec64073, seguido de smoke110s Stage_2-1 com2clientes headless reais.
Nenhuma capturaGPU neste lote; manter QA perceptual separado.

C08 baseline ativo é protocolo63 (commits dechat posteriores aC05), mantido
sem nova alteração dewire. Development compilou; smoke atual completou no
servidor Magnet/Search/Kick nativos e presenter-removal. Aguardar fechamento
dos3processos/validaçãoJSONL para concluir integração; exec64073 continua.

C08 Peers1 REPROVADO pelo validador: Kick autônomo avançou atéx32.061985
antes de curva confirmada. Servidor completou os3trajetos e remoção; logs dos
2clientes preservados. Não aumentar tolerância nem descartar a evidência.
Causa: SamplePlayback ainda aplicava continuação localKick em worldMotion=true.
Corrigido para continuar localmente apenas comando não-world; chute local
predito mantém caminho existente. Seis regressões adicionais comprovam espera
noendpoint deKick/Magnet/Search autônomos para owner/observer, sem inventar
uma direção pela geometria livre. Full2final emexec28650, depoisDevelopment2
ePeers2; nenhuma publicaçãoC08 até passar. Código final congelado.

C08 Full2FINAL aprovado1028/1028,0skips; inclui15regressões novas e todo
OnlineRoom/COM. C#finalexec39612 aprovado0erros/0avisos. Development2 em
exec28650, seguido dePeers2. Não modificarfontesrunner até terminar.

### C08 — integração final aprovada em 05/11/2026

Peers2 passou por 117,875s em Stage_2-1 com dois clientes Mirror reais headless.
Ambos apresentaram origem, canto e destino de Magnet, Search e Kick redirecionado;
cada um registrou nove amostras de movimento após transferência do presenter.
Zero erros e zero avisos do servidor. SHA de Assembly-CSharp idêntico nos três
processos: c73da0f8f49ec3a9cdc7b6cfa28a95bcec85860d7bbde6650efdb9bcdda4b1e8.

Full2 final: 1028/1028, sem skips. C# final: 0 erros/0 avisos. Python: 86/86.
Fontes do runner idênticas às testadas; restaurados apenas os três arquivos
próprios de código/teste. Preservados os arquivos gerados anteriormente no runner.
Packages/manifest.json e packages-lock.json do usuário intactos, fora do lote.
Nenhuma cena/prefab editada; o fixture é exclusivo de Development.
Checklist atualizado para 23/33 e 10 parciais. Próximo item: C09.

Revisão finalC08 antes do commit: stop antigo com revisão menor que uma
continuação enfileirada ainda podia descartar a rota, pois ClientMotionRevision
representa só o trecho aplicado. Adicionado gate pelo maior serial já enfileirado,
antes de processar cancelamento/troca de kind. Três regressões específicas.
Root tem versões STAGED anteriores e mudanças finais UNSTAGED emFX/teste;
restage somente após validar. Final18/Development3/Peers3 emexec61196, fontes
próprias novamente no runner. Nenhum commit/pushC08 ainda. PreservarPackages.

C08 Final18 aprovado18/18; gate de serial enfileirado validado. C#final
exec14743:0erros/0avisos. Development3/Peers3 ainda emexec61196. Depois de
passar: conferirparidadeUTF8 e hashesPackages, restaurar só3fontes próprias
no runner, restageFX/teste/docs, commit/push e acompanhar publicaçãoboth.
Basepublicada63/16f96722f confirmada noActions42077315561 success.

C08 Peers3 FINAL aprovado em 118,812s, já com gate de revisão enfileirada.
Ambos os clientes apresentaram os três trajetos e oito amostras após transferência
do presenter, sem erros nem avisos do servidor. Assembly igual nos três processos:
a2ab64f9a80ce0b9c760d1796c40c208492fe91617d3501dc783ddc454a90c26.
Final18 18/18; Full2 anterior 1028/1028; C# final0erros/0avisos; Python86/86.
Paridade de fontes/UTF8 confirmada e só as três fontes próprias foram restauradas
no runner. Packages do usuário preservados byte a byte. Pronto para commit/push.

C08 commit3ca94c369c235e32799826e386700bfb680ad123 enviado aorigin/v0.5.0.
Actions42110831096 iniciado para cliente+servidor no protocolo63:
https://github.com/MauricioSuporte/Super-Bomberman-6-Unity/actions/runs/42110831096
Aguardar deployLinux e canalGameUpdater antes de marcar publicação concluída.

### Publicação C08 concluída em 05/11/2026

Actions42110831096 publicou Linux, Windows e canal GameUpdater com sucesso.
Builds sem erros, targetboth/profilequick; deploy-status.success=true;
deployment.protocol=63 e workingTreeDirtyBeforeBuild=false. Todos os manifestos
apontam para 3ca94c369c235e32799826e386700bfb680ad123.

Validação antes da publicação: Full2 1028/1028; Final18 18/18 após gate adicional
de serial; C# final0erros/0avisos; Python86/86; Peers3 com2clientesMirror reais
headless e três trajetos nativos, sem erros/avisos e com transferência preservada.
Checklist23/33 implementados/testados,10parciais. PróximoC09.
QA perceptual/GPU/offline segue na matriz geral. Nenhuma alteração do usuário
emPackages entrou no lote; hashes originais preservados até a confirmação final.

### C09 retomado em05/11/2026 — protocolo64 local, não publicar ainda

Base99a72b14f (C08 publicado63). Packages/manifest.json e packages-lock.json
são do usuário; hashes emLogs/PredictionC09-user-packages.json. Runner isolado
livre ao início,12arquivos gerados deCI preservados. Fontes próprias listadas
emLogs/PredictionC09-owned-paths.json; inclui2novosC# e metas. Editor24924 não
foi fechado. Nenhuma cena/prefab editada. Nenhum commitC09 ainda.

Implementação local: CoopTileFrame inclui animated/frame no snapshot inicial;
CoopTileAnimation envia deltas compactos de até48células, revision por mapa,
a20Hz somente nas células animadas cacheadas. Snapshot estrutural a4Hz não
repete matrizes por avanço de frame. Delta altera só frame no cache para não
perder mudança ainda não enviada de cor/matriz/colisão. Frames recebidos pausam
relógio nativo do tile; leitura no client não consome Random. Tiles comPrefab
viram clones visuais semPrefab/collider. Conveyor mantém seu transporte próprio.

Caldeira usa Bomb.Bind/Report/EndNetworkBombMotion para fallback WorldBombMotion
quando o jogador é removido. OnDisable cancela MovePosition pendente e restaura
a animação congelada; End não publica sem NetworkServer.active. Environment
cancelado corta a fila se stop divergente; regressões C08 ampliadas. Foguete
semRigidbody agora participa do freeze de gameplay no stream de sprites.

Novos testes OnlineRoomMechanismPhaseTests: RNG/frame/color/matrix, prefab
sanitizado, protocolo/revisão/scene/path/frame inválido/late snapshot/terreno
removido, NativeBoiler PlayMode e pause doRocket. Focused1 e2 não executaram
por erro de API(nome GetInstantiatedObject) e namespace(global::StageAssets),
já corrigidos. Focused3 emexec62222; aguardar XML antes de copiar fonte.

C#csproj raiz não incluiu arquivo novo automaticamente. Check explícito com
Logs/PredictionC09-compile.targets, condicionado apenas aAssembly-CSharp, passou
0erros/6avisos legados antes das últimas revisões. Refazer checkfinal com esse
target se .csproj continuar antigo; não editar csproj gerado. Primeiro target
sem condição afetou referências e falhou; foi corrigido, não é falha runtime.
Próximos: focados, suíte completa, fixtures2clientesrenderizados (caldeira,
blackout/swap, foguete/tiles), shader/material e auditoriaStartup; só depois
marcarC09, commit/publicar64 ambos e confirmarCI. Checklist segue23/33.


C09 checkpoint adicional: Focused3 passou13/13 e Full1 passou1036/1036.
Câmera aprovada agora acompanha transições nativas de sala; bounds de BoxCollider
desativado no cliente são calculados sem reativar colisão. Cobertura de câmera
e wire adicionada. C# final local0erros/0avisos; Python fixtures4/4.
Full2 e buildDevelopment/testes renderizados de2clientes em execução no runner
isolado (sessão30928): boiler, blackout, world-blackout, rocket e dynamite.
Aguardar resultados emLogs/PredictionC09Full2 e PredictionC09Peers-* antes de
marcarC09 ou publicar64. Sem alterações emfontes após cópia ao runner.


Retomada: Full2 passou1037/1037 e Development compilou. Peers1 falhou por
parâmetro de fixture ausente no client; Python agora passa -load-test-mechanisms
nos dois lados e CLI expõe --mechanisms. Peers2/3 interrompidos durante retomada
(nenhum resultado válido). Peers4 apresentou pull/endpoints em2clientes sem
avisos, mas detectou ausência de frames da porta: Unity headless não avança
GetAnimationFrame. Correção local adiciona relógio visual servidor por tile,
Time.time escalado, velocidade declarada (sem sorteioextra), invalidação em
mudanças deasset/célula e limpeza de cena. Offline/renderizador nativo intactos.
Novo teste de avanço/loop/RNG. Full3+Development2+Peers5 emsessão43223; aguardar
resultados antes de concluirC09/publicar64. Editor do usuário não está aberto
na retomada. Packages continuam com hashes preservados. Protocolo64 não publicado.


Full3:1037/1038; único erro foi teste de late snapshot esperando frame do renderer
no servidor headless. Teste ajustado ao relógioaprovado, Final5 passou5/5, incluindo
late snapshot, revisão inválida, porta/RNG e câmera. CompilaçãoC# incremental
Logs/PredictionC09CSharpFinal.log passou0erros/0avisos (restore anterior só6Mirror
legados). FixtureRocket percorre2transições nativas até sala3, com2atores.
BuildDevelopment3 + Peers5 emsessão55871. Development2 teve execução sobreposta
na orquestração e foi descartada; somente Unity do runner foi encerrado, sem
Editor do usuário. Não concluir nem publicar antes de verificar Peers5.


Peers5 caldeira e blackout/swap passaram nos2clientes reais renderizados,
0erros/avisos servidor, owners destravados. Porta comframes intermediários
inspecionados e2683deltas de tile aplicados porclient durante110s naStage2-2;
origem/fim da bomba apresentados. Blackout claro/escuro0.85 confirmado emlogs
e imagem01-16, incluindo retorno. Stage2-2 estabilizou perto60fps/P9516.75ms,
egress servidor total142–148KiB/s para2peers (sem baseline direto; custoR02
continua emaberto). WorldBlackout/rocket/dynamite ainda executando em55871.


Peers5Rocket passou fases em2clients, semerros/avisos; a câmeraaprovada chegou
à sala3 através das2transições. Captura inicial ficou comorigemfora doquadro;
fixture passa aselecionar célula visível, sem alterar alvoRNG nativo. WorldBlackout
Peers5 validou estados/material, mas salaescura fora doquadro; nova fixture entra
pela transição nativa na sala2. BuildDevelopment4+Peers6(world/rocket60s) em20806,
processoPython21308. Somente fixtures mudaram apósDevelopment3 atéentão.
Peers5Dynamite falhou comexplosõesclient=0: SpawnExplosionCrossForEffect não
coletava/publicava Fx deBattle. Correção local usa coletor existente+try/finally
para preservar escopo ancestral e transporta partes/terreno aprovados, comgate
client e servidorativo. Teste nativo deescopo/guard acrescentado à PlayMode.
AguardarPeers6 encerrar antes decopiar fontes; próximo script pronto em
Logs/PredictionC09-dynamite-final.py: Final2 cincoafetados, Development5 e
Peers7-dynamite60s. C#final precisa ser repetido apósBombController. C09aberto.


Peers6WorldBlackout passou2clients após transição nativa: imagens01-05(escuro
comcírculos/partesvisíveis) e01-20(claro) inspecionadas. Peers6Rocket passou
fases/marker/endpoints, GPU mostra subida, porém teve1aviso deEmitAbilityExplosionFx
emMechaBomb.Update apósNetworkServer.Stop. Guarda NetworkServer.active já entrou
na correçãoBombController paraDynamite; repetirRocket nomesmo binário5 para
exigir0avisos. Final2+Development5+Peers7Dynamite em28689/Python22592. Aoencerrar,
Logs/PredictionC09-rocket-final.py repeteRocket60s semrebuild, porta28721.


Final2 passou5/5 comescopo nativo deexplosão/cross eguardclient. C#final passou
0erros/0avisos apósBombController. Development5 emexec28689, seguidode
Peers7Dynamite60s. Exec81488 esperaPython22592 terminar e rodaPeers7Rocket60s
comguardas paraXMLFinal2 aprovado ebuild5 concluída. Não copiarfontes enquanto
estiveremativos. Código/fixture finais jácopiados ao runner naexec28689.
Protocolo64 ainda não publicado; não stagePackages. Aguardarambosresultados,
inspecionarchamas naDinamite e repetircheckbytes/dirty. FecharC09 epublicarambos
apenas depois disso. Os demais cenários aprovados estão emPeers5boiler/blackout
ePeers6world-blackout, comimagens jáinspecionadas.


### C09 implementado e validado — protocolo64 pronto para publicação

Resultados finais: Peers5boiler/blackout, Peers6world-blackout e
Peers7rocket/dynamite passaram com2clientes Mirror reais renderizados.
Todos0erros e0avisos servidor. ImagensURP inspecionadas: abertura intermediária
da porta, blackoutUI escuro/claro, sala2 comcírculos de luz/partesvisíveis,
foguete visível emsubida e dinamite comchamas. Rocket7 teve Rocket2–5/7–10/12/15
e Target0/1 nos2clientes; Dynamo7 teve explosõesclientes>0(5,15...).
O aviso teardown doRocket6 não reapareceu noRocket7 comguarda servidorativo.
As tentativas falhas foram preservadas, sem diminuir critérios do verificador.

C#final0erros/0avisos; Python75CI+15Directory=90 aprovados. Full2 1037/1037;
Full3 1037/1038 (único teste de late snapshot ajustado ao clockheadless), Final5
5/5 e Final2 5/5 após escopo daexplosão. Não declarar Full3 totalmente aprovado.
Nenhuma cena/prefab/texture editada na raiz. Packages preservados e hashes
verificados. Checklist24/33 feitos e9parciais; próximoC10. QA humano de sensação
comoffline e custo comparativoR02 permanecem no roteiroglobal.
Próximo passo autorizado: commit/pushv0.5.0 e Actions ambos, confirmarservidor,
cliente e updater no mesmo commit64; salvar confirmação neste checkpoint.


C09 commit46b1ba1c8b7e110c6bc5a5c6afad91f324b2d505 enviadoav0.5.0.
Actions42152138685/job127677638641 emandamento, alvoambos/quick. Ainda confirmar
buildscliente/Linux, deployservidor64 echannelupdater no mesmo sourceCommit.
Fontes próprias restauradas no runner depois dos testes;4arquivosnovos removidos
somente daquele checkout. Gerados preexistentes preservados. Raiz temsomente
Packages/manifest.json ePackages/packages-lock.json do usuário fora docommit;
hashes originais preservados. Não publicar sóservidor nem alterar essesPackages.
Se interrompido: consultar oActions acima, corrigir falha casoexista eguardar
resultado final aqui. C09 implementado/testado; publicação pendente.


PublicaçãoC09: Actions42152138685 concluiu buildsLinux(179.5s)/Windows(213.7s)
e deployservidor. VerificaçãoSSH direta de~/sb6/server/deployment.json confirmou
sourceCommit46b1ba1c8b7e110c6bc5a5c6afad91f324b2d505, protocol64,
benchmarkHarness=false; evidência emLogs/PredictionC09-live-server.json.
Canalupdater ainda emupload noúltimo check; ponteiro público aindaeraC08.
Não finalizar a publicação antes deconcluirStep8 econfirmarReleaseonline-v0.5.0
comsourceCommit46b1ba1c8. ArquivosBuild/CI de deployment/status copiados aLogs.


### Publicação C09 concluída — Actions42152138685 success

Builds Linux e Windows, deploy do servidor, canal do atualizador e artefatos
concluíram com sucesso. Servidor ativo e build-info do cliente confirmados em
46b1ba1c8b7e110c6bc5a5c6afad91f324b2d505/protocolo64. O ponteiro público da
Release online-v0.5.0 também aponta para esse commit, com asset/tamanho conferidos
pela API. Evidência pública salva emLogs/PredictionC09-public-updater.json;
manifestos/deploy-status copiados aLogs/PredictionC09-*.json.

C09 concluído em implementação/testes/publicação. Checklist24/33 feitos e9parciais;
próximoC10. QA perceptual/offline permanece no roteiroglobal, sempromessa de
inputlagzero. Raiz preserva somente as2alteraçõesPackages do usuário; hashes
originais conferidos apóspublicação. Nenhuma cena/prefab do usuário alterada.


### C10 iniciado em06/11/2026 — protocolo65 local, ainda não publicar

Base9ef356171 (C09 publicado64). Raiz tinha somente2Packages do usuário;
hashes emLogs/PredictionC10-user-packages.json. SemEditor/players ativos no início.
Fontes próprias emLogs/PredictionC10-owned-paths.json. Nenhuma cena/prefab editada.

Auditoria: SuddenDeath usava início/drop sem timestamp e client iniciava
DropTileRoutine nativa, incluindo commit local de terreno ao fim do seu timer.
Novo partial Network transporta snapshots completos com caminho, elapsed de
Time.time, pausado, trajetos/começo/duração de quedas e conjunto de commits reais.
Heartbeat/revisões/buffer32 e scene gate; late observer recupera sem eventos
anteriores. Client anima a curva linear nativa/sombra, espera commit explícito e
não executa limpeza/dano/callbacksGameManager. OnDisable cancela coroutines,
retira reservas pendentes e publica clear. Sombras guardam escala/alpha do lead.
FallingBomb recebe faseblink de gameplayTime aprovado e valida finitos antes
serial; zero sorteio/spawn no client. Teto/powder mantêm transporte43 auditado.
ProtocolosUnity/supervisor65 alinhados. C#local0erros/0avisos apósimpl principal;
repetircheck final apósfixtures. Python6/6 testes de mecanismo passaram.

Três novos testes de SD: deadline semcommit, confirmação+destrutível, wire,
revisões/inputs inválidos, recuperação tardia, copy/cancel, pausa e buffer32.
FallingBomb existente ampliado parafase deblink eNaN. FixturesDevelopment
estendem -load-test-mechanisms: sudden/falling/powder/roof nas arenas15/2/3/8.
Sudden usa timer48 e pausa/resume nativos; nenhumplayer simulado.
Próximos: suíteUnity ebuildDevelopment isolados;4smokes2clientes renderizados,
inspecionarimagens/custos, corrigirfalhas, finalizarC10 sóapós evidências.
Checklist continua24/33 implementados/testados e9parciais; publicação65 pendente.


C10 validação emexec16333: fontes próprias copiadas para runner livre, estado
prévio emLogs/PredictionC10-runner-dirty.txt. FullUnity OnlineRoom+COM em
Logs/PredictionC10Full.xml/.log; se passar geraDevelopment e4Peers60s. Não copiar
fontes nem fechar essaUnity enquantoexecativa. TesteSudden reposiciona2atores
nocentro por APIsnativas antes detimer48 para não morrerem jáno1ºanel; pausa
invocaTogglePause nativo e valida pausa/resume. Fases/commits sãoexigidos emcada
cliente pelo validadorPython. Semcompromisso comsucesso atélerosresultados.


C10 checksC#0erros/0avisos ePython77CI+15Directory=92 aprovados. Apóscópia ao
runner, revisão corrigiu rootNetwork commit de destrutível paraWorldToCell do
centroindestrutível (mapascomoffsets) e ampliouteste3novo paraisso ecurva50%.
Esses2arquivos rootaindaNÃO foramcopiados; aguardarexec16333 encerrar. Smokes
iniciais nasarenas (mapassobrepostos) continuam úteis para caminho normal.
Repetir3ClosingTimelineTests depois desseajuste eC#final; não usarhash dobinário
anteriorcomoprova dooffset. NativeServer/editor dousuário preservados.


C10 Full passou1041/1041. Development compilou;4Peers(sudden/falling/powder/roof)
60s passaram com2clientes reais renderizados,0erros/avisos servidor. Sudden
atravessou1recarga de rodada:104células de drop_finished servidor iguais às104
commit_apply emcada cliente. Pausa capturada5vezes emcada. Imagens conferidas:
queda/sombraSD, FallingBomb alvo+voo, Powderframes1/3/5/8 e restauração, teto
aberto/fechado. Comparação de células agoraobrigatória novalidador final.

Apósajuste deWorldToCell/colisores,4testesFinal passaram4/4, incluindonativo em
PlayMode: disable impedecommitpendente, liberareservas e permitepousooffline
apósreuso. Curva50%, snapshot tardio/RNG/cópia/serial/pausa/buffer32 também
aprovados. Development2+Peers2-sudden emexec37958/Python8904 para validarcódigo
finalexato. Ainda aguardar esse resultado antes demarcarC10/publicar65.
C#final/bytes precisam ser conferidos apósúltimacorreção;92Pythonjáaprovados,
6focados doverificador passaram apóscomparação estrita de células.


### C10 implementado/testado — protocolo65 pronto para publicação

Full1041/1041, Final4/4 (incluiPlayMode disable/reusooffline eoffsets). C#final
0erros/0avisos;77CI+15Directory=92Python e6focados apósvalidador estrito.
Peers iniciais60s deSD/Falling/Powder/Roof aprovados nos2clientes reais comGPU.
Peers2Sudden no código final também passou:104células pousadas de fato no servidor
iguaisàs104aplicadas emcada cliente; pausa/resume e1recarga de rodada,0erros/avisos.
Imagens dequeda/sombraSD, alvo+vooFalling, frames+restorePowder, tetoaberto/fechado
inspecionadas. Fase desconhecida não prevê dano nem rotaRNG; commit nunca vemdo
fimdo timer visual. Recuperação desnapshot não altera política de entrar numa
Battlejáemcurso. Nativecancel também remove reservas antes de reuso.

Custo medido noSD finalperto60fps/P9516.75ms; egress total2clientes75–103KiB/s
emamostras finais. Semcomparativo específico offline/baseline; R02 permanece
pendente. Nenhuma cena/prefab/texture alterada na raiz.2Packages do usuário com
hashes preservados. Checklist25/33 feitos e8parciais; próximoR01, QA humano
perceptual/offline segue global. Próximo passo autorizado épublicar ambos65 e
confirmar Actions, servidorativo eponteiroUpdater no mesmo commit.


C10 commitb391172d18e0303ddbee30e93c1387f26202057b enviadoav0.5.0.
Actions42204673208 emandamento, ambos/quick. Ainda confirmarLinux+Windows,
deploy65 eponteiro públicoUpdater nomesmo sourceCommit. Rootsomente2Packages
usuário fora docommit. Runnerfontes próprias restauradas e4novos removidos
somente daquele checkout apósúltimo smoke. Checkpoint/skill/notas atualizados;
não encerrar publicação65 antes de confirmarstepcliente apósdeployservidor.


### Publicação C10 concluída — Actions42204673208 success

BuildsLinux(149.3s)/Windows(180.4s), deployservidor, canalupdater eartefatos
concluíram comsucesso. Servidorativo verificado porSSH em
b391172d18e0303ddbee30e93c1387f26202057b/protocolo65/benchmarkHarness=false.
Build-info cliente confirmoumesmo commit65. Ponteiro públicoReleaseonline-v0.5.0
também confirmado nesse commit; tamanho doasset eSHA256 conferidos com pacoteCI.
Evidências emLogs/PredictionC10-live-server.json e-public-updater.json,
manifestos/deploy-status copiados aLogs/PredictionC10-*.json.

C10 concluído em implementação/testes/publicação. Checklist25/33 feitos e8parciais;
próximoR01. Suite1041/1041+Final4/4,92Python, cinco execuções2clientes (4cenários
iniciais+SDfinal),0erros/avisos servidor. QA perceptual/offline permaneceglobal.
2Packages do usuário preservados comhashes originais; nenhuma cena/prefab editada
na raiz e nenhum código de fixture deDevelopment nos binários publicados.

### R01 retomado em 06/11/2026 — auditoria apenas, sem código novo

Ambiente desta sessão: shell Linux (VM) sem Unity, dotnet nem o runner
`.sb6-actions-runner`; só leitura/edição de fontes e testes Python são possíveis.
Nada foi compilado, testado em Unity, capturado entre peers nem publicado.
Branch `v0.5.0`, HEAD `b7c77eff3`. `git status` completo NÃO terminou nesta montagem Linux
(timeout); a afirmação anterior de árvore limpa estava errada. Alterações do usuário em
Packages/manifest.json e packages-lock.json devem ser presumidas e preservadas.

Auditado (leitura): NetworkMountAnimation (history64 por tipo/geração, interpolação
de localPosition/pose com limite de offset aprovado, pausa congela), NetworkPlayerAnimation
(history64 de renderer/frame/offset/pose, interpolação só no mesmo renderer, limite 0,25 s),
NetworkVisualPose (pose relativa ao ator, nunca move o root de gameplay) e NetworkPlayerSetup.
Nenhuma divergência comprovada foi encontrada que justifique editar C# sem poder compilar
e capturar owner/observador. Nota: NetworkPlayerAnimation.OnVisualTime não tem a guarda
`NetSync.Mode/NetworkClient.active` que NetworkMountAnimation tem; hipótese a verificar
com prefab offline, não é bug confirmado.

R01 segue parcial (36). Pendências: capturas owner/observador entre peers, custo CPU/bytes,
validação de transições/papéis (troca de montaria/geração, morte, emote/AFK, pausa, rodada).
Próxima ação: em máquina com Unity 6000.6.0f1, estender `LocalServerLoadTest.cs` para player
remoto, gerar build Development e rodar `smoke_full_clients.py --rendered --mount-visuals`
(receita na seção 9 do handoff), comparando com `analyze_prediction.py`.
Publicação: não iniciada.

Análise OnVisualTime (estática, não executada): é hook de SyncVar de NetworkPlayerAnimation,
chamado só ao desserializar estado de rede em cliente, e retorna cedo no servidor e no owner sem
apresentação de autoridade. LateUpdate também sai sem NetSync.IsOnline. Offline não há
desserialização, logo o hook não roda; guarda por simetria com NetworkMountAnimation seria
desnecessária e pode desativar animações válidas. Hipótese refutada por leitura; confirmar
só se um log online/offline mostrar alteração indevida de pose.

Preparação R01 (06/11) — NADA compilado nem executado em Unity/Windows; publicação não iniciada.
Estado do checkout: branch v0.5.0, HEAD b7c77eff3. `git diff --name-only` confirma alterações do
USUÁRIO em Packages/manifest.json e Packages/packages-lock.json (preservadas, não tocadas).
Arquivos próprios (tracked): LocalServerLoadTest.cs, NetworkMountAnimation.cs,
NetworkPlayerAnimation.cs, Tools/RoomDirectory/smoke_full_clients.py e estes dois docs.
Novos (untracked, sem .meta pois não são assets Unity): Tools/Diagnostics/analyze_remote_player.py,
Tools/Ci/test_remote_player_analysis.py, Logs/R01-validate-assisted.py (Logs não é versionado).

C# (compilação PENDENTE; revisão estática apenas):
- NetworkMountAnimation.TraceVisual e NetworkPlayerAnimation.TracePlayerVisual passam a registrar
  `rev=` (frameRevision) e `ptime=` (AuthorityPresentationTime ou NetworkTime.time, InvariantCulture)
  no detail. Só log; sem mudança de wire/protocolo (65 mantido). Validadores existentes leem o
  detail por chave, então campos extras não os afetam.
- LocalNetworkInputSmoke ganhou `-load-test-remote-walk` (DriveRemoteWalk/ReleaseRemoteWalk): ciclo
  de 14 s (direita, esquerda, cima, baixo, com paradas) no slot de hardware 1, só do próprio
  processo, só após CanPredictNetworkMovement e fora da pausa; loga `[LoadTest] remote-walk started`.
  smoke_full_clients.py ganhou `--remote-walk` (exige BattleMode + diagnostics, sem outras
  fixtures, sem --rendered) e falha se um cliente nunca iniciar a caminhada.
Motivo: nas capturas antigas 26 de 42 cenários são inconclusivos (jogadores parados); mount-cycles
anda ~0,35 s por ciclo e Sudden Death não anda.

Analisador (Python, 15 testes sintéticos passando; 92 testes Tools/Ci executados, 15 pulados, só na
VM Linux): atraso esperado = timeline-ptime (ou bufferMs); atraso constante perto disso NÃO é falha;
falhas = atraso acima do esperado+150 ms, deriva entre janelas de 3 s >150 ms, travamento (>=0,5 s
parado enquanto o servidor se move, ignorando as bordas), descontinuidade, RMS>0,35, pose ausente ou
inexistente no servidor, geração de montaria inexistente no servidor, offset p95>0,5. Arcos de
montar/Pink são excluídos. Sem movimento suficiente => inconclusivo, nunca "0 ms". Limites provisórios.
Em ~50 capturas antigas: atrasos medidos 20–100 ms ~ bufferMs 33–52 ms. Pistas, não conclusões:
VisualRepair58Peers2/4 (build antiga) em que o observador nunca mostrou MountAscendLeft/Right; casos
de cart/portal com descontinuidade (arcos de outros presenters).

Script (Logs/R01-validate-assisted.py): bloqueia (exit 3) se fontes (Assets+ProjectSettings) do runner
divergirem do checkout, Packages divergirem (sem --accept-packages-diff), runner ocupado ou RAM/disco/
portas insuficientes. Resultados separados: falha de execução (1), falha de medição (2), inconclusivo
(4, não é aprovação); 0 = dentro dos limites provisórios, ainda sem aceitar o R01. Casos: locomotion
(novo, sem render), mount-visuals, regression-sudden, mount-cycles (regressões complementares).
Comando: `python Logs/R01-validate-assisted.py --step preflight`, depois `--step all`; devolver a pasta
`Logs/R01-assisted-<data>` inteira. Resultado esperado: o primeiro erro provável é de compilação do
driver C# (falha de execução, não de medição). Próxima ação após os resultados: diagnosticar e
corrigir; R01 segue parcial.

## Preparação R01 (06/11, parte 2) — NADA compilado nem executado no Windows

- `Logs/R01-validate-assisted.py` agora aceita `--resume <pasta|latest>`: preflight, tests, build e smoke ficam na mesma pasta `Logs/R01-assisted-<data>`; o ponteiro fica em `Logs/R01-latest.txt` e `result.json` guarda `invocations` (um registro por comando). Sem `--resume`, cada comando cria pasta nova; o vínculo é `Build/ServerBenchmark/r01-build-identity.json` no runner (sha256 do SB6.exe e digest das fontes) mais os digests de cada `result.json`. Antes de cada smoke o binário é reverificado.
- Divergência de fontes: o preflight bloqueia e gera `sync-plan.txt`, `sync-to-runner.ps1` (dry-run; `-Apply` copia só arquivos próprios e seus `.meta`, com backup em `runner-backup/`; `-IncludeOther` explícito) e `restore-runner.ps1`. Nunca apaga, não toca em `Packages`. O script Python não copia nada. Testado só em Linux com pastas temporárias.
- Novo caso `locomotion-rendered` (porta 29023): `--remote-walk` com clientes renderizados, salvando `walk-NN-<UTC>.png` do observador a cada 0,5 s para conferir root, pose e montaria. `locomotion` (sem renderização) segue como medição automatizada; logs `player_visual_sample` headless existem em capturas antigas, mas movimento do root sob `-nographics` só será confirmado na primeira execução Windows.
- R01 continua **parcial**. C# novo (`DriveRemoteWalk`, captura) não foi compilado.

## Preparação R01 (06/11, parte 3) — MCP restrito e sincronização (nada executado no Windows)

- `Tools/R01Mcp/` (não rastreado): servidor MCP stdio com operações fixas (start/job/cancel/results/read), um job por vez, sem shell, sem Packages, sem `--accept-packages-diff`. Instalação por `install.ps1` (simulação por padrão; `-Apply` faz backup do config e mexe só em `mcpServers.sb6-r01` via `merge_config.py`). Testado só em Linux (protocolo, trava, cancelamento sem órfãos por grupo de processos). Job Object do Windows e exposição do servidor a esta sessão: NÃO verificados. A documentação oficial diz que servidores MCP locais não rodam em sessões na nuvem; confirmar após instalar.
- Preflight de 12:40 (usuário) não comparava fontes; corrigido. Runner em HEAD `b391172` contra `b7c77eff3`: HEAD sozinho não prova divergência; vale o manifesto de `Assets/`+`ProjectSettings/`.
- Diferenças agora classificadas: próprias (copiadas por `-Apply`), atrasadas com cópia intacta (só com `-IncludeBehind`), alteradas pelo próprio runner (nunca copiadas; seguir só com `--accept-runner-local-diffs`, registrado como ressalva).

## MCP sb6-r01 — verificação em 06/11 12:58 (local): NÃO visível nesta sessão
- `.sb6-r01-mcp` existe no usuário e `Logs/r01-mcp/server.log` mostra 3 inicializações do servidor às 12:54 (provavelmente autotestes do instalador e/ou do app; não distingue). Nenhuma chamada de ferramenta registrada.
- `get_device_info.localMcpServers` segue `[]`; `RefreshMcpTools` não listou `sb6-r01` (servidor `remote-devices` com 54 ferramentas, sem acréscimo); ToolSearch não achou `r01_start`.
- Conclusão: INCONCLUSIVO, não é prova de incompatibilidade. A sessão pode ter sido criada antes do reinício do app; a documentação oficial também diz que MCP local não roda em sessões na nuvem. Próximo passo: abrir sessão NOVA e repetir a checagem. Preflight não executado por esse caminho.

## Preflight via computer use (06/11 13:12 local) — BLOQUEADO por fontes (esperado), nada copiado
- Caminho usado: computer use (Explorador de Arquivos, nível click) com duplo clique em `Logs/R01-run-preflight.cmd` (fixo: só `--step preflight`). Terminais não aceitam digitação por computer use; só arquivos preparados. O app Claude não foi concedível, então Configurações → Desenvolvedor não foi inspecionado. MCP `sb6-r01` ainda não verificado em sessão nova.
- 1ª execução (13:04) acusou 8604 diferenças; amostra de 400: 399 eram só quebra de linha (runner CRLF, repo LF). Manifesto passou a normalizar CRLF→LF em texto (`norm_sha`; arquivos com NUL ficam crus), inclusive para `Packages`.
- 2ª execução (pasta `R01-assisted-20261106-131233`): diferenças reais = próprias 3 (`LocalServerLoadTest.cs`, `NetworkMountAnimation.cs`, `NetworkPlayerAnimation.cs`); atrasadas 2 (`Assets/Resources/OnlineBuildIdentity.txt` e `.meta`); alteradas pelo runner 1 (`Assets/Resources/OnlineBattleAudio.asset`). `Packages/manifest.json` e `packages-lock.json` também diferem (hash normalizado). RAM livre 4,9 GiB. `acceptRunnerLocalDiffs=false`.
- Pendente de decisão do usuário: copiar as 3 próprias (reversível); revisar OnlineBuildIdentity e OnlineBattleAudio.asset; decidir sobre Packages. Tests/build/smoke NÃO iniciados.

## Sincronização temporária do runner e preflight (06/11 13:23 local)
- Seguido `Docs/multiplayer/revisao-runner-r01.md`. Script `Logs/R01-runner-sync.ps1` (modos Plan/Apply/Restore; wrappers `R01-sync-*.cmd`), executado por computer use. Apply copiou repo -> runner, só no runner, com backup em `Logs/R01-runner-sync/backup-20261106-132330` e `state.json` (hashes original/instalado): 3 fontes C#, `OnlineBattleAudio.asset`, `OnlineBuildIdentity.txt` + `.meta` (par tratado junto; GUID do runner d8252331… sem outras referências), `Packages/manifest.json` e `packages-lock.json`. Nada no checkout original foi alterado; sem reset/clean.
- Restore só roda com Unity/SB6/validação/Actions parados e só repõe arquivo que ainda tem o conteúdo instalado; senão reporta "changed since sync" (ou `-Force`, salvando antes a cópia atual). Packages podem ser regravados pelo Unity durante o teste; esperar conflito e tratar no restore.
- Efeitos a registrar: Unity pode importar/baixar pacotes no runner (Library do runner não é revertida). OnlineBuildIdentity do runner passa a ser o do checkout original (não prova as fontes dirty; a identidade do teste é o manifesto + assemblies). Áudio igualado ao original: cobertura de áudio do runner anterior não vale.
- Preflight (`R01-assisted-20261106-132339`): exit 0, fontes idênticas, Packages iguais. NADA medido; R01 segue parcial. Próximo: tests, depois build, depois smoke (um por vez, sem publicar).

## R01 — execução assistida: testes, build e smokes (06/11 15:00 local)
Pasta de resultados: `Logs/R01-assisted-20261106-132339` (smokes/analyses anteriores arquivados em `previous-smokes-1`, `previous-analyses-1`; imagens em `evidence/`).
- Testes EditMode (filtro `OnlineRoomEditModeTests;BattleModeComEditorTests`): 1041/1042. A falha é o catálogo de áudio (asset original sem entradas que o Unity adiciona), preexistente e alheia ao R01.
- Build Development (runner sincronizado, Packages iguais): sucesso; identidade em `binary-identity.json` (hash da Assembly-CSharp.dll). Duas intercorrências minhas: (1) um duplo clique acidental reexecutou o build-accept (falhou sem alterar a identidade); (2) o checador de "binário novo" olhava só SB6.exe, que o Unity não religa quando só scripts mudam; corrigido para exigir log "Succeeded" + DLL mais nova que os scripts do runner.
- Correções feitas após os primeiros smokes (todas de diagnóstico, nenhuma muda o jogo): `NetworkPlayerAnimation.LateUpdate` passa a chamar `TracePlayerVisual()` também durante arco de montaria/toupeira/barco (antes o observador não logava pose nesses trechos; agora há MountAscend/Descend nos logs do observador); analisador ignora linhas do observador após o fim dos dados do servidor, aceita revisões `frameRevision` intermediárias (contador único por player, bumps sem linha no servidor), ignora estados de uma única amostra (aliasing a 10 Hz) e compara o offset da montaria com qualquer offset publicado pelo servidor na tolerância (o offset espelha com a direção); `mount-visuals` passou a 150 s. Testes sintéticos: 19 passam.
- Resultado dos 5 smokes (binário identificado, análise pelo analisador corrigido): locomotion headless e renderizado e mount-visuals: dentro dos limites provisórios (lag de raiz constante 40–60 ms ≈ atraso de interpolação, 0 stalls, 0 descontinuidades, RMS 0,01–0,11, p95 ≤0,27; atraso de pose mediano 34–83 ms). mount-cycles e regression-sudden: sem falhas, mas players parados, logo raiz não mensurável (registrado como "não aplicável", não como evidência de locomotion).
- Imagens do locomotion renderizado (`evidence/*-sheet.png`, frames walk-NN): partida renderiza normalmente com mounts, bombas e explosões; não vi corrupção óbvia. Há silhuetas pretas perto de um COM em dois frames (provável sprite de efeito/morte); não consegui julgar. Julgamento perceptual (suavidade, alinhamento de pose/mount, GPU real) fica como QA manual PENDENTE, separado.
- Runner restaurado com `R01-sync-Restore.cmd` (todos os arquivos voltaram aos hashes originais; state arquivado no backup). Checkout do usuário intocado; nada publicado. Library do runner não foi revertida.
- Estado: R01 segue PARCIAL. Limites são provisórios (sem baseline), há apenas uma execução por smoke e falta o QA manual perceptual. Não marcado como concluído.


## R01 - investigacao adicional antes de fechar (06/11, 15:30 local) - R01 CONTINUA PARCIAL, SEM PUBLICACAO

### Medido
- Catalogo de audio (teste unico no runner): baseline equivalente SEM as alteracoes R01 e sem trocar o asset PASSOU; com o asset do repositorio trocado temporariamente FALHOU de forma identica. Atribuicao: a falha nao vem do codigo R01; e latente do asset commitado quando o runner nao o reserializou (Logs/R01-baseline-*/). Causa exata no asset segue nao analisada.
- Analisador reescrito: comparacao por entidade, geracao de montaria e tempo de apresentacao (ptime), nao mais por offset publicado em qualquer instante. Amostras de um quadro, poses ambiguas e revisoes intermediarias sao contadas como "inconclusivas" (coverageNotes/candidates), nunca descartadas em silencio. 24 testes do analisador passam.
- Repeticao 3 (build com trace do servidor a 20 Hz, dt= por quadro, limite de log 64 MiB; nenhum capture_limit): 
  - locomotion rendered: dentro dos limites provisorios (atraso de apresentacao ~38 ms, erro de posicao no mesmo ptime p95 0,39, 0 divergencias estritas).
  - locomotion headless: FALHA de medicao (posicao no mesmo ptime p95 0,52 > 0,5; deriva de lag de 260-280 ms em uma janela, na mesma janela nos 3 observadores). Nao tratar como aprovado.
  - mount-visuals, mount-cycles, regression-sudden: INCONCLUSIVOS (jogador quase parado, raiz sem lag identificavel); 0 divergencias estritas de pose/montaria; revisoes intermediarias de montaria (mount-cycles: 2, 4, 8) listadas.
- Causa provavel dos "buracos" no log do servidor, das derivas e dos degraus grandes: travadas reais de quadro do SERVIDOR (dt medido). Ex.: locomotion dt=610 ms (ptime 11,6), 281 ms (18,1) e 4050 ms (35,0 - explica o degrau de 2,76 u); rendered 655 ms e 275 ms; demais smokes 190-925 ms perto de 9-10 s (inicio de rodada). O servidor headless divide a maquina com 2 clientes. Isso e correlacao forte, nao prova de que o p95 de 0,52 seja so isso.
- Silhuetas pretas: identificadas como TemporarySpeedBlink (MovementController, tint preto/branco a cada 0,1 s) disparado por caveira/velocidade temporaria; comportamento esperado. Evidencia em evidence/black-silhouette-crops.png. Fase do blink entre clientes nao verificada.

### Descartado
- Falha de audio como regressao do R01 (baseline passa sem R01, falha com o asset do repositorio igual em ambos).
- Truncamento de logs por limite de 8 MiB (corrigido: 64 MiB).
- Tolerancia por offset em qualquer momento (substituida por ptime/geracao).

### Candidatos abertos (nao resolvidos)
- Recuo do relogio de apresentacao (ptime) de 0,1-0,23 s em todos os smokes; efeito visual nao provado (pode coincidir com as travadas do servidor).
- Estado de um quadro so no observador (Down,MountAscendLeft; antes: Mole body=Down) que o servidor nunca registrou.
- Degraus grandes sem referencia densa do servidor (parcialmente explicados pelas travadas).
- Reexecutar locomotion headless para ver se o p95 > 0,5 reproduz fora das travadas.

### Sem cobertura
Qualidade perceptiva/GPU; comparacao offline x online; morte montado, pausa, troca de rodada; custo de CPU/bytes; correlacao por tick; sincronia da fase do blink da caveira; visibilidade do recuo de ptime.

### QA manual pendente (2 clientes, ~10 min, build Development com -online-motion-logs, gravar a tela)
1. Locomocao: A anda em quadrado e diagonais por 20 s; em B, procurar engasgos ao virar/parar.
2. Pose: A vira nas 4 direcoes, coloca bomba e para; em B, procurar piscadas de pose.
3. Montarias: A monta Tank/Mole/Louie e anda em todas as direcoes; em B, offset do corpo acompanha a direcao, arcos de subir/descer, e atencao ao flash de corpo inteiro em Mole so-cabeca (inversoes Cima/Baixo).
4. Ciclos: A monta/desmonta 5x rapido; B nao pode mostrar o quadro da montaria anterior apos desmontar.
5. Caveira: A pega caveira; B ve piscar preto ~0,1 s, sem ficar preso no preto, terminando junto.
6. Nao coberto: pausa, troca de rodada, morte montado.

## Rodada 06/11/2026 noite - hitches, ptime, quadro do Mole, locomotion (R01 segue PARCIAL, sem publicacao)

Condicoes: CPU antiga nas medicoes principais; a maquina trocou para Ryzen 5 5600GT (16 GB) no fim da rodada. Tempos valem para a CPU antiga; repetir locomotion na nova.

Medido
- Lacunas no log do servidor vinham do orcamento de 180 linhas/s da captura (289 de 291 amostras esparsas eram logDropped), nao de travadas. Com `-online-motion-log-budget 600` (so densidade de captura; limiares do analisador intactos): locomotion_n2 e locomotion-rendered_n3 dentro dos limites provisorios; locomotion-rendered_n2 e locomotion_n3 inconclusivos (degraus em lacunas reais do servidor, janela de lag 160 ms nao corroborada por ptime). A "falha" headless anterior (p95 0,52) era artefato de referencia esparsa. Nenhuma janela descartada: todas listadas acima.
- Travadas reais do servidor existem sem captura (plain-nocapture_n1: 7 apos 15 s, max 338 ms, fase update; carga de cena ate 1,9 s). Custo de captura pequeno: formatacao 2-7 ms/s, IO 0,4-1,1 ms/s. mount-visuals_n3: 7 travadas no servidor (5 coincidem com GC na fase early, max 775 ms na carga de cena, 1 fixed 161 ms, 1 preLate), 160 coletas. GC coincide mas causalidade nao provada. Clientes renderizados: 47-61 travadas (max ~385 ms), pois 2 clientes + servidor dividem a maquina.
- ptime recua por TimelineClamp/reset do Mirror (passos de -20 a -200 ms; -1,2 s no reset). 9 de 21 recuos seguiram travada >=100 ms do servidor; 12 sem causa isolada (rtt mediano 37 ms). O recuo chega a posicao apresentada: 4 recuos em movimento deram degraus de 0,3-0,4 u nos renderizados (0 de 3 no headless n3). Visibilidade perceptiva nao julgada. Relogio monotonico NAO imposto. Pausa (~1 s) nao gerou salto de ptime.
- Quadro de corpo inteiro na montaria: reproduzido de novo em mount-visuals_n3 (56/51 mount_body_edge nos clientes) e ausente do log denso do servidor, portanto nao e amostragem. Observador mostra corpo base Down por 1 frame (~16 ms), as vezes Left/MountDescendRight ate ~67 ms antes de MoutedRigth/HeadOnly*; root e rev inalterados; player_renderer_edge nao mostra. Sem correcao tentada.
- Novo: classe "borderline" no analisador (degrau 90.68 e subleitura do pico pelo servidor) = inconclusivo, nunca falha. 27 testes passam.

Nao concluido / pendente
- round-reload (200 s) FALHOU na orquestracao: "missing server checkpoint: [LoadTest] completed" (smoke-round-reload_n2/failure.json); troca de rodada continua sem cobertura automatizada. Nao e resultado.
- mount-visuals_n3: inconclusivo (servidor moveu 0,00 u nas janelas dos clientes; lag nao identificavel). Morte montado: observador mostra body vazio ~43 ms e depois Down; nao analisado em profundidade.
- Fora do escopo: GPU/percepcao, offline x online, custo CPU/bytes, correlacao por tick, pausa com montados.

Manual (usuario): o flash de 16-67 ms e visivel? Os degraus de 0,3-0,4 u sao visiveis? Fase do blink (TemporarySpeedBlink e comportamento esperado).

Ferramentas: FrameHitchProbe (`-online-hitch-probe`, `-online-hitch-ms`), Tools/Diagnostics/hitch_report.py, `-online-motion-log-budget N`, `-online-motion-logs-off`, casos plain-nocapture/plain-capture/round-reload no orquestrador. Runner restaurado (11 arquivos; FrameHitchProbe removido); checkout do usuario intocado.

## Falha separada: catalogo de audio (OnlineBattleAudio)
Linha de base sem alteracoes do R01 passa; com o asset do repo copiado para o runner falha de forma identica. Causa no asset nao analisada. Nao relacionada ao R01.

## Rodada 06/11/2026 22:00-23:30 - correcoes do flash de corpo e dos passos para tras, round-reload e baseline na CPU nova (R01 segue PARCIAL, sem publicacao)

Hardware: runs com tag `_n*` e `_h*` antigos = CPU antiga; `_g*`, `_k*`, `_m*`, `_p*` = CPU nova (Ryzen 5 5600GT, 16 GB, GTX 1660). Tempos nao sao comparados diretamente entre as duas.

Causas encontradas e correcoes (codigo de jogo, so apresentacao em observadores; sem mudar geracao, HeadOnly ou modo offline)
- Flash do corpo base (Down/Left por 1-4 frames) na montagem: dois mecanismos. (1) No fim do arco de montar/desmontar, `NetworkBombFx.ClearRidingPresentation` -> `PlayerRidingController.StopNetworkArcVisual` chama `EnableExclusiveFromState()` DEPOIS do `LateUpdate` do `NetworkPlayerAnimation`, entao o corpo a pe chegava a tela por 1 frame ate o proximo `ClientApply`. (2) Mole/Tank: o mount e o corpo vem de dois streams bufferizados e o mount podia aparecer ~1 frame (16 ms) antes do corpo autoritativo (antes: ate ~67 ms).
- Correcao: `NetworkPlayerAnimation.EnforceAuthorityPose()` (reaplica a pose autoritativa no fim do frame, chamado por `NetworkMountAnimation.LateUpdate`, order 1000; mesmas guardas do LateUpdate: arco de montar, MoleStartup/barco do dono, predicao local, pausa, fim de fase) e `NetworkMountAnimation.HideMountBeforeAuthorityBody()` (esconde o mount no frame em que o corpo autoritativo ainda e o a pe; nao age no dono que prediz, em movimento de habilidade, MoleStartup, morto, pausa). `MovementController.IsPlainFootBodyVisible()` e o predicado. Interruptores de A/B: `-online-no-pose-enforce`, `-online-no-mount-gate`. Linhas novas: `player_pose_enforced`, `mount_gated_by_body`.
- Medicao (mount-visuals, CPU nova; quadros finais com corpo a pe + mount visivel, 2 clientes): tudo desligado 8 (m2; antigo n3: 9) -> so reaplicacao 3 (k2) -> + gate observador 2 (k1/m1) -> + gate no dono nao-predito 1 (p1, Mole no proprio dono) e 0 (p2). Residual: 1 frame (~16 ms) no dono durante MoleStartup, caminho local que nao foi alterado; aparece em 1 de 2 execucoes.
- Tentativa descartada: adiar a aplicacao do `mountType` ate o ptime alcancar o carimbo do servidor (A/B h1 vs h2: 14 vs 13 eventos, sem efeito) - revertida; `NetworkPlayerState.cs` identico ao HEAD.
- Passos para tras do timeline: o relogio do Mirror (`NetworkTime.time`) NAO foi tocado. `NetworkPlayerAnimation.PresentSlewedPosition()` mantem, so para a posicao de jogadores remotos, um tempo de apresentacao que nunca volta: apos um recuo avanca a meia taxa ate o relogio do Mirror alcancar (gap maximo 0,25 s). Cortes legitimos nao sao suavizados: recuo > 0,25 s, lacuna de entrega > 0,25 s entre os snapshots amostrados, salto do tamanho de teleporte (1,5 tile), pausa, habilidade/arco/lancamento/carrinho dono da raiz, morto/fim de fase. `-online-no-timeline-slew` desliga. Linhas: `timeline_slew_begin/end` (gap, duracao, motivo).
- Medicao (CPU nova): locomotion-rendered k1 (com) teve 40 recuos de ptime (28 em movimento, ate 216 ms) e 0 passos para tras > 0,1 u (30 janelas de suavizacao no headless, 70 no renderizado, 15 no round-reload); k2 (sem) teve 5 recuos (4 em movimento) e 0 > 0,1 u; h2 sem suavizador: 1 passo de 0,13 u e 1 descontinuidade de raiz. Na CPU antiga (sem suavizador): 0,32-0,38 u (n2/n3). A CPU nova quase nao reproduz o problema, entao a evidencia de beneficio e indireta (muitos recuos grandes sem passo visivel), nao um A/B limpo. Visibilidade perceptiva continua sendo QA manual.
- round-reload: causa = harness. O cliente tem duracao padrao de 180 s (`-load-test-duration`) e saiu antes do servidor de 200 s, que nunca imprimiu `[LoadTest] completed`; nao era timeout nem queda do jogo. `smoke_full_clients.py` agora passa `-load-test-duration seconds+40` ao cliente. round-reload_k1: exit 0 (troca de rodada exercitada, 15 janelas de suavizacao, 0 recuos).
- mount-cycles: uma execucao falhou a validacao do dono ("Dismount: predicted=4, confirmed=3", k1); repetida 3x (m1, m2 com tudo desligado, m3, p1) sem falha - flutuacao, nao regressao. Fica registrado.
- Baseline na CPU nova: locomotion headless k1 dentro dos limites provisorios (exit 0); renderizados k1/k2 e demais cenarios inconclusivos (degraus em lacunas reais do servidor/"borderline"; cenarios parados nao medem locomotion). Hitches (hitch_report): servidor 6-9 (max 1,0 s no carregamento de cena; GC 4-5, update 2-4), clientes renderizados ~75 (fase preLate = renderizacao). Captura: formatacao 4,6-5,3 ms/s na CPU nova vs 7,6-8,0 na antiga. Nenhuma janela descartada; os valores inconclusivos estao nos JSON `analysis-*`.

Nao coberto / pendente
- Pausa com jogadores montados e morte montado em profundidade: fixtures rodaram (regression-sudden k1/p1, mount-visuals) mas ficaram inconclusivos para locomotion; nenhuma discontinuidade nova foi vista, nao ha criterio automatico dedicado.
- Percepcao (GPU/olho), offline vs online, custo de CPU/bytes e correlacao por tick continuam fora.
- QA visual (usuario): os frames existentes `smoke-mount-visuals_p1/client-N-frames/arc-*.png` (arcos Blue/Pink) e `visual-*-phase.png`; para o Mole/Tank, as linhas `mount_body_edge`/`player_pose_enforced`/`mount_gated_by_body` com frame e ptime em `client-N-motion.jsonl` (ex.: client-1 frame 2414, client-2 frame ~4034) marcam o quadro exato; nao ha captura quadro a quadro dessas transicoes.


## Rodada 07/11/2026 00:00-01:30 — relógio único de apresentação, gate do Mole no dono, bordas da fila de input (R01 segue PARCIAL, sem publicação)

Hardware: só CPU nova (Ryzen 5 5600GT, 16 GB). Tags desta rodada: `_s*`/`_r*` (rodada 3), `_m*`/`_n*` (rodada 4); nenhuma comparação com a CPU antiga. Pasta: `Logs/R01-assisted-20261107-002305`. Protocolo 65 inalterado; sem mudança de wire.

**Correção do registro anterior:** o `PresentSlewedPosition()` da rodada de 06/11 nunca agiu — `if (slewPresented >= mirror) EndSlew("caught_up")` encerrava toda retenção no mesmo frame (todas as linhas `timeline_slew_end` tinham `duration_ms=0`). O A/B "com/sem suavizador" daquela seção não é válido e "0 passos > 0,1 u com suavizador" não demonstra benefício.

Correções (código de jogo, só apresentação online; offline intocado)
- `NetworkPlayerAnimation.RemotePresentationClock`: um único tempo de apresentação para observadores; raiz, pose e mount amostram o mesmo instante (`NetworkPlayerInput.AuthorityPresentationTime` → `ObserverPresentationTime`). Recuo do relógio do Mirror ≤ 0,25 s é retido e reencontra o Mirror a meia taxa (retenção máx. 1 s); cortes legítimos seguem o Mirror (lacuna de entrega > 0,25 s, segmento > 1,5 tile, pausa, habilidade/lançamento/carrinho, morto/fim de fase). `NetworkTime.time` não é alterado.
- `NetworkMountAnimation.HideMountBeforeAuthorityBody` agora vale também para o dono que prediz (o flash "corpo Left + Mole" de 1 frame no dono, p1 frame 2414, era esse caso).
- `NetworkPlayerInput`: a fila de input do servidor (8 ticks) guardava só o frame; um estouro após travada descartava o frame com Select e o desmonte rodava num frame só "held" com `CurrentSelectTick` antigo (647 em vez de 839). O cliente não casava o tick e registrava `new_sequence` ("Dismount predicted=4 confirmed=3"). `EnqueueBounded` move os bits pressionados do frame descartado para o próximo, preservando o tick original da borda.
- Interruptores A/B (`-online-no-timeline-slew`, `-online-no-pose-enforce`, `-online-no-mount-gate`) só em build de desenvolvimento (`Debug.isDebugBuild`, avaliação preguiçosa; a leitura em inicializador estático causava 33 falhas de teste).
- `OnlineBattleAudio.asset`: adicionadas as 2 entradas que o gerador inclui (`player_leave`/`player_join`); o teste do catálogo de áudio passa.
- `FrameHitchProbe`: removidos 2 campos privados não lidos (CS0414).
- Harness: `-load-test-server-stall-ms/-every` (travadas determinísticas do servidor, só `-load-test-server` em Development) e captura PNG de 8 quadros a cada troca de montaria (`mountchange-*.png`).

Métrica de passo para trás (nova, independente do relógio)
- Linhas `root_backstep`/`root_motion_summary` por observador. A 1ª versão (contra o segmento amostrado) e a 2ª (janela de 1 frame) marcavam saltos PARA FRENTE através de reversões reais do servidor (ex.: locomotion_s2 p3 -6,29 → -6,009 em 1 frame, depois virando à esquerda; round-reload p6 com Louie em dash -3,85 → -3,13 enquanto o servidor revertia em -3,25).
- Versão final (3ª): a referência é o caminho autoritativo entre os dois tempos apresentados; se o tempo recuou, é o movimento à frente terminando no tempo apresentado. Isto corrige falsos positivos com evidência; o limite de 0,1 u não mudou.
- **A 3ª versão compilou (dotnet, 0 erros/0 avisos), mas não foi executada em smoke** (a rodada 5 foi interrompida; ver abaixo).

Resultados reais
- Testes EditMode no runner: 1047/1047 (0 ignorados), incluindo 5 novos do R01 (relógio: retém / corte legítimo / limite; fila: borda+tick preservados; gate do dono). A primeira execução teve 33 falhas (inicializador estático), corrigidas.
- Python: Tools/Ci 107/107, Tools/RoomDirectory 15/15.
- dotnet build do Assembly-CSharp: 0 erros, 0 avisos.
- mount-cycles: Dismount predicted=4 / confirmed=4 com travada (s1) e sem (r1); Mount 3/3. Exit 4 só porque o cenário parado não mede atraso de locomotion.
- mount-visuals_r1: 0 quadros com corpo a pé + mount visível no dono e no observador (antes: 1 no dono em MoleStartup). 27 trocas capturadas quadro a quadro (`client-N-frames/mountchange-*.png`), revisadas por amostragem (Mole no dono, Tank no observador), sem combinação inválida.
- regression-sudden_r1: fases e apresentação aprovada presentes; inconclusivo para locomotion (parado).
- Travadas de 180 ms a cada 3 s (métrica 2ª versão), somando 8 observadores em movimento: relógio ligado (m1) 17 passos > 0,1 u; desligado (m2) 29; renderizado ligado (m1) 38. Uma execução por lado, sem significância estatística.
  - Nenhum passo marcado ocorreu durante retenção (`stepsWhileHolding=0` em todas); as retenções terminam quase sempre em `legitimate_cut`.
  - Caso examinado (locomotion_m1, client-1, t=81,254): o observador mostrou y=1,829, além do mínimo do log do servidor (2,25), logo após a travada, onde o log do servidor tem lacuna de 285 ms. É compatível com o próprio servidor dando um passo grande (dt da travada) e revertendo; não é verificável pelo log.
- Sem travadas: locomotion_r1 teve 1 passo (0,109 u); locomotion_n1 teve 0 (exit 4: inconclusivo por movimento); round-reload_n1 teve 7 passos, só no p6 montado (dash do Louie, falsos positivos da 2ª versão, ver acima).
- Recuos do relógio do Mirror: sem travada, 0-1 por execução (≤ 41 ms); com travada, 2-5 (até 295 ms).

Interrupção e estado do runner
- A rodada 5 (build + 4 smokes para validar a 3ª versão da métrica) foi encerrada pelo Claude Code por memória baixa do sistema durante o build do Unity. Não foi reiniciada.
- O runner tem um `Temp/UnityLockfile` órfão do build morto (nenhum processo Unity rodando); a Library do runner pode ter compilação parcial.
- Runner restaurado (`R01-runner-sync.ps1 -Mode Restore`): 15 arquivos voltaram aos hashes originais e 2 novos foram removidos, sem conflitos; restam só as modificações locais preexistentes do runner.
- Checkout do usuário: Packages intocados; nada publicado nem enviado.

Status do R01 por critério
- Pose/posição/mount no mesmo tempo: implementado e testado (unidade); sem regressão nos smokes.
- Sem corpo a pé + mount: atendido nos smokes (0 no dono e no observador, 1 execução); QA perceptual pendente.
- Desmonte confirmado sob travada: atendido (4/4 em 2 execuções); mais repetições recomendadas.
- Passos para trás visíveis: NÃO validado. A métrica final não foi executada; sob travadas sintéticas ainda há passos marcados fora de retenção (provavelmente movimento autoritativo); o benefício do relógio não foi demonstrado estatisticamente.
- Pausa/morte montado, custo CPU/bytes, offline × online: não cobertos.
- QA humano perceptual: separado e pendente.

Próximo (sem avançar para o R02): com RAM livre ≥ 3 GiB, reaplicar a sincronização (`R01-runner-sync.ps1 -Mode Apply`) e reexecutar `Logs/R01-round5-chain.ps1` para medir a 3ª versão da métrica (q1/q2 com travada, renderizado, round-reload). Repetir cada lado ≥ 3 vezes antes de concluir sobre o relógio; restaurar o runner depois.

## Rodada 07/11/2026 06:00-08:30 — A/B controlado, remoção do relógio, pausa e morte montado (R01 segue PARCIAL: 1 achado de pose do servidor aberto, QA humano pendente, sem publicação)

Hardware: só a CPU nova (Ryzen 5 5600GT, 16 GB). Protocolo 65 inalterado, sem mudança de wire. Nada publicado, nenhum push, servidor remoto intocado. Os Packages do usuário (`manifest.json` e `packages-lock.json`) não foram alterados.

### 1. Recuperação do runner após o build interrompido
- Nenhum processo nosso restante (Unity, players SB6 ou Python do harness). O serviço do GitHub runner (que não é nosso) foi deixado como estava.
- `Temp/UnityLockfile` órfão: nenhum processo o mantinha aberto e foi deixado no lugar (o Unity o reutiliza). Nenhum cache, Library ou lock foi apagado.
- Sincronização reaplicada com manifesto e backup em `Logs/R01-runner-sync/backup-20261107-061218`; restauração condicionada a hash no fim (seção 7).

### 2. Captura densa do servidor (correção da lacuna que impedia a comparação)
- Antes, o servidor gravava `player_visual_sample` a cada 50 ms. Um passo marcado no observador caía entre duas amostras e não podia ser classificado; essas comparações eram inconclusivas.
- Correção: `-online-dense-server-reference` (só Development) faz o servidor gravar `player_visual_sample` e `mount_visual_sample` em todo frame.
  - Números de frame faltando = lacuna de captura (inconclusivo).
  - Salto de tempo entre frames consecutivos = travada real do servidor (referência exata).
- Resultado: **0 lacunas de captura** em todas as execuções desta rodada (`serverCaptureDropped=0`, `captureGaps=0`).

### 3. Métrica final de passos para trás (classificação por tempo no caminho)
Cada passo visível (> 0,1 u, limite inalterado) recebe o tempo τ do servidor em que aquela posição existiu no caminho denso, lido como o estado menos defasado que ele pode mostrar e nunca no futuro.

| Classe | Significado | Efeito no veredito |
|---|---|---|
| `path_forward` | τ avançou: raiz atrasada alcançando o servidor, ou reversão do próprio servidor mostrada em ordem | não é passo para trás |
| `presentation` | τ recuou: o observador voltou a um estado anterior | **passo para trás real (falha)** |
| `off_path` | posição que o servidor nunca teve | falha |
| `ambiguous_reversal` | o servidor reverteu em ≤ 2 frames e as duas leituras cabem em 35 ms | inconclusivo (nunca aprovação) |
| `unverifiable` | o passo cai numa lacuna de captura | inconclusivo |

Implementação: `Tools/Diagnostics/analyze_remote_player.py` (classify/path_times/path_classes) e `Tools/Diagnostics/r01_ab_report.py` (agregação A/B). Testes: `Tools/Ci/test_r01_ab_report.py` cobre catch-up, recuo real, fora do caminho, lacuna, reversão de 2 frames e a distinção entre lacuna de captura e travada.

### 4. A/B controlado do relógio de apresentação (3 execuções por configuração)
Cenário `locomotion`, 2 clientes renderizados e 6 jogadores, cerca de 21 a 26 mil frames em movimento por execução. "Travada" = `-load-test-server-stall-ms 180 -load-test-server-stall-every 3`. Pasta: `Logs/R01-assisted-20261107-061225` (`ab-stall.json`, `ab-calm.json`).

| Configuração | passos reais (`presentation`) | por 1000 frames | `path_forward` | retenções | maior retenção | recuperação máx. | atraso mirror−apresentado p95 | raiz≠pose / pose≠mount |
|---|---|---|---|---|---|---|---|---|
| travada, relógio ligado | 0 / 2* / 0 | 0 / 0,085 / 0 | 14 / 27 / 23 | 342 / 330 / 297 | 218 / 120 / 141 ms | 100 / 100 / 117 ms | 0,5 / 0,5 / 0,4 ms | 0 / 0 |
| travada, relógio desligado | 0 / 0 / 2 | 0 / 0 / 0,094 | 23 / 24 / 17 | 0 | — | — | 0,4 / 0,5 / 0,4 ms | 0 / 0 |
| calmo, relógio ligado | 0 / 0 / 0 | 0 | 2 / 2 / 2 | 43 / 50 / 55 | 102 / 107 / 127 ms | 0 / 17 / 183 ms | 0,5 / 0,3 / 0,4 ms | 0 / 0 |
| calmo, relógio desligado | 0 / 1 / 0 | 0 / 0,039 / 0 | 0 / 3 / 0 | 0 | — | — | 0,3 / 0,5 / 0,5 ms | 0 / 0 |

\* Os 2 passos de on2 caíram numa reversão do servidor em 2 frames (1,125 → 0,812 → 1,062). Reclassificados como `ambiguous_reversal`: inconclusivos, não contados como melhora nem como falha.

- Atraso acumulado: a diferença entre o tempo do Mirror e o apresentado não cresce (p95 ≤ 0,5 ms em todas as execuções; máx. 0,5 ms).
- Alinhamento raiz/pose/montaria: 0 frames em que a raiz e a pose/montaria amostram tempos diferentes; 0 pares pose≠mount no mesmo frame.
- **Conclusão: o relógio não demonstrou benefício.** Os passos reais são raros e equivalentes nos dois lados (≤ 2 por execução, cerca de 0,09 por 1000 frames). O relógio só acrescentava retenções de até 218 ms.
- Os passos marcados que restam são, em quase todos os casos, `path_forward`: atraso legítimo da interpolação alcançando o servidor depois de uma travada. Não são passos para trás.

### 5. Remoção do relógio (item 5) e correções mantidas
- Removidos: `RemotePresentationClock`, `ObserverPresentationTime`, as linhas `timeline_slew`, o interruptor `-online-no-timeline-slew`, `RemoteObserverTime` e os 3 testes do relógio. `AuthorityPresentationTime` voltou a usar `NetworkTime.time` para observadores.
- Mantidos, porque foram comprovados:
  - gate do mount também no dono que prediz;
  - reaplicação da pose (`-online-no-pose-enforce`);
  - fila de input com tick por borda;
  - catálogo de áudio.
  - Mantidos também: a exclusão de diagnóstico `UsesSpecialPresentationTimeline` e a medição `root_backstep`/`root_motion_summary` (agora com `units;ptime;pprev;mirror`, sem campos do relógio).
- Confirmação sem relógio, com o build final (pasta `Logs/R01-assisted-20261107-071951`, `ab-final.json`):
  - locomotion_s1 (travada): 0 `presentation`, 12 `path_forward`, 0 lacunas.
  - locomotion_n1 (calmo): 0 `presentation`, 1 `path_forward`.
  - Nos dois: 0 desalinhamento e atraso p95 de 0,4 ms.
  - Execuções sem relógio somadas: travada 0/0/2/0 e calmo 0/1/0/0.
- Inconclusivo que permanece (sem mudar limites): o verificador de locomotion do `analyze_remote_player` marca como "borderline" de 1 a 5 passos grandes (≥ 1 u) por observador, em excursões rápidas do servidor (velocidade de 7 a 18 u/s), abaixo do próprio limite de amostragem. Com a referência densa, os mesmos passos são `path_forward`. Ficam registrados como inconclusivos, não como aprovação.

### 6. Fila de input: o tick preservado não duplica nem reaplica ações
- Cada elemento da fila carrega um tick por borda (`QueuedInput.EdgeTick`). No estouro, as bordas do frame descartado passam para o próximo com o tick original. As bordas do próprio frame mantêm os seus ticks.
- Uma borda repetida continua sendo uma borda. Frames antigos não voltam, porque `CmdControls` só aceita ticks mais novos.
- Teste EditMode novo, `R01ServerInputQueueOverflowNeverDuplicatesOrRelabelsActions`:
  - o A do próprio frame mantém o tick 11 e o Select mesclado mantém o tick 10;
  - numa rajada de 40 ticks, nenhuma borda é aplicada duas vezes e o total aplicado fica entre 1 e o número enviado.
- Smokes `mount-cycles` sob travada (c1, c2, c3): Select enviado 4, aplicado 4, duplicado 0 em cada cliente, nas 3 execuções. Desmonte previsto 4 / confirmado 4 e montagem 3/3 em c1 e c2; c3 teve Select 4/4 e 0 duplicações.

### 7. Pausa, morte montado e regressões
- Pausa montado (`mount-visuals_v2`, novo trecho do fixture): cada dono anda montado por 0,3 s, pausa por 1 s (`GamePauseController.TogglePause`), retoma e anda por 0,4 s. Janelas de pausa medidas: 0,97 e 0,98 s (cliente 1) e 1,07 e 1,01 s (cliente 2). Durante e depois delas: 0 passos visíveis e 0 trocas de renderer observadas.
- Morte montado:
  - Bug pré-existente encontrado e corrigido: depois da morte montado em Tank, o servidor lançava `NullReferenceException` em todo frame (`NetworkMountAnimation.cs:139`), porque o mount destruído ainda era tratado como mount vivo.
  - Correção: um mount destruído é tratado como "sem mount", e renderers destruídos forçam o rebind.
  - `mount-visuals_v1`, antes da correção: falha com NRE. `mount-visuals_v2`, depois: 0 exceções.
  - Servidor: `dead=True`. No observador, o p2 passa de Tank para None no frame 4108 e entra no renderer Death no frame 4109; nenhum quadro com mount aparece depois da morte. Capturas `client-1-frames/mountchange-13-00-f4108-*.png` e `mountchange-13-01-f4109-*.png` foram revisadas visualmente.
  - Limitação: o cliente não registra `dead=` replicado, então "morto visto no cliente" é inferido pelo renderer Death.
- Corpo a pé + mount: 0 quadros no dono e no observador (`mount-visuals_v2`, mount-cycles c1–c3).
- `regression-sudden_s1`: o mecanismo terminou (`mechanism completed kind=sudden`), as fases e a apresentação aprovada estão presentes, 0 avisos e 0 exceções. É inconclusivo só para atraso de locomotion, porque o cenário é parado.
- `round-reload_r1` (calmo, cerca de 200 s, com troca de rodada): terminou (`[LoadTest] completed`), com 0 exceções e 0 avisos, mas teve **exit 2 por 1 falha de pose**: duas cabeças no servidor, ver "Achados separados".

### Testes e verificações
- EditMode no runner, com o código final (relógio removido e correção da NRE): **1045/1045** aprovados, 0 falhas, 0 ignorados e 0 avisos `warning CS` no log de compilação (`Logs/R01-assisted-20261107-071951/R01-tests.xml`). O total caiu de 1047 porque os 3 testes do relógio saíram e 1 teste de fila foi acrescentado.
- Python: Tools/Ci 113/113 e Tools/RoomDirectory 15/15.
- UTF-8: os 9 `.cs` alterados ou novos decodificam em UTF-8 estrito, sem BOM corrompido e sem U+FFFD.

### Achados separados (não são do R01, ficam registrados)
- Corrida no teardown: `NetworkBombFx::ServerEmit called when server was not active` depois de `[LoadTest] completed` (smoke-locomotion_a1, exit 1). Não se reproduziu nas demais execuções desta rodada.
- **Duas cabeças ativas no servidor (aberto, pré-existente).**
  - O que aconteceu: em `round-reload_r1` (exit 2), o p5, montado no Mole, ficou com `HeadOnlyUp,HeadOnlyDown` ativos por 61 frames (7167–7227, cerca de 1 s).
  - No mesmo frame, os três atores sintéticos receberam uma pose de 1 s: p4 foi para `MoutedDown` e p6 ficou sem renderers rastreados (emote). Só no rider de Mole a cabeça anterior continuou ligada, junto com a cabeça para baixo do override de inatividade montado (`SetInactivityMountedDownOverride`).
  - Os dois observadores mostraram só `HeadOnlyUp` nessa janela. O analisador acusa "observer never showed HeadOnlyUp,HeadOnlyDown"; a falha foi mantida, não suprimida, e o limite não mudou.
  - Antes, um caso da mesma classe (`HeadOnlyDown,HeadOnlyRight`, cerca de 500 ms) apareceu num run com o relógio ligado. Portanto não vem da remoção do relógio, e o R01 não altera visuais do servidor (só acrescentou o getter `IsPlainFootBodyVisible`).
  - Próximo passo: investigar `MovementController.SetInactivityMountedDownOverride` × `MountVisualController` no Mole, offline e online. Isso é gameplay, fora da apresentação do observador.

### Status do R01 por critério (técnico)
| Critério | Status | Evidência |
|---|---|---|
| Raiz, pose e mount no mesmo tempo | **atendido**: 0 frames desalinhados e 0 pose≠mount em 16 execuções | ab-stall/ab-calm/ab-final.json |
| Passos para trás visíveis | **atendido**: ≤ 2 passos reais por execução (≤ 0,094 por 1000 frames), iguais com e sem relógio, 0 no build final | idem |
| Atraso acumulado e recuperação | **atendido**: p95 ≤ 0,5 ms, sem crescimento | idem |
| Corpo a pé + mount | **atendido**: 0 no dono e no observador | mount-visuals_v2, mountchange-*.png |
| Desmonte/montagem sob travada, sem duplicar ações | **atendido**: Select 4/4/0 duplicações em 3 execuções; teste de rajada | mount-cycles_c1–c3, EditMode |
| Pausa montado | **atendido**: 0 passos e 0 trocas durante e depois | mount-visuals_v2 |
| Morte montado | **atendido** depois da correção da NRE (o observador não registra `dead`; inferido pelo renderer) | mount-visuals_v1/v2 |
| Locomotion (verificador de amostragem) | inconclusivo "borderline" em excursões rápidas; o caminho denso classifica esses passos como `path_forward` | analysis-locomotion_*.json |
| Pose no observador × pose do servidor | **aberto**: 1 falha em round-reload_r1, causada por uma pose inválida do próprio servidor (duas cabeças no Mole durante o override de inatividade); o observador mostrou uma cabeça | round-reload_r1 |
| Relógio de apresentação | removido (sem benefício) | seção 4 |
| QA humano perceptual | **pendente e separado** | — |

### QA humano (separado, não executado)
Usar 2 clientes, build Development com `-online-motion-logs`, gravando a tela, por cerca de 10 min:
- andar com o outro jogador visível;
- montar e desmontar Louie, Mole e Tank;
- pausar montado;
- morrer montado;
- trocar de rodada.

Anotar qualquer tranco, passo para trás ou quadro de corpo a pé com montaria, junto com o horário.

### Reprodução
1. `powershell -File Logs\R01-runner-sync.ps1 -Mode Apply` (com RAM livre ≥ 3 GiB e sem `Runner.Worker`).
2. `python Logs\R01-validate-assisted.py --step build` e depois `--step tests --resume latest`.
3. `powershell -File Logs\R01-round7.ps1 -Runs "locomotion:_k1:stall,locomotion:_k2:calm,mount-cycles:_c1:stall,mount-visuals:_v1:calm"`
4. `python Tools\Diagnostics\r01_ab_report.py --group stall <pastas> --group calm <pastas> --output ab.json`
5. `powershell -File Logs\R01-runner-sync.ps1 -Mode Restore`

Próximo: QA humano e investigação do achado das duas cabeças no servidor. Não avançar para o R02 sem autorização.

## Rodada 07/11/2026 08:30-09:20 — duas cabeças no Mole: causa, correção e round-reload no código final (sem publicação)

Pasta: `Logs/R01-assisted-20261107-084653`. Hardware: só a CPU nova. Protocolo 65 inalterado. O suavizador/relógio continua removido.

### Causa (reproduzida)
- O caso de `round-reload_r1` (frames 7167–7227 do servidor) não era inatividade: era um **stun de 1 s**.
- No mesmo frame, p4 (Louie) passou a mostrar só `MoutedDown`, p6 (a pé) ficou sem renderers rastreados e p5 (Mole) mostrou `HeadOnlyUp,HeadOnlyDown`.
- `StunReceiver.ShowMountedPlayerDownForMountedStun` liga a pose do rider olhando para baixo (`GetMountedDownRendererForExternalStun`), mas não desliga a pose sentada atual.
  - Montarias com renderer de stun animado (Louies) passam antes por `SetVisualOverrideActive(true)`, que esconde tudo. Por isso só aparece uma pose.
  - O Mole tem `useAnimatedStunRenderer: 0` (stun só com tremor, `Mole.prefab`). Ele mantém os sprites do rider, então a cabeça anterior continuava ligada ao lado da cabeça do stun durante todo o stun.
- O componente é comum a offline, host, dono e servidor. Os observadores copiam a pose única que o servidor amostra; por isso mostravam só `HeadOnlyUp`.

### Correção
- `MovementController.CollectMountedBodyRenderers`: lista as poses sentadas do rider (mounted e head-only).
- `StunReceiver`:
  - ao mostrar o rider olhando para baixo, esconde as outras poses sentadas visíveis e as guarda;
  - ao sair, devolve essas poses só se o rider ainda estiver montado, vivo e sem outra pose sentada visível. Se algo assumiu a pose no meio, a cabeça antiga não volta por cima.
  - A visibilidade é medida pelo `SpriteRenderer`, porque `RestoreAnimations` pode deixar o sprite ligado com o animador desligado.
  - Uma conclusão de stun com apresentador externo descarta a lista.
- O Louie (stun com override visual) não muda: nada fica visível para esconder.

### Regressão e reprodução determinística
- EditMode `R01MountedShakeOnlyStunShowsOneRiderHeadAndRestoresTheSeatedPose`, com `Player.prefab` e `Mole.prefab` reais:
  - Up → stun → só `HeadOnlyDown`; reentrada mantém uma cabeça; saída → `HeadOnlyUp`;
  - já virado para baixo: permanece `HeadOnlyDown`;
  - outra pose escolhida durante o stun: a antiga não volta;
  - desmontado durante o stun: nada é restaurado.
- Fixture `mount-visuals` (só Development): depois da pausa, cada rider é virado para cima e recebe `ForceStun(1 s)`. O fixture falha se houver mais de uma pose sentada durante o stun.

### Resultados no código final
| Verificação | Resultado |
|---|---|
| EditMode no runner | **1046/1046**, 0 falhas, 0 avisos `warning CS` (`R01-tests.xml`) |
| mount-visuals_t1 | servidor: Mole `before=HeadOnlyUp during=HeadOnlyDown after=HeadOnlyUp`; Tank igual. Servidor, dono e observador: 0 bordas com duas poses sentadas e as mesmas bordas `HeadOnlyDown`. Pausa: janelas de 0,97–1,03 s, 0 passos e 0 trocas. Morte montado: sem NRE. 0 exceções |
| round-reload_t1 | **0 falhas** (antes exit 2), `[LoadTest] completed`, 0 avisos, 0 exceções, 0 poses duplas em servidor e clientes. Passos: 0 `presentation`, 5 `path_forward`, 0 lacunas de captura |
| regression-sudden_t1 | 0 falhas, 0 poses duplas |
| mount-cycles_t1 (travada) | Select 4/4, 0 duplicados; 0 `presentation` |
| locomotion_t1 (travada) | 0 falhas, 0 `presentation`, 17 `path_forward`, 0 lacunas, atraso p95 de 0,4 ms, 0 desalinhados |

O round-reload não garante um stun montado em toda execução. A cobertura determinística do stun é o fixture e o teste EditMode; o round-reload confirma que não há regressão.

### Separação final do R01
**Falhas técnicas abertas:** nenhuma reproduzida no código final.
- As duas cabeças foram corrigidas.
- A NRE da morte montado foi corrigida.
- A corrida no teardown (`NetworkBombFx::ServerEmit` depois de `[LoadTest] completed`, 1 ocorrência em `locomotion_a1`) não se reproduziu desde então. Fica registrada como não reproduzida, não como corrigida.

**Medições inconclusivas e limitações:**
- O verificador de locomotion por amostragem marca como "borderline" passos de cerca de 1 u em excursões rápidas do servidor (7–18 u/s), abaixo do próprio limite de amostragem. Na referência densa, todos são `path_forward`. O limite não foi mudado e eles continuam inconclusivos, não aprovados.
- Os cenários parados (mount-cycles, sudden, mount-visuals) não medem atraso de locomotion.
- "Morto no cliente" é inferido pelo renderer Death, porque o cliente não registra `dead=` replicado.
- 1 a 4 execuções por cenário e só a CPU nova; não há comparação com a CPU antiga.
- O stun montado no round-reload é casual.

**QA perceptual humano (pendente, separado):** 2 clientes, build Development com `-online-motion-logs`, gravando a tela, por cerca de 10 min:
- andar com o outro jogador visível;
- montar e desmontar Louie, Mole e Tank;
- pausar montado;
- ser atordoado montado no Mole e no Louie;
- morrer montado;
- trocar de rodada.

Runner restaurado no fim (ver relatório). Não avançar para o R02 sem autorização.

## R01 — status registrado em 07/11/2026 09:30: implementação e validação automatizada concluídas; aceite manual pendente (não publicado)

**Status:** a implementação e a validação automatizada estão concluídas. O **aceite manual (QA perceptual humano) está pendente.** Nada foi publicado e o R02 não foi iniciado.

### Concluído
- **Código:**
  - gate do mount também no dono;
  - reaplicação da pose;
  - fila de input com tick por borda;
  - NRE da morte montado;
  - duas cabeças no stun do Mole (`StunReceiver`);
  - catálogo de áudio;
  - remoção do relógio/suavizador, que não demonstrou benefício.
- **Validação automatizada no código final** (`Logs/R01-assisted-20261107-084653`):
  - EditMode 1046/1046, 0 avisos;
  - Python Ci 113/113 e RoomDirectory 15/15 (sem mudanças em Python desde a última execução);
  - smokes mount-visuals, mount-cycles (travada), round-reload, sudden e locomotion (travada), sem falhas técnicas;
  - 0 passos para trás reais (`presentation`) e 0 lacunas de captura;
  - Select 4/4 sem duplicar;
  - 0 poses sentadas duplas.

### Limitações preservadas (não são aprovação)
- **Medições inconclusivas:** o verificador de locomotion por amostragem marca como "borderline" passos de cerca de 1 u em excursões rápidas do servidor (7–18 u/s). Na referência densa eles são `path_forward`, mas continuam inconclusivos; o limite não mudou.
- Os cenários parados não medem atraso de locomotion.
- "Morto no cliente" é inferido pelo renderer Death.
- 1 a 4 execuções por cenário, só na CPU nova.
- O stun montado no round-reload é casual; a cobertura determinística é o fixture e o EditMode.
- **Teardown antigo:** `NetworkBombFx::ServerEmit called when server was not active` depois de `[LoadTest] completed` (`smoke-locomotion_a1`, 07/11, exit 1). Não se reproduziu nas execuções seguintes. Está registrado como não reproduzido, **não como corrigido**.

### Aceite manual (pendente)
- Pacote: `C:\Users\mauri\SB6-QA-R01`, fora do repositório e do runner.
  - Cópia da build testada, com o mesmo SHA-256 do Assembly-CSharp: `1b3cba05…`.
  - Scripts `1-Servidor.cmd` (servidor dedicado local na porta 7790, Development, `-online-motion-logs`), `2-Cliente1.cmd` e `3-Cliente2.cmd` (`-online-join kcp://127.0.0.1:7790`).
  - `LEIA-ME.txt` com o roteiro.
- Verificado em 07/11 às 09:25: o servidor escuta na 7790 e os dois clientes entram no lobby (`[Room] join player=1/2`). Os processos de teste foram encerrados em seguida.
- Roteiro:
  - andar com o outro jogador visível;
  - montar e desmontar Louie, Mole e Tank;
  - pausar montado;
  - ser atordoado montado no Mole e no Louie;
  - morrer montado;
  - trocar de rodada.
- Observar o dono e o observador. Gravar a tela e anexar os logs.
- Depois do aceite: decidir a publicação. Não avançar para o R02 sem autorização.

## R01 — aceite manual aprovado em 07/11/2026 (status corrigido na seção seguinte: R01 NÃO concluído)

**Status (substituído pela seção seguinte):** implementação, validação automatizada e aceite manual completos; o R01 continua parcial por duas medições obrigatórias pendentes. Publicação não feita; R02 não iniciado.

### Aceite manual
- O usuário relatou em 07/11/2026: "QA manual aprovado nos cenários testados".
- O QA usou o pacote `C:\Users\mauri\SB6-QA-R01`: build Development validada (Assembly-CSharp SHA-256 `1b3cba05…`), servidor dedicado local na porta 7790 e 2 clientes.
- O roteiro oferecido cobria:
  - andar com o outro jogador visível;
  - montar e desmontar Louie, Mole e Tank;
  - pausar montado;
  - ser atordoado montado no Mole e no Louie;
  - morrer montado;
  - trocar de rodada.
- O aceite vale para os cenários efetivamente testados pelo usuário. Cenários do roteiro que não tenham sido exercitados não ganham cobertura por este registro.

### Base do fechamento
- **Código:**
  - `NetworkMountAnimation`: gate do mount também no dono; rebind de mount destruído na morte montado.
  - `NetworkPlayerAnimation`: reaplicação da pose, diagnósticos e remoção do relógio.
  - `NetworkPlayerInput`: fila com tick por borda.
  - `StunReceiver` + `MovementController.CollectMountedBodyRenderers`: uma cabeça no stun do Mole.
  - `OnlineBattleAudio.asset`.
- **Validação automatizada no código final** (`Logs/R01-assisted-20261107-084653`):
  - EditMode 1046/1046, 0 avisos;
  - Python Ci 113/113 e RoomDirectory 15/15;
  - smokes mount-visuals, mount-cycles (travada), round-reload, sudden e locomotion (travada), sem falhas técnicas;
  - A/B do relógio com 3 execuções por configuração (`Logs/R01-assisted-20261107-061225`).

### Limitações preservadas (continuam válidas depois do aceite)
- O verificador de locomotion por amostragem marca como "borderline" passos de cerca de 1 u em excursões rápidas do servidor (7–18 u/s). Na referência densa eles são `path_forward`. Continuam inconclusivos, e o limite não foi mudado.
- Os cenários parados (mount-cycles, sudden, mount-visuals) não medem atraso de locomotion.
- "Morto no cliente" é inferido pelo renderer Death, porque o cliente não registra `dead=` replicado.
- 1 a 4 execuções automatizadas por cenário, só na CPU nova (Ryzen 5 5600GT); não há comparação com a CPU antiga.
- O stun montado no round-reload é casual. A cobertura determinística é o fixture `mount-visuals` e o teste EditMode.
- Teardown antigo: `NetworkBombFx::ServerEmit called when server was not active` depois de `[LoadTest] completed` (`smoke-locomotion_a1`, 07/11, exit 1). Não se reproduziu depois. Está registrado como **não reproduzido, não corrigido**.
- Custo de CPU/bytes e comparação offline × online não foram medidos especificamente para o R01.

### Próximos passos (não executados)
- Publicação do lote R01: aguarda autorização explícita.
- R02: não iniciar sem autorização.

## R01 — status corrigido em 07/11/2026: PARCIAL, com 2 medições obrigatórias pendentes (não publicado)

Motivo: os critérios registrados para o R01 incluem medições que não foram feitas. Elas pertencem ao R01, não ao R02:
- este arquivo, na auditoria de 06/11: "R01 segue parcial (36). Pendências: capturas owner/observador entre peers, **custo CPU/bytes**, validação de transições/papéis";
- `handoff-claude-predicao-online.md`, na tabela de IDs: "R01 — Faltam capturas entre peers, **custo** e validação das transições/papéis";
- a receita do R01 no handoff (§9): `analyze_prediction.py --online <online.jsonl> --offline <offline.jsonl> --output Logs/R01-comparacao.json`.

O R02 mede custo/atraso **por família de sprites no co-op** (geometria, props e paredes). É outro escopo e não cobre o player remoto do R01. As medições a seguir não são tratadas como validadas por nenhum item.

| Critério do R01 | Status |
|---|---|
| Capturas owner/observador entre peers | atendido (smokes com 2 clientes renderizados e captura densa do servidor) |
| Validação de transições/papéis: troca de montaria, morte, emote/AFK, pausa, rodada, stun | atendido (automatizado + aceite manual nos cenários testados) |
| Raiz/pose/mount no mesmo tempo; passos para trás; fila de input sem duplicar | atendido |
| **Custo CPU/bytes do player remoto** | **PENDENTE (obrigatório)**: não medido. Precisa de uma baseline comparável (mesmo cenário, CPU nova, antes × depois ou referência documentada); uma medição isolada não prova nada |
| **Comparação online × offline** (`analyze_prediction.py`, `Logs/R01-comparacao.json`) | **PENDENTE (obrigatório)**: não executada. Comparar mesma fase, loadout, ação e papel |
| QA perceptual | aprovado pelo usuário nos cenários testados |

Limitações preservadas (não são aprovação):
- borderline de amostragem em locomotion (inconclusivo);
- cenários parados sem medir atraso;
- morte no cliente inferida pelo renderer Death;
- 1 a 4 execuções só na CPU nova;
- stun montado casual no round-reload;
- teardown antigo (`NetworkBombFx::ServerEmit` após `[LoadTest] completed`, `smoke-locomotion_a1`): não reproduzido e não corrigido.

**Status:** R01 **PARCIAL**. Implementação, validação automatizada e aceite manual concluídos; faltam as duas medições obrigatórias acima. Não publicado; R02 não iniciado. O R01 só deve ser marcado como concluído depois das duas medições, com evidência em `Logs/`.

## R01 — critérios das duas medições obrigatórias, registrados ANTES das execuções (07/11/2026)

Ferramentas (não publicadas):
- `Assets/Scripts/Diagnostics/R01ComparisonHarness.cs`: só em Development, inerte sem argumentos `-r01-*`. Usa apenas APIs anteriores ao R01, para entrar também na build baseline.
- `Tools/Diagnostics/r01_compare_run.py`: lança e amostra as execuções.
- `Tools/Diagnostics/r01_compare.py`: gera `Logs/R01-comparacao.json`. Testes em `Tools/Ci/test_r01_compare.py`.
- `Tools/Diagnostics/r01_cost_report.py`: relatório de custo.

Cenário comum:
- BattleMode_15, 2 jogadores (P1/P2 Man, demais Off), sem COM, sem fixture de spawn, Sudden Death forçado Off e restaurado no fim.
- P1 executa um script fixo pelo slot sintético de hardware: Direita, Esquerda, Cima, Baixo, cada um segurado 2,4 s com 0,6 s de pausa, ×3 (36 s), mais 3 s de repouso. P2 fica parado.
- Processos renderizados a 60 fps, vSync 0, 1024×896. Online: servidor dedicado local (batchmode, nographics) + 2 clientes renderizados (cliente 1 = dono de P1; cliente 2 = observador de P1).

### Medição 1 — custo do jogador remoto
- Builds:
  - **baseline** = HEAD `b7c77eff3` (sem o diff do R01) + somente o harness;
  - **R01** = árvore atual;
  - **R01 com chaves A/B desligadas** (`-online-no-pose-enforce -online-no-mount-gate`).
- 3 repetições por configuração, sem captura por quadro nem logs de movimento.
- Métricas:
  - CPU de processo (% de um núcleo) do servidor, do cliente 1 e do cliente 2 na janela do script;
  - `[ServerMetrics]` do servidor: CPU, KiB/s de entrada e saída, marcadores BattlePerf.
- Execução válida: todos os processos saem com 0, a janela do script foi vista e há janela de servidor sem transição. Execuções inválidas são listadas.
- Diferença em relação à baseline: "não resolvida" se as faixas mín–máx se sobrepõem; caso contrário, "diferença medida", com o tamanho. Não há limite de aprovação para CPU.
- Atribuição:
  - baseline → R01 é a mudança total;
  - R01 → chaves desligadas isola só a imposição de pose e o gate do mount;
  - a fila de input e a correção do stun não têm chave e **não são isoláveis**.
- Bytes: o diff do R01 não adiciona SyncVar, Command, Rpc nem mensagem. O resultado esperado é igual e é verificado pela medição.

### Medição 2 — online × offline (`Logs/R01-comparacao.json`)
3 execuções offline + 3 online com captura, pareadas pelo índice.

| Critério | Exigência |
|---|---|
| A. Fase e loadout | Mesma cena; 2 jogadores; spawn de P1 com \|Δ\| ≤ 0,01 u por eixo; mesma velocidade, bombas e raio; Sudden Death Off; mesma impressão dos blocos destrutíveis; P1 desmontado e vivo no início do script; processos renderizados a 60 fps / vSync 0 |
| B. Sequência de ações | Offline e dono executaram os 12 segmentos na ordem; cada um segurado 2,4 s ± 0,034 s (2 quadros) |
| C. Apresentação | A sequência colapsada de renderers do corpo de P1 é igual à offline no servidor, no dono e no observador; nenhuma combinação de renderers de P1 ou P2 que nunca aparece offline (pega pose dupla); P2 parado não se move |
| D. Movimento | Trechos de movimento de P1 (\|deslocamento\| ≥ 0,05 u; pausas ≤ 0,1 s unidas): mesma quantidade e direção que offline; por trecho, \|Δ deslocamento\| ≤ 0,25 u e \|Δ duração\| ≤ 0,1 s |
| E. Posição final | \|Δ\| ≤ 0,25 u |
| F. Só relatado, sem limite | Início input→movimento (offline × dono); atraso do servidor e do observador em relação ao dono (relógio UTC comum); resumo do `analyze_prediction.py` |

- As execuções offline também são comparadas entre si por C, D e E. Se offline não reproduz offline, o resultado é **inconclusivo**, nunca aprovação.
- O `analyze_prediction.py` mede apenas a latência `input_to_motion` e a contagem de eventos. Ele **não** verifica fase, loadout, sequência, pose nem posição. Por isso os critérios A–E ficam no `r01_compare.py`, e a saída do `analyze_prediction.py` vai embutida no JSON.

### Piloto de ferramenta e emenda de protocolo (07/11/2026, antes das execuções medidas; nenhum limite alterado)
Piloto em `Logs/R01-measure-pilot` (1 offline + 2 online), **fora dos resultados**. Encontrou dois problemas.

1. **Bug do harness.** A impressão dos blocos destrutíveis dependia da ordem em que o `FindObjectsByType<Tilemap>` devolve os tilemaps: o cliente 1 deu outro hash com as mesmas 182 células. Passou a ser uma soma independente de ordem. As duas builds foram refeitas com o harness idêntico. Na nova build piloto, os 6 papéis dão o mesmo valor (`182:5fd04101`).
2. **Quadros longos e o critério B.** O input sintético é aplicado no `Update` e a borda cai no primeiro quadro renderizado depois do prazo. Quadros longos (92–282 ms) prolongam o hold de verdade, também no tempo de jogo; o piloto offline teve um hold de 2,5165 s. Isso faz o input deixar de ser equivalente, e é exatamente o que B detecta. Emenda: uma execução cujos únicos problemas são de B fica listada como "input não equivalente", sai do pareamento e é substituída. São no máximo 6 tentativas por modo e o veredito exige 3 válidas por modo. As excluídas ficam no JSON. Limites inalterados.

Limitação de cobertura vista no piloto: a partir do spawn em BattleMode_15, P1 tem só 1 ou 2 tiles livres. São 7 trechos de movimento de no máximo 0,52 s por execução, então a comparação não cobre corridas longas. Elas continuam cobertas pelos smokes de locomotion.

Builds medidas (`C:\Users\mauri\SB6-R01-cost`; as v1 foram preservadas):

| Build | Conteúdo | Assembly-CSharp SHA-256 |
|---|---|---|
| baseline | runner `b391172d` (código igual a HEAD `b7c77eff3`; os commits seguintes só mudam docs) + harness + os mesmos Packages do repositório (só versões do SDK/toolchain Linux) | `ae600537…` |
| R01 | conjunto completo do R01 + harness | `4642fb27…` |

- Unity 6000.6.0f1, Development, sem avisos CS.
- Marcadores verificados no binário: a baseline **não** contém `-online-no-pose-enforce` nem `CollectMountedBodyRenderers`; a build R01 contém os dois.
- O runner foi restaurado depois de cada build e voltou às 14 diferenças locais pré-existentes.

## R01 — resultado das duas medições obrigatórias (07/11/2026; não publicado; R02 não iniciado)

**Status:** R01 continua **PARCIAL**.
- A comparação online × offline foi demonstrada (com as emendas registradas).
- A medição de custo foi feita e revelou dois custos. Um deles levou a uma correção (gate Development-only) que **ainda não foi compilada no Unity nem medida**: a execução de testes e build foi interrompida pelo Claude Code por falta de memória.
- QA manual preservado: "QA manual aprovado nos cenários testados".

### Comandos
```
python Tools/Diagnostics/r01_measure_all.py --baseline C:\Users\mauri\SB6-R01-cost\baseline --r01 C:\Users\mauri\SB6-R01-cost\r01 --output Logs/R01-measure-20261107            # custo (GetProcessTimes) + comparação 1
python Tools/Diagnostics/r01_measure_all.py --only compare ... --output Logs/R01-measure-20261107b                                                                          # comparação 2 (veredito)
python Tools/Diagnostics/r01_measure_all.py --only cost ... --output Logs/R01-measure-20261107c                                                                             # custo 2 (QueryProcessCycleTime)
python Tools/Diagnostics/r01_compare.py --offline <3 pastas> --online <3 pastas> --output <json>
python Tools/Diagnostics/r01_cost_report.py --baseline <3> --r01 <3> --r01-switches-off <3> --output <json>
```

Cenário: o pré-registrado. Builds: baseline `ae600537…` × R01 `4642fb27…`. Máquina: Ryzen 5 5600GT + GTX 1660, Windows 11.
- Servidor dedicado local em batchmode/nographics.
- Os 2 clientes rodam em `-batchmode` com dispositivo D3D11, **sem janela visível**. Isso corrige a descrição "renderizados".
- Cada execução dura ~85 s; a janela medida é a do script, de 36 s.

### Medição 2 — online × offline: **demonstrada** (`Logs/R01-comparacao.json`)
- **Sessão 1** (`Logs/R01-measure-20261107/compare`, relatório `R01-comparacao-regras-originais-FALHOU.json`): **falhou** pelas regras originais. Duas causas, nenhuma é divergência do R01:
  1. O instrumento gravou a linha de setup no primeiro quadro em que P1 existia, antes de P2 nascer naquele processo (`players=1`). Os mesmos arquivos têm amostras de P2 desde 3,3 s antes do script.
  2. P2 parado mostrou `Afk2` no servidor, no dono e no observador de online-3. `InactivityAnimation` escolhe `Afk2` no lugar de `Afk` com 30% de chance, e as 3 execuções offline tiraram `Afk`.
  - P1 bateu exatamente em todos os papéis: Δ deslocamento 0, Δ final 0.
- **Segunda emenda**, registrada no `r01_compare.py` antes da sessão 2. Nenhum limite numérico mudou:
  - os 2 jogadores são verificados pelas amostras;
  - pose dupla passa a ser explícita: 2 ou mais renderers do corpo visíveis ao mesmo tempo;
  - a sequência de P2 no dono e no observador tem de ser igual à do servidor, e `Afk2` é aceito como estado de P2;
  - P1 continua exigindo igualdade total com offline.
- **Sessão 2** (`Logs/R01-measure-20261107b`, cópia em `Logs/R01-comparacao.json`): **demonstrated**, 3 offline + 3 online, 0 execuções excluídas.

| Critério | Resultado (3 pares) |
|---|---|
| A. Fase e loadout | Iguais: BattleMode_15, spawn (-7,4)/(5,4), velocidade 4, 1 bomba, raio 2, SD Off, blocos `182:5fd04101`, 60 fps / vSync 0; 2 jogadores presentes |
| B. Ações | 12/12 segmentos; holds de 2,4000 a 2,4058 s |
| C. Apresentação | Sequência de P1 igual à offline nos 3 papéis; 0 poses duplas; P2 `Down → Afk` igual nos 3 papéis; P2 sem movimento |
| D. Movimento | 7 trechos (RLDUDUD) em todos os papéis; Δ deslocamento 0,000 u; Δ duração máx.: servidor 0,017 s, dono 0,001–0,059 s, observador 0,034–0,050 s |
| E. Posição final | Δ 0,000 u |
| Offline × offline | 7 trechos iguais; Δ duração ≤ 0,016 s |
| F (relatado) | Início input→movimento: offline mediana 33 ms (17–34; varia com a fase do quadro); dono mediana 17 ms (11–59). Atraso em relação ao dono: servidor, medianas por execução 1,5–11 ms; observador 28–46 ms |

`analyze_prediction.py` (embutido no JSON): `input_to_motion` com 6–7 amostras livres por captura, cliente ~17 ms. O p95 fica nulo, porque o mínimo é de 20 amostras. Ele **não** mede fase, sequência, pose nem posição; isso é feito pelo `r01_compare.py`.

Limitação: a partir do spawn, P1 tem só 1–2 tiles livres. São trechos curtos (≤ 0,52 s), sem corridas longas.

### Medição 1 — custo do jogador remoto: **feita; revelou dois custos; correção pendente de validação**
- **Sessão 1** (`Logs/R01-measure-20261107/R01-custo.json`): 9/9 execuções válidas. Os clientes usam ~1 s de CPU por 70 s, e o `GetProcessTimes` (cobrança por tick de 15,6 ms) **não resolve** isso: o mínimo do cliente 2 chegou a 0,000%. Os números de CPU dessa sessão ficam como não resolvidos.
- **Emenda de instrumento:** `QueryProcessCycleTime`, que conta ciclos exatos.
- **Sessão 2** (`Logs/R01-measure-20261107c/R01-custo.json`): 9/9 válidas. Valores: média [mín–máx], n = 3.

| Métrica | baseline | R01 | R01 sem chaves | R01 − baseline |
|---|---|---|---|---|
| Cliente 2, observador do jogador remoto (Mciclos/s) | 350,9 [346,2–359,7] | 373,3 [368,9–376,9] | 355,5 [349,9–365,3] | **+22,4 (+6,4%), diferença medida.** Contra R01 sem chaves: +17,8 (+5,0%), medida |
| Cliente 1, dono (Mciclos/s) | 344,8 [342,6–347,2] | 353,1 [346,4–359,7] | 348,7 [341,9–358,5] | +8,3 (+2,4%), não resolvida |
| Servidor (Mciclos/s) | 337,1 [331,2–340,2] | 333,0 [322,9–348,3] | 341,6 [329,4–349,5] | −4,0, não resolvida |
| Bytes servidor entrada (KiB/s) | 2,571 | 2,576 | 2,570 | +0,2%, não resolvida |
| Bytes servidor saída (KiB/s) | 21,13 | 21,07 | 21,40 | −0,3%, não resolvida (coerente com nenhuma mudança de wire) |
| Alocação GC no servidor (KiB/s) | 646,9 [645,7–648,8] | 702,9 [701,5–704,2] | 702,3 | **+56 (+8,6%), medida.** Não vem das chaves; a sessão 1 também deu +61 |
| Marcadores BattlePerf do servidor | — | — | — | todos não resolvidos (≤ 0,01 ms) |

**Achado.** A captura de movimento estava **ativa em todas as execuções de custo**, nas duas builds. O `Assets/Resources/GameplayLatencyDiagnostics.json` versionado tem `"enabled": true`, o que liga a captura em qualquer build. Isso contraria o "sem logs de movimento" pré-registrado. O custo medido inclui, portanto:
- a imposição de pose e o gate do mount: a parte isolável pelas chaves, cerca de +17,8 Mciclos/s no observador;
- os diagnósticos adicionados pelo R01: amostra do servidor a cada 0,05 s em vez de 0,1 s, linhas de borda por quadro, rastreio de raiz e limite de 64 MiB em vez de 8 MiB. A alocação do servidor (+56 KiB/s) vem daqui.

Não isolável: a fila de input e o stun não têm chave. O total do processo inclui Unity, Mirror e o escalonador.

**Correção aplicada (não validada):** a densificação de diagnósticos do R01 passa a ser só de Development (`NetworkPlayerAnimation.R01DenseDiagnostics => Debug.isDebugBuild`). Ela gata `PresentObserverPosition`, `TracePlayerEdge`, `TraceBodyEdge`, a amostra de 0,05 s do servidor e o limite de 64 MiB (`GameplayLatencyDiagnostics.MaxBytes`).
- Em release com captura ligada, volta a densidade anterior ao R01. Em Development nada muda, então os smokes e a comparação continuam válidos para esse comportamento.
- Validação feita: Python CI 124/124 e UTF-8 sem BOM corrompido.
- **Pendentes:**
  1. compilação Unity e EditMode no código final. A execução de 07/11 11:05 (`Logs/R01-final-tests-20261107-110541`) foi encerrada pelo Claude Code por falta de memória, aos 40 s, durante a importação; nenhum processo ficou ativo;
  2. builds sem captura (`baseline-nodiag` pronta, `ae600537…` + JSON `enabled:false` via overlay `Logs/R01-nodiag-overlay`; a `r01-nodiag` não foi feita), para isolar o custo da funcionalidade sem diagnósticos;
  3. decidir se o +5% de ciclos do observador (a imposição de pose chama um segundo `ClientApply()` por quadro) é aceitável ou se precisa de otimização. Isso não deve ser decidido sem a medição sem captura.

Runner restaurado depois de cada etapa (14 diferenças locais pré-existentes). Nenhuma publicação.

### Sessão sem captura — critérios registrados antes das execuções (07/11/2026, 14:05; sessão `Logs/R01-measure-20261107d`)
Retomada autorizada pelo usuário depois do bloqueio de memória. Gate de 3 GiB de RAM livre mantido; etapas sequenciais.
- **Builds:** `baseline-nodiag2` (código de HEAD + harness + Packages iguais) × `r01-nodiag` (R01 final com o gate Development-only).
  - Nas duas, o `GameplayLatencyDiagnostics.json` vem com `"enabled": false`, aplicado pelo overlay `Logs/R01-nodiag-overlay`.
  - O harness é o mesmo nas duas. A `baseline-nodiag` anterior (`ae600537…`) foi preservada, mas não é usada: não tem a coleta de frame.
- **Cenário:** igual ao das sessões anteriores. BattleMode_15, 2 jogadores, SD Off, script de 36 s, 75 s de partida, servidor e 2 clientes locais em batchmode.
- **Repetições:** 5 por configuração, intercaladas baseline / R01 / R01 sem chaves. A regra de sobreposição mín–máx é a mesma.
- **Como confirmo que não houve captura:** uma execução só é válida se as 3 condições valerem nos 3 processos:
  1. o log tem `[R01Frame] installed … motionCapture=False` (lido de `GameplayLatencyDiagnostics.Enabled`);
  2. o log não tem nenhum `[GameplayLatency] Capture:`;
  3. a pasta não tem nenhum `*-motion.jsonl`.
- **Métricas novas por processo**, vindas do mesmo harness nas duas builds (`-r01-frame-stats`), dentro da janela do script (UTC exato de `script_start`/`script_done` no log do cliente 1):
  - **tempo de trabalho do frame na main thread:** do início do EarlyUpdate ao fim do PostLateUpdate, ou seja, sem a espera de frame rate. Reporto média ponderada, p95 médio e máximo;
  - frames por segundo;
  - alocação gerenciada em KiB/s (`GC Allocated In Frame`);
  - coletas gen-0 por minuto.
  - Continuam também ciclos de CPU, bytes e métricas do servidor.
- **Critério de experiência:** custo absoluto do frame. Uma diferença medida no tempo de trabalho é reportada em ms por frame e em % do orçamento de 16,7 ms a 60 fps. Nenhum limite de aprovação/reprovação.
- **Gatilho da otimização:** se a diferença no observador continuar medida sem captura, investigo uma otimização da reaplicação de pose que preserve a correção do flash, com regressão direcionada.

### Sessão sem captura — resultado (07/11/2026, 14:12–14:35; `Logs/R01-measure-20261107d/R01-custo.json`)
**Comandos:**
```
.\Logs\R01-runner-sync.ps1 -Mode Apply -Overlay Logs\R01-nodiag-overlay              # R01 final + JSON enabled:false
python scratchpad/run_tests.py                                                         # Logs/R01-final-tests-20261107-140033
python scratchpad/cost_build.py r01-nodiag C:\Users\mauri\SB6-R01-cost\r01-nodiag      # Logs/R01-cost-build-r01-nodiag-20261107-140414
.\Logs\R01-runner-sync.ps1 -Mode Restore
.\Logs\R01-runner-sync.ps1 -Mode Apply -Overlay Logs\R01-nodiag-overlay -Only <harness .cs/.meta, GameplayLatencyDiagnostics.json, Packages/manifest.json, packages-lock.json>
python scratchpad/cost_build.py baseline-nodiag2 C:\Users\mauri\SB6-R01-cost\baseline-nodiag2
.\Logs\R01-runner-sync.ps1 -Mode Restore                                               # runner de volta às 14 diferenças pré-existentes
python Tools/Diagnostics/r01_measure_all.py --only cost --repetitions 5 --frame-stats --require-capture-off --baseline C:\Users\mauri\SB6-R01-cost\baseline-nodiag2 --r01 C:\Users\mauri\SB6-R01-cost\r01-nodiag --output Logs/R01-measure-20261107d
```

**Código final:**
- Compilação Unity + EditMode no runner: **1046/1046 aprovados**, 0 avisos CS (`Logs/R01-final-tests-20261107-140033`).
- Python CI: 124/124. UTF-8 dos .cs alterados sem BOM corrompido, sem U+FFFD.
- O harness ganhou só `-r01-frame-stats` e o UTC nas linhas `[R01Compare]`. O relatório refeito da sessão c deu resultado idêntico.

**Builds (Unity 6000.6.0f1, Development, 0 avisos CS):**

| Build | Assembly-CSharp | Conteúdo |
|---|---|---|
| `baseline-nodiag2` | `3226f140…` | HEAD + harness + Packages; sem código R01 (o assembly não contém `EnforceAuthorityPose`) |
| `r01-nodiag` | `45770046…` | R01 final com o gate Development-only |

Nas duas, o `resources.assets` embutido contém `"enabled": false` e nenhum `"enabled": true`.

**Como foi confirmado que não houve captura:** as 15 execuções passaram pela regra `--require-capture-off`, e nenhuma ficou inválida. Em cada uma, os 3 processos:
- registraram `[R01Frame] installed gcRecorderValid=True motionCapture=False`;
- não tiveram nenhum `[GameplayLatency] Capture:`;
- não deixaram nenhum `*-motion.jsonl`.

**Resultados:** 5 válidas por configuração. Média [mín–máx], janela do script de 36 s (UTC exato).

| Métrica | baseline | R01 | R01 sem chaves | R01 − baseline |
|---|---|---|---|---|
| Observador (cliente 2), Mciclos/s | 325,9 [312,7–337,3] | 324,7 [311,0–337,0] | 332,8 [322,8–339,2] | −0,4%, não resolvida |
| Observador, trabalho do frame, média (ms) | 0,980 [0,907–1,034] | 1,004 [0,887–1,258] | 0,976 | +0,024 ms (0,15% de 16,7 ms), não resolvida |
| Observador, p95 do trabalho (ms) | 1,157 | 1,149 | 1,171 | não resolvida |
| Observador, alocação (KiB/s) / GC por min | 113,4 / 6,5 | 112,8 / 6,8 | 113,2 / 6,8 | não resolvidas |
| Dono (cliente 1), Mciclos/s | 323,9 [305,5–338,3] | 336,3 [322,1–345,3] | 329,6 | +3,8%, não resolvida |
| Dono, trabalho do frame, média (ms) | 0,995 [0,878–1,117] | 1,037 [0,988–1,131] | 1,040 | +0,042 ms (0,25%), não resolvida |
| Dono, alocação (KiB/s) | 140,2 | 139,4 | 139,6 | não resolvida |
| Servidor, Mciclos/s | 313,5 | 312,7 | 311,5 | não resolvida |
| Servidor, trabalho do frame, média (ms) | 0,973 | 1,024 [0,929–1,238] | 0,967 | +0,052 ms (0,31%), não resolvida |
| Servidor, alocação (KiB/s) | 433,4 [432,7–434,0] | 432,7 [427,3–434,7] | 432,8 | −0,1%, não resolvida |
| Bytes do servidor, entrada / saída (KiB/s) | 2,572 / 21,32 | 2,572 / 20,92 | 2,585 / 21,25 | não resolvidas |
| fps (3 processos) | 59,8–60,0 | 59,8–59,9 | 59,8–60,0 | não resolvida |

- O máximo de trabalho por frame (picos de 35 a 377 ms) aparece nas 3 configurações. É instável entre execuções e não foi resolvido.

**Leitura:**
- Sem captura, nenhuma métrica mostra diferença resolvida entre baseline e R01, nem entre R01 com e sem as chaves.
- O custo absoluto médio do frame fica abaixo de 0,06 ms (< 0,35% do orçamento de 60 fps) nos 3 processos, e nem isso é separável do ruído.
- Os +6,4% de ciclos do observador e os +56 KiB/s de alocação no servidor da sessão c dependiam da captura ligada. Isso é coerente com o código: com a captura ligada, a imposição de pose calcula a máscara de corpo visível antes e depois e grava linhas `player_pose_enforced`.
- **Otimização:** não acionada, pelo gatilho pré-registrado: o custo da reaplicação de pose não ficou demonstrado sem captura. Não aceitei nem rejeitei um percentual isolado: o custo absoluto não é distinguível do ruído.

**Limitações:**
- Todas as builds medidas são Development. O efeito do gate em release com captura ligada foi validado por compilação, EditMode e revisão, não por medição em build release.
- A fila de input e o stun continuam sem chave; o que se mede é o total da build.
- O orçamento de 16,7 ms usa a cadência de 60 fps do cenário. Os clientes são batchmode, sem janela visível, e a GPU não é medida.
- Na comparação online × offline, os trechos de movimento são curtos.
- A otimização "componentes em cache em `EnforceAuthorityPose`/`ClientApply`" (`GetComponent` por frame) continua possível, mas não foi feita: não há custo demonstrado que a justifique.

**Status:** R01 **CONCLUÍDO** nos critérios obrigatórios, mas não publicado e com o R02 não iniciado:
- QA manual aprovado nos cenários testados;
- comparação online × offline demonstrada (sessão 2);
- custo medido, com a parte atribuível isolada até onde as chaves permitem e as limitações declaradas;
- correção dos diagnósticos compilada e testada.

### Publicação R01 concluída — Actions 42403939455 success (07/11/2026)
Publicação autorizada pelo usuário para homologação. O R02 não foi iniciado.

**Commit de runtime:** `b01817c73c62a28e7b936bcd6af559e045240969` (`fix: complete R01 remote player presentation and measurement`), 28 arquivos.
- **Runtime e testes:** NetworkPlayerAnimation, NetworkMountAnimation, NetworkPlayerInput, NetworkLaunchPresentation, LocalServerLoadTest, MovementController, StunReceiver, GameplayLatencyDiagnostics, OnlineRoomVisualRepairTests e `OnlineBattleAudio.asset`.
  - O `.asset` registra os clipes `player_join`/`player_leave`, que já existiam. Ele estava na build validada.
- **Diagnósticos Development:** `FrameHitchProbe` e `R01ComparisonHarness` (+ .meta).
- **Ferramentas e testes Python:** `analyze_remote_player`, `hitch_report`, `r01_ab_report`, `r01_compare`, `r01_compare_run`, `r01_cost_report`, `r01_measure_all`, os 3 testes em `Tools/Ci` e `smoke_full_clients.py`.
- **Docs:** checkpoint, matriz e `prediction-and-reconciliation.md`.

**Ficaram fora do commit:**
- os 2 arquivos de `Packages/`, alterações do usuário. Hashes iguais antes e depois: manifest `b8b74927…`, lock `803f0e80…`;
- `Tools/R01Mcp/`, `.codex-remote-attachments/`, `handoff-claude-predicao-online.md` e `revisao-runner-r01.md`, que não são necessários para o runtime.

**Correspondência com o validado:** os 19 arquivos sincronizados no runner para os testes EditMode 1046/1046 e para a build `r01-nodiag` têm SHA-256 idêntico ao conteúdo commitado.
- A única diferença é intencional: `GameplayLatencyDiagnostics.json`. Na medição, o overlay o desligou; no commit, ele segue igual ao HEAD (`enabled: true`, como em todas as publicações anteriores).
- **Diferença de Packages:** a validação local usou os Packages do usuário (SDK/toolchain Linux 1.1.1). O CI compilou com os Packages commitados (1.1.0, os mesmos do C10). O código C# é o mesmo.
- **Quebra de linha:** o Git normaliza para CRLF no checkout do CI; o conteúdo não muda.
- **Trava órfã:** havia um `.git/index.lock` de 06/11 22:13, com 0 bytes e nenhum processo git ativo. Removi só essa trava antes do stage.

**Pipeline:** push em `v0.5.0` disparou o Homologation both/quick, sucesso em 10m16s.
- Artefatos: `SB6-Windows-2026-11-07_14-58-57` e `SB6-Validation-2026-11-07_14-58-57`.
- Antes do push, conferi que nenhum outro job estava rodando.

**Verificação** (`python Logs/R01-verify-publication.py` → `Logs/R01-publication.json`; manifestos copiados para `Logs/R01-publication-*.json`):
- **Servidor ativo, via SSH:** `sourceCommit=b01817c7…`, `protocol=65`, `benchmarkHarness=false`.
- **deploy-status:** mesmo commit, `success=true`, ativado em 2026-11-07T19:07:31Z. A ativação reiniciou as salas, como autorizado.
- **Cliente:** o `build-info.json` dentro do pacote publicado tem o mesmo commit e o protocolo 65.
- **Atualizador:** o ponteiro público da Release `online-v0.5.0` aponta para o mesmo commit.
  - Asset: `SB6-Windows-2026-11-07_14-58-57.zip`, 183.620.834 bytes, SHA-256 `35249bc3…`. Tamanho e hash conferem com o pacote do CI.

**Observação sobre o binário publicado:** diferente do C10, o cliente release contém os tipos `R01ComparisonHarness` e `FrameHitchProbe`.
- O `LocalServerLoadTest` continua ausente, porque usa `#if UNITY_EDITOR || DEBUG`.
- Os dois tipos retornam na primeira linha quando `Debug.isDebugBuild` é falso, então ficam inertes em release; foi isso o que se validou.
- Se a convenção "sem código de fixture no binário publicado" tiver de valer também para eles, o próximo passo é envolvê-los no mesmo `#if` em uma publicação separada. Não fiz isso agora para não publicar código diferente do validado.

## R02 — critérios obrigatórios e validação prevista (início 07/11/2026)

**Escopo:** apresentação do mundo co-op (`OnlineCoopWorld`, `OnlineCoopSprites`, `OnlinePresentationMetrics`). Autorizado pelo usuário em 07/11; sem push/deploy/publicação; R03+ não iniciar.

**Estado inicial:**
- branch `v0.5.0`, HEAD `43bf8d0e0` (commit do usuário "Add more options in video menu", que inclui os Packages);
- única alteração local: `Assets/Resources/Fonts/PressStart2P-Regular SDF.asset`, do usuário, a preservar e não commitar;
- Unity 6000.6.0f1; protocolo 65;
- runner ocupado no início pelo Actions 42415518535 (push do usuário); não usar enquanto isso.

**Fontes dos critérios:**
- matriz R02: "Co-op com paredes, props móveis e várias famílias de sprites — geometria de consulta aprovada pronta antes da previsão; medir custo/atraso por família e preservar forma das curvas";
- plano §4.3: "auditar atraso e custo por família; usar descritores de arco/segmento onde interpolação linear perde a forma; não reativar em massa os scripts/física de cena desativados no client";
- checkpoint: geometria concluída em d25c34734; curvas e comparação de custo pendentes; dois peers/manual pendentes;
- skill online-prediction: geometria de consulta, timeline/offset dos carriers (C06), pausa, snapshots tardios e cleanup.

| # | Critério obrigatório | Validação prevista |
|---|---|---|
| CR1 | Geometria de consulta aprovada pronta antes da previsão (paredes, props móveis) | Testes EditMode Coop existentes; captura com 2 clientes reais em cena com props (Stage_3-4 ou 3-6) confirmando a prontidão |
| CR2 | Atraso medido por família nos clientes (do estado do servidor até a apresentação), incluindo fome de buffer | Instrumentação Development com a captura ligada; clientes reais; repetições, com variação registrada |
| CR3 | Custo por família: CPU de captura no servidor, CPU de apresentação no cliente, bytes de frame; custo total contra baseline comparável | Por família: captura ligada. Total (ciclos, trabalho por frame, alocações, GC, bytes): captura desligada, baseline = HEAD + harness, mesmas dependências, 5 repetições, regra de não sobreposição de faixas |
| CR4 | Preservar a forma das curvas: medir onde a interpolação linear perde a forma e usar segmentos/pontos aprovados onde perder | Erro da reconstrução enviada contra a trajetória nativa quadro a quadro no servidor, por família; limiar pré-registrado abaixo; antes/depois |
| CR5 | Continuidade entre segmentos: início após repouso, término, visibilidade, cancelamento/remoção | Mesma métrica de reconstrução (início após repouso separado) + passos/paradas por quadro no cliente; regressões EditMode |
| CR6 | Snapshots tardios, geração/revisão de cena, substituição de entidade, morte/disable, desconexão e troca de cena, com históricos limitados e cleanup | Regressões EditMode nos caminhos tocados; captura com troca de cena/recarga quando o caminho for tocado |
| CR7 | Pausa/retomada sem avanço/retrocesso do buffer nas famílias que pausam com o gameplay | Regressão EditMode + pausa em captura real |
| CR8 | Sem reativar scripts/IA/RNG/física de cena no cliente; sem apresentador concorrente; autoridade e caminhos offline/host intactos | Revisão de código + testes existentes de gates + logs (0 erros/avisos) |
| CR9 | Comparação com o comportamento offline quando aplicável | A referência nativa é a trajetória quadro a quadro executada pelo servidor (mesmo código do offline); comparar passos/paradas apresentados pelo cliente com essa referência |
| CR10 | Validação: compilação C#/Unity sem warnings novos, EditMode, Python, 2 clientes reais headless e renderizados, capturas | Runner sincronizado por manifesto, com backup e restauração por hash |

**Regras pré-registradas, antes de qualquer medição R02:**
- **Forma:** a família perde a forma quando, em frames visíveis em movimento, o erro da reconstrução linear contra a trajetória nativa tiver p95 > 1 px ou máximo > 2 px (PPU 16) em pelo menos 2 execuções. Corrigida = p95 ≤ 1 px e máximo ≤ 2 px nas mesmas condições. Não aumentar esses limites depois de ver os dados.
- **Continuidade:** "fome" = o relógio de apresentação passou da amostra mais nova de um stream contínuo (amostras separadas por ≤ 100 ms) antes da chegada da seguinte. Reportar a fração por família. Uma correção só conta se reduzir a fração com faixas que não se sobreponham.
- **Atraso:** reportar média e p95 por família. Qualquer aumento precisa ser explicado pelo mecanismo; não é aceito só porque a fluidez melhora.
- **Custo:** mesma regra do R01. Uma diferença só conta se as faixas mín–máx das repetições não se sobrepuserem. Considerar o custo absoluto (ms/frame, % de 16,7 ms, KiB/s), não só o percentual.
- Execução inválida ou inconclusiva não conta como aprovação. Falhas e dados inconclusivos ficam preservados.

### R02 — auditoria, implementação e método de medição (pré-registro, antes das capturas)

**Auditoria do código (HEAD `43bf8d0e0`):**
- O servidor amostra cada `SpriteRenderer` de cena a 20 Hz (`TickCoopWorld`, `coopNextFrame` + 0,05 s em `Time.unscaledTime`) e envia só frames alterados.
- Renderers com `AnimatedSpriteRenderer` são sempre "alterados" (`Matches` exige `animation == 0`).
- Frames parados não são reenviados. Um renderer que volta a se mover depois de um repouso tem como amostra anterior a do início do repouso. O cliente interpola essa amostra antiga até a nova e começa o movimento antes, com um salto, exceto para inimigos acima de 0,25 s, onde há corte.
- **Relógio do cliente:** `CoopVisualClock` = `NetworkTime.time` (já atrasado em `bufferTime` = 1/60 × multiplicador dinâmico ≥ 1,5) − offset do NT do player (sendInterval × (mult − 1 + mult), com mult = 1, logo ≈ 16,7 ms). Resultado: ≈ 33–50 ms atrás do servidor.
- O intervalo do stream é de 50–67 ms, por causa da quantização em frames. **Hipótese a medir:** o relógio passa da amostra mais nova antes da próxima chegar ("fome"), com retenção e depois salto, nas famílias em movimento.
- A interpolação linear a 20 Hz entre poses nativas de 60 Hz pode perder a forma em curvas, como saltos e arcos. É medido por família.
- A geometria de consulta (CR1) já existe (`CoopCollisionViews`/`OnlineCoopCollision`, d25c34734): não foi alterada.
- Os carriers (C06, carrinho) usam o mesmo relógio: atrasar o stream inteiro desalinharia suporte e rider. Por isso não foi escolhido.

**Implementação (protocolo 65 → 66; novo `CoopMotion`):**
- Entre os frames completos de 20 Hz, o servidor envia a cada frame só a posição base (sem o offset da animação) dos renderers que transladaram.
- Só com o último frame completo visível e com a mesma rotação/escala. Mudanças de rotação, escala, visibilidade e sprite continuam no frame completo.
- **Âncora:** quando o movimento começa depois de um repouso, entra a pose aprovada do frame anterior do servidor (`anchorTime`), também nos frames completos (`TryCoopRestAnchor`).
- **Cliente:** só continua streams existentes e visíveis; ignora view removida, amostra repetida ou antiga e cena diferente; histórico limitado a 64.
- A fase da animação usa a origem do frame completo (`playbackTime`, campo interno, não serializado).
- Não reativa scripts nem física, não cria um apresentador novo e não prevê nada: são poses do servidor.
- **Chave Development `-coop-r02-legacy`:** desliga as amostras de posição e as âncoras (stream pré-R02), para A/B no mesmo binário.
- **Instrumentação Development com a captura ligada:**
  - `coop_family_shape`: reconstrução do que foi enviado contra a pose nativa quadro a quadro no servidor; início após repouso; passos e paradas nativos;
  - `coop_family_cost`: acrescenta entradas, bytes e CPU de posição;
  - `coop_family_ids`;
  - `coop_present_window` nos clientes: fome, atraso, paradas, passos, CPU, frames retidos na pausa.

**Fixture `-load-test-coop-world`** (servidor e clientes Development; `smoke_full_clients.py --coop-world`):
- 30 s de mundo nativo estável;
- pausa nativa de 2 s;
- recarga da fase (limpeza de cena + snapshot inicial novo);
- segunda pausa;
- marcadores com o relógio UTC do harness;
- PNG renderizado a cada 1 s (0,25 s durante a pausa).

**Builds** (Unity 6000.6.0f1, Development, mesmo runner, mesmos Packages):
- `baseline` = HEAD + só a fixture;
- `r02` = código R02.
- `legacy` = binário `r02` com `-coop-r02-legacy`.

**Sessões planejadas:**
1. **Piloto:** 1 execução com a captura ligada em Stage_1-1 e Stage_3-4, só para validar a fixture/captura e escolher a cena com mais famílias. Não conta como medição.
2. **Famílias** (captura ligada, `-online-motion-log-budget 2000`): r02 × 3 e legacy × 3 headless, intercaladas, + 1 renderizada de cada. A execução é inválida se houver linha descartada, limite de captura ou falha da fixture.
3. **Custo** (captura desligada: `-online-motion-logs-off -r01-frame-stats --cycle-samples`): baseline/r02/legacy × 5 headless intercaladas + baseline/r02 × 3 renderizadas.
   - Janela fixa: de "coop-world started" até "paused index=1" (30 s).
   - Validade: os 3 processos com `motionCapture=False`, sem linha Capture, sem arquivo motion.

**Critérios:** os pré-registrados acima (forma p95 ≤ 1 px / máx ≤ 2 px; fome; atraso explicado; custo pela regra de não sobreposição, com o custo absoluto).
- Custo **atribuível:** r02 − legacy (mesmo binário).
- Custo **total:** r02 − baseline.
- **Por família:** só com a captura ligada.

### R02 — piloto e decisões antes das sessões de medição

**Piloto** (`Logs/R02-pilot-20261107`, binário r02 inicial, assembly `6353db49…`, preservado em `C:/Users/mauri/SB6-R02/r02-pilot`):
- Stage_1-1 e Stage_3-4 passaram em todos os checkpoints da fixture, sem erros/avisos e sem linhas descartadas. **Não conta como medição.**
- **Forma (r02):** p95 0,1 px e máximo ≤ 0,43 px em todas as famílias.
- **"Fome" residual de 7–45%,** média de ≈ 12,7 ms por evento. Origem: streams que só animam, como tochas, BlackoutVisibleParts e inimigos parados mas animados.
  - Eles recebem só frames completos a 20 Hz (intervalo de 50 ms) com uma margem de ≈ 30–37 ms, então o relógio passa da amostra mais nova em ≈ 13 ms.
  - Para esses streams a pose não muda e a animação continua localmente a partir da origem do frame, então não há retenção de posição visível.
- **Fluxo da câmera em Stage_3-4:** a família `UrpPixelPerfectCameraFallback` é composta pelos 200 grãos de `Stage34SandstormEffect` (mais morcegos), filhos da câmera, a 112 px/s.
  - Eles geraram ≈ 950 mil entradas de posição.
  - **Sonda dirigida** com a captura desligada (`Logs/R02-probe-camera-20261107`, 1 execução de cada; reprodução, não medição de custo), em KiB/s de saída do servidor nas janelas sem transição:

| Configuração | Saída do servidor (KiB/s) |
|---|---|
| r02 | 1283–1485 |
| legacy | 975–1199 |

  - Diferença: ≈ +290 KiB/s (≈ 145 KiB/s por peer), atribuível aos grãos.

**Decisões, antes de qualquer sessão de medição:**
1. **Sobreposições de espaço de tela** (renderer com `Camera` ancestral: areia, neve, morcegos, cardume) ficam fora do stream de posição e das âncoras.
   - Elas seguem a câmera do servidor e não têm significado de gameplay.
   - Mantêm exatamente o stream de 20 Hz pré-R02.
   - Implementação: `CoopScreenSpace`, com cache por id, limpo na remoção e na troca de cena; regressão EditMode.
2. **Métrica suplementar `moveStarved`** (janela v=3): a fome contada só quando a amostra nova muda a pose.
   - A métrica pré-registrada continua sendo a principal e é reportada sem mudança.
   - A suplementar só separa o caso animação-sem-translação. Nenhum limite foi alterado.
3. O binário r02 é refeito com essas mudanças. A baseline não muda: a fixture é a mesma.

### R02 — sessão de famílias 1 (Stage_3-4) e correção do wrap das sobreposições

**Sessão** `Logs/R02-families-20261107`:
- binário r02 com a exclusão de tela, assembly `87e7c330…`, preservado em `C:/Users/mauri/SB6-R02/r02-families1`;
- r02 × 3 e legacy × 3 headless intercalados, + 1 renderizada de cada;
- 8/8 com saída 0 e todos os checkpoints da fixture; 8/8 válidas (sem descarte nem limite);
- relatório: `families.json`.

| Família | Métrica | legacy (faixa) | r02 (faixa) | Classificação |
|---|---|---|---|---|
| JunctionTurning | erro máx. (px) | 1,92–2,99 | 0,43 | **diferença medida** |
| JunctionTurning | p95 (px) | 0,5 | 0,1 | diferença medida |
| JunctionTurning | retenção no início após repouso, máx. (px) | 0,64–1,28 | 0 | diferença medida |
| JunctionTurning | fome (pré-registrada) | 0,224–0,326 | 0,100–0,129 | **diferença medida** |
| JunctionTurning | paradas no cliente | 0,033–0,050 | 0,021–0,031 | diferença medida (faixas por cliente) |
| Speeder | erro máx. (px) | 1,92–2,99 (3,2 renderizada) | 0–0,43 | diferença medida |
| Speeder | fome | 0,223–0,326 | 0,113–0,150 | diferença medida |
| BubbleChipFloat | p95 (px) | 0,8 | 0,1 | diferença medida |
| BubbleChipFloat | fome posicional | 0,27–0,38 | 0–0,006 | diferença medida |
| BubbleChipFloat | paradas no cliente | 0,13–0,19 | 0–0,006 | diferença medida (contra 0,6–1,7% de paradas nativas) |
| Sandpile | fome | 0,226–0,317 | 0,197–0,268 | não resolvida (faixas se sobrepõem) |

- **Forma pela regra pré-registrada:** o legacy passou de 2 px em 1 de 3 execuções headless em Junction e Speeder, mais 1 renderizada em Speeder (3,2 px). Speeder perde a forma em 2 execuções; Junction só em 1, o que não basta para a regra. O r02 fica em p95 ≤ 0,1 px e máximo ≤ 0,43 px em todas as famílias móveis e execuções.
- **Atraso médio:** faixas sobrepostas (legacy 62–87 ms, r02 63–84 ms). Sem aumento atribuível.
- **Atraso máximo:**
  - **r02-1:** chegou a 1,2–1,6 s em uma janela em que os 2 clientes viram ao mesmo tempo o RTT subir de 19 para 306 ms e o buffer de 36 para 200 ms. É um pico ambiental, não do mecanismo; as médias nessa execução ficaram normais.
  - **Demais execuções headless:** r02 de 99 a 221 ms, legacy de 282 a 408 ms.
  - **Renderizadas** (1 de cada): r02 307–330 ms, legacy 105–131 ms. **Inconclusivo** com 1 execução.
  - **Desvio do pré-registro:** a instrumentação de cliente registra média e máximo, não p95.
- **Pausa:** `pausedMoving` = 0 em todas as famílias que pausam. A única exceção são as sobreposições de tela, que se movem na pausa também no nativo (`Stage34SandstormEffect` usa `unscaledDeltaTime` de propósito); isso confere com o offline.
- **Achado — sobreposições de tela** (grãos de areia, família classificada como `UrpPixelPerfectCameraFallback`): erro máximo de 237–255 px **em ambas as configurações**, um defeito anterior ao R02.
  - Ao dar a volta na tela, o grão vai de uma borda à outra (descontinuidade nativa) e o cliente interpola atravessando a tela.
  - **Correção:** o servidor envia `enemyStepLimit` negativo (−4 unidades) para renderers com câmera ancestral. Não custa bytes extras.
  - O limite positivo continua significando "congela na pausa", o que essas sobreposições não fazem.
  - `CoopDiscontinuity` corta o segmento só em saltos maiores que 64 px (o wrap ou um corte de câmera), nunca no avanço de ≈ 6 px por amostra.
  - A reconstrução de forma no servidor usa a mesma função.
  - Desligado em `-coop-r02-legacy`. Regressão EditMode `CoopDiscontinuityCutsScreenSpaceWrapsButKeepsDriftAndEnemyRules`.
- O binário r02 foi refeito. A sessão de famílias é repetida com o binário final, e a sessão 1 fica preservada como evidência intermediária.

### R02 — sessão de famílias 2 e remoção do corte de wrap (sem benefício demonstrado)

**Sessão** `Logs/R02-families2-20261107`:
- binário com o corte de wrap, assembly `194bb543…`, preservado em `C:/Users/mauri/SB6-R02/r02-wrapcut`;
- EditMode 1050/1050 (`Logs/R02-tests-20261107-185049`);
- 8/8 com saída 0 e válidas; relatório `families.json`.

**Famílias móveis** (confirmam a sessão 1):

| Família | Métrica | legacy | r02 | Classificação |
|---|---|---|---|---|
| Junction | erro máx. (px) | 1,92–2,99 | 0,43 | diferença medida |
| Speeder | erro máx. (px) | 1,92–4,64 | 0–0,32 | diferença medida (**legacy perde a forma em 2/3 execuções headless**) |
| Junction | fome | 0,229–0,333 | 0,082–0,184 | diferença medida |
| Speeder | fome | 0,230–0,337 | 0,094–0,216 | diferença medida |
| Junction | fome, renderizada (1 + 1) | 0,75–0,85 | 0,31–0,35 | 1 execução de cada; mesma direção |
| BubbleChip | fome posicional | 0,27–0,39 | 0–0,006 | diferença medida |
| BubbleChip | paradas no cliente | 0,12–0,18 | 0–0,006 | diferença medida |
| BubbleChip | fome pré-registrada | 0,223–0,322 | 0,10–0,235 | não resolvida nesta sessão (faixas se sobrepõem; na sessão 1, resolvida) |
| Sandpile | fome | — | — | não resolvida |
| Estáticas/animadas (Dogman, Torch) | fome | 0,22–0,32 | 0,19–0,45 | não resolvida; esperado, pois o R02 não altera esses streams |

**Demais métricas:**
- **Atraso médio:** faixas sobrepostas (legacy 62–83 ms, r02 63–92 ms).
- **Atraso máximo headless:** legacy 127–399 ms, r02 86–396 ms.
- **Pausa:** `pausedMoving` = 0 e `pausedHeld` igual entre as configurações em todas as famílias que pausam.

**Corte de wrap das sobreposições:**
- erro máximo de 338 px no r02, contra 226–255 px no legacy;
- o corte segura a posição anterior ao wrap nos dois eixos até a próxima amostra de 20 Hz, o que gera um erro diagonal;
- o ganho seria só perceptual (some o rastro atravessando a tela) e não foi demonstrado.
- **Removido** (lição do R01: não manter mecanismo sem benefício demonstrado).

**Código final:**
- os 6 arquivos são idênticos byte a byte aos do build da sessão 1 (hashes conferidos contra o estado do sync);
- **binário final r02** = assembly `87e7c330…`, agora em `C:/Users/mauri/SB6-R02/r02`;
- EditMode com esse código: 1049/1049 (`Logs/R02-tests-20261107-181940`);
- as famílias medidas na sessão 1 são as do binário final.

**Limitação registrada (anterior ao R02, não resolvida):** as sobreposições de tela continuam perdendo a forma no wrap (erro máximo de 226–255 px, idêntico ao pré-R02, porque estão fora do stream R02). Opções para decisão humana:
- (a) stream por frame das sobreposições: +≈ 145 KiB/s por peer, medido na sonda;
- (b) um descritor aprovado de deriva + wrap no referencial da câmera do cliente, o que também resolveria a câmera do servidor diferente da do cliente;
- (c) aceitar, por ser decorativo.

## R02 — status em 07/11/2026 (mundo co-op: técnico validado; QA perceptual pendente; não publicado)

**Identidade:**
- branch `v0.5.0`, HEAD `43bf8d0e0`; mudanças locais não commitadas;
- protocolo 66; Unity 6000.6.0f1;
- binário final `C:/Users/mauri/SB6-R02/r02`: assembly `87e7c330…`, SB6.exe `b5d982ad…` (igual ao da baseline: o executável não muda, só o assembly);
- baseline `C:/Users/mauri/SB6-R02/baseline`: assembly `e182fcda…`, HEAD + fixture.

| # | Critério | Status | Evidência |
|---|---|---|---|
| CR1 | Geometria de consulta aprovada antes da previsão | **atendido**: inalterada desde d25c34734; EditMode Coop passa; capturas com props em Stage_3-4 | `Logs/R02-tests-20261107-181940` |
| CR2 | Atraso e fome por família | **medido**: atraso médio igual (faixas sobrepostas, ≈ 62–92 ms); fome reduzida nas famílias móveis (ex.: Junction 0,23–0,33 → 0,08–0,18) | `Logs/R02-families-20261107`, `Logs/R02-families2-20261107` (`families.json`) |
| CR3 | Custo por família e total contra a baseline | **medido**; detalhes abaixo da tabela | `Logs/R02-cost-20261107/cost*.json` |
| CR4 | Forma das curvas | **atendido nas famílias de gameplay**: legacy até 1,92–4,64 px (Speeder perde a forma em 2/3) → r02 p95 ≤ 0,1 px, máximo ≤ 0,43 px. **Não atendido** nas sobreposições de tela (decorativas), ver limitação | sessões de famílias |
| CR5 | Continuidade (início após repouso, término, visibilidade) | **atendido**: retenção no início após repouso de 0,64–1,28 px → 0; regressões EditMode de âncora, visibilidade, rotação/escala e origem da animação | testes `CoopMotion*` |
| CR6 | Snapshots tardios, geração, substituição, morte/disable, desconexão, troca de cena, limites e cleanup | **atendido nos caminhos tocados**: EditMode (amostra antiga/repetida, view removida, cena diferente, limite 64); recarga real 2× por execução, todas válidas. Desconexão: `ClearCoopWorld` → `ClearCoopMotion`, sem captura dedicada | testes + fixture |
| CR7 | Pausa e retomada | **atendido**: `pausedMoving` = 0 e `pausedHeld` igual ao legacy em todas as famílias que pausam; as sobreposições se movem na pausa como no nativo (`unscaledDeltaTime`) | sessões de famílias |
| CR8 | Sem reativar scripts/IA/RNG/física; sem apresentador concorrente; autoridade e offline intactos | **atendido**: publicação só no servidor, aplicação só no cliente, mesmo apresentador; revisão de código; 0 erros/avisos nos logs | diff + logs |
| CR9 | Comparação com o offline | **atendido**: referência nativa quadro a quadro no servidor; paradas no cliente ≤ paradas nativas; o legacy inventava paradas em BubbleChip (12–19% contra 0,6–1,7% nativas), o r02 fica em 0–0,6% | `families.json` |
| CR10 | Validação | **atendido**: dotnet 0 avisos e 0 erros; EditMode 1049/1049 (final) e 1050/1050 (variante com corte); Python 132/132 + 15/15; 2 clientes reais headless e renderizados; bytes dos `.cs` conferidos | ver logs |

**Detalhes do CR3:**
- **Por família, com a captura ligada:**
  - entradas e bytes de posição: Junction ≈ 200 KB, Speeder ≈ 66 KB, BubbleChip ≈ 19 KB por execução;
  - CPU de posição de 15 / 5 / 1,3 ms por execução.
- **Atribuível (r02 − legacy, mesmo binário, 5 + 5, captura desligada):**
  - servidor: +0,13 ms/frame (0,78% de 16,7 ms), +28 Mciclos/s (+4,5%), +13 KiB/s de saída (+1,1%);
  - alocações: +11–17 KiB/s;
  - clientes: sem diferença resolvida em trabalho ou ciclos.
- **Total (r02 − baseline):** servidor +8,5 KiB/s headless e +13,8 KiB/s renderizado; trabalho do servidor renderizado +0,12 ms/frame; alocações +46 KiB/s nos 3 processos.
  - Desses, +34 KiB/s vêm do campo de cliente `playbackTime` (+8 bytes por `CoopSpriteFrame`, ≈ 4000 frames/s em Stage_3-4, quase todos dos grãos de areia), presente também no modo legacy.

**Limitações:**
1. **Sobreposições de tela** (areia, morcegos): a perda de forma no wrap (erro máximo de 226–255 px) é anterior ao R02 e não foi corrigida.
   - Elas foram excluídas do stream de posição, que custaria +≈ 145 KiB/s por peer.
   - Um corte de wrap foi testado e removido: não teve benefício demonstrado e o erro máximo subiu para 338 px.
   - Opções para decisão: stream por frame, descritor de deriva + wrap na câmera do cliente, ou aceitar.
2. **Atraso:** foi registrado média e máximo, não p95 (desvio do pré-registro).
   - Máximos isolados: 1,2–1,6 s em uma execução, num pico ambiental de RTT (19 → 306 ms) visto pelos 2 clientes.
   - Renderizadas com 1 execução de cada: inconclusivo.
3. **Fome residual do r02** (≈ 8–22% nas famílias móveis, ≈ 0,4–14 ms): inclui as paradas nativas de 1 frame (≈ 21–23% dos frames), em que segurar a pose é correto. A métrica não separa esses casos.
4. Só Stage_3-4 foi medido nas sessões de famílias e custo; Stage_1-1 só no piloto. Só builds Development, só a CPU atual.
5. O desconecta/reconecta não foi capturado em execução real; coberto por revisão e pelo cleanup comum.

**Demais itens:**
- **Runner:** sincronizado e restaurado 4 vezes por manifesto, com backups em `Logs/R02-runner-sync/backup-*`. No estado final, os 6 arquivos R02 estão fora do runner (o modo Plan confirma). As outras diferenças locais do runner são reserializações automáticas da Unity anteriores ao R02 e não foram tocadas.
- **QA perceptual:** **pendente**. Pacote e roteiro em `C:\Users\mauri\SB6-QA-R02\ROTEIRO.md`, binário `C:\Users\mauri\SB6-R02\r02`.
- **Publicação:** não realizada (sem push, deploy ou publicação). R03 não iniciado.

## R02 parte 2 — implementação e pré-registro (07/11/2026, antes de qualquer medição da parte 2; R02 continua parcial; sem publicação)

**Pedido:** p95 de atraso e investigação do pico de 1,2–1,6 s; desconexão/reconexão reais; wrap das sobreposições com trajetória aprovada; alocações do campo de cliente; frequência do envio posicional; fome residual separada das paradas nativas e cobertura de cenas.

**Mudanças de código (protocolo 66 → 67):**
- **Campo de cliente fora da mensagem:** `CoopSpriteFrame.playbackTime` foi removido. O cliente guarda `CoopVisualSample` (frame + `playbackOrigin` + `cut`) só no histórico local. Os arrays de rede do servidor e dos clientes voltam ao tamanho pré-R02.
- **Reposicionamento explícito das sobreposições de tela** (`TrackCoopOverlays`, todo frame do servidor, só renderers presos à câmera):
  - um salto maior que 2 unidades (32 px) entre dois frames do servidor envia `repositionAnchors` (pose do frame anterior, fim aprovado do segmento antigo) e `repositions` (pose nova) na `CoopMotion`;
  - o cliente marca a amostra nova como corte (`cut`): segura o fim do segmento antigo e começa o novo sem interpolar pela tela, nas duas ordens de chegada (frame completo antes ou depois da mensagem);
  - nenhum RNG/gameplay roda no cliente; as sobreposições continuam sem amostras por frame (custo: 2 entradas por salto).
- **Frequência:** no máximo 1 amostra de posição por renderer por tick de envio do Mirror (sendRate 60; intervalo mínimo 0,9/60 s). Taxa maior que 60 Hz só acrescentaria bytes, sem poses mais cedo no cliente.
- **Modos Development de comparação** (`-coop-r02-motion frame|half|segments`; Release sempre `frame`):
  - `half`: 1 amostra a cada 2 frames;
  - `segments`: batimento a cada 3 frames + vértice aprovado (pose do frame anterior) quando a reta desde o início do segmento erra uma pose nativa em mais de 0,5 px;
  - a âncora após repouso passa a ser decidida pelo repouso real do frame anterior em todos os modos.
- **Métrica do cliente `coop_present_window` v=4:**
  - **retenção aprovada:** a amostra seguinte repete a pose, ou seja, o objeto nativo parou e segurar era o correto;
  - **fome real:** a pose mudou enquanto o cliente segurava;
  - atraso = defasagem de rede/linha do tempo (buffer + RTT/2 + offset visual) + retenção de apresentação (relógio − tempo do estado não interpolado);
  - idade de chegada;
  - histograma de atraso em bins de 10 ms (0–600 ms + transbordo).
- **Reconexão:**
  - `OnlineRoomBrowser`, só em Development com `-load-test-rejoin`: depois da primeira sala, entra na sala seguinte pelo mesmo `StartClient`;
  - `LocalNetworkInputSmoke`: `-load-test-leave-after S` sai pela `LeaveRoomToBrowser`;
  - log Development `[Coop] stream begin … prior*=` a cada início de stream;
  - `Tools/RoomDirectory/smoke_coop_reconnect.py`.

**Critérios pré-registrados (não serão alterados depois de ver dados):**
1. **Atraso:**
   - p95 por família e agregado (`*all*`) por cliente e execução, sobre os frames apresentados em movimento, com o limite superior do bin;
   - o r02 não pode ficar medido pior que o legacy (faixas sem sobreposição);
   - **pico:** toda janela com atraso máximo > 500 ms é classificada como dominada por retenção (retenção máxima ≥ 50% do atraso máximo) ou por defasagem de rede, e cruzada com a idade de chegada e com o RTT/buffer do mesmo segundo nos dois clientes.
2. **Reconexão:** `smoke_coop_reconnect.py` aprovado em 2 execuções headless + 1 renderizada, com:
   - sala A com 2 clientes e saída de 1 deles;
   - sala A encerrada pelo contrato dedicado;
   - os 2 clientes de volta ao navegador e na sala B;
   - fixture completa na sala B;
   - todos os inícios de stream com `prior*` = 0;
   - 0 erros/exceções.
   Qualquer falha fica preservada.
3. **Sobreposições** (família `UrpPixelPerfectCameraFallback`, Stage_3-4):
   - **corrigida:** p95 ≤ 1 px e máximo ≤ 2 px em pelo menos 2 execuções (mesma regra da forma);
   - **tráfego:** os bytes de reposicionamento da família devem ficar ≤ 5% dos bytes de frame da mesma família;
   - **sem varredura:** nenhuma amostra de corte interpolada (garantido pela regra comum de segmento) + verificação visual no pacote de QA.
4. **Alocações:** sessão de custo com a captura desligada (baseline × r02 parte 1 × r02 parte 2, 5 headless intercaladas):
   - a alocação do servidor da parte 2 deve ficar medida abaixo da parte 1 (sem sobreposição);
   - a diferença contra a baseline é relatada pela regra de não sobreposição.
5. **Modo de envio:** entre `frame`, `half` e `segments`, escolher o de menos bytes de posição que cumpra:
   - forma p95 ≤ 1 px e máximo ≤ 2 px em toda execução válida de toda família móvel;
   - fome real, p95 de atraso e paradas não medidos piores que `frame` (sem sobreposição).
   Se nenhum modo mais barato cumprir, fica `frame`. O teto de 60 Hz vale em todos os modos.
6. **Fome residual:** relatar a retenção aprovada e a fome real separadamente; só a fome real conta.
7. **Cobertura:** além de Stage_3-4, são medidas Stage_1-1 e Stage_3-6, com o modo escolhido e o legacy × 2 cada, captura ligada.

**Sessões (sequenciais; runner sincronizado por manifesto, com backup e restauração):**
- **Modos em Stage_3-4:** frame/half/segments/legacy × 3 headless intercaladas.
- **Cobertura:** Stage_1-1 e Stage_3-6.
- **Reconexão.**
- **Custo:**
  - 5 headless intercaladas: baseline, r02 parte 1 e parte 2;
  - 3 renderizadas: baseline e parte 2.
- **Validade:** sem linha descartada, sem limite de captura, fixture completa. Execução inválida não conta e fica preservada.

## R02 parte 2 — resultados (08/11/2026; R02 continua PARCIAL; sem publicação; R03 não iniciado)

**Identidade:**
- binário `C:/Users/mauri/SB6-R02/r02b`: assembly `f40db7d1…`, protocolo 67, mesmo runner/Packages (HEAD `43bf8d0e0` + 7 arquivos R02 sincronizados e restaurados);
- dotnet: 0 avisos, 0 erros;
- EditMode 1053/1053, incluindo 4 testes novos (`Logs/R02-tests-20261107-232936`);
- Python 136/136 (`Tools/Ci`) + 15/15 (`Tools/RoomDirectory`);
- `.cs` conferidos: UTF-8 estrito, sem BOM corrompido, CRLF.

**Incidente preservado:** a primeira cadeia foi interrompida por falta de memória. A restauração dela rodou antes do build, que compilou o runner já restaurado (HEAD + edições locais não rastreadas, sem nenhum código da parte 2). Esse binário inválido está em `C:/Users/mauri/SB6-R02/r02b-INVALID-runner-restored-before-build` e não foi usado. O EditMode dessa cadeia é válido: rodou antes da restauração, e os testes novos aparecem no XML.

### 1. Atraso: p95 e o pico (`Logs/R02b-modes-20261108`, 3 execuções por modo, 2 clientes)

**p95 do atraso dos frames em movimento**, agregado por cliente:

| Execuções | frame | legacy |
|---|---|---|
| 1–2 | 70–100 ms | 90–110 ms |
| 3 | 120 ms | 200–210 ms |

- As faixas se sobrepõem, então o r02 não é medido pior que o legacy (critério 1 atendido).
- Média de 60–72 ms nos dois.
- **Decomposição em regime normal (frame):**
  - defasagem de rede/linha do tempo: máximo de 80–215 ms;
  - retenção: máximo de 16–368 ms, em instantes isolados.

**Picos** (> 500 ms, `spikes.json`):
- **frame-3:** 1 segundo em 1 cliente (máximo de 502 ms, dominado por retenção).
- **Entre 04:24 e 04:32 UTC:** um evento do host afetou half-3, segments-3, legacy-3 e a execução renderizada.
  - O RTT de localhost subiu a 733/735 ms nos dois clientes (até 1558 ms em um deles).
  - O atraso apresentado chegou a 2,9 s no legacy e a 5,9 s no half.
  - Os casos dominados por retenção são lacunas de entrega: idade de chegada até 3,4 s; o cliente segurou a última pose aprovada.
  - Os dominados por defasagem são o buffer dinâmico do Mirror crescendo depois do jitter.
- **Conclusão sobre o pico de 1,2–1,6 s da parte 1:** é o mesmo padrão (RTT 19 → 306 ms visto pelos 2 clientes). É lacuna de entrega no host, com retenção aprovada mais crescimento do buffer.
  - Aparece igual no legacy, então não é causado pelo R02.
  - Nada foi alterado: extrapolar contrariaria a autoridade do servidor.

### 2. Desconexão e reconexão reais (`Logs/R02b-reconnect-20261108`): **atendido**

3/3 execuções aprovadas (2 headless + 1 renderizada; `smoke_coop_reconnect.py`):
- o cliente 2 sai pela `LeaveRoomToBrowser`; a sala A fecha pelo contrato dedicado;
- o cliente 1 é desconectado; os 2 voltam ao navegador e entram na sala B pelo `StartClient`;
- a sala B completa a fixture (pausa, recarga, segunda pausa);
- todos os 6 inícios de stream por execução têm `prior*` = 0: amostras, remoções, views, estatísticas e bombas;
- 0 erros, 0 linhas descartadas;
- a apresentação volta em movimento nas duas salas;
- capturas renderizadas: a sala B começa limpa, sem inimigos nem areia da sala A.

### 3. Wrap das sobreposições de tela (família `UrpPixelPerfectCameraFallback`, Stage_3-4): **melhorado; critério de forma NÃO atendido**

| | legacy | frame |
|---|---|---|
| Erro máximo | 200–255 px | 9,1–19,5 px |
| Frames > 2 px por execução | 28–31 mil | 0,5–3,9 mil |
| p95 | 0,8 px | 0,8 px |

- O traço pela tela no wrap acabou: corte explícito na trajetória aprovada, nas duas ordens de chegada (EditMode).
- **Tráfego:**
  - 11,5–12 mil reposicionamentos por execução;
  - 391–407 KB por execução, ou 0,75–0,78% dos bytes de frame da família (limite de 5% atendido);
  - nenhuma amostra por frame para partículas.
- **Resíduo:** 96–99% dos frames > 2 px caem nas janelas logo depois de travamentos do servidor (frame > 40 ms):
  - frame-1: 3896/3901;
  - frame-2: 459/465;
  - frame-3: 1617/1735.
- O mecanismo exato dentro do travamento não foi isolado. Fica como limitação, sem aumentar limites.

### 4. Alocações do campo do cliente: **causa removida; critério do servidor não resolvido**

**Repetição limpa `Logs/R02b-costparts-20261108`** (5 + 5, captura desligada):

| Processo | r02b − r02 (parte 1) | Classificação |
|---|---|---|
| Clientes | −26,2 / −26,3 KiB/s | diferença medida |
| Servidor | média de −27,7 KiB/s | **não resolvida**: as faixas 1837–1862 e 1813–1841 se sobrepõem em 4 KiB/s |

- Trabalho e ciclos: sem diferença resolvida.

**Renderizadas** (`Logs/R02b-cost-20261108`, 3 + 3, diferença medida):

| | r02b − baseline | parte 1 − baseline |
|---|---|---|
| Alocação do servidor | +13,6 KiB/s | +47,0 KiB/s |
| Alocação dos clientes | +21,3 / +21,6 KiB/s | +45,3 / +45,7 KiB/s |
| Saída do servidor | +22,6 KiB/s | +13,8 KiB/s |
| Trabalho do servidor | +0,17 ms/frame (1,0% de 16,7 ms) | — |
| Ciclos do servidor | +35 Mciclos/s | — |

- O aumento da saída corresponde aos reposicionamentos: ≈ 3,3 KiB/s por peer.

**Sessão headless de custo** (`Logs/R02b-cost-20261108`, 4/4/5 válidas; baseline-1 e r02-1 falharam por timeout de prontidão, preservadas): **inconclusiva**. O host alternou entre 2 estados de desempenho no mesmo binário (servidor com trabalho de 5,4–6,8 ms contra 2,4–2,8 ms). Por isso foi feita a repetição justificada acima, registrada antes de rodar.

### 5. Frequência do envio posicional: **`frame` mantido, com teto de 60 Hz**

**Por que 60 Hz:** é a taxa de envio do Mirror. Uma amostra por renderer por tick de envio é o máximo útil: amostras mais frequentes chegariam no mesmo lote.

**Regra pré-registrada aplicada** (Stage_3-4, 3 execuções por modo):
- **half:** −47% de bytes, mas perde a forma (Junction com máximo de 2,24 e 3,2 px em 2 de 3 execuções); fome real do BubbleChip 0,003 → 0,071 (medida). **Reprovado.**
- **segments:** −71% de bytes e forma mantida (máximo de 1,28 px), mas:
  - paradas do Speeder 0,032 → 0,048 (medidas);
  - fome real do BubbleChip 0,003 → 0,064.
  **Reprovado.**
- **frame:** forma com p95 de 0,1 px e máximo ≤ 0,43 px em todas as famílias de gameplay. Bytes de posição por execução de 120 s: Junction ≈ 190 KB, Speeder ≈ 64 KB, BubbleChip ≈ 20 KB, Sandpile ≈ 2,7 KB.

### 6. Fome residual separada: **feito**

Stage_3-4, frame:

| Família | "Fome" antiga (total) | Retenção aprovada | Fome real |
|---|---|---|---|
| BubbleChip | 8–15% | quase toda | 0–1% |
| Junction / Speeder | — | — | 7–15%, média de 0,7–16 ms (menos de 1 frame de folga do relógio) |
| Sobreposições | — | — | 14–28% |

- No legacy, a fome real é de 10–57%.

**Cobertura** (`Logs/R02b-coverage-20261108`, frame × legacy, 2 + 2 por cena):
- **Stage_1-1** (`EnemyMovementController`): frame com p95 de 0,1 px e máximo de 0 px; legacy com máximo de 4,32 e 1,44 px.
- **Stage_3-6** (9 famílias): frame com máximo ≤ 0,64 px; o legacy perde a forma em BigTelpio, Checker93, Heli, Roboface, Tread e UnclassifiedWorldVisual (2,4–8,2 px).
- **Fome real menor no frame:** BubbleChip 0,0025 contra 0,14–0,25.
- **Atraso dessa sessão: inconclusivo.** O RTT de localhost teve p95 de 142–518 ms nas duas configurações.

### Status da parte 2

**Atendidos:**
- reconexão;
- p95 e explicação do pico;
- fome separada;
- frequência justificada e limitada;
- causa das alocações removida (clientes resolvidos);
- cobertura com 3 cenas.

**Não atendidos:**
- **forma das sobreposições:** o wrap foi corrigido, mas o resíduo ligado a travamentos do servidor deixa o máximo em 9–20 px;
- **queda da alocação do servidor contra a parte 1:** sobreposição de 4 KiB/s.

**Pendente:** QA perceptual (`C:\Users\mauri\SB6-QA-R02\ROTEIRO.md`, atualizado).

Sem push, deploy ou publicação. R03 não iniciado.

## R02 parte 3 — pré-registro (08/11/2026; registrado antes de qualquer medição da parte 3; R02 continua PARCIAL)

**Diagnóstico do resíduo das sobreposições (pelo código, antes de medir):**
- O `BombermanNetworkManager` tem `DefaultExecutionOrder(-150)`. O `LateUpdate` dele chama `TickCoopWorld`, que amostrava o mundo co-op do servidor.
- O `Stage34SandstormEffect` (sobreposições presas à câmera) e o `CameraFollowClamp2D` movem os renderers no `LateUpdate` deles, com ordem 0, ou seja, **depois** da amostra.
- **Instante da amostra:** cada amostra trazia a pose do frame anterior com o carimbo de tempo do frame atual.
- **Fase do movimento:** a sobreposição anda por `deltaTime`. Num frame longo (Δ) o passo dela é grande, mas esse passo só entra na amostra seguinte, carimbada um frame depois. A trajetória aprovada dobra em torno do frame longo, com erro ≈ velocidade × Δ (112 px/s × 0,1 s ≈ 11 px, a mesma ordem do máximo de 9–20 px).
- **Referencial da câmera:** a posição base da sobreposição segue a câmera, que também se move depois da amostra.
- **Aplicação do wrap:** o salto do wrap também era detectado um frame atrasado.
- **Inimigos:** não são afetados, porque se movem no `Update`/`FixedUpdate`, antes de qualquer `LateUpdate`.

**Correção implementada (binário r02c, protocolo 67 inalterado, sem mudança de mensagem):**
- `CoopLateCaptureDriver` com `DefaultExecutionOrder(32000)` faz a captura do servidor depois de todos os `LateUpdate`.
- O cliente não muda.
- `-coop-r02-capture-early` só funciona em Development e restaura a fase antiga, para o A/B.
- A sonda `coop_phase_probe` (captura Development) registra, em cada frame > 40 ms e uma vez por segundo:
  - a diferença entre a pose vista na fase antiga e a pose renderizada;
  - o movimento da câmera entre as duas;
  - os wraps dentro do frame.

**Reprodução controlada:**
- Fixture de servidor `-load-test-hitch-ms 120`: dentro da janela de 30 s de regime, bloqueia a thread principal do servidor por 120 ms a cada 2,5 s, 12 vezes, em instantes registrados (`[LoadTest] coop-world hitch`).
- Execuções, sequenciais, no Stage_3-4 (2 clientes headless, captura ligada), mesmo binário r02c:
  - `early-hitch` ×2;
  - `late-hitch` ×2;
  - `late` sem hitch ×3 (regressão das famílias de gameplay);
  - mais 1 `late-hitch` renderizada para o QA visual.

**Critérios (fixados agora, sem afrouxar depois):**
- **Forma das sobreposições (`UrpPixelPerfectCameraFallback`):** p95 ≤ 1 px e **máximo ≤ 2 px** nas execuções `late-hitch` e `late`. O limite de 2 px não muda.
- **Reprodução válida:** `early-hitch` precisa reproduzir o defeito (máximo > 2 px nas janelas dos hitches registrados). Se não reproduzir, a reprodução é declarada inválida, sem conclusão.
- **Sonda:** em `early-hitch`, frames > 100 ms devem mostrar `early_late_max_px` ≥ 5 px. Em `late`, o critério de forma decide.
- **Famílias de gameplay em `late`:** máximo ≤ 2 px e p95 ≤ 1 px, como na parte 2.
- Se o critério de forma falhar em qualquer execução `late*`, a falha fica registrada e preservada, sem tolerância nova e sem repetir até passar. Repetição só com causa registrada antes (falha de ambiente comprovada, por exemplo timeout de prontidão).

**Alocações: duas afirmações separadas:**
1. **"Campo removido":** fato de código. A medição direcionada por caminho é o teste EditMode `CoopSpriteMessageAllocationsArePerPathMeasured`. Ele mede `GC.GetAllocatedBytesForCurrentThread` por mensagem de 48 frames em três caminhos:
   - array de lote do servidor (atual × elemento com o `double` removido);
   - serialização num writer reutilizado;
   - desserialização no cliente.
   A economia esperada em KiB/s vem de bytes por mensagem × frames por segundo medidos (`changed` do `coop_family_cost` da parte 2).
2. **"Redução estatisticamente demonstrada":** só com faixas de processo sem sobreposição. A bateria da parte 2 **não** demonstrou isso para o servidor e não será repetida para buscar aprovação.

**Regressão de custo total no código final:** uma única sessão headless de custo, 5 baseline + 5 r02b (parte 2) + 5 r02c, captura desligada, alternadas. Regressão = faixas sem sobreposição com r02c pior que r02b em alocação, trabalho ou saída do servidor ou dos clientes. O custo total contra a baseline é registrado na mesma sessão. Faixas sobrepostas = sem regressão demonstrada (não é aprovação de melhoria).

**Picos de localhost:** a origem é classificada pela regra a seguir, fixada antes de aplicá-la aos dados, a partir do maior `frameMs` registrado por processo em ±2 s. Transparência: a regra foi escrita no código (`delay_spikes`) antes de ser aplicada, mas este registro no documento é posterior à primeira aplicação dela às evidências da parte 2. As execuções novas da parte 3 usam a mesma regra, sem alteração:
- **"travada do servidor"** se o do servidor for ≥ 50% do atraso máximo;
- senão, **"travada do cliente"** se o do próprio cliente for;
- senão, **"atraso observado pelo Mirror (origem não isolada)"**.

Transporte e escalonamento dos processos pelo sistema operacional não são separáveis com estes logs, então nenhum pico é atribuído à rede.

## R02 parte 3 — resultados (08/11/2026; R02 continua PARCIAL: falta o aceite perceptual; sem publicação; R03 não iniciado)

**Identidade:**
- binário `C:/Users/mauri/SB6-R02/r02c`: assembly `020208c8…`, protocolo 67 (mensagens iguais às do r02b), mesmo runner/Packages (HEAD `43bf8d0e0` + 7 arquivos R02 sincronizados e restaurados);
- as strings novas foram conferidas na DLL (UTF-8 e UTF-16);
- dotnet: 0 avisos, 0 erros;
- EditMode **1055/1055** (`Logs/R02-tests-20261108-032440`), com 2 testes novos;
- Python 137/137 em `Tools/Ci`, com 1 teste novo;
- `.cs` conferidos: UTF-8 estrito, sem BOM, CRLF.

**Falhas preservadas:**
- `Logs/R02-tests-20261108-030526` e `-031435`: o teste de alocação falhou porque o Mono do Unity devolve 0 em `GC.GetAllocatedBytesForCurrentThread`.
- `Logs/R02-tests-20261108-031944`: o contador do heap não resolve arrays pequenos.
- O teste foi corrigido para medir pelo heap e só fazer asserções sobre o que é resolvível. Isso não mudou nenhum código de jogo, e o binário r02c não inclui o assembly de testes.

### 1. Resíduo das sobreposições depois de frames do servidor > 40 ms: **causa isolada e corrigida**

**Causa** (pré-registro da parte 3): a captura do servidor rodava no `LateUpdate` do manager (ordem −150), antes do `LateUpdate` da areia e da câmera (ordem 0).
- **Instante da amostra:** a pose era do frame anterior, com o carimbo do frame atual.
- **Fase do movimento:** depois de um frame longo, o passo grande entrava um frame atrasado. A trajetória dobrava.
- **Wrap:** era detectado um frame atrasado.
- **Correção:** a captura passou para depois de todos os `LateUpdate` (`CoopLateCaptureDriver`, ordem 32000). Um teste EditMode garante que nenhum `MonoBehaviour` do assembly roda depois dela.

**Reprodução controlada** (`Logs/R02c-repro-20261108`, Stage_3-4, 2 clientes): servidor bloqueado 120 ms a cada 2,5 s, 12 vezes, em instantes registrados.

| Execução | Máximo (px) | p95 (px) | Frames > 2 px | Janelas após hitch: máximo |
|---|---|---|---|---|
| early-hitch-1 / 2 (fase antiga) | 10,52 / 10,52 | 0,8 | 5142 / 5477 (87–93% nas janelas após hitch) | 10,52 |
| **late-hitch-1 / 2** | **1,13 / 1,13** | **0,8** | **0** | 1,13 |
| **late-1 / 2 / 3** (sem hitch) | **1,16 / 1,06 / 1,15** | **0,8** | **0** | — |
| **late-hitch renderizada** | **1,13** | **0,8** | **0** | 1,13 |

- **Critério de forma** (p95 ≤ 1 px, máximo ≤ 2 px; o limite de 2 px foi mantido): **atendido** nas 6 execuções `late*`.
- A reprodução é válida: `early-hitch` reproduz o defeito nas 2 execuções.
- Comparado com o r02b frame da parte 2, o erro máximo cai de 13,5 para 1,12 px (diferença medida).

**Sonda `coop_phase_probe`:** diferença entre a pose vista na fase antiga e a pose renderizada, nas execuções `late*`.
- Frames ≤ 40 ms: até 2,24 px (um passo de frame).
- Frames > 100 ms: 17–31 px (média dos máximos 13–20 px).
- **Câmera:** 0 px em todos os casos. O referencial da câmera não contribui no Stage_3-4.
- **Wraps dentro do frame longo:** 41–387 por execução, todos registrados no frame certo depois da correção.

**Desvio do pré-registro, registrado:** a sonda só roda no caminho da captura tardia. Por isso o critério "em `early-hitch`, ≥ 5 px" não pôde ser avaliado nessas execuções (0 linhas). A mesma grandeza foi medida nas execuções `late*`, onde passa com folga. A decisão continua com o critério de forma.

**Regressão das famílias de gameplay** (late × r02b frame): máximo ≤ 0,43 px e p95 de 0,1 px. Nenhuma diferença medida pior em forma, fome real, paradas ou atraso. O p95 do atraso foi de 80–90 ms (late) e 90–100 ms (late-hitch), contra 70–120 ms no r02b. Diferenças medidas em métricas de apresentação:
- CPU de apresentação do BubbleChip: 3,63 contra 2,68 µs/frame. O código do cliente é idêntico entre r02b e r02c, então é variação do host.
- Bytes de movimento do Speeder: +3,7%.

### 2. Alocações: "campo removido" × "redução demonstrada"

**Campo removido (fato de código, desde a parte 2):** o `double playbackTime` saiu do `CoopSpriteFrame`, de 168 para 160 bytes por elemento.

**Medição direcionada por caminho** (EditMode `CoopSpriteMessageAllocationsArePerPathMeasured`, mensagem de 48 frames, alocador Boehm do Mono, o mesmo do player Mono):

| Caminho | Atual | Com o campo |
|---|---|---|
| Array de lote do servidor (48 frames) | 8192 B | 8192 B |
| Serialização em writer reutilizado | 0 B | — |
| Leitura no cliente (inclui o array de frames) | 8192 B | 8192 B (estimativa) |

- **Por que é igual:** acima de ~2 KiB o Boehm aloca blocos inteiros de 4 KiB. Entre os comprimentos de lote resolvíveis (13–48 frames), só n = 25 cai num bloco a menos (4096 contra 8192 B).
- **Economia esperada no servidor:** ≈ 0 KiB/s. Um lote cheio não economiza nada, e só o último lote de uma captura (`count % 48`) pode, se tiver exatamente 25 frames. A projeção ingênua de 8 B × ~4150 frames alterados/s ≈ 32 KiB/s **não vale** com este alocador.
- Isso explica a diferença "não resolvida" do servidor na parte 2.
- **Os −26 KiB/s medidos nos clientes da parte 2 não podem ser atribuídos ao campo:** o caminho de leitura tem o mesmo arredondamento, e a parte 2 mudou outras coisas no cliente (histórico `CoopVisualSample` com 176 B por elemento, em listas reaproveitadas, e reposicionamentos).
- A bateria não foi repetida para buscar aprovação.

**Redução estatisticamente demonstrada:** nenhuma para o campo. O que está demonstrado é o custo total do R02 contra a baseline, a seguir.

**Custo total no código final** (`Logs/R02c-cost-20261108`, headless, captura desligada; 5 baseline + 5 r02b + 5 r02c alternadas; 15/15 válidas; host estável, com trabalho do servidor de 2,4–2,6 ms nas 3 configurações):

| | r02c − r02b | r02c − baseline |
|---|---|---|
| Alocação do servidor | +2,6 KiB/s, não resolvida | **+25,2 KiB/s (+1,4%), medida** |
| Alocação dos clientes | +1,4 / +0,8, não resolvida | **+23,0 / +23,2 KiB/s (+2,5%), medida** |
| Saída do servidor | +1,9 KiB/s, não resolvida | **+22,3 KiB/s (+1,8%), medida** (amostras de posição + reposicionamentos) |
| Trabalho e ciclos (servidor e clientes) | não resolvidos | não resolvidos |

- **Regressão do r02c contra a parte 2: nenhuma demonstrada.**
- O custo total do R02 contra a baseline continua medido e pequeno. Não é reivindicado como melhoria.

### 3. Picos de localhost: origem separada (`Logs/R02c-spike-origin-20261108/spike-origin.json`)

Regra pré-registrada: o maior `frameMs` registrado por processo em ±2 s, comparado com 50% do atraso máximo.

**Sessão modes da parte 2** (janelas distintas com atraso > 500 ms):

| Execução | Travada do servidor | Travada do cliente | Atraso observado pelo Mirror, origem não isolada |
|---|---|---|---|
| frame-3 | 1 (frame máximo do servidor 426 ms) | — | — |
| frame-rendered-1 | 8 (até 3,6 s) | — | — |
| half-3 | 18 (até 9,3 s) | — | 2 |
| legacy-3 | 7 (até 2,6 s) | — | — |
| segments-3 | 7 | 5 | — |
| **Total** | **41** | **5** | **2** |

**Correção da parte 2:** o pico atribuído a "lacuna de entrega no host / RTT de localhost" é, na maior parte, **travada do processo servidor**.
- O RTT do Mirror sobe junto porque o servidor responde ao ping na thread principal. O RTT não é evidência de rede.
- Nenhum pico é atribuído à rede.
- Os 2 casos não isolados ficam registrados como **atraso observado pelo Mirror**: transporte e escalonamento dos processos pelo sistema operacional não são separáveis com estes logs.

**Cobertura da parte 2:** misto de travadas do servidor (inclusive na recarga da fixture, com até 5,5–7,9 s), travadas de cliente e 4–318 entradas não isoladas por execução.

**Parte 3:** nenhum pico nas 7 execuções headless da reprodução, inclusive com hitches de 120 ms. Houve 3 entradas na renderizada, todas travadas do servidor.

### Status da parte 3

**Atendidos:**
- forma das sobreposições, com causa isolada, reprodução controlada e máximo ≤ 2 px;
- medição de alocação por caminho;
- custo total sem regressão contra a parte 2;
- origem dos picos separada.

**Não demonstrado (registrado como tal):** redução de alocação causada pelo campo removido. Pelo alocador, ela é ≈ 0 no servidor.

**Pendente:** aceite perceptual (`C:\Users\mauri\SB6-QA-R02\ROTEIRO.md`, atualizado para r02c, com o cenário de travadas). Por isso o **R02 continua PARCIAL**.

Sem push, deploy ou publicação. R03 não iniciado.

### Checkpoint de publicação R02 em homologação (08/11/2026; autorizada pelo usuário; R02 continua PARCIAL; R03 não iniciado)

**Escopo da autorização:** publicar o R02 em homologação, servidor e cliente compatíveis, pelo pipeline existente (push em `v0.5.0` → Homologation both/quick), com o reinício das salas que o deploy exige.
- A publicação **não** é aceite: o QA visual humano não foi feito e continua pendente (`C:\Users\mauri\SB6-QA-R02\ROTEIRO.md`).
- Seguem registradas as limitações de R02:
  - o campo removido não tem redução de alocação demonstrada (≈ 0 no servidor pelo alocador);
  - existe um custo total medido contra a baseline (+25/+23/+22 KiB/s);
  - há atraso observado pelo Mirror com origem não isolada em poucos picos.

**Protocolo: 65 → 67 (o 66 nunca foi publicado).**

| Versão | Situação | Conteúdo do formato de rede |
|---|---|---|
| 65 | publicado (C10, R01 e `43bf8d0e0`) | stream co-op pré-R02 |
| 66 | só local (binário r02 da parte 1) | `CoopMotion` com amostras por frame e âncoras + campo `playbackTime` dentro de `CoopSpriteFrame` |
| 67 | este commit | `playbackTime` removido do `CoopSpriteFrame`; `CoopMotion` com `repositionAnchors`/`repositions` para as sobreposições |

- **Por que 67 e não 66:** a parte 2 mudou a serialização de duas mensagens já alteradas no 66. Sem o `double`, o layout de cada frame muda. Os reposicionamentos são campos novos em `CoopMotion`.
- Um par 66 × 67 desserializaria errado em silêncio, então a versão foi incrementada de novo para que o autenticador recuse a combinação.
- A parte 3 não mudou mensagens (só a fase da captura no servidor) e manteve o 67.
- **Contrato do pipeline:** `OnlineVersionAuthenticator.ProtocolVersion = 67` e `Tools/RoomDirectory/supervisor.py PROTOCOL = 67`, conferidos pelo `release.py preflight`.
- Clientes 65 já instalados são recusados pelo servidor 67 e atualizados pelo canal do atualizador publicado no mesmo job.

**Identidade validada × commit:**
- **Runtime:** os 6 `.cs` de runtime do commit têm SHA-256 idêntico aos instalados no runner para a build `r02c` (assembly `020208c8…`, binário `b5d982ad…`).
- **Testes:** os 7 arquivos, incluindo o teste, são idênticos aos usados no EditMode 1055/1055 (`Logs/R02-tests-20261108-032440`). Os estados da sincronização estão em `Logs/R02-runner-sync/backup-20261108-030525` e `-032439`.
- **Diferenças esperadas:**
  - o CI compila um player **release**: os interruptores e sondas do R02 exigem `Debug.isDebugBuild`, e o `LocalServerLoadTest` continua fora do release;
  - a quebra de linha é normalizada pelo Git.

**Ficaram fora do commit:**
- o asset de fonte `PressStart2P-Regular SDF.asset`, alteração do usuário;
- `Packages/`, sem alterações no momento e nunca incluído;
- `Logs/` (evidências, ignoradas pelo Git);
- `Assets/_Recovery/`.

### Publicação R02 concluída — Actions 42473009178 success (08/11/2026)
Publicado em homologação com a autorização do usuário. **O R02 continua PARCIAL**: o QA visual humano não foi feito, e as limitações acima continuam valendo. R03 não iniciado.

**Commit:** `b2d90fb7bbaf11b4e8cdb6fca99761fcfe031c9e` (`fix: complete R02 co-op world motion samples and capture phase (protocol 67)`), 15 arquivos.
- 6 `.cs` de runtime, o teste EditMode, 3 docs, `smoke_full_clients.py`, `supervisor.py` (PROTOCOL 67), `smoke_coop_reconnect.py`, `r02_coop_report.py` e `test_r02_coop_report.py`.
- **Fora do commit:**
  - o asset de fonte do usuário, que continua modificado no working tree;
  - `Packages/`, com hashes inalterados: manifest `b8b74927…`, lock `803f0e80…`;
  - `Logs/`.

**Pipeline:** o push em `v0.5.0` disparou o Homologation both/quick, sucesso em 8m45s.
- Run: https://github.com/MauricioSuporte/Super-Bomberman-6-Unity/actions/runs/42473009178
- Job: https://github.com/MauricioSuporte/Super-Bomberman-6-Unity/actions/runs/42473009178/job/128707982474
- Artefatos: `SB6-Windows-2026-11-08_06-49-05` e `SB6-Validation-2026-11-08_06-49-05`.
- Nenhum outro job rodava antes do push. Nenhuma falha de pipeline e nenhuma validação contornada.

**Verificação** (`python Logs/R02-verify-publication.py` → `Logs/R02-publication.json`; manifestos copiados para `Logs/R02-publication-*.json`):
- **Servidor ativo, via SSH:** `sourceCommit=b2d90fb7…`, `protocol=67`, `benchmarkHarness=false`.
- **deploy-status:** mesmo commit, `success=true`, ativado em 2026-11-08T10:55:24Z. A ativação reiniciou as salas, como autorizado.
- **Cliente:** o `build-info.json` dentro do pacote publicado tem `sourceCommit=b2d90fb7…`, `protocol=67`, build de 2026-11-08T10:54:02Z.
- **Atualizador:** o ponteiro público da Release `online-v0.5.0` aponta para o mesmo commit.
  - Asset: https://github.com/MauricioSuporte/Super-Bomberman-6-Unity/releases/download/online-v0.5.0/SB6-Windows-2026-11-08_06-49-05.zip
  - Tamanho: 192.265.046 bytes; SHA-256 `7640c96a…`. Tamanho e hash conferem com o pacote do CI.
- **Tipos no cliente release (Mono):** `CoopLateCaptureDriver`, `CoopVisualSample` e a sonda estão presentes; `LocalServerLoadTest` está ausente, como no R01.
  - A sonda e os interruptores do R02 exigem `Debug.isDebugBuild` e ficam inertes em release.
  - A captura tardia é o comportamento padrão de release.

**Pendências mantidas após a publicação:**
- aceite perceptual humano (`C:\Users\mauri\SB6-QA-R02\ROTEIRO.md`, cenários 1–8, inclusive as travadas do servidor);
- redução de alocação do campo removido não demonstrada (≈ 0 pelo alocador);
- custo total contra a baseline medido (+25/+23/+22 KiB/s);
- poucos picos registrados como atraso observado pelo Mirror, com origem não isolada.

## R02 aprovado e abertura do R03 — 09/11/2026

O usuário solicitou explicitamente marcar R02 como aprovado e iniciar o próximo item. R02 concluído com aceite humano nos cenários testados, preservando custos, cobertura e limitações das rodadas anteriores. A publicação existente permanece b2d90fb7b/protocolo 67; esta atualização documental não inicia novo deploy.

R03 autorizado para continuidade pelo Claude Code, responsável pela implementação conforme a divisão de trabalho vigente. Auditoria inicial de escopo: caminhada, saltos e avanços aprovados de EnemyMovementController e especializações, priorizando FrogEnemyMovementController, HopRobotMovementController, SnowManMovementController e KorobokkuruMovementController; reaproveitar OnlineCoopSprites/OnlineCoopWorld e as melhorias publicadas do R02. IA, alvo, pathfinding, RNG, contato, dano e morte continuam autoritativos.

Próxima ação concreta: Claude extrair critérios R03 do plano/matriz, mapear por família a trajetória nativa e a apresentação existente e preparar reprodução dirigida. Validar forma/continuidade/pose, pausa, interrupção, snapshots tardios e cleanup, com comparação offline/online e custo aplicáveis aos critérios. Executar compilação, testes e dois clientes reais no código final, sem parar em scripts não executados. Nenhuma implementação C# ou validação R03 foi realizada nesta atualização; R03 permanece parcial. Sem autorização nova de publicação ou avanço para R05 nesta etapa.


## R03 — implementação Codex e critérios prévios (09/11/2026)

Usuário transferiu explicitamente a execução integral do R03 ao Codex. Sem publicação/commit/push nesta etapa. Baseline atual 392dd3326/protocolo67; verificar alterações anteriores de documentação e preservar. Runner livre inicialmente e ~6,9 GiB RAM livre; baseline Unity iniciada em Logs/R03-baseline.

Famílias prioritárias confirmadas por GUID de prefab/cena: Frog Stage_3-1; Hop Robot Stage_3-4; SnowMan e Korobokkuru Stage_3-5. Native Start/Fixed/AI/RNG permanecem exclusivamente na autoridade. Reusar stream R02.

Critérios antes das novas capturas: (1) frame manual aprovado não avança autonomamente no cliente, mas animações automáticas continuam; (2) corpo, sombras e efeitos usam o mesmo clock, sem congelamento fora de pausa; (3) curva comparada com referência nativa por família: p95 <=1px e máximo <=2px onde a referência densa cobre o intervalo, sem adivinhar movimento em gaps/cortes; (4) pausas em salto mantêm posição/pose e retomam; stun/morte/disable/cena não ressuscitam estado; (5) snapshots atrasados/duplicados, corte de wrap e histórico limitado testados; (6) servidor e 2 clientes reais, incluindo imagens gráficas; (7) custo/bytes medidos contra baseline com mesma instrumentação e sem captura para custo total; (8) compilação C#/Unity, suite pertinente e Python aprovados no código final, UTF-8 e warnings revisados. Resultados sem movimento/referência são inconclusivos, não aprovação.

Hipótese confirmada por leitura: CapturePlayback não inclui manualAnimationUpdate; PresentCoopAnimation avança a partir do frame manual da autoridade. Preparar regressão direta antes da correção. Avaliar publicação das transições aprovadas de visibilidade/frame manual sem esperar 20Hz; não antecipar IA nem lançar uma curva desconhecida. QA humano não será inventado; deixar roteiro/pacote se necessário.

### R03 — baseline e orçamento antes das medições finais

Baseline 392dd3326: EditMode 1062/1063; única falha em OnlineChatEditModeTests.RoomChatUsesAuthenticatedNameAndOwnsKeyboardAcrossSceneChanges, anterior ao R03. Core gameplay sem falha; não atribuir ao R03 nem esconder. Build baseline concluída. Três smokes gráficos de entrada de fase: 3-1/3-4 sem erros; 3-5 falhou por floco procedural não legível em CoopSpriteId. Causa identificada em Stage35SnowfallEffect.Apply(false,true); correção mínima mantém a textura legível e ganha regressão.

As famílias prioritárias estavam inativas nas salas não visitadas; essas três capturas não comprovam saltos. Baseline-fixture inclui o mesmo harness/probes, sem as correções de produção R03, e percorre as transições nativas. Smokes baseline-fixture 3-1 e 3-4 concluídos sem erros/avisos; preservados em Logs/R03-peers-baseline-fixture.

Orçamento adicional pré-comparação final: avaliar CPU real de captura/publicação por frame e bytes de frames/motion por família; se incremento >0,25 ms/frame de CPU de apresentação/transporte ou >10 KiB/s/cliente por família, investigar/otimizar antes de aceitar. Custo total sem captura será relatado separadamente, com repetições comparáveis; ranges sobrepostos não provam custo zero. Custos de descoberta, diagnóstico, fanout e GPU devem ser discriminados onde não isoláveis. Capturas iniciais não foram usadas para escolher tolerâncias de erro nem descartar falhas.

Sincronização temporária aplicada no runner por Logs/R03-runner-sync.ps1, backup Logs/R03-runner-sync/backup-20261109-194652. Estado ativo em state.json; restaurar após processos terminarem. Suíte final/build em execução no runner; fontes originais do usuário preservadas.

### R03 — segunda rodada e correção da água (09/11/2026)

Build `final-render` compilada, testes focados 9/9; três smokes gráficos reais concluídos (3-1, 3-4, 3-5), sem erros de execução. Referências offline nas três cenas concluídas. C# com zero erros/avisos. Python Tools/Ci 153/153 antes da nova regressão de água (agora 12 testes específicos). Suíte core anterior 1060/1060. Isso não encerra o item: medições finais precisam usar a correção de água.

A referência de renderer do servidor a 20Hz deixou candidatos isolados não resolvidos: Frog um frame divergente e Hop quatro amostras acima de 2px. Referência nativa agora por frame, cliente continua 20Hz. Não descartar candidatos nem elevar tolerâncias: a nova captura distingue subamostragem de defeito. Holds por falta de continuação aprovada são separados do erro no instante aprovado; máximos antigos incluem períodos imóveis e pausa. Não alegar ausência de travamentos por isso.

Lacuna confirmada: Frog usa PlayerWaterSubmersionEffect com material runtime e MaterialPropertyBlock; CoopSpriteFrame não transportava esses parâmetros. Adicionada receita de água aprovada (linha d’água, altura, linha e premultiplicação), com validação e restauração do material padrão. Nenhuma consulta de terreno ou IA roda no observador. Mudança de wire exige protocolo **68**; servidor publicado continua 67. Teste de round-trip inclui parâmetros inválidos. Build final-water/suíte core e medições sequenciais em execução; cadeia anterior interrompida antes de custo para não medir versão superada.

Offline: quatro famílias registradas. Desvio da fórmula ideal sem colisão em Hop/Koro não comprova defeito online: colisões nativas ajustam grid/target/direção durante o arco. Comparação real usa renderer aprovado do servidor por instante, não impõe seno ideal a colisão. Guardar essas limitações e os gaps explicitamente.

### R03 — retomada Codex em 10/11/2026

Conferido em disco: final-water passou 1064/1064 core EditMode e gerou a build; smokes gráficos 3-1/3-4/3-5 e referências offline nas três cenas concluíram. Tools/Ci 154/154 e RoomDirectory 16/16. Frog: zero divergência de sprite/água/pausa nos dois observadores, máximo de posição abaixo de 1px nos trechos verificáveis. Hop: zero divergência de sprite/água/pausa, mas até 4,36px em visuais AttachedVisual durante instantes de repouso. Portanto R03 continua parcial.

Causa identificada: o publicador novo de arestas de pose não usava TryCoopRestAnchor, que já existia na publicação periódica. Ao trocar sprite e iniciar movimento no mesmo frame, omitia o vértice de repouso; a interpolação podia começar cedo. Correção reaproveita o helper existente e adiciona regressão do caminho real de publicação. Sem elevar tolerâncias ou alterar IA/curva offline.

Lote rest-edge: core Unity/build em execução; sequência automatizada aguarda identidade da build para smokes normais, offline, smokes adicionais `-r03-enemy-interruptions` (barrel Hop, desativação real e morte de todas as quatro famílias), reconstrução da baseline comparável e três pares de custo. A captura de custo final-water foi interrompida no primeiro caso e revelou clientes sintéticos espúrios na baseline antiga; inválida. Nova baseline-guard mantém a produção pré-R03 e usa o mesmo harness com a guarda de clientes reais. Originais do runner continuam em backup reversível; não publicar nem avançar R05.

#### R03 — âncora capturada mas não enviada

Lote rest-edge compilou e passou 1065/1065; smokes gráficos concluíram, porém Hop ainda teve quatro candidatos no observador1 (até 5,76px). Investigação por linhas reais mostra a autoridade parada de 26,3209 a 26,4376 e o client interpolando cedo para a posição de 26,4543. O helper recusava a âncora porque previous.time tinha sido atualizado por uma captura periódica inalterada, nunca enviada. Removida essa dependência: vértice finito/anterior ao frame e horário do último envio são os critérios. A regressão agora usa precisamente previous.time==âncora e último envio mais antigo. Nenhum resultado anterior foi promovido a aprovação.

Lote rest-capture em execução: inclui a correção e restaura pose idle/água do Frog e pose idle do Hop após desativação, em coerência com a fase reiniciada pela limpeza. Analisador agora valida integridade da captura e conta água ativa separadamente; 15 testes específicos aprovados. Rest-edge interrompido antes das medições restantes por estar superado. Runner segue sincronizado temporariamente, com backup original preservado.

#### R03 — rest-capture: geometria confirmada em dois observadores

Build Development `C:/Users/mauri/SB6-R03/rest-capture`, Assembly-CSharp SHA256 `6578928d44125064fe7b6d76cbc5aad9d3e47b9571de0a6c3442cb3fc446dd81`, protocolo68. Core Unity 1065/1065. Três smokes gráficos completos, sem erros/avisos do servidor. Reports `Logs/R03-{frog,hop,snow}-rest-capture.json`: todas capturas íntegras; zero sprite divergente, zero movimento em pausa, zero erro de água e zero amostras acima de2px. Frog máximo0,342px e água ativa comparada em231/225 amostras dos observadores; Hop/SnowMan/Korobokkuru máximo<0,000031px nos trechos verificáveis. Gaps/transições/retencões continuam contados separadamente; não implicam perfeição em WAN.

Inspeção visual inicial Codex: PNGs do Hop em ambiente normal, blackout, olhos, visual anexado e pausa, sem corrupção visível. Isso não é aceite humano nem avaliação completa de GPU/latência Internet. Referências offline rest-capture em execução, depois interrupções por família, baseline-guard e três pares de custo. **R03 continua parcial até terminar esses critérios.**

#### R03 — definição da janela de custo (antes de examinar números de CPU/tráfego)

O harness R03 faz transição de sala/salto/stun antes da espera nativa de30s. O intervalo started→pause1 ficou maior que30s e inclui preparação. Para comparar custo de regime sem essa preparação, usar **os últimos30s imediatamente anteriores ao primeiro pause1**, em todos os três pares baseline-guard/rest-capture. Não selecionar janelas por valores observados. Nova opção `r02_coop_report.py cost --steady-seconds 30`, mantendo comportamento padrão R02 intacto, com regressões de duração inválida/insuficiente. Duração do processo cost100s (mesma para ambos) continua cobrindo toda a janela; logs e intervalos completos são preservados. Custos de transição não são representados por essa janela de regime.

Baseline-guard compilada: assembly `a709a71ae0fcc806740f1a4c7f441fb6b3b496e3aa423729c3e6f2115c5de95a`, produção pré-R03 + mesmo harness/guarda de clientes reais. Primeiro caso baseline saiu0, ainda sem análise dos números. Interrupções: todos três smokes gráficos saíram0; morte visível em ambos observadores para quatro famílias; nenhuma sombra permaneceu além do último estado visível da autoridade. Snow/Koro têm sombra nativa durante parte da morte: preservar, não apagar antecipadamente. Reports por cena em Logs/R03-entrega-20261110.

#### R03 — custo rest-capture e otimização antes da entrega

Seis casos rest-capture/ baseline-guard concluídos, três pares válidos, captura desligada e janela final30s. `Logs/R03-entrega-20261110/custo.json`: saída total servidor +21,30KiB/s (+3,20%); alocação servidor +409,33KiB/s (+23,16%); clientes +33,69/+29,90KiB/s. CPU por ciclos teve ranges sobrepostos; não provar aumento nem custo zero. Frame work cliente1 delta+0,351ms, com ranges sobrepostos e picos de parede; investigar e repetir, não declarar otimização validada ainda.

Causas de alocação redundante encontradas por revisão: consulta de shader.name em todos renderers a cada frame (trocar por referência Shader em cache) e publicação de sprite de clipes automáticos, que já avançam pela fase aprovada. Lote lowalloc mantém eventos de frames manuais/static/idle e transições de visibilidade/speed, com regressão do publicador real para manual versus automático. Core/build e nova cadeia completa em execução; baseline-guard será reutilizada somente após conferir SHA da DLL e igualdade exata do harness de input/fixtures. Custos anteriores são preservados, não substituídos por alegação de ganho sem nova medição.

#### R03 — experimento lowalloc rejeitado por frame divergente

Core lowalloc 1067/1067 e smokes gráficos completos, mas a comparação densa detectou SnowMan8 frames divergentes por observador e Korobokkuru2/3. Geometria/água/pausa continuaram corretas. Remover publicação de frames automáticos **não preserva equivalência exata**: a animação nativa muda em frames quantizados, enquanto avanço contínuo no cliente pode cruzar a borda antes/depois. Experimento rejeitado; não relaxar o analisador para aceitar.

Publicação de arestas automáticas restaurada, com regressão de ambos modos no caminho real de envio. Otimização shadercache mantém apenas a troca de comparação por shader.name por referência Shader em cache, sem mudar quais estados são enviados. Nova suíte/build/cadeia completa shadercache iniciada. Lowalloc interrompido após detectar a regressão; não usar como build de entrega. Baseline-guard a709a71a continua válida, mesmo harness. QA será copiado só da build final aceita; C:/Users/mauri/SB6-QA-R03 já contém atalhos/roteiro, ainda sem build.

#### R03 — shadercache: código final candidato e validações

Candidato atual **shadercache**, assembly `72006e013f1c8ae4f585de894a35ca773fa040680746ef2ebc2a781ad5a2aefa`, protocolo68. Publicações automáticas preservadas; só o cache de Shader é a otimização aceita para medir. Core Unity1067/1067, C#0 erros/0 avisos, Python Tools/Ci161 e RoomDirectory16 aprovados. Três smokes gráficos normais concluídos, zero divergência de sprite/pausa/água em quatro famílias e ambos clientes; Frog máximo0,163px, água ativa233/230 amostras; Hop/Snow/Koro máximo<0,000031px.

Três referências offline shadercache concluídas. Três smokes gráficos adicionais de interrupção saíram0; análise final desses logs ainda deve rodar, sem presumir aceite por marcador. Nova rodada de três pares de custo em `Logs/R03-cost-shadercache`: primeiro par já concluído, demais em andamento. Não usar custo rest-capture como custo do candidato shadercache. Baseline-guard reaproveitada com SHA/harness conferidos, sem compilação concorrente com benchmark.

Ao retomar: acompanhar `Logs/R03-measure-all.py shadercache` (processo próprio), conferir todos seis resultados e gerar custo com `--steady-seconds 30`; gerar reports normais com offline + reports de interrupções pelo analisador; conferir baseline/dados/limites, inspecionar PNGs, finalizar docs/matriz/checklist conforme cobertura real, copiar shadercache para `C:/Users/mauri/SB6-QA-R03/build` e identidade, restaurar fontes do runner por `Logs/R03-runner-sync.ps1 -Mode Restore` ao terminar processos. Sem publicar/commit/push ou avançar R05.

#### R03 — cliente: custo adicional investigado e cache de apresentação

Shadercache cumpriu geometria/pose/água, mas custo de três pares mostrou cliente1 +12,29% ciclos/s e +0,238ms work/frame; cliente2 +13,82% ciclos/s e +0,344ms work/frame, ranges separados. Alocação extra servidor caiu para+58,71KiB/s (+3,35%), contra+409,33KiB/s do lote anterior; tráfego médio+13,83KiB/s total, ranges sobrepostos (não ganho/custo zero demonstrado). Resultados guardados em Logs/R03-resultado-final-20261110/custo.json, ainda **não** custo do novo candidato.

Chamadas nativas redundantes encontradas: SetPropertyBlock(null) e reatribuição de material em cada apresentação, inclusive vistas sem água. Candidato viewcache2 mantém parâmetros aplicados por vista e id de material comum; só reaplica quando há mudança; invalida ao sair de água/blackout, remover vista e limpar cena. Novo teste do caminho real faz água→normal→material conhecido→padrão→água e verifica limpeza de caches. A primeira execução falhou só por catálogo de teste com arrays nulos; fixture inicializada como catálogo real. Core viewcache2 **1068/1068**, build assembly `2c3bb067ec602195d5dc069a05ecb4166b9cbdc3d6b1e49f55ee979732dd34f7`, protocolo68.

Três smokes gráficos e três referências offline viewcache2 concluídos. Reports em Logs/R03-viewcache2-reports: zero divergência sprite/pausa/água nos quatro tipos, Frog máximo0,420px e água ativa224/231 amostras; demais máximo<0,000031px. Interrupções e custo final em curso por Logs/R03-measure-all.py viewcache2. Não transferir números de shadercache para viewcache2. QA na tela aguardará benchmarks terminarem para não contaminar CPU. Sky/Computer Use inicializado; clientes batchmode não expõem janela interativa. A checagem visual em janelas reais deve usar clientes sem -batchmode, mantendo mesmo exe e fixture local, sem terminal por UI.


## R03 — entrega técnica final em 10/11/2026

Candidato final **viewcache2**, protocolo **68**, assembly `2c3bb067ec602195d5dc069a05ecb4166b9cbdc3d6b1e49f55ee979732dd34f7`. Implementação e validação técnica concluídas; **aceite perceptual humano pendente**, sem publicação/commit/push, sem avançar o plano. QA preservado em `C:/Users/mauri/SB6-QA-R03` (build, identidade, quatro atalhos visíveis e saves isolados). Nenhuma cena/prefab foi alterada ou depende de novo wiring.

Correções: frames manuais obedecem à autoridade; arestas de pose/visibilidade e vértices de repouso são publicados sem antecipar a locomoção; água preserva parâmetros do shader; corpo/sombra seguem o mesmo histórico aprovado; Frog/Hop limpam e reiniciam estado nas interrupções nativas. IA, RNG, colisão e dano continuam no servidor. Cache por vista evita limpar MaterialPropertyBlock/reatribuir material a cada frame; transições água/normal/blackout e remoção invalidam o cache.

Validação final: core EditMode online/COM **1068/1068**, build Development, C# **0 erros/0 avisos**, Tools/Ci **161** e RoomDirectory **16** testes aprovados. Não é a suíte inteira do projeto: a falha de chat de teclado encontrada na baseline continua registrada fora deste core. Três smokes gráficos com servidor e dois clientes Mirror reais, três referências offline nativas e três smokes de interrupções concluídos. Sem descarte por limite de captura. Em ambos clientes e nas quatro famílias: zero divergências verificáveis de sprite, movimento em pausa ou água; zero amostras verificáveis acima de2px. Frog máximo **0,420px**, água ativa **224/231** amostras; demais máximos <0,000031px (erro numérico do renderer, não garantia de precisão física). Lacunas, cortes e referência ausente continuam explicitamente separados.

Interrupções: morte nativa de Frog/Hop/SnowMan/Korobokkuru visível nos dois clientes; esmagamento por barrel aplicado nos dois Hops; desativação/reativação e recarga verificadas. Sombra Frog teve cauda de **7,1/20,6ms** após a última amostra visível da autoridade, compatível com cadências diferentes; não declarar remoção instantânea. Snow/Koro mantêm sombra durante parte da morte nativa e isso foi preservado.

Custo final: **três pares** baseline-guard/viewcache2, mesma fixture, captura desligada, últimos30s antes de pause1 (janela definida antes da análise). Baseline assembly `a709a71ae0fcc806740f1a4c7f441fb6b3b496e3aa423729c3e6f2115c5de95a`. Clientes: delta ciclos **+2,97%/+0,59%**, work/frame **−0,0177/+0,0039ms**, todos com ranges sobrepostos; não demonstram custo zero. O incremento separado de +0,238/+0,344ms da candidata shadercache foi resolvido nesta medição. Servidor: alocação **+71,97KiB/s (+4,10%)**, saída total **+42,83KiB/s (+6,55%)**, ranges separados. Saída agrega o mundo inteiro/2clientes (~21,4KiB/s por cliente), não é custo isolado de uma família. Não escondê-la como otimização de tráfego.

Evidências: `Logs/R03-entrega-viewcache2-20261110/{identity,custo,familias}.json`, `Stage_3-X-comparacao.json` e `Stage_3-X-interrupcoes.json`; capturas originais em `Logs/R03-peers-viewcache2`, `Logs/R03-offline-viewcache2`, `Logs/R03-interruptions-viewcache2`, custos em `Logs/R03-cost-viewcache2`. Os somatórios instrumentados por família excluem envelopes, âncoras e fanout, têm durações/RNG nativos diferentes e não substituem a medição total sem captura. A comparação offline valida curva/fase nativa; execuções independentes não repetem exatamente o RNG ou colisões.

Inspeção Codex: imagens finais Snow/Koro e janelas reais de ambos observadores do Hop/blackout/olhos conferidas sem corrupção evidente. Firewall resolvido manualmente pelo usuário; nenhum controle de segurança automatizado. Não equivale a aceite humano ou estudo perceptual contínuo. Limites: uma máquina Windows, Development, poucas repetições, sem GPU/WAN/jitter/produção Linux medidos, intervalos sem referência inconclusivos e transições excluídas da janela de custo de regime. Pacote e roteiro prontos para aceite humano.


### R03 — encerramento da validação técnica e restauração (10/11/2026)

Janelas reais sem batchmode: smokes adicionais Stage_3-4 e Stage_3-5 concluíram com dois clientes, zero erros/avisos do servidor, mesma DLL final. Inspeção Codex do Hop nos dois observadores, blackout/olhos e Snow/Koro em poses diferentes sem corrupção evidente; PNG final Frog/água/sombra também conferido. Não houve aceite humano nem teste manual de todos os inputs, e não foram inferidos a partir das imagens. O aviso de Firewall foi resolvido manualmente pelo usuário.

Pacote final em C:/Users/mauri/SB6-QA-R03: cópia da DLL 2c3bb067ec602195d5dc069a05ecb4166b9cbdc3d6b1e49f55ee979732dd34f7 verificada, identity.json, RESULTADO.md, LEIA-ME.md, quatro atalhos com clientes visíveis e launcher com sintaxe verificada. Antes da restauração, todos scripts de runtime no runner conferiram com identity.json e com as fontes do checkout (normalizando somente CRLF/LF).

Runner restaurado pelo sync reversível com exit0: dez arquivos originais restaurados e dois arquivos novos removidos apenas do runner. Conferência independente dos 12 hashes/ausências originais passou; estado arquivado em Logs/R03-runner-sync/backup-20261109-194652/state-restored.json. Library/build/cache não foram revertidos. Fontes implementadas permanecem no checkout principal. Sem reset/clean, Packages ou fontes/URP/settings preexistentes alterados.

Conferência final: git diff --check sem erro; UTF-8 estrito nos 11 C# alterados/novos sem BOM corrompido/duplicado ou replacement character; dotnet0avisos/0erros; Tools/Ci161/161 repetidos após empacotamento. R03 permanece com checkbox aberto exclusivamente pelo aceite perceptual humano; implementação, evidência automatizada, custo e pacote estão entregues. Nenhuma publicação/commit/push nem avanço ao próximo ID.


## R03 aprovado pelo usuário — 10/11/2026

Após receber o roteiro de QA visual do pacote C:/Users/mauri/SB6-QA-R03, o usuário confirmou: "está aprovado". Aceite perceptual registrado nos cenários testados; R03 passa a CONCLUÍDO no checklist. Implementação, core Unity1068/1068, comparação online/offline, interrupções e três pares de custo permanecem como descritos na entrega técnica viewcache2 (protocolo68, assembly2c3bb067…). Todas as limitações de amostragem, ambiente e custo permanecem; o aceite não equivale a medição WAN/GPU/Linux. Seções anteriores com aceite pendente são histórico, superado por esta seção.

Publicação do R03 continua pendente. Este aceite não executou commit/push/deploy nem iniciou outro ID.

Checklist por ID após este aceite: 28/32 itens marcados. A01–A13, C01–C10, R01–R04 e R07 marcados; A14, R05, R06 e R08 continuam parciais. A contagem descreve os checkboxes existentes, não aprovação de todas as pendências de QA global anotadas nos itens marcados. Próximo R pendente: R05; A14 também permanece aberto.


## A14 — retomada e associação do primeiro comando (10/11/2026)

Usuário autorizou terminar A14. R03 aprovado permanece no checkout; não publicar. Cruzamento com C08/R07/R01 confirma cobertura posterior de segmentos Search/Magnet/Kick, lifecycle e transporte sem presenter. Não reimplementar esses sistemas. Lacuna ainda presente em NetworkPlayerInput.FixedUpdate: múltiplas bordas iguais no mesmo lote sobrescrevem CurrentAction*Tick com o último tick, embora gameplay só receba um GetDown. Correção candidata preserva primeiro tick por botão no mesmo frame renderizado, inclusive múltiplos FixedUpdates, sem criar segundo A; próximo frame aceita novo toque. Quatro regressões cobrem overflow carrier, held-only, segundo passo físico, frame seguinte e rollover0. Ainda não compilado/rodado neste checkpoint.

Pendente real: executar regressões/corpo online+COM, smokes entre peers atuais por família com inputs e interrupções, organizar matriz/evidências e QA visual. Runner será sincronizado reversivelmente com fontes R03+A14; backup próprio em Logs/A14-runner-sync.


### A14 — primeiro lote de testes aprovado

Core online/COM1072/1072 passou no candidato input-fix (quatro casos novos de tick por frame); build Development concluída. Novo harness opt-in -load-test-bomb-abilities em LocalBombAbilitySmoke.cs + cliente sintético existente (ordem-150) preparado: A via transporte real deve plantar sem levantar; segundo A explícito levanta e release arremessa; Kick/Punch/Blue/Yellow/Rubber por ambos owners, pausa e remoção; recovery bounce nativo. Novos testes/build abilities1 em execução, ainda sem resultado. Fixtures desativam COM nesse cenário e removem/publicam apenas destrutíveis do corredor de spawn; não desabilitam colisão do client. Search/Magnet e presenter-removal reutilizam o cenário C08/R07 existente. Sem publicação. Runner temporário ativo em Logs/A14-runner-sync/state.json; restaurar no final.


### A14 — ponto de retomada do harness

Código novo salvo em Assets/Scripts/Netcode/LocalBombAbilitySmoke.cs (.meta pareado), hooks em LocalServerLoadTest e extensão de smoke_full_clients.py via --extra-args=-load-test-bomb-abilities. Cada peer atua como owner nas seis famílias; outro observa. Glove tem primeiro A plant/hold sem pickup, segundo A explícito e release/throw. Bounce usa PlayerPushedOutOfInvalidTile.NotifyExternalPushed contra um blocker local Development, pausa e recuperação. Não há IA/colisão simulada no cliente. Ainda não afirmar cenário executado.

Unity atual: python Logs/A14-unity.py abilities1, sessão66814, core+build no runner. Compilação de scripts passou sem CS errors/warnings até aqui, aguardando fim dos testes. Em seguida executar python Logs/A14-smokes.py abilities1 (normal170s, stall120ms/3s170s, field-transfer90s), após Unity terminar; script interrompe na primeira falha e preserva erro. Binário fica em C:/Users/mauri/SB6-A14/abilities1 e identidade em Logs/A14-abilities1/identity.json. Se necessário corrigir fixture, Update no sync e novo label de build; não sobrescrever provas anteriores. Ao final: comparar logs de predição/confirmação/cleanup por bombNetId/player/família, conferir capturas, matriz e pacote de QA, testes finais/UTF-8/diffcheck e Restore do runner. Ainda falta aceite perceptual A14.


A14 auditoria adicional: Bomb.StartPunch (usado também por Throw/Rubber) não tinha a guarda de pure client presente em StartKick/StartMagnetPull/TryRedirectKick. Adicionada a mesma guarda no entrypoint e estendido BombClientEntryPointsCannotStartOrRedirectNativeMotion. Ainda não está na build abilities1 nem no runner: incorporar no próximo candidato antes do fechamento. Evitar mudar fontes do runner enquanto os smokes abilities1 estão ativos. Sem publicação.


### A14 — defeito nativo reproduzido durante pausa da luva

Abilities1 passou1072/1072 e buildf89ada37. Smoke real em Logs/A14-peers-abilities1/abilities: owner1 Kick/Punch/Glove iniciados/concluídos; Blue falhou. Logs do servidor mostram input_locked desde35,087s (throw) atéBlue42s, clientmountedsemInputLocked e predição dePunch rejeitada. Causa em RestoreMovementLockToBaseline: IsGlobalLockActive incluiPause, end de coroutine pode chegar depois do último yield na borda da pausa, grava InputLocked=true e descarta baseline; pausa não desfaz esse campo no resume. Correção candidata distingue pausa global (que já bloqueia gameplay) de intro/ending, restaurando baseline quando só pausa existe e preservando locks externos/prévios. Três regressões novas do helper real. Não desbloquear ator pelo fixture.

Também corrigida exigência de checkpoints de spawn clássico no script para o fixture A14, que intencionalmente desativa aquele cenário concorrente; mantém exigência de conclusão por família/2owners. Capturas da rodada falha copiadas depois do fechamento (server/client1/client2-motion.jsonl); rodada não aceita. Fontes novas de Bomb.StartPunch guard e PowerGlove ainda aguardam Update+core/build abilities2. Ajuste de facing montado no fixture e logs de pré-condição ajudam separar erro de setup.


A14 checagem dos logs de predição da rodada abilities1 revelou setup incompleto adicional: fixture ligava AbilitySystem, mas NetworkPlayerState transmite PlayerPersistentStats.GetRuntime; owner não recebia Kick/Punch/Glove e só o Blue preparou predição. Corrigido o fixture para semear os flags de runtime por caso, antes de aguardar replicação; não mudar produção para aceitar grant local. Não chamar os três primeiros casos de prova de predição, apenas ação nativa/observador. Waiter abilities2 (PID16624 próprio, ainda aguardando build, sem clientes) parado para não iniciar smokes de setup superado. Unity abilities2 continua até resultado core/compile; depois Update e abilities3 com runtime flags corretos.


Abilities2 core1074/1075: restauração da luva e guarda nativa de Punch passaram, mas um assert novo usava MovementController.CanPredictNetworkMovement como se fosse gate de pausa. Essa propriedade descreve capacidade local; a pausa é aplicada em NetworkPlayerInput.Blocked e no loop nativo. Assert corrigido para conferir o gate real, sem alterar gameplay para atender teste incorreto. Nenhuma build/smoke abilities2 foi produzida/iniciada; waiter cancelado antes. Próximo abilities3 incorpora este ajuste e flags runtime do fixture.


### A14 — abilities3 em integração (10/11/2026)

Core1075/1075 e build Development concluídos. Assembly6a321e5b23ba4010f130efea96ce00d558322473dbfffb1eda924561a38fccee em C:/Users/mauri/SB6-A14/abilities3; protocolo68 mantido. Scripts/identidade Logs/A14-abilities3. Smokes via Logs/A14-after-build.py abilities3, sessão17564, em andamento. Primeiro owner completou Kick/Punch/Glove/Blue/Yellow/Rubber/Bounce; Blue registra locked=False, fechando o bloqueio que reproduziu após throw/pausa. Segundo owner em curso. Não presumir conclusão final ou predição só pelo checkpoint servidor.

Novo Tools/Diagnostics/analyze_bomb_abilities.py exige, por bombNetId e owner: begin+confirm para Kick/Punch/Blue/Yellow/Rubber e Lift+Throw, sem rejeições em ações válidas; observador não pode prever ação alheia e deve ter apply+samples; bounce aplicado nos dois roles; identidades iguais e capturas íntegras. Cinco testes sintéticos passaram e Tools/Ci166/166, RoomDirectory16/16. Depois dos smokes usar esse analisador em abilities e abilities-hitches, validar field-transfer pelo validador C08 existente, conferir imagens, rodar dotnet/UTF8/diffcheck e restaurar19 fontes/ausências do runner (estado ativo Logs/A14-runner-sync/state.json). Sem publicação/commit/push.


### A14 — lacuna de entrega soltava a luva indevidamente

Abilities3 completou todas sete famílias do owner1 (incluindo Blue desbloqueado após pausa), owner2 Kick/Punch; falhou "explicit second A did not lift" no Glove. Investigação real: servidor recebeu segundo A68,655s, pickup68,656/Lift e Carry68,756, mas publicou Throw68,922 enquanto os logs de input não mostram soltura e amostras antes/depois ainda têm A16. A luva chegou a pegar e arremessou antes da checagem0,4s. O timeout de held100ms zera todos botões; captura gráfica pode atrasar entrega e produzir um falso release de A.

Correção candidata em NetworkPlayerInput: expirar caminhada/auto-C, reter somente A já recebido até o próximo comando real. Pause limpa ações/direções mas conserva esse hold lógico; comandos mascarados durante pausa continuam ignorados; primeiro comando real após resume decide soltura. Reset de retorno/morte e remoção de ator continuam limpando normalmente. Nenhum GetDown é sintetizado. Nova regressão usa ApplyServerControls real e verifica A held sem Down, sem caminhada/C, neutral explícito solta. Ainda não compilado/medido; incorporar em abilities4, repetir normal/stall/field antes de aceitar. Capturas falhas3 preservadas, não aprovadas.


### A14 — outra causa medida de hitch inicial: delta anterior à predição

Análise por bombId em abilities3: owner1 tem begin+confirm para todas seis habilidades, owner2 Kick também; Punch owner2 id10 começou59,522 e foi descartado por segment_timeout59,526 (apenas4ms!), antes do RPC59,648. PresentBombMotions adicionava todo deltaTime do frame anterior ao estado recém-criado em Update; um hitch pré-input >1s extinguia a predição no próprio primeiro LateUpdate, sem confirmação nem rollback apropriado. Correção candidata marca predictedFrame nas duas entradas, preserva primeiro passo limitado e não consome delta pré-input. Timeout não confirmado passa pelo rollback explícito; expiração genérica fica para estados confirmados. Regressões reais de Kick e Punch incluem frame inicial1,2s e timeout de frame posterior. Limite inicial Lift/Yellow capado por fixedDeltaTime. Ainda não compilado: levar ao candidato abilities5 após validação4 finalizar. Não declarar abilities3 aprovado nem aceitar RPC posterior como confirmação de uma predição já extinta.


Waiter de smokes abilities4 (PID23840 próprio, ainda sem filho/sem identidade publicada) parado enquanto build4 terminava, pois a regressão comprovada de expiração no primeiro frame precisa entrar em abilities5. Não houve smoke4. Build/core4 continuam para guardar compilação dos ajustes de A held. Após saída do Unity: sync Update (inclui NetworkBombFx e NetworkBombMotion novos na lista), rodar python Logs/A14-unity.py abilities5 e depois python Logs/A14-after-build.py abilities5. Ao final aplicar analyze_bomb_abilities.py em normal e hitches; não aceitar evidência apenas nativa. Runner ainda não restaurado; não publicar.


### A14 — término Carry confirmava Throw incorretamente

Abilities5 core1076/1076/buildf00f1275. Native normal e stall concluíram ambos owners sem erros de gameplay, inclusive Glove hold/pausa. Porém analyze_bomb_abilities normal FAILED: owner1Throw não teve confirmação correspondente. Logs id5: beginThrow31,442/tick1188; confirmação31,555 veio como Carry stop/tick1191, seguida Throw primeiro segmento sem histórico. Fim de Carry com tick agrupado mais novo tomava a predição de Throw e reiniciava a fase. Guard estreito agora ignora esse trailer Carry/Lift stop no owner enquanto há Throw não confirmado; aguarda matching Throw, ack/rejeição ou timeout normal. Não ignora mudanças autoritativas worldMotion. Duas regressões cobrem confirmação com fase preservada e rejeição com collider/lock restaurados. Ainda não compilado no candidato5; próximo abilities6 após smokes5 terminarem. Não mudar o analisador para aceitar Carry como confirmação de Throw.

Metadados do smoke atualizados para identificar A14 explicitamente (spawn clássico não roda; syntheticHardwareInput=true); dados5 carregaram versão anterior do módulo com campos legacy — não usar aqueles booleans como prova de cenário, usar config extraArgs e marcadores. Também preparada opção capture_frames=False para QA humano sem custo de PNG, mantendo captura padrão intacta para as provas automatizadas.


## A14 — implementação e validação técnica finais (10/11/2026)

Status: **implementação/validação técnica concluídas; aceite visual humano pendente**. R03 continua aprovado. Protocolo68 local, sem publicação/commit/push, sem avanço ao R05. Candidato final abilities6, assembly `30bbbefd4bd8ef8457cf0ed4a67636f9203ac8bd899b006d361fad820709236b`; fonte/build/testes em `Logs/A14-abilities6`, integrações em `Logs/A14-peers-abilities6`. Pacote preservado em `C:/Users/mauri/SB6-QA-A14`, com identidade e DLL conferidas. Nenhuma cena/prefab exigiu alteração ou novo wiring.

### Correções comprovadas nesta rodada

- Comandos agrupados: um GetDown por frame conserva o primeiro tick de cada botão, inclusive entre vários FixedUpdates, e não é relabelado pelo último toque do lote. Replay continua apenas de movimento.
- A da Power Glove: silêncio de rede não equivale a soltar o botão. Só o A já recebido é retido até um comando real; caminhada e auto-C continuam expirando. Pause preserva esse hold lógico sem executar ações; reset/remoção de ator mantêm a limpeza existente. Sem fabricar GetDown.
- Lock da luva: finalizar coroutine na borda da pausa não torna InputLocked permanente; restaura baseline e preserva locks de intro/ending ou anteriores. Reproduzido em abilities1, corrigido e retomada validada.
- Predição recém-criada: não herda deltaTime do frame anterior ao input. Um hitch1,2s pré-input não pode apagar o estado em4ms antes do RPC. A expiração não confirmada passa pelo rollback explícito; primeiro passo permanece limitado.
- Carry→Throw: stop do Carry/Lift não confirma um Throw pendente nem reinicia sua fase, mesmo com tick agrupado mais novo. Confirmação correspondente preserva fase; rejeição continua restaurando collider e lock. Reproduzido em abilities5 e corrigido em6.
- Autoridade: StartPunch/Throw/Rubber agora tem a mesma guarda de pure client dos outros entrypoints nativos. Cliente continua apresentando receitas, sem iniciar gameplay.

### Matriz A14 e evidência

| Critério | Evidência final | Estado |
|---|---|---|
| Tick do primeiro comando, inclusive lote/overflow carrier/frame seguinte/rollover | Quatro testes A14BatchedPress… | aprovado |
| Gap não solta A nem duplica press; movimento/auto-C expiram | A14InputSilence… + luva real em dois owners | aprovado |
| Baseline de lock na pausa | Três A14GloveFinishingDuringPause… + pausa/retomada nas integrações | aprovado |
| Kick/Punch/Glove/Blue/Yellow/Rubber por owner/observador | 12 casos nativos por execução, begin+confirm correspondente por dono e apply+samples no outro cliente; Glove exige Lift **e** Throw | aprovado, normal e stall120ms/3s |
| Bounce do jogador | Recovery nativo contra blocker Development, pausa, recuperação de input; receipt/apply nos dois papéis; curva/fila/stop nos testes existentes | aprovado |
| Rubber determinístico/curvas, rejeição, obstáculos, replay e cleanup | Core atual inclui BombAbilityPresentation…, InitialKickPrediction…, RubberBounceSequence…, InactiveOwner… e WorldBombMotion… | aprovado |
| Search/Magnet/Kick com curva/corner/stop | Cenário C08 field-transfer; ambos clientes passaram kind=Magnet/Search/Kick | aprovado |
| Remoção do presenter no arco vivo | Mesmo cenário, ambos clientes têm8 amostras móveis após transferência | aprovado |
| Trailer Carry stop versus Throw e rejeição | Dois A14CarryStop… + confirmação Throw real nos dois owners | aprovado |
| Visual/sensação comparada ao offline | Pacote guiado e jogo livre para Search/Magnet, com roteiro | **pendente humano** |

Core Unity online+COM **1078/1078**, não a suíte inteira do projeto. Tools/Ci166 e RoomDirectory16 aprovados. Compilação dotnet final:0erros; seis avisos de dependências Mirror/Core/Editor/Examples/Edgegap, nenhum nos arquivos alterados; Packages não foram editados para suprimi-los. O primeiro --no-restore não encontrou project.assets.json após Unity limpar Temp; build normal restaurou os artefatos NuGet e passou. UTF-8 estrito verificado nos20 C# alterados/novos; diffcheck sem erros. Avisos de shutdown de ComputeBuffer já existentes não foram declarados corrigidos.

`normal-analysis.json` e `hitches-analysis.json`: PASSED, problems vazio, capturas servidor/clientes válidas. Ambos donos têm predição+confirmação de Kick/Punch/Lift/Throw/Blue/Yellow/Rubber; observadores não preveem comandos alheios e apresentam segmentos. Zero rejeições inesperadas nesses casos válidos. Não confundir checkpoints do servidor com essa prova: o analisador inicialmente recusou abilities5 por Carry confirmar Throw. Todos resultados superados/falhos foram preservados. Cinco testes do analisador impedem aprovar apenas ação nativa/observador, Lift sem Throw, outro bombId, rejeição ou captura vazia/truncada/dropped.

Inspeção Codex dos PNGs finais de throw e Yellow sem corrupção evidente. A silhueta branca do outro player ocioso corresponde a Afk2 no trace, com dead=False, não a uma morte/remoção indevida. Isso não equivale a aceite humano. QA guiado sem PNG evita custo de codificação durante a avaliação; os smokes automatizados mantêm PNGs e logs. Jogo livre separado usa menu nativo, sem inputs sintéticos. Saves isolados; não usa servidor publicado.

Limites explícitos: Windows Development, localhost, dois peers e stalls programados; não mede GPU/hardware de input/WAN/perda real, todos controles físicos ou sessão gráfica host. Offline: geometria/fórmulas e caminho nativo são cobertos pelos testes/referência autoritativa; não foi produzida uma captura offline temporal independente de A14 nesta rodada. Comparação perceptual com offline permanece no aceite humano. Não foram feitas novas medições isoladas de CPU/bytes A14; não transferir custos medidos de R03 para esta DLL nem afirmar custo zero.


### A14 — pacote executado e encerramento técnico (10/11/2026)

O próprio pacote C:/Users/mauri/SB6-QA-A14/run-qa.py normal foi executado: servidor+2clientes visíveis, sem PNG (captureFrames=false),170s, ambos owners completaram7famílias,0erros/avisos de rede do servidor. Análise adicional qa-no-png-analysis.json PASSED, sem problemas. Não é aceite humano; demonstra que o launcher de entrega realmente roda, além dos smokes gráficos com PNG e do teste C08.

Runner restaurado com exit0, conferidos independentemente os22 hashes/ausências originais:18 arquivos restaurados,4 novos removidos somente do runner. Estado em Logs/A14-runner-sync/backup-20261110-124655/state-restored.json. Não houve reset/clean nem alteração de Packages; Library/build/cache não foram revertidos. Implementação permanece no checkout principal. QA preserva a DLL30bbbefd4bd8ef8457cf0ed4a67636f9203ac8bd899b006d361fad820709236b conferida. UTF8 final20 C# válido. Comparação de runtime source antes da restauração passou (normalizando só CRLF/LF).

A14 fica aberto só pelo aceite visual/comparação perceptual offline: 1-normal.cmd (guiado, observar sem enviar comandos), 2-hitches.cmd (stalls deliberados), e jogo livre3/4/5 para Search/Magnet pelo menu nativo. Roteiro em LEIA-ME.md. Nenhuma publicação, commit/push ou avanço a outro ID.


## A14 aprovado pelo usuário — 10/11/2026

Usuário confirmou explicitamente: "Está aprovado A14." Após o roteiro fornecido e a entrega do pacote SB6-QA-A14, aceite humano registrado nos cenários testados. A14 passa a CONCLUÍDO no checklist; os registros anteriores de aceite pendente são históricos, superados por este aceite. Permanecem as evidências e limitações da entrega abilities6/protocolo68 (assembly30bbbefd…), sem ampliar o aceite para cenários não testados ou afirmar medição independente offline/GPU/WAN inexistente.

Estado dos checkboxes:29/32 marcados (90,625%). A01–A14 e C01–C10 marcados; R01–R04 e R07 marcados; R05, R06 e R08 parciais. Algumas linhas marcadas mantêm ressalvas de QA global, que não foram apagadas. Próximo item aberto na sequência: R05. Publicação de R03/A14 permanece pendente; este registro não fez commit, push, deploy nem iniciou outro ID.


## R05 — implementação iniciada (10/11/2026)

Autorização: "pode iniciar o R05 e trabalhe nele até terminar". Base limpa v0.5.0/f9c085042, Unity6000.6.0f1. R03/A14 já aprovados; R05 ainda em validação, sem publicação. Registro nativo das famílias Heli, Icicle/partículas, FishMan, Matango, BirdRobot/Gurun/impactos, Despider, Faroh, Star (reta/curva/órbita), Heart, FreezerVenus (Tornado/Ice/Doll) e Queen/coroa/neve/trilha. Reutiliza CoopSprites/CoopMotion, protocolo68 sem alteração; cliente não simula dano, homing, RNG nem colisão.

Primeiro lote: observação do ciclo de vida do renderer, captura de edges de sprite/visibilidade/rotação/escala e último ponto antes de ocultar; merge de registros antes do tick visual; guards adicionais nas fábricas Despider/Faroh; driver Development opt-in -r05-projectiles para comparar as fábricas nativas offline e servidor. Nenhum resultado aceito ainda. candidate1 falhou compilação no nome SaveSystem (namespace), corrigido global::SaveSystem em candidate2. Runner sincronizado reversivelmente em Logs/R05-runner-sync/backup-20261110-163701; restaurar ao final com R05-runner-sync.ps1 -Mode Restore. Packages conferidos idênticos após normalização de finais de linha. Próximos: coreUnity, build, dois peers renderizados em sete cenas, referências offline, análise estrita de trajeto/vida/pausa e custo comparável.


### R05 — critérios e rodada intermediária (10/11/2026)

candidate4: core1083/1083 e build passaram, smoke Stage_3-6 executou servidor e dois clientes/render; não aceito: orçamento diagnóstico8000 excedia o máximo permitido5000 e voltava ao padrão180, com drops. Captura preservada, não usada para aprovar. Detectou visibilidade inicial incompleta no impacto BirdRobot. candidate5: core1085/1085 e build passaram; smoke Stage_3-6 sem drops/erros, visibilidade corrigida por publicar após RestartAnimation e completar full frames do mesmo timestamp em ordem confiável. Uma aparente ausência foi falsa classificação: a única amostra visível do observador estava0,4ms depois da última referência nativa, ainda antes da remoção. Agora presença e erro temporal são separados; não se inventa posição após a última referência. O driver original também atirava na parede adjacente; não comprovava voo de foguete. candidate6 mede corredor de três tiles de chão nativo (remove apenas destructíveis como setup Development, igual em offline e online), fonte inativa lida pelos filhos authored sem ativar IA, impacto único Gurun registrado e morte nativa de donos BirdRobot durante voo. core/build/smokes ainda em andamento. Nenhum candidato publicado.

Critérios prévios para os próximos resultados: sem erros/avisos novos ou perdas de captura; fábricas/contatos client sem gameplay/RNG; cada família obrigatória presente em ambos os observadores, e famílias de voo com movimento nativo>0,2u. Erro posicional p95<=2px (0,125u a16PPU), sprite/visibilidade comparados com referência do servidor, pausa separada da linha do tempo que continua avançando. Efeitos visíveis nativamente por>=80ms exigem presença; mais curtos e fronteiras sem referência seguinte ficam explícitos, sem alegação de comparação interpolada. Comparação offline por lançamentos identificados: alvo/colisão/IA autônomos podem diferir, portanto distribuições não são uma aprovação automática de trajetória idêntica.

Custo a medir sem motion capture/PNG: mesma build/mesmas fontes, A/B apenas -coop-r05-legacy contra padrão, três pares por cenário selecionado, janela idêntica de fixture (inclui os mesmos lançamentos/pausa), CPU/frame/GC e wire totais do servidor com dois peers. Orçamento inicial: incremento de wire total<=20KiB/s e nenhuma regressão sustentada>10% em trabalho de frame/GC quando as faixas das repetições não se sobrepõem; se exceder, otimizar ou registrar bloqueio, não declarar ganho sem medir. Não mede GPU, WAN ou input físico. O protocolo68 e a autoridade de dano/alvo permanecem inalterados.


### R05 — retomada em 10/11/2026, candidate11 em validação

R05 permanece parcial; sem commit, push ou publicação. Runner com backup reversível ativo em `Logs/R05-runner-sync/backup-20261110-163701`.

- Candidate10: EditMode 1086/1086, build Development e smokes de Stage_2-3, Stage_World3Hall, Stage_3-6, Stage_3-4 e Stage_3-5 passaram. Stage_3-3 detectou JellyFish antecipando um frame não apresentado pelo servidor durante um quadro longo; lote interrompido. As outras três fases passaram na candidate7, mas precisam da repetição final.
- Diagnóstico adicional: TankShot deslocava 0,48 unidade durante pausa por interpolação do root recebido. A candidate11 acrescenta apresentação apenas no filho visual, congelada durante pausa e retomada após amostra nova. O preview segue spawn/direção autorizados, limitado a 150 ms / 1 unidade, sem dano/contato local. Timestamps do NetworkTransform são corrigidos pelo offset de buffer já calculado pelo projeto.
- Animações dos projéteis genéricos passam a apresentar os frames confirmados, sem inventar frames intermediários que o servidor saltou; animator nativo permanece intacto. Testes novos exercitam essa separação e pausa/retomada/limpeza do visual TankShot.
- Analisador passou a registrar e rejeitar deslocamento acima de 2 px durante pausa. Os 14 testes Python atuais passam. Dados de pausa não são usados como erro de trajetória com relógio avançando.
- Pendentes: compilação/testes da candidate11, repetição final nas nove fases, comparação offline por caso controlado, custo A/B com captura desligada, revisão de imagens, pacote QA, atualização da matriz e restauração do runner. Não considerar resultados de versões anteriores como aprovação da versão final.


### R05 — candidate12 e repetição candidate13 (10/11, 22h)

Candidate11 falhou apenas por acesso internal nos novos testes; corrigidos para reflexão, sem mudar visibilidade da API de runtime. Candidate12: core Unity1088/1088 e build Development aprovados, assembly `ae01f11317df2b54f46becf484b5ab309aaf92f47e1c978458ace004b2f6abde`. Em Stage_2-3, pausa deixou de deslocar os visuais: máximo0u nos dois clientes (112–115 amostras pausadas comparáveis), antes0,48u. Não aceito ainda: p95 posicional0,127506/0,125443u, ligeiramente acima0,125u; uma amostra inicial entre nascimento e primeiro FixedUpdate concentra o maior erro. O corredor tinha jogador adiante e os disparos terminavam após poucos passos. Candidate13 altera apenas setup Development para conferir Player/Bomb em todos os três tiles, permitindo medir voo sustentado; não amplia tolerância nem descarta a execução falha. Core/build e repetição final em andamento.

Identidade de fontes:637 scripts runtime idênticos ao checkout após normalizar finais de linha; manifests de Packages iguais. Manifesto completo em `Logs/R05-candidate12/source-comparison.json`: as demais diferenças são fontes TMP com entradas fallback nulas extras, identidade gerada da build, migração Physics2Dschema6→11 (mesmos valores/matriz, sufixo unsigned e low-level nulo) e arquivos de exemplos/testes Mirror. Sem alteração de pacotes pelo R05. Verificação semântica da física e hashes runtime em `Logs/R05-candidate13/source-comparison.json`. Build e smokes usam o mesmo binário dos dois lados; o texto OnlineBuildIdentity isolado não prova suas fontes.

Orquestração sequencial para continuar se houver interrupção: `python Logs/R05-finish.py candidate13` está aguardando a identidade da build, depois compila .NET, roda as nove fases renderizadas, referências offline e18 custos sem captura. Não executar uma segunda instância. Estado em `Logs/R05-candidate13/finish-state.json` e processos/console da sessão. Falha interrompe o lote. Preservados logs/identidades/DLL das builds antigas em `Logs/R05-retired-builds`; apenas cópias descartáveis R05 candidate4–10 foram removidas para liberar disco. Candidate12 permanece disponível. Sem publicação, commit ou push; runner ainda precisa ser restaurado ao final.


### R05 — preparação da candidate14 (10/11,22h10)

Candidate13 core1088/1088, build e compilação .NET passaram (0 erros;6 avisos em pacotes Mirror não alterados,0 nos arquivos runtime do jogo). Assembly `a37a88f0c4368399d5139115d743553fc10d1709ba7d640a1548c83705271849`. Stage_2-3 executou e a análise original falhou p95=0,1607u. Inspeção linha a linha: as maiores diferenças eram o aviso de pausa que chega ao cliente após o servidor parar, e a espera pela primeira amostra nova na retomada. O analisador excluía apenas pausa do cliente; agora separa também referência nativa pausada e registra `pauseTransitionSamples`. Não muda o limite de movimento2px, nem oculta drift durante pausa (continua avaliado e0u). Recalculando a janela de movimento: p95=0,119753/0,115912u, máximos0,129094/0,128605u,23/22 amostras válidas. Os máximos no nascimento entre FixedUpdates continuam registrados, sem afirmar precisão física subpixel. Resultado antigo preservado; gerar nova análise separada se necessário.

A medição foi fortalecida: cada família móvel precisa de>=3 posições alinhadas; trilha QueenPixelTrail exigida no Hall, sem exigir movimento dessa partícula estacionária.18 testes do analisador aprovados (inclui pausa nativa chegando antes ao observador e comparação offline por casos).

Candidate14 muda apenas probe Development: com motion capture desligada, não monta strings/amostras nem envia metadados de medição R05. O driver continua disparando os mesmos casos para o A/B. Assim o custo sem captura não inclui diagnóstico por projétil. Runner sincronizado com backup anterior mantido. `R05-finish.py candidate13` foi encerrado somente no controlador próprio antes de iniciar referências/custos; o smoke filho terminou e limpou seus processos. Nenhum offline/custo13 rodou. Controlador atual `python Logs/R05-finish.py candidate14` aguarda build14, depois .NET,9smokes renderizados,9referências offline,comparação e18custos. Não executar outra instância; verificar processos e `finish-state.json`. Core/build14 em andamento. Sem publicação; R05 continua parcial.


### R05 — build final candidate14, primeiras integrações aprovadas (10/11,22h19)

Core Unity1088/1088; build Development e .NET aprovados. Assembly `b7c70e056d6440b104901e5a24ab944853d983b3567faea568fa9f3a3cbc59fb`;637 fontes runtime idênticas ao checkout normalizando finais de linha. Compilação .NET sem erro/aviso de código do jogo;6 avisos de dependências Mirror sem alteração. Tools/Ci184/184 e analisador18/18. Protocolo68 mantido.

- Stage_2-3: aprovado em ambos. P95=0,108175/0,116366u,45 posições alinhadas por cliente; drift em pausa0u. Comparação de nascimento/movimento/pausa sem mudar tolerância.
- Stage_3-3: aprovado em ambos. FishMan e JellyFish: spriteMismatch0, drift em pausa0u; posições alinhadas85–86/137–141 amostras, erro p95<0,00000024u (erro numérico de renderer, não precisão física).
- Hall em execução, depois seis fases restantes; offline/custo ainda não começaram. A revisão de imagens já iniciou em `Logs/R05-candidate14/visual-review.json`, explicitamente sem inferir suavidade/GPU ou aceite humano de frames estáticos.

Controlador ativo `R05-finish.py candidate14` (iniciado pela sessão exec65965); estado em `Logs/R05-candidate14/finish-state.json`. Ao retomar, consultar estado/processos e saídas, não relançar o controlador se ainda ativo. Parou apenas se algum teste/análise falhar. Scripts de pacote e relatório final preparados em `Logs/R05-package.py` e `Logs/R05-final-report.py`, ainda não executados. Pacote QA-R05 ainda não existe. Runner precisa ser restaurado no fim. Cópias antigas candidate12/13 também retiradas com identidade/DLL preservadas em R05-retired-builds, para não esgotar o disco. Sem commit/push/publicação. R05 segue parcial até terminar todas as medições e entregar o aceite visual.


### R05 — integrações6/9 e controle sem PNG (10/11,22h33)

Candidate14 aprovou também Hall, Stage_3-6, Stage_3-4 e Stage_3-5. Stage_3-2 em execução, depois Stage_1-7/2-7. Ambas as visões verificadas sem divergência de sprite/visibilidade e sem drift na pausa nos relatórios aprovados; não confundir posição correta no tempo apresentado com ausência de retenção de dados.

Analisador acrescentou `movingHoldSamples/movingHoldMaxMs`: separa um último ponto parado legítimo de retenção>100ms enquanto a referência nativa se desloca mais de2px. Não muda o critério posicional; deixa retenções explícitas. Hall renderizado com PNG: um caso no cliente1 com hold259ms e frame187ms, cliente2zero casos. Precisa do controle sem PNG para isolar custo de captura; não declarar ausência geral de travamento só por erro no stateTime baixo.19 testes sintéticos do analisador passam. Cada resultado final será recalculado em arquivo separado `projectiles-analysis-final.json`, preservando o primeiro.

Custo ampliado para4cenários: Stage_3-6, Stage_3-5, Stage_2-3 e Hall (mais projéteis/partículas). Três pares por cena,24execuções ao total; Hall230s, demais120s. Mesma build/A-Bflag e janela nativa, motion capture/PNG desligados. Critérios do orçamento continuam os mesmos. Medição total de cena, não atribuição isolada de cada família.

Outro controlador próprio ativo: `python Logs/R05-after-finish.py candidate14` (sessão84240), aguardando finish-state completed. Depois roda Hall renderizado sem PNG, recalcula9análises finais, confere orçamento, cria QA-R05 e exercita um lançamento de Tank/Poyo do pacote. Não relançar simultaneamente. R05-finish14 permanece na sessão65965. Ambos scripts falham sem aprovar se faltar evidência. Após terminar, agente ainda precisa revisar comparação offline/custo/controle/imagens, atualizar checklist e matriz, verificar UTF8/diff, restaurar runner e entregar. Nenhuma publicação autorizada/feita nesta etapa.


### R05 —9/9 integrações aprovadas, offline iniciado (10/11,22h38)

Todos os9smokes da candidate14 aprovados. Evidência `Logs/R05-peers-candidate14/<cena>/projectiles-analysis.json`;6cenas de inimigos/efeitos e3cenas de boss. Dois clientes reais e servidor, renderização com PNG, ciclos de pausa/retomada/reload; captura sem drops/truncamento. Referências offline iniciaram em Stage_2-3. Custos e Hall sem PNG ainda pendentes. Checklist atualizado para refletir a implementação atual, sem marcar aceite humano inexistente.


### R05 — referências offline concluídas, custo em andamento (10/11,22h59)

Nove referências offline terminaram com a mesma build14, save isolado. `Logs/R05-candidate14/R05-comparacao.json`:96rótulos controlados correspondentes, sem labels faltantes. Revisão qualificada em `offline-review.json`: velocidades das trajetórias simples/curva fixa compatíveis, mas órbita Clown inclui movimento independente do boss e Hall tem alvos/obstáculos/impactos diferentes. Não declarar replay idêntico de RNG ou todos os sprites/tempos iguais entre partidas independentes. Um caso Tank/Poyo offline (round0) desapareceu após1amostra visível; velocidade não comparável e causa não isolada. Rounds1/2 têm voo mensurável em ambos os modos. A velocidade condicional calculada exclui pares sem deslocamento e pode diferir do valor nominal do FixedUpdate; é comparação entre os registros, não nova regra de gameplay.

Custo Stage_3-6:3pares aprovados na checagem de orçamento. Delta mediano wire+2,534KiB/s (<20); alocação servidor+1,060%, clientes+1,083%/+0,953%; work/frame com faixas sobrepostas, sem regressão resolvida. Não afirmar ganho por valores centrais negativos. Resumo parcial em `cost-Stage_3-6-partial.json`. Ainda faltam3cenários (gelo, Tank/Poyo, Hall); Stage_3-5 em execução. Controladores65965/84240 continuam ativos, sem outro Unity/SB6 concorrente. A rodada completa terá24execuções sem captura. Sem commit/push/publicação. Runner não restaurado ainda; pacote QA só será criado após orçamento e controle sem PNG.


### R05 — custo3/4cenários dentro do orçamento (10/11,23h25)

18/24execuções de custo concluídas. Stage_3-5:3pares, wire mediano-6,140KiB/s com faixas sobrepostas (não declarar ganho), maior aumento work/frame+0,029118ms no cliente2 (+1,708%, faixas separadas), GC sem regressão resolvida; dentro dos limites. Stage_2-3: primeiro par teve+29,922KiB/s e segundo-4,218KiB/s; a mediana das3repetições foi+5,525KiB/s, não extrapolar um par isolado. GC servidor+2,636%, clientes+1,386%/+1,186%, todos com faixas sobrepostas; work/frame também sobreposto. Sem problemas de medição. Resumos `cost-Stage_3-5-partial.json`/`cost-Stage_2-3-partial.json`; foguetes já descritos acima.

Halllegacy1 terminou, final1 iniciou. Faltam Hall3pares e controle renderizado sem PNG; o controlador pós-custo ainda aguarda. Mesma build14, nenhum código C# mudou desde sua compilação. O R05 permanece parcial até fechar medições/entrega técnica e aceite visual; não publicar nem avançar R06. Sessões ativas65965(finish)/84240(after-finish), verificar processos/estado se retomar. Runner com sincronização/backup ativos; restaurar só ao terminar os processos. Nenhum pacote QA-R05 criado ainda.


## R05 — entrega técnica concluída, aceite visual pendente (11/11/2026)

Retomada após limite: os controladores concluíram em10/11,23h50, sem publicar/commit/push. Nenhum Unity/SB6/controlador R05 permaneceu ativo na conferência de11/11. Não foi iniciado R06. Checklist R05 continua aberto somente pelo aceite visual humano, não por uma execução técnica pendente. Permanecem29/32 checkboxes marcados; após aceite R05 serão30/32, restando R06/R08.

### Implementação entregue

-17famílias observadas: Heli, Icicle/partículas, FishMan, Matango, BirdRobot/Gurun/impactos, Despider, Faroh, JellyFish, Furama, BubbleFurama, Star reta/curva/órbita, SunHeart, FreezerVenus Tornado/Ice/Doll, Queen neve/coroa, QueenPixelTrail e TankShot. Protocolo68 inalterado. Clientes não executam IA, homing, RNG, contato, dano ou explosão para apresentar os projéteis.
- `CoopProjectileVisual` registra o renderer inicializado, envia bordas aprovadas e endpoint antes de ocultar, reutilizando CoopSprites/CoopMotion. Frames aprovados com playback.speed0 evitam inventar um frame que o nativo saltou; o animator nativo/offline não é congelado. Preservado o primeiro frame de sombras R03 ao consumir registros pendentes.
- Poyo passou a publicar o TankShot de fato no servidor; antes o objeto existia nativamente sem spawn para os clientes. A apresentação remota do Tank/Poyo escreve somente o filho visual, corrige o offset dos timestamps Mirror, limita preview a150ms/1u e congela na pausa até amostra nova na retomada. Owner prediction A14 preservada e regressões aprovadas.
- Nenhuma cena/prefab exigiu wiring novo: registro pelas fábricas nativas e componentes runtime. Efeitos estáticos Fiber/Powder e mecanismos ambientais RocketPenguin/Vulcan continuam separados do trajeto R05, conforme notas de implementação; não transferir aceites para R06/R08.

### Evidência final

Build Development `b7c70e056d6440b104901e5a24ab944853d983b3567faea568fa9f3a3cbc59fb`. Fontes/identidade, core Unity1088/1088 e C# em `Logs/R05-candidate14/`. Compilação sem erro/aviso de código do jogo;6avisos em dependências Mirror sem alteração. Testes do analisador21/21; rodada Python CI e RoomDirectory registrada junto da conferência final.

-9smokes renderizados: Stage_2-3,3-3,World3Hall,3-6,3-4,3-5,3-2,1-7,2-7. Ambos clientes aprovados. Relatórios originais preservados; versões com as métricas finais em `Logs/R05-peers-candidate14/<cena>/projectiles-analysis-final.json`.
- P95 posicional máximo0,116366u=1,862px (TankShot). Erro comparado no tempo efetivamente apresentado, sem alegar precisão física a partir dos valores numéricos quase zero das demais famílias. Sprite/visibilidade sem divergências verificáveis; drift medido em pausa0u; efeitos breves/gaps/ausência de referência continuam explícitos. Impactos sem amostra pausada não são prova de congelamento daquela subfase.
-9referências offline e96labels correspondentes em `R05-comparacao.json`; revisão em `offline-review.json`. Um primeiro Tank/Poyo offline tem só1amostra e não comprova velocidade; os outros2casos têm voo medido. Órbita com centro móvel, alvos, impactos e caminhos do Hall podem diferir entre partidas nativas. Não houve replay determinístico integral de RNG e não foram aprovadas como idênticas todas as sequências de sprites dessas execuções independentes.
- Controle Hall renderizado sem PNG aprovado em `Logs/R05-no-png-candidate14/Stage_World3Hall/projectiles-analysis-final.json`:0retenções em movimento>100ms nos dois clientes,0drift pausado e0divergências verificáveis de sprite/visibilidade. A execução com PNG mantém1retenção259ms no cliente1, correlacionada a frame187ms; não foi apagada nem apresentada como correção de qualquer hitch de CPU/GPU/WAN.
- A métrica movingHold foi revisada para não perder retenção quando o stateTime cai num gap de referência. Também conta separadamente `pauseHistoryHoldSamples`: um ponto anterior à pausa, aguardando confirmação de retomada, não representa o jogo andando durante todo o intervalo pausado.21testes sintéticos cobrem essas bordas. Nenhuma alteração C# após a build14; apenas analisador/relatórios/documentação foram refinados nesta retomada.

### Custo A/B sem captura

24execuções:4cenários×3pares, mesma DLL, flag Development legacy contra padrão. Capture/PNG desligados e sem gerador paralelo de peers sintéticos; servidor+2clientes reais. Mede CPU/alocação/tráfego totais da cena, não GPU nem custo isolado de cada família. O braço legacy desliga publicação/preview/spawn R05; correções comuns de autoridade estão nos dois braços, não é um binário histórico completo pré-R05. Janela de cada cenário inclui o mesmo setup/lançamentos/pausa nativos, especialmente a introdução no Hall.

| Cenário | Delta mediano wire servidor | Maior aumento central de work/frame | Maior aumento central de alocação |
| --- | --- | --- | --- |
| Stage_3-6 |+2,534KiB/s | sem aumento central |+1,083% |
| Stage_3-5 |-6,140KiB/s, faixas sobrepostas |+0,029118ms /1,708%, cliente2 |+0,025% |
| Stage_2-3 |+5,525KiB/s | sem aumento central |+2,636%, faixas sobrepostas |
| World3Hall |+4,204KiB/s |+0,027711ms /1,853%, faixas sobrepostas |+4,073%, cliente2 |

Todos dentro dos limites prévios: incremento mediano total<=20KiB/s; nenhuma regressão sustentada>10% em work/frame/GC quando as faixas não se sobrepõem. Valores negativos com sobreposição não provam ganho. O primeiro par Tank teve+29,922KiB/s, preservado; o resultado das3repetições usa mediana, sem descartar o par. Evidência completa `Logs/R05-cost-candidate14/`, `cost-report.json` e `delivery.json`. Não extrapolar3pares para garantias estatísticas de toda máquina/rede.

### Pacote e restauração

`C:/Users/mauri/SB6-QA-R05` contém cópia imutável da DLL testada, ferramentas Python locais,9launchers .cmd e LEIA-ME.md. Fluxo real do pacote (servidor+2janelas, Tank/Poyo) aprovado; cada rodada cria pasta nova em runs, sem usar servidor remoto/saves normais. Identidade conferida por SHA256. Revisão de imagens em visual-review.json é do agente, não aceite perceptual humano.

Runner restaurado com `R05-runner-sync.ps1 -Mode Restore`, estado arquivado em `Logs/R05-runner-sync/backup-20261110-163701/state-restored.json`. Hash/ausência de todos os arquivos do manifesto conferidos em runner-restored.json; Library/cache e migrações geradas não foram revertidos. Checkout do usuário mantém a implementação. Nenhum reset/clean nem limpeza de arquivos alheios.

Para aceite: executar um .cmd de cada vez no pacote e observar nascimento/voo/impacto, pausa/retomada, remoção do emissor e recarga sem sprites antigos. O guia lista as famílias. Não há nova tarefa de implementação pendente neste lote; permanece o aceite visual do usuário, sem presumir que testes automatizados medem sensação, WAN ou input físico.


Conferência final em11/11: Tools/Ci187/187, analisador21/21 e RoomDirectory16/16 aprovados.25arquivos C# modificados/novos passam UTF8 estrito/BOM; git diff --check sem erros. Os46itens do manifesto de restauração têm os hashes/ausências originais; nenhum Unity/SB6/controlador R05 ativo. DLL do pacote e cópia do analisador final conferidas. `final-verification.json` registra o fechamento técnico; não repetir build/smokes para simplesmente coletar o aceite humano. Se houver problema no QA, usar a pasta runs correspondente para diagnosticar antes de alterar código. Nenhuma publicação, commit/push ou avanço R06.


## R05 — aprovado pelo usuário em 11/11/2026

Aceite explícito: "Está aprovado R05 pode marcar como concluido." R05 marcado como concluído. As seções anteriores de aceite pendente são históricas, superadas por este registro. Preservadas as evidências da candidate14/protocolo68 e todas as limitações de amostragem, offline, custo, captura e ambiente. O aceite não amplia a medição para WAN, GPU, input físico ou cenários não testados.

Checklist: **30/32 concluídos (93,75%)**; restam **R06 e R08**. Nenhum deles foi iniciado. Antes de seguir, o usuário solicitou limpeza de builds, testes e temporários no C: e no repositório. Não houve commit/push/publicação nesta atualização.


### Limpeza autorizada de artefatos antigos —11/11/2026

Após o aceite R05, usuário solicitou liberar espaço no C: antes de iniciar R06. Removidos17diretórios de builds intermediárias/duplicadas e39.813arquivos brutos obsoletos de testes (logs, capturas e imagens). Entre eles2.498originais de Movement identificados pelos logs dos testes; arquivos de Movement sem essa identificação foram preservados. Espaço efetivamente recuperado77,70GiB; C: terminou com82,09GiB livres (4,38GiB imediatamente antes da remoção; inventário inicial3,27GiB).

Preservados código e estado Git, Assets/_Recovery, Packages/ProjectSettings, saves, backups de fontes/sincronizações, scripts, relatórios JSON/XML e evidências completas finais da candidate14. Library principal/runner mantidas para evitar reimportação. Históricos/configurações/skills/credenciais Codex/Claude e temporários não identificados também preservados. Sem falhas de remoção; diff/status Git não mudaram durante a limpeza. Apenas esta documentação de aceite/limpeza foi atualizada.

**Evidências históricas anteriores:** os caminhos de raw logs/PNGs/builds antigas referenciados nas seções anteriores podem não existir mais. Relatórios resumidos e identidades permanecem; a limpeza não recalcula nem amplia aceites anteriores. Build de benchmark C:/Users/mauri/SB6-R05/candidate14 era duplicada e foi removida; a cópia aprovada permanece em **C:/Users/mauri/SB6-QA-R05**, com SHA256 conferido. Novo mapa de localização em Logs/R05-candidate14/artifact-locations.json. QA antigos R01/R03/A14 mantêm relatórios/saves e aviso de build removida.

Manifesto e auditoria: Logs/disk-cleanup-20261111-{plan,result,inventory,details}.json e script .ps1. O manifesto result registra os caminhos exatos, contagens, bytes e verificações; não executar novamente os scripts antigos que dependiam de builds apagadas sem preparar um novo binário. R05 continua CONCLUÍDO,30/32; R06/R08 ainda não iniciados. Sem publicação/commit/push nesta limpeza.


Complemento da limpeza build (11/11/2026): usuário perguntou especificamente sobre a pasta build. Inspeção confirmou que WorktreeBackups continha somente duas builds Windows antigas (não código/patches/saves); pasta removida. Validation/LocalServerLogs tiveram arquivos compilados, tar.gz, capturas e logs antigos removidos, preservando JSON/XML, scripts, rollback de prefabs e saves. Recuperados mais3,40GiB, total aproximado81,10GiB desde a limpeza; C: com85,48GiB livres. Restam apenas relatórios/dados pequenos em build. Manifesto Logs/disk-cleanup-build-20261111-result.json. Este registro supera a indicação anterior de WorktreeBackups preservado; backups reais de fontes/sincronização continuam preservados.


## R06 iniciado —11/11/2026

Autorização: executar R06, conferir Mechas que não entram na Stage_1-6 e Pretty Bomber que aparece inteira na intro Freezer Venus, sem emergência gradual do chão. Base v0.5.0/2a09bf1af, Unity6000.6.0f1. Alterações pendentes anteriores são apenas docs de aceite/limpeza R05; preservar. Runner livre, caches preservados, builds antigos removidos; QA-R05 permanece como baseline imutável.

Auditoria inicial: MechaBossSequence está no GameplayRoot de Stage_1-6. Start online inicia uma coroutine e marca sequenceStarted antes de StageIntroTransition.Start desativar esse mesmo GameplayRoot; Unity interrompe a coroutine, mas o flag impede OnEnable de reiniciá-la. Hipótese sustentada pela ordem de lifecycle, baseline real em execução para confirmar. PrettyBomberMagicEffect.Reveal usa SpriteMask, enquanto PublishPortalMasks só aceita Stage_3-4/BattleMode_7 e nome Stage34PortalFixedMask; o chão da introdução não é transportado. Também há texturas procedurais mutáveis (elipse/feixe/sombra) e material de absorção que precisam de auditoria, além das demais famílias R06. Nenhuma correção C# ainda aplicada neste checkpoint.

Baseline: python Tools/RoomDirectory/smoke_full_clients.py --server C:/Users/mauri/SB6-QA-R05/build/SB6.exe --client C:/Users/mauri/SB6-QA-R05/build/SB6.exe --output Logs/R06-baseline-Stage_1-6 --port 29871 --mode NormalGame --scene Stage_1-6 --coop-world --seconds 100 --diagnostics --require-unlocked --rendered. Processo lançado; consultar status/logs antes de executar outra Unity/build/smoke. Fonte baseline é a DLL R05 aprovada. Próximo: reproduzir/tratar lifecycle Mecha, máscara de piso e efeitos da intro, criar testes/medição R06 de movimento/poses/telegraph/áudio, validar offline/2peers/custo e pacote final. Não iniciar R08 nem publicar sem solicitação.


### R06 — candidata inicial e baseline 1-6 (11/11/2026)

- Baseline real, anterior às alterações R06: `Logs/R06-baseline-Stage_1-6/result.json`, dois clientes renderizados, 100 s, assembly `b7c70e056d6440b104901e5a24ab944853d983b3567faea568fa9f3a3cbc59fb`. Passou nos gates do harness; imagem world-025 mostra o Mecha branco já na arena. Portanto, o relato de nunca entrar não reproduziu nesta execução. O gate de players desbloqueados sozinho NÃO prova a progressão dos três Mechas.
- Correção defensiva do ciclo Start/OnEnable: adiar a primeira tentativa online dos Mechas por um frame; `StageIntroTransition.Start` pode desativar GameplayRoot e cancelar a coroutine. Não adquirir `sequenceStarted` antes dessa fase evita ficar preso ao reativar. Offline continua usando o fluxo nativo de OnEnable.
- Causa concreta da revelação da Pretty Bomber: `Pixel Portal Floor Mask` não estava na whitelist do transporte de SpriteMask. Adicionado o tipo, com centro e tamanho calculados dos bounds/pivô reais do sprite; avisos explícitos de criação e retirada, incluindo desativação antes de Destroy adiado.
- Absorção da Venus: receita render-only do material runtime `_LightAmount`, aplicada com MaterialPropertyBlock por view e retirada/limpeza explícitas. Alteração de formato `CoopSpriteFrame`: protocolo 69; requer cliente/servidor compatíveis. Nenhuma publicação autorizada nesta rodada.
- Bosses/Bombers identificados por componentes nativos (SunMask, ClownMask, FreezerVenus e MovementControllerAI.isBoss) passam a usar as bordas de pose/visibilidade existentes, inclusive máscara, cor e luz. Não há decisão de ataque, colisão ou dano no cliente.
- Harness opt-in Development `-r06-bosses`: rastros native/view e progressão nativa branco → preto → dourado usando dano autoritativo. Novas fontes ainda aguardam compilação final; NÃO considerar o harness aprovado por revisão estática.
- Validação em andamento: `Logs/R06-candidate01`, core EditMode + build Development, overlay reversível `Logs/R06-runner-sync.ps1`. Estado aplicado em `Logs/R06-runner-sync/state.json`; restaurar ao terminar. Existem alterações adicionais de instrumentação no checkout ainda não copiadas para essa primeira candidata.
- Check UTF-8 estrito dos 10 C# novos/alterados e `git diff --check` passaram. R06 permanece parcial; sem aceite inventado, push ou publicação.


### R06 — validação intermediária (11/11/2026, 06:48)

- `candidate01`: C# compilou; 1091/1092 core EditMode. Falha no novo teste: estado artificial com Quaternion zerado não se compara como rotação válida. Corrigido o fixture para Quaternion.identity; nenhum relaxamento na lógica de comparação de produção.
- `candidate02`: 1092/1092 core EditMode, build Development concluída sem warning CS novo. NÃO usar sua identidade de fontes como evidência final: ao fim da build houve atualização do overlay para candidate03 enquanto a cópia/identidade ainda terminava. Nenhum smoke foi rodado com candidate02; candidata descartada para aceite.
- `candidate03` em execução: código de jogo igual ao de candidate02; captura R06 passou a registrar o instante interpolado correto, sprite efetivamente apresentado e horizonte recebido; novo teste de roundtrip Mirror da receita + isolamento/limpeza de luz por renderer; driver offline em saves obrigatoriamente isolados via SB6_SAVE_DIRECTORY.
- `Logs/R06-candidate03/source-comparison.json`: 639 fontes runtime + os dois Packages idênticos entre checkout e runner, normalizando somente CRLF/LF. Nenhum reset/clean. CI Python 187/187 passou.
- Scripts de continuação: `Logs/R06-unity.py candidate03`; `Logs/R06-smokes.py [Stage_1-6|Stage_1-7|Stage_2-7|Stage_World3Hall]`; `Logs/R06-offline.py candidate03 [cenas]`; `Logs/R06-analyze.py <pasta-smoke>`. NÃO rodar smoke enquanto Unity testa/constrói. Os scripts de smoke apontam exclusivamente para candidate03, não candidate02.


### R06 — checkpoint de execução, 11/11/2026 06:56

- candidate03 e candidate04: 1093/1093 core EditMode (XML em cada `Logs/R06-candidate*/tests.xml`). candidate03 build concluída; candidate04 build ainda em andamento, sessão exec94381. Nenhum smoke das candidatas novas foi iniciado. NÃO editar/copiar overlay enquanto Unity/build/cópia de identidade estiverem ativos.
- Alterações adicionais no checkout, AINDA não sincronizadas para candidate04: dois sons da sequência Hall agora chamam CoopPlaySound (entrada da Pretty com ganho2, impacto dos blocos); cliente mantém ganho >1 usando PlayOneShot, sem alterar o caminho de ganhos <=1/vozes. Auditoria: Venus/Clown/Sun e Bombers restantes já usam wrappers GameAudioSettings/CoopPlaySound. Adicionado burst de até20 PNGs por revelação de máscara no harness, para capturar a subida de0,5s; reset por handle de cena, apenas Development/opt-in.
- Próximo build FINAL previsto: candidate05, depois da saída completa da sessão94381. `Logs/R06-runner-sync.ps1 -Mode Update`, então `python Logs/R06-unity.py candidate05`. O script de overlay inclui World3HallStageSevenSequence.cs. Não usar04 como build final após os ajustes de áudio.
- `Logs/R06-smokes.py` aponta exclusivamente para candidate05: Stage_1-6 (190s, três Mechas), Stage_1-7 (110s), Stage_2-7 (110s), Stage_World3Hall (220s, encontro nativo e repetição após reload), dois clientes renderizados, budget5000 (máximo aceito pelo código), limite256MiB. A captura R06 foi limitada a20Hz e bordas de visibilidade, sem spam contínuo de renderers invisíveis.
- Após smokes: analisar cada pasta com `Logs/R06-analyze.py`; `Logs/R06-offline.py candidate05` para as quatro cenas (SB6_SAVE_DIRECTORY isolado obrigatório); `Logs/R06-cost.py` compara R05 aprovado (protocolo68, build retida no QA) com final69, Sun/Clown, duas repetições, sem diagnóstico/PNG, mesmos fixtures nativos. Baseline não controla a escolha aleatória dos ataques; registrar a limitação, sem prometer equivalência perceptual/estatística.
- `Logs/R06-package.py` prepara C:/Users/mauri/SB6-QA-R06 com a build05 e quatro atalhos, apenas APÓS validar a candidata. Novo pacote não substitui R05 aprovado. Ao final, restaurar overlay via -Mode Restore e verificar hashes originais; preservar apenas build final/evidências úteis e metadados das candidatas.
- R06 continua parcial; sem aprovação manual, commit, push, publicação ou trabalho no R08.


### R06 — candidata06 consolidada (11/11/2026 07:05)

- candidate04 terminou com1093/1093 e build; candidate05 NÃO compilou: CS0619 na conversão implícita de SceneHandle para int do novo capturador. Corrigido comparando diretamente Scene; sem suppressions. Sem smoke com05.
- candidate06:1093/1093 core EditMode, build em andamento, sessão75107. Não há alteração C# pendente fora do runner nesta candidata. `Logs/R06-candidate06/source-comparison.json`:11 C# novos/alterados com UTF-8 estrito válido; fontes runtime/Packages idênticos entre main/runner normalizando apenas CRLF. diff --check passou.
- TODOS os scripts de smoke/custo/pacote agora apontam para candidate06. Após a saída COMPLETA da sessão75107 (incluindo cópia e identity.json), iniciar `python Logs/R06-smokes.py`. Não lançar outro Unity ou smoke em paralelo.
- Depois: `python Logs/R06-offline.py candidate06`; `python Logs/R06-cost.py`; `python Logs/R06-cost-report.py candidate06`; analisar as quatro pastas R06-peers-candidate06-<scene> via `Logs/R06-analyze.py`. Captura offline e todos os novos smokes/custos usam SB6_SAVE_DIRECTORY isolado por execução, não o save do usuário.
- Preparador QA `Logs/R06-package.py` também referencia06. NÃO executar/aceitar o pacote antes de avaliar os smokes finais e as imagens (especialmente r06-reveal-*.png no Hall). Candidate02/03/04 são builds intermediárias; remover seus binários quando a final estiver validada, mantendo logs/XML/identidades pequenas.
- Overlay aplicado segue em Logs/R06-runner-sync/state.json. Restaurar e verificar hashes ao final. Checklist R06 permanece parcial; nenhum aceite/push/deploy/R08.


### R06 — smoke final iniciado (11/11/2026 07:09)

- candidate06 build completa e sessão75107 encerrada com exit0. Assembly final:72ab9f3cc093ea6747e3b640427f67d967c0d2ac5fd66c6705fe796682ca7fde. Todos os1093 core EditMode aprovados;187 CI Python aprovados. Código estável desde a correção SceneHandle→Scene.
- `python Logs/R06-smokes.py` rodando na sessão44908: primeira cena Stage_1-6, depois Stage_1-7, Stage_2-7, Stage_World3Hall, em sequência. Não abrir outra Unity/smoke enquanto essa sessão estiver ativa. Pastas `Logs/R06-peers-candidate06-<cena>`.
- Stage_1-6 já registrou `r06 mecha combat index=0`,1,2, `r06 mecha sequence verified`, pausa1/retomada/reload/pausa2/retomada e `coop-world completed`. Imagem client-1 world-060 mostra o dourado em combate. Isso é evidência de progressão nativa + imagem; a medição dos dois observadores ainda aguarda fechamento do smoke/copiar JSONL.
- Após fechar cada cena, rodar `python Logs/R06-analyze.py Logs/R06-peers-candidate06-<cena>` para diagnóstico. Não confundir o sucesso do harness com aceite visual/posicional. Se falhar ou der inconclusivo, investigar antes de marcar R06 entregue.
- Restantes previstos: offline quatro cenas (`Logs/R06-offline.py candidate06`), custo baseline/final sem PNG/captura (`Logs/R06-cost.py` e `Logs/R06-cost-report.py candidate06`), revisão visual da subida Pretty nos PNGs r06-reveal, pacote QA (`Logs/R06-package.py`), docs/matriz e restauração de overlay. Sem push/publicação/R08.


### R06 — achados reproduzidos e candidata07 (11/11/2026 07:21)

- Sessão44908 encerrou exit0: quatro fixtures reais com dois clientes renderizados em06, sem erros nos gates de execução. SunMask passou na comparação dos dois observadores. Mechas/Clown/Venus têm divergências de sprite, ainda NÃO aceitas: exame das linhas mostrou que a amostragem R06 omitia a borda de sprite entre amostras de posição (intervalos até100ms). Candidate07 registra bordas de sprite/máscara/luz imediatamente, mantendo20Hz só para posição estável. Não houve relaxamento do comparador.
- Mechas06:16/16 entidades visíveis cobertas em cada observador, posição p95=0,06230 e0,06197 unidades (<1pixel), zero divergência de visibilidade. Isso não anula a checagem de poses pendente.
- Hall06: PNGs r06-reveal mostram nenhuma Pretty → cabeça parcial → corpo completo, porém cenário preto durante parte da máscara. Incerto se defeito do render de captura ou do jogo. Não aceitar QA ainda. Geometry07 preserva Z da escala em1 (bounds2D têm espessura0; não criar matriz singular); assert explícito no teste. Revisar imagens na repetição07 e, se necessário, investigar range/material/captura.
- REPRODUZIDO offline em06: `Logs/R06-offline-candidate06/Stage_1-6/failure.json` e offline-motion.jsonl, ~30110 linhas sem qualquer Mecha visível, `InvalidOperationException: R06 Mecha never entered combat: 0`. Encerrado somente o processo próprio SB6pid24308 após guardar a evidência; sessão33625 terminou com falha esperada. Não esperar240s de um processo cujo driver já falhou.
- Causa offline: primeiro OnEnable precede Start; sem reativação posterior, Start offline não tentava iniciar. Em07, o agendamento de um frame ao fim de Start agora vale para TODA simulação nativa (ShouldSimulateLocally), não apenas servidor co-op. OnEnable continua podendo iniciar e sequenceStarted evita duplicação. Clientes puros permanecem fora.
- Candidate07 test/build em andamento na sessão21457, overlay já atualizado. Esperar o encerramento completo antes de smoke/offline. Scripts R06-smokes/cost/package agora apontam para07. Após build, PRIORIDADE: offline07 Stage_1-6 para verificar correção reproduzida; Hall07 para verificar cenário/máscara; depois todos os bosses/poses.
- Acrescentado `Logs/R06-portals.py`: dois peers renderizados portal7/BattleMode_7 e portal34/Stage_3-4, por compartilharem o transporte de máscaras. Rodar em série, sem outra Unity/SB6.
- Pendentes adicionais: offline quatro cenas com07; custo07 (duas repetições baseline/final Sun/Clown, sem PNG/diagnóstico); relatório `R06-cost-report.py candidate07`; QApackage07; docs/matriz, restaurar overlay e hashes, excluir apenas builds intermediárias geradas por nós. Nenhum commit/push/publicação/R08 autorizado.


### R06 — retomada e candidata08 (11/11/2026 10:40)

-07 terminou1093/1093 +build. Offline07 Stage_1-6 PASSOU: branco/preto/dourado, sequência verificada e R06Offline completed. Corrige o bloqueio reproduzido no06. Logs em R06-offline-candidate07/Stage_1-6.
-Hall07: harness com2clientes passou; o comparador antigo apontava erro espúrio por considerar só sprites numa vizinhança de50ms e descartar a pose ANTERIOR ainda válida (ex.: anterior60ms atrás, próxima40ms à frente). Comparação exata sample-and-hold em3823/3822 amostras dos clientes encontrou ZERO divergência. Novo Tools/Diagnostics/analyze_bosses.py usa último estado <=ptime, exclui gaps>150ms e pausa de forma explícita, preserva contagem antiga como neighborhoodSpriteMismatch.6 testes sintéticos passaram, incluindo pose futura que DEVE falhar. Logs/R06-analyze.py agora delega ao novo módulo; não relaxar o estado discreto para um sprite futuro.
-Hall07 imagens via SubmitRenderRequest continuam com cenário preto em parte do burst. Uma observação Computer Use da janela real, sem capturas internas, mostrou cenário e absorção normais, mas não estabeleceu toda a fase mascarada. Portanto ainda é inconclusivo, não presumir corrigido por Z=1.
-Candidate08 em tests/build, sessão54133. Única diferença C#07→08: captura Development opt-in -r06-backbuffer-captures usa ScreenCapture.CaptureScreenshot do end-of-frame real, em clientes VISÍVEIS sem -batchmode, evitando render manual adicional. Sem mudança de gameplay/receita/protocolo (69).
-Scripts smokes/cost/package/portals apontam para08. Após a saída COMPLETA54133, executar Logs/R06-visible.py: servidor +2clientes visíveis,150s, saves isolados, PNGs reais em Logs/R06-backbuffer-hall-candidate08/client-N-frames/r06-reveal*.png. Conferir início, meio e fim de subida em ambos; cenário deve permanecer visível. Analisar pelo novo módulo.
-Depois: Logs/R06-smokes.py Stage_1-6 Stage_1-7 Stage_2-7 (Hall já coberto pelo backbuffer); analisar cada pasta; offline08 quatro cenas; R06-portals.py; custo08 e relatório; pacote QA08; atualizar matriz/checklist; restaurar overlay e verificar hashes; remover apenas nossos binários intermediários. Nada de outra Unity/smoke em paralelo.
-CUA: skill computer-use lida (SKILL.md, guidance, confirmations; API básica). Usar node_repl+@oai/sky, nunca terminal via UI nem permissões de segurança. Janelas antigas07 já fecharam, não reutilizar handles. Não automatizar Codex/Chrome/auth. Nenhum canal externo utilizado.
-Sem commit/push/deploy/R08. R06 continua parcial até concluir medições e QA.


### R06 — máscara reproduzida no framebuffer e candidata10 (11/11/2026 10:53)

-08:1093/1093 Unity +193/193 CI; build feita; Native ScreenCapture sem render request adicional em2clientes VISÍVEIS confirmou cenário preto durante a revelação. Logo NÃO é só defeito de captura manual. Evidência em Logs/R06-backbuffer-hall-candidate08/client-N-frames/r06-reveal*.png (04 ainda mostra arena,08 mostra preto). Não aprovar08.
-09 adicionou faixa de ordenação reservada durante Reveal (ordem original+10000, restaura no finally), máscara custom somente nessa faixa. Seu novo teste falhou: ao definir back10004 antes de front10005, Unity retornou back0. Não chegou a construir09.
-10 corrige a ordem dos setters: servidor define front primeiro, depois back, com margem1; cliente ConfigurePortalMaskRange primeiro amplia os limites, depois define front/back, inclusive ao transitar entre intervalos altos e negativos. SpriteMask customrange afeta só Pretty, não arena/players/HUD. Teste novo sobe e desce a faixa e confere getters. Proteção null da máscara no finally para cancelamento/remoção da cena.
-Candidate10:1094/1094 Unity PASSOU. Build em andamento, sessão6607. Scripts smokes/cost/package/portals/visible apontam para10. Não lançar outro smoke/Unity até saída completa6607 e identity.json gravado.
-Próximo: python Logs/R06-visible.py (Logs/R06-backbuffer-hall-candidate10), verificar ambos os clientes nas imagens reais do burst, início/meio/fim e após retorno à ordem normal; analisar via python -m Tools.Diagnostics.analyze_bosses <pasta>. Se cenário ainda preto, investigar; não aceitar só porque fixture concluiu.
-Depois: R06-smokes.py Stage_1-6 Stage_1-7 Stage_2-7; analisar cada; offline10 quatro cenas; R06-portals.py; R06-cost.py +R06-cost-report.py candidate10; pacote R06-package.py; docs/matriz/skill lessons; restaurar overlay/hashes e remover nossos binários intermediários.
-O novo comparador sample-and-hold é versionado em Tools/Diagnostics/analyze_bosses.py,6 testes em Tools/Ci/test_boss_analysis.py; Logs/R06-analyze.py apenas delega.193 CI passaram em08 (sem alteração Python posterior). Não usar comparador antigo de vizinhança50ms como aceite de sprite discreto.
-Sem commit/push/publicação/R08; overlay continua aplicado em Logs/R06-runner-sync/state.json.


### R06 — correção da leitura visual e separação pose/entrega (11/11/2026 11:06)

- CORREÇÃO das seções10:40/10:53: a afirmação de cenário preto NO JOGO foi precipitada e está retirada. Inspeção direta dos pixels dos PNGs RGB de08 retornou arena cinza (152,152,152) nos mesmos pontos que apareciam pretos na visualização sequencial da ferramenta; reabrir individualmente confirmou a arena intacta. Logs/R06-review-png.py verifica pixels originais:08 teve37+37 frames, ZERO frames com arena preta;10 teve38+37, também ZERO. Não registrar isso como bug do jogo corrigido. O isolamento da máscara permanece uma melhoria de escopo, e o clamp dos setters foi um defeito de configuração realmente comprovado pelo teste.
-Candidate10:1094 Unity passou e build completa; sessão6607 encerrada. Hall10 backbuffer terminou150s com2clientes reais, pausas/reload/repetição da intro. PNGs mostram revelação parcial e completa, arena/HUD preservados. Medição de pose confirmada passou em ambos.
-Na análise estrita pelo relógio atual apareceram3 amostras manuais de Venus com pose antiga, MAS newest/state também antigos: nenhuma nova pose havia chegado. Novo analisador verifica manual contra o state recebido e relata deliveryPoseHolds/deliveryHoldMaxMs à parte; não chamar isso de sprite errado nem ocultar o atraso. Maior retenção com PNG foi~280ms. Necessário controle final sem PNG para qualificar fluidez. Automáticas continuam comparadas no relógio de apresentação.8 testes focados passaram, incluindo falha obrigatória se uma pose futura for inventada durante retenção. CI completo anterior193; com os2 novos testes ainda precisa registrar execução completa195.
-Sessão38118 executa R06-smokes.py Stage_1-6 Stage_1-7 Stage_2-7 com10. Mechas10 fixture completou três entradas, pausas e reload; Clown em execução, Sun em seguida. Não lançar outra Unity/SB6 até encerrar.
-Próximos: analisar as três pastas10; controle Hall10 com capture_frames=False (criar pasta nova, saves isolados; pode adaptar R06-visible.py). Depois offline10 quatro cenas, regressão2portais, custo8execuções +relatório, QApackage10. Atualizar docs/matriz e restaurar overlay/hashes antes de encerrar. Sem commit/push/deploy/R08.


### R06 — final12 e cadeia autônoma (11/11/2026 11:47)

-11 corrigiu deriva de quadros automáticos: CaptureCoopBossPlayback mantém apenas o frame aprovado(speed0), sem alterar a animação nativa; movimento continua interpolado. Mechas11 passaram nos2observadores. Clown11 tinha2 amostras por cliente com sprite nulo: PresentCoopAnimation podia substituir frame.sprite válido por idleSprite=-1.12 dá prioridade ao sprite explícito nos estados retidos; novo teste reproduz a receita idle vazia.
-12 também retira dependência de shader no produtor da absorção: FreezerVenusIntro registra o valor de luz em dados CPU do manager, incluindo criação/atualização/retirada/limpeza. CaptureCoopAbsorption consulta esses dados antes de material. Necessário porque ProjectSettings.dedicatedServerOptimizations=1 e o build Linux é subtarget Server. Referência primária: https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/dedicated-server/get-started/optimizations . Teste sem material/shader passou; não inferir execução Linux desse teste.
-Candidate12:1097/1097 Unity, build Development concluída, assembly6e4a9ad77bb84f24fe5ebb91f9bcae4b8b949bc558fcece211ce48894123fc69.195 CI completos passaram em11; Python não mudou depois.12 C# novos/alterados passaram UTF-8 estrito; main/runner fontes e Packages idênticos (normalizar só CRLF); sem CS warnings/errors nos logs12.
-Cadeia autônoma Logs/R06-final-chain.py, sessão81867 /pythonPID10280 (confirmar PID vivo antes de agir), roda SERIAL e para em qualquer falha. Estado persistido em Logs/R06-candidate12/chain-state.json; log por etapa chain-*.log. Não lançar outro Unity/SB6 enquanto rodar.
-Etapas bosses +analyze-Stage_1-6 +analyze-Stage_1-7 +analyze-Stage_2-7 já PASSARAM em12 com2clientes reais. Cobre3Mechas, pausas/reload e poses; Clown null corrigido. Em execução: controle Hall sem PNG. Próximas etapas automáticas: analyze-control, Hall framebuffer visível, analyze-hall, render-check,2portais,4offline,8custos baseline/final Sun/Clown e relatório.
-Depois da cadeia: inspecionar todos os relatórios, retenções de entrega(sem ocultar picos) e comparativo de custos. Scripts extras preparados Logs/R06-bossrush.py (BossRush Stage_1-7 com2peers,110s) +análise; Logs/R06-linux-build.py (compila servidor Linux release, não publica). Rodar também em série, só após cadeia terminar. Finalizar pacote QA12 com Logs/R06-package.py APENAS depois das verificações, docs/matriz/lessons, restaurar overlay/hashes e excluir nossos binários intermediários.
-Sem commit/push/deploy/R08. Não marcar aceite manual sem resposta do usuário. A alegação antiga de arena preta continua RETIRADA; pixels originais comprovam arena visível (ver correção11:06).


### R06 — entrega técnica final12 e QA pendente (11/11/2026)

Este registro supera os estados de execução anteriores. **R06 permanece parcial somente pelo aceite visual humano; implementação e validação técnica desta entrega terminaram.** Não foi solicitado nem presumido aceite. Checklist30/32, faltam R06 e R08; R08 não foi iniciado. Nenhum commit, push ou publicação.

- Build final: C:/Users/mauri/SB6-R06/candidate12; DLL SHA-256 `6e4a9ad77bb84f24fe5ebb91f9bcae4b8b949bc558fcece211ce48894123fc69`. Pacote imutável copiado para C:/Users/mauri/SB6-QA-R06, identidade/sintaxe conferidas em Logs/R06-candidate12/qa-package.json. Protocolo69 requer cliente/servidor correspondentes.
- Mechas: sequência nativa adia a aquisição inicial até depois da inicialização da cena; se a intro desativar GameplayRoot, OnEnable ainda pode iniciá-la. Branco, preto e dourado entraram/combateram nos fixtures offline e online; não há simulação nativa de boss no observador.
- Pretty/Venus: máscara replica geometria/pivô/faixa, restaura sorting e remove estado ao encerrar; subida gradual e absorção usam dados CPU aprovados, independentes de shader no servidor dedicado. Sons da entrada/impactos passam pelo transporte existente. Bosses retêm frames aprovados com movimento interpolado; sprite explícito não é substituído por idle vazio. RNG, alvo, colisão e dano permanecem autoritativos.
-1097/1097 testes Unity core;195/195 Python CI repetidos.12 C# passaram UTF-8 estrito; fontes e Packages main/runner iguais antes do build, normalizando apenas CRLF. Windows Development e Linux Dedicated Server Release compilaram; sem warning/error CS nos logs finais. Linux foi SOMENTE compilado, sem teste de runtime Linux.
- Cadeia completa: Logs/R06-candidate12/chain-state.json, todos exit0. Dois clientes Mirror reais em Stage_1-6,1-7,2-7 e Hall; controle Hall sem PNG, Hall visível com80 PNGs;2regressões de portal;4cenas offline;8execuções de custo. Adicional BossRush Stage_1-7 com2clientes passou; estado em extra-state.json. Comparador r06-analysis.json de cada cenário: zero divergência estrita de sprite e zero deliveryPoseHolds. Controle Hall: p95 Pretty<=.0021u/Venus<=.0479u; comPNG Venus.0937u, instrumentação separada.
- Custo em Logs/R06-candidate12/cost-report.json:2repetições por cena/configuração, baseline R05 aprovada versus final12, processos headless sem PNG/motion capture, janela nativa antes da pausa. Clown: trabalho médio do servidor -.0317ms, clientes +.0226/-.0239ms; saída -.026%; alocação servidor +6.65KiB/s(+1.27%), clientes +.64/+.69KiB/s. Sun: servidor +.0195ms, clientes +.0586/+.0280ms; saída -1.11%; alocação servidor +39.72KiB/s(+5.97%), clientes +2.12/+2.03KiB/s. Trabalho médio absoluto<1ms nos três processos. Faixas de trabalho do Sun e tráfego se sobrepõem; não alegar causalidade/ganho estatístico universal. As alocações adicionais ficam registradas, não ocultadas.
- Comparação em Logs/R06-comparacao.json: três Mechas em combate nos dois modos;4sprites comuns Clown e7Sun; revelação de Pretty em posições parciais e completas, diferença de amostragem~1pixel; absorção0→1 nos dois modos. Execuções sem replay idêntico de RNG/input: conjuntos de sprites/cobertura não demonstram equivalência integral. Não mede GPU, WAN ou input físico; efeitos<80ms, gaps>150ms e fases sem referência ficam explícitos nos relatórios. Nenhum teste headless substitui aceite perceptual.
- QA: abrir UM atalho por vez no pacote (1-Mechas.cmd,2-ClownMask.cmd,3-SunMask.cmd,4-Pretty-Venus.cmd). Servidor local+2janelas iniciam automaticamente; atores protegidos/dano automatizado nos primeiros Mechas. Observar entradas, poses, subida a partir do chão, absorção, sons, pausa e recarga nos dois clientes. LEIA-ME.md contém roteiro. Saves isolados; logs por execução em runs/. Lançadores conferidos por sintaxe/identidade; mesmas fixtures/binário já executados, não presumir clique humano nem aprovação.
- Runner: restauração com hashes originais em Logs/R06-candidate12/runner-restore.log; caches Library/importação não revertidos. Limpeza planejada de8builds intermediárias próprias(~8.59GiB) foi rejeitada automaticamente pela política de execução (blocked by policy, sem detalhe adicional); nenhum desses diretórios foi removido. Plano em cleanup-plan.json. Preservados final12, QA, baseline R05 e todas as evidências/backups.
- A alegação antiga de arena preta no jogo continua RETIRADA: pixels RGB originais08/10/12 mantêm arena visível. A correção do clamp de sorting foi testada independentemente; não apresentá-la como conserto de tela preta.


## R06 — aceite humano registrado (11/11/2026)

Usuário confirmou: "R06 está aprovado." R06 CONCLUÍDO nos cenários testados, superando os registros anteriores de aceite pendente. Mantidas as evidências e limitações da final12/protocolo69; sem ampliar o aceite para WAN/GPU ou runtime Linux não medidos. Checklist31/32 concluídos; resta R08. Nenhum commit, push ou publicação e R08 não iniciado nesta atualização.

Retenção recomendada para R08: preservar C:/Users/mauri/SB6-QA-R06 como baseline aprovada. C:/Users/mauri/SB6-R06 contém a cópia duplicada final12 e candidatas intermediárias, dispensáveis quando a DLL de QA-R06 estiver conferida. Pacotes antigos QA-A14/R01/R02/R03/R05 não são necessários ao desenvolvimento do R08; seus runs/saves são evidências/dados de teste opcionais. Não houve exclusão nesta atualização.
