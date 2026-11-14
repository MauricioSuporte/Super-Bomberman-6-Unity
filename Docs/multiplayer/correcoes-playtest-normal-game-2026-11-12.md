# Correções do playtest cooperativo — 12/11/2026

## Como retomar

Pedido original: relatos do Normal Game online jogado por Maurício e amigo em 12/11, 19:39–21:05 (America/Cuiaba). Este arquivo é o checkpoint do trabalho; não substituir a matriz/contrato de predição. Nenhum relato é considerado resolvido só por compilar.

1. Ler AGENTS.md, skills Unity/Online/Prediction e contrato-validacao-offline-online.md.
2. Conferir git status/diff e o estado final de publicação ao fim deste arquivo. Usuário autorizou commit/push e publicação na v0.5.0 após todos os testes em 14/11; não repetir deploy sem verificar o commit já publicado.
3. Ler as seções finais, especialmente o incidente de isolamento de save em14/11. NÃO relançar binários B15/B16 ou anteriores com janela visível: eles ignoram SB6_SAVE_DIRECTORY fora de batch. FilaB16 foi interrompida e seus processos próprios encerrados.
4. Estado atual: 40/40 itens tratados e validados no escopo documentado. NG19 fechado B24 nos três contextos, NG40 e complemento NG21 fechados B25. Gates finais: Unity1180/1180, Python16+218, C# incremental0/0, UTF8/BOM73Cs. Nenhum jogo/Editor de teste ativo. Usuário autorizou commit/push na v0.5.0 e publicação ao terminar; em preparação. NG11 é queda de FPS não reproduzida nos cenários controlados, sem alegação de correção de causa desconhecida. Save real preservado nos testes isolados posteriores; estado anterior do primeiro slot permanece desconhecido, não restaurar backup de outubro.
5. Atualizar esta tabela e evidências em cada sessão, com comandos, contagens, falhas e próximo passo.

## Estados

Pendente = relato preservado, hipótese de owner ainda não confirmada. Implementado, validação pendente = código alterado sem aceite runtime. Validado = gates e cenários relevantes completos, com evidência. Owners abaixo são pontos de investigação, não diagnósticos confirmados.

| ID | Relato / aceite esperado (paridade com offline) | Estado | Onde começar |
| --- | --- | --- | --- |
| NG01 | Boss: itens invisíveis | Validado (drops nativos/visíveis, B18) | Itens/ItemPickup; Netcode/OnlineCoopWorld |
| NG02 | Clown Mask: introdução com luzes ausente | Validado (luzes, B12) | Bosses; StageIntro; captura de câmera/luz co-op |
| NG03 | Fácil: não renasce automaticamente online | Validado (explosão/buraco/controle, B09/B13/B14R1) | PlayerMovimentation/MovementController; Netcode/NetworkRevengeReturn |
| NG04 | Pink Louie: sombra ausente após pulo | Validado (sombra após pulo, B15) | Mounts; Netcode/NetworkBombFx e NetworkPlayerAnimation |
| NG05 | 2-2: tela não fica vermelha durante steam | Validado (overlay, B13) | StageAssets; transporte de efeitos de câmera/material |
| NG06 | 2-2: fogo não percorre todo o percurso | Validado (tiles finais, B13) | StageAssets; transporte de explosões/mecanismos |
| NG07 | 2-2: porta sem animação | Validado (frames reais, B13/B14R1) | Tilemap; clock headless/frame deltas |
| NG08 | 2-3: barcos ausentes | Validado (visual, B09) | StageAssets; catálogo e spawn co-op |
| NG09 | Barco + montaria HeadOnly: offset da cabeça errado | Validado (Mole/Tank no barco, B17) | Mounts; assento/offset e apresentação de riding |
| NG10 | 2-3: molas ausentes | Validado (visual, B09) | StageAssets; catálogo/spawn e visual tile-prefab |
| NG11 | 2-4: queda de FPS no cliente | Validado (sem queda reproduzida em combate, B19) | Diagnostics; OnlineCoopWorld; medir antes de mudar |
| NG12 | 2-5: movedores com poucos frames | Validado (movimento contínuo, B13/B14R1) | StageAssets; segmentos aprovados/sprite streaming |
| NG13 | 2-5: porta cortada | Validado (portão inteiro, B15) | Tilemap; mask/sorting/viewport |
| NG14 | Normal Game: todos os ovos devem ser random egg; 2-5 tem Purple Louie | Validado (ovo genérico/coleta, B19) | GameManager atribuição de drops; Resources/Items; autoridade RNG |
| NG15 | 2-5: itens reaparecem | Validado (prêmio consumido uma vez, B19) | ItemPickup; identidade e remoção de snapshots co-op |
| NG16 | 2-6/2-7: câmera parada na intro e duas câmeras sobrepostas | Validado (pan/seleção de intro, B14R1/B15) | StageIntro; seleção/posição de câmera co-op |
| NG17 | World 3 Hall: falta piscar ao entrar no portal | Validado (blink/entrada, B13) | StageAssets portal; apresentação/visibilidade player |
| NG18 | 3-1: submersão de players/Louies diferente do offline | Validado (submersão/player/Blue/Pink, B20) | Water; shader/property block; sprites montados |
| NG19 | Música: loop não reinicia com jogo minimizado | Validado B24 offline/dedicado/host, minimização real | Sound/GameMusicController; foco/clock de áudio |
| NG20 | 3-1 room 2: falta SFX ao abrir portão | Validado (áudio nativo/dedicado/host, B15/B17) | StageAssets; transporte áudio de mecanismo |
| NG21 | Raio de fogo 1 deve ocorrer somente em 1-1 | Validado offline/dedicado B19 + host B25 | PlayerPersistentStats; bootstrap/loadout; transição de estágio |
| NG22 | Chip: estrelas ausentes na coleta/entrega ao World 3 Hall | Validado (coleta/entrega real, B15) | StageAssets; FX aprovados e tempo de vida |
| NG23 | 3-2: mudança de sala deixa passageiro preso no minecart | Validado (passageiro/troca de sala, B18) | StageAssets transição; Mounts minecart; cancelamento/locks |
| NG24 | 3-1 room 1: saída do minecart não quica sobre bomba/tiles | Validado B23 offline/dedicado/host | Mounts minecart; player bounce; predição |
| NG25 | 3-2: vulcão com som baixo no disparo/queda de fogo | Validado (áudio nativo/dedicado/host, B15/B17) | StageAssets; ganho/clip e transporte áudio |
| NG26 | 3-3: predição water pulse diverge entre papéis/offline | Validado B21 offline/dedicado/host | StageAssets; Netcode movimento forçado/reconciliação |
| NG27 | 3-3: explosão atravessa geyser | Validado (explosões/renderers finais, B20) | Bomb/Explosions; contrato de bloqueio do tile |
| NG28 | World 3 Hall: Louie some durante entrega do chip | Validado (Blue/Pink na entrega real, B15) | StageAssets sequência; Mounts e supressão visual |
| NG29 | World 3 rooms 3: bubble libera passagem sem SFX | Validado (SFX bubble nativo/remoto, B15) | StageAssets; áudio aprovado e destruição da origem |
| NG30 | 3-4 room 3: tornado não puxa player | Validado (tração/aprovação nativas, B15/B18) | Enemies; força/segmento aprovado e predição owner |
| NG31 | 3-5 room 1: adicionar SFX ao quebrar gelo/bloquear tile | Validado (Ice/blocked nativos, B18) | StageAssets; áudio nativo + transporte online |
| NG32 | Bomba na água: falta animação e cor | Validado (sink/cor replicados, B18) | Bomb; shader/material e estados de água aprovados |
| NG33 | CoreMechanism carregado não deve piscar com inimigo ferido | Validado (core isolado no blink, B19R2) | Enemies; renderer ownership de blink |
| NG34 | 3-6 rooms 2/3: câmera começa fora de foco | Validado (primeiros frames, B18) | StageAssets transição; bounds/seleção/snap de câmera |
| NG35 | 3-6 room 3: disparo verde invisível quando origem está piscando | Validado (tiro visível durante blink, B19R2) | Enemies/projectiles; herança renderer/alpha/material |
| NG36 | World 3 Hall: portal novo sem arredondamento nas pontas | Validado (máscara do portal, B13) | StageAssets; Tilemap mask/material/shape |
| NG37 | Aumentar fonte dos textos online | Validado (fontes/layout, B09/B14R1) | Netcode UI; localização 4 idiomas e fallback japonês |
| NG38 | Unlock toast ausente online | Validado (toasts/persistência/retorno, B20) | Unlock; HUD; persistência owner e apresentação cliente |
| NG39 | Normal Game completo não mostra créditos | Validado (créditos/retorno, B12–B14R1) | EndScreen; progressão e fluxo de cena online |
| NG40 | Stage label da intro/pausa e menu de pausa não acompanham o multiplicador do safe frame | Validado B25 offline/dedicado/host, três modos | Stage/StageLabel; PixelPerfectViewport; Pause |

## NG03 — implementação inicial

Causa confirmada: HasEasyNormalGameRespawn excluía toda sessão online. Removida a exclusão; servidor continua responsável pelo fim da morte/respawn. Reutilizado o RPC existente de retorno confirmado, sem alterar wire/protocolo, para limpar morte, posição, loadout e histórico de inputs nos clientes sem exigir arco de Revenge/eliminated. Adicionado caso sem arco ao teste existente de retorno confirmado. Proteção temporária continua originada no servidor e replicada pelo estado de FX existente.

Revisar: morte por explosão e buraco, último player morto simultaneamente, respawn na sala atual, direção inicial do boss, pausa/morte durante transição, host e teardown. Conferir que nenhuma coroutine nativa de respawn roda no pure client e que owner retoma input só após aprovação. O caso de teste é de apresentação/estado; não prova partida real.

## Validação e evidências desta sessão

- Baseline inicial: branch v0.5.0, árvore limpa; ProjectVersion.txt = 6000.6.0f1 (a versão estática em AGENTS.md está antiga).
- Nenhuma instância Unity detectada no início. Unity 6000.6.0f1 instalado.
- Python RoomDirectory: 16 testes aprovados; Python Ci: 218 testes aprovados (12/11/2026). Protocolo verificado pela suíte real, sem alteração de wire.
- Unity 6000.6.0f1 compilou e executou o baseline completo via Tools/Ci/release.unity: 1.127 testes, 1.127 Passed, zero falhas/skips. Os dois casos ConfirmedReturnRestoresOnlyApprovedStatsWithOrWithoutRevengeArc passaram. Nenhum error CS/warning CS no log. Saída Logs/PlaytestNormalGame20261112/unity.log e editmode.xml. O helper foi chamado diretamente: build()/deploy não foram chamados.
- Offline + dedicado/owner/observador + host: pendentes; não houve execução real ainda.
- Não foram gerados builds nem feitos deploys. Save dos testes isolado por SB6_SAVE_DIRECTORY via helper. O Editor regenerou 3 assets de fonte e migrou Physics2DSettings; duas fontes foram restauradas pela própria execução. Os 2 arquivos restantes (fonte Resources e Physics2DSettings) foram arquivados em Logs/PlaytestNormalGame20261112/editor-side-effects e restaurados ao conteúdo do HEAD inicial limpo, respeitando os finais de linha do checkout. Manifesto em cleanup-manifest.json. Nenhum arquivo foi removido; recovery/saves/caches preservados.

## Pistas confirmadas na leitura para os próximos lotes

- NG19: Sound/GameMusicController.PlayLoopAfterIntroRoutine tem condição Application.isFocused antes de reconhecer fim da intro. Não retirar cegamente o teste de foco: o comentário registra que AudioSource.isPlaying pode ficar falso ao perder foco. Usar um clock de término apropriado, respeitando pause, pitch, substituição/StopMusic e áudio real do cliente. OnlineCoopMusic já envia o par intro/loop; servidor dedicado sai dessa coroutine.
- NG21: GameManager.EnsureStage1_2PlayersHaveMinimumExplosionRadius é chamado apenas por LoadNextStageRoutine offline. OnlineCoopSession.CoopStageCompletedRoutine faz RouteCoopScene sem esse ajuste. Verificar também seleção direta de estágio, reset de morte, estágio/runtime e BossRush/Battle antes de centralizar o mínimo.
- NG39: OnlineCoopSession.CoopStageCompletedRoutine roteia boss/estágio já concluído para WorldMap; apenas o outro ramo considera END_SCREEN quando nextStage vazio. GameManager.ShowEndingAfterDelayRoutine usa StageIntroTransition.StartEndingScreenSequence/EndingScreenController. Comparar o critério de campanha completa e toda a sequência nativa; nenhum diagnóstico de causa final confirmado.

- NG33: Health/CharacterHealth.EnsureRendererCache captura todos os SpriteRenderer filhos, inclusive attachments. Conferir separação de ownership do AttachedCoreMechanismsController; não alterar o blink dos sprites nativos do inimigo.
- NG35: identificar primeiro qual prefab é o inimigo verde em 3-6 room 3. Se for Faroh, EmitFireBurst instancia fireVisual.gameObject (pode herdar color/alpha do blink de CharacterHealth). Não afirmar que é Faroh sem verificar a cena/prefab.

## Cobertura runtime que falta preparar (NG03)

O smoke cooperativo atual configura dificuldade Hard em LocalServerLoadTest e não contém fixture específica de morte/respawn Fácil. É necessário estender o fixture de desenvolvimento ou executar o cenário manual com binários novos; um smoke ocioso não comprova NG03. Não usar binários anteriores para aprovar esta alteração. Validar offline/local2P, dedicado com dois clientes (matar P1/P2, morte simultânea, observar respawn/controle/invulnerabilidade), host e troca de sala/pausa/desconexão. Nenhum desses cenários foi executado nesta sessão.

## Ordem de execução e aceite por lote

1. **Fluxo de sobrevivência (NG03)**: concluir regressão automatizada e cenário Fácil antes de ampliar o lote.
2. **Itens e objetos ausentes/reaparecendo (NG01/08/10/14/15)**: comparar objeto autoritativo, prefab/NetworkIdentity, catálogo, descoberta e visual. Testar revelação, coleta, destruição, disputa por dois players e troca de sala. NG14 é regra nova explicitamente solicitada: RNG exclusivamente na autoridade; confirmar catálogo de ovos elegíveis sem reutilizar configuração Battle indevidamente.
3. **World 2 e introduções (NG02/04/05/06/07/09/12/13/16)**: comparar vídeo offline/dedicado/owner/observador; preservar curvas, luzes, sombras, masks e frames intermediários de portas em servidor headless. Pausa e interrupção devem restaurar estado.
4. **World 3, montarias, combate e câmeras (NG17/18/20/22–36)**: testar cada sala nativa, mecanismos em sequência, players montados/a pé e mudança de sala com outro player preso em veículo. Dano/colisão permanecem no servidor; movimentos forçados usam o caminho de autoridade/predição existente.
5. **Áudio, base de fogo e progressão/UI (NG19/21/37/38/39)**: focar também minimização/retorno, volume local, quatro idiomas, fontes japonesas, save novo/existente e persistência do dono sem gravar progresso alheio no save offline. Créditos exigem conclusão real do fluxo, não só carga de END_SCREEN.
6. **Desempenho (NG11/12)**: capturar 2-4/2-5 com mesmo binário, cenário, sequência, settings e janela estável; separar queda de frames renderizados, coleta/serialização servidor, banda e atraso de apresentação. Não reduzir frequência de dados que destrua animações para melhorar FPS aparente.

Em cada lote: compilação Unity/C#, baseline EditMode completo, ambas as suítes Python e cenário offline + dedicado/2 clientes. Host/COM quando o caminho diferir. Evidência de propriedade/pausa/morte/interrupção/teardown conforme o recurso, sem declarar papéis não exercitados como aprovados. Wire novo exige bump coordenado e testes de protocolo; nenhuma publicação está incluída neste pedido.

## Evidência final do lote NG03

- Fonte base: d1b928a28752bc46487f8af920dfaa7ac7bde8fe + diff local sem commit.
- Relatório compacto e hashes: [evidências NG03](evidencias/playtest-normal-game-ng03-2026-11-12.md).
- Próximo passo obrigatório: cenário de renascimento Fácil descrito acima. Os outros 38 relatos continuam pendentes.

## Continuação — lote de regras/estado

NG19: troca intro/loop usa avanço de AudioSettings.dspTime e pitch, desconta pausa e preserva a proteção contra isPlaying falso ao perder foco. NG21: mínimo central em LoadInto/BeginStage, Stage_* exceto 1-1; BossRush/Battle preservados. NG33: cache de CharacterHealth inclui só sprites próprios, exclui objetivos anexados e outros atores filhos; não modifica o visual do core depois de detach. Testes adicionados ao owner OnlineRoomEditModeTests. Gates deste lote pendentes.

A validação runtime será agrupada em binários novos por lote para evitar reconstruir todo o projeto por relato. Isso altera a ordem operacional do plano, sem dispensar nenhum aceite.

NG01: MechaBossSequence (dois spawns), PrettyBomberDefeatSequence e BossEscapeOnLastLife instanciavam prefabs de item com NetworkIdentity sem NetSpawn.Server. Como o stream co-op exclui network parents, o item existia só na autoridade. Todos os quatro sites agora registram o spawn. NG05: overlay vermelho da Boiler foi adicionado à captura cinematográfica existente. NG06: registro imediato do renderer de PowderIgniterFlame reduz perda de FX curtos na janela de descoberta; diagnóstico de percurso completo ainda pendente, portanto NG06 não foi marcado implementado.

## Lote 03 em andamento

- NG04: handoff para locomoção do dono reapresenta a sombra Pink explicitamente; os drivers nativos da montaria continuam desativados no cliente. Corrigido reset contínuo do timer da sombra.
- NG09: assento do barco pode sobrepor visualmente runtimeBaseOffset sem apagá-lo, evitando somar o offset HeadOnly da montaria ao assento; ao sair restaura o offset anterior. Native e owner usam o mesmo modo.
- NG16/34: incluir a Entrance Camera untagged; seleção vem do controlador nativo, e pose/tamanho/estado do follow são enviados para intro e cortes. O follow local continua no gameplay. Wire mudou para 72 no jogo e RoomDirectory; não publicar binários misturados.
- NG18: clientes recebem o mesmo PlayerWaterSubmersionEffect do spawner e o efeito puramente visual da montaria não é desligado com AI/animadores no rebind. Cenários com bomba/ponte ainda precisam comparar a geometria aprovada.
- NG20: PlayRoomCompletionSfx agora publica o clip/gain nativo além de tocar no servidor/host.
- NG23: cancelar e dispor os iteradores da viagem de minecart antes de FreezeGameplay capturar locks e mover players entre salas; restaurar cart parado reutilizável.
- NG25: gain explícito 3 para disparo/ar/queda do vulcão, preservando clip original no catálogo e volume local. Falta aceite auditivo.
- NG19: coberto também host gráfico; troca local intro/loop não rebroadcasta para reiniciar música nos observadores.
- NG03 runtime tentativa 01: Build/NormalPlaytest/Batch02/easy-online-01. FALHOU na preparação (slot ativo inicial era inexistente, fixture não ativava Easy), sem morte exercitada. Correção do fixture: selecionar slot0 temporário antes de ServerStartCoop. Não marcar aceite. Binário da tentativa é cópia imutável + identity.json/source.patch no diretório Batch02.
- Gates lote02: 1135 Unity Passed, 16 RoomDirectory Passed, 218 Ci Passed; Development Windows Player 1101 MB, zero erros de build. Logs em Logs/NormalPlaytest/Batch02. Lote03 ainda não compilado/testado.

