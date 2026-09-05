# World blackout — Stage 3-4, Room 2

`StageBlackout` retains the Canvas output used by Stage 2-5. Its optional
`World Overlay` output uses a URP transparent mesh bounded by the Room 2
BoxCollider2D. Explosion registration still goes through the existing
BombController spotlight path, including explosion reach and intensity timing.

The Room 2 object in Stage_3-4 has both components configured:

- Darkness alpha: 0.92.
- Sorting Layer: Default; Order in Layer: 100.
- Player vision: 1.5 world units of clear radius plus 0.35 of soft edge.
- All living active players (P1–P6) inside the room contribute circles.
- The mesh stays in the room when the camera switches or scrolls. Its world
  coordinates require no conversion through the pixel-perfect viewport.

To show a whole SpriteRenderer or TilemapRenderer above darkness, use the
same sorting layer and an order greater than 100. Account for a parent
SortingGroup if the object has one.

## Selected pixels only

For a reusable prefab palette, add `BlackoutColorPalette` to the prefab root
and edit `Visible Colors` (up to eight colors) and `Color Tolerance`.
Assign that component to `Palette` on each `BlackoutVisibleParts` entry.
All directions can share one palette while retaining their own regions.
An assigned empty or disabled palette reveals nothing. Without a palette,
the existing inline color settings continue to work.

`Fully Visible Sprites` on an entry bypasses the palette only for the listed
frames. OwlEye's two eye renderers list the eight CoreMechanisms destruction
frames, so its final effect is fully visible while its other poses keep their
palette. The CoreMechanisms `Death` child has its own full-region mask with
no palette; Barrel pillar effects inherit this mask when they clone that child.

Add `BlackoutVisibleParts` to a prefab root or a persistent visual parent,
assign `Assets/BlackoutVisibleParts.mat`, then add entries to `Parts`:

1. `Source`: the original SpriteRenderer. Add each directional renderer when
   an enemy switches between Up/Down/Left objects.
2. `Region`: normalized sprite rectangle, measured from the bottom-left.
   `(x: 0, y: 0.5, width: 1, height: 0.5)` selects the upper half.
3. `Colors`: empty means all pixels in the region. Otherwise select up to
   eight exact sprite colors; start with tolerance 0.01. Region and colors
   are intersected, so repeated colors elsewhere can be excluded.

For eyes, select their rectangle and white/eye palette colors. Include pupil
and outline colors if those pixels should also be visible. Configure each
direction separately; an animation with differently positioned eyes may need
a larger rectangle. Palette filtering alone cannot distinguish two pixels
that have the same color.

The selected pixels follow animation frames, flips, transforms, renderer
visibility and tint. Copies only render over the world blackout and are
clipped to its room bounds. They do not affect the legacy Canvas blackout.
The current implementation targets ordinary Simple SpriteRenderers, as used
by the configured prefabs; it does not reproduce a custom deformation shader
or sliced/tiled sprite geometry.

CoreMechanisms has a `BlackoutColorPalette` on its root containing #F8F800,
#F87800, #F80000 and #F8F8F8. Its Animation mask references that palette
across the entire sprite, with color tolerance 0.001. This follows
all six animation frames. BubbleChip has no blackout exemption.
These prefab settings have no effect in rooms without a world blackout.
Enemy eye masks are authored per enemy, using the entries above; no global
white-color exemption is applied to all enemies.

## Unity visual validation still required

- Enter Room 2 from Room 1; leave for Room 3; scroll the room camera.
- Confirm darkness covers the room and stays within its bounds/safe frame.
- Check circular vision with one player and multiple players, including
  mounted movement, airborne movement, death and room transitions.
- Explode short/long bombs and chain reactions next to walls and destructibles.
- Check CoreMechanisms animation/destruction and its four selected colors.
- Author an enemy eye mask, then check every direction and animation frame.
- Confirm Stage 2-5 still uses its existing Canvas blackout.

No Unity compilation, build or Play Mode validation was triggered during
the implementation, per repository instructions.
