# Plano: Lobby online (Mirror) — sala + seleção de personagem + ready

## Context

Hoje o online é **quick-connect**: `OnlineLobbyMenu` (OnGUI) só faz Host/Join e cai
direto na partida. **Não há sala**, escolha de personagem, nem ready. Bug conhecido
("personagem aleatório", M2): o host aplica a skin do **próprio save local** por
playerId, não a escolha de cada cliente. Precisamos de (a) uma **sala de verdade**
pra acoplar ao menu do jogo-base e (b) **corrigir o char-select**. Direção acordada:
**Fase 1 (lobby-lite)** → **Fase 2 (lobby completo)**.

## Veredito de arquitetura: ESTENDER o nosso NM (não migrar pro `NetworkRoomManager`)

O Mirror tem `NetworkRoomManager`/`NetworkRoomPlayer` (sala, ready, min-players,
swap room→game de graça). MAS o nosso `BombermanNetworkManager` estende
`NetworkManager` (`BombermanNetworkManager.cs:39`) e já implementa à mão: alocação de
playerId (`playerIdByConnection`, `:42`), `ServerStartMatch`/`ServerReturnToLobby`/
`ServerStartNextRound` (`:81,92,134`), auto-start do servidor dedicado
(`:103-123`), `OnServerAddPlayer`/`OnServerDisconnect` custom (`:288,316`), e as
messages `RoundOverMessage`/`RoundTimerMessage` (`:11-24`). Migrar pro
`NetworkRoomManager` = **refactor grande + risco** ao fluxo que já funciona e ao
dedicado. **Decisão: estender o nosso**, reaproveitando só o **padrão** do
`NetworkRoomPlayer` (um objeto por conexão com SyncVars) sem trocar a classe-base.

## Modelo — `LobbyPlayer` (por conexão)

Objeto networkado leve (NetworkBehaviour), **1 por conexão**, spawnado no **ramo-lobby**
do `OnServerAddPlayer` (`BombermanNetworkManager.cs:296-300`, que hoje só registra a
conexão sem spawnar). SyncVars: `playerId`, `character` (byte), `skin` (byte),
`ready` (bool), [`name` opcional]. O cliente-dono seta character/skin/ready via
`[Command]` (padrão de `NetworkPlayerInput.cs`); as SyncVars replicam pra todos → a
**lista + escolhas + ready** da sala saem "de graça" (todos veem todos), sem message
manual. No match-start, o host lê cada `LobbyPlayer` pra um mapa `chosenLoadoutByConnection`,
e o spawn normal da batalha aplica.

## Fase 1 — Lobby-lite (resolve o char-select)

1. **Prefab + script `LobbyPlayer`** (NetworkIdentity + SyncVars playerId/character/
   skin/ready + `[Command]`s). Spawnado por conexão via `AddPlayerForConnection` no
   ramo-lobby do `OnServerAddPlayer` (preserva playerId allocation + dedicated auto-start).
2. **Char-select fix (M2)** — `Assets/Scripts/Netcode/NetworkPlayerState.cs`:
   - Adicionar SyncVar `character` (hoje só `skin`, `:27`).
   - `ServerSample` (`:189-192`): ler character+skin do mapa `chosenLoadoutByConnection`
     (escolha do `LobbyPlayer`) em vez de `PlayerPersistentStats.Get(pid)` (save do host).
   - `ClientApply` (`:294-298`): chamar `PlayerBomberSkinController.Apply(character, skin)`
     (2-arg, `Skin/PlayerBomberSkinController.cs:162`) em vez do 1-arg.
   - Ao spawnar o Player na batalha (`OnServerAddPlayer` ramo-batalha `:303-311` /
     `NetworkPlayerSetup.ServerAssignPlayerId`), carimbar character+skin do mapa.
3. **Ready-gate**: `ServerStartMatch` (`:81`, choke point único) só inicia com todos
   ready (host-GUI). **Dedicado bypassa** (auto-marca ready OU mantém o count-based de
   `TryDedicatedAutoStart` `:103`). `-server`/`-autohost` seguem intactos.