- NG14 confirmado pelo usuário: ovo genérico, Louie revelado na coleta. Novo ItemType.RandomLouieEgg=29, prefab Resources/Items e registro no OnlineLobby; normalização dos ovos Normal Game e sorteio somente na coleta autoritativa. Battle/BossRush mantêm seus ovos/configurações concretas. Os sete Louies originais continuam elegíveis.
- NG17: snapshots de NetworkPlayerAnimation passam a incluir a cor do renderer aprovado, para blink de portal que não é invulnerabilidade de CharacterHealth.
- NG22: texturas runtime das estrelas/triângulos eram marcadas não-legíveis, incompatíveis com a definição de sprite co-op. Mantidas legíveis (32x32) e registrado renderer imediatamente.
- NG32: BombAnimations inclui estado de aparência de afundamento aprovado (posição/escala/cor); o cliente apresenta/interpola sem executar coroutine de água/RNG/dano. Despawn da água usa NetSpawn.
- NG35: na Stage_3-6 há Gurun, que compartilha BirdRobotRocket com BirdRobotRed e clona o template durante blink. Init restaura cor original do template através do CharacterHealth antes de apresentar projétil/impacto. Nenhuma alteração em Faroh, que não está nessa cena.
- Total atual: 19 relatos implementados (validação pendente), 20 pendentes. Testes novos cobrem offset do assento/restauração, sprite dinâmico legível, ovo genérico sem RNG antecipado/registro spawnável e sombra Pink após handoff. Gates lote03 em preparação.

## Lote 04 em andamento

- NG27: HasIndestructibleAt considera o collider ativo do GeyserLauncherBootstrap, além dos tiles; ambos os caminhos de linha de explosão usam esse bloqueio. Teste cobre barreira presente/adjacente/desativada.
- NG31: FragileFloor expõe clips crack/block e toca pelo GameMusicController persistente (transporte co-op existente). FragileFloorIce ligado aos clips Ice/blocked; callbacks não simulam no pure client.
- NG38: snapshots sucessivos de progresso notificam novos desbloqueios sem chamar setters persistentes. Primeiro snapshot é baseline silencioso; duplicatas não repetem; host mantém eventos nativos. Teste de delta e save offline preservado adicionado.
- NG39: END_SCREEN é sentinel de sequência nativa, não cena física. RouteCoopScene inicia StageIntroTransition.StartEndingScreenSequence; créditos entram no StageStatus e cada cliente apresenta a UI nativa, permanecendo observador do Start/retorno do dono. Fluxo final vazio/END_SCREEN precede retorno a WorldMap em replays, como offline. Aceite do fim real ainda pendente.
- NG12 parcial: renderer do proxy de pusher é registrado no mesmo frame do Instantiate. Não encerra o diagnóstico de cadência.
- Lote03Retry01: 1.139 testes, 1.138 Passed, 1 falha de fixture no offset de barco (animação sem clip/idle não atualizava offsets). Corrigido fixture com idle=true. Não esconder tentativa reprovada. Não houve build deste lote reprovado.
- Lote04: gates em execução em Logs/NormalPlaytest/Batch04. Python repetido; resultado final a registrar. Binário Windows será produzido somente se baseline Unity inteiro passar. Servidor Linux de testes acessível por SSH; nenhuma alteração em serviço publicado.
- Total: 23 implementados com validação pendente, 16 relatos pendentes. Nenhum aceite runtime declarado.

## Lote 05 — máscara do portal e fixtures

- Lote04 compilou sem erros C#, mas baseline reprovou: 1141 total, 1139 Passed, 2 Failed. Barco precisava inicializar Awake no objeto inativo; geyser precisava adicionar BoxCollider2D concreto antes do BombController (RequireComponent abstrato). Não houve build nem aceite runtime.
- NG09: fixture agora chama Awake e efetivamente testa os offsets. SetExternalBaseOffsetFromInitial já suspendia/restaurava runtimeLockX; ApplyOffset/ResetOffset também preservam a precedência do override do assento caso outro código atualize o lock durante a viagem.
- NG36: CoopPortalPalette transporta exclusivamente os parâmetros aprovados do shader existente World3 Hall Portal Palette, incluindo sprite de máscara, revealY e retângulos de mundo/UV. Cliente aplica MPB e o limpa no término; beam registrado no nascimento. Teste de captura/aplicação/limpeza e rejeição de NaN adicionado.
- Rodando 4 regressões direcionadas em Logs/NormalPlaytest/Batch05Targeted antes de repetir o baseline completo e builds. Sem publicação.
- Total: 24 implementados com validação pendente, 15 pendentes.

## Retomada em 13/11 após limite de acesso

- Lote05 completo: Unity 1142/1142 Passed; binários Windows e Linux Development prontos, protocolo72, identity.json/source.patch e cópias imutáveis em Build/NormalPlaytest/Batch05. Logs em Logs/NormalPlaytest/Batch05. Python16+218 aprovados na rodada anterior; repetir após novas alterações.
- Linux transferido para diretório isolado /home/mauricio/sb6-tests/normal-playtest-20261113-b05/player, sem tocar serviço publicado. Runtime real 2 clientes em execução: boats-springs-01, Stage_2-3, 150 segundos, renderizado, pausa/reload nativos.
- Offline Easy tentativa01: quatro mortes nativas executadas, dois respawns individuais confirmados e simultâneo observado, mas fixture interrompeu em "Native pause rejected". Normal Game proíbe pausa durante morte; isso não é falha de produto. Processo próprio encerrou pelo timeout160s; evidência parcial não é aceite. Fixture corrigido: pausa/resume com atores vivos e confirmação de rejeição durante morte. Correção requer binário novo (B06).
- NG15: Stage_2-5 usa ResurrectingAnimatedTileProgressHandler. GetSpawnForDestroyedBlock não removia a atribuição após reservar a revelação; bloco renascido podia retornar a recompensa. Consumida a atribuição só após reserva bem-sucedida. Dois testes: reserva rejeitada preserva item; primeira revelação bem-sucedida impede repetição.
- NG30: Hurricane fazia MovePosition pendente em player cujo motor online escreve posição imediatamente para cada tick; tick recebido antes da física pode apagar o pull. Autoridade com NetworkTickDriven aplica posição imediata e sincroniza transform, como o caminho WaterCycle já faz. Offline conserva MovePosition; previsão mantém aprovação existente. Novo teste exige que pull sobreviva a tick idle antes da física. Ainda não compilado/testado nesta revisão.
- Total atual: 26 implementados com validação pendente; 13 pendentes. Não confundir implementação com aceite runtime.

## Lote06 em preparação

- NG37: mínimo de6 pixels de referência nas labels do lobby/lista, chat7/status6 e nome sobre player7. Reorganizados status/cabeçalhos/lista e larguras das5 colunas; cabeçalhos curtos localizados nos4 idiomas, fallback japonês preservado. Pendente render/QA4 idiomas, sem alegar aceite visual.
- Fixture de desenvolvimento estendido com inspeção native offline/servidor: stage23 (foco em barco e mola), gate (abertura nativa), intro, hall (Blue Louies + sequência nativa de chip e reveal de portal), water (entrada física no current). UI preview usa sala fictícia somente em memória, browser parado, save isolado obrigatório, screenshots nos4 idiomas; não aloca sala pública nem altera PlayerPrefs de nome.
- NG15/30/36: regressões direcionadas passaram:7 casos em Batch06TargetedRetry01. Tentativa anterior teve erro de compilação por colisão de namespace SaveSystem no fixture; corrigido com global::. Nenhum erro escondido.
- Runtime B05 Stage2-3: Linux dedicado +2 clientes reais renderizados,150s, pausa/reload e stream novo concluídos sem exceções/shader stripping. result.json e3 diagnósticos completos arquivados em boats-springs-01. Captura inicial não enquadra barco/molaa; isso continua pendente até inspeção próxima, não é aceite deNG08/10.
- Total:27 implementados com validação pendente,12 pendentes.

### Lote06 — gates e captura real

- Batch06:1145 testes,1144 Passed,1 falha no caso antigo PendingEnemyMovePositionDoesNotPublishUnexecutedLargeDisplacement(Hurricane), que pressupunha MovePosition pendente em todo pull. Ajuste: passos imediatos limitados a0,25tile; deslocamentos maiores preservam física pendente, e esse teste agora configura pullSpeed20 explicitamente para exercitar esse limite. Novo teste cobre passo curto já aplicado antes de um tick idle. Batch06Retry01:1145/1145 Passed, zero skips/falhas e sem warning/error CS. Builds Windows/Linux em andamento; não chamar runtime B06 aprovado ainda.
- Captura de desenvolvimento: com -r06-backbuffer-captures encontra câmera ativa mesmo se MainCamera estiver desativada na entrada. Warmup pode iniciar durante Intro/timeScale0. Clientes devem ser visíveis/sem-batchmode para backbuffer/UI reais; render request antigo omitia HUD fora dos fixtures de mecanismo.
- Python repetido:16 RoomDirectory e218 Ci Passed.
- B05 Stage2-3: relatório R02 válido (sem drop/overflow/capture limit), families.json. Aproximadamente14800 frames apresentados por família BoatRideZone/SpringLauncher em cada cliente; objetos parados nessa amostra, portanto não há prova de pilotagem/lançamento ou aparência correta. PNGs iniciais não enquadram objetos. Não transformar evidência de transporte em aceite visual.

### Pistas adicionais a validar, sem diagnóstico final de FPS

- NG11: Stage2-4 contém InfiniteBackgroundScroll em SpriteRenderer Tiled,size42x32. Script cria material runtime e altera _ScrollUv; CoopSpriteFrame atual não leva drawMode/size/UV e CatalogId de material runtime retorna-1. É uma divergência visual confirmada na leitura, ainda não prova da queda de FPS. Medir antes de atribuir causa ou otimizar; se corrigir, usar receita limitada ao shader existente/phase aprovada, sem coroutine de mundo no cliente.
- NG39: revisar skip dos créditos: bool credits inicia UI em todos, mas RollCreditsRoutine só aceita Start na autoridade; sem fase aprovada de skip/final, o dono remoto pode continuar vendo o scroll mesmo depois de o servidor pular. Manter esse aceite pendente e sincronizar fase se confirmado. Não dar como concluído apenas por aparecer a primeira tela.


## Lote07 diagnóstico — continuidade após nova retomada

- B06Retry01:1145/1145 Passed; Windows/Linux Development concluídos, identidade imutável em Build/NormalPlaytest/Batch06/identity.json (protocolo72). Python16+218 Passed. Nenhuma publicação/alteração do serviço existente.
- B06 offline-easy-01 concluiu4 mortes/4 respawns nativos, pausa com vivos e rejeição da pausa durante morte. Controle por ator ainda pendente: fixture antigo compartilhava origem entre P1/P2. B07 mantém dicionários por ator e aguarda5s após cada retorno para medir movimento sem confundir atores.
- B06 stage23-close-01, gate25-01 e clown-intro-01: Linux dedicado +2 clientes Windows reais renderizados,150s cada, cena/pausa/reload concluídos sem exceções. Isso valida execução; não equivale a aceite visual/gameplay dos relatos.
- NG08/10: comparação próxima confirma barcos e molas visíveis offline e ausentes/parciais online. R02 comprova transporte, mas não desenho. Investigar SpriteRenderer/proxy/animação/material/ordenação; não alterar sorting sem evidência. B07 inclui metadados de renderers no fixture.
- B06 UI preview12 capturas concluiu, porém foi invalidado para QA visual: browser consultou diretório público antigo(protocolo71) e mostrou rejeição vs72. B07 desliga consulta somente no preview Development, usa sala em memória e reinicia creationOpen entre idiomas; gate de compatibilidade real preservado.
- B07Diag:10/10 testes direcionados Passed, compilação sem warning/error CS, Windows Development pronto; baseline completo desta revisão ainda pendente. Protocolo de fonte/B07 agora73 (cliente e supervisor alinhados), B06 permanece72. Não misturar binários. Linux B07 diagnóstico em construção.
- NG39: fase aprovada de skip/final dos créditos adicionada ao StageStatus; observer sai do scroll quando servidor aprova Start do dono. Fixture credits prepara campanha isolada e mata chefe final pelo caminho nativo, para validar sequência/retorno reais.
- NG37: entrada do chat mede largura real TMP e recorta somente apresentação do draft preservando mensagem integral/surrogates;2 testes de largura inglês/japonês. Preview B07 offline tem12 PNGs em4 idiomas; português browser inspecionado, demais telas ainda pendentes.
- B07Diag stage23-offline-01 concluiu e gerou32 PNGs; offline-020 mostra barcos vermelho/roxo e molas douradas. player.log contém metadados de sprites. B06 close online tem imagem correspondente sem barcos e sem bobina da mola; defeito permanece aberto.
- Total permanece27 implementados com validação pendente/12 pendentes. Nenhum relato foi declarado integralmente aceito em todos os papéis.


## Lote08 — diagnóstico e layout

- B07Diag stage23-render-01 repetiu150s dedicado Linux +2 clientes, pausa/reload concluídos. SpriteRenderer dos proxies de barco/mola está enabled/active/forceOff=false, sprite/posição/escala/sorting/bounds.size iguais aos nativos offline. Invisibilidade permanece; não há base ainda para alterar sorting. B08 amplia log com color/bounds.center/câmera/tilemaps e parâmetros da luz do Clown.
- B07Diag clown-offline-01 confirma escurecimento e foco circular offline (offline-009); B06 clown-intro-01 online não apresenta esse escurecimento. Investigar transporte/material/visibilidade do Image com dados B08. Porta2-5 offline capturada em gate25-offline-01 para comparação; aceite ainda pendente.
- NG37 QA12 telas B07: browser/creation legíveis nos4 idiomas, mas NameHint sobrepõe linhas (espanhol e português); altura explícita de10px por linha adicionada para name/password. Chat com contraste de nomes também mostrou deslocamento ao quebrar linha; contraste agora alinha cada glyph ao texto principal preservando outline. Regressão de presença multilinha adicionada.
- Batch08Diag abortou compilação no teste chamando helper internal diretamente; corrigido acesso por reflection seguindo os testes existentes. Nenhum build da tentativa reprovada. Batch08Retry01: baseline completo aprovado (ver identity.json/editmode.xml), Windows/Linux Development concluídos, protocolo73, sem warning/error CS. Builds imutáveis/identidades em Build/NormalPlaytest/Batch08Retry01; logs em Logs/NormalPlaytest/Batch08Retry01.
- Python novamente16 RoomDirectory+218 Ci Passed, incluindo alinhamento real do protocolo73.58 arquivos C# alterados decodificados estritamente UTF-8 sem BOM corrompido/duplicado/replacement.
- Diagnósticos completos dos3 papéis B06 stage23-close/gate25/clown-intro e B07 stage23-render arquivados com origem/tamanho/SHA256 em diagnostics-manifest.json de cada execução. Nenhum original/save/recovery removido.
- Fonte atual contém as correções B08. Após restabelecimento de quota, continuar dos binários B08, não repetir compilação sem novas alterações. Em execução: transferência Linux isolado b08 e offline stage23-offline-01; próximos: online stage23-render e Clown com log de luz; depois Easy por ator, créditos/skip/final/retorno e demais casos pendentes.

## Lote09 — causas identificadas e correções em validação

- NG08/10 causa: map.ClearAllTiles+reconstrução encolhe Tilemap.origin/size às células ocupadas. B08 log offline Ground.centerY4.5 vs online-1, Indestructibles/Water6 vs-0.5; câmera usa CustomAxisY, alterando a ordenação dos chunks e cobrindo os sprites. Proxies têm sprite/alpha/posição/escala/sorting corretos. CoopTiles agora transporta origin/size nativos e reaplica após células; teste preserva margens vazias/localBounds e rejeita tamanho inválido. Ainda repetir PNG/runtime para confirmar o desenho; NG13/07 permanecem pendentes até comparação própria.
- NG02: servidor B08 tem holofote nativo com alpha0.9 e coordenadas corretas. Cliente alterna active/alpha porque a imagem chegou inicialmente como fade simples oculto (antes do material pronto), e coopFadeSamples continua impondo invisibilidade a cada frame após receber spotlight=true. ApplyCoopCinematics remove esse fade residual quando assume holofote e reativa Image. Teste hidden-fade → spotlight-visible → PresentCoopFades verifica permanência e material.
- Wire CoopTiles mudou; fonte/protocolo cliente e supervisor agora74. B08 continua73 e serve apenas de diagnóstico anterior. Não misturar versões nem alterar publicado. Lote09 aguardando gates/builds e validação runtime nova.
- Total:30 implementados com validação pendente;9 relatos pendentes de solução. Nenhum aceite completo de papéis inferido.


### Lote09 — gates aprovados, builds/runtime em curso

- Baseline1154/1154 Passed, sem falhas/skips e sem warning/error CS; Python16+218 Passed novamente com protocolo74. UTF-8/BOM dos58 C# alterados aprovado.
- Windows imutável pronto: Assembly a80782c5ffe4dcb9a8350b3b53fc55dad5adceedfdb77ac4c2254fb603ebe34c. Linux em construção na sessão93090; identidade atualizada apenas depois de cópia completa.
- Offline Stage2-3 B09 concluiu com32 PNGs. Preview UI B09 concluiu12 PNGs; NameHint nos4 idiomas revisado, sobreposição corrigida com baselines10px. Chat ao vivo e nome sobre player continuam pendentes.
- run-offline.py detector procurava case completed, mas inspeções emitem inspection completed case=. Corrigido somente o detector e os4 relatórios afetados; saídas iniciais arquivadas em result.initial-detector.json, log/binário preservados. Não era falha de produto nem execução incompleta.
- B08 clown-light-01 concluiu dedicado+2 clientes150s/pausa/reload; logs confirmam material do servidor com alpha0.9 e cliente escondido pelo fade residual. Próximo B09 deve confirmar permanência da luz.
- Fila de validação B09 na sessão62441 aguarda Linux+UI: prepara diretório remoto isolado b09 e executa stage23-render,clown-light,easy,hall,water33,gate25, nesta ordem. Binários/source imutáveis; não misturar B08/73 com B09/74. Sessão70741 conclui offline+UI. Ver result.json e PNGs, não aprovar só pelo runner.
- Próximo lote depois que os builds terminarem: evitar setters origin/size quando já iguais (rebuild desnecessário); preparar caso idle de custo sem trace e caso nativo de boiler/porta2-2; minecart exit com bomba e entrega de chip. NG38 runtime requer ownerFlowConfigured/save adotado pelo fluxo nativo, não apenas o startup direto do load-test (que deixa progress snapshot vazio).
- Avisos/erros nativos de allocator UDS do Editor aparecem nos logs de build desde B06/B07 (Failed to free block size delta44 / Evict-before-extend), sem erro C#; preservar evidência, não suprimir nem limpar cache ativo.


