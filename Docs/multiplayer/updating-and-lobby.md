# Updating and lobby controls

The title's highlighted **Update game** entry and the lobby's incompatible-version
action share `GameUpdateController`. Discord opens the same invite from Options
and the browser footer. Options keeps Discord directly above Reset Save.

## Matching builds and publication

`OnlineBuildStamp` writes the Git commit into `Resources/OnlineBuildIdentity` for
every build target, including manually exported Android Build Profiles. Clients
must still match the directory/server protocol and source identity exactly.
An old Android APK is not exempted from compatibility checks.

The complete online release builds the Linux server and Windows, Linux and
Android clients from one checkout, before deployment. The release body keeps
the existing `sb6-update` pointer and adds `linuxUrl`, `linuxSha256`, `androidUrl`
and `androidSha256`. Immutable packages are uploaded before changing that pointer.
The README buttons use stable aliases in the `online-v0.5.0` channel:
`SB6-Windows.zip`, `SB6-Linux.zip`, `SB6-Android.apk`. They become available after
the first complete publication of these changes; no assets are published merely
by editing this checkout.

Actions uploads each client separately: `SB6-Windows-<timestamp>`,
`SB6-Linux-<timestamp>` and `SB6-Android-<timestamp>`. Linux ZIP and Android APK
must not share an upload step. `ReleaseProfileTests` guards that separation.

Validation on 2026-11-15: RoomDirectory 16/16 and CI 219/219 tests passed,
including repository protocol alignment and platform artifact separation.
The full Unity EditMode baseline ran in the open Editor: 1179/1180 passed;
`OnlineRoomEditModeTests.ClownIntroResetKeepsItsAlreadyRunningCoroutineAlive`
failed before runtime changes. No production C# or Unity content change remains.

USB diagnosis on Samsung SM-G781B / Android 13 identified the installed APK as
source `392dd3326f967eecdebdc84c719efdee78914b55` (2026-11-08). Original
process remained alive on a dark screen in Vulkan and forced OpenGL ES attempts;
no fatal crash for the original package was captured. Root cause is unconfirmed.
An isolated, locally signed debuggable copy of that APK is installed as
`com.mauriciosuporte.sb6diagnostic00`; game code/assets are unchanged, manifest
and resource package differ. It is a diagnostic copy, not a corrected release.
Native capture awaits the user unlocking the sleeping phone. A new instrumented
APK build failed when the local Editor exhausted memory during a concurrent CI
build; texture allocation errors preceded its crash. Temporary instrumentation,
build settings and generated serialization changes were archived and restored.
Logs, APK identities, test results and cleanup manifest are retained locally in
`Build/AndroidDiagnosis/`. Real offline/online Android acceptance remains pending.

Android startup follow-up (2026-11-20): native sampling on the same phone
found the main thread in `libz.inflate` during a long boot. The original signed
APK remained dark at 66.7 seconds and showed its title/menu at 104.5 seconds in
a fresh process. `BomberSkinResourceCatalog` previously loaded all character
textures for roster discovery and loaded sprite sheets to check palette
existence during P1-P6 save normalization. These queries now use
`Resources/BomberSkinCatalog.txt`, refreshed by the existing skin generator and
build preprocessor from importer sprite metadata without loading textures.
Custom character ids, sort order, resource paths, skin fallbacks and protocol
remain unchanged.

The corrected release-mode IL2CPP APK built successfully in Unity 6000.6.0f1
(ARM32/ARM64, zero build errors; five existing engine/Mirror/TMP warnings), and
was installed on the Samsung SM-G781B, Android 13. First intro graphics were
captured at 3.16 seconds and the title/menu was verified at 33.38 seconds. These
are observations, not an exact first-menu timestamp; the normal intro plays in
between. APK SHA256:
`e04859026ba24fada3cc5ceff85aa7a9ec2d84c399ca607950f6461295aae7f3`.
It uses the local debug signing key, version 0.5.0/code 1, and is a test artifact,
not a published update. The user authorized removal of the original installation
and its test saves; production signing in CI is unchanged.

Real-device checks passed for fresh save creation, character/palette selection,
WorldMap, Stage_1-1, pause/resume and a normal cold relaunch to the menu. A local
dedicated server plus two real clients (current-source Editor owner/observer and
corrected Android client) entered BattleMode_1 with four server-controlled COMs.
Both clients rendered the selected characters and colors; Android movement and
bomb placement, death presentation, synchronized pause/resume and room teardown
were observed. USB carried unchanged KCP packets through a temporary loopback
relay. The dedicated executable is the published Windows baseline at source
`6cbb452506cfb9338592718dfd1edba5da393ee6`; the Editor and APK add the uncommitted
catalog optimization. All report protocol 74 and the matching commit identity;
no compatibility gate was disabled. This is interoperability evidence, not a
claim of identical DLLs across roles. The Android-as-admin path, host with the
corrected APK, online campaign/Boss Rush and natural repeated-round completion
remain outside this manual sample.

