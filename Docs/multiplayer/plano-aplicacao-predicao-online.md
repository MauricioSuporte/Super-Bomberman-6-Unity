# Plano de aplicação de predição e apresentação online

Data: 25/10/2026. Baseline de código: `2e41c0c47`, protocolo 24, Unity 6000.6.0f1.

**Acompanhamento atualizado:** consulte [Estado atual e checklist de execução](progresso-predicao-online.md).
Este documento preserva o levantamento e os critérios iniciais; o checkpoint
registra o que já foi implementado, validado, publicado e o que ainda falta.

Objetivo: aproximar a resposta e a continuidade visual do online à experiência
offline, usando a skill `super-bomberman-6-online-prediction` onde houver ganho
real. Este documento é um plano de investigação e implementação futura; não
declara que os candidatos abaixo já receberam predição ou apresentam um defeito
reproduzido. Nenhum comportamento de gameplay foi alterado nesta análise.

## 1. Alcance e confiança do levantamento

- Varredura textual dos **577 arquivos C# de `Assets/Scripts`**, agrupados por
  domínio; **183** possuem sinais de input, alteração de posição ou bloqueio de
  movimento. Essa seleção inclui falsos positivos de menus e efeitos visuais.
- Leitura dirigida dos caminhos de input, reconciliação, apresentação de bombas,
  montarias, habilidades, cenário, itens e replicação co-op. Não foi uma revisão
  manual integral das 577 implementações.
- Conferência de `EditorBuildSettings`: **45 cenas habilitadas**, incluindo as
  **15 arenas Battle Mode**. Consulta de referências diretas de componentes em
  cenas e prefabs para locomover, lançar, transportar e empurrar jogadores.
  Uma referência em prefab não contabiliza automaticamente todas as instâncias
  em cenas; componentes adicionados em runtime também precisam de rastreamento.
- Abrangência: Battle Mode, Normal Game online e Boss Rush online, preservando
  offline/local, host/servidor, client proprietário, observadores e COM.
- Ganho percebido e frequência de correções são **hipóteses a medir**. Não houve
  novas capturas comparáveis nem partida de teste nesta etapa.
- Um arquivo existente, `BlackLouieDashPushAbility.cs`, falhou na decodificação
  UTF-8 estrita. Foi considerado na busca textual sem regravar seu conteúdo.
  Verificar sua codificação antes de uma implementação nesse arquivo.

## 2. Matriz de decisão

| Situação | Técnica | Limite de autoridade |
| --- | --- | --- |
| Input do próprio player com resultado previsível | Predição, confirmação/rejeição, reconciliação e replay | Input original enviado ao servidor; replay não repete comandos, áudio ou habilidades |
| Ação sobre bomba/objeto compartilhado | Predição reversível da apresentação, correlacionada ao input e à entidade | Servidor decide posse, disponibilidade, colisão e efeito real |
| Continuação determinística conhecida | Previsão curta com mesma função, estado e índice | Direção conhecida não garante resultado de colisão ou parada |
| Outros jogadores e atores remotos | Interpolação; extrapolação curta apenas se segura | Não executar inputs, decisões de IA ou gameplay remoto no client |
| Movimento imposto por evento ainda desconhecido | Evento/segmento autoritativo seguido de apresentação contínua | Não inventar início, destino ou alvo; antecipar só quando a informação for suficiente |
| Dano, morte, coleta, placar, destruição ou progresso | Resultado autoritativo; feedback reversível separado quando útil | Não conceder vantagem, remover obstáculo ou persistir resultado por previsão |

## 3. Baseline que deve ser preservado