### B09 — NG08/10 validados no escopo visual

- Stage2-3:150s Linux dedicado +2 Windows reais, pausa/resume, reload e novo stream completos sem exceções. Dono e observador mostram barcos vermelho/roxo e bobinas douradas das molas; PNG035 dos2 papéis comparado ao offline B09. Ground renderer.centerY=4.5 novamente, igual offline. Detalhes/caminhos em stage23-render-01/visual-review.json.
- Correção aplica somente no pure client (CanApplyCoop exclui NetworkServer.active); host mantém renderização nativa offline exercitada. Sem lógica COM/resultado de embarque alterada. Visibilidade/sorting NG08/10 encerrados; interação/offset NG09 continua com aceite próprio pendente.
- NG02: PNG012 em ambos os clientes já mostra escurecimento/foco circular comparável ao offline; aguardando terminar pausa/reload desta execução antes de fechar.
- Total:2 relatos validados no escopo correspondente;28 implementados com validação pendente;9 pendentes de solução. Não confundir os estados.
- Fonte B10 já iniciou após término dos builds B09: ApplyCoopMapBounds evita setters quando origin/size iguais; fixture opt-in idle(30s) e boiler(bomba real no tile registrado, caminho nativo, coleta de cobertura por célula e frames da porta). Ainda não compilado. B09 permanece imutável/protocolo74, fila de runtime continua na sessão62441.


## Lote10 — fixtures e bordas de coleta

- Fonte atual: ApplyCoopMapBounds evita setters idênticos; caso idle30s sem trace para custo; boiler usa TryPlaceBombAtIgnoringInputLock no tile cujo GroundTileResolver resolve BoilerTriggerGroundTileHandler (aciona porta/caldeira nativos), mede todas as células previstas do fogo e frames da porta no renderer aprovado. Não simula mundo no cliente.
- NG14: fila de ovos cheia/bomba na PowerGlove rejeita toque antes de sortear, mantendo RandomLouieEgg e Random.state. Novo teste de toque rejeitado com fila cheia.
- Credits fixture offline confirma retorno real ao TitleScreen e para o roteiro de mundo após ending. Executar em Stage_World3Hall: Stage3-8 é encontro virtual desse Hall; LocalProjectileSmoke.EnsureEncounter solicita BeginHallBattle(true) e aguarda FreezerVenus ativo.
- B09 clown-light-01: foco/escurecimento visíveis em ambos os clientes; primeira pausa/resume e reload passaram, porém segunda pausa foi rejeitada (linha210, antes de paused index2). Motivo nativo específico não foi registrado (intro/morte/estado bloqueado); não atribuir a falha de produto. B10 consulta IsPauseBlockedByGameplayState até estado elegível após reload, sem forçar desbloqueio. NG02 permanece em validação.
- Batch10 abortou antes dos testes/builds por CS0136 no fixture boiler (focus/index conflitam com variáveis posteriores do método). Renomeados boilerFocus/boilerPlayerIndex. Batch10Retry01 em execução na sessão54453, ainda sem resultado final. Não usar a tentativa reprovada como gate aprovado.
- B09 easy-01 passou:4 mortes/4 revivals; clientRevivals=[4,4]; clientMovement=[2,2]; pausa/resume/reload completos. Evidência NG03 atualizada. Offline por ator/host ainda pendentes.
- A fila B09 sessão91207 parou após Easy: hall-01 foi recusado pela reserva mínima3072MiB antes de qualquer processo/sala começar (pasta vazia preservada). Retomar com nome novo hall-02 depois do build B10, seguido water33 e gate25. Sessão62441 anterior parou na segunda pausa de Clown; não está mais ativa.
- Arquivados os3 diagnósticos de cada B09 stage23-render/clown-light/easy; Stage23 families.json válido sem drop/overflow/capture limit. B09stage23 NG08/10 continuam validados visualmente. Total2 validados/28 implementados pendentes/9 pendentes de solução.
- Pistas para o próximo lote: host tem OnStartHost mas não botão de produção; preparar fixture Development StartHost com transporte local/save isolado+cliente real para NG03. NG38 precisa adoção nativa do save do dono (startup load-test direto deixa ownerFlowConfigured=false e snapshots de progresso vazios).


## Lote11 — reprodução mais fiel e papéis adicionais

- B10Retry01 compilou sem erro/warning CS,1155 testes com1154 Passed/1 erro no UnityTest existente LobbyUsesDedicatedDirectoryWithoutStartingLocalServer. Native callstack Release of invalid GC handle aponta UnityWebRequestAsyncOperation na troca de domínio. Teste isolado Batch10DomainRetry passou1/1 sem alterar código. Baseline completo ainda será repetido; nenhum build B10 foi produzido.
- B09 hall-02 concluiu sequência de chip/portal com2 Blue Louies, estrelas e ambos montados visíveis nos PNG18/21. Roteiro genérico falhou na primeira pausa: Hall fora do duelo proíbe pausa explicitamente em IsPauseAllowedInCurrentScene. B11 verifica rejeição nativa nesses2 pontos, mantendo reload e sem alterar regras de produto. Não confundir com falha das luzes/respawn.
- NG28 fixture anterior montava depois da intro; não cobre retorno já montado. B11 salva Blue no estado persistente, recarrega o Hall pelo fluxo nativo, verifica os2 mounts e só então entrega o chip.
- Novo caso portal: revela portal nativo3-1, TrySelectStage+ConfirmSelection, registra alpha do corpo de ambos os atores no servidor/clientes, exige blink/alpha0 e entrada/restauração na Stage3-1. Valida NG17 e oferece capturas do reveal NG36, sem coroutine de mundo no cliente.
- Novo -normal-game-host (Development opt-in com save isolado): StartHost real no manager/KCP do lobby, aguarda cliente remoto real, ServerStartCoop nativo e caso Easy; StopHost/teardown ao fim. Browser desliga diretório somente nesse fixture local, mantendo gate de compatibilidade de produção. run-host.py preparado, não executado ainda.
- Gate fixture aguarda8s antes de ForceUnlock para registrar porta fechada após câmera aproximar; não alterar conteúdo/sorting sem comparar essa fase.
- B09 water33-01 e gate25-01: dedicado+2 clientes150s, cenários/pausa/reload completos sem exceções. Entrada na corrente confirmada, mas deslocamento/predição/efeitos ainda exigem análise de dados/imagem; não marcar NG26/13 resolvidos por conclusão do runner.
- Fila B09 sessão76593 terminou. Sessões de build/runtime anteriores encerradas; próximo build Batch11 vai incluir todas as mudanças B10/B11 e repetir baseline inteiro antes dos binários. Ainda protocolo74 (fixtures não alteram wire). Para próximos testes usar binários B11 quando prontos, preservando B09 como evidência anterior.


## Retomada após limite — B11 e investigação de host

- B11 terminou com baseline completo1155/1155 Passed, zero failed/skipped. Builds Windows637ac88217ff95b2e4043f0815cd224a225e3c3ddc072076189359a6211b8cab e Linuxaf00b399445e562a86acd6c2203a0579a03f8bde5ef1b5805f423662b97ac7c4, protocolo74. Identidade/source.patch preservados em Build/NormalPlaytest/Batch11.
- Host-easy-01 reprovou antes das mortes: Unknown message id50207 = CoopStageStatus recebido pela conexão local sem handler; Mirror desconecta P1, deixando apenas P2. Não é evidência de reprovação da rotina de respawn. Runner terminou por timeout180s, encerrou apenas seus processos e resultado diagnóstico foi preservado.
- Fonte pós-B11 corrige todas as publicações de apresentação puramente remota (status/sons/música/FX/loops/máscaras) via SendPresentationToRemoteReady, excluindo conexão local e peers não autenticados/prontos. Host mantém apresentação nativa. Novo teste NativeHostDoesNotReceivePureClientWorldPresentationMessages cobre envio remoto e rejeição dos demais papéis. Teste direcionado Batch12HostTarget em execução; ainda sem build novo.
- Fila B11 sessão17524: boiler22,clown-light,hall-mounted,portal,credits,gate25,180s cada. Boiler já registrou cobertura38/38 células de fogo e5 quadros da porta nos DOIS clientes; precisa fechamento/pausa/reload e comparação offline antes de aprovação.
- Offline Easy B09 também concluiu4 mortes/4 retornos e recuperação de movimento dos2 atores; host permanece pendente.
- NG26: hipótese de input sintético apagado fora do tick foi descartada pela leitura do serverHeldMask persistente. Não mudar regras com base nessa hipótese. Cenário anterior teleportava diretamente em corrente curta e não cobre entrada caminhando/contradireção; precisa reprodução natural antes de corrigir predição.


### Retorno do usuário: fogo nos tiles finais e janelas encerradas

- Usuário confirmou que os tiles finais do fogo2-2 ainda não estão visíveis. NG06 permanece aberto. Telemetria38/38 em B11 mede sprites habilitados em cena, não prova visibilidade/oclusão no backbuffer. Câmera B11 ficou nas caldeiras; fonte B12 acrescenta segunda observação20s com câmera/atores próximos do último waypoint, mantendo fogo nativo.
- Usuário informou fechamento manual de algumas janelas. B11 clown-light-01 interrompeu antes de worldCompleted, com desconexãoP2 e encerramento dos clientes; classificar execução interrompida, repetir com nome novo. Não tratar como regressão de luzes/pausa sem reprodução íntegra.
- B11 boiler22-01 concluiu180s/pausa/reload/novo stream sem exceções, porém NG06 NÃO aprovado pelo resultado do runner. NG07 também aguarda comparação visual offline.
- Batch12HostTarget passou1/1 (regressão de mensagens remotas/host). Batch12 baseline completo e builds em execução; nenhuma alteração de wire/protocolo74.


### B12 — gate completo aprovado e fila seguinte

- Full baseline1156/1156 Passed, nenhum failed/skipped. Python RoomDirectory16/16 e Ci218/218 passaram novamente. UTF-8/BOM estrito dos62 C# alterados passou; git diff --check sem erro (avisos de política CRLF do Git permanecem informativos).
- Build Batch12 sessão37985: Windows em andamento, Linux depois. NÃO editar fonte C# até as2 cópias imutáveis terminarem. Fila Batch12 sessão88318 aguarda ambos os hashes: offline boiler finais, host Easy, offline Hall persistido, offline créditos; empacota b12 e depois online boiler finais,Clown,portal,créditos,gate25,bubble33 R08.
- B11 fila17524 terminou. Hall-mounted-01 passou180s, native persisted mount entry/chip,2 pausas corretamente rejeitadas no Hall e reload/new stream. Imagens mostram Blue Louies visíveis; ainda comparar offline/fases exatas. Portal-01 falhou no setup TrySelectStage porque save isolado não tinha Stage3-1 desbloqueada; B12 runner acrescentará somente no save isolado do caso portal. Fonte de jogo não precisa afrouxar desbloqueio. Credits-01/gate25-01 foram rejeitados pela reserva de memória ANTES de iniciar; pastas vazias preservadas. Usar outro lote/nome para repetir.
- Diagnósticos3 papéis B11 boiler/clown/Hall/portal arquivados com manifests SHA256. Clown interrompido não foi aprovado.
- NG12 investigação: Stage2-5 contém PushIndestructibleTileHandler (guidb15fab...), não instancia prefab Pusher (guid7ddf...). Caso gate sem bomba não exercita movedores, por isso relatório R02 B09 não contém família Pusher. Testar explosão nativa em tile empurrável, não alterar FPS do PusherPatrolAndPush por suposição. Correção de rastreio imediato no moveVisual já está na fonte, mas falta prova de criação/translação/descarte da vida0.5s.
- NG06 caminho final authored termina em(3,14),(3,13),(3,12). PowderIgniterFlame SR order0 e Ground order0/Chunk tornam sorting/oclusão hipótese concreta; confirmar na nova câmera dos tiles finais e offline antes de mudar ordem. Contagem de SR habilitados não comprova pixels finais.


### Próximo lote de reprodução (após B12 imutável)

- NG28 ainda tem lacuna: fixture Hall persiste mounts antes da intro, mas injeta revealingPart/Start manualmente depois. Para fluxo exato, chamar World3HallChipAssemblyController.PrepareReturn("Stage_3-1", true) ANTES do ServerChangeScene/LoadScene e deixar Awake/Start nativos consumirem pendingReveal/PresentationBlocked. Não fechar relato só pelas imagens da aproximação atual.
- NG24: testar cart31 com bomba real de outro jogador no ExitTile, fuse longo; entrada/saída nativas e acompanhamento de ServerPlayerBounce/DeferBounceForApprovedCart, restauração de controles e corpo. Resolver ignora Bomb!IsSolid; verificar condição real antes de mudar regra. MoveOneTile usa MovePosition, mas inputLocked deve bloquear motor; não atribuir automaticamente a mesma causa do tornado sem reprodução.
- NG11: InfiniteBackgroundScroll cria material em Awake e usa SpriteRenderer Tiled size42x32 + _ScrollUv; frame genérico não transporta esses parâmetros. Divergência visual concreta, FPS ainda sem causa provada. Se estender receita, manter template autorado/MPB e geometry limitados, sem coroutine de mundo/RNG no cliente; aumentar protocolo e repetir regressão se alterar wire.
- NG26 precisa entrada caminhando e contradireção reais dos2 donos. Não usar teleporte direto como aceite de predição. Desenvolvimento opt-in pode enviar comando sintético pelo input/transporte existente, com estado ativo da corrente/posições autoritativas observadas.


## Lote13 — fonte posterior aos builds B12

- B12 completos protocolo74: Windows e095d69db6326bf319cce335372d1570636ab8d053fcaacbd5bec175ac134bc7; Linux440a5afffb5d4536ef8d2eb93b347d042585696530cc744d3cb5bb7cfe291231. Source.patch/identity imutáveis preservados.
- B12 offline boiler passou51 PNGs/caminho38/38/porta6frames. offline034 mostra fogo vertical nos últimos waypoints na câmera final; final-review.png compõe observação32–51. Não fecha NG06 online, usuário confirmou lacuna nos clientes. Offline Hall fixture anterior passou36 PNGs (ainda sem pendingReveal exato). Offline créditos passou: chefe final nativo derrotado→créditos→Start skip→mensagem final→Start→TitleScreen, todos os marcadores confirmados.
- B12 host Easy avançou2 jogadores/primeiros retornos, mas na pausa recebeu Unknown message32046=OnlinePauseMessage, desconectou local e gerou NRE secundário no fixture por ator destruído. NÃO atribuir esse caso ao fechamento manual de janelas do usuário. Corrigir caminho da pausa antes de repetir; respawn host ainda não aprovado.
- Fonte B13: pausa usa envio somente a remotos prontos. Host registra handlers compartilhados de domínio por RegisterRemoteWorldHandler, que consome sem reaplicar efeito quando NetworkServer.active (evita outros unknown Minecart/Blackout/etc sem duplicar mundo nativo). Dois testes direcionados de proteção de host passaram2/2 em Batch13HostTarget, sem erro/warning CS.
- Fixture Hall B13 usa PrepareReturn3-1/firstClear ANTES do reload, deixa Awake/Start nativos controlarem entrega e aguarda liberação da seleção; remove injeção manual de Start. Portal fixture também desbloqueia3-1 nativamente no save isolado, cobrindo offline.
- Novo caso blocks em2-5 usa resolver/handler nativos,6 movimentos com destino aprovado pelas consultas reais; câmera autoritativa acompanha cada bloco sem mover jogadores para lava/hole. Ainda não compilado.
- Water fixture B13 substitui teleporte direto por aproximação externa ao collider, input real do dono pelo transporte nativo e contradireção durante push, para ambos os donos. Seleciona corrente ativa com maior corredor livre e registra candidatas/entrada/saída/deslocamento. Mensagem de input exclusiva Development opt-in, limitada a direções/duração/cena, sem simulação de mundo nos clientes. Ainda não compilado, não chamar correção da predição.
- Fila B12 sessão88318 segue boiler-final online,Clown,portal,credits,gate25,bubble33. Custo sessão48575 aguarda bubble33 terminar. Nenhuma fonte B13 entrou nesses binários.


### NG02 validado; novo ajuste NG06 em B13

- Clown B12 concluiu180s com2 clientes reais/dedicadoLinux,2 cargas de cena, pausa/resume/reload/novo stream sem exceções. PNG12 dos2 clientes comparado ao offlineB07 PNG9: escurecimento+foco circular corretos. Logs de CADA cliente confirmam14 amostras enabled/active/alpha0.9 na carga inicial e15 depois do reload. visual-review.json e diagnósticos3 papéis arquivados. Aplicação do spotlight/fade exclui host; sua apresentação nativa offline fica preservada; sem alteração de controle COM. NG02 encerrado no escopo relatado.
- NG06 fonte B13 acrescenta SetGroundSorting:6 orientações de chama acima da ordem/camada do Ground Chunk; Steam conserva ordem autorada. Isso evita que a âncora única do chão encubra chamas na ponta do percurso. Novo teste cobre prefab real/todas orientações/Steam preservado. BoilerTrace agora também dispara até12 PNGs quando renderers das3 células finais estão ativos e enquadrados, com posição de tela/ordem/alpha registrados; não depender só de amostragem1Hz/contagemSR.
- B13 build/baseline sessão41951 em andamento, ainda não aprovado. NÃO editar C# até ambos os builds prontos. Fila B13 sessão14180 aguarda: hostEasy+hostBoiler; offlineBoiler/Hall exato/Water entrada real/Blocks; onlineBoiler/Hall/Water/Blocks/Portal/Créditos/Gate/Bubble33.
- Filas B12/Cost antigas foram encerradas SOMENTE nos PIDs próprios23392/20352, mantendo o filho Clown até completar normalmente. Após seu término não há mais runtimesB12 ativos. Casos B12 restantes passam paraB13 para evitar reserva de memória durante compilação. Nova fila de custo sessão58016 aguarda B13bubble33 e ausência de Unity/SB6 antes de cada janela; B12binários mantidos como baseline de desempenho. Nenhuma janela de custo foi executada ainda.
- Total da tabela agora3 validados/28 implementados aguardando validação/8 sem solução confirmada. Não confundir ajuste de sorting do fogo com aceite final.


### B13 baseline e próximos fluxos de UI

