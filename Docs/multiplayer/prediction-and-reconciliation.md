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


## Initial local kick prediction (protocol 22)

Compared client motion-20261025-180908-86ed8bf0.jsonl and offline
motion-20261025-181207-3054eb6b.jsonl with server
motion-20261025-180906-e426d789.jsonl. All used 3ab80cb/protocol 21.
Three correlated online contacts waited 70-75 ms for confirmation (RTT 66-73 ms).
Eight offline kicks first moved in 0.7-24.5 ms. Continuation prediction alone
could not remove the initial wait for a server-approved segment.

The owner now predicts initial kick presentation on the actual movement contact,
using BombKickAbility's existing reach and early-kick gates. This works independently
of diagnostic logging. It checks the next exit locally, moves the replica immediately,
and plays local kick audio. It never calls client StartKick, sends an extra kick
command, changes fuse/damage, destroys items or invokes terrain effects. The server
still evaluates the normal ordered movement input and sends authoritative segments.

Each local prediction records contact tick, origin and bounded per-input bomb
positions. Reconciliation restores these positions during motor replay and
reproduces the original adjacent-kick motor hold without starting another kick.
Confirmed trajectories retain this history and smooth small forward corrections.
If the server acknowledges the contact without a kick, or confirmation times out,
presentation rolls back to the replicated position. A rejected same-direction
contact is suppressed until direction or bomb position changes. Pause freezes the
presentation timeout; disable/disconnect cancels pending predictions.

The ordinary NetworkTransform stays enabled to decode authoritative snapshots.
Already-moving replicas and blocked/unknown exits are conservative prediction
failures; those cases still wait for server authority. Continuation remains bounded
to one additional tile and 150 ms. This is not full rollback of the entire world.

Kick audio uses a dedicated NetworkBombFx RPC. The predicting owner suppresses
its confirmed duplicate, other clients play once, and the host plays locally once.
Other sound events keep the existing shared audio path.

New diagnostics: kick_prediction_begin (durationMs=0 local presentation wait),
kick_prediction_confirm (elapsed wait for authority and prediction error delta),
kick_prediction_reject (acknowledgement/timeout and rollback delta). The existing
kick_segment_receive still measures network feedback, not initial visual delay.

Regression coverage includes immediate presentation before any response, blocked
exits, Offline/Host role guards, duplicate contacts, confirmation, rejection/retry,
replay positions and history capacity/uint rollover. Validate online manually with
normal/RubberBomb, own/other bombs, R stop, glove/punch, moving players, pause,
water/holes/wrap, explosions and RTT/jitter/loss. No scene/prefab rewiring required.

The first confirmation preserves a bounded owner-input timeline lead (maximum
150 ms), rather than slowing the bomb back to server-now after immediate local
feedback. Stop messages inherit no lead. A regression checks this alignment.


## Recent-kick motor parity and presentation measurements

Compared online motion-20261025-185743-31199fd6.jsonl and offline
motion-20261025-185917-988ea9dd.jsonl, both 3dc021d/protocol 22.
All 11 local kick predictions were confirmed, none rejected. Confirmation still
took 61-93 ms, but no longer gated initial presentation. Median input-to-motion
was 15.7 ms online versus 14.1 ms offline. These captures contain different input
sequences; large waiting samples include blocked input and do not measure raw lag.

The online capture still had ten kick-window player corrections above 0.125 world
units, including one 0.75 correction with prediction disabled. An in-game frame
also took 178 ms; that is a local frame hitch, not a measured network round trip.

The motor previously checked Bomb.IsBeingKicked, which is never set on pure-client
replicas, and cleared its recent-kick reference during every restore. It could
therefore apply stationary-bomb reentry/centering rules while the server treated
that same bomb as already kicked. The client now rebuilds that reference for each
current/replayed input tick and queries the existing kick presentation history.
Stop ticks restore ordinary collision rules; earlier replay ticks retain the
historical moving state. Offline/server still consult their actual Bomb flag.
No client gameplay coroutine, damage or terrain effects are introduced.

Previous bomb_sample events run before NetworkBombFx's LateUpdate and can show
the older NetworkTransform timeline. kick_endpoint_wait labels confirmation gaps,
including gaps already bridged by prediction; it is not proof of a visible pause.
New kick_presentation_sample events sample after presentation at 10 Hz.
kick_segment_apply.delta now measures the immediate displayed correction;
predictionError separately records target-minus-previous presentation. A large
server-now difference is not automatically a visible correction on the owner's
ahead-of-server presentation timeline.

Regression coverage extends initial prediction/confirmation/rollback with motor
reference restoration, before-start checks, and historical/current stop behavior.
Wire protocol remains 22; public builds still require matching build identities.
Two-peer subjective gameplay validation remains required after publication.


## Bomb abilities (protocol 23)

NetworkBombFx now shares its presentation/confirmation/rollback path with punch,
glove lift/carry/throw and Yellow Louie's bomb push. Only the owning client samples
real input, once per Update; movement replay cannot fire an ability again. The
existing input command still carries the original directional intent. The local
motor history records the short offline animation lock independently.

Pure clients no longer run the authoritative punch/glove/Yellow gameplay routines.
Prediction moves the replica and renders its pose, without StartPunch, fuse pause,
damage, random draws, terrain mutation or a client-created bomb. Rejection restores
the buffered server position, collider and sorting. A per-bomb serial revision
prevents an older presenter (for example the bomb owner after another player picks
it up) from restoring stale motion. Holding is anchored to the carrier's current
client position; ordinary walking can still predict while carrying.

Punch and throw use the existing arc duration, height and pixel-grid sampling.
Unknown stage bounds, wrapping and room seams wait for the authoritative segment.
Search/Magnet selection and random RubberBomb landing bounces remain authoritative;
approved segments bypass the additional NetworkTransform presentation buffer.
Rubber wall reflections may continue for at most one cell / 150 ms against a known
solid wall and a free return path. This does not predict random landing choices.
Yellow's moving destructible blocks are visualized only after server approval and
settle/destroy messages. Approved moving blocks reserve current/next cells with
the same Stage collision size as the offline motor; no speculative terrain
changes are performed.

New client diagnostics: bomb_motion_prediction_begin/confirm/reject,
bomb_motion_apply, bomb_motion_sample and rubber_bounce_prediction. Server
bomb_motion_send records kind, input tick, duration and displacement. Existing
kick-specific event names remain available for Kick comparisons. A local begin
with durationMs=0 records presentation starting without a server response, not a
claim of zero hardware/render latency. The supplied 213057 online / 213615 offline
captures predate these per-ability events and use different action sequences.

Regression tests cover arc/carry/reflection geometry, collision roles, immediate
prediction, confirmation/rejection, movement while carrying, stale presenter
messages, observer authority guards, Yellow tile settle/destruction and wire
roundtrip. Manual host/owner/observer gameplay (including mounted actions, pause,
water/holes/wrap, chain reactions and packet jitter) still needs post-deploy checks.
No prefab or scene rewiring is required. Client and server must both use protocol23.


## Tile-to-tile bounce continuity (protocol 24)

Short confirmed punch/throw landing arcs now queue on NetworkBombFx until the
current arc finishes, including their final stop. Continuations retain only the
fractional-frame remainder instead of starting at packet age or adding the initial
prediction lead on every tile. This preserves short apexes and direction changes
when multiple messages arrive together. Queues are bounded at 32 segments; a new
first segment replaces the prior sequence. Rubber landing directions now use the
shared deterministic sequence described below.

PlayerPushedOutOfInvalidTile reports approved parabola segments, wraps and a final
stop through NetworkBombFx's NetworkPlayerBounce partial. The client presents
those segments continuously, disables its replica collision only for that motion,
restores the active bounce position after owner reconciliation, and gives it pose
priority over buffered player/mount animation. Curve duration and quantized fixed
step completion time are distinct. No client runs the resolver's kill/hole/world
logic; host/offline retain the existing coroutine. Initial external pushes still
require the authority event; this is not prediction of hidden or random hazards.

