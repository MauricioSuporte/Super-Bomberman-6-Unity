# Netcode — Arquitetura-alvo e Roadmap (Battle Mode online, Mirror, host-autoritativo)

Documento consolidado a partir de uma auditoria de 7 frentes do projeto
(`Assets/Scripts`, ~181k linhas). Escopo primário: **Battle Mode PvP online**.
Co-op de campanha (inimigos/bosses/IA) é tratado só como "fase futura".

> TL;DR — O núcleo de rede (conexão, spawn, input sintético, física no host,
> replicação de posição) **funciona**. O que quebra é a estratégia de replicar
> o **estado visual/gameplay**. A correção não é mais um patch pontual: é uma
> **virada de padrão** + aplicação sistemática de um "gate" de simulação e a
> migração do estado por-player (hoje `static`) para estado replicado.

---

## 1. O erro de fundo e o princípio-alvo

**Hoje (frágil):** o cliente **RECONSTRói** o estado visual a partir de poucos
dados sincronizados (posição + `faceDir` + `moving`) e reexecuta um pedaço da
lógica do `MovementController`. Isso só cobre 4 estados de locomoção; os >15
estados restantes (montaria, transição de montaria, morte normal/explosão/
buraco, endstage, cheering, cornered, afk, stun, skull, punch, dash, carry
bomb, preparing, loopup, ball, bouncing) **não têm gatilho no cliente** ou
disparam errados. Além disso, vários drivers locais de animação
(`InactivityAnimation`, `CorneredAnimation`, `StunReceiver`, animators de
mount) rodam **sem gate** no cliente e disparam animações espúrias → o dessync
observado.

**Alvo (robusto): REPLICAR A SAÍDA AUTORITATIVA, não reconstruir.**
O host simula tudo; o cliente é um "terminal burro" que aplica o estado que o
host manda. Para animação isso significa sincronizar **qual renderer está ativo
+ flags (idle/flip/loop) + frame + skin**, não "facing+moving". Cobre todos os
estados de uma vez, porque replica o *resultado* e não a *derivação*.

---

## 2. Os três mecanismos de replicação (como decidir)

| Categoria de estado | Mecanismo | Exemplos |
|---|---|---|
| Objetos de simulação criados em runtime | `NetworkIdentity` + `NetworkServer.Spawn` (via `NetSpawn.Server`) | bombas, itens revelados, Louies/ovos de mundo, projéteis (TankShot) |
| Estado contínuo por-entidade | `[SyncVar]` (ou SyncList) | vidas, nº bombas, raio, velocidade, flags de ability (bitmask), `MountedType`, renderer ativo+frame, skin id, timer de round, placar, players ativos |
| Eventos pontuais / mutações de mundo não-replicáveis | `ClientRpc` (ou evento) | destruição de célula de Tilemap, quedas do Sudden Death, teleporte de portal, resultado de arremessos (mola/rope/minecart), visual de explosão |

Regra da explosão (caso especial): `BombExplosion` é **hitbox de dano + visual
na mesma entidade** e usa **pool estático** incompatível com o ciclo do Mirror.
→ **Não replicar o GameObject.** Manter o dano só no host (colisão) e recriar o
**visual** no cliente via `ClientRpc` (posição, parte, direção, raio, pierce),
reusando o pool local.

---

## 3. O "gate" de simulação precisa ser sistemático

Hoje só consultam `NetSync.ShouldSimulateLocally`: `MovementController.Update/
FixedUpdate` (`:568`,`:1758`), `BombController.Update` (`:312`), `PlayersSpawner`
(`:82`), `NetSpawn`. **Todo o resto simula em qualquer máquina.**

Precisam do mesmo gate no cliente puro (early-return em `Update/FixedUpdate/
LateUpdate/OnTrigger*`/coroutines):
- **Bomba:** `Bomb.cs` (fuse `:257`, kick, punch, magnet, `OnTriggerEnter2D`
  chain `:1693`, `FixedUpdate` `:206`), `MagnetBomb.Update` (`:92`).
- **Animação/emote:** `InactivityAnimation.Update` (`:126`), `CorneredAnimation.
  Update` (`:106`), `StunReceiver` (`StunRoutine` `:322`), animators de mount
  (`Mounts/*Animator.cs`), `PlayerRidingController.LateUpdate` (`:79`),
  `PowerGloveAbility`/`BombPunchAbility` updates.