- Baseline B13 passou1158/1158, zero falhas/skips, incluindo ordenação do prefab de fogo e consumo sem reaplicação no host. Windows/Linux ainda em build na sessão41951. Fila atual14180 (substitui91825) inclui também hostBoiler; custo58016 espera fim de B13bubble33 e ausência de Unity/SB6.
- Para NG38 runtime, já existe Tools/RoomDirectory/smoke_owner_flow.py e LocalOnlineFlowSmoke dentro LocalServerLoadTest.cs. Esse fluxo usa servidor SEM -load-test-server, recebe OnlineConfigureRoomMessage(ownerSave) real, passa SkinSelect→SaveFileMenu→WorldMap→Stage1-1 e confirma slot1Hard nos2 clientes. O observer sai após8s de gameplay por desenho do smoke; um caso específico de unlock deve aguardar o delta/toast antes de sair. Não combinar ingenuamente -load-test-server: TickLocalLoadTest chama ServerStartCoop diretamente antes da configuração e ownerFlowConfigured continua falso.
- GlobalUnlockController e UnlockToastPresenter são persistentes DontDestroyOnLoad, portanto não são desativados pela limpeza de scripts da cena co-op. Hipótese de whitelist descartada por leitura de Bootstrap/EnsureInScene. Testar delta real de UnlockProgress através do owner-save flow, sem mexer em saves do usuário.
- UI chat real ainda pode ser validado pelo T/Enter nativo (OnlineChatOverlay usa InputSystem), nos2 clientes renderizados, com textos PT/EN/ES/JP e rascunho comprido. Não há smoke de chat já pronto; previews offline4 idiomas e testes de alinhamento não substituem esse cenário.


## B13 — NG05/06 validados e fonte B14 iniciada

- HostEasy B13 passou4 mortes/4 retornos, movimento local2/remoto2, pausa vivos/rejeição durante morte, StopHost client=False/server=False, sem exception/unknown-message. Evidência de dano normal suficiente, mas cobertura por buraco ainda pendente, por isso NG03 continua implementado em validação.
- HostBoiler B13 passou caso nativo/teardown sem erros. PNG host boiler-final12-cell3-12 e remoto boiler-final11-cell3-13 mostram faixa contínua superior e descida final; offline B13 completou63 PNGs. Dedicado boiler-final-01 concluiu180s/pausa/reload/novo stream sem exceções; PNG boiler-final11-cell3-12 em CADA cliente mostra a ponta final acesa (fase de expiração diferente entre relógios).38/38 células observadas, imagens durante vida das3 células finais, order1/alpha1. Overlay vermelho presente nos2 clientes e comparável offline/host. NG05/06 encerrados; visual-review.json e diagnósticos3 papéis +host/client arquivados comSHA256.
- NG07 cliente registra5 quadros da porta/offline6, mas ainda falta revisão de backbuffer dessa animação; não encerrar só pelo contador.
- Hall offline B13 (PrepareReturn antes do reload, entrega nativa) passou26 PNGs e liberação da seleção. Blocks offline B13 passou6 movimentos/20 PNGs. Water offline B13 falhou setup: escolheu WatterCicleRigth center38,2/corredor1, caminhou até37,2 e nunca iniciou activePushes. Processo próprio23124 foi encerrado após falha preservada para evitar esperar170s; resultado interrompido não é aceite do recurso. B13 runner water online será NOT RUN/exit2 por pré-condição offline reprovada, sem lançar peers; usar fonte B14 corrigida.
- Fonte B14 (ainda NÃO compilada): water testa aproximações dos2 lados a partir do primeiro contato, descarta caminho inicial bloqueado e prepara sala/câmera por LocalEnvironmentSmoke.PlacePlayers, reaproveitado internal. Easy ciclo1 agora chama KillByHole real (antes era TakeDamage(false), que NÃO era buraco); asserção usa actor.isDead, mantendo4 mortes/retornos. Não reinterpretar evidência B13 como buraco testado.
- Chat UI B13 executou180s íntegro, mas tentativa interativa ficou inconclusiva. Histórico/presença visíveis, digitação por sky.type_text não apareceu; não foi comprovado defeito de altura. Fontes consultadas têm métricas compatíveis em princípio. Ferramenta perdeu binding na troca de janelas e2 activate_window retornaram timeout; parar inputs dessa seleção. Validar via roteiro InputSystem ou nova janela estável. Nenhuma mudança de layout baseada só nessa hipótese.
- run-offline.py agora escreve falha e encerra apenas seu processo ao detectar exceção nativa, depois exige completed/exit0; evita150 PNGs de um fixture já abortado. run-host.py preserva runner-failure.json em exceção/timeout e valida unknown-message também.
- Atual:5 validados/26 implementados pendentes/8 pendentes de solução. B13 fila41209 continua Hall,Blocks,Portal,Créditos,Gate,Bubble33 (Water pulado conformeacima). Custo46125 aguarda queue-completed.json B13 e ausência de Unity/SB6. B13 builders41951 terminaram; Windows816901d09642bf0d04daf4d1b2267c3d947c3db7a67b389379d21e1114a85103, Linuxa189ee75aeeb97aeb38af831cf701f40e57747ad406b6d8c6168bfdafddd226a; protocolo74. Python16+218 passaram também após B13, UTF-8/BOM64 C# aprovado antes das2 mudanças de fixture B14.


## Retomada — resultados finais B13 e custo inicial

- Filas B13/custo terminaram; queue-completed.json presente, sem processos SB6/Unity/Python de teste ativos na retomada. Não relançar lote inteiro por ausência de sessões antigas.
- Hall,Blocks,Portal,Gate,Bubble33 B13: dedicadoLinux+2 clientes180s concluídos sem exceções/pausa/reload quando aplicável. WaterB13 não executado online (setup offline inválido, usar fonteB14). CreditsB13 passou validações especiais de entrada/skip/final/desconexão dos2 clientes; worldCompleted=false é esperado no encerramento nativo, não falha. OfflinePortal B13 passou34 PNGs. OfflineGate passou24 PNGs. HostHall eHostPortal também concluíram caso/StopHost sem erros; diagnósticos host/client arquivados.
- NG17 encerrado: servidor e CADA cliente mostram, para CADA ator,4 estados alpha0.45 e4 estados alpha0, iguais em host/remoto; entrada em3-1 e estado sem locks restaurados. Fluxos offline/dedicado/host passaram; revisão em portal-01/visual-review.json.
- NG36 encerrado: máscara do reveal comparada entre offlineHall022, dono/observadorHall023, hostHall008 e remotoHall018; extremidade inferior oval e topo moldado correspondem à renderização nativa. portal-mask-review.json preservado. NG28 continua pendente de variante adicional de Louie; Blue está visível em entrega nativa nos papéis testados, sem causa específica atribuída.
- Custo B12:4 execuções150s, sem Editor/build/PNG/motion capture, janelas de60s entre coop-world started e primeira pausa.2-4 clientes59.53–60 FPS, trabalho médio1.880–2.047ms;1-1 clientes59.99–60FPS,1.739–1.813ms. Não reproduziu queda sustentada no cenário parado. cost-summary.json contém todos os papéis/janelas/GC; ampliar para caminhar+bombas/destruição antes de fechar NG11.
- Fonte B14 adicional (NÃO compilada): ApplyCoopMapPose evita setters iguais de pose/escala/cor, preservando escala aprovada se o parent mudar. Teste cobre Transform.hasChanged e escala mundial; switch Development -normal-map-pose-unconditional permite A/B no mesmo binário. Caso combat dirige ambos os donos pelo input real (movimento+ActionA),40s de bombas/caminhada; atualizar runner inspection cases para aceitar combat. Fixture Gate afasta atores4tiles para desobstruir imagem da porta; comparar closed/open com offline. Não mudar arte/crop baseado em imagem encoberta por jogadores/nomes.
- Total:7 validados/24 implementados aguardando validação/8 sem solução confirmada. B14 ainda precisa compilar todas as mudanças desdeB13, repetir gates completos e executar novos casos.


## Retomada após limite — B14 (13/11, noite)

- Nenhum Unity/SB6 de teste ativo ao retomar; filas B13 e custoB12 já concluídas. Janelas encerradas anteriormente não foram contadas como sucesso. Artefatos de cada execução permanecem separados.
- Python RoomDirectory 16/16 e Ci 218/218 aprovados nesta retomada. Check UTF8 estrito dos 65 C# alterados aprovado (sem prefixo corrompido/duplicado ou replacement).
- B14 congelado em Build/NormalPlaytest/Batch14/source.patch, patch SHA81cfd0151e24c789b28f084d862a97442b2b2c9304a82cbcb99b884fb040f52a. Suíte completa/builds ainda em execução; NÃO confundir compilação C# sem erros com aceite runtime.
- Fila Build/NormalPlaytest/queue-b14.py aguarda build Linux imutável: Water offline natural, Easy com buraco, Hall Blue/Pink, frames porta2-2, porta2-5 desobstruída, host Easy/Créditos, dedicado chat/Easy/Hall/Boiler/Gate/intro2-6/intro2-7; Water dedicado só se abordagem offline completar. Resultados individuais e queue-progress.json são a fonte de verdade.
- R02 de B13 blocks25-01 é válido, sem dropped/overflow. Exatamente seis IDs UnclassifiedWorldVisual, correspondentes aos seis pushes nativos do fixture. 171 frames nativos em movimento, 166 amostras de forma: erro máximo0px/p95 bin0.1px; cada client169 frames em movimento,169 interpolados,0 stalls/late/starved. Passos máximos1.933/1.819px. Capturas de cenário offline e ambos clients preservadas. Falta fechar revisão/host específico antes do aceite NG12. Relatórios r02-report e moving-block-analysis.json no run.
- CustoB12: quatro execuções sem captura/Editor, 2-4 e1-1 repetidos, clientes~59.5–60fps. Não houve queda sustentada em repouso; combate B14 preparado para evitar declarar resolvido sem reproduzir a carga.


### B14 tentativa inicial rejeitada pelo gate

1159 testes executados:1158 passaram,1 falhou no novo teste RepeatedStaticMapSnapshotsDoNotDirtyTheTransformAndStillFollowParentScaleChanges. A hipótese de que os setters idênticos marcariam Transform.hasChanged era falsa no Unity6000.6: também no caminho original o flag permaneceu false. Removidos a otimização sem benefício demonstrado, seu switch A/B e o teste baseado nessa hipótese; não se alterou tolerância nem se ocultou uma regressão existente. NG11 segue pendente, com combate preparado. Nenhum player foi compilado/validado a partir da tentativa reprovada.

Batch14Retry01/queue-b14r1.py é a nova tentativa ativa. Acrescentado host-blocks para fechar a cobertura do NG12. A observação Easy agora reconhece isDead/renderer Fall além de life0: morte nativa por buraco pode preservar health.life e não deve ser confundida com ausência de respawn. Asserções de retorno/controle continuam exigidas. Python da tentativa anterior aguarda nada: queue-b14 foi encerrada pelo PID exclusivo após verificar CommandLine; nenhum processo de jogo foi encerrado.


### B14Retry01 — gate aprovado / builds em execução

- XML Logs/NormalPlaytest/Batch14Retry01/editmode.xml:1158/1158 Passed,0 failed/skipped. Python novamente16+218 aprovados; protocolo real incluído. UTF8-check.json:65C# válidos. Nenhum warning/error CS nos logs consultados.
- Patch0abfc7f9c6dba59c188009d554dc7c73896f3f549a3244d1c23397493e6c270a; Windows SHA78d6b8c90561e09780018e362c7b8becfbabbf4c8c6e1e6ae26b906cdd6f6aa5. Linux ainda compilando. Fonte permanece congelada até terminar os dois builds; fila70129 aguarda hash Linux. Fila inicial B14 encerrada após falha de hipótese; build ativo sessão31307.
- Mover blocos NG12: r02-report válido +6IDs/6movimentos já analisados; host-blocks adicionado à fila para fechar papel host explícito. Gate25/Bubble33 B13 tiveram diagnósticos três papéis arquivados nesta retomada.
- Próximo lote tem rascunhos opt-in em Build/NormalPlaytest/b15-audio-methods.txt e b15-music-methods.txt, NÃO aplicados/compilados. Audio: GetOutputData/RMS de cada AudioSource para provar saída real, gate Room2 via destruição nativa dos cores e gelo Room1 via duas entradas/saídas físicas; conferir configuração e revisão dos setters antes de aplicar. Music: reiniciar intro após20s, registrar clip/time/dsp/loop/peak, usar Sky em uma janela própria retornada por list_apps para minimizar de fato e observar passagem intro→loop +2voltas. Rascunho120s pode precisar duração maior conforme clip; não basta contador de mensagem nem Application.isFocused para alegar minimização.
- Skill computer-use lida (SKILL/guidance/api/confirmations) para futuro teste de minimização, sem ação de UI nesta retomada ainda. Não tocar Snes9x/Discord/Aseprite nem usar handles antigos. Não existe API Sky minimize direta: observar botão/Alt+Space+menu e atualizar estado entre ações.


### NG12 fechado — movimento de blocos em2-5

B13 blocks-offline-01 realizou6pushes nativos; B13 blocks25-01 realizou os mesmos6com Linux dedicado e2clientes renderizados, pausa/retomada e reload aprovados, sem erros. Os seis IDs de UnclassifiedWorldVisual correspondem exatamente aos seis transientes PushIndestructibleTileHandler. R02:171frames nativos em movimento,166amostras de forma, erro máximo0px/p95bin0.1px; em cada cliente169frames em movimento e169interpolados,0stalls/late/starved. Não se confundiu com PusherPatrolAndPush (não é o mecanismo dessa fase). B14R1 host-blocks-01 fechou o papel host+cliente real:6movimentos, conclusão e StopHost limpo, sem erros/UnknownMessages. Mesmo código de produção B13/B14R1, registros identificados por DLL/patch. Cenário e capturas nativas/cliente conferidos. Aceite NG12: correção de registro imediato do transiente preserva a animação curta, sem limitar o deslocamento a poucos snapshots.

Contagem atual8validados/24implementados pendentes/7pendentes. NG03buraco passou offline ehost (4retornos;ambos controles2x); falta dedicadoB14R1. NG07 tem PNGs offline dos frames0–4 mostrando portas das duas caldeiras abrindo; falta comparação dos clientsB14R1. WaterofflineB14R1 falhou corretamenteantesdoaceite: corrente escolhida estava coberta por grama destruível no tile(-4,0),confirmado no YAML ePNG. B15fonte já alterada (ainda não compilada): bomba nativa limpa a cobertura antes de entrar, outro jogador estacionado fora da aproximação e espera por fim da intro. Probes/casos environment/música e asserção de bombas reais no caso combate também aplicados após terminarbuildsB14R1. Não confundirfonteB15comDLLsB14R1.


### NG37/39 fechados — fontes e créditos

NG37: chat-01 B14R1 concluiu cenário Linux+2clients com pausa/reload/teardown sem erros. Cada cliente digitou5mensagens pelo teclado virtual InputSystem na rotina nativa (PT/EN/ES/JP/rascunho longo), os10envios tiveram eco real do servidor. PNGs de rascunho/histórico revisados: japonês renderizado, cursor/finaldo texto longo cabem, nomes escuros com contraste, letras maiores legíveis. Altura preferida7.01px/JP8.56px em retângulo14px, sem inventar aumento de altura por falha do Sky. Prévia lobby/nome4idiomas B09 já aprovada; overheadmaior também aparece nos hosts. Aceite é fonte/layout; não afirma que a injeção externa/foco Windows anterior tenha passado (política de foco em produção não mudou).

NG39: host-credits-01 B14R1 passou nativamente: FreezerVenus morto, créditos entram, Start aprovado salta rolagem, mensagemfinal visível em host eguest, Start retornaTitleScreen/fechaambos transportes sem erros. PNGhost037 mostra100% ePNGremote049 mostramesma tela final. ComplementaofflineB12e dedicadoB13credits-01 comdoisclients. Ofixtureautomáticosem ownerFlowConfigured não pretende validarpercentual doobserver (0% nessefixture), persistência ou toast; esses ficam noNG38viafluxoreal menus/owner-save. Evidênciaaceitedaaparição/skip/retorno:accepted-ui-ending-review.json. Contagem10validados/22implementadospendentes/7pendentes.


### NG03 fechado — respawn Fácil, incluindo buraco real

B14R1 completou easy-hole-offline-01 (50 PNGs), host-easy-hole-01 e easy-hole-01 (Linux dedicado, dois clientes renderizados). Cada execução autoritativa teve quatro mortes/retornos: explosão individual, buraco nativo e morte simultânea; ambos os jogadores recuperaram movimento duas vezes. O dedicado também completou pausa, retomada, recarga de cena e encerramento sem erros. Host encerrou client=False/server=False. Complementa a regressão B09/B13 com mortes por dano, retorno sem arco de Revenge e rejeição de pausa durante morte.

Os logs brutos de apresentação registram 5/4 observações de retorno nos clientes (o detector vê life/Fall), e não devem ser apresentados como mortes únicas. A autoridade registrou exatamente quatro retornos e os dois owners recuperaram controle duas vezes. Evidência: easy-hole-01/acceptance-review.json, result.json e diagnósticos dos três papéis; host e offline têm resultados separados. Contagem atual: 11 validados, 21 implementados com validação pendente, 7 pendentes.


### NG07 fechado — animação das portas2-2

B14R1 boiler-door-offline-01 e boiler-door-01 (Linux + dois clientes) completaram. PNGs capturados quando o frame mudou mostram as duas chapas fechadas, a retração parcial e as passagens abertas, em paridade com offline. Client1 capturou frames0–4; client2 capturou0/1/2/4, sem afirmar que o probe amostrou todo edge de20Hz. Host boilerB13 já completou animação nativa e cliente remoto, além de encerramento limpo; dedicado completou pausas/reload. Correções compartilhadas de apresentação/bounds mantêm a animação visível. Relatório door-animation-review.json no run. Contagem12validados/21implementados pendentes/6pendentes.

NG28: o retorno preparado com mounts Blue/Pink passou offline, dedicado e host; PNGs durante a entrega mostram ambos visíveis. Ainda mantido pendente até B15 chip realizar coleta real → commit nativo → Hall, pois injetar montaria persistida antes do reload não cobre perda durante a conclusão da fase. O novo caso chip já está na fonte B15, não compilado. Esse cenário também cobre a coleta das estrelas de NG22; a entrega já tem evidência em todos os papéis.


## Próximo lote B15 — conteúdo e fixtures congelados para gate

