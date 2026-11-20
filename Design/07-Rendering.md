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

## Static world scene (T1.6)

The first world renderer replaces the T1.5 primitive test scene:

- `WorldLoader` parses `newworld.zen`, converts the world mesh through
  `ZenKitMeshAdapter`/`MeshBuilder`, maps materials with `MaterialSetBuilder`,
  composes the VOB tree with `GothicVobTransform`, converts waypoints and resolves
  the `zCVobStartpoint` position/direction.
- World render geometry skips invisible portal and ghost-occluder polygons for
  the visual pass; the BSP/portal data remains available in the ZEN for T1.9.
- The world mesh uses one shared vertex/index buffer. Adjacent submeshes with
  the same material are merged into one draw range: 12,729 converted submeshes
  become about 1,081 draws without duplicating vertices.
- Static VOB visuals resolve `.3DS` names to compiled `.MRM`/`.MSH` files. Each
  unique mesh is uploaded into a `DrMesh` with one `DrMeshPart` per submesh
  (312 unique meshes for 11,546 instances); each instance applies its composed
  engine transform. The world mesh itself remains a `WorldMeshGpu` shared buffer
  because its 1,081 material ranges need one vertex buffer and merged draw
  ranges.
- Materials are drawn with `BasicEffect` for solid surfaces and
  `AlphaTestEffect` for alpha surfaces; `MaterialDesc.IsTwoSided` selects
  `CullNone`. Lightmaps exist in the ZEN, but `MeshVertex` has no lightmap UV
  projection yet, so `DualTextureEffect` is deferred.
- The camera starts at the startpoint plus eye height, looking along its
  converted direction; far plane is 160,000 cm and fog fades the world into the
  sky colour.
- Window resize is handled by updating the camera aspect from
  `GraphicsDevice.Viewport` every draw.
- `GpuTextureCache` still uploads only the top mip.

`--check-world` performs the same CPU load without GPU upload and prints mesh,
VOB, waypoint, bounds, start-point and timing statistics. `--screenshot` renders
the world once into a fixed 1280x720 depth-buffered target and exits; the scene
and camera are static, so repeated runs are byte-identical.

### Debug overlay (T1.6)

`F3` toggles a Myra debug overlay in the top-left corner:

- Source: `Ui/DebugOverlay.xmmp` (a `VerticalStackPanel` with one `Label` per
  line). `myra-export` generates `Ui/DebugOverlay.Generated.cs`; the hand-written
  `Ui/DebugOverlay.cs` owns styling and the label updates and is preserved by the
  exporter. `FieldsVisibility="Private"` keeps the generated label fields private
  to the partial class.
- `Ui/UiRoot` sets `MyraEnvironment.Game`, owns the `Desktop` and renders it after
  the 3D scene.
- Lines: frame FPS, world/static draw calls and submitted triangles. The counters
  are produced by `WorldRenderer` for the current frame and exclude Myra's own UI
  draws.
- Labels refresh at 4 Hz to avoid per-frame string allocations. The overlay is
  hidden by default and is not included in the deterministic `--screenshot`
  output unless it is toggled on.
- `Ui/ControlsHud.xmmp` is a second, always-visible block at the bottom-left. It
  currently lists `F3 - Toggle debug overlay`; future controls go there instead
  of the debug overlay.

### Skinned model VOBs (T1.7)

- `SkinnedModelRenderer` uploads one shared `DrMesh` per `GothicModelAsset` and
  draws every `ModelVobInstance` with a per-instance `DrModelInstance` and
  `AnimationController`. The CPU loader attaches an empty root `DrMesh` before
  constructing `DrModel`, so `DrModel.Meshes` is populated; GPU upload fills that
  mesh with skinned and attachment parts.
- Runtime state uses DigitalRiseModel objects directly, with typed tag payloads in
  `Rendering/DigitalRiseModelTags.cs`: `DrModelTag` stores the asset;
  `DrMeshPartTag` an attachment's bone index; and `DrMaterialTag` the
  `MaterialDesc`. Scene instances live in a private
  `SkinnedModelRenderer.ModelInstanceState` record holding the
  `DrModelInstance`, optional `AnimationController` and world `Matrix`. There are
  no parallel GPU model wrapper classes.