New player_bounce_send / player_bounce_apply events correlate segment revisions,
durations and displacement. Manual checks should include pushers/spring/minecart
landings, occupied landing tiles, mounts, holes, wrap, death, pause and disconnect.
See the repository skill super-bomberman-6-online-prediction for reusable criteria.


RubberBombBounceSequence computes a stable seed from launch cell, cardinal facing
and throw/punch distance, using explicit uint arithmetic. Direction at an index
and the minimum 3-5 bounces are pure functions of that seed, independent of
UnityEngine.Random, frame time, netId or platform hash codes. Offline/server use
the same function; a new launch resets its seed and index. Other gameplay RNG,
such as expelling an item, is unchanged. Repeating the same launch now repeats
its bounce directions by design.

Motion messages include seed, ordinal, landing duration and height. Clients can
anticipate at most one mandatory landing bounce / 150 ms, inside known stage/room
bounds, then retain that phase on matching confirmation. After the deterministic
minimum, the server still checks occupied landing tiles and decides whether to
continue, stop, wrap, stun, explode or mutate the world. Wrap ordinals are sent
by the server; clients do not speculate through a room seam. This avoids guessing
when a hidden/moving actor would change termination while removing the per-tile
wait for a known mandatory direction. rubber_landing_prediction logs seed/index.


## Plan milestones 1-3 (protocol 25)

Mounted owner walking now uses the same tick/replay motor with confirmed mount
context, speed and pass capabilities. The local mount presenter drives only
walking visuals; ride transitions, external movement, stun and special poses
retain authority. A mount visual mismatch suspends prediction until its context
matches. BombPass/DestructiblePass queries during replay use acknowledged flags,
not a newer SyncVar ability update.

Bomb placement has a reversible SpriteRenderer-only proxy and per-input stock
reservation. The server selects the permitted prefab asset, validates placement,
spawns the real bomb and confirms the explicit A tick. Rejection/timeout releases
the proxy/virtual footprint; confirmation hands rendering to the spawn while
stock remains reserved until the movement acknowledgement. Dedicated placement
audio suppresses the predicting owner's duplicate. Line casts still use their
existing authority path and do not create speculative gameplay bombs.

Green Louie starts local dash presentation without running its gameplay routine.
Its read-only obstacle probe uses the existing dash collision rules. A revisioned
server event confirms the C tick; movement snapshots correct the displayed path.
Continuation without fresh authority is bounded to 150 ms and cancels on timeout.
The server retains dash collisions, damage, invulnerability and stop decisions.

Motion captures label context at input and response. Run
`python Tools/Diagnostics/analyze_prediction.py --online ONLINE.jsonl --offline OFFLINE.jsonl --output REPORT.json`
for build/context-separated statistics. Blocked or unclassified response samples
are excluded; older captures need recapture for classified comparisons. Observed
frame hitches are not network latency. See `progresso-predicao-online.md` for the
remaining plan and the exact automated/manual validation boundary.

## Pink and Red Louie presentation (protocol 26)

Pink jump geometry is shared with the offline/server landing resolver. The owner
starts a bounded jump presentation on its C edge, with ground position separate
from the sine height. The client never runs the jump coroutine or grants
invulnerability. Confirmation keeps the owner's displayed phase; rejection,
death, mount loss and timeout restore presentation/colliders. A normal early stop
waits for the approved arc to finish. Observers render the approved arc on their
interpolation timeline without taking over NetworkTransform's ground position.
Apply the parent arc before positioning the shadow in world space. Egg followers
still require the separate ownership/presentation work in A11/R07.

Red predicts the owner's punch pose and local motor wait. It does not test hits,
apply stun or expel items. The server confirms the original C tick, the longer
success cooldown and the hit sound. A confirmed pose never restarts at frame zero.
The offline sound is hit feedback, so a speculative punch does not play it. The
server's local audio and the single action RPC avoid duplicating the generic
co-op sound broadcast. Observer punch poses retain authoritative mount frames.

Automated pose/reconciliation tests complement, but do not replace, two-peer
validation of mount transitions, pause, death, dynamic blockers and degraded
networks. See the progress checkpoint for the reports and unfinished plan IDs.

## Conveyor motor composition (protocol 27)

For network-controlled players, Battle Mode 5 / Stage 3-4 / Stage 3-6 conveyor
displacement runs once inside the same input tick as the walking motor. The
arena loop skips those actors and pure clients do not move other players or
bombs or trigger global controls. Offline and COM retain the existing loop.
Pink jump is excluded from conveyor influence while airborne in both paths.

Movement snapshots include the acknowledged conveyor phase and the target cell /
incoming direction needed to finish a corner before turning. Replay restores
that traversal context and does not use a newer global phase. Approved phase
messages update world presentation, including observers. Speed modifiers carry
base speed, multiplier and remaining simulated duration so batched replay does
not depend on the current rendered frame's Time.time. Multiple controllers in
one scene conservatively disable owner prediction; current conveyor scenes have
one controller each. Dynamic creation/reparenting of controllers is not covered.

Motion context now includes conveyor path/phase/traversal; phase changes have
send/apply events. The comparison tool separates captures, players, platforms
and conveyor phases, and a capture without a header cannot inherit the previous
file's build. Automated coverage: 154 Unity tests and 40 Python tests passed for
this milestone. Two-peer validation of all three scenes and degraded networks
remains pending; no measured zero-latency or hitch-free claim follows from tests.

## Tank shot presentation (protocol 28)

The owner starts Tank's pose, sound and a SpriteRenderer-only projectile proxy
on its real C edge, after receiving a confirmed ready state. The proxy has no
TankShot, collider, Rigidbody or NetworkIdentity and advances at most 150 ms
without the authoritative spawn. Confirmation preserves its phase and the
server controls readiness/cooldown. Rejection, missing spawn, death and timeout
clear reversible presentation. Confirmed audio does not duplicate the owner's
predicted sound or the generic co-op sound broadcast.

The real NetworkTankShot carries its launch owner/input tick, geometry and time.
On matching spawn, presentation transfers to the projectile's child visual,
leaving NetworkTransform, root position and collision authority intact. Latest
approved snapshots bound short extrapolation; a read-only sweep respects known
geometry without invoking impact, damage, explosion or bomb-chain callbacks.
Pause keeps the displayed world position even if the buffered root moves. After
a long pause, presentation waits for a fresh approved sample, with a bounded
one-second fallback rather than treating the pause as packet loss immediately.

Diagnostic input context classifies predicted ability poses/motion and pause as
blocked, keeping intentional waits out of free movement latency percentiles.
Validation: 158/158 Unity tests, then 4/4 targeted Tank tests after the final
diagnostic-only adjustment; C# compiled without warnings/errors and 40 Python
tests passed. Manual two-peer checks of directions, nearby/moving obstacles,
cooldown tint, mount changes, pause/death, network gaps and impacts remain pending.

## Black Louie owner dash (introduced in 29, published with 30)

Black predicts only the owner's finite dash/pose and sound. Shared read-only
blocker checks use acknowledged destructible-pass state; they never kick a bomb,
push/stun another actor or grant invulnerability. Movement without fresh authority
is bounded to 150 ms and the validated total range. Server events confirm the C
tick, contact hold and stop; snapshots correct or reject presentation. Readiness
is distinct from cooldown, whose remaining duration is projected to the next
input's estimated server validation time.

The network path writes validated dash positions immediately, keeping root,
Rigidbody and stop snapshots coherent. Offline retains MovePosition. A stop keeps
root presentation until the post-dash snapshot releases the motor; the server
sends that snapshot after restoring the input lock. Afterimages/manual hold do
not start gameplay coroutines on clients. Other actors' pushes remain authority
results and still need the R04 presentation audit.