- B14R1 completou todos os runs da fila, mais host-hall-pink-01. Ninguém ficou ativo após encerrar. Comparação dos PNGs do portão2-5 mostrou o teto também encoberto no offline: ambos sprites tinham ordem1, igual à parede Chunk; spriteY15.5 e âncora da paredeY9 provocavam cobertura. B15 altera apenas os dois renderer orders para2 na cena Stage_2-5. Teste de regressão usa OpenPreviewScene e confere os dois estados acima da parede. Ainda precisa renderizar a mudança, não é aceite NG13.
- NG09: além do desenho já substituir o assento, RestingLocalPosition ainda somava o deslocamento da montaria ao assento externo. Corrigido para refletir a mesma base renderizada; estendido BoatSeatReplacesMountedHeadOffsetAndRestoresItOnRelease. Precisa gate e runtime do caso boat.
- B15 LocalPredictionSmoke possui casos novos, opt-in Development: environment (R08 + gate3-1/gelo3-5, probe de saída AudioSource), music (duração derivada dos clips, duas voltas reais, volume/peak/dsp/foco; precisa Sky para minimizar de verdade), unlock (servidor só dispara após ownerFlowConfigured real; dois clients menus/slot1Hard, evento/mesh do toast, duplicata rejeitada), chip (coleta física em portal criado pelo gate → commit → Hall preservando Blue/Pink), pink (input ActionC, EOF observa sombra após pouso), boat (Mole/Tank, quatro direções), cart (outro owner coloca bomba sólida com fuse pausado na saída nativa), rooms (carrinho ativo em3-2 + transições nativas; em3-6 verifica salas2/3; EOF captura primeiros frames), force (fase nativa expandida Hurricane12/13, deslocamento real + recibo do owner).
- O fixture de water agora usa bomba nativa para destruir a grama em cima da corrente, espera4.5s e separa o outro jogador. Nenhuma correção nova de predição de água foi aplicada sem esse repro.
- Combate agora exige observar bombas nativas; o custo em2-4 precisa rodar com Editor/jogos de outros cenários encerrados. Não chamar a medição em repouso de correção do FPS em combate.
- Antes de rodar B15: estender listas de casos do prepare-remote.py, limitar músicas por timeout adequado (offline/host padrão170/180s pode ser insuficiente para duas voltas; fonte limita duração500s), criar runner Linux de owner flow sem -load-test-server para NG38. Não alterar o protocolo para mensagens opt-in já compatíveis em campo; usar sempre os dois binários do mesmo lote.


## Retomada em 14/11 — B15 concluído, B16 em compilação

B15 terminou com baseline Unity 1159/1159 Passed e builds imutáveis Windows/Linux. A fila `Build/NormalPlaytest/Batch15/queue-completed.json` terminou sem processos de jogo restantes. Passaram cenários offline, dedicado com dois clientes renderizados e host para Pink, portão2-5 e coleta real de chip→commit→Hall; intros26/27 offline+host complementam dedicadoB14R1. Water33, tornado34, salas32/36 e mecanismos/áudio31/32/33/35 completaram offline e dedicado. Capturas e áudio ainda precisam de revisão antes de fechar IDs. Contagem mantida em12/39.

Três falhas de preparação foram isoladas: barco alcançava um BoatUnmountZone legítimo; carrinho foi procurado incorretamente em Stage_3-1 (o carrinho de campanha está em Stage_3-2); perfil CI já tinha todas as43 skins e rejeitava corretamente um novo unlock. B16 inicia barco em água longe da margem, calcula saída do carrinho32 e usa save isolado sem skins extras no teste unlock offline. Adicionado quit Development após retorno nativo ao Title para testar owner-flow real sem matar o processo antes de gravar o save. Nenhum save do usuário alterado.

Build B16 começou com fonte congelada; não editar fontes de jogo até terminar teste/builds. Python nesta retomada: RoomDirectory16/16 e Ci218/218 aprovados, incluindo regressão real de protocolo. Próximos: revisar B15; terminar B16; repetir somente casos corrigidos; fluxo real owner-save/menu para NG38; minimizar janelas próprias no caso música; custo de combate2-4; fixtures restantes de itens/água/projéteis.

## Revisão de capturas B15 — 16/39 validados

NG04: PNGs `pink-p1-grounded-shadow.png` e `pink-p2-grounded-shadow.png` revisados em offline, dois clientes dedicados, host e remoto. Sombra reaparece após salto, com logs de grounded/shadow para os dois jogadores; não depende só do término da coroutine. Montagem de revisão `Batch15/pink-review.png`.

NG13: teto do portão agora aparece inteiro acima do chunk de parede nos estados aberto/fechado. Revisados offline017/018, clientes world026/027, host013 e remoto026/027; iluminação escura é nativa da fase. Teste de preview-scene verifica ambos os renderers com ordem2 acima de parede1. Montagem `gate-review.png`.

NG22/28: fixture percorre coleta física do chip3-3, commit nativo e retorno ao Hall com Blue em P1 e Pink em P2. Estrelas visíveis na coleta e entrega; Louies continuam desenhados durante/depois da entrega em offline, dedicado/owner/observador e host/remoto. Capturas do Hall dedicado world045–057 e host22–34, remoto42–54. Montagens `chip-review.png` e `chip-hall-review.png`. B15 logs registram `native chip collection and mounted Hall return completed`, sem erros dos cenários. NG28 deixa de ser pendente: o fluxo real complementa o ensaio manual B14R1.

Contagem:16 validados,19 implementados aguardando validação,4 pendentes (NG11/24/26/29). B16 continua compilando; alterações acima são somente documentação de evidência B15.

## Investigações confirmadas nesta retomada — câmera e water pulse ainda abertos

B15 rooms36: cada peer registrou uma única câmera MainCamera ativa por frame (offline30, cada cliente40), mas nos primeiros frames da Room2 as posições de jogador no viewport chegam a x1.88. Portanto NG34 NÃO foi fechado apesar do marcador de cenário concluído. `World3RoomTwoTransition.MoveLivingPlayersToDestination` altera Rigidbody/Transform sem corte explícito do histórico NetworkTransform; câmera já muda enquanto raízes ainda interpolam o deslocamento entre salas. Preparado patch para ServerTeleport existente + reconcile, sob tela preta, sem novo wire. Ainda não aplicado durante a compilação congelada B16.

B15 water33: movimentos autoritativos nativos iniciam em x39 e terminam perto de41.5. Observador acompanha aproximadamente39→41; donoP1 apresenta45.83→47.88 e donoP2 34.39→35.33, retornando ao root correto ao soltar. Erros são exatamente +7/-7, opostos ao spawn inicial(-7/+7). `NetworkPlayerInput.OnStartLocalPlayer` desativa NetworkTransformReliable; seu OnDisable executa ResetState que zera lastDeserializedPosition DEPOIS de ler o spawn inicial. Deltas seguintes ficam deslocados pela origem perdida. Raiz livre mascarava o erro por reconciliação direta; water/autoridade lê o buffer corrompido. Preparado `PlayerNetworkTransform` derivado que mantém decoder ativo e só evita UpdateClient no dono, preservando interpolação dos observadores. Preparados testes de continuidade initial→delta, dono vs observador e prefab. Nada aplicado ainda enquanto B16 compila. Não desativar gates nem aceitar o cenário water apenas por terminar.

Python16+218 e integridade UTF8 estrita dos65 C# alterados passaram. Baseline B16 aprovado1159/1159; builds Windows/Linux ainda em andamento. Fila B16 aguarda ambas identidades imutáveis antes de iniciar três repros corrigidos e unlock com menus/owner-save reais.

## Revisão adicional B15 — 18/39 validados

NG16: montage `Batch15/intro-review.png` compara offlineB15, dedicadoB14R1 (ambos peers), hostB15 e remoto para2-6/2-7. Câmera percorre entrada→arena; apenas uma vista é desenhada, sem sobreposição antiga. Capturas do host iniciam após readiness e complementam o caminho nativo offline; remoto do host registra o pan inteiro.

NG29: saída real do áudio foi observada no próprio AudioSource: offline env33 `World3_BubbleCrashSfx`, ambos clientes env33 `World 3 Buble Crash` (picos0.109579/0.062745, SFX local0.5), e host-chip33 fonte nativa pico0.062666 + remoto0.109579. O som acompanha a quebra nativa da bubble e liberação do chip, sem duplicação host. Fonte bubble já publicava o evento no código inicial; não foi inventada uma causa nem alteração adicional nela. Aceite refere-se ao comportamento reproduzido na árvore corrente/B15. Registros em `Batch15/audio-reviewed.json`, logs originais preservados.

Contagem18 validados/18 implementados aguardando validação/3 pendentes(NG11/24/26). NG34 continua aberto com defeito confirmado nos primeiros frames da sala2.

## Incidente crítico de isolamento de save — 14/11/2026

A afirmação anterior de que todos os saves renderizados eram isolados estava errada. SaveSystem.SaveDirectoryPath exigia Application.isBatchMode além de SB6_SAVE_DIRECTORY; clientes renderizados/host/offline Development usam isBatchMode=false. O env estava presente, mas o código o ignorava. Os testes offline chamavam ResetSlot(0), portanto o primeiro slot real pode ter sido alterado. Interrompidos queue-b16 e todos os SB6 próprios B15/B16; nenhum jogo/Editor de usuário encerrado. Linux da tentativa privada possui timeout e não usa save do usuário. Não relançar binaries antigos.

Preservado save atual byte a byte em `Build/NormalPlaytest/SaveIsolationAudit20261114/current-user-save.dat`, sha256efc6c5adffa5c58c53d4f63b9bec89600323cc2722477149c89ef536e00f4d63. Segundo slot ainda tem22 fases concluídas e22 liberadas; primeiro slot Easy iniciado sem fases concluídas, terceiro vazio. Há backup de10/10 sem progresso em todos slots: NÃO restaurar essa cópia antiga, pois perderia o progresso atual. Auditoria de189 candidatos em candidates.json não encontrou cópia recente original do primeiro slot. Usuário foi informado e perguntado se o primeiro slot tinha progresso; resposta pendente. Recuperação fica aberta, sem inventar o estado anterior.

Correção B17: override explícito funciona em batch OU Debug.isDebugBuild; sessões normais sem env mantêm pasta usual. Fixture ValidateSaveIsolationGuard exige igualdade entre caminho efetivo e sandbox e loga antes do primeiro reset. Helpers offline/host/dedicado/real-owner recusam DLL sem esse guard. Nova regressão exercita resolução do caminho com/sem override. Validação seguinte deve provar caminho efetivo nos logs e SHA do save real inalterado. Evidências visuais/runtime anteriores continuam sendo observações reais; alegações de isolamento/persistência anterior NÃO são aceitas. NG38 persistência continua pendente.

B16 terminou baseline1159/1159, Windows c0d5ad17ab2e1351d639e6c8911a745dcbc0b169518a7a07407220b69abf2342, Linux02ba1430e3028ff39c75ed2a43b9cde28910f8871b6c994b71432fe2bf1b08af. Offline boat passou todas8 poses Mole/Tank; cart32 falhou por bomba ainda trigger (fixture movia atores sem Physics2D.SyncTransforms antes da inicialização da bomba, corrigidoB17). Unlock também falhava por uma segunda razão: todas43 paletas são defaults de SaveSystem.NormalizeData, não existe skin trancada por projeto atual. FixtureB17 usa unlock nativo da arena11 e evento OnBattleModeStageUnlocked, verificando toast e owner/guest separado. Não remover palettes padrão do jogo para fazer o teste passar.

MúsicaB15 offline iniciada e abortada: não vale como minimização. Segurança do Windows cobria janela SB6; Computer Use recusou clique na barra porque alvo real era PickerHost. Nenhuma ação no firewall foi feita. Pergunta assíncrona ao usuário para Cancelar aviso ficou pendente; teste pode ser repetido com B17 após isolamento comprovado.

Fonte B17 aplicada depois dos buildsB16 imutáveis terminarem. Em compilação; não editar fontes até finalizar teste/builds. Novos testes: decoder initial→delta após OnStartLocalPlayer, root dono versus observer, wiring de prefab e caminho de save renderizado.

## B17 automatizado completo e início runtime isolado

Baseline1163/1163 Passed, incluindo quatro testes novos; Python16+218 novamente Passed. BuildWindows eLinux imutáveis completos (identity.json/source.patch/untracked-assets.zip em Batch17). Nenhum warning/error CS introduzido. Todos68 C# alterados/novos passam UTF8 estrito.

Primeiros cenários B17: unlock offline passou arena11→toast New Stage→duplicata rejeitada; barco offline passou8 poses Mole/Tank; water33 offline passou abordagem natural/corrente/contra-input. Logs confirmam o caminho sandbox efetivo antes de qualquer reset; SHA do save real permaneceu idêntico em todos. O unlock de arena substitui corretamente a hipótese inválida de skin bloqueada (paletas43 já são defaults). Persistência online ainda precisa owner-flow real.

Cart32 B17 falhou antes da viagem: bomba solid=False/fusePaused=True. Causa de fixture refinada: TryPlaceBombAtIgnoringInputLock termina com isTrigger=true para permitir saída do dono. Se o teste planta longe do dono, não ocorre OnTriggerExit para solidificar. B18 agora põe o outro dono no destino, planta, aguarda FixedUpdate e o retira, deixando a física nativa solidificar a bomba. Nenhuma mudança de colisão de bomba feita para satisfazer setup artificial.

Fonte B18 de fixtures aplicada APÓS buildsB17 imutáveis: submerge(player/Blue/Pink), waterbomb(entrada nativa em água), items(ovo genérico/guest pickup/célula regenerada), blink(dano real carrier/core e tiro nativo Gurun na fase invisível), geyser(explosão real dos dois jogadores), bossitems(Mecha random/final, Magnet escape e Pretty defeat). Ainda NÃO compilada/validada. Scripts temporários b18-*-methods.txt foram aplicados; não reaplicar apply-b18-fixtures.py. Não iniciar Editor/buildB18 enquanto filaB17 tem clientes reais: preservar RAM e cenários. Próximos: resultados/capturas B17; owner-save; compilar B18; música realmente minimizada com avisoWindows resolvido pelo usuário; custo de combate2-4 em janela exclusiva sem Editor/captura.

## NG09 fechado com B17 — 19/39 validados

Barco HeadOnly validado offline, servidor Linux+dois clientes e host+remoto. Oito poses nativas por execução cobrem Mole/P1 e Tank/P2 nas quatro direções; cada uma verifica o offset do barco contra RestingLocalPosition do ASR. Valores variam entre as duas instâncias autoradas de barco (isso é esperado, não um offset por tipo errado). Revisadas capturas de embarque, direção lateral e retorno à montaria, incluindo offline018, clientes world022–028 e host002–008/remoto018–024. Montagem `Batch17/boat-review.png`. Sem erros de spawn/runtime; pausa/reload/teardown e StopHost concluídos. SHA do save real inalterado, sandbox efetivo confirmado em todos peers renderizados. Contagem19 validados/17 implementados aguardando validação/3 pendentes(NG11/24/26).

As capturas B17 são1920x1080 apesar de argumentos512x448: TitleScreen.Start aplicava fullscreen do perfil antes de Offline alterar o save. Isso não invalida as poses capturadas, mas os argumentos não provam resolução real. Fonte B18 agora define vídeo antes de TitleScreen.Start, aplica resolução explícita e loga dimensões/modo efetivos. O benchmark de FPS precisa registrar resolução real e incluir1080p; não chamar um teste512 de aceite para o desempenho1080p.

Compilação C# terminal B18: dotnet build com restore passou. Primeira compilação trouxe6 avisos em pacotes Mirror preexistentes; recompilação incremental das novas fixtures passou0 avisos/0 erros. Isso NÃO substitui baseline/builds UnityB18, ainda pendentes. Probe de áudio nativo passou a registrar clip real de PlayOneShot (source.clip é null nesses sons), com metadados bounded Development-only. Evitar concluir que som nativo está ausente por filtrar apenas nome de clip nulo.

## B17 water/câmeras e correção de host preparada no B18

Water dedicadoB17 terminou com captura completa e zero dropped/limit nos três processos. Ambos donos agora percorrem o corredor nativo, sem o salto de±7 tiles do B15; cada trajeto autoritativo nativo percorreu2.5 tiles. `Batch17/water33-01/water-owner-review.json` registra janelas/roots apresentados. Não inferir percentis com2–3 amostras por impulso nem comparar ptime bruto ao server-now sem o offset de interpolação. B18 amplia para quatro impulsos (entrada com/contra a corrente, P1/P2), pausa real no meio e host/remoto. NG26 passa de pendente para implementado com validação pendente. Contagem19 validados/18 implementados pendentes/2 pendentes(NG11/24).

Rooms36B17: offline e cada cliente dedicado têm zero jogador fora do viewport nos primeiros frames das salas2/3, uma única MainCamera ativa. Contudo remoto do host ainda teve10 amostras fora emRoom2 e9 emRoom3 (x até3.44), embora host estivesse correto. NÃO fechar NG34. `Batch17/room36-camera-review.json` mantém os resultados por papel. Causa: ServerTeleport da Mirror reseta a compressão no servidor e seu RPC local no host reseta novamente depois de um novo pacote já ser serializado. FonteB18 PlayerNetworkTransform.ServerRoomCut aplica/reset nativo uma vez e reutiliza o RPC existente; override OnTeleport descarta somente o eco local correspondente já aplicado. Não há mensagem/RPC novo nem alteração do wire/protocolo74. Novo teste simula eco atrasado após codificador já ter avançado, preservando reset em corte genuíno posterior. UnityB18 ainda pendente; C# terminal passou0 warnings/0errors após essa correção.

## Áudio31/32 fechado e filaB17 concluída — 21/39 validados

NG20: gate31 sala2 libera após cores nativos; fonte CoreMechanisms_RoomCompletedSfx offlineB15 e hostB17 apresenta saída não-zero. Cliente dedicadoB15 e remoto do hostB17 reproduzem World3GateOpened. Host/remoto picos0.106206 com SFX local0.5; fonte host usaOneShot(volumeScale), portanto volume1 do AudioSource não indica ignorar slider. Fluxo/pausa/StopHost passaram.

NG25: ganho nativo das três fases de erupção foi elevado para3 e o mesmo ganho viaja no evento aprovado. OfflineB15/dedicadoB15/hostB17/remoto registram saída real. Host fonte Vulcano Eyes Room3 tem picos0.072737/0.032641 no disparo e0.030432/0.010024 na queda; remoto traz EruptionStart, EruptionInTheAir e EruptionFall com picos correspondentes, volume local0.5 e escala deOneShot3. Não confundir VulcanFlameLeap (outro mecanismo) com os olhos do vulcão. Ganho é relativo ao slider, não aumento do volume global.

Contagem21 validados/16 implementados pendentes/2 pendentes(NG11/24). FilaB17 terminou. Todos runs concluídos após o incidente mantiveram SHA do save real inalterado. NG31 continua aguardando probe nativo com nome exato de clip no B18 (ambos clientes e remoto do host já reproduziram Ice/blocked).

