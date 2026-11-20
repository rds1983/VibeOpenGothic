# Rendering

## Priority

Rendering is the lowest-priority subsystem. Gameplay, data, scripts and physics
come first. Until the game is playable, use MonoGame stock effects and do not
build a custom shader pipeline.

Renderer ladder:

| Stage | Scope | Effects |
| --- | --- | --- |
| R0 | Debug shapes, test scene | `BasicEffect` |
| R1 | World and NPCs visible | `BasicEffect`, `AlphaTestEffect`, `SkinnedEffect`, `DualTextureEffect` |
| R2 | Materials, fog, sky, day/night, water (static) | Stock effects + UV transforms |
| R3 | Particles, decals, distant detail, shadows (optional) | Custom or stock |
| R4 | Visual parity pass | Custom shaders only where stock effects cannot express the material |

Each stage must be replaceable without touching gameplay code.

## Platform and device

- MonoGame DesktopGL, `GraphicsProfile.HiDef`.
- Fixed backbuffer; UI rendered at full resolution while the 3D scene may render
  at a lower scale (T5.1).
- VSync on by default; expose window mode, resolution and AA in settings.
- One `GraphicsDevice` owned by `GothicGame`; `Rendering` is the only layer
  allowed to create GPU resources.

## Frame structure

```
Update:
  session.UpdateLogic(dt)          // world, AI, scripts, physics, animation
  renderer.Prepare(session, camera) // culling, batching, bone matrices

Draw:
  1. opaque world (static batches)
  2. opaque models (skinned/static VOBs)
  3. decals and transparent objects (sorted back to front)
  4. particles and visual FX
  5. sky
  6. UI (Myra)
  7. debug overlay (FPS, counters) if enabled
```

Do not mutate gameplay state in `Draw`. Bone matrices are snapshotted from the
animation layer during `Prepare`.

## Camera

- Position, yaw/pitch/roll, vertical FOV (default 67.5 from `Gothic.ini`),
  near ~1 cm, far tuned to fog distance (start 20,000 cm).
- Third-person orbit with collision; first-person for free look/dialogs; debug
  free camera in marvin mode.
- Camera smoothing and collision use physics raycasts with the `Camera` mask.
- Screen shake from scripts is an additive offset applied at the end.
- Coordinate precision: float is sufficient at centimetre scale; no
  camera-relative rendering initially.

### Handedness

Gothic data is left-handed; the renderer never deals with it. All geometry,
transforms and animations are converted to MonoGame's right-handed space at load
(see [03-Models-And-Skinning.md](03-Models-And-Skinning.md)), including reversed
triangle winding. Consequences:

- The camera uses MonoGame's right-handed `CreateLookAt`/`CreatePerspectiveFieldOfView`
  directly; there is no mirrored view matrix.
- Culling uses the normal `CullCounterClockwise` state. If a model is
  inside-out, the mesh conversion is wrong, not the renderer.
- Verify with an asymmetric textured model (a readable sign) and a VOB chain:
  text must be readable and silhouettes must match the source data.
- No per-draw or shader-side sign flips are allowed.

## Materials and stock effects

`ZenKit.Material` maps to a small `MaterialDesc` used by the renderer:

| Gothic property | Stock effect mapping |
| --- | --- |
| color, texture | `BasicEffect.DiffuseColor`, `.Texture` |
| `AlphaFunction` != none | `AlphaTestEffect` with the closest compare function |
| `DisableLightmap` false and a lightmap exists | `DualTextureEffect` (texture * lightmap) |
| two-sided material | `RasterizerState.CullNone` |
| transparency / additive | `BlendState.AlphaBlend` / `.Additive` |
| texture scale | `BasicEffect.TextureTransform` |
| texture animation (fps, mapping mode, direction) | animate `TextureTransform` offsets from game time |
| environment mapping | `EnvironmentMapEffect` as an approximation |
| wave (water) | static plane with the water texture in R2; vertex waves in R4 |
| fog | `BasicEffect.FogEnabled` with scene fog color/range |
| `IgnoreSun` | exclude from directional light (requires a custom effect; defer) |

Per-material draw state is cached; never construct effects or states per frame.

Missing textures produce a cached magenta placeholder, never a crash. Texture
animation frame names follow Gothic's suffix convention; resolve existing frames
only and log missing ones once.

## Lighting and sky

Gothic's lighting is mostly baked:

- World polygons reference lightmaps (`Polygon.LightMapIndex`), rendered with
  `DualTextureEffect`.
- The sun is a directional light; ambient color and fog come from the sky
  controller and world zones.
- Dynamic point lights (torches, spells) are limited in R2 to a small set of
  the nearest lights; full light support comes with custom effects in R4.
- Sky: gradient dome, sun/moon position, stars and clouds from the sky
  controller. First pass: a solid fog-colored clear + a colored dome.
- Day/night advances with the session clock; colors are interpolated from the
  sky controller's keyframes.

## Static geometry and batching

- Convert each world mesh polygon into triangles grouped by
  (material, lightmap). Build one immutable vertex/index buffer per group.
- Skip portals, occluders and ghost occluders as render geometry; they are
  visibility data.
- VOB static meshes are instanced by transform (draw the same buffer with
  different world matrices) where materials match.
- First pass: no BSP/portal culling. Add frustum and distance culling in T1.9.
- Never rebuild world buffers per frame or per camera move.

## Skinned models

- One `SkinnedEffect` instance per material combination; bone transforms are set
  per draw from `DrModelInstance.GetSkinTransforms`.
- `WeightsPerVertex = 4`; see [03-Models-And-Skinning.md](03-Models-And-Skinning.md)
  for the 72-bone limit and the fallback plan.
- Attachments use the owning node's global transform and a static or skinned
  effect as appropriate.
- "Fatness" (NPC body scale) is a per-instance shader parameter or a CPU-side
  offset; do not duplicate meshes per NPC.

## Transparent objects, decals and particles

- Transparent geometry sorts by distance to camera; split large submeshes if
  sorting artifacts are unacceptable.
- Decals are projected quads with polygon offset; stock `BasicEffect` with
  alpha blending in R2.
- Particle effects are CPU-simulated quads in R3 (PFX definitions are data).
  Keep the simulation independent of the renderer so it can be tested headless.

## UI rendering

Myra renders after the 3D scene with its own batch and uses FontStashSharp for
text. The initial UI uses a default FontStashSharp font; Gothic `.FNT` fonts are
an optional parity feature (see
[08-UI-Audio-Video.md](08-UI-Audio-Video.md)) and do not block gameplay.

## Diagnostics

- FPS/frame-time overlay, draw calls, triangles, visible objects, texture memory.
- Screenshot command (`--screenshot <path>` or F12) writes a PNG with a fixed
  camera setup; screenshots are the evidence for visual tasks.
- A null renderer lets headless tests run sessions without a `GraphicsDevice`.
- Debug wireframes: collision meshes, VOB bounds, waynet, skeleton bones.

## Risks

- Stock effects cannot express all Gothic material behavior (alpha test + fog +
  texture animation + two-sided in one pass). Accept visual differences until R4.
- `SkinnedEffect` shader permutations and bone counts can cause hitches; warm up
  effects at load.
- Batching by material can create huge buffers for a world; build incrementally
  and reuse arrays to avoid LOH pressure.
- Gamma: Gothic textures are authored for a specific gamma. Use the MonoGame
  default until a color-management task exists; do not add ad-hoc gamma tweaks
  per texture.
