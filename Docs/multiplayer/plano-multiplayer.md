# Plano: Multiplayer online (Mirror) para Super Bomberman 6 — visão geral + ação

## Context

O projeto é um fan-game Bomberman 2D em Unity (branch de trabalho `feature/mirror-v0.5.0`, base `upstream/v0.5.0`, Unity 6000.5.4f1). **Ele não nasceu para multiplayer** — é single-player/local-coop, com estado global, RNG local, mundo em Tilemap e fluxo de cena direto. O objetivo é oferecer **Battle Mode PvP online** usando **Mirror**, no modelo **host-autoritativo** (um jogador hospeda = servidor + cliente local; os demais são clientes puros que só renderizam).

Este documento serve para: (1) confirmar a **viabilidade** da estrutura, (2) listar os **problemas estruturais** que precisam ser adaptados, (3) apresentar um **plano de implementação transparente** (o que já foi feito e o que falta) para comunicar aos outros devs, e (4) dar **diretrizes** para o time não introduzir novos padrões incompatíveis com rede.

Companheiro técnico detalhado (com file:line): `Docs/multiplayer/netcode-architecture.md` (auditoria de 7 frentes já commitada no repo).

---

## Veredito de viabilidade: SIM, é viável — e já está provado

A estrutura é adaptável para Mirror host-autoritativo **sem reescrever os controllers monolíticos**, graças a duas características do projeto:

- **Tudo é indexado por `playerId` (1..6)** — `GameSession`, `PlayerIdentity`, `MovementController.SetPlayerId`, `BombController.SetPlayerId`. Isso mapeia quase 1:1 para conexões de rede.
- **Input centralizado com "input sintético"** — `PlayerInputManager.Get(playerId, action)` retorna `sintético || hardware`. No host, injetar o input dos clientes como sintético faz o `MovementController`/`BombController` **existentes** simularem os jogadores remotos sem alterar a lógica de gameplay.

**Prova concreta (já funcionando na v0.5.0, testado lado a lado):** conexão host/cliente, spawn por conexão, movimento, animação (andar/idle/AFK sincronizados), bombas colocadas dos dois lados, explosão visual replicada e dano host-autoritativo com vida no HUD.

**Porém:** viabilidade ≠ trivialidade. O jogo tem ~231 MonoBehaviours e só ~5 são de rede hoje; a maior parte da simulação (inimigos, bosses, itens, mounts, gimmicks) roda igual em host e cliente. A adaptação é **sistemática** e precisa seguir um padrão único, senão vira "whack-a-mole" (aprendemos isso: tentar reconstruir estado no cliente não escala — a solução é **replicar a saída autoritativa do host**).

---

## Princípios de arquitetura (o "como", que todo dev deve seguir)

1. **Host-autoritativo.** O host simula 100% do gameplay; o cliente é um terminal que renderiza o estado replicado. Nada de decisão de gameplay (dano, morte, RNG, spawn) no cliente.
2. **Gate de simulação em todo driver de gameplay.** Padrão já estabelecido: no topo de `Update/FixedUpdate/LateUpdate/OnTrigger*`/coroutines, `if (!Assets.Scripts.Netcode.NetSync.ShouldSimulateLocally) return;`. O `NetSync` (em `Assets/Scripts/Netcode/NetSync.cs`) é a ponte SEM dependência do Mirror — o código core só consulta ele.
3. **Replicar a SAÍDA, não reconstruir.** Ex.: animação = replicar o AnimatedSpriteRenderer ativo + frame + flags (não "facing/moving"). Cobre todos os estados de uma vez.
4. **Três mecanismos de replicação:**
   - `NetworkIdentity` + `NetworkServer.Spawn` (via helper `NetSpawn.Server`) → objetos de simulação (bombas, itens revelados, projéteis).
   - `[SyncVar]`/SyncList → estado contínuo (vida, bombas, raio, velocidade, flags, timer, placar).
   - `ClientRpc` de evento → mutações não-replicáveis (destruição de célula de Tilemap por coordenada; visual de explosão — o pool estático de `BombExplosion` é incompatível com spawn do Mirror).
5. **RNG só no host.** Toda decisão aleatória de gameplay é resolvida no host e o resultado replicado (ou seed compartilhada). Cosmético pode ficar no cliente.

---