| Sistema | Evidência no código | Ação no plano |
| --- | --- | --- |
| Locomoção comum do proprietário | `NetworkPlayerInput`, snapshots de `MovementController`, histórico/replay | Medir regressões; estender o modelo existente |
| Kick, punch, glove, ações de bomba dos Louies amarelo/azul | `NetworkBombFx`, `NetworkBombMotion` | Reutilizar confirmação, rejeição, revisões e restauração de collider/sorting |
| Search/Magnet | Segmentos aprovados no caminho de apresentação de bombas | Não confundir apresentação contínua com predição da escolha de alvo |
| RubberBomb | `RubberBombBounceSequence`, seed/índice e previsão limitada | Preservar sequência compartilhada e independência do RNG global |
| Bounce tile a tile do player | `PlayerPushedOutOfInvalidTile`, `NetworkPlayerBounce` | Reutilizar como referência para arcos aprovados, sem supor cobertura do lançamento que ocorre antes deles |
| Observadores | `NetworkPlayerSetup` usa ServerToClient; animação remota tem histórico em `NetworkPlayerAnimation` | Medir alinhamento posição/pose antes de reduzir buffers |
| Atores de fase no co-op | `OnlineCoopWorld` desativa lógica/física de atores de cena no client; `OnlineCoopSprites` já interpola amostras | Refinar eventos/curvas específicos, sem criar uma segunda simulação completa |

Constatações que direcionam a prioridade:

1. `MovementController.CanPredictNetworkMovement` exclui explicitamente
   `isMounted`, `IsRidingPlaying()`, overrides externos e stun. A predição da
   ação de bomba de um Louie não significa que sua locomoção montada já prediz.
2. `BombController.Update` retorna no client puro; a colocação passa por
   `PlaceBomb` e `NetSpawn.Server`. Há oportunidade de feedback imediato, mas
   não foi identificado nesse fluxo um proxy previsto de colocação.
3. `NetworkTankShot.OnStartClient` desliga colisores e simulação física;
   `TankShot.FixedUpdate` é autoritativo. Um disparo previsto precisa de uma
   apresentação própria, não da execução de `TankShot` com dano local.
4. `BattleMode5ConveyorController` aplica `MovePosition` diretamente no corpo
   do jogador. É candidato a divergência entre forças externas e replay do
   motor; a ocorrência e intensidade do problema precisam de captura.
5. No co-op, a captura visual é programada em intervalos de 0,05 s e a de tiles
   em 0,25 s. Esses intervalos não provam um atraso desse tamanho em todo evento,
   pois também existem mensagens específicas. Medir o caminho de cada ação
   antes de aumentar taxas globais.

## 4. Backlog por sistema

Prioridades: **P0** = diagnóstico/contratos prévios; **P1** = primeira onda de
ganho provável; **P2** = expansão com mais dependências; **P3** = apresentação
especializada/otimização posterior. A ordem não substitui medições.

### 4.1 Player, habilidades e montarias

Fontes dos nomes abaixo: `Assets/Scripts/Abilities`, `Mounts`,
`PlayerMovimentation`, `Bomb` e `Netcode`.

