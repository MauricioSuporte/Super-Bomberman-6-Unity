# Predição de movimento e reconciliação

O online usa servidor autoritativo com predição do movimento do jogador local.
O cliente envia controles, nunca posição, velocidade, dano ou um delta de tempo.
A mesma lógica de curvas, alinhamento e passos de pixels do `MovementController`
é executada em ticks de física no cliente e no servidor.

## Fluxo

1. O cliente amostra teclado, controle ou toque pelo PlayerInputManager local P1,
   independentemente do slot P1–P6 atribuído na sala. Bordas das ações ficam
   acumuladas até o próximo tick; direções usam o estado mantido mais recente.
2. Cada tick recebe uma sequência crescente e entra no histórico local. Após o
   primeiro snapshot, andar e mudar de direção respondem sem esperar a rede.
3. O servidor aceita comandos somente do dono do objeto Mirror, normaliza as
   máscaras e consome no máximo um comando por tick de física. Não aceita passos
   extras de movimento pelo simples envio de mais comandos. COM e host local
   mantêm sua simulação anterior.
4. Depois da física, o servidor envia ao dono a posição, o último input processado,
   a velocidade autorizada e o estado das curvas/acumuladores/alinhamento.
5. O cliente restaura esse estado, remove inputs confirmados e reexecuta somente
   os restantes. Replay não simula a cena física inteira, não aplica dano e não
   executa chutes ou outras habilidades que alterem objetos do mundo.
6. O NetworkTransform continua ServerToClient para observadores. No dono, seu
   Update fica desativado para não sobrescrever a posição reconciliada. A animação
   de caminhada é local; observadores continuam usando os frames do servidor.

O histórico contém até 128 ticks. A fila do servidor contém até oito: após uma
interrupção longa, descarta os comandos mais antigos para não acumular segundos
extras de atraso. Inputs descartados não são simulados; a confirmação do próximo
comando traz o resultado real. Ações nesses comandos também podem ser descartadas.
Se faltar uma parte do histórico, o cliente suspende a predição até conseguir
reconciliar um histórico completo. Transporte e comandos continuam confiáveis,
portanto perdas podem atrasar sua entrega. Não há garantia de latência zero.
Snapshots são enviados a até 30 Hz e também durante a pausa (tempo não escalado).
Cada snapshot confirma o resultado após a física, incluindo múltiplos ticks que
possam ter ocorrido no mesmo frame. Pausa/início/resultado bloqueiam a predição;
comandos pendentes no servidor são descartados durante esses bloqueios.

## Escopo e limites

Esta implementação faz rollback **do movimento do dono**, não do mundo inteiro.
Bombas, explosões, itens, dano, RNG, inimigos, vitória e alterações de tiles
continuam autoritativos. Colocar/chutar/arremessar bombas ainda depende da autoridade do servidor.
O chute já confirmado tem apresentação imediata no client de quem chutou,
conforme o complemento abaixo; ainda não há predição especulativa do chute.
A reconciliação consulta o mundo atualmente conhecido pelo cliente; não guarda
histórico de tiles, bombas ou outros jogadores. Divergências podem gerar correções
visíveis de posição, especialmente perto de obstáculos dinâmicos.

Montarias, saltos de montar, lançadores, movimentos externos, stun e poses especiais
suspendem a predição e usam posição/animação autoritativas. Não reproduzir saltos
ou poses de luva paralelamente evita efeitos duplicados durante o replay.

Para predição de todas as ações, o próximo trabalho é separar dados de simulação
por tick dos MonoBehaviours e corrotinas: registrar bombas/fusíveis/ocupação de
células, IDs de ação, RNG e eventos de spawn/despawn; prever as ações, confirmar ou
rejeitar cada ID e reproduzir efeitos audiovisuais uma única vez. Só então seria
correto anunciar rollback completo. Reexecutar Physics2D global e corrotinas sobre
a cena atual não fornece esse contrato.

## Compatibilidade e validação

O protocolo passa de 17 para 18. Cliente e servidor precisam ser recompilados
juntos. A implementação usa os componentes existentes do Player.prefab; nenhuma
nova referência de cena ou prefab é necessária.

Testes EditMode em OnlineRoomEditModeTests cobrem sequência/rollover, histórico
limitado e restauração/replay do motor com curvas e frações de pixel, além do gate
Offline/Host/Client existente. Esses testes não substituem uma sessão com dois
peers. Validar com os mesmos builds: Offline P1/P2, host, dono e observador;
movimento/curvas, obstáculos, soltar direções, bombas e fuga, montaria/desmontaria,
luva, stun, morte, pausa, resultado e duas rodadas. Repetir com RTT de 50/100/200 ms,
jitter e perda de pacotes, conferindo que ações não se duplicam e correções não
atravessam paredes no estado autoritativo.