## Problemas estruturais a resolver (o que comunicar aos devs)

Verificado na v0.5.0 atual. Ordem por criticidade:

1. **`PlayerPersistentStats` é `public static class`** (`Assets/Scripts/PlayerPersistentStats.cs:5`) — todo o loadout dos 6 jogadores (vida, bombas, raio, velocidade, habilidades, Louie, skin, ovos) vive em arrays `static`. Em host-local, host e cliente compartilham o MESMO slot → impossível dois jogadores com estado independente. **Precisa virar estado por-jogador replicado** (SyncVars num `NetworkBehaviour` por player; o `static` fica só como cache/HUD alimentado pelo host). *(Já iniciado — ver F0.)*
2. **Mundo em `Tilemap`, sem `NetworkIdentity`** (`GameManager.cs:158-171`; `SetTile` em `OnDestructibleDestroyed :1045` e handlers). Destruição de blocos e revelação de itens/inimigos **não chegam ao cliente**. Solução: componente autoritativo que emite eventos de célula via `ClientRpc`, e itens revelados spawnados por `NetSpawn.Server`.
3. **RNG local decidindo gameplay** (184 usos de `Random.*`). Críticos: layout de itens escondidos (`GameManager` Shuffle `:632/658/711`, eggs `:2077/2101`), drops de morte (`:1673`), skull (`SkullDebuffController.cs:46`), sudden death (`BattleSuddenDeathController.cs:464-471`), IA/inimigos (14 controllers), bosses. **Todos precisam ser host-only.**
4. **Fluxo de cena por `SceneManager.LoadScene`** (50 usos; 9 no `GameManager.cs`: `:902,907,921,977,1003,1008,1896,2000,2017`; zero `ServerChangeScene`). No online, troca de stage/round/menu quebra o sincronismo. Precisa de `NetworkManager.ServerChangeScene` + re-spawn dos players já conectados.
5. **Ausência quase total de gating.** Fora de `Netcode/` + 8 arquivos já gateados, 200+ MonoBehaviours (todos os 33 inimigos, IA, 11 bosses, itens, 24 mounts, 22 abilities, gimmicks de BattleMode, StageAssets, tile handlers) rodam idênticos nos dois lados → dupla simulação/divergência.
6. **Singletons de sessão** (`GameSession`, `PlayerInputManager`, `GamePauseController`, `BossRushSession` static, `NormalGameAIManager` static com `AiPlayerIds={2,3,4}`, `BattleModeTeams`, `BattleRevengeSystem`) — colidem entre host e cliente no mesmo processo; `GameSession` precisa ser autoridade-do-servidor replicada.
7. **Sem lobby online.** A config de partida (nº de players, humano/COM, stage, regras, times, loadout) é escolhida no `BattleModeMenu` e persistida no **`SaveSystem` LOCAL** + `GameSession`, e a cena é carregada por `SceneManager.LoadScene` (`BattleModeMenu.cs:5888`). Cada máquina leria o próprio save. Precisa: host decide a config → **replica** → todos entram na mesma cena por `ServerChangeScene`.
8. **Controllers monolíticos** (`MovementController` ~5k linhas, `BombController` ~3.4k, `Bomb` ~2.7k). Não precisam ser reescritos (a costura de input sintético evita isso), mas exigem gating cuidadoso e replicação de saída.

---

## Progresso até agora (transparente) — branch `feature/mirror-v0.5.0`

Tudo commitado; padrão validado com testes host+cliente lado a lado.