Validation: 162/162 Unity tests, 40 Python tests and C# compilation passed without
introduced warnings. Two-peer validation is pending. Co-op keeps original scene
colliders disabled; protocol 30 supplies separate approved query geometry.
Black dash and co-op queries were published together in d25c34734 (CI 40281389234).

## Co-op prediction collision geometry (protocol 30)

Pure co-op clients keep original scene scripts, rigidbodies and colliders
disabled. Separate native query shapes receive approved server geometry:
TilemapCollider2D views use plain generated Tile assets, with no renderer,
MonoBehaviour or TileBase Startup gameplay; boxes/circles/capsules mirror
server-approved pose, size, offset, tag, layer and enabled state. Destructible
pass keeps using acknowledged motor capabilities against those tags.

Initial map chunks and collider snapshots must complete before owner prediction
starts. Unknown active shapes or unsupported composites suspend prediction,
while original inputs continue to the server. Scene changes dispose generated
tiles/views and unsubscribe tile notifications. Original scene components never
regain gameplay authority on clients.

Server tile changes and collider/pose changes are captured on the 20 Hz world
tick; periodic tile scans remain as a fallback. Approved Yellow tile-motion
events update query cells immediately, preserving the existing motion blockers.
This is current approved geometry, not speculation about future stage
mechanisms, moving obstacles or destruction. Those trajectories still require
the separate C/R audits and manual two-peer validation.

Validation: 169/169 full online EditMode tests passed, followed by 9/9 co-op
tests after the missing collision-sprite readiness guard. C# compiled with zero
warnings/errors, and 40 Python tests passed. New two-peer captures remain pending.

## Mole startup presentation (protocol 31)

The owner's C edge presents phase 1 immediately through the existing drill
animator, down-facing head offset and reversible sprite suppression. The server
confirms the input tick without restarting elapsed phase; it signals phase-1
completion before enabling the drill's real protection. Subsequent burrow and
emergence frames remain server presentation. The random exit destination is
never guessed and no client drill coroutine, teleport, RNG or invulnerability
is started. Observers continue through authoritative mount frames.

Processed input without a start rejects the gesture. Monotonic revisions reject
stale starts after phase completion. Pause freezes elapsed presentation; bounded
timeout, mount loss, death, disable and phase handoff restore the local pose.
The original directional input and C edge still reach server validation; motor
replay does not activate drilling or play audio again. Owner sound is correlated
with confirmation, while server co-op broadcast is avoided for this gesture.

Only the known preparation is predicted. This deliberately does not infer
underground safety, future collisions or the teleport target from a local scene.

Validation: 171/171 online Unity EditMode tests, 40 Python tests and C#
compilation with zero warnings/errors passed. Manual two-peer checks of phase
handoff, head offset, sound, cancellation and teleport remain pending.

## Tank cooldown boundary (protocol 32)

Tank eligibility is distinct from cooldown readiness. MountActionState confirms
that the ability is enabled, mounted and otherwise available even during its
cooldown. The owner projects remaining cooldown using the movement snapshot's
server timestamp and NetworkTime.predictedTime, the estimated validation time
of the next input. It need not wait for a later ready=true packet at expiry.

Shot confirmation uses that same projected time for the cooldown deadline,
without adding the confirmation's age again. It preserves the already displayed
pose. Processed pending inputs retain rejection/spawn handling, and eligibility
loss continues to block previews even when remaining cooldown is zero. Real
shooting, inventory, impact and damage remain server validated. This is a partial
A14 regression improvement; it does not complete the remaining prediction audit.

Validation: 172/172 full online Unity EditMode tests passed, along with
40 Python tests and C# compilation with zero warnings/errors. Tests cover
projected cooldown expiry without a new ready packet, lost eligibility,
snapshot capture, confirmation/rejection and the existing projectile presenter.
Two-peer cooldown-boundary and pause/resume gameplay checks remain pending.

## LineBomb and Purple grouped placement (protocol 33)

Predicted placement now uses the real input tick, action kind (plant/A-line/C-
Purple) and cell position. Every proxy shares approved inventory reservations,
including confirmed cells awaiting a snapshot. Server spawns confirm each cell
independently; acknowledgement rejects unmatched cells. Ghosts contain only
presentation components and never join Bomb.ActiveBombs, start fuses or mutate
real stock. Known geometry queries stop at water, holes, walls, enemies and
active explosions. The source line origin/seed rules are shared with authority.

Server snapshots include the next placement prefab and continuation prefab.
The latter resolves an already-active owner bomb without spawning one: Power
remains first-only, while Search/Magnet respect their every-placement setting.
The same continuation also applies to pending ordinary placements.

Purple predicts its manual cast pose even with zero bombs, matching offline.
Confirmation preserves phase; rejection, completion, mount loss, death, timeout
and disable restore the pose. Pause freezes it; mount rebind reapplies progress.
Its gameplay Update/coroutine does not run on clients. The server selects every
real placement and the C tick is never confused with the player's A tick. Audio
plays once per group and avoids both owner echo and the generic co-op broadcast.

Validation: 176/176 full online Unity EditMode tests passed. Five additional
geometry-entry tests then passed with no runtime changes between runs. C#
compiled without warnings/errors, Python40 and strict UTF-8 checks passed.
Manual two-peer checks remain pending; no measured latency gain is claimed.

## Controlled detonation audit and diagnostics (wire remains 33)

ControlBomb has no offline cast animation or movement lock to anticipate. The
mobile button already applies its pressed scale/sprite/icon locally in
MobileButton.OnPointerDown. Preserve that immediate input feedback and wait for
authority to select and explode a bomb. No speculative blast or extra cast
was added.

The existing client input_send event records B's real pressed bit/tick. The
server now records control_detonation_validate with CurrentActionBTick, the
selected bomb id, oldest/all/empty and accepted. Tick zero is expected for
paths without a remote input tick. This event confirms selection, not when the
client displayed an explosion; it cannot establish end-to-end explosion
latency on its own. Both public player detonation methods reject pure-client
gameplay before editing the planted list.

Validation: 182/182 full online Unity EditMode tests passed with the authority
guard regression, C# compiled without introduced warnings, and UTF-8 checks
passed. New paired diagnostic captures and manual two-peer checks are pending.
This instrumentation is held for the next gameplay publication.


## Riding and manual dismount presentation (protocol 34)

`NetworkRidingPresentation` extends the existing `NetworkBombFx` owner. Select
starts a reversible dismount arc only with approved eligibility and arc geometry.
The native riding coroutine remains on the authority: clients do not detach a
mount, transfer eggs, grant protection, unlock gameplay or invoke its callbacks.
Mounting itself starts from an approved event, because ownership may be disputed.

The descriptor preserves the offline parabolic, pixel-quantized curve, heights,
facing, start and target. A retained state includes elapsed phase and pause, so a
late observer does not consume a long pause as flight time. Confirmation preserves
the owner's phase; rejection restores the latest authoritative position. Root
presentation survives NetworkTransform and movement reconciliation. Native online
arcs keep Rigidbody and transform coherent before snapshots, without changing the
offline velocity path.

Approved landing recovery bounces wait for the unfinished riding arc and retain
their final stop. Death, cancellation, timeout and a newer motion release the
presenter. Completion bridges to an approved movement snapshot rather than
granting speculative ownership or gameplay unlock. Diagnostic events
`riding_prediction_begin`, `riding_prediction_confirm` and `riding_prediction_end`
include the input tick, correction and endpoint `hold_ms`. Egg queue and detached
mount presentation still require A11/R07 follow-up; this is not all of A11.

The movement history now stores the original directional command sent to the
server. Gesture locks mask only the current motor/replay copy while still active.
After rejection or completion, pending movement can replay its real direction
without firing ability edges again. Tests cover a rejected Red gesture with
walking input retained through the lock. This does not close all A14 regressions.