| ID / prioridade | Sistema e arquivos principais | Abordagem proposta | Risco/condição para avançar |
| --- | --- | --- | --- |
| A01 / P1 | Andar montado: `MovementController`, `PlayerMountCompanion`, `NetworkMountAnimation`, `NetworkPlayerState` | Predição de locomoção e replay do proprietário com estado de montaria confirmado | Não basta remover `!isMounted`: capturar velocidade, offsets, colisão e transições; validar cada montaria |
| A02 / P1 | Plantar bomba: `BombController.PlaceBomb`, `NetworkPlayerInput` | Proxy visual reversível identificado pelo input, depois vinculado à bomba do servidor | Sem fuse, dano, coleta ou NetworkIdentity fictícia; resolver capacidade, tile ocupado, posse e regras de atravessamento da bomba recém-plantada |
| A03 / P1 | Green Louie: `GreenLouieDashAbility` | Início/pose imediatos e percurso limitado, usando a geometria de dash existente | Colisões e parada confirmadas; não executar a coroutine de gameplay no client |
| A04 / P1 | Pink Louie: `PinkLouieJumpAbility`, animator/shadow | Predição do início e da parte conhecida do salto; confirmar pouso e direção | Tratar direção durante salto, sombra, invulnerabilidade, obstáculos e buracos separadamente |
| A05 / P2 | Black Louie: `BlackLouieDashPushAbility` | Predizer o dash do proprietário; reproduzir empurrões de terceiros após aprovação | Não reposicionar outro player/inimigo por especulação; exige arbitragem e cancelamento de movimento externo |
| A06 / P2 | Red Louie: `RedLouiePunchStunAbility` | Predizer pose e som do soco; opcional alcance visual | Stun, expulsão de item e acerto permanecem autoritativos |
| A07 / P2 | Mole: `MoleMountDrillAbility` | Predizer preparação/pose; avaliar deslocamento apenas no trecho conhecido | Não conceder invulnerabilidade, atravessamento ou destino de saída localmente |
| A08 / P2 | Tank: `TankMountShootAbility`, `NetworkTankShot`, `TankShot` | Pose, som e projétil visual previstos; casar com spawn autoritativo | Deduplicar tiro e som; cooldown, impacto, chain reaction e dano no servidor |
| A09 / P2 | LineBomb e Purple Louie: `LineBombAbility`, `PurpleLouieBombLineAbility` | Preview visual da linha e do cast, correlacionado a um grupo de ação | Confirmação parcial por tile; não criar bombas reais nem consumir estoque de forma definitiva |
| A10 / P2 | ControlBomb: `BombController.TryExplodeOldestControlledBomb` | Feedback imediato do botão/gesto; medir se algo além disso é necessário | Não antecipar explosão real, destruição ou dano; seleção da bomba continua no servidor |
| A11 / P2 | Montar/desmontar: `PlayerRidingController`, `PlayerManualDismount`, `MountWorldPickup`, `MountEggQueue` | Antecipar gesto e arco quando o pedido já tiver alvo conhecido; confirmar troca de estado | Disputa pelo mesmo pickup, dano durante transição, fila de ovos e mount ownership |
| A12 / P2 | Revenge: `BattleRevengeController`, `BattleRevengeSystem`, lançamento em `BombController` | Avaliar input de mira/movimento permitido e recoil/lançamento visual | Não prever elegibilidade, troca de player, retorno à arena ou eliminação |
| A13 / P1 | Velocidade, controles e passagens: `SkullDebuffController`, `BombPassAbility`, `DestructiblePassAbility`, `NetworkPlayerState` | Reproduzir durante replay os modificadores já confirmados pelo servidor | Item/debuff não pode mudar retroativamente todo o histórico pendente; correlacionar alteração ao tick |
| A14 / manutenção | Kick, punch, glove, Yellow/Blue, Search/Magnet, Rubber e bounce já cobertos | Comparar logs, tratar interrupções e ampliar testes por papel | Não criar outro presenter concorrendo com os atuais |

Os demais registros de `AbilityRegistry` — `FullFire`, `PierceBomb`, `PowerBomb`
e `InvincibleSuit` — afetam regras/estatísticas. Usar estado confirmado no motor
ou na apresentação; não prever concessão de alcance, penetração, poder ou proteção.

### 4.2 Cenário, arenas e deslocamentos externos

| ID / prioridade | Sistema e arquivos principais | Abordagem proposta | O que permanece autoritativo |
| --- | --- | --- | --- |
| C01 / P1 | Esteiras: `BattleMode5ConveyorController` | Integrar influência conhecida ao tick/replay; sincronizar fase/direção da esteira e transferências de célula | Mudanças globais, bloqueio dinâmico e movimentação de objetos de terceiros |
| C02 / P2 | Mola e lançadores: `BattleMode4SpringTileController`, `Ground/SpringLauncher`, `StageAssets/Cannon/CannonLauncher`, geyser bootstrap | Trajetória aprovada contínua; avaliar antecipação do contato apenas com configuração e fase conhecidas | Destino, disputa do lançador, consequências do pouso e interação com hazards |
| C03 / P2 | Corda: `BattleMode11ImpulseRopeController` | Sincronizar início/fase/direção e apresentar arco/voo inteiro; prever só pedido local validável | Ativação concorrente, término e dano durante voo |
| C04 / P2 | Carrinho: `BattleMode9MinecartController` | Estado de trilho, segmento, progresso e velocidade para apresentação contínua; avaliar comandos do ocupante | Entrada/saída, desvios dinâmicos, atropelamento e disputa de ocupação |
| C05 / P2 | Portais: `BattleMode7PortalController`, handlers de swap e portais World3 | Feedback reversível da entrada; transição visual após confirmação; snap explícito do teleporte | Destino, reserva de saída, troca de sala e scene routing; não interpolar através do mapa |
| C06 / P2 | Correnteza/barco/plataforma: `WaterCycleCurrent`, `BoatRideZone`, `FloatingPlatform` | Compor deslocamento do suporte/corrente com locomoção; fase confirmada e referencial relativo | Embarque, desembarque, propriedade do transporte, hazard e afogamento |
| C07 / P2 | Pusher: `PusherPatrolAndPush` e resolver de tile inválido | Segmentar o empurrão inicial e o bounce posterior; preservar colisão e continuidade entre ambos | Contato inicial contra atores móveis e resultados de esmagamento |
| C08 / P2 | Ímãs e redirecionamento: `MagnetIndestructibleTileHandler`, `BattleMode6RedirectionController` | Reutilizar apresentação de bomba/objeto por segmento e revisões | Escolha/disputa do objeto, mudança do campo e colisões |
| C09 / P3 | Caldeira, tiles de força/swap, dinamite, foguetes e mecanismos destrutíveis | Eventos e trajetórias aprovadas para puxar/mover/animar; inferir fase apenas quando estável | Explosão, edição de tilemap, ativação e spawn de inimigo/item |
| C10 / P3 | FallingBomb, powder trail, teto e Sudden Death | Apresentação temporal de eventos já anunciados; conferir feedback existente | Sorteio/ativação, queda efetiva, bloqueio, dano e encerramento da rodada |

