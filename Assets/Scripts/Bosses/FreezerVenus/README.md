# Freezer Venus

The `FreezerVenus` prefab is referenced by `World3HallStageSevenSequence` in
`Stage_World3Hall`. Stage 3-8 is a hall encounter, not a separate scene. After
Pretty Bomber rises in her death pose, the hall waits for `FreezerVenusIntro`
before restoring player movement, colliders and bomb placement. During absorption, Pretty Bomber renders in front of Venus and the beam, blinks progressively faster, fades out and becomes nearly white with a pale yellow tone through a URP 2D sprite shader.

The boss descends with closed eyes, extends and widens a light cone, lifts and
fades Pretty Bomber, retracts the cone, opens her eyes and smiles. All new timers
and movement respect pause. The transparent yellow beam is a Point-filtered 16 PPU pixel texture from the red crown gem to the original portal floor. Descent stops one tile higher and starts shortly after Pretty emerges.

Combat starts with exactly 10 HP in every difficulty. A hit grants 0.85 seconds
of invulnerability. She flies in eight directions over pillars and bombs between attacks, choosing upper-half destinations on four of every five trips and periodically approaching the closest living player. Destinations exclude indestructible and destructible tiles so she stops on attackable floor; flight still crosses obstacles and bombs. Her hitbox is at her feet, aligned with a black pixel shadow that grows in integer pixel steps during descent and blinks together with her body on death. All positions snap to 1/16 world units while movement retains fractional progress internally. Casting starts immediately upon reaching a destination. Ice preparation distributes the ten configured Ice Cast frames evenly across 1.1 seconds (0.11 seconds each); after firing, she stays still for another 1.1 seconds, holding DollCast4 during firing and for 0.94 seconds after it, then showing DollCast0 for the final 0.16 seconds of recovery. Attacks:

- A 5-unit/second tornado born at the boss's ground tile. Its visual sits half a tile above the ground collider. It waits one second, then follows tile centers toward the nearest living player. Breadth-first routes avoid missing ground, indestructibles, destructibles and bombs. Turns pause for 0.18 seconds; a bomb appearing in an active segment makes it pause and return to the previous center before rerouting. It expires after six active seconds or an explosion.
- Six downward ice shards at 7.6 units/second, rotating 90 degrees every 0.25 seconds. Ice uses the SunMask star's centered radius-0.3 circular trigger and passes through pillars, bombs and explosions.
- Four invocations, born simultaneously in the nearest four clear columns after the summon effects. Each uses FreezeVenusInvocation.png at native 16 PPU, walks exactly one tile down with frames 1,2,1,3, then stops for one jump sequence: 4,5,6,7,8,7,6,5,4 followed by the three 32x32 frames in the second row. The sequence runs once in 0.2 seconds. The invocation then holds the third second-row sprite and immediately advances down at a constant 6.6 units/second (3x its initial walking speed), without acceleration. Explosions hold the ninth first-row sprite for five 0.1-second frames, then play the shared SmallEnemiesDie finish used by Funya. Bomb or wall contact disables damage immediately and plays Summon_1..5 at 0.1 seconds per frame. On bomb contact, invocations also kick downward through Bomb.StartKick with the same blocking masks and parameters used by BombKickAbility.

Ice, IceCast and SummonCast each use a configurable 3x one-shot gain, multiplied by the player SFX setting.

Ice flies across interior pillars; grounded attacks obey the grid. All attacks expire at the arena boundary or their lifetime limit and are removed on boss death/disable. Player hits use the existing mount
damage path. Defeat lasts six seconds plus the final explosion lifetime. Each explosion plays once, uses the encounter's pause-aware clock and is destroyed at the end. It uses the existing boss explosion sprites and `BossEndStageSequence`,
records the virtual stage `Stage_3-8`, and follows the current campaign ending
through `GameManager`, without introducing a separate save mechanism.

## Authoring

`Tools > Bosses > Rebuild Freezer Venus` imports the supplied sheet and rebuilds
the prefab. Sprite IDs remain stable on repeat imports. Import settings are
16 PPU, Point filtering, no mipmaps, Default compression None and no platform
overrides. The original PNG is copied intact; sprite rectangles exclude the
sheet's annotations. `Wire Freezer Venus To Open Hall` updates the reference in
the open World3Hall scene and saves it.

The prefab exposes descent, light and absorption durations, movement speed, sprite sequences and audio. Shared damage, explosion and
victory audio come from the SunMask prefab. Intro timing is independent of the
music's length.

## Research and adaptation

Requested references:

- https://bomberman.fandom.com/wiki/Freezer_Venus
- https://bomberman.fandom.com/pt-br/wiki/Freezer_Venus
- https://www.youtube.com/watch?v=u-q3SatZVh0

Those URLs could not be retrieved by the research tool during implementation;
the video was not viewed. The accessible behavior description at
https://game.bad-person.net/gamenote/superbom3/superbom3_boss5.html describes
pursuit, destructible homing tornadoes lasting about six seconds, six downward
ice shots and dolls that kick bombs. The supplied sprite sheet also contains
the corresponding casting and tornado frames.

This implementation adapts those mechanics to this project's hall: an explicit
three-attack cycle makes every pattern available, and the small copies use
reduced native-pixel boss sprites from the supplied sheet. The requested Pretty Bomber
absorption and 10 HP are intentional project-specific choices, not claims of
frame-perfect reproduction of Super Bomberman 3.

## Validation

Four NUnit checks cover routes around pillars, sealed corridors, the nearest four open lanes and blockers above the spawn point. They were invoked directly through Unity MCP and passed. The current Unity Test Framework runner returned zero discovered tests for this predefined assembly. Live Play Mode also verifies the intro pixel texture, crown origin, foot hitbox and shadow.