## Approved egg queue presentation (protocol 35)

`MountEggQueue` continues to own the travelled route, membership, freeze/transfer,
collection and destruction on the authority. `NetworkTransformReliable` continues
to interpolate follower positions; no client queue is constructed or simulated.
`NetworkEggFollower.QueuePresentation` supplies the confirmed facing, moving/idle,
visibility and sorting with a timestamp. A bounded 32-state buffer selects them on
the transform's observer timeline, including its send-interval/timeline offset.
This prevents an interpolated stationary frame from inventing an idle transition
and makes boat suppression, forced hiding and frozen queues visible on clients.

A forced-hidden online follower keeps its identity active so Mirror can spawn it
for a late observer. Its sprites are hidden and its hitboxes disabled; showing it
restores only the hitboxes that were enabled before hiding. Offline retains its
native SetActive lifecycle. Client hiding never disables the network root; client
colliders remain disabled. Once approved destruction starts, its existing VFX owns
the renderers and queue presentation stops overriding them.

The detached `NetworkWorldMount` pickup was also audited: its native inactivity
loop alternates approved emotes and does not move the pickup. Its existing frame
stream and collider-free clone are appropriate for that static case. This does
not establish that every R07 item/skull trajectory is complete, nor measure the
latency of the owner-to-egg trail. Manual two-peer transfer/boat/death tests remain
necessary to judge the resulting gameplay.


## Approved external movement and aligned poses (protocol 36)

A pure owner client normally disables NetworkTransform updates while using input
prediction. Previously an unrecognized authoritative flight therefore moved its
root only on reconciliation packets. `NetworkAuthorityMotion` extends the existing
`NetworkPlayerInput`: a native server routine marks a finite motion scope, and its
movement snapshots opt into continuous presentation while prediction is locked.
The owner consumes the existing, still-decoded NetworkTransform snapshot buffer
on the observer timeline, without toggling the component or resetting its delta
baseline. Reconciliation restores motor context but preserves the displayed path
between packets. Specialized riding/bounce/dash presenters retain priority.

This is interpolation of approved samples, not a client launch coroutine, contact
prediction or a descriptor of the whole future curve. It does not select a target,
run collision outcomes, grant protection or replay action edges. Pause holds the
view. Gaps over 250 ms or sample steps over 1.5 tiles use a cut rather than drawing
a trip across the map; there is no extrapolation. Ending the scope returns to the
existing authoritative reconciliation/replay path. Begin/end diagnostic events
identify these waits as approved presentation. Cannon and arena spring update
the server Transform after their Rigidbody writes so both packet sources agree;
the offline velocity and routine remain on their original path.

The touched entry points are Cannon, SpringLauncher (also used by geysers), arena
4 spring and arena 11 rope preparation/flight. They reject client gameplay starts.
This does not finish C02/C03: complete curve descriptors, some ambient arena visual
phases and manual comparisons remain. Minecart was deliberately left for C04's
own transport, because its cart visual must accompany the rider before client
ride routines can be disabled. Enemy pulls that compose with normal walking need
motor/replay composition; do not apply a locked-flight scope to them blindly.

Player head offsets interpolate between compatible approved pose samples. Mounted
observers now buffer changed frame sets on the transform timeline, including its
send/timeline offset. A mount generation plus mounted type prevents a replacement
from inheriting another mount's buffered pose. Only local offsets interpolate;
frame indices, visibility, colors and flips remain at their approved sample.
The bounded history holds 64 sets and unchanged server poses do not add timestamp
traffic. Predicted owner locomotion and specialized ability poses keep precedence.
Two-peer synchronization, bandwidth and frame hitch captures still need measuring.

## FallingBomb warning and spawn authority (protocol 36)

Arena 2 clients no longer run the local target lottery or instantiate a falling
bomb. `FallingBombTarget` publishes approved target, remaining warning time, fuse,
radius, pause and revision/time at start/stop plus 100 ms refresh while active.
Scene and freshness checks prevent old events from reviving a warning. Clients
only display the warning; reaching its display deadline never spawns a bomb or
triggers damage. The native server bomb now spawns through NetSpawn before its
punch presentation starts, and failed launches despawn consistently. Offline uses
the native lottery, timing and gameplay. Other C10 mechanisms remain separate.


## Minecart presentation and approved portal particles (protocol 37)

Battle arenas 9 and 12 now present the cart itself from `MinecartView`, in addition
to the approved rider movement scope. A client does not board, collide, run the
ride coroutine or destroy a cart from its own sudden-death simulation. The server
sends the selected cart renderer/frame, relative visual position, root position,
direction, loop state and rider identity for timeline alignment. Changed views
sample at 20 Hz; idle views keep a 500 ms heartbeat for late observers. A bounded
64-sample history follows approved rail corners and cuts room jumps or delivery
gaps. Releasing the occupant preserves the final buffered pose and timeline offset.
The cart's sink/rise also preserves approved scale and local offsets between compatible render samples.
No rider ownership, invulnerability, item effect or collision is granted locally.

The existing co-op world stream continues to draw Stage_3-2 carts; their riders use
the same approved movement scope. Original disabled co-op controllers are not
reactivated. Battle cart loops follow approved state, and their initial playback
is not duplicated through the generic one-shot sound broadcast. Enter/exit sounds
retain the existing server sound event. Player animation now replicates sorting
with pose time; predicted owners accept only that confirmed ordering while keeping
their immediate local frame, so head ordering can recover after leaving a cart.

Cart portal stars also receive approved visual descriptors. The server keeps its
original RNG draws and publishes the chosen sprite index, pose, scale, drift,
spin and timing. Clients create Transform + SpriteRenderer only and reuse the
native visual curve without gameplay RNG, physics, NetworkIdentity or ownership.
Revisioned clear events cancel old queued particles; disable releases their
coroutines and visuals. This is separate from damage/teleport eligibility. The
BattleMode_7 portal, other swaps and fixed-mask/sink behavior remain under C05.

Authored controller references were checked in BattleMode_9, BattleMode_12 and the
three Stage_3-2 shuttles; no scene/prefab wiring was changed. Manual two-peer entry,
exit, portal, pause, collision and loop-audio comparisons are still required before
claiming offline parity. Boarding prediction is not introduced by this transport.


## Portal renderer poses and masks (protocol 38)

`NetworkVisualPose` captures position, rotation, scale and mask interaction relative
to the replicated actor. It covers direct sprite transforms and inherited mount
offsets that animation runtime offsets alone miss. Apply only to presentation
children; a root renderer cannot move or scale its gameplay actor. Compatible
approved samples interpolate transform pose, keeping mask/frame discrete. Owner
authority scopes capture and restore original visual transforms before returning
to predicted walking; mount replacement restores/rebinds this baseline.

Portal7 native player and bomb entry points reject pure-client gameplay. Its
server routine marks approved owner motion and synchronizes Transform after body
writes. The existing short-segment presenter cuts cross-room jumps. Clients do
not select destinations, lock colliders, grant invulnerability or punch bombs.
Hidden sprite renderers now produce the absent player-animation index. Approved
star descriptors share `PortalStarView` with cart portals; Battle clients render
only sprite particles, while co-op retains its existing world sprite transport.

Stage3-4 fixed portal masks have a bounded, scene/revision/time-checked full
snapshot stream. Client views contain only SpriteMask and Transform. Empty
snapshots remove prior views on the approved timeline; pause freezes playback.
Co-op world sprites also carry their approved mask interaction. Native sink/rise
restores its visual baseline/masks on iterator disposal and explicit controller
disable, without teleporting an interrupted actor back to the portal.

This is approved presentation, not speculative portal entry. Manual comparisons
of entry/exit, mounted/held-bomb cases, audio, pause and packet delay remain.
`SwapGroundTileBlackoutHandler` swaps terrain and blackout rather than a player;
continue auditing it with C09. Do not mark the whole C05 plan complete yet.

