# Screen filters

Options > Video has an optional filter and intensity (0, 25, 50, 75, 100%).
The default remains Off; missing fields in old saves normalize to Off / 50%.

- Scanlines: horizontal scanline modulation.
- CRT Light: scanlines and a gentle edge vignette.
- CRT RGB: the same plus a physical-pixel RGB aperture grille.
- LCD: a grid aligned to the logical game pixels.

## Runtime design

VideoFilterPresenter is installed by the existing pixel-perfect camera adapter.
It lazily creates one non-interactive Screen Space Overlay canvas and a URP HLSL
material. UICameraViewportFitter restricts the draw to the game rectangle in all
three scaling modes. Menus, HUD and lobby receive the same effect; side/background
art does not. Turning the filter Off disables its draw, and the default Off path
creates no canvas/material. Scene unload destroys these per-camera resources.

The shader uses multiplicative blending with zero texture samples, screenshot
captures or intermediate render textures. Its patterns are static and fade near
native resolution to reduce aliasing. These are lightweight display masks, not
full signal/beam simulations: there is no barrel distortion, temporal persistence,
composite-video decoding or multipass bloom. Their cost still depends on viewport
size and GPU fill rate; no cross-device millisecond budget is claimed.

## Research

- https://docs.libretro.com/shader/crt/ — CRT characteristics and lightweight
  approaches such as zfast-crt / yeetron.
- https://docs.libretro.com/shader/handheld/ — LCD grid style references.
- https://github.com/adevra/unity-shader-agent-skills/tree/main/skills/mobile-post-processing
- https://github.com/adevra/unity-shader-agent-skills/tree/main/skills/mobile-shader-optimization
- https://docs.unity.com/en-us/engine/6000.3/manual/post-processing-and-full-screen-effects/urp/custom-post-processing/renderer-feature-full-screen-pass

The external skills were reviewed as design references, not installed as runtime
dependencies. Their older Execute / in-place blit examples are not used. The
implementation is original project HLSL; no Libretro shader source is copied.