### Verificação desta implementação

C# runtime/Editor compilados pelo dotnet; scripts e Weaver do Mirror compilados
no Unity 6000.6.0f1. As 24 verificações selecionadas passaram por invocação direta
dos métodos NUnit no Editor, incluindo replay com curva, subpixel e bloqueio por
parede. Registro local: `Logs/OnlinePredictionDirectChecks.txt`. Essa execução não
incluiu setup/teardown do Test Runner. O Test Runner foi cancelado ao solicitar
salvamento da TitleScreen, que já estava modificada; a cena aberta foi preservada.
Não foram executadas partidas entre dois peers, testes com latência/perda, nem
validação visual das montarias/arenas. Essas validações continuam pendentes.


## BombKick: resposta após confirmação e diagnóstico (protocolo 19)

A caminhada é prevista, mas BombKickAbility continua sendo validada no servidor.
Antes, além da ida/volta do input, o jogador esperava o buffer/timelineOffset do
NetworkTransform da bomba. A posição lógica do client também continuava lendo
lastPos, que só a simulação do servidor atualiza, mesmo depois de o transform
replicado já ter avançado. Agora o client lê a posição replicada; offline/servidor
continuam lendo lastPos.

O servidor envia ao client de quem chutou cada segmento já aprovado (origem,
destino de uma célula, duração, tempo e tick). NetworkBombFx apresenta esse trecho
sem aguardar o buffer global. Usa o relógio estimado do servidor para recuperar a
parte do trecho transcorrida durante a rede e limita a posição ao destino aprovado.
Não chama StartKick no client, não decide colisão/dano, não repete sons e não
extrapola o restante do corredor. Observadores e host mantêm a apresentação anterior.
O relógio estimado é predictedTime menos meia RTT; não é medição exata de trânsito.

NetworkTransform continua ativo, inclusive a descompressão dos snapshots. O
presenter escreve depois da interpolação normal. No fim, mantém a posição confirmada
até a timeline atrasada alcançar a parada e devolve a apresentação ao transform;
se faltar um segmento por um segundo de gameplay, retorna à replicação normal.
Durante pausa, o trecho fica congelado. A bomba destruída ou o player desativado
liberam o estado. Chutes recusados continuam usando a posição autoritativa anterior.

Os logs existentes em Diagnostics/Movement agora incluem:

- kick_client_contact: candidato observado no tick predito atual; replay não gera outro contato.
- network_tick_apply: duração em fila no servidor, inputTick e queuedInputs.
- kick_result/kick_accepted/kick_rejected: decisão autoritativa e bloqueios.
- kick_segment_send/kick_segment_receive: confirmação enviada/recebida;
  durationMs no client mede contato local até feedback, authoritativeTick permite
  correlacionar com o servidor e messageAgeMs é a idade estimada da mensagem.
- kick_stop_send/kick_stop_receive e kick_transform_handoff: término e devolução
  ao NetworkTransform. Entrada de controles: L=256 e R=512.

Na captura local do protocolo 18 examinada, a RTT mediana era aproximadamente
73 ms e o buffer de interpolação 40 ms. Seis deslocamentos de bombas observados
após input próximo tiveram mediana de 120 ms; esse evento antigo não era um ack
exato de chute. Os novos eventos separam candidato, processamento e confirmação.

Cliente, servidor e supervisor precisam ser recompilados juntos no protocolo 19.
Validar com dois peers e latência: chute de bomba própria/alheia, corredor bloqueado,
parada com R, RubberBomb, movimento seguido de chute, pausa, explosão durante o
chute e troca de rodada. A apresentação rápida remove o buffer adicional, mas
não elimina o tempo que a rede e a validação do servidor exigem.


### Captura do protocolo 19 e instrumentação adicional

O client 7e1af3a registrou seis confirmações em 76–198 ms após contato previsto,
com RTT de 68–84 ms. No servidor da mesma sessão, a fila de input amostrada
teve média de 44 ms e máximo de 140 ms. Não são medidas isoladas da rede:
contato candidato pode anteceder o alcance permitido pelo servidor.

Logs adicionais (mesmo protocolo, sem alterar regras de jogo):
- kick_segment_send agora registra cada trecho, duração, deslocamento e tick.
- kick_segment_apply registra cada trecho recebido, idade estimada e correção
  de posição aplicada à bomba (delta).
- kick_endpoint_wait mede tempo de gameplay segurando o destino de um trecho
  esgotado até receber outro; pausa não entra nessa duração.
- kick_player_reconcile registra delta final após restore/replay por dois segundos
  após contato candidato, além do tick confirmado e inputs pendentes.