Validation:221/221 OnlineRoom Unity tests passed in the isolated runner, including
relative hierarchy poses, hidden renderers, approved masks, particle authority and
cleanup through iterator disposal/controller disable. C#0 new warnings/errors;
Python40/40, strict UTF-8/BOM and diff checks passed. Manual two-peer comparison
is still pending.


## Water currents and platform riders (protocol 39)

WaterCycleCurrent uses the approved owner-motion scope during its native locked
push. Entry points reject pure clients; they never choose contact, obstacles,
hazard results or a stop. Pauses freeze progression and explicit disable/disposal
restores the previous lock/override/hazard flags. Online remote player steps write
body and transform consistently; offline keeps native MovePosition. The existing
server input path still handles braking; input is not replaced by the presenter.

The native exit barrier is also part of NetworkMovementState. Owner motor/replay
queries and releases its own approved copy. Reconciliation restores that copy;
predicted retreat cannot remove a barrier from the server's static dictionary.
Without that state, resuming normal prediction at a blocked exit would repeatedly
walk forward and be corrected even if the push itself looked smooth.

FloatingPlatform holds an approved presentation scope for each rider, disposing
it on confirmed unmount/disable. Centered riders suspend walking prediction even
at a stop: native LateUpdate owns their pose there. This does not lock real inputs
or change offline boarding. Unmount applies the final destination before forcing
reconciliation. Client native transport/boarding routines are gated and pause
freezes platform simulation. Existing co-op sprite views present the support.

World co-op poses and animation playback now use the local player's transform
send/timeline offset, so support and rider sample the same server time. Bombs
retain their own identity's offset. This is not boat prediction: BoatRideZone's
water-tag collision permission, owner head-only pose and support projection still
need approved context and a dedicated presentation path before enabling it.

No manual two-peer, braking/pause, multiple-rider or water-boundary comparisons
have been completed for this batch. Final Unity suite227/227 passed; C#0 new
warnings/errors, Python40/40, strict UTF-8/BOM and diff checks passed. Reports:
Logs/PredictionWaterPlatform39FinalTests.xml and .log.


## Magnetic field authority (protocol 40)

Native Bomb.StartKick, TryRedirectKick and StartMagnetPull now reject pure-client
simulation at their entry points. Client prediction keeps using the existing
render-only previews/presenter; it never calls these gameplay starts. Arena6's
OnBombAt likewise cannot redirect a real client bomb.

MagnetIndestructibleTileHandler no longer scans/pulls or rotates a tile because
of a local explosion on a client. Startup reads the authored/approved direction
without resetting the tile. Battle14 receives MagnetFieldPhase with scene/path,
revision/time and bounded cell/direction arrays. Fields are polled at10Hz, sent immediately on rotation
and refreshed with an idle500ms heartbeat; received phases buffer up to32 and apply on the
player transform timeline, preserving pause. A buffered phase cannot replace a
confirmed sudden-death stone with a magnet. Stage2-1 keeps its co-op tile stream.

This does not predict target selection or extend a pull beyond approved motion
segments. Bombs without a live presentation owner remain part of R07's lifecycle
work, and two-peer magnetic-field/chain-reaction comparisons are still pending.


Validation40: the final combined run passed321/321 Unity cases (OnlineRoom229 and
COM92). The COM definitions stay in their runtime assembly to access internal
helpers, with an abstract base and a concrete Editor discovery wrapper. Fixtures
now supply concrete colliders, initialize Edit Mode caches/input profiles and
activate the tilemap used by native coordinate queries; all original assertions
remain. Full CI selects both suites, while quick still builds without running
tests. C#0 new warnings/errors, Python40/40 and UTF-8/diff checks passed.


## Confirmed bomb motion outlives an inactive owner (protocol 41)

NetworkBombMotionLifetime is a presentation helper on the existing manager,
ordered200 after NetworkTransform. A pure-client owner whose identity remains
spawned retains confirmed bomb playback while cancellation rolls back unconfirmed
commands and clears player ability/placement poses. The helper ticks only the
extracted bomb-motion loop, never the player's input, abilities or gameplay.
Inactive owners do not predict another endpoint or begin a new local ability pose.

Reactivation delegates to the original LateUpdate. A frame guard prevents a second
presentation advance if deactivation followed that LateUpdate. Scene clear,
OnStopClient, identity removal and destruction release playback/captured collider
state. Integration extends the existing OnStartClient in Riding presentation.
Opt-in diagnostics record bomb_motion_owner_inactive/owner_reactivated.

Final validation:323/323 combined Unity cases passed (OnlineRoom231 + COM92),
C#0 new warnings/errors, Python40/40 and strict UTF-8/BOM/diff checks. Tests include
pending-command rollback, confirmed movement, pause, ordered stop, disconnect and
identity-removal cleanup while a bomb still has a captured collider. Manual peers
and reactivation timing remain to compare. Ownerless bomb transport and explosion
VFX scheduled on an inactive owner remain separate R07 work; do not claim them
solved by retaining motion alone.

## Received explosion VFX outlives its owner (protocol 42)

RpcPlay now schedules an approved visual wait on the existing manager's active
NetworkBombMotionLifetime helper. It captures the explosion prefab and event data,
not the BombController or NetworkBombFx, so an already-received event survives
owner deactivation or destruction. An active owner is only a fallback when the
manager helper cannot host it; an inactive owner is never a coroutine host.

Scene/client cleanup cancels the helper's visual waits; disconnect also ends the
wait. The shared static visual spawn keeps explosion collision disabled and does
not calculate damage, terrain changes or chain reactions. Hosts keep native VFX.
This does not transport an event emitted without an owner or recover an RPC whose
endpoint disappeared before reception. Those cases and manual parity remain R07.

Validation:325/325 combined Unity cases passed (OnlineRoom233 + COM92), C#0
warnings/errors, Python40/40, UTF-8/BOM and diff checks. New cases exercise active
host selection, inactive fallback rejection, host exclusion, owner destruction
during the wait and disconnect cancellation. Reports:Logs/PredictionBombVisual42Tests.*.

## Approved arena roof and powder frames (protocol 43)

BattleMode8RoofController no longer resets or advances its native roof clock on
a pure client. RoofTileState carries a complete bounded snapshot of decorative
cells, using indices into the existing authored swaps and explicit -1 clears.
The authority publishes each transition plus a500ms heartbeat for late observers.
The eight actual roof assets are plain Tiles with no collider or spawned prefab;
the original controller remains in place and no scene wiring changes are needed.

BattleMode3PowderTrailController publishes approved frame/end events with the same
heartbeat. Clients present its existing generated, collider-free tiles and restore
the original floor/matrices at the approved end or disable. Local explosion hooks,
Ignite, its coroutine and SpawnHitboxes reject client gameplay. Client presentation
does not set native ignitionRunning, grant damage or choose a chain reaction.

Both histories are revisioned, limited to32 samples, scoped by scene/path and
sampled on the player's transform timeline. Future frames wait; pause freezes
presentation; malformed or stale messages are rejected. This is presentation of
confirmed phases, not speculative activation. SuddenDeath's timeline/authority
audit and manual comparisons remain C10 work. Final validation passed332/332
Unity cases (OnlineRoom240 + COM92), C#0 warnings/errors and Python40/40, plus
strict UTF-8/BOM, metadata and diff checks. Reports:Logs/PredictionRoofPowder43Tests.*.

## SuddenDeath's approved path and audio wait (protocol 44)

The pure client copies the received closing path without first drawing a local
pattern, corner or rotation. Native Revenge cleanup remains on the authority.
Incomplete/oversized begin payloads and invalid timing are rejected before side
effects; a drop requires the approved path to have started and contain its cell.

