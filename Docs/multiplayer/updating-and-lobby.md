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
