# Battle Mode Online (Mirror) — POC host-autoritativo

Branch: `feature/mirror-battlemode-pvp`

Prova de conceito de multiplayer online para o **Battle Mode**, usando
[Mirror](https://mirror-networking.gitbook.io/) num modelo **host-autoritativo**:
um jogador hospeda (é servidor + cliente local) e os demais conectam como
clientes puros.

## Por que este projeto encaixa bem

O jogo já foi feito para multiplayer **local** de 1–6 jogadores, e isso é o
que torna a port online barata:

- Tudo é indexado por `playerId` (1..6): `GameSession`, `PlayerIdentity`,
  `MovementController.SetPlayerId`, `BombController.SetPlayerId`.
- O input é centralizado em `PlayerInputManager` e já suporta **input
  sintético** (`SetSyntheticHeld` / `TapSynthetic`), usado hoje pela IA e
  pelo mobile.
- `PlayerInputManager.Get(playerId, action)` retorna
  `sintético || hardware`.

➡️ **Consequência-chave:** no host, basta injetar o input dos clientes como
input sintético do `playerId` deles, e o `MovementController`/`BombController`
**existentes** simulam esses jogadores sem nenhuma mudança na lógica de
gameplay.

## Modelo de rede

```
Cliente (dono)                         Host (servidor + cliente local)
--------------                         -------------------------------
lê hardware local  --- [Command] --->  aplica como input sintético do
(esquema player 1)   (bitmask de         playerId daquela conexão
                      ações)            simula TUDO (movimento, bombas,
                                        explosões) com o código existente
posição/anim   <--- [NetworkTransform] -- replica estado
bombas/itens   <--- [NetworkServer.Spawn] -
```

- **Offline** (single-player / local co-op): nada muda; `NetSync.Mode = Offline`.
- **Host**: `NetSync.Mode = Host` → simula localmente + é autoridade.
- **Client**: `NetSync.Mode = Client` → **não** simula; só renderiza.

## Arquivos adicionados (`Assets/Scripts/Netcode/`)

| Arquivo | Papel |
|---|---|
| `NetSync.cs` | Gate de estado **sem** dependência do Mirror. Toda a lógica core consulta só isto. Define `ShouldSimulateLocally`, `IsOnline`, `IsServer`. |
| `NetSpawn.cs` | Helper para replicar objetos spawnados pelo servidor (bombas/explosões/itens). No-op offline. |
| `BombermanNetworkManager.cs` | `NetworkManager` custom: seta `NetSync.Mode`, atribui `playerId` por conexão (host = 1), spawna o Player na posição do `PlayersSpawner`, sincroniza `GameSession`. |
| `NetworkPlayerSetup.cs` | `NetworkBehaviour` no Player: replica o `playerId` (SyncVar) e aplica nos componentes; deixa o Rigidbody2D kinematic no cliente. |
| `NetworkPlayerInput.cs` | `NetworkBehaviour` no Player: cliente empacota input local → `Command`; servidor aplica via `SetSyntheticHeld`. |

## Alterações mínimas no código existente

Guards de 3 linhas, todos consultando apenas `NetSync`:

- `MovementController.Update` e `FixedUpdate` — cliente puro não simula.
- `BombController.Update` — só o servidor coloca/detona bombas.
- `PlayersSpawner.SpawnNow` — online, quem spawna é o NetworkManager.

Nenhuma lógica de gameplay foi reescrita.

## Instalação do Mirror

Adicionado via **OpenUPM** em `Packages/manifest.json`:

```json
"scopedRegistries": [
  { "name": "package.openupm.com", "url": "https://package.openupm.com",
    "scopes": ["com.mirrornetworking"] }
],
"dependencies": { "com.mirrornetworking.mirror": "96.6.4", ... }
```

> Nota: a numeração do OpenUPM (`96.6.4`) difere da do Asset Store
> (`96.0.1`). Só existem no OpenUPM as versões publicadas a partir das tags
> do GitHub do Mirror — hoje `96.6.3` e `96.6.4`. Usar uma versão inexistente
> gera erro de "dependência inválida".

Ao abrir o projeto no Unity 6000.4.5f1 (versão original do repositório), o
Package Manager resolve o Mirror automaticamente (precisa de internet no
primeiro import). Alternativa: importar o `.unitypackage` do Asset Store e
**remover** a linha do manifest.

## Wiring no Editor (necessário — não dá para fazer via terminal)

Estas etapas exigem o Unity aberto:

1. **Abrir o projeto** no Unity **6000.4.5f1** (versão original do repo).
   Confirmar que o Mirror importou e o projeto **compila**.
2. **NetworkManager**: numa cena `BattleMode_*`, criar um GameObject
   `NetworkManager` com os componentes:
   - `BombermanNetworkManager`
   - `KcpTransport` (ou `TelepathyTransport`)
   - `NetworkManagerHUD` (só para testar host/join rapidamente)
3. **Player.prefab** (`Assets/Prefabs/Player.prefab`) — adicionar:
   - `NetworkIdentity`
   - `NetworkTransformReliable` (ou Unreliable) — **Sync Direction: Server→Client**
   - `NetworkPlayerSetup`
   - `NetworkPlayerInput`
   - Arrastar o prefab para o campo **Player Prefab** do NetworkManager.
4. **Spawn positions**: o NetworkManager usa o `PlayersSpawner` da cena.
   Manter o `PlayersSpawner` na cena (ele não spawna mais localmente online,
   mas fornece as posições via `GetResolvedSpawnPosition`). Player Spawn Method
   do NetworkManager pode ficar em qualquer valor (a posição é resolvida por nós).

## Como testar a M1

- **ParrelSync** ou dois builds: rode uma instância como **Host** (botão do
  NetworkManagerHUD) e outra como **Client** (endereço `localhost`).
- Esperado: o Player do host aparece nas duas telas; ao conectar o client,
  um segundo Player aparece. Mover no client → o host simula e a posição
  replica para as duas telas. O host move o próprio player normalmente.

## Escopo entregue vs. próximos passos

### ✅ Milestone 1 (nesta branch — código pronto, falta wiring/teste no Unity)
Conexão host/client, spawn de players por `playerId`, movimento
host-autoritativo, encaminhamento de input, sync de posição.
**É a validação do risco arquitetural central.**

### ⏳ Milestone 2 — Bombas, explosões, tiles (próximo)
- Gatear `Bomb.cs` (fuse/kick/punch) para rodar só no servidor; clientes
  renderizam.
- `NetworkIdentity` + registro dos prefabs de bomba/explosão; trocar os
  `Instantiate` de bomba/explosão por `NetSpawn.Server(...)` em
  `BombController` (linhas ~1005, ~2777, ~2910) e no fluxo de explosão.
- **Sync da destruição de tiles**: o `Tilemap` não é replicável por
  NetworkIdentity. Provável abordagem: um `NetworkBehaviour` que envia os
  eventos de destruição/revelação de bloco (coordenada da célula) via
  `ClientRpc`, reaplicando no `Tilemap` dos clientes.

### ⏳ Milestone 3 — Regras/estado de partida
Sync de itens (pickups), mounts/Louie, morte/eliminação, sudden death,
placar de rounds, HUD. Rodam no host; falta replicar o estado visível.

## Limitações conhecidas da M1

- Sem bombas replicadas ainda (M2). No host as bombas funcionam; nos
  clientes, não.
- Fidelidade de animação no cliente depende do que o `NetworkTransform`
  carrega; animação de andar pode precisar de sync dedicado (SyncVar de
  direção / estado) na M2.
- Transporte local/IP direto apenas. Matchmaking/relay (ex.: Steam via
  FizzySteamworks) fica para depois.
