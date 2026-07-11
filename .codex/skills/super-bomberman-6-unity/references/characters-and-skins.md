# Characters and Skins

Use this reference when adding or changing a playable character, palette skin,
character animation, selection preview, or HUD portrait.

## Ownership map

- `Assets/Scripts/Skin/BomberCharacter.cs` defines playable character ids.
  The current characters are Bomberman, Lady Bomber, and Tiny Bomber.
- `Assets/Scripts/Skin/BomberSkin.cs` defines palette/skin ids.
- `Assets/Scripts/Skin/BomberSkinResourceCatalog.cs` owns generated Resources
  paths, sheet names, supported skins, normalization, and fallbacks.
- `Assets/Scripts/Skin/BomberSkinSelectMenu.cs`, `SkinSelectBootstrap.cs`, and
  `SkinSelectFlowRouter.cs` own selection, persistence, and return destinations.
  Battle Mode embeds the same selection component in `BattleModeMenu`.
- `Assets/Scripts/Skin/PlayerBomberSkinController.cs` loads generated sheets
  and applies runtime frames for movement, AFK, cornered, death, end-stage, and
  ability visuals.
- `Assets/Scripts/PlayerPersistentStats.cs` and
  `Assets/Scripts/SaveSystem/SaveData.cs` persist character and skin for players
  1-6. `SaveSelectedSkin()` writes both values.
- `Assets/Scripts/Hud/HudCharacterPortraitCatalog.cs` resolves six portrait
  expressions: default, dead, time-up, cornered, inactivity, and victory.
- `Assets/Scripts/Editor/BomberSkinSheetGenerator.cs` and
  `BomberHudPortraitGenerator.cs` generate derived skin sheets and portraits.

## Resource conventions

- Keep original character sheets and palettes under
  `Assets/Resources/Sprites/<Character>/`.
- Generate runtime sheets under the path returned by
  `BomberSkinResourceCatalog.GetGeneratedResourcesPath(...)` and name them with
  `GetSheetName(...)`.
- Generate portraits under
  `Assets/Resources/Sprites/Portraits/<Character>/<SheetName>/`.
- Treat the original sheet and palette as source assets. Regenerate derived
  sheets and portraits through the Editor tools and preserve every Unity
  `.meta` file.
- `Generate Missing Bomber Skin Sheets` skips existing derived sheets. When a
  source sheet or palette changes, do not assume that command refreshed an
  existing output.
- Do not assume every `BomberSkin` enum value is generated or selectable;
  confirm membership in `BomberSkinResourceCatalog.BombermanSkins` and the
  generator palette list.
- Keep point filtering, sprite slicing/import settings, frame indices, and
  case-sensitive `Resources.Load(...)` paths stable.

## Add a playable character

1. Append the enum value in `BomberCharacter` without renumbering existing save
   values.
2. Add source sheet, palette, generated folder mapping, and sheet suffix to
   `BomberSkinResourceCatalog` and `BomberSkinSheetGenerator.CharacterSources`.
3. Add the portrait source/output mapping to `BomberHudPortraitGenerator` and
   the Resources folder mapping to `HudCharacterPortraitCatalog`.
4. Add the character to `BomberSkinSelectMenu`, update serialized selectable
   lists in `Assets/Scenes/SkinSelect.unity` and
   `Assets/Scenes/BattleModeMenu.unity`, and verify idle, confirm, team-preview,
   celebration, and end-stage animation rules.
5. Update `PlayerBomberSkinController` frame tables and timing for every
   character-specific movement, AFK, cornered, death, end-stage, punch, and
   Power Glove visual that differs from Bomberman.
6. Keep `PlayerPersistentStats` loading/normalization and `SaveData` defaults
   compatible with existing saves for all six players.
7. Verify `PlayersSpawner` applies the selected character, and ensure Boss Rush
   resets preserve it instead of replacing it with the default.
8. Verify normal HUD, Battle Mode HUD, time-up/draw, round-win, match-win,
   Game Over, and end-stage visuals.

## Add or change a skin

1. Append the `BomberSkin` value without renumbering persisted values. Do not
   rename an unlockable value without migrating the stored string key.
2. Keep the skin lists in `BomberSkinResourceCatalog` and
   `BomberSkinSheetGenerator` aligned with palette columns.
3. Update selection ordering, fallback behavior, save normalization, and
   generated sheets for every supported character.
4. If unlockable, update `UnlockProgress`, `AchievementCatalog`,
   `SkinUnlockHintCatalog`, `UnlockToastCatalog`, localized strings, and UI
   icons together.
5. Decide explicitly whether the skin is unlocked by default:
   `SaveSystem.defaultUnlockedSkins` currently uses the generated-skin catalog,
   so adding an entry there also changes normalization of existing saves.
6. Regenerate portraits and verify that every expression resolves through
   `HudCharacterPortraitCatalog`.

## Validation

- Test each affected character with representative default, alternate, and
  unlockable skins.
- Test players 1-6 where persistence or multiplayer selection changed.
- Test Normal Game, Boss Rush, Single Battle, and Tag Battle selection/return
  paths as applicable.
- Test movement, AFK, cornered, death, victory, end-stage, punch, Power Glove,
  mounted state, HUD portraits, and scene reload persistence.
- Report any sheet generation or prefab/scene verification that still requires
  the Unity Editor.