- network_input_queue distingue fila vazia que soltou input e descarte por fila
  cheia; limitado a quatro eventos por segundo por player.

Para predição imediata do kick, o client precisará manter estado especulativo da
bomba e reconciliar/reexecutar junto ao jogador. Só acelerar o visual confirmado
não remove a espera inicial. Comparar as novas capturas antes de alterar colisões,
principalmente bomba própria/alheia, RubberBomb, parada com R e explosões.


## Kick continuity and input bursts (protocol 20)

Compared client motion-20261025-034120-50fba506.jsonl with server
motion-20261025-034122-c32c9d2e.jsonl, both c5c5851/protocol 19.
Six kick confirmations: median 97 ms, maximum 210 ms. The client recorded 46
endpoint holds (median 47 ms, maximum 88 ms), and 13 of 311 post-replay corrections
exceeded 0.1 world units (maximum 1 tile). Server input queue samples reached
151 ms. Server segments arrived mostly every 117 ms, while the sent duration
was 111 ms; catching up network age at every segment compounded the endpoint holds.

Protocol 20 changes:
- Server drains each received input batch, using latest held direction and merging
  action edges; one simulation step per server physics tick remains the speed limit.
  This trades intermediate movement samples in a burst for current input latency.
- Empty input gaps retain direction only, for at most 100 ms. Pause clears the grace.
  No action taps repeat. Predictable movement snapshots require a new acknowledged
  tick, avoiding replaying pending ticks on top of server-extrapolated duplicate acks.
- Kick messages report fixed-step-rounded segment duration. Offline kick physics,
  authoritative tile effects, pickups, damage, fuse and bounce rules are unchanged.
- A confirmed kick may continue locally at most one additional tile and 150 ms.
  Read-only collision queries block occupied exits, Stage and Water triggers,
  destructible and indestructible tiles. Stops, reversals and large differences
  apply the authoritative state immediately; smaller forward corrections decay.
- The local motor restores the presented bomb position before current/replayed
  collision queries, preventing NetworkTransform's older timeline from temporarily
  becoming the obstacle position. NetworkTransform remains enabled for decoding.
- kick_continuation_prediction marks speculative continuation. kick_endpoint_wait
  now labels confirmation gaps with local continuation versus blocked exits.

This is bounded continuation prediction AFTER the first server confirmation.
The first kick still waits for server validation; this change does not claim zero
initial kick latency or full historical rollback of every moving world object.
No scene/prefab changes. Rebuild client, server and supervisor together.

Regression tests cover bounded continuation, stop messages, solid/trigger exits,
input burst action preservation and held-input timeout. Two-peer manual QA still
needs kicks against moving players, RubberBomb bounce, R stop, explosions, pause,
water/holes, stage wrap and repeated rounds under RTT/jitter/loss.


## Explicit action edges and complete movement steps (protocol 21)

Client motion-20261025-041934-7bc49a82.jsonl (77fb7df Editor) was compared with
server motion-20261025-041936-e9ced81d.jsonl (a0ff0be). Both had six transmitted
A edges; held-only messages had pressed=0. Synthetic input nevertheless inferred
GetDown from held transitions. Protocol 20 cleared action holds during message
gaps; the next held message could therefore cause another pickup, followed by
a release/throw on another gap. This was input reconstruction, not evidence of
six duplicated network commands. The client also recorded 21 of 170 kick-window
reconciliations above 0.1 world units (maximum 0.5625).

Remote network synthetic input now requires explicit transmitted edges for
GetDown. Held state survives ordinary gaps for all buttons. After 100 ms without
input it releases safely; resumption without a new edge cannot activate the glove.
COM/offline synthetic inputs retain their existing inferred-edge default.
network_action_edge, power_glove_pickup and power_glove_release correlate these
transitions in subsequent client/server captures.

Supersedes protocol 20's latest-held batch merging. Every acknowledged input now
gets its complete movement step, in order. Server-clock credits accrue once per
physics tick, cap at eight, and allow up to four queued steps per physics tick.
A delivery gap accumulates time to recover; packet floods cannot create time.
Empty queues do not extrapolate movement then replay that time again. The motor
uses immediate positioning inside server network ticks as it already did during
owner prediction, so multiple steps before the physics solver do not overwrite
one another's MovePosition. Normal offline and host-local loops remain unchanged.
Actions are still exposed once per rendered gameplay frame, rather than running
bomb placement/glove updates during each catch-up motor step.

Regression coverage includes explicit edges versus held transitions, bounded
server-time credit/floods, and server as well as client motor replay with turns,
subpixels and walls. Two-peer latency/jitter gameplay remains a separate manual
check: single A plants once; holding A does not pick up; a new A picks up and
release throws; also test kick, R, pause, mount and repeated rounds.