| Fase | Entrega | Status |
|---|---|---|
| Setup | Mirror embutido (`Packages/com.mirrornetworking.mirror`), `BombermanNetworkManager`, conexão host/cliente, spawn por conexão, `NetSync`/`NetSpawn` | ✅ |
| **F1** | Animação por replicação-de-saída (`NetworkPlayerAnimation`) + gate dos drivers locais de animação | ✅ testado |
| **F0** | Estado por-player no HUD (`NetworkPlayerState`: vida/bombas/raio/velocidade/skin + bitmask de 11 flags de powerup) | ✅ testado |
| **F2** | Bombas em rede (spawn/despawn) + explosão visual por `ClientRpc` (`NetworkBombFx`) + input dos dois lados (tap explícito) + dano host-only (`CharacterHealth.TakeDamage` gateado) | ✅ testado |
| **F3** | Destruição de blocos por evento de célula (`RpcClearDestructibles`) + animação de quebra no cliente + item revelado spawnado por `NetSpawn.Server` (alinhado ao grid nos 2 lados) + pickup host-only | ✅ testado |
| **F4** | Efeitos temporários: **gameplay já era host-autoritativo** (pickup/RNG/dano host-only); só faltava o **visual** — `NetworkPlayerState.tempFx` replica o blink de skull/invencibilidade | ✅ testado |
| **F5a** | Fim de round host-autoritativo: eliminação replicada (`NetworkPlayerState.eliminated`), round-end online dedicado (sem fade/reload) + freeze de input, timer replicado (`RoundTimerMessage` + `NetSync.IsNetworkedScene`), placar/troféu replicado (`RoundOverMessage` com wins) | ✅ testado |

`Player.prefab`: 7 componentes de rede. Scripts novos em `Assets/Scripts/Netcode/`. Guards de 3 linhas em 8 arquivos core. Nenhuma lógica de gameplay reescrita.

---

## Roadmap de implementação

**Escopo alvo confirmado: Battle Mode PvP "Core" (F0–F5).** Mounts/gimmicks (F6–F7) e co-op de campanha (inimigos/bosses/IA) são fases FUTURAS, claramente marcadas.