Para C02-C07, separar **a ação principal** do bounce de recuperação ao aterrissar.
`NetworkPlayerBounce` cobre o segundo caminho; isso não demonstra que o voo do
canhão, a viagem de carrinho ou a entrada no portal já usam a mesma técnica.

O wiring direto das cenas também mostra reaproveitamento: a arena 12 referencia
`BattleMode9MinecartController`; a 13 usa `SwapGroundTileBlackoutHandler`; a 14
referencia dinamite e ímãs; a 15 usa `PassThroughIndestructibleTileHandler`; e a 8
combina teto com `PowerGroundTileHandler`. Não usar apenas o número no nome do
script para determinar sua cobertura. Nas arenas sem controlador encontrado por
referência direta, rastrear também prefabs e loaders de runtime antes de concluir
que não existe mecânica especial.

### 4.3 Observadores, inimigos, bosses e objetos remotos

| ID / prioridade | Área | Abordagem proposta | Exclusão |
| --- | --- | --- | --- |
| R01 / P1 diagnóstico, P2 ajuste | Players remotos: `NetworkPlayerSetup`, `NetworkPlayerAnimation`, `NetworkMountAnimation` | Medir alinhamento de transform, pose e montaria; ajustar buffer e extrapolação curta com limite | Não reproduzir inputs ou prever decisões de outro jogador |
| R02 / P2 | Co-op: `OnlineCoopWorld`, `OnlineCoopSprites` | Auditar atraso e custo por família; usar descritores de arco/segmento onde interpolação linear perde a forma | Não reativar em massa os scripts/física de cena desativados no client |
| R03 / P3 | Inimigos: `EnemyMovementController` e especializações | Interpolar caminhada e apresentar saltos/avanços aprovados; priorizar frog/hop/snowman/korobokkuru e movimentos em tiles | Escolha de alvo, pathfinding, RNG, contato e morte não são previstos |
| R04 / P2-P3 | Forças de inimigos: `HurricaneMovementController`, `MagnetMovementController`, `SubmarineBubbleMovementController` | Eventos de captura/puxão e segmentos contínuos do player afetado | Não prever captura, alvo, aprisionamento, dano ou libertação |
| R05 / P3 | Projéteis de inimigos/bosses: spear, rockets, ice, stars, hearts e demais famílias | Trajeto visual após spawn/direção confirmados; parar/corrigir explicitamente no impacto | Sem colisão de dano client-side; homing requer atualizações autoritativas |
| R06 / P3 | Bosses e Bombers: `SunMask`, `ClownMask`, `FreezerVenus`, `MechaBossSequence` | Interpolar movimento; eventos de ataques e curvas aprovadas; preservar telegraph, pose e áudio | Não antecipar a decisão de ataque nem mostrar área segura com base em adivinhação |
| R07 / P3 | Itens expulsos, skull bounce, montarias e ovos no mundo: `ItemPickup`, `NetworkWorldMount`, `NetworkEggFollower` | Auditar caminhos existentes de expel/landing e buffers; apresentação por fase quando necessário | Coleta, posse, destruição e alteração do loadout continuam no servidor |
| R08 / P3 | Cenário móvel/ambiental: barreis, snowballs, bubbles, plataformas, foguetes e elementos World3 | Interpolação ou fase determinística visual para movimentos periódicos confirmados | A geometria visual não pode prometer colisões diferentes das autoritativas |

