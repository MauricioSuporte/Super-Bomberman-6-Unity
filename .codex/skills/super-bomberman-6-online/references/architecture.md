# Current architecture

Snapshot: 2026-10-12, following Mirror PR #7 and the online integration.
Confirm these symbols in the active checkout before editing.

## Flow and owners

| Concern | Source and responsibility |
|---|---|
| Title | `Assets/Scripts/TitleScreen/TitleScreenController.cs` and `TitleScreenBootstrap.cs`: Battle Mode -> Offline first / Online second; Online routes to `OnlineLobby` |
| Network lifecycle | `Assets/Scripts/Netcode/BombermanNetworkManager.cs`: connection-to-slot map, human spawn, timer/round messages, next round, dedicated server |
| Room | `Assets/Scripts/Netcode/OnlineRoomSession.cs`: manager partial, roster/config/character/pause messages, turn input validation, COM spawn, readiness and presentation acknowledgements |
| Lobby UI | `Assets/Scripts/Netcode/OnlineLobbyMenu.cs`: runtime uGUI/TMP Host/Join/IP/Configure/Leave; visible only in `OnlineLobby`; releases its owned EventSystem on transitions |
| Configuration | `Assets/Scripts/BattleModeMenu/BattleModeMenu.cs` and `OnlineBattleModeMenu.cs`: host runs native flow; clients apply native panels from `OnlineView` without editing rules |
| Characters | `Assets/Scripts/Skin/BomberSkinSelectMenu.cs` and `OnlineCharacterSelection.cs`: embedded sequential selection, per-slot cursors and remote input only for the owner of the current turn |
| Settings | `Assets/Scripts/SaveSystem/OnlineBattleSettings.cs` and `SaveSystem.cs`: temporary `BattleData`, `BattleConfigurationData`, whitelist and redirected getters/setters |
| Player selections | `Assets/Scripts/Skin/OnlinePlayerSelections.cs`, `PlayerPersistentStats.cs`, `NetworkPlayerState.cs`, `PlayerBomberSkinController.cs`: per-slot character/skin, base/runtime cache and initial spawn state |
| Input | `NetworkPlayerInput.cs`, `NetworkPlayerSetup.cs`, `PlayerInputManager.cs`: owned commands, synthetic input and local P1 translated to assigned room id |
| Results | `Assets/Scripts/GameManager.cs`: server verdict, deferred check and shared local victory/draw presentation |
| Pause | `Assets/Scripts/Pause/GamePauseController.cs` and `OnlineGamePause.cs`: host input, scene-scoped pause/menu/confirmation and coordinated exits |
| Text | `Assets/Scripts/Localization/OnlineRoomText.cs`: four languages and TMP Japanese fallback |

Slots take the first free P1-P6 id and stay stable when a participant leaves.
Six occupied slots reject another connection. Connected slots are fixed Man;
the host assigns COM/Off to empty slots. Joining is restricted to the lobby;
configuration requires two participants. `OnlineRoomMode` names Battle Mode,
Normal Game and Boss Rush, but the latter two are future work.

## Actual simulation and presentation

`NetSync` is the mode bridge. Old comments and the original roadmap describe
a stricter server-only model than current human movement.

- Human owners simulate/predict movement locally. Transform and normal player
  animation send owner output ClientToServer; gameplay decisions stay on the
  server. Do not imply that movement validation/reconciliation is complete.
- `PrepareOnlinePlayer` configures COM local simulation, player animation and
  transform ServerToClient. Audit mounts/eggs separately; configuring the player
  does not configure every companion's authority.
- `NetworkPlayerState` replicates life, loadout, character, skin, abilities,
  temporary FX, mount, death, glove and elimination. Remote static caches serve
  the HUD and rendering, not independent gameplay decisions.
- `NetSpawn` mediates runtime spawn/despawn. `NetworkBombFx` replicates explosion
  visual cells and cleared destructible cells through RPCs. Pooled explosions
  remain local visual objects; damage is server-only. Keep the host guard
  against duplicate VFX. Non-explosion tile changes need their own event path.
- Replicate active renderer/frame, not just facing/moving. Owner animation,
  remote manual frame application, COM output and locally replayed victory are
  different paths.

## Room settings and selections

`BeginOnlineBattleSettings()` clones settings for the room. The snapshot's
explicit whitelist transfers rules, stage, teams, item/Louie amounts, handicap,
music, character and skin ids. Controls, language, campaign saves, achievements
and unlocks remain local. Online setters use the temporary copy and skip disk
writes. Ending the room restores offline settings.

Apply selections to both base and runtime caches before spawn. Initial
`NetworkPlayerState.OnStartServer()` sampling includes selected character/skin.
Receiving a remote skin uses generated-catalog normalization rather than the
observer's unlock clamp. Preserve validation of choices in the selection flow.

`OnlineView` carries native screen, selected options, working values, restore
confirmation, cursor frame, sound sequence/volume, start cursor position/frame,
blink and fade visibility/alpha. Configuration snapshots refresh at 30 Hz,
other room snapshots at 10 Hz. Clients observe native panels; selection input
is validated against the sender's connection slot and active selection turn.

## Round lifetime and pause

The server registers a victory once; `RoundOverMessage` carries absolute
scores, active ids, surviving winner ids and `matchComplete`. Capture the
previous HUD score before incrementing/applying the new score.

Each peer freezes gameplay and owner prediction, then runs shared victory/draw
routines. Winners switch manual renderers back to local End Stage animation;
network animation and glove/mount/ability updates must not overwrite that pose.
Preserve victory SFX/voice, fades, scoreboard and the Win Match prefab.

Clients acknowledge presentation completion. The host currently waits at most
five seconds before `ServerChangeScene` to another round or Stage Select.
Room settings survive; cumulative wins survive ordinary round reloads and
reset for a new match. A disconnect during configuration/play returns remaining
peers to the lobby. Closing the host returns clients to Title.

`roundSpawnedPlayerIds`/`roundPlayersReady` reset in `OnServerChangeScene`.
Human and COM paths register their slots; readiness latches after all configured
slots have spawned. Later death/despawn must not make the round unready.
`ShouldDeferBattleVictoryCheck` also respects the four-second grace and Revenge
swap. The pending death check retries after the gates clear.

Pause networking uses unscaled time at `Time.timeScale == 0`. Only host input
toggles/navigates pause; receivers apply state only for the matching scene.
Ignore gameplay action commands during pause/result presentation. Pause exit
routes use Mirror coordination rather than local loads on each client.