Owner-flow01 falhou em preparação: nomes iguais rejeitaram segundo cliente; relay privado antigo morreu por UDP Windows10054 e derrubou conexão do primeiro. Corrigidos nomes únicos via-local-diagnostic-name (não escreve PlayerPrefs) e tratamento10054 somente no relay privado. Owner-flow02 chegou pelos menus reais a Stage1-1, slot1Hard/2jogadores, e ambos receberam eventos arena11 E arena13. Arena13 é desbloqueio automático legítimo do co-op, não duplicação. Fixture antiga confundia qualquer segundo evento com duplicata e lançou erro antes de capturar toast. Correção em `Build/NormalPlaytest/apply-unlock-distinct-events.py` está PREPARADA, ainda NÃO aplicada porque B18 já congelou a fonte. Aplicar somente após buildsB18 imutáveis terminarem; aceita eventos distintos, rejeita replay da MESMA arena e verifica cada toast enfileirado. NG38 continua pendente. Não mudar autenticação nem suprimir unlock legítimo para satisfazer fixture.

DriverB18 executou owner-flow02 e iniciou build completoB18. Não editar fontes enquanto ele roda. FonteB18 inclui corte seguro host+regressão nova, seis casos adicionais, quatro impulsos water/com e contra +pausa, fix de preparação física cart, áudioOneShot identificável e resolução real registrada. Baseline esperado1164. PróximoB19 precisa apenas corrigir o fixture de unlock e quaisquer falhas B18 descobertas; não chamarB18 unlock aprovado com o guard antigo.

## Próximos passos exatos para continuidade

1. Driver `Build/NormalPlaytest/driver-b18.py` iniciou teste/buildB18 depois do owner-flow02 falhar por guard de fixture (dois unlocks legítimos11/13). FilaB18 (`queue-b18.py`) já está aguardando as duas DLLs; não lançar uma segunda fila. Conferir processos/logs antes de retomar; sessões de ferramenta podem expirar sem encerrar processos.
2. FonteB18 está congelada durante builds. Após identidades Windows/Linux completas, arquivar os novos scripts/metas de acordo com hashes identity.json e então aplicar APENAS `apply-unlock-distinct-events.py`. Essa correção muda fixture, não produção. PrepararB19 com outras correções que resultadosB18 exigirem. C# terminal já passou0warnings/0errors antes do freeze; fullUnityB18 está em execução.
3. FilaB18 casos: cart32,water33,rooms36,rooms32,submerge31,waterbomb23,items25,blink36,geyser33,bossitems16,bossitems26,bossitems37(Hall),env35. Offline que falha não passa para online. Depois cada caso aprovado roda dedicado Linux+2clientes e host/remoto; force34 host complementa B15. Revise frames/JSONL/áudio/primeiros viewports, não aceite apenas inspection completed.
4. NG38: novo runner realowner em `run-real-owner-unlock.py`, dois nomes únicos e relay privado corrigido10054. Guard de fixture precisa distinguir arena11 da13, capturar ambos toasts e rejeitar somente mesmo índice repetido. Verificar ownerSave segundo slotHard, flags11/13 persistidas, guest slots/arenas inalterados e retornoTitle/quit nativos. B17ambos eventos chegaram, mas guard lançou antes do aceite visual. Não chamar01/02 aprovados.
5. NG19: ainda precisa minimizar de verdade via node_repl+@oai/sky, com skillComputerUse lida. AvisoSegurançaWindows bloqueava janela; não tocar no firewall. Foi perguntado ao usuário paraCancelar manualmente. B15music foi abortado e NÃO é aceite. UsarB18+ guard save e resolução confirmada; intro153s +2loops geram janela observação~298s, timeout650. Testar offline e dois clientes renderizados/host se diferente, logando duas voltas e saída real antes/depois de minimizar.
6. NG11: rodar custo exclusivo depois de todos Editor/jogos de outros cenários saírem. Caso combat produz bombas reais via inputs dos donos; exigir native bombs>0. Derivar runner sem screenshots/captura, `-r01-frame-stats -online-motion-logs-off`; comparar2-4/1-1 repetido, resolução efetiva512x448 E1920x1080 via argumentos B18, registrar dados porsegundo/GC/FPS/max. Não extrapolar B12idle para combate nem comparar resolutions diferentes.
7. Save real preservado em SaveIsolationAudit20261114; SHA continuaigual. Segundo slot22 fases. Pergunta sobre progresso anterior do primeiro slot permanecependente; não substituir por backup outubro vazio. Todos novos runners recusam DLL sem ValidateSaveIsolationGuard e guardam hash antes/depois do save real.
8. Quando tudo validado: baseline fullUnity/Python final da última fonte, protocolo real, UTF8/metas/fields, evidências imutáveis e manifesto de limpeza antes de restaurar apenas fontes/Physics geradas. Não limpar saves/recovery/caches ativos/scripts necessários. Sem commit/push/deploy solicitado.

## Retomada B18 concluído, B19 em validação — 23/39

B18 completou 60 registros da fila, incluindo arquivos de capturas, com save real SHA efc6c5adffa5c58c53d4f63b9bec89600323cc2722477149c89ef536e00f4d63 inalterado em todos. Baseline1164 Passed; Windows d07f2edc578278826849eb6189628fb47aea4462e8076cf2b6447f1263368e9c, Linux e99f36c8a3509d200805bd6fd9f3116612f585b8fb2d935014aaacb0d87596b2. Untracked arquivados antes das correçõesB19.

NG34 fechado: offline, ambos clientes dedicados, host e remoto têm uma MainCamera e zero players fora do viewport nas amostras iniciais das salas2/3. Montagem rooms36-review.png revisada nas cinco vistas; corte seguro evita reset duplo no host, com regressão automatizada. Não confundir marker final com a inspeção dos primeiros frames.

NG31 fechado: Ice e blocked identificados no AudioSource real e saída não-zero. Offline peaks0.000030/0.022248, clientes dedicados0.000030/0.022367 e0.001450/0.022248; host0.000030/0.022367 e remoto0.001450/0.022248, SFX local0.5. O fixture usa quebra/bloqueio nativos. Metadados OneShot identificam clip que não fica em AudioSource.clip; volumeScale preserva slider.

B19: ajustes de preparação, sem nova alteração de regra de gameplay. Blink nativo reduz alpha a0.2 (limiar antigo0.1 nunca detectava); RestoreMounted(None) não desmonta (usar ClearMountedStateForForcedArenaRemoval no setup); servidor sem gráficos verifica componente de água, clientes renderizados continuam exigindo material visível; Room3 é ativada antes de cada aproximação water. Coleta de ovo dedicada voltava ao spawn(-4,-4) porque fixture não cortava histórico de posição: setup agora reutiliza ServerRoomCut e mantém coleta física/critério de remoção. Erros de coleta passam a incluir estado/posição para diagnosticar repro que reste. Vídeo é reaplicado depois do Start da TitleScreen. Não declarar estes repros aprovados antes da repetição.

Compilação terminalB19 passou0errors; seis warnings já existentes em pacotes Mirror após restore. PythonRoomDirectory16/16 eCi218/218 Passed, inclusive protocolo real74. BuildB19 iniciado, sessão33828; fonte de jogo congelada até Windows/Linux completos. Enquanto compila, revisar positivosB18 restantes (bossitems/cart/rooms32/geyser/waterbomb/tornado); não repetir runtime pesado junto do Editor. NG38 script de eventos distintos JÁ aplicado; não reaplicar.

Usuário confirmou fechar avisoWindows, podendo retomar teste de minimização por Sky com observação nova. Sobre primeiro slot original respondeu não lembrar. Preservar estado atual e recuperação pendente; nenhuma restauração de outubro autorizada ou realizada. Hash real conferido idêntico nesta retomada.

## Itens de boss e bomba na água revisados — 25/39

NG01 fechadoB18: Mecha fábrica aleatória e recompensa finalBombKick; Magnet fuga por dano real eMagnetBomb; Pretty derrota eSearchBomb. Os cinco papéis renderizados mostram os itens nos mesmos pontos aprovados, com sprites válidos. Montagem bossitems-review.png15 vistas; itens adicionais aleatórios documentados nos logs de cada rodada. FactoryMecha regular é invocada no owner existente pelo fixture, derrotas passam por CharacterHealth nativa; nenhum spawn substituto só para desenhar o resultado. Runs completos/teardown e identidadesB18 preservados.

NG32 fechadoB18: duas bombas nativas entram na água emStage2-3, uma de cada jogador. Offline, ambos clientes, host e remoto mostram sink com escala reduzida e tint azul; probes do renderer real registram escala0.88–0.91 eRGBA variável (ex.remoto0.931/0.969/1/0.987). Montagem waterbomb-review.png compara capturas dos papéis; logs vinculam native water entry aos netIds3/4 e aos renderers observados. Não existe nova simulação de explosão/água nos clientes; propriedades são aprovadas e apresentadas pelo stream existente. Pausa/reload dedicado eStopHost concluídos.

Contagem25 validados/12 implementados pendentes/2 pendentesNG11/24. A revisão de geyser precisa ainda conferir chamas reais nos clientes: o probe FindObjectsByType<BombExplosion> não encontra proxies co-op, portanto ausência de erro nesse probe não é evidência visual de bloqueio. Não fecharNG27 somente com marker do servidor.

## Passageiro do carrinho segue para próxima sala — 26/39

NG23 fechadoB18: passageiro realmente embarca no shuttle da sala1 de3-2, depois transições nativas até salas2/3 cancelam a viagem, restauram collider/locks e movem ambos jogadores. Offline, dono/observador dedicados, host e remoto chegam desenhados sem carrinho nas salas de destino. Montagem rooms32-review.png10 vistas e cart-room-reviewed.json: após minecart_end, cliente1/2 possuem74/73 amostras posteriores do passageiro, posições15,4→37,4 e zero locked=True; remotohost47amostras27,2→43,4, zero locks. Servidor verifica RideActive=false e InputLocked/ExternalMovementOverride=false na chegada. Pausa/reload/dedicatedteardown eStopHost completos.

NG24 NÃO fechado ainda: bounces nativos realmente ocorrem nas duas saídas, com endpoints5,-6 e8,-2; ambos clientes recebem3/4 revisões. Entretanto player_visual_sample roda antes de NetworkBombFx.LateUpdate e amostra do observadorP1 mostra só root final. Necessário observar após presenter (EOF/screenshot/diagnóstico final), não usar esse log intermediário para alegar animação final correta. Não inferir regressão visual nem sucesso só a partir dessa ordem de diagnóstico. Carrinho autorado existe em3-2, não em3-1; não inserir conteúdo em3-1 para satisfazer o número aparentemente trocado no relato.

## Tornado3-4 confirmado — 27/39

NG30 fechado: B15offline/dedicado registram tração real no FixedUpdate nativo paraP1/P2 (0.02–0.04 tile por observação curta); cada dono dedicado recebe Hurricane autorizado. B18host complementa caminho local/remoto: tração0.07998657/0.04000092 e remotoowner2 com hurricane approval. Captura force-p2-approved-pull.png revisada mostra apresentação do furacão e jogador no corredor autorado. O fix usa posição física imediata antes da movimentação posterior do player; mantém bloqueio de parede e resposta contra-input existentes, sem RNG/dano cliente. Pausa/reload dedicado eStopHost concluídos. EvidênciaB15 precede a correção do sandbox, portanto não a usar para alegar isolamento de save; B18 manteve hash real.

Contagem27 validados/10 implementados pendentes/2 pendentesNG11/24. B19 em compilação; queue-b19.py sessão41834 aguarda as duas DLLs e roda quatro repros corrigidos offline→dedicado→host, depois owner-flow real. Build sessão33828. Não iniciar uma segunda fila.

## B19 imutável e repetição iniciada

Baseline1164/1164 Passed, Windows e5dc894bb0d08b00bb3d4e94ce5b94e6bf7bfbe80150766aff297dee5160e118, Linux a9b537f70e4bf06596867543b45c5bdda2c678b6125669fb91540d89789df63a. Todos70 C# alterados/novos passaram UTF8 estrito antes do freeze. Arquivo untracked-assets.zip mantém todos assets novos de runtime com hashes do identity.json; notas de progresso avançam durante build e ficam em live-notes com manifesto de hashes separados (não fingir que são o snapshot antigo do build).

FilaB19: water offline falhou no segundo cohort porque tentou plantar a bomba de preparação dentro da própria grama; setup PlacePlayers(Room3) sozinho também não restringia seleção, que ainda escolhia Room1 pelo clear score. Submerge offline passou duas fases com desmontagem verdadeira. Demais casos ainda em execução. FonteB20 ajustou seleção explícita dentro do collider Room3 e remove cover pelo owner nativo ClearDestructibleForEffect, antes da entrada natural caminhando; não afirmar que essa preparação usa bomba nativa. Continua exigindo chão/sem parede, walk→push, ambos sentidos/owners, pausa e liberação real. Adicionado corte aprovado no setup da posição. Não alterar WaterCycleCurrent para resolver setup inválido.

Depois dos buildsB19 imutáveis, aplicado UMA vez apply-b20-final-frame-probes.py mais input pós-quique e preparaçãowater. Métodos EOF já estão na fonte; não reaplicar. Geyser usa sprites do prefab Resources/Explosions/BombExplosion para reconhecer os renderers reais/proxies e exige ausência no barrier23,-1 e além; nativefixture exige selecionar essa coordenada autorada. Quique observa transform após todos presenters e captura primeiros20 frames por jogador, além de solicitar input esquerda após nativebounce para provar recuperação. Ainda não compilaram no Unity; dotnet sessão25937 em andamento. Nenhum EditorB20 iniciado.

Custo queue-cost-b19.py sessão1949 aguarda fim de queueB19, faz inventário para recusar Editor/jogo ativo, cria runners sem capturas/motion logs e roda offline1-1/2-4 uma vez porresolução mais dedicado2clientes duas repetições porresolução512x448/1920x1080. Usa os bináriosB19 imutáveis, não fonteB20. Verificar dimensões reais, bombas>0 e janela estável do combate, não médias de warmup/idle. O waiter antigo54384/Process6856 foi encerrado por ID+cmdline próprios e substituído depois de acrescentar offline; não retomar esse antigo.

Próximos: resultadosB19 e ownerflow; custo exclusivo; música realmente minimizada com Sky (skill/API lidas novamente, ainda nenhum input Windows nesta retomada); só então fullUnity/Python/buildB20 e cart/geyser/water corrigidos, mais qualquer falha B19 restante. Não compilarB20 enquanto custo está em execução. Sem commit/push/deploy.


B19offline items25/blink36 também passaram: ovoRed só escolhido na coleta guest2 e célula regenerada sem novo reward; carrier coreMinimumAlpha1; Gurun sourceAlpha0.2/shotAlpha1. Ainda sem aceite online. DedicadoB19submerge falha no guard headless do player root: o efeito de player é criado em NetworkPlayerSetup.OnStartClient, que servidor dedicado não executa. Isso é cobertura gráfica inapplicável nesse root, não defeito demonstrado no cliente. B20 agora verifica componente quando há mount nativo, documenta rootGraphicsInapplicable=True e mantém exigência dos quatro player/mount views nos clientes reais. Ainda repetir dedicado para chegar à fasePink (B19 para antes). Não adicionar componente gráfico ao servidor apenas para satisfazer o fixture.

C# terminalB20 recompilação incremental passou0warnings/0errors, sessão82473 concluída. FullUnity/B20 runtime continuam pendentes; fonteB20 não foi congelada nem buildada. Runners/cost usam somente DLLB19. Review-cost-b19.py preparado para janela estável: primeiro R01 pós-combat+5s até último R01 pré-fim-2s, exclui setup/idle; amostragem1s limita precisão dos limites, sem percentil global inventado a partir de percentis porsegundo. Exige bombas e ausência de motion capture.


AtualizaçãoB20 após quickcompile: fixture music agora também verifica pausa nativa por0.75s e retomada1s após duas voltas; o clock do AudioSource deve congelar nos papéis renderizados. Preferir testar minimização comB20 (ainda não buildado) para incluir esta cobertura offline também. Não afirmar minimização doB19: nenhuma janela foi minimizada nesta retomada. B20 precisa nova compilação incremental após essa última edição e todos gates completos depois que custoB19 terminar.

Plano de fechamento remanescente: B19items14/15, blink33/35 e ownerflow38; B20submerge18 ewater26 preparados; custoB19 raio21/FPS11; B20music19 com minimização real; B20cart24/geyser27 com EOF. Isso preserva os12 IDs abertos atuais. Não fechar a partir de markers preliminares sem completar papéis/capturas/teardown.

## Ovo genérico e prêmio sem retorno — 29/39

NG14 fechadoB19 conforme resposta explícita do usuário: ovo genérico no mundo; sorteio somente na coleta autoritativa. Offline Red, dedicado Pink e host Purple nesta rodada, cada vez mesmo resultado para dono/observador. Antes da coleta todos mostram EggClassic genérico, sem antecipar uma cor. Montagem items-review.png10 vistas compara antes/depois. Escuridão é a iluminação nativa da fase2-5 e ocorre offline também. Script de preparação usa corte aprovado da posição; não houve correção adicional de gameplay para fazer a coleta funcionar.

NG15 fechadoB19: após coleta nativa do guest2, assignment da célula foi consumida; regenerar e destruir a mesma célula não recria reward. Células offline(-8,-1), dedicado(-4,0) ehost(-6,1), logs hidden reward one-shot verified. Clientes confirmam ovo→Louie e remoção nativa do item; nenhum snapshot antigo o recria. Dedicado completou pausa/reload/teardown, host completou StopHost com zero runtime errors; save real SHA inalterado nos três runs. Baseline1164/Python16+218/buildsB19 identificados acima.

Contagem29 validados/8 implementados pendentes/2 pendentesNG11/24. FilaB19 agora roda blink36 dedicado→host, depois ownerflow real. Water/submerge dedicados aguardamB20 de preparação; não fechar por offline/host apenas.


B19blink36-01 dedicado revelou perda de eventos (HitchProbe captureDropped307), causada pelo orçamento padrão180 eventos/s, menor que R05 com vários sprites porframe. NÃO fecharNG33/35 a partir de ausência de alpha incorreto nessa captura. Preparado retry com os MESMOS bináriosB19, budget5000eventos/s e cap256MiB existentes e bounded Development; nenhuma regra de gameplay alterada. run-host.py agora acrescenta esses flags só para caseblink. Host-blink36-01 já tinha sido iniciado antes da alteração; também repetir em02.

Queue-blink-review-b19.py sessão17807 espera queueB19 completo (inclui ownerflow), então roda dedicado blink36-02 duração120s +host-blink36-02, arquiva, confere dropped/limit e grava blink-retry-completed.json. Não iniciar outro runner nas mesmas portas. Review-blink-b19.py agora lê02. O script de retry no disco foi melhorado após o waiter já estar carregado: o processo17807 usa a versão inicial da checagem; o primário deve confirmar manualmente os CINCO captures (server/c1/c2/host/client) com headers Budget5000, linhas positivas, zero dropped/limit e os alphas/PNGs. Não chamar um script não executado de gate aprovado.