### 4.4 Onde não aplicar rollback especulativo de gameplay

- `Health/CharacterHealth`, `Enemies/StunReceiver`, explosões e hazards:
  dano, stun, invulnerabilidade, morte e chain reactions são decisões do servidor.
  Pose/som de uma ação ofensiva podem antecipar o gesto sem confirmar o acerto.
- `Itens`, `Destructible`, `Indestructible`, `GameManager`: não conceder item,
  abrir caminho, destruir tile ou gerar recompensa por hipótese. Feedback de
  tentativa deve ser reversível e visualmente distinguível de confirmação.
- `GameSession`, `PlayerPersistentStats`, `SaveSystem`, `Unlock`,
  `Achievements`, regras/resultado Battle e estado Boss Rush: não prever placar,
  progresso, save, desbloqueio, eliminação ou conclusão de fase.
- Lobby, seleção, menus, localização, Discord, Editor e infraestrutura:
  ficam fora desta skill. Botões podem ter resposta visual local sem rollback
  de simulação ou confirmação falsa de uma operação de rede.
- Câmera, HUD, áudio, partículas e backgrounds: tratar como apresentação.
  Corrigir sincronização, duplicação ou frame hitch; não adicionar um motor de
  rollback a um efeito puramente cosmético.
- COM/IA: decisões continuam no servidor. Reutilizar interfaces de geometria
  para apresentação sem executar a decisão duas vezes.
- Não converter automaticamente todos os sistemas aleatórios em determinísticos.
  RubberBomb foi uma mudança explicitamente solicitada; outra regra exige uma
  decisão de design própria e avaliação do efeito na gameplay offline.

## 5. Ordem de execução recomendada

| Etapa | Entrega concreta | Dependências/critério de saída |
| --- | --- | --- |
| 0 — Medir | Capturas online/offline comparáveis e ficha por ação; separar latência, correção e frame hitch | Identificar responsável, gatilho, alvo, início/fim, papel e quantidade de ocorrências; priorizar por impacto observado |
| 1 — Motor e contexto | A01 locomoção montada, A13 modificadores por tick e investigação C01 esteiras | Replay sem aplicar força duas vezes; nenhum collider/velocidade de estado futuro em input antigo |
| 2 — Ações locais simples | A02 plantar bomba; A03/A04 Green/Pink em mudanças separadas | Apresentação no primeiro frame possível de input aceito localmente; confirmação/rejeição e deduplicação corretas |
| 3 — Ações compartilhadas | A05-A12, começando por pose de Red e disparo de Tank, depois push/linha/drill/transições | Autoridade e confirmação parcial definidas; nada cria dano, posse ou bomba duplicada |
| 4 — Ambiente que move o player | C02-C08, uma família por vez; encadear movimento principal e bounce | Pose, collider, referência espacial e devolução ao motor sem salto no encerramento |
| 5 — Mundo remoto | R01/R02 antes de R03-R08; depois C09/C10 conforme medições | Melhora mensurável sem reintroduzir simulação de cena nem saturar a rede |
| 6 — Consolidar | Atualizar skill/notas, reproduções e cobertura por modo | Documentar limites, papéis testados e caminhos ainda exclusivamente autoritativos |

Não começar por uma migração geral para rollback de toda a física Unity. O motor
atual já possui tick/histórico e presenters especializados. Criar outro sistema
paralelo ampliaria a disputa por transform, input lock e colisores.

## 6. Contrato de investigação de cada item

Antes de implementar um ID, registrar:

1. Gatilho real e papel que executa: hardware local, input sintético, trigger,
   IA, coroutine, callback de rede ou evento de fase. Confirmar wiring em cena,
   prefab e criação em runtime; não concluir ausência de autoridade só pela
   falta de `NetSync` no arquivo de gameplay.