Both Python suites passed (RoomDirectory 16, CI 219, including the real protocol
regression). All six new catalog cases passed; a final targeted run also passed
the previously failing Clown and Lobby cases (8/8). The first full EditMode run
was 1181/1186, with five failures; all five passed isolated repeats. A final full
baseline repeat finished 1182/1186: the three domain-reload failures persisted
(ThreeRoomBrowser, LobbyCanHostAndReturnToTitle, MinecartNativeCancellation), as
did the mount-death frame timing assertion. All six new catalog tests passed.
Curl timeout 28 and MCP reconnect messages accompanied reloads; these results
do not establish a fully green baseline. Their cause remains pending, and the
isolated passes do not override the full-run failures. Logs, screenshots, identities, raw Unity test results
and cleanup records are in `Build/AndroidDiagnosis/2026-11-20/`. Phone USB stay-awake,
Editor worker count, save isolation and player name were restored; the relay,
temporary server and original-code diagnostic app were removed/stopped.

The runner needs Unity Linux Dedicated Server, Linux Player and Android build
support (SDK/NDK/OpenJDK). Android publishing requires a permanent signing key:

- `SB6_ANDROID_KEYSTORE`: path on the self-hosted runner to the existing keystore.
- `SB6_ANDROID_KEYSTORE_PASSWORD`, `SB6_ANDROID_KEY_ALIAS`, `SB6_ANDROID_KEY_PASSWORD`:
  repository secrets, never committed to source.

Use the same package id and signing key as previously distributed APKs. Android's
installer rejects updates signed by another key. The complete CI build fails
before deploying if no permanent Android key is configured. Local test builds can
continue to use Unity's debug key. Android version codes increase with UTC minutes.

## Installation

Windows starts the temporary updater helper, waits for the old game to exit,
checks the package checksum, installs with the existing rollback, and starts the
updated executable automatically.

Linux ships `GameUpdater.py` alongside `SB6.x86_64`. It requires Python 3.9+,
copies itself out of the installation, waits for the game to exit, verifies SHA256
and paths, backs up managed files, rolls back on a failed copy, and restarts the
game. Files outside `client-files.json`, including user saves, are preserved.
The temporary `SB6Updater-*` directory holds `update.log` if installation fails.
For the first ZIP install, extract and run `chmod +x SB6.x86_64` before launching.

Android downloads and verifies the matching APK in the app cache. The installer
activity runs in its own `:updater` process so Unity can exit without terminating
installation. It uses a PackageInstaller session, opens the required Android
confirmation and attempts to relaunch the game on success. Unknown-source
permission may need to be enabled once; return to the game and press Update again.
System/OEM restrictions can require the player to tap Open manually after install.
No uninstall is performed and the app's save directory is retained.

## Rooms and input

The supervisor already defaults to capacity **3**, and the service explicitly
passes `--capacity 3`. Its lock protects allocation, including rooms that are
starting, full, or in a match. `/health` advertises that capacity and `/rooms`
lists all live rooms. The browser fits three rows, labels unavailable rooms and
blocks joining them. It disables room creation at capacity; the server remains
authoritative if two clients race to create the last room.

Default room names follow the current player's name (`Mauricio's Room`). Custom
room names allow up to 32 letters, digits, spaces, apostrophes, hyphens and
underscores; player names retain the existing 16-character validation.

While editing a name or password, physical keyboard gameplay bindings are ignored.
Enter confirms and Escape cancels. Configured controller/mobile Start confirms,
B cancels, and controller directions retain character/digit selection. Android
opens its native keyboard. The touch CHAT button between the directional control
and L opens that keyboard for both public lobby chat and authenticated room chat.

`OnlineLobbyInputEditModeTests` covers keyboard binding collisions, password
entry, default names, visible non-joinable matches and the full-directory gate.
With a graphics device, it also renders the runtime lobby with three representative
local room entries to `Captures/OnlineLobby/three-rooms-discord.png`; these are
preview entries and do not allocate public server rooms.

The creation mode picker omits locked Boss Rush in both directions. This restriction does not apply to joining or starting an existing room, including when its administrator changes. Three 24-pixel rows use equal four-pixel outer vertical padding inside the 92-pixel room viewport.