Cost waiter antigo1949/Process18676 foi encerrado por ID/cmdline próprios e substituído por sessão82746, agora aguardando TAMBÉM Batch19/runtime-review-completed.json. Esse marker ainda NÃO existe; escrever somente depois de revisar B19 ownerflow e retriesblink, sem outra janela/Editor. Isso impede medir FPS enquanto o retry ainda roda. CostusaB19 imutável; não iniciar EditorB20 enquanto cost ativo. Não retomar waiters antigos54384/1949.

## Blink corrigido, capturas completas — 31/39

NG33/35 fechadosB19R2: offlineB19 +dedicado blink36-02 ehost-blink36-02. Conferidos cinco headers Budget5000, linhas positivas6045/6088/13741/1953/3575, zero dropped/limit; arquivo blink-reviewed-quality.json. Carrier oscila alpha0.2↔1, core permanece1 com zero hiddenSamples dentro da janela contínua visível. Dedicado clientes169/173 amostrascarrier,171/175core; host/remoto181/185carrier e182/186core. Projétil nativoGurun disparado com templatealpha0.2 apresentaalpha1 em todas17amostras nativas e de cada cliente dedicado; host/remoto16amostras cada. Não usar01 incompleto para essa ausência de defeito.

Montagem blink-review.png dez vistas revisadas. Core offline correto é offline-016.png; host é host-002.png (as escolhas iniciais018/008 já mostravamRoom3 e foram substituídas na montagem). Fotos r05-03 eblink-rocket-3 mostram tiro existente. Nativecore é tratado no owner de blink, não escondido junto ao inimigo; novo disparo restaura sua opacidade, não herda invulnerabilidade da origem. Dedicado pausa/reload/teardown ehostStopHost completos; save real igual.

B19owner-flow01 mostrou os DOIS toasts11/13 para ambos jogadores e persistência sandbox correta: owner activeSlot1, flags11/13true; guest activeSlot-1, flags11..15false. Porém helper falhou no fechamento porque esperavaTitle para todos e encerrava ao ver server/relay sair, antes dos2s de quit. Fonte produção OnlineRoomSession retorna iniciadorLeaveRoom aoTitle e receptorOnClientDisconnect aoOnlineLobby. Não alterar essa rota para fazer teste passar. B20 LocalOnlineFlowSmoke agora exige as duas rotas corretas, ambas desconectadas, e quit nativo2s depois, métodoQuitAfterNativeReturn. Helper run-real-owner-unlock.py recusa binário antigo sem método e dá35s bounded para clientes terminarem após servidor sair; ainda exige menus, eventos distintos/toasts, saves e exit0. NG38 segue pendente de repetir comB20.

Relay privado foi corrigido também no ladoLinux para ignorar ICMPConnectionRefusedError normal quando servidor termina, mantendo2peers/timeout. Arquivo local Build/ConnectionIdle/ssh_udp_bridge.py enviado ao mesmo helperprivado, sem serviço publicado. Processos de retry já carregados não mudam com essa cópia. Não tratar esse fechamento de diagnóstico como bug de autenticação/protocolo.

Contagem31 validados/6 implementados pendentes/2 pendentesNG11/24. B19fila e retry completos; depois do quickcompileB20, liberar runtime-review-completed.json para custo exclusivoB19. B20fullUnity/builds continuam aguardando fim do custo, não disputarEditor/RAM com benchmark. Restantes8: NG11/18/19/21/24/26/27/38.

## Custo B19 em andamento — não abrir Editor

Runtime-review-completed.json foi escrito depois da revisão B19/retry e quick C#3965 (0warnings/0errors). Primeiro custo: offline1-1/2-4 em512 passaram, ambos com20 contagens de bomba (10 amostras de2 bombas, não20 entidades distintas). Raio inicial offline1-1=1,2-4=2. Dedicado1-1 com120s terminou combate sem erro e saiu durante reload antes do marker final genérico: helper reprovou corretamente. Preservado cost-1-1-512-1, não chamado ciclo aprovado.

Queue-cost-b19.py foi retomado como sessão47522 com180s por dedicado, reuso dos offline completos e nomes-retry para não sobrescrever tentativas. Sessão82746 terminou; não retomar. cost-progress.json contém tentativa falha mais sucessos; contar exit0/targets únicos, não tamanho cru. Agora4/12 targets válidos: doisoffline +dedicado1-1-retry2 e2-4 primeira repetição512. Próximo nativo1-1 repeat2 ativo. Ainda NÃO existecost-completed.json.

Dados preliminares (não fecharNG11 antes de todas repetições/resoluções): dedicado512 1-1 clientes59.99/60.00FPS, trabalho1.897/1.994ms, max7.310/6.331ms;2-4 ambos60FPS, trabalho2.077/2.017ms, max4.518/4.585ms. Offline1-1 frio possui pico261.399ms, incluído explicitamente na análise (média59.08FPS); offline2-4 foi60FPS/max3.315ms. Não esconder o pico nem atribuir causa sem profiler. Falha120s possui janela de combate íntegra, mas é sinalizada como ciclo incompleto e separada da repetição aceita.

Review-cost-b19.py usa u doR01, que é UTC em ms com offset próprio63900000000000, NÃO Unix epoch. Relatório agora guarda windowR01UtcOffsetMs e conversão UnixUTC por+1764403200000, com cadência dos limites1s. Janela do primeiroR01 após combate+5s até últimoR01 antes do fim-2s. Manter máximos, allocations/GC, dimensões reais e captura desligada. Até agora512x448 confirmado. 1080p ainda pendente.

FonteB20 recebeu depois do últimoquickcompile: EOFwater20Hz após todos presenters (x/y,pt,nt,rt,owner), eprepare-remote/run-host usam budget5000/cap256 em water/cart/geyser/submerge/blink para não repetir overflow. BootEOF já inclui water. Ainda compilar novamente após custo; fonte de jogo NÃO editada durante buildB19 e todos novos códigos são fixtures opt-in Development. Não reaplicar scripts de inserção já aplicados.

## Custo encerrado — 32/39, buildB20 congelado

NG11 validado como comportamento sem queda reproduzida NO cenário controlado atual; não há causa individual isolada nem nova correção específica de FPS atribuída. Doze targets completos,20 janelas de papéis renderizados: offline1-1/2-4 em512x448 e1920x1080, dedicado2clientes com duas repetições porfase/resolução. Todos com bombas reais via inputdos donos; tamanhos reais, recorderGC válido e motion/frame captures desligados confirmados em combat-cost-reviewed-quality.json. Fonte/buildDLLB19 imutáveis, sem Editor/outros jogos durante os runs. RelayKCP privado/SSH, hardware atual; não extrapolar para todo hardware/RTT/fase/situação.

Oito janelas de clientes2-4 (quatro runs) ficaram59.994–59.998FPS; trabalho médio1.933–2.077ms, maior trabalho6.109ms, maior dt17.070ms. Offline2-4 ficou60FPS em ambasresoluções, máximos3.315/3.417ms. Comparação1-1 e máximos completos preservados em combat-cost-review.json; offline1-1 teve picos frios261.399ms e143.725ms, explicitamente mantidos. Não chamar o jogo inteiro de isento de hitches. Tentativa inicial120s teve ciclo incompleto e permanece separada/sinalizada, sem ocultar seus dados nem reduzir critérios. Cenários completos usaram180s dedicado, pausa/reload/teardown nativos. SHA do save real inalterado em todos.

DriverB20 sessão38901 passou preflight C#/Python (ver Logs/NormalPlaytest/Batch20-preflight) e iniciou fullUnity/builds. driver-b20-status.json: full-unity-gate-and-immutable-builds-source-frozen. NÃO editar fonte enquanto roda. Custo47522 completo; não retomar ou recriar. Depois dos builds imutáveis, driver prepara arquivos novos/metas, roda queue-b20.py automaticamente e publica statusruntime, mas isso NÃO significa aceite manual. Casos offline que falharem não passam a online; investigar os resultados antes de fechar IDs.

FonteB20final inclui ainda: escolha explícita geyser23,-1/direçãoUp (não depender da enumeração), áudio com atores protegidos para impedir GameOver substituir a música no ensaio longo, pausa/retomada após duasvoltas; EOFwater20Hz e input depois do quique; headless submerge distingue root cliente de mount nativo; ownerflow retorna receptor aoBrowser e iniciador aoTitle. Todos já aplicados, não reaplicar arquivosapply-*.

Música: run-offline-music.py e run-host-music.py preparados sem frame/motion captures, para minimizar via Sky e observar áudio nativo. Ainda nenhuma minimização foi feita nesta retomada. ApósB20build+fila, criar variante run-linux-music.py sem screenshot/motion capture e executarStage1-1/music com duração480 (20s preparo+~298s música+pausa+reload; timeout650 nos helpersoffline/host). Trêscontextos offline/dedicado2janelas/host+remoto. Não tocar em avisoWindowsSegurança; se bloquearinput, handoff manual conforme skill. Não rodar junto deEditor/custo/outroscenários.

Restantes7: NG18/19/21/24/26/27/38. NG21 já tem default1 em1-1 offline/dedicado e min2 nosdemais cenários; complementarhost1-1 no casoMusic antes do fechamento. Próximo: resultados/gatesB20, revisarEOFcart/geyser/water e quatroviewsSubmerge, ownerflowpersistência/retorno, música realminimizada, evidênciafinal/manifesto de limpeza. Sem commit/push/deploy solicitado.

## Retomada após quota — B20 completo, 35/39

Sem Editor/jogo/python ativo ao retomar; driverB20 terminou14:05:31UTC, não reiniciar. B20 fullUnity1164/1164; preflight C#0warnings/0errors ePython16+218 Passed. Windows045684ae3f0d3259c1aa203d6d6ace2d831da92bb5a69f5fa538ef71f27c77e6, Linuxdc08f3ddd4a0d7d6083bef1255206380f96e38007255c380eea6f672079bc76a. Todos runs da fila mantiveram save realefc6c5adffa5c58c53d4f63b9bec89600323cc2722477149c89ef536e00f4d63.

NG18 fechadoB20: offline/dedicado2clientes/host+remoto completaram duas fases: P1bare+P2Blue, depoisP1Pink+P2bare. Quatro keys de shader visível em cada cliente, efeitos de água presentes nos mounts e nos jogadores. Headless confere componente de mount, sem exigir root gráfico que inicializa emOnStartClient. Montagem submerge-review.png10 vistas revisada, incluindo P2 após desmontar. As primeiras escolhasworld024 de fase1 estavam ANTES do eventoPink; substituídas por frames depois dePink+0.6s (dedicadoworld028, remotohostworld020), mapeados emsubmerge-review-selected.json. Não alegar queworld024 mostravafasePink. Pausa/reload/teardown/StopHost completos.

NG27 fechadoB20: chamas reais/renderers finais dos dois clientes e remoto host identificados pelos sprites ExplosionStartWeak23,-3 eExplosionMiddleWeak23,-2, nunca no barrier23,-1 ou além. ProbeEOF observa renderer real após todos presenters, não depende de componentes gameplay ausentes no client. Native explode duas bombas reais, uma porjogador, raio suficiente, com guarda de aproximação adjacente e bloqueio. Geyser-review.png cinco papéis revisados: fogo para abaixo do geyser. Fluxos completos/sem erros.

NG38 fechadoB20: dois toasts11/13 visíveis nos dois clientes, sem replay de mesmo unlock. Unlock-review.png quatro capturas legíveis/revisadas. Owner sandbox persiste slot1Hard e11/13; guest continua slotsnãoiniciados/flags11..15false. Menus nativosSkinSelect→SaveFileMenu→WorldMap→Stage1-1; guestLeaveRoom retornaTitle e ownerrecebe fechamento→Browser, ambos desconectados e quit2s nativo/exit0. Helper fez todas assertivas; arquivoresult.json tinha somente LABEL antigo bothReturnedToTitle=True: preservado original, correção de metadado emresult-reviewed.json com hash do original e rotas corretas. Helper fonte corrigido para futurosruns. Não alterar gameplay de retorno para satisfazer o nome antigo.

B20 water/cart offline falharam em preparação, portanto não rodaram online nesse lote. Water ficou39,2: só centro/entrada foram limpos e o falso scoreclear6 vinha de chamar IsBlockedAhead em ponto deslocado. Corrente pode abortar imediatamente se próximo tile está com grama. B21 calcula corredor estático em centros de tile, preserva chão/paredes e remove grama por ClearDestructibleForEffect sem prefab de quebra no caminho completo. Ainda exige entrada caminhando e impulso real, ambosowners/sentidos/pausa. Diagnóstico de falha agora mostrablocked/touched/locks/barrier/bounds. Sem mudança emWaterCycleCurrent para forçar o teste.

CartB20 provou ambos bounces e inputP1 (5,-6→4.19,-6); P2 terminou8,-2 e o teste mandou andar à esquerda para7,-2, parede que acabara de fazê-lo quicar. B21 escolhe vizinho com chão e query nativaIsBlockedAtPosition(...allowMovementAbilities:false), pede input real e exige deslocamento. EOF também captura rotina nativa de bounce offline/host via _resolveRoutine, além do fx aprovado dos clientes. Não mudar colisões ou quebrar parede para fazer teste passar.

B21 quickC# passou0warnings/0errors após última edição; PythonRoom16/16 eCi218/218 Passed (sessão44482finalizada). Build completo sessão48056 em andamento; fonte CONGELADA até ambasDLLs. Não reaplicar alterações de fonte. Restantes4: NG19/21/24/26. Próximo: terminarB21/arquivar assetsnovos, queuewater/cart offline→dedicado2clientes→host; música realminimizada comSky, cobrindohost1-1 paraNG21; evidênciafinal/cleanupmanifest. Nenhum teste de minimização realizado ainda.

## B21 executando; correção adicional NG24

B21 concluiu fullUnity1164/1164 (zero CS warnings/errors nos três logs), Windowsbc8a7b6f4bf1ddf4a33b2ae78507c276a06afd980e58baffa228a45d8711057e eLinux29948e98a672e7229fe509de7ec81f13a8c4a096b1ac24632e0b151bda9b7ea9. Assets novos arquivados; sessão48056 terminou. Queue41634 segue comwaterdedicado→host; não duplicar portas. Offlinewater passou quatroentradas naturais P1/P2 contra/a favor, pausaativa econtradireção (2.9375/3.375tiles). Ainda precisa análise onlinefinal.

Cartoffline B21 falhou com qualquerowner na viagem de volta: saída5,-2→bounce8,-2, fora do chão. A checagem de vizinhos confirmou que não era apenas direção de input do fixture: PlayerPushedOutOfInvalidTile usa bounds globais das três salas, então atravessa a paredeRoom1 e para no vão sem terreno. Fonte APÓS DLLs B21 recebeu _resolveRoomBounds capturado por NotifyExternalPushed e TryStepWithWrap usa o mesmo interior autorado de Bomb.TryWrapToPunchRoomBounds. Semroom, conserva boundsglobais. Quatroregressões adicionadas OnlineRoomRevengeCartTests (duasbordas, passointerior, semroom); compilação/fullgates/runtime aguardam B22. Não chamar a fonte corrigida de testada nos binários B21.

run-linux-music.py B21 preparado semmotion/backbuffer/mountcaptures, caminhos B21/b21. Música ainda não executada. Skyinicializado via node_repl, skill/docsguidance/api/confirmations lidos; nenhum inputWindows foi enviado. ApósqueueB21 pode rodar músicaB21 imutável antes de abrir EditorB22; depois B22fullgate+cart offline/dedicado/host. Contagem35/39 permanece. UTF8strict70Cs passou antes da nova alteração, repetir para os doisCs adicionais. Save real SHA continuaefc6c5adffa5c58c53d4f63b9bec89600323cc2722477149c89ef536e00f4d63.

## NG26 validado B21 — 36/39

Water pulse: quatro entradas caminhando por P1/P2, a favor e contra, pausa ativa, contradireção e soltura nativa. Offline e dedicado usaram corrente Up (43,-3); host usou Right (38,2). Não comparar curvas de eixos diferentes como se fossem a mesma entrada. Servidor dedicado + dois clientes e host + remoto encerraram cenário, pausa/reload/teardown ou StopHost sem erros; save real inalterado.

water-final-frame-review.json registra vinte grupos observados após todos presenters: dois impulsos por jogador em cada um dos cinco papéis renderizados. Cada grupo tem 21–42 amostras, 7–15 posições distintas, e deslocamento contínuo no eixo aprovado, sem antigo deslocamento de origem de vários tiles. Os donos receberam duas transições authority_motion_begin/end cada; input servidor é solto sem locks ao terminar. Countersteering após soltura muda o endpoint e não deve ser tratado como erro de curva. Não declarar zero latência nem igualdade por server-now: relógios/interpolação/pausa e input chegam em instantes diferentes. Comparação bruta de pt com a curva server-now foi exploratória, não é métrica de erro renderizado aceita.

water-reviewed-quality.json: cinco capturas com headers Budget5000, linhas positivas 17368/8094/8065/2510/1208, zero dropped/limit/overflow. Revisados water-contact.png e water-client-contact.png: fases nativas/pausa e ambos jogadores na corrente nas duas janelas, câmera e terreno iguais à autoridade. Capturas individuais e JSONL originais preservados. Esta correção vem do decoder B17 mantido ativo; B21 corrigiu a preparação, sem alterar a corrente para fazer o teste passar.

Restam NG19 (música minimizada), NG21 (complemento host1-1), NG24 (limite da sala no quique). B22 fullUnity/builds sessão74316 CONGELADO; queue-b22.py sessão16465 espera DLLs e roda apenas cart offline→dedicado→host. Arquivo untracked-current-before-build-end.zip preserva bytes de todos untracked iguais aos hashes de início B22. PerformanceTestRun*.json são artefatos gerados pelo test-framework; se mudarem durante gate, manifest distingue dos Assets runtime novos e dos notes vivos. Nenhum novo teste de minimização executado ainda.

B22 gate FAILED:1166/1168, apenas duas regressões novas de borda. Não gerou DLLs. Queue16465/Process19844 próprio foi encerrado após conferir cmdline; não retomar. Fórmula copiada de Bomb pressupunha margem/cantos exatos do collider, e o collider de presença realRoom1 tem16x14, centro(-1,-0.3), além da margem nativa; não é o retângulo dos tiles. Ajuste posterior captura footprint de tiles Ground+Indestructibles cujos centros pertencem à sala e converte ao grid de referência; mantém limites globais sem sala. Quatro testes agora criam centros nativos, footprint de piso+bordas e tile remoto fora da sala; verificam o wrap e a ausência dele. Ainda precisa gate novo B23. Nenhuma DLL B21 contém essa correção adicional.