The authority still selects and plays its voice in the original audio coroutine,
then publishes the begin event with that coroutine's music wait in BattleFeedback.
Clients use the existing approved sound event and this duration, rather than
drawing another voice to calculate their wait. Native offline/host RNG ordering
is preserved. Items retain their existing feedback semantics; the new field is
default zero for their messages. Both peers require protocol44.

This is not the complete C10 drop timeline. Begin/drop timestamps, late observers,
landing outcomes and authoritative terrain commit still need separate work and
manual validation. Do not infer that every approved fall-start already confirms
all later terrain decisions. Final validation:335/335 Unity cases (OnlineRoom243
+ COM92), C#0 new warnings/errors, Python40/40 and strict UTF-8/BOM/diff checks.
Reports:Logs/PredictionSuddenDeath44Tests.*. Opt-in diagnostics now record
sudden_death_begin (stable path hash/count and approved music wait) and
sudden_death_drop_begin/finished (cell/position, initial duration). These are
instrumentation for future captures, not a measured latency gain. The per-ID
manual matrix remains unexecuted.

## Boat owner movement and presentation (protocol 45)

After the authority confirms a BoatRideZone attachment, walking keeps using the
owner's existing movement motor and input replay. NetworkMovementState now carries
the approved tagged-obstacle permission and tag; all four motor queries use that
historical context inside predicted ticks. Reconciliation does not mutate the
client's native water permission, occupancy, ignored collision pairs or protection.

The boat descriptor identifies the authored definition by cached hierarchy/local
position, and carries server renderer IDs, follow/head offsets and render flags.
The key is cached before native anchoring/following; a moving boat does not change
its definition key. Missing, ambiguous or malformed definitions suspend boat
prediction and keep authority. An optional codec writes only one presence byte
for a player outside a boat, rather than sending empty poses on every snapshot.

NetworkBombFx owns the local boat view: only SpriteRenderer/AnimatedSpriteRenderer
children, using the existing authored clips/materials and the current player's
head renderers. Position follows the predicted Rigidbody; direction changes do
not wait for a new server frame. Native body suppression/protection are not granted
by this view. The head and mount network presenters yield their visual ownership
while the boat view is active; mounted sprites/shadow remain hidden as offline.

Co-op stream suppression uses the server's approved renderer IDs, not client
GetEntityId values. Claims have an owner, so cleanup of an older presenter cannot
unhide a newer one. Releasing a claim restores the last approved visibility
immediately. Observers keep the existing approved co-op stream. Pause preserves
the presented root, direction, idle and animation phase; death, source removal,
scene/client stop and confirmed detach release the projection. Approved continuous
transport scopes, including portals, take over through their existing presenters.

Native client anchoring/following/mounting and zone triggers are gated before a
snap or gameplay side effect. Native offline/host behavior remains; server-only
reconciliation is guarded by an initialized server identity. The unmount zone
forces reconciliation after its final snap, not halfway through the transition.
No scene/prefab changes were needed; RedBoat, Submarine and authored BoatRideZone
references remain the existing integration points.

Diagnostics label boat=True/False from native authority or acknowledged client
context. The comparator separates boat and boat_mounted from walking/mounted,
uses the context at input and excludes boat_context_pending from free input
latency. Older captures without this flag cannot isolate boat movement.

Final validation:347/347 Unity (OnlineRoom255 + COM92), C#0 new warnings/errors,
Python42/42 and UTF-8/BOM/meta/diff checks. Reports:Logs/PredictionBoat45DiagnosticsTests.*.
Earlier342 and346 reports were retained before the diagnostics extension. New
cases cover conflicting native versus historical water permissions, offline/host
and client gates, owner direction/root/pause, pure visual structure, source loss,
stop cleanup, wire presence/roundtrip, claim transfer and NPA/NMA integration.
The per-ID manual matrix, real two-peer captures and a measured latency gain
remain pending; automated cases do not prove offline-identical feel in every scene.


## C07 foundation: world-clock push and diagnostics (protocol45)

Pusher contact still uses its native world FixedUpdate and pending MovePosition.
A remote player's zero-to-four input catch-up ticks run before that one force.
Applying the push inside every server motor tick would multiply it; writing its
position immediately would change the geometry seen by a second pusher before
physics. The cast now deduplicates attached bodies, so multiple player shapes
produce one push target and one resolver notification. Axis, normal/overlap
filters, patrol phase and offline last-target semantics are retained. Native
entry points reject pure clients and pause. No client world simulation is enabled.

Movement snapshots no longer discard an authoritative position change merely
because its input acknowledgement repeats. The unchanged-position duplicate
optimization and30Hz scheduling remain. This covers pusher motion, its later
resolver snap and other world forces without introducing a new wire structure.
The client restores the approved state and replays its existing pending inputs.
This does NOT yet replay a speculative pusher force or predict its contact.

Optional motion capture distinguishes pusher_step (source, world step, requested
delta), pusher_resolve_grid_snap (actual native correction before hole/blocked
checks), movement_world_snapshot (position changed with same acknowledgement)
and movement_reconcile (approved position, raw error and visible root correction
after replay). Events are sampled/rate-limited, so their counts are not every
physics contact. Correlate build, scene, player/netId and authoritative input tick;
realtime clocks on different machines cannot be subtracted as round-trip delay.
The existing comparator counts these event kinds, without claiming pusher latency
percentiles from them. New matching captures and two-peer testing remain pending.

The next C07 implementation must carry approved contact/phase into a separate
owner history, bound continuation before endpoint/reversal and restore it on
replay without advancing the global source. The native resolver first snaps to
the grid before checking whether bounce/hole recovery is required; this is a
separate authoritative transition and must not be replaced by a free-flight lock.

Validation for this foundation:359/359 Unity (OnlineRoom267 + COM92), including12
pusher cases; C#0 new errors/warnings, Python42/42, UTF-8/BOM/meta/diff checks.
The focused fixture verifies actual native cast contact before asserting motion,
advances Physics2D explicitly, restores its simulation mode and covers the authored
kinematic circle as well as multiple shapes. Coordinates710 are used: the initial
71000-unit cast fixture found no contacts and its failed reports were retained.
Final reports:Logs/PredictionPusherFinal45Tests.* and PredictionPusherFocused45Tests.*.
No scene, prefab or player body-type change was made. C07 remains partial.


## C07 confirmed owner continuation (protocol46, locally validated)

Native pusher contact records an authority-only receipt for the current world
physics step. Its pending MovePosition stays native. Owner movement snapshots now
carry an optional aligned, unrounded Box descriptor: post-physics source bounds,
velocity, filters, source identity, remaining phase and an approved safe cell.
Absence costs one byte. More than one force source, missing/disabled source,
unsupported shape and an expired world receipt publish no continuation.

Only the owner composes this force after its motor/conveyor tick. It casts the
approved source shape against its own actual body, preserving normal/overlap
filters and walking. It never moves the native source or calls recovery/damage.
Reconciliation restores that owner's source phase/budget before input replay.
The server does not apply this context in its zero-to-four input catch-up steps:
its autonomous source already pushes once in world FixedUpdate.

Continuation stops on lost contact, unknown/blocked exit, hole, safe-cell boundary,
phase endpoint or150ms budget. It does not invent a reversal or another contact.
Source phase advances only with simulated owner ticks, never again from packet
age. Old/future confirmations beyond150ms of estimated server-now cannot restart
it. Pause freezes it; an approved clear, bounce lock, death or network shutdown
releases it. Contact heartbeats and clears are sent even with unchanged input ack
and root position. Observers retain approved world/player interpolation.

The native grid snap and recovery arc remain authoritative. Horizontal walking
can recenter its perpendicular row; force tests compare with the same no-force
motor step instead of assuming total vertical displacement equals the push.
Input remains free. Same-cell/phase limits intentionally fall back to authority
for uncertainty; this is not whole-world deterministic physics rollback.