- **Mounts/abilities:** `PlayerMountCompanion.Update` (`:115`),
  `PlayerManualDismount.Update` (`:35`), `MountEggQueue`, `MountWorldPickup.
  OnTriggerEnter2D` (`:54`), `MountMovementController.Update/FixedUpdate`
  (`:40`,`:90`), e o `Update()` de **todas** as abilities (todas leem
  `PlayerInputManager` e disparam efeito/`Instantiate` local).
- **Itens:** `ItemPickup.OnTriggerEnter2D` (`:701`) — pickup deve resolver só no
  host; `SkullDebuffController` (contágio/física/timers), `ClockStageStunEffect`.
- **Tiles/gimmicks:** todos os `BattleModeN*Controller`, handlers de tile
  (`Destructible/`,`Indestructible/`), `BattleSuddenDeathController`,
  `FragileFloor` — toda mutação de `Tilemap`/dano só no host.
- **Fluxo:** `GameManager` (morte/round/vitória/timer) — decidir só no host.
- **Fase futura (co-op):** todos os `Enemies/*`, `IA/*`, `Bosses/*`.

Padrão recomendado: helper único `if (!NetSync.ShouldSimulateLocally) return;`
no topo dos loops, e para os que também precisam existir no cliente como
receptores, gatear só a parte de *decisão/mutação*, mantendo a *apresentação*.

---

## 4. Estado por-player: o refactor central (`PlayerPersistentStats`)

`PlayerPersistentStats` é uma **classe `static`** com arrays `_p[6]`/`_stage[6]`
por `playerId` (`:50`,`:60`), populada do **save da máquina local**. No cliente
puro ela devolve valores locais errados → HUD, skin e timing de animação
divergem.

**Alvo:** um `NetworkBehaviour` por player (ex.: `NetworkPlayerState`) com
SyncVars autoritativas do host:
- `Life`, `BombAmount`/`BombsRemaining`, `ExplosionRadius`, `SpeedInternal`.
- Flags de ability empacotadas num `int` bitmask (kick/punch/glove/pierce/
  control/power/rubber/magnet/fullfire/bombpass/destructiblepass/invincible).
- `MountedType`, `IsMounted`, fila de ovos (tipos+contagem) via SyncList.
- `Skin` (byte do enum `BomberSkin`) — hook aplica `PlayerBomberSkinController.
  Apply(...)` direto, **sem** passar por `PlayerPersistentStats`.

O `static` continua existindo só como cache local/HUD, alimentado pelo host.
HUD (`BattleModeHud`) passa a ler o estado replicado, não o `static` local.

---

## 5. Auditoria por domínio (o que replicar e como)

### 5.1 Movimento + Animação
- **Já OK:** posição via `NetworkTransformReliable` (server→client). ⚠️
  `syncScale=0` → o "afundar" da morte em buraco (escala→0) não replica.
- **Trocar** o `NetworkPlayerAnimation` atual (reconstrói, cobre 4 estados) por
  **replicação de saída**: índice do renderer ativo (1 byte; ordem estável de
  `GetComponentsInChildren<AnimatedSpriteRenderer>(true)`, idêntica por ser o
  mesmo prefab) + flags (idle/flipX/loop/pingPong) + `CurrentFrame` do ativo
  (para one-shots: morte/endstage/punch) + skin id. Loops (andar/afk) podem
  auto-animar no cliente (o `AnimatedSpriteRenderer.Update` já roda sozinho).
- **Gatear** os drivers locais (§3) senão brigam com a saída.
- Fonte no host: `MovementController.ActiveSpriteRenderer` (`:255`).

### 5.2 Bombas + Explosões
- Trocar `Instantiate` por `NetSpawn.Server`: `BombController.cs:1009` (place),
  `:2781` (revenge), `:2914` (IA/ability), bloco destruído `:2063/2065`, item
  escondido `:2109`, item expelido `Bomb.cs:556`. `Destroy` → `NetSpawn.Despawn`
  (`:1285/1488/1530`).
- Prefabs de bomba precisam de `NetworkIdentity` + registro no
  `BombermanNetworkManager`.
- `Bomb.cs`/`MagnetBomb.cs` inteiros gated no cliente (§3).
- **Explosão:** dano só no host; **visual via `ClientRpc`** (não replicar o
  objeto por causa do pool estático `BombExplosion.cs:8/65/152`).
- `Random` de rubber bounce (`Bomb.cs:849`) e direção de item expelido
  (`:1077`) resolvidos só no host.