- **F0 (fatia 2) — Estado por-player completo.** Estender `NetworkPlayerState`: bitmask de flags de habilidade (kick/punch/glove/pierce/control/power/rubber/magnet/fullfire/pass), e o mínimo de mount para o HUD. *(Resolve problema estrutural #1.)*
- **F3 — Destruição de blocos + revelação de itens.** `StageTileSync` (NetworkBehaviour na raiz do stage): host destrói célula e emite `RpcClearDestructible(cell)` / `RpcSetTile(cell,id)`; `GameManager.SetupHiddenObjects` (RNG do layout) roda só no host; item revelado via `NetSpawn.Server`. *(Resolve #2 e parte de #3.)*
- **F4 — Itens/powerups + skull.** Prefabs de item com `NetworkIdentity`; pickup resolvido só no host (`ItemPickup.OnTriggerEnter2D`), aplica stats no estado replicado (F0); efeitos temporários (skull/clock/invencibilidade) calculados no host e replicados. *(Resolve #3 restante + gating de itens.)*
- **F5 — Regras de partida + fluxo de cena.** Fim de round/vitória/empate/timer decididos só no host (gate `NetSync.IsServer` em `GameManager` + `ClientRpc`); trocar `SceneManager.LoadScene` por `ServerChangeScene` (`GameManager.cs:1896/2000/2017`); caminho de re-spawn dos players já conectados no restart de round (hoje `BombermanNetworkManager.OnServerAddPlayer` só spawna em conexão nova). *(Resolve #4, #6.)*
- **F5b — Lobby online completo (DECIDIDO).** Adaptar o `BattleModeMenu` num lobby networked: o **host** escolhe match mode/players/stage/regras/times/loadout; essa config é **replicada** aos clientes (via `GameSession` autoritativo + estado de rede, substituindo o `SaveSystem` local); ao confirmar (hoje `BattleModeMenu.cs:5888` `ConfirmSpecificSettingsStart`), todos entram na cena por `ServerChangeScene`. Slots: conexão remota = `Man` daquele player; vazios = `Com`/`Off` decididos pelo host. Fim de match volta todos ao lobby (host-autoritativo), não cada cliente ao menu local. *(Resolve #7.)*
- **Transporte para internet (DECIDIDO — planejar já no F5):** hoje é KCP (LAN/IP direto). Adicionar um caminho para jogar pela internet sem port-forward — **relay** (ex.: Edgegap/relay) ou **Steam via FizzySteamworks**. Como o Mirror abstrai o transporte, é trocar/empilhar o componente de `Transport` no `NetworkManager`; o resto do netcode não muda. Definir qual (Steam exige Steamworks/AppID; relay pode exigir serviço/conta).
- **Futuro F6–F7:** Mounts (Louie)/abilities + Sudden Death/gimmicks de stage.
- **Futuro (co-op de campanha):** gatear inimigos/IA/bosses + `NetworkTransform` + spawn de projéteis + RNG host-only. Grande esforço, fora do Battle Mode.

Cada fase é uma fatia testável (compila, conecta 2 instâncias, valida um comportamento) — mesmo padrão que usamos até aqui.

---

## Diretrizes para os outros devs (para a estrutura seguir viável)

Enquanto o multiplayer é construído, o time deve evitar reintroduzir bloqueios:

- **Não adicionar estado `static` mutável de gameplay/sessão.** Use estado por-componente/por-player. Estado que precisa persistir em partida = candidato a SyncVar (host-autoritativo).
- **Novos objetos spawnados em runtime** (bombas, projéteis, itens, efeitos): prefira prefab com `NetworkIdentity` e roteie o `Instantiate` por `NetSpawn.Server(...)` (no-op offline). Se for puramente visual/cosmético, tudo bem ficar local.
- **RNG que afeta gameplay** deve ser resolvido no host. Nada de `Random` decidindo layout/dano/spawn no cliente.
- **Novos `MonoBehaviour` de simulação** (inimigo, ability, gimmick): já nasça com o gate `if (!NetSync.ShouldSimulateLocally) return;` nos loops, e replique a saída visível.
- **Troca de cena de gameplay**: quando online, tem que ser `ServerChangeScene` (não `SceneManager.LoadScene`).
- **Dano/morte**: só no host; o cliente recebe vida/estado replicado.
- **Objeto com `NetworkIdentity` NÃO pode ficar colocado em cena** de gameplay sem processamento (Mirror exige `sceneId`; um prefab-instância largado na cena quebra o build/spawn). Se um prefab networked também for usado colocado manualmente numa cena (ex.: LandMine), NÃO ponha `NetworkIdentity` nele — trate a versão de cena como local. Regra: `NetworkIdentity` é para o que o host **spawna em runtime**, não para o que já está na cena.
- **Objeto spawnado pelo Mirror deve nascer na RAIZ** (sem parent). No `SpawnMessage` o Mirror replica `transform.localPosition` (não a world) — se o objeto for filho de algo com offset (ex.: o Tilemap), ele aparece deslocado no cliente. Instancie na raiz quando online (ou reparente pra bater a `localPosition`). Cuidado extra com `AnimatedSpriteRenderer` na raiz: ele captura a base de offset no `Awake` conforme o parent daquele momento — reparentar **depois** do `Awake` deixa a base velha e desloca o host. Nasça já na raiz.

---

## Verificação (como testar)

- **Setup de teste:** um **build** roda como **Host** (Build And Run) e o **Editor** roda como **Client** (Play na `BattleMode_1` → HUD do Mirror → Connect `127.0.0.1`). Validação assistida via Unity MCP (ler Console, inspecionar prefab/cena).
- **Ajustes temporários de teste** (não commitar): `BattleMode_1` como cena inicial do build + modo janela — reverter antes de finalizar/push.
- **Critério por fase:** conectar 2 instâncias e confirmar o comportamento da fatia (ex.: F3 = bloco destruído some nos dois lados e item revelado aparece igual; F5 = round/placar/timer iguais nos dois, troca de cena coordenada).
- **Git/entrega:** branch `feature/mirror-v0.5.0` (sobre `upstream/v0.5.0`). **Push pro fork `0xdeadbad` é feito pelo usuário** (`git push -u origin feature/mirror-v0.5.0`) — o shell do agente não tem SSH.

---

## Decisões

**Já decididas:**
- **Lobby online = COMPLETO** — adaptar o `BattleModeMenu` num lobby networked com config replicada do host (F5b).
- **Transporte = planejar internet já no F5** — relay ou Steam (FizzySteamworks), além do KCP local. Definir qual.
- **Escopo alvo = Battle Mode PvP Core (F0–F5/F5b)**; mounts/gimmicks (F6–F7) e co-op de campanha ficam como fases futuras.

**Ainda em aberto:**
- **Cena dedicada** `BattleMode_1` online vs generalizar o suporte para todas as `BattleMode_*` (naturalmente resolvido pelo lobby completo, que precisa carregar qualquer stage escolhido).
- **Servidor dedicado** no futuro (build headless) — a arquitetura host-autoritativa já deixa isso incremental.
- **Qual transporte de internet** exatamente (Steam exige Steamworks + AppID; relay exige serviço/conta).