2. Estado mínimo: tick/ação, entidade, revisão, direção, segmento/fase, duração,
   curva, cooldown e modificadores necessários. Documentar o que não é previsto.
3. Quem escreve transform/collider/pose, incluindo motor, NetworkTransform,
   reconciliação, mount animation e efeitos de cenário. Definir o único dono
   temporário da apresentação e a restauração antes das consultas de colisão.
4. Confirmação, rejeição e transferência: casar o objeto previsto com o real;
   tratar resposta parcial, duplicada, antiga, fora de ordem ou ausente.
5. Cancelamento: dano, morte, desmontagem, explosão, pausa, desconexão, fim de
   rodada/fase, troca de cena, wrap e quebra de sequência.
6. Medições e reprodução: mesma fase, loadout, velocidade, tipo de bomba,
   sequência de input e build/protocolo; anexar logs do client e do servidor.

## 7. Medições e aceitação

Registrar por tipo de ação: tempo input→primeira apresentação, espera por
autoridade, número/magnitude de correções visíveis, espera no endpoint, perda de
frames de arco, rejeições, fila/timeout, RTT/jitter, frame time e tráfego por ação.
Usar percentis somente com amostra suficiente; separar input bloqueado, ação
rejeitada e ocorrência de frame hitch das ações válidas em movimento livre.

Critérios para aprovar cada entrega:

- Ação local previsível deixa de depender do primeiro round trip para exibir sua
  apresentação, mantendo as regras e consequências do servidor.
- Confirmação não reinicia animação, não repete som/efeito e não devolve um
  objeto ao presenter anterior.
- Rejeição restaura posição, collider, sorting, posse visual e locks pertinentes;
  replay não reexecuta a habilidade. Um alvo de erro deve ser definido por
  mecânica e escala de pixel, não um valor universal arbitrário.
- Arcos aprovados são contínuos, a parada vem depois do segmento correto e
  filas são limitadas. Teleporte não vira um movimento atravessando o cenário.
- Sem vantagens locais: dano, coleta, cooldown real, estoque, invulnerabilidade,
  destruição e placar continuam autoritativos.
- Ganho medido não aumenta correções relevantes ou custo de rede/CPU acima
  do limite definido para a ação. Se o ganho não aparecer, não ampliar a técnica.

## 8. Matriz de validação futura

- Offline solo e local 2P; servidor/host, proprietário, observador e COM.
  Usar também client atribuído a P2-P6; slot de hardware local não é PlayerId.
- Battle arenas relevantes, Normal Game co-op e Boss Rush co-op. Confirmar
  cenas habilitadas e catálogos de apresentação para conteúdo de campanha.
- Teclado, gamepad e mobile/sintético, inclusive press/release e inputs curtos.
- RTT ilustrativo de 0/50/100/200 ms, variação de jitter e perda controlada;
  distinguir perdas/retransmissões de problema de renderização. Esses valores
  são cenários de teste, não uma promessa de suporte ou limite já aprovado.
- Alvo parado/móvel, bomba própria/alheia, dois players disputando o mesmo alvo,
  mudança de habilidade durante ação, água/buraco/wrap, colisão dinâmica e
  interrupção antes/durante/depois da confirmação.
- EditMode direcionado em `OnlineRoomEditModeTests`: tempo controlado,
  confirmação/rejeição, replay, sequência/revisões, filas, curvas e limpeza.
  Compilar C#/Unity e remover warnings introduzidos em cada mudança de código.
- Partidas com dois peers para avaliar a sensação. Testes de helpers não
  demonstram ausência de input lag percebido.
- Alterou wire/RPC? Atualizar protocolos compatíveis e validar ambos os builds.
  Publicação e reinício de salas dependem da autorização da tarefa futura.

## 9. Primeiro lote recomendado

Começar com **instrumentação + locomoção montada + plantar bomba**, entregues em
mudanças separadas. Em seguida, **Green Louie e Pink Louie**. Esteiras entram
cedo se as capturas confirmarem correções frequentes do motor. Essa sequência
prioriza ações repetidas e perceptíveis, estabelece as bases para outras
montarias e evita começar pelos casos de maior disputa entre atores.

O plano não inicia implementação, publicação nem atualização de protocolo.