### 5.3 Tiles + Gimmicks de Stage
- Blocos são **Tilemap** (célula = `TileBase`), não GameObjects. Criar um
  `StageTileSync` (`NetworkBehaviour` na raiz do stage): host executa
  `SetTile(cell,null)` (`BombController.cs:2078`, `SuddenDeath:650`,
  `Minecart:1757`, `YellowLouie:370`) e emite `RpcClearDestructible(cell)`;
  para tiles que mudam/renascem, `RpcSetTile(cell, tileId)` com um registry de
  `TileBase`→id por stage (TileBase não serializa).
- **Revelação de itens:** `GameManager.SetupHiddenObjects` (`:542`, shuffle
  `UnityEngine.Random` `:571`) e `GetSpawnForDestroyedBlock` (`:770`) só no
  host; item revelado spawnado via `NetworkServer.Spawn`. (A "ordem de
  destruição" define o drop — tem que ser autoritária.)
- **11 gimmicks (BM2..BM11):** estado discreto (esteira `clockwise/fast`, teto
  fase+timer) vira `[SyncVar]` com `NetworkTime.time`; eventos aleatórios
  (alvo da falling bomb, pouso da mola, teleporte de portal, impulso do rope,
  minecart) → host decide o `Random` e envia resultado por `ClientRpc`; VFX
  puramente cosméticos (estrelas/fogos) podem ficar locais, **mas dano/hitbox
  é host-autoritativo**.
- **Sudden Death:** padrão/canto/direção (`Random`, `:464-471`) + cronograma
  gerados só no host; replicar a `suddenDeathPath` + tempos, `RpcDropTile(cell)`
  por queda; dano só no host.
- Bootstrap: os `[RuntimeInitializeOnLoadMethod]` instanciam controllers em
  todos os clientes → manter, mas gatear toda mutação por `isServer`.

### 5.4 Itens + Powerups
- Pickup resolvido só no host (`ItemPickup.OnTriggerEnter2D` `:701`); host
  aplica stats (no estado replicado do §4) e faz `NetSpawn.Despawn` do item.
- Todos os prefabs de `Resources/Items` com `NetworkIdentity` + spawn de rede.
- Skull: efeito escolhido por `Random` (`SkullDebuffController.cs:46`) só no
  host; replicar qual efeito + tempo restante; a física de "skull bounce"
  (`ItemPickup.cs` ~717-1622) roda só no host, posição replicada.
- `AutoItemDatabase` (dict `static`) e drops de morte (`GameManager:1590`,
  `Random`) → host-autoritativo.

### 5.5 Mounts (Louie) + Abilities
- Componente análogo ao de animação, para montaria: SyncVar de `MountedType`,
  `IsMounted`, fase de riding (arco), e "ação de ability em curso" (dash/kick/
  punch/jump/drill/shoot + direção) para dirigir `MountVisualController`/
  `*Animator` no cliente (onde o `MovementController` está off).
- Louies/ovos **de mundo** (destacados/pickups) e projéteis (TankShot
  `:312`, bombas de Purple/PowerGlove) → `NetSpawn.Server` + `NetworkIdentity`,
  ou tratados como visuais por SyncVar.
- Gatear todos os scripts de mount/ability (§3). Flags de bomba (rubber/pierce/
  control/power/fullfire) NÃO precisam replicar — o host as consome na
  simulação; o objeto resultante (bomba/explosão) já é replicado.
- PvP: `RedLouiePunchStun`/`BlackLouieDashPush` afetam outros players → 100%
  autoritativo no host.

### 5.6 Fluxo de jogo + Sessão + HUD
- **Só o host decide** morte/round/vitória/empate/time-up: gatear
  `NotifyPlayerDeathStarted`/`EvaluatePlayerWinState`/`Trigger*Sequence`/
  `UpdateBattleRoundTimer` (`GameManager.cs:1309-1926`,`1723-1768`) com
  `NetSync.IsServer` e propagar o resultado (vencedor/overlay) por `ClientRpc`.
- **Cenas:** trocar as 9 `SceneManager.LoadScene` (`GameManager.cs:819..1925`)
  por `NetworkManager.ServerChangeScene`. Definir o caminho de **re-spawn no
  restart de round** (hoje `BombermanNetworkManager.OnServerAddPlayer` só
  spawna em conexão nova — GAP).
- **HUD replicado:** mask de players ativos (`GameSession`),
  `CharacterHealth.life`, runtime stats por player (§4), `battleMatchWins`,
  `battleTimeRemainingSeconds`+`hasBattleTimeLimit`, flags de morte, regras/
  times. `GameSession` é o candidato natural a virar autoritativo/replicado.
- **Timer/pausa:** timer único no host (hoje `Time.unscaledDeltaTime` por
  processo); `Time.timeScale`/`GamePauseController` são globais — pausa online
  precisa de política (host-authoritative ou desabilitar).

### 5.7 Transversais (riscos que "vazam" host↔cliente no mesmo processo)
- **`static` mutável de gameplay/sessão:** `PlayerPersistentStats` (§4),
  `BossRushSession`, `NormalGameAIManager`, `FireworkTileHandler._total/_destroyed`,
  `SaveSystem.data`, `PlayerIdentity.activePlayers`, buffers de física estáticos
  (`_bombOverlapBuffer`, etc. — OK se só o host simular), SFX estáticos
  (`kickSfxOwner`...).
- **Singletons `DontDestroyOnLoad`:** `GameSession`, `PlayerInputManager`,
  `GamePauseController`, `GameMusicController`, runners de coroutine, etc. — no
  host+cliente-local mesmo processo, cada "metade" vê o mesmo objeto.
- **RNG:** todo `UnityEngine.Random` que afeta gameplay (layout de itens
  `GameManager:571`, sudden death, drops, skull, IA/boss) deve ser resolvido só
  no host (spawn autoritativo ou seed replicado). Não há `System.Random`.
- **`Time.frameCount`** em lógica (chain de bombas `BombController:1834`,
  invuln de mount `MountEggQueue:122`, snapshots de IA) — frames não alinham
  entre máquinas.
- **Descoberta de players:** `PlayerIdentity.activePlayers` (static) +
  `FindObjectsByType`/tag "Player" (inimigos/bosses) — mapear para a lista de
  players de rede (`BombermanNetworkManager.SyncActivePlayersToSession`).

---

## 6. Roadmap priorizado (fatias verificáveis)

Cada fatia deve compilar, conectar 2 instâncias e ser testável isoladamente.

- **F0 — Fundterra do estado por-player (`NetworkPlayerState`)** e migração do
  HUD/skin/velocidade para ele. Base de quase tudo. *(refactor do `static`.)*
- **F1 — Animação por replicação-de-saída** (renderer ativo+frame+flags+skin) +
  gate de todos os drivers locais de animação. Substitui o `NetworkPlayerAnimation`
  atual. **Resolve o dessync visual reportado.**
- **F2 — Bombas** (spawn replicado + `Bomb`/`MagnetBomb` gated) e **explosão
  visual por RPC** com dano só no host.
- **F3 — Tiles destrutíveis + revelação de itens** (`StageTileSync` por célula +
  spawn autoritativo de item + RNG de layout no host).
- **F4 — Itens/powerups + skull** (pickup host-only, stats no `NetworkPlayerState`,
  efeitos temporários replicados).
- **F5 — Regras de partida** (morte/round/vitória/placar/timer host-only + RPC,
  `ServerChangeScene`, re-spawn no restart).
- **F6 — Mounts (Louie) + abilities** (estado de montaria replicado + spawn de
  Louies/ovos/projéteis + gate de todas as abilities).
- **F7 — Sudden Death + gimmicks de stage** (por BattleMode).
- **Fase futura (co-op):** Enemies/IA/Bosses (gate + NetworkTransform + spawn de
  projéteis + RNG no host). Fora do escopo do Battle Mode PvP.

---

## 7. Decisões em aberto (para alinhar antes de F0)

1. **Escopo do Battle Mode alvo:** só locomoção+bomba+itens+regras (F0–F5), ou
   incluir mounts/gimmicks (F6–F7) na POC?
2. **Fidelidade de animação:** sincronizar `CurrentFrame` sempre (mais banda,
   sem defasagem de fase) ou só em one-shots (morte/endstage/punch)?
3. **Explosão:** confirmar a abordagem "dano no host + visual por RPC" (vs.
   reescrever o pool para custom spawn handler do Mirror).
4. **Cena dedicada:** manter a `BattleMode_1` como cena online-dedicada (intro
   pulada) ou generalizar para todas as `BattleMode_*`?
5. **Pausa online:** desabilitar pausa no online ou torná-la host-autoritativa?