Validation:378/378 Unity (OnlineRoom286 + COM92), C#0 new warnings/errors,
Python43/43 (including pusher motion classification), UTF-8/BOM/meta/diff checks.
Reports:Logs/PredictionPusher46FinalTests.*. Manual two-peer feel, latency captures
and performance measurements remain pending; no zero-latency claim is made.


## R04 approved enemy forces (protocol46)

Hurricane, Magnet and Submarine Bubble record the native pending force target;
only a matching physics result and current world-step receipt can publish owner
continuation. The three audited sources expose read-only recipes through
INetworkEnemyForceSource. Their AI, target selection, randomness, capture, native
coroutines and damage remain server-only. Offline/host/COM keep their native force.
Source callbacks do not reapply displacement during the server's input batch.

Movement snapshots add a compact optional recipe: source/kind, scene, anchor or
direction, speed, remaining phase/distance, corridor/range and safe cell. The owner
composes it after walking/conveyor, restoring its phase and budget before replay.
Known boundaries, holes, lost alignment, source ambiguity, a different scene,
phase end and150ms budget revoke it. A pusher plus an enemy never sum forecasts.
Receipts are scoped to the current physics step and stops/heartbeats are sent even
without a changed input ack or position. Null scene is invalid; an empty untitled
Editor scene name is allowed only when it matches the active scene exactly.

Hurricane uses the same radial flee predicate as offline, including a sideways
step with positive outward dot. Its confirmed phase may project the historical
half-speed modifier after the motor; it never edits native stats during replay.
A receipt that only confirmed slow cannot grant a new pull when input releases.
Adjacent known active frames can bridge as one phase; another cycle/AI decision
requires approval. Magnet preserves its approved ray; Bubble clips both phase and
remaining one-tile distance. Native Bubble pause does not spend push distance.

Pure clients cannot start these native force/attack paths or draw bubble RNG.
Queries in Hurricane/Magnet reuse lists with explicit clears, retaining all hits
and body deduplication rather than a fixed collider cap. This removes recurring
allocation sites, not a measured claim of zero GC or zero input latency.

Validation:423/423 Unity(OnlineRoom331+COM92), C#0 new errors/warnings,
Python44/44, UTF-8/BOM/meta/diff checks. Reports:Logs/PredictionEnemyForce46FinalTests.*.
Coverage includes all three native/owner paths, actual physics commit, input
catch-up, walking/flee, replay/RNG, pause, walls/holes, scene/stale phase, source
conflicts, heartbeat/clear, time/cell budgets and22shapes across two bodies.
Matching real captures and the two-peer matrix remain pending.

## C03: deformação aprovada da corda (protocolo 47)

BattleMode11ImpulseRopeController publica snapshots completos dos frames de
repouso, preparação e impulso em LateUpdate, após as coroutines nativas. Deltas
visuais são enviados quando o conteúdo muda; heartbeat de 500 ms atende observadores
que chegam depois. O histórico de 64 snapshots usa a timeline do transform do
jogador, com revisão, cena e caminho do controller. Pausa congela apresentação;
clear vazio desfaz a camada, e mensagens antigas não podem ressuscitá-la.

O client não chama StartRopeDeformation nem a rotina de impulso ao tocar a corda.
O caminho de movimento existente continua usando as posições aprovadas e as poses
do servidor. Não há antecipação de contato, escolha local do lançamento nem
rollback de ativação concorrente. O destino interrompido por obstáculo continua
autoritativo; esta mudança não envia uma curva futura completa do jogador.

Os 32 assets de tile referenciados na cena foram auditados: sem prefab e com
collider de sprite. Por isso os frames usam Tiles simples gerados, sem prefab ou
collider, numa Tilemap de apresentação. A tilemap original mantém o tile e seu
collider: só a cor da célula fica oculta durante a sobreposição. Cor e flags são
restauradas no repouso/clear/disable. Substituição por Sudden Death remove a
sobreposição imediatamente, sem recriar o tile antigo. Nenhuma cena foi editada.

## R05 / C02: renderers curtos e limite de lift (47)

CoopTrackRenderer insere renderers plain na fila de captura20 Hz; as factories
registram o renderer configurado. Isso evita a descoberta250 ms, mas efeitos que
vivam menos que o intervalo50 ms ainda precisam de evento/descritor próprio. Não
se deve reduzir a descoberta global nem ativar gameplay client para fazê-los surgir.

Battle4 tem lift20 tiles em100 ms. NetworkVisualPose carrega offsetLimit da
AuthorityMotionScope: valor padrão4, finito e no máximo128. NPA e NMA interpolam
offset/pose entre amostras compatíveis até esse limite aprovado, inclusive na
fronteira de retorno ao repouso. O root continua com seu corte de teleport/gap,
sem aprovação de direção/destino/hazard. Isso trata o degrau causado pelo limite
fixo4; ainda não transporta a curva futura inteira nem mede ganho de latência.

## C09 /48: caldeira e blackout aprovados

Boiler usa BombMotionKind.Environment para seu puxão nativo80ms. O client apresenta
um segmento recebido antes de seu stop, sem inventar próxima célula ou bounce Rubber,
sem pose de habilidade e sem alterar o collider. Captura, fuse, ignição e explosão
continuam nativos no servidor. Finally/disable limpam o presenter quando há endpoint
válido; owner removido ainda depende da auditoria R07.

BlackoutView leva alpha,36 spotlights lógicos,16 room lights, visão e roomActive.
O coordenador inclui componentes inativos e apresenta na timeline aprovada. A
inicialização visual é idempotente e pode reativar o próprio Image GO. Fading mantém
o unscaled do offline durante pausa; nunca inicia timer de gameplay client. O mesh
render-only usa matemática Box no client e mantém bounds/OverlapPoint offline.

CoopSpriteFrame.blackout é opcional e contém receita do material de olhos, paleta8,
região e oclusão de barrel. Cada view possui clone isolado; não há fallback que
revele o sprite inteiro se faltar template/occluder. Material/cópias saem em remoção,
clear e troca de papel. Arrays8 são enviados desde a primeira configuração, evitando
o limite de crescimento da API Unity. Templates World3 já constam dos catálogos.
Nenhuma cena/prefab foi editada. Provas de renderização e custo real permanecem na
matriz manual; testes de estado/material não provam a imagem final no jogo.

## R03/49: famílias prioritárias de salto

Frog/Hop/SnowMan/Korobokkuru continuam IA servidor. Suas poses aprovadas carregam
limite1.5tile para interpolação de amostras próximas; wrap e gap>250ms são cortes.
Sombras e ondas/gotas compartilham timeline e perfil (inclusive máscaras de olhos).
Pausa mantém a pose/frame já exibidos; não bloqueia recebimento nem clear. O perfil
não se aplica indiscriminadamente a projéteis/bosses de alta velocidade. Valores
atuais foram auditados nos prefabs; configurações futuras de arco maior exigem
revisão. Não prevê alvo, colisão, morte ou nova direção de NPC.


## Revenge Bomber: transporte da bomba (protocolo50)

LaunchRevengeBomb só executa em autoridade/offline e registra o spawn antes do
primeiro StartPunch. O motion kind Revenge reaproveita a curva nativa e a fila
ordenada de arcos/stop, inclusive quando o owner já está desativado; não inicia
pose de BombPunch/PowerGlove nem predição de retorno, continuação ou Rubber.
Falha após spawn usa despawn replicado. A compatibilidade exige client/server50.
Isso corrige o caminho da bomba; não implementa input/transporte do carrinho ou
reativação do jogador. A12 permanece parcial e requer validação real entre peers.


## R06/R08: proteção das entradas nativas (protocolo50, lote seguinte)