B23 fullgate/builds sessão86046 iniciados, fonte CONGELADA. Último C# incremental0warnings/0errors após mover captura de footprint para depois do teste de bloqueio (não comprimir/varrer mapas em saídas livres). PythonRoom16Passed, Ci sessão62925 em finalização. MúsicaofflineB21 sessão21404 foi abortada só no processopróprio26544, cmdline conferida; manual-review.json registra bloqueio por Firewall e zero ações de minimizar. Não aceitar por log focus=True/False. Pergunta async ao usuário permanece pendente (aviso reapareceu para caminhoB21; usuário fechou o antigo, não presumir que fechou este). Mais nenhum inputWindows enviado após handoff. Próximo B23gate/cart; música depois do aviso manualmente fechado.

B23 fullUnity PASS1168/1168, inclusive quatro testes novos RecoveryBounceWrapsInsideItsAuthoredRoomInsteadOfTheMultiroomMap. Compileincremental final0warnings/0errors, Python16+218 Passed. Nenhuma alteração C#/Assets após início do build. Windows+Linux em andamento; queue38771 aguardando. Helpers record-music-ui.py e review-music-minimized.py preparados: após ação realSky, registrar UTC/idWindow/byteOffset+últimoDSP; exigir intro+loop ewrap1/2 entreminimize/restore, comsaída nãozero eplayingTrue. Nenhuma ação de minimizar ocorreu na tentativa bloqueada.

Usuário respondeu à pergunta async atual: "fechei" (aviso Firewall). Handoff manual encerrado; não perguntar de novo pelo mesmo aviso. Aguardar fim B23build/cart antes de música B21 imutável, para reutilizar o caminho cujo aviso foi fechado. Se novo aviso realmente reaparecer, observar/recovery uma vez antes de solicitar nova ação. Nenhuma ação Windows sobre segurança foi enviada.

Usuário adicionou NG40: stage label na intro/pausa e menu de pausa parecem tamanho fixo em vez de acompanhar safe frame 4:3; pequeno fica grande, alta resolução ainda não testada pelo usuário. Total40; contagem36/40 antes da revisão finalNG24. Skill unity-pixel-art-ui-ux lida/anunciada. Investigação: StageLabel usa Camera.pixelRect (inválido como fonte única sob URP) e aplica clamp minScale serializado1 DEPOIS de dividir designUpscale4 e Canvas.factor; prende fonte no tamanho de design nas resoluções baixas. Update só atualiza retângulo, sem reconstruir tags de tamanho ao redimensionar; GamePauseController.Update retorna cedo no pureclient, portanto resize de pausa precisa owner de apresentação local. CoopStageStatus transmite world/stage, NÃO texto escalado: hipótese de richtext vindo do servidor foi descartada pela leitura da fonte. Não alterar wire/autoridade para corrigir escala local.

## NG24 fechado B23 — 37/40

B23 fullUnity1168/1168, Python16+218 e C# incremental0/0. DLL Windowsd5f31c3baf7c64b016fc45aabb53407985da6e345ee0bcfb7f3ce9ce29d552cf, Linux5d96ae215843b49be61ff6bfff911abbc840873d3f8dae547ab6944050117d0f; frozen-csharp-review.json confirma source C# exato de início. Correção de footprint nativo só roda quando existe bloqueio real; sem sala mantém regra global.

Offlinecart32-offline-02, dedicadocart32-02 ehost-cart32-02 completos. Primeira tentativa offline01 falhou ANTES de embarcar: bomb.IsSolidFalse apesar de fusePausedTrue. Não aceitar como bounce nem mudar collider para forçar; segundo run dos mesmos binários teve bomba sólida e ambos trajetos nativos. Ida3,-6→5,-6 e volta5,-2→wrap da borda→-7,-2 (em vez de8,-2 sem piso). Ambos controles reais recuperados, inclusive subir desde-7,-2. Host inverteu a ordem dos owners: P2Pink na ida, P1a pé na volta; dedicado P1a pé ida/P2Pink volta. Sem exceções, pausa/reload/teardown dedicado e StopHost completos; save real SHA igual.

cart-final-frame-review.json e cart-reviewed-quality.json: cinco papéis renderizados, ambos jogadores, muitas posições finais distintas; cinco capturasBudget5000 semdrop/limit. cart-review.png dez vistas reais revisadas: salto a pé e Pink sobre bomba/tiles, mesmo sprite/apresentação nos dois clientes e remoto host. EOF completo inclui corte de wrap; PNGs de bounce são primeiros20frames, não alegar que todos os instantes do wrap estão fotografados. Endpoints e input depois da soltura registrados nativamente. Relato dizia3-1; carrinhos autorados estão3-2, não inserir carrinho em3-1.

NG40 fonte StageLabel agora usa PixelPerfectViewport.ResolveFinalPixelRect, aplica min/max no multiplicador físico ANTES da compensação design4/Canvas, elimina mínimo local10 que distorce fontes pequenas, conserva tamanhos fracionários locais com cultura invariável. Retângulo/moldura usam mesmaescala; LateUpdate recompõe view local quando escala muda, preserva seleção/confirm/Battle. Onze regressões em novo OnlineRoomStageLabelTests.cs+meta (7escalas,4views). Ainda não compilado/validado; precisaB24 e runtime1x/2x/4x offline/dedicado/host, quatro idiomas e resize pausado. Não alterar wire/autoridade.

MúsicaB21 offline02 foi realmente minimizada viaSky (7670304; get_window_state retornou explicitamente window is minimized), intro→loop+2wraps e picos nãozero durante314s. Restauração falhou (foreground window did not report a process id), pois fixture abortou em premissa de pausa errada: NativePause mantém soundtracktocando. NÃO fechar NG19 nem chamar restauração aprovada. Journalrestore havia sido escrito apesar doerro; corrigido imediatamente pararestore_failed, originais preservados before-review-correction/unaccepted e review atualacceptedFalse. Fontefixtureagora exige músicaavançando durantepausa e margem30s apóswrap2 (era8s). Repetir offline/host B24; dedicado B21headless não tem assert deAudioSource.time e permanece480s, pode validar minimização/restauração completa enquanto preparaNG40. Sem alterações no GameMusicController apósB21.

MusicdedicadoB21 music-01: duasjanelas minimizadas confirmadas Sky15142834/7735840 e journalgrupo (não atribuir umID a umPID específico). Intro→loop+1wrap comsaída nãozero, mas sala encerrou no idle nativo300s antes dewrap2; NÃO aceite. Causa verificada OnlineRoomSession.TickRoomTimeout/RegisterRoomActivity: nenhum inputpor300s. ReadDedicatedNumber permite1800s, portanto hipótese de cap de load-test-duration300 foi descartada; não alterar esses limites nemtimeoutprodução. Fontefixturemusic agora inclui dois inputs reais curtíssimos viaowner a cada90s e TickWaterInput aceita caseMusic, mantendo regraIdle normal; precisaB24. Não registrou restauração deste dedicado, pois asjanelas já haviam fechado quando conferidas. Nenhum dado deste run permite alegar duasvoltas/restauração.

B24 fullUnity PASS1180/1180 (12novosUI:7escalas+5views incluindoBossRush). C#incremental final0warnings/0errors, Python16+218Passed. Windows61a9b1cdfc7b18d3b05be47514bf57b9cb14025a3ac995df2990ab3c82b6f5c7, Linuxbuildem andamento sessão10986. Fonte CONGELADA desdeinício B24. RuntimeUIScale3res384x336(safe1x)/512x448(2x)/1920x1080(4x), quatroidiomas menu/confirm, inputs reais doadmin viaTickUiScaleInput earlyUpdate. NativeBattle15 eBossRush1-6 suportados nafixtureoptin, nenhumRPCnovo. Helpersrun-offline-ui/run-host-ui/Batch24run-linux-ui preparados; queuenativa só avança casosoffline completos e exigeclient12+12samples comcompletion. UIheadless confere fluxo nativo, gráficosinaplicáveis. NativeCoop client mantém StageLabelenabled porqueéfilhodeCanvas, confirmadoKeepCoopPresentation.

B24 Music temfixturepausacorrigida(continuatocando), margem30sapóswrap2 eatividade real doowner a cada90s preservandoidle300 deprodução. Ainda precisa repetirtrêscontextos minimizados/restaurados. Helpersrecord-music-ui e review-music-minimized existentes; registrarrestore SOMENTE seSky ação+refreshconfirmarem; seerro, registrar restore_failed e não chamar success. CandidatosSky B21 antigos estãofechados; nunca reusar IDs7670304/15142834/7735840. Selecionar novasjanelas doB24porlist_windows, ativar oapppróprio antesdeobservar para evitaroclusão pelo navegador. Getstateapósmininclude_screenshotFalse pode confirmarwindow isminimized; primeiraresposta pode ser assíncrona, relersemnovo inputantesdeassumirfalha. UsuáriofechouFirewall atual, pedirnovamente sóseumavisonovo realmente bloquear.

B24 NÃO fecha NG40: trêsoffline deramexit0/flagsamostras, mas revisãoPNG revelou doisproblemasnão cobertos pela checagem de pointSize. FontebaseTMP36 permaneceu fixa para espaços entre spans e linhas vazias; rótulo intro era na verdadepausa, poisprobesperavaTime.timeSinceLevelLoad>1 duranteintro comTimeScale0. LogsconfirmamNativePauseStartedANTESdasamostrasintro. ui-normal-offline-scale-review.json marcadoacceptedFalse, originalpreservado; não usarprimeiralinhaPNG comoIntro. Fonteposterior(B25) captura authoredBaseFontSize porTMP eescala36 juntoaostags; testes existentes agora verificamfontbase/preferredWidth/Height ao mudarCanvas. Probeintro exigeestadoIntroRunning(autoridade) oulabelaprovado no cliente, nuncaPaused, semtempoescalado; runtimecompara preferredboundsnormalizados entrezoomstolerância1pixel. Ainda precisa B25fullgate/runtime.

FilaB24 sessão43169 terminou comprepare-remoteexit2: VPSroot100% (4.1MiBlivres), extraçãoparcialb24. NãoexecutouonlineUI. Cleanupguarded doPRIVATE testserver: onze linux.tar.gz redundantes b11/12/13/15/16/17/18/19/20/21/23, cadaum comSHAigual aoarquivo localpreservado. Manifesto foiarquivadoANTES deunlink, em Docs/multiplayer/evidencias/limpeza-arquivos-testes-2026-11-14.json;2,008,548,961bytes liberados; players/logs/saves/caches/produção mantidos. B24reextraçãodoMESMOtar verificado emandamentosessão19242, exigetabelacompletafiles/hashigualàcópialocal antesdeusar. NãoretomarUIB24 mesmoapósreparo, pois há gapsdefont/fixture conhecidos.

MúsicaofflineB24 sessão10323 ativa, única janela3411578 minimizadaconfirmadaSky, journalgravado. NãoabrirEditor/B25enquantoáudioativo. EsperarduasvoltasdeLoop e restaurarcom30sdemargem; sógravarrestoredepoissucessoUIcomfreshstate. B24Musicfixturejátemidle/pausacorrigidos, produçãoGameMusicControllerigualB25. FonteUIScaleB25podeserpreparadaduranteáudio, mas nãohotreload. Contagem37/40restamNG19/21/40.


B24 reparado: 423 arquivos do player remoto conferidos byte a byte por SHA contra Linux imutável; remote-repair-result.json completo. Música offline01 terminou exit0, duas voltas e pausa/retomada nativas, mas janela fechou antes da restauração: não aceitar NG19. Journal tem apenas minimizar; nenhum restore inventado. Próxima tentativa terá watcher bounded do log na segunda volta para restaurar dentro da margem de30s.

B25 fullgate/builds iniciados sessão37369; fonte C#/Assets congelada. Queue UI sessão99428 espera builds, repete offline/dedicado/host nos três modos; não usar resultadosB24 para aceitar escala/intro. Correção adicional escala fontSize baseTMP36 (espaços/linhas vazias) e probeintro rejeita Paused/usa IntroRunning sem relógio escalado; runtime compara bounds normalizados por safe-frame e fontebase. Testes5views com assertfontbase +preferredbounds preservam tolerância1.5localpixels pelo arredondamento inteiro de indent. Contagem37/40.

B25 fullgate PASS1180/1180; Python16+218Passed, strictUTF8/BOM73CsPassed. Restore dotnet teve6avisos vendorMirror existentes; incremental final0warnings/0errors. Windows5079068584d2a5f4c37cc9e6ab05f5310f25fc45a81cbcea4a7f22cb39f96c1e Linuxb97a304c496460bcadfcdaab6a9389097a44596700c8dc11a06f4976acebce67; sourcePatch68f6d4f9c390e1ef54f9e5af95821d674d0f843edbfd46092c631f371d1230f2; frozen-csharp-review confirma fontes. TrêsofflineUI completos(27/24/27amostras), introrealPausedFalse emNormal/BR, Battle semcampaignintro. Fontbase9/18/36, glifos11/22/44(Normal/BR) ou9.5/19/38(Battle), boundsnormalizadosestáveis. Sheetsnormal4idiomas3res eBattle/BossRush revisadas,semcorteescalafixa. DedicadoNormal completo180s,2clientes27amostrascada, ownerInputconfirma/cancela/retoma, worldreload/teardown, zeroerros; pausalayoutrevistobothclients. Queue99428 emhostNormal, restamBattle/BR online; nãofecharNG40ainda. SaveSHAigualemcadaetapa.


## NG21 fechado B25 — 38/40

Host1-1 confirmou autoridadeP1/P2radius1, cliente remoto presente, pausa/confirm/retoma+StopHostcompletos,zeroerros,saveigual. campaign-radius-host-review.json arquiva hashes. CompletaB19offline/dedicado1-1raio1 e2-4mínimo2, regressõesmaioresraiospreservados eBattle/BRexcluídos. ProbeBombControllerpureclientmostra9defaultprefab: componente nãosimularesultado; NÃOémediçãoderaioautoritativo. NetworkPlayerStateSyncVar escreveRuntime.ExplosionRadiususadopeloHUD; bomba/explosãosaemdaautoridade. Nãoalterarconfigclientparafazermarkerpassar. RestamNG19/40.


## NG40 fechado B25 — 39/40

Trêsmodos Normal/Battle/BossRush completosoffline,dedicadoLinux2clientes ehost+remoto:15papéisrenderizados,390amostras (Normal135/Battle120/BR135). Quatroidiomas,1x/2x/4x de safe-frame,redimensionamentopausado,confirmação/cancelar/retomar porinputadmin,teardownnativo. IntroNormal/BR realPausedFalse; Battle semcampaignintrolabel,aplicapausa/confirm. Headlessverificafluxo,tipografiaGPUinaplicável; COMnãotemcaminhodetipografiadistinto ebaselineCOMpassou. ui-scale-metrics-review.json15rows+shaLogs; sheetslanguage/intro/confirmoffline epausaPortuguêsnos5papéisdecadamodorevisadas,semcorte/escala fixa. Fontbase ewhitespaceacompanham,preferredboundsnormalizadosvariamaomenos1pixel; nãoaceitarpointSizeisolado. Zerofalhas/erros/updatesdesave real,queue99428exit0. RestasomenteNG19; músicaB24imutáveltemmesmocódigoGameMusicController,fixturecorrigida; nãoabrirEditornemoutrosjogosduranteáudio.

## NG19 fechado B24 — 40/40

Offline music-offline-02, dedicadoLinux480s music-02 ehost+remoto host-music-01 completos exit0. Cincojanelas renderizadas minimizadas/retornadas de verdade viaSky, refreshconfirmou minimizaçãoe restaurosmostraramjogo. Duasvoltasinteiras deStage1-1Loop duranteintervalominimizado emtodos, samplespositivosdeintro+loop,playingTrue epeaknãozero; áudioapósrestaurocontinua,pausa/retomadanativas+teardown/StopHostcompletos,zeroerros. Journals delimitam byteOffsetconservador ANTESdoprimeirorestauro; mapasgrupo nãoatribuemPIDaIDsemprova. music-minimized-review.jsonregeneradosapósquitcomSHAdefinitivo emanual-review.jsonacceptedTrue. TentativasB21/B24offline01inconclusivaspreservadassemaceite. B24/B25productionaudio/onlinepausepatchesidênticosverificadosmusic-build-compatibility-review.json.

Usuárioautorizoucommitna0.5.0parapublicar: branchrealv0.5.0confirmada,originigualHEADantesdocommit, workflowonline-release.ymlpushpublicabothclientes+servidor. Preflightprotocolo74passou. Antesdopushliberarespacodeprópriastestescópiasredundantescomhash/manifesto,poisactivate_releaseexige2GiB. Nãodesativargate,nãopublicarserver-onlycomclientesantigos.


## Encerramento e publicação autorizada

Todos os40itens estão tratados/validados dentro dos limites desta evidência. NG11: nenhum declínio deFPS foi reproduzido nos cenárioscontroladosB19; não atribuircorreçãodecausadesconhecida nemgarantirFPSemqualquerhardware/rede. Incidentesantigosdesavepreservados: slot2com22fasescontinua,situaçãoanteriordoslot1desconhecida,nãorecuperadaapartirdebackupprovadamenteatual; testesB17+isoladosmantêmSHAreal.

Evidênciacompactaversionada: evidencias/fechamento-playtest-normal-game-2026-11-14.json. Revisõesoriginais/identidades/PNGs/logsficamBuild/NormalPlaytest/Batch09–Batch25 eZIPfinal-validation-reviews-20261114.zip. Antesdelimpar,manifesto ebytesgeradosde3assetsforamarquivadosBatch25/generated-before-cleanup.zip: sónullsdefallbackTMPemduasfonteseequivalentemigraçãoserialPhysicsforamrestauradosaHEAD. UmúnicoespaçoASCIIdefimdelinhaemLocalPredictionSmokefoiremovidodepoisdoB25,semalteraçãodetokens; C#finalpassou0/0eUTF8strict73.

Limpezaprivataconcluída:6playersantigosb11/12/13/15/16/17,423arquivoscada,2,079,179,915bytesredundantes. TabelasSHAremoto/localiguaiserechecadasantesdermtree; cópiaslocaisimutáveispreservadas. ManifestocompletoBuild/NormalPlaytest/private-player-cleanup-20261114.json eSHA/resultadoem evidencias/limpeza-players-testes-2026-11-14.json. Logs/saves/capturas/produção/rollback/cachesmantidos. Espaçolivrefinal3,360,788,480bytes,gate2GiBpreservado. Cleanupanteriorde11tarsemmanifestoseparado.

Usuárioautorizoucommitar/pushnav0.5.0parapublicar. Ocommitdecódigoacompanhadodestaevidênciaaciona.github/workflows/online-release.yml,targetboth(Windows/Linux/Android+servidor), protocolo74alinhado epreflightpassado. EstadofinaldapublicaçãodeveserconferidonoActionsdestecommit eBuild/NormalPlaytest/publication-result.json; nãoinferirsucessodopushnemrepublicarsemconferiroestado. Estaorientaçãoevitacriaroutrocommitdesóevidênciadepoisdobuild ealteraraidentidadedosclientes/servidor.