- Skinned bone matrices come directly from
  `DrModelInstance.GetSkinTransforms(part.Skin.SkinIndex)`; the renderer does not
  keep a second per-instance `Matrix[][]`, because `DrModelInstance` already
  caches and updates those arrays.
- Soft meshes use `SkinnedEffect` with `WeightsPerVertex = 4` and per-mesh `DrSkin`
  joints; static MDM attachments use `BasicEffect`/`AlphaTestEffect` and follow
  their named bone.
- Bone matrices are computed from `DrSkinJoint.InverseBindTransform` and
  `DrModelInstance.GetBoneGlobalTransform` without adding GPU meshes to
  `DrModel.Meshes`, so shared CPU models stay immutable after upload.
- The default idle clip is chosen from `S_RUN`/`S_WALK`/weapon variants, then the
  first looping clip. Normal interactive runs spawn an `Orc.mds` idle showcase in
  front of the start point; `--spawn-model <mds>` overrides it and `--screenshot`
  omits it so the deterministic screenshot remains unchanged.
- `SkinnedEffect` has no alpha-test variant; alpha model materials currently use
  alpha blending, not cutout. Hairs and alpha fringes may sort differently until
  a custom skinned effect lands in the visual-parity pass.
- The overlay counters include skinned model draw calls and triangles.

### Forward renderer

`ForwardRenderer` owns the stock 3D effects (`BasicEffect`,
`AlphaTestEffect`, `SkinnedEffect`), camera view/projection state and drawing
statistics. `WorldRenderer` and `SkinnedModelRenderer` only upload/organize
assets and call:

- `DrawMeshPart(DrMeshPart, Matrix, optional bone matrices)`
- `DrawRange(...)` for the merged world mesh ranges

`DrawMeshPart` chooses `SkinnedEffect` when `DrMeshPart.Skin` is set and
`BasicEffect`/`AlphaTestEffect` otherwise, applies materials and counts draw
calls, polygons and static/skinned calls for the frame. `BeginFrame` saves and
applies the depth-stencil, blend, rasterizer and sampler-0 states; `EndFrame`
restores them and leaves statistics available for the overlay. GothicRenderer
exposes the total counters to the debug overlay.

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
| `MaterialAlphaMode.AlphaTest` | `AlphaTestEffect` |
| lightmap present and not disabled | `DualTextureEffect` (texture * lightmap) |
| `MaterialDesc.IsTwoSided` | `RasterizerState.CullNone` |
| transparent / additive / multiply | matching `BlendState` |
| texture scale | `BasicEffect.TextureTransform` |
| texture animation (fps, mapping mode, direction) | animate `TextureTransform` offsets from game time |
| environment mapping | `EnvironmentMapEffect` as an approximation |
| wave (water) | static plane with the water texture in R2; vertex waves in R4 |
| fog | `BasicEffect.FogEnabled` with scene fog color/range |
| `IgnoreSun` | exclude from directional light (requires a custom effect; defer) |

Per-material draw state is cached; never construct effects or states per frame.

Missing textures produce a cached magenta placeholder, never a crash. Texture
resolution tries the exact name, `<stem>-C.TEX` and `<stem>.TEX`
case-insensitively. Texture animation frame names follow Gothic's suffix
convention; resolve existing frames only and log missing ones once.

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
  Low-LOD polygons are skipped; the highest LOD is kept.
- Keep portals, occluders and ghost occluders in the converted mesh; they are
  visibility data and culling/portal logic decides what to draw in T1.9. Do not
  discard geometry during conversion.
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

- `F3` debug overlay: current-frame FPS, world/static draw calls and submitted
  triangles (see T1.6 above), plus frame-time logs. Visible objects and texture
  memory come later.
- Screenshot command (`--screenshot <path>`) writes a PNG with a fixed
  camera setup; screenshots are the evidence for visual tasks. `--screenshot`
  uses a fixed 1280x720 render target and exits after writing the file.
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