SunMask/ClownMask não iniciam movimento, reação, morte ou intro pelo OnEnable
no client puro. FreezerVenus mantém combate, seleção de ataques, movimento,
transformação e contato no servidor; Mecha não inicia sua sequência nativa.
Heart/Star não escolhem alvo/orbit, lifetime ou dano no client. Heart também não
muda a matriz global Physics2D.IgnoreLayerCollision. Offline/host conservam as
regras e chamadas RNG nativas. Star registra seus renderers plain prontamente;
Freezer registra corpo/sombra no coordenador existente.

RocketPenguin mantém arbitragem/target/reload no servidor; SnowBall não avança
sweep e o barril não consome pilares nem inicia queda no client. Seus visuais
permanecem no stream aprovado existente. Não se prevê terreno ou contato futuro.

19 testes focados passaram, com corpo físico ativo separado na fixture e estado
inicial assertado. Isso não cobre todos os bosses, máscaras de intro, áudio em loop,
curvas completas ou sensação/render/custo entre peers; R06/R08 continuam parciais.


## A12: carrinho Revenge e retorno confirmado (protocolo51, publicado em b6a886569)

O manager transporta input mesmo com o player eliminado/inativo. Comandos levam
cena/id/generation/tick; servidor valida slot atribuído pela conexão e identity.
Fila8 usa créditos do relógio de física e não de pacotes, normaliza máscaras e
trata wrap. Sem pacote, a carga pode manter seu relógio nativo mas não há passo
extra de movimento; atraso de150ms/overflow cancela carga sem inventar release.
Host/COM/offline mantêm o Update nativo. Client usa hardware1 para seu P1-P6.

O motor nativo compartilha seleção de parede, distâncias, boost e8segmentos com
a predição. Replay guarda dt por tick (história128), sem executar carga/bomba/som.
A fração renderizada responde antes da próxima confirmação; previsão para após
250ms sem aprovação. Roster20Hz (vazio500ms, mudanças acordam envio) tem cena/revisão,
owner/id/generation e poses/sorting/frames/indicadores aprovados; o clone não tem
simulação/colisores. Fonte e destino das poses usam o relógio do transform. Não há
extrapolação de decisões de lançamento, transferência ou elegibilidade.

Durante pausa, caminhada/frames já apresentados ficam imóveis. Entrada/saída
nativas continuam unscaled, assim como o offset visual aprovado do recoil. Cursor
de pouso fica na posição mundial confirmada, separado do root previsto. Som de
lançamento é confirmado por serial e omitido ao criar uma view inicial/late join.
Muzzle e preview de carga/recoil/aim ainda precisam auditoria/antecipação própria.

ServerBeginRevengeReturn abre fase de apresentação: reativa clone/corpo visual,
mantém life0/isDead e bloqueia input/collider. Riding aceita somente arco confirmado
nessa fase. O fim envia stats/posição/ack, limpa input/poses antigos e aplica o estado
sem LoadInto da base local. Eliminação é resetada somente pela autoridade, e o
victim do swap também recebe eliminação explícita. O término é enviado antes do
bounce seguinte. Bombas aprovadas do owner continuam sendo apresentadas.

Diagnostics têm revenge_cart_authority (servidor) e revenge_cart_present (depois da
apresentação client), até10Hz, com cart/gen/owner/tick/ack/pending/posição/idade.
Esses samples não medem sozinhos latência desde o hardware; o comparador ainda não
calcula percentis específicos de Revenge. Requer capturas e validação entre peers.

25 focados passaram. A primeira suíte500 revelou envio do roster ao host, que não
registra views remotas; o envio agora exclui localConnection. O UnityTest real de
duas rodadas passou após o fix. Suíte completa final500/500 passou, incluindo budget pausado sem créditos e
velocidade confirmada no retorno. A12 segue parcial; publicação ainda separada.


Publicação51 confirmada no CI40558343289 (completed/success), client/server/updater.
Validação final500/500 inclui velocidade do retorno aplicada antes da predição e
pause do budget sem crédito acumulado nem release de A fabricado. Não substitui
manual de dois peers nem encerra os previews visuais pendentes de A12.


### Correções de Battle Mode após protocolo52

A máscara compacta de RevengeCartInput é direções1/2/4/8, A16 e C64.
Não usar `1 << (int)PlayerAction`: Start ocupa o ordinal4 e desloca as ações.
O sampler nativo e o cliente usam NetworkCartActionMask; o replay continua apenas
motor. Inputs aceitos não vazios renovam atividade da sala; heartbeat não renova.
A carga, alcance, indicador e disparo permanecem aprovados pelo servidor.

RidingArcActive controla o arco do jogador. Não deve bloquear os frames da
montaria no chão em NetworkMountAnimation, inclusive do owner; durante esse arco
não substituir os frames aprovados por locomotion local. Outros presenters que
realmente possuem a montaria (boat/dash/bounce/Pink) preservam suas guardas.

Um novo segmento reto aprovado de Kick retém o progresso exibido dentro dele,
limitado150ms e ao endpoint. Isso evita reduzir a velocidade visual ao decair
um offset longitudinal; não prevê novo trecho, colisão, troca de direção nem
um primeiro lançamento sem predição anterior. A validação subjetiva entre peers
continua separada dos testes dos helpers.


### Lote55: continuidade aprovada e diagnóstico de custo (local)

NetworkItemMotion identifica cena/netId/revisão e entrega segmentos de rolagem
ou poses de push. ItemPickup bloqueia as entradas nativas no client e apresenta
SmoothStep/rotação, wrap discreto e paradas em ordem, com fila32. A pausa congela
progresso; peers tardios recebem a fase aprovada. Não altera colisores, landing,
coleta nem RNG. NetworkWorldMount guarda root e frames na mesma amostra, com
histórico32 e corte de descontinuidade; a montaria exibida acompanha o mesmo tempo.

WorldBombMotion usa o manager quando o endpoint do ator original não existe.
Cache256 e retry limitado cobrem ordem de spawn/evento e observador tardio. O owner
remoto de Kick mantém Target/predição, sem receber a duplicata de observador.
worldMotion não herda inputTick/lead/pose de outra ação nem concede habilidade.
A fonte precisa existir apenas para aprovar a ação; o presenter atual pode mudar.

A11 adia somente a remoção visual de uma montaria durante o arco de saída de um
rider vivo. Estado, morte e propriedade continuam imediatos e autoritativos.
A12 não devolve o motor previsto antes do término da fase de entrada/saída já
apresentada. Isso evita pular o final do histórico visual.

coop_family_cost agrega CPU de captura e bytes serializados de frames por família
somente com diagnóstico habilitado. Não mede custo GPU nem pacote completo: exclui
envelopes, definições, fanout por peer, transporte e overhead do diagnóstico.
analyze_prediction separa esse contexto das latências de ações. Capturas reais
co-op ainda são necessárias; o smoke headless Battle não comprova custo ou fluidez.

O primeiro smoke55 com servidor e dois clientes reais apresentou Skull/LineBomb e
confirmou desmontagem Mole/Tank em ambos, mas falhou no encerramento: singleton
Unity destruído ainda passava no pattern C#, e callbacks tentavam enviar RPC após
NetworkServer parar. Guards corrigidos; nova suíte e repetição em andamento.
Não confundir esse resultado com validação gráfica/manual ou deploy do55.


R07: não usar apenas connectionId para excluir owner ou marcar revisão entregue.
Uma reconexão pode reutilizar esse id: o cache guarda a instância da conexão e
remove peers desconectados. Isso permite recuperar a trajetória sem reenviar
heartbeats idênticos aos clientes ainda conectados. Item também captura/restaura
a ordem elevada e flips nativos durante roll/push, sem mudar o collider.

Validação de integração55: Run2 de Battle com2clientes reais passou por2cargas da
arena e encerramento, sem erros/avisos. A execução é headless e usa atores duráveis
de Development; não é comparação de fluidez com offline nem aprovação gráfica.