4. **UI da sala (mínima)**: lista (dos `LobbyPlayer`) + seletor de personagem + toggle
   Ready + botão Start do host (habilita com todos ready / mín. players). Pode começar
   estendendo o OnGUI atual (`OnlineLobbyMenu.cs`); vira uGUI na Fase 2.
5. **Retorno pós-partida**: o F5b já volta pra lobby (`ServerReturnToLobby`) → os
   `LobbyPlayer` reaparecem.

**Resolve:** "personagem aleatório" (item 5 do backlog de playtest).

## Fase 2 — Lobby completo

- **Settings do host** replicados: `BattleModeRules` (`Assets/Scripts/BattleMode/
  BattleModeRules.cs`) — matchMode (single/tag) + times, `victoriesToWinMatch`,
  `roundTimer`, sudden-death/revenge/item-drops, `computerLevel`, música, loadout
  inicial. Hoje são `[SerializeField]` na cena → replicar (SyncVars/message aplicados a
  `BattleModeRules.Instance` antes do `ServerStartMatch`).
- **Nomes de jogador** (net-new — hoje só existe playerId+skin).
- **Telas uGUI** de verdade (Host/Join com IP pré-preenchido `sb6.kyvora.dev`, lista
  com avatares, kick).
- **Fila de late-join** → resolve o **item 1 do backlog** (blocos dessincronizados no
  late-join: quem chega no meio espera na sala).

## Acoplamento com o menu do jogo-base

- **Time deles (menu):** nova linha "Batalha Online" no submenu GameModes do
  `TitleScreenController.cs` (perto de `:245-247`) + flag `OnlineBattleRequested` +
  branch `SceneManager.LoadScene(lobbyScene)` no `TitleScreenBootstrap.ShowTitleScreen`
  (perto de `:193`). `BattleModeMenu.cs` é o análogo local (template).
- **Nós:** entry point único (`OnlineEntry.Open`) que carrega a `OnlineLobby` (dona do
  NM). O botão deles só chama isso — sem acoplar a nomes de cena/Mirror.
- **Boot:** reverter o hack "OnlineLobby como cena 0" no build do **CLIENTE**
  (`OnlineClientBuild.cs` — bootar normal no TitleScreen); **manter** no build do
  **SERVIDOR** (`DedicatedServerBuild.cs` + `-server`/`HeadlessBootstrap`).
- **Retorno ao menu:** hoje `offlineScene: OnlineLobby` (`OnlineLobby.unity:196`) fixa o
  teardown na lobby; pra voltar ao menu deles, limpar/override `offlineScene` antes do
  `StopHost/StopClient` OU `LoadScene(menu)` explícito após o Stop.

## Wiring no Editor (não dá via terminal)

- Criar o prefab `LobbyPlayer` (NetworkIdentity + script) e registrá-lo no NM.
- Ajustar os build scripts (cliente boota TitleScreen; servidor mantém OnlineLobby cena 0).
- (A SyncVar `character` no `NetworkPlayerState` é código — não muda prefab.)

## Verificação (como testar)

- **Fase 1:** 2+ clientes conectam → a sala mostra todos + cada um escolhe personagem +
  Ready → host Start → cada player **nasce com a skin/character ESCOLHIDA** (não
  aleatória). Dedicado: auto-start segue funcionando (bypassa ready).
- **Fase 2:** host muda settings → refletem na partida; nomes aparecem; late-join entra
  na fila em vez de dessincronizar.

## Decisões em aberto

- **`LobbyPlayer` object (recomendado)** vs. server-dict + broadcast message (mais
  simples, menos idiomático, UI manual).
- **Nomes** na Fase 1 (senão só slot+personagem) ou só na Fase 2.
- **uGUI:** quando trocar o OnGUI — depende do menu que o time do jogo-base entregar.
