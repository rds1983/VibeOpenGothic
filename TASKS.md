# Roadmap

This file is the task queue. The workflow for selecting, finishing and
committing tasks is in [AGENTS.md](AGENTS.md). Documentation lives in
[Design/](Design/README.md).

## Status legend

| Marker | Meaning |
| --- | --- |
| `[ ]` | Not started |
| `[~]` | In progress (at most one task) |
| `[x]` | Finished and explicitly confirmed by the developer |
| `[!]` | Blocked; the blocker is written under the task |

Task IDs are stable. Do not renumber tasks; add new ones with a suffix
(`T1.3a`) or at the end of the milestone. Agents may reorganize the remaining
(unchecked) tasks freely after a task is confirmed.

## Definition of done

A task is done only when:

- The behavior described under "Done when" is implemented.
- `dotnet build` and `dotnet test` pass without warnings.
- New logic has unit tests; data-dependent behavior has diagnostics or a
  skipped-by-default integration test.
- Relevant documents in `Design/` are updated.
- The developer has explicitly confirmed the result.

## Current focus

**T2.1 - Static collision world.** T1.9 is complete.

> T1.9 adds frustum/distance culling and overlay counters; the start view dropped
> from 23,118 to 2,063 draw calls. T2.1 starts the Bepu static collision world.

## Milestone overview

| Milestone | Outcome |
| --- | --- |
| M0 Foundation | Builds, runs, finds Gothic, mounts its data, has CI |
| M1 Data and static world | Converts meshes/models/textures, renders a real world and NPCs |
| M2 Movement and world interaction | Walking with collision, waynet AI, doors/triggers, dynamic props, usable HUD |
| M3 Scripts and gameplay | Daedalus boot, NPCs, dialogs, combat, magic, quests, saves |
| M4 Presentation | Functional Myra UI, audio, visual FX, video; optional original UI/visual parity last |
| M5 Hardening | Performance, mods, settings, packaging, full-game parity |

---

## M0 - Foundation

- [x] **T0.1 Solution scaffold** (2026-11-20)
  Create `VibeOpenGothic.slnx`, `VibeOpenGothic/VibeOpenGothic.csproj`
  (WinExe, net8.0), `VibeOpenGothic.Tests/VibeOpenGothic.Tests.csproj` (xUnit),
  `Directory.Build.props` (nullable, warnings as errors, deterministic builds),
  `Directory.Packages.props` (central versions), and reference MonoGame
  DesktopGL 3.8.0.1641, Myra, DigitalRiseModel.MonoGame, ZenKit 1.3.0-cs.1,
  BepuPhysics 2.4.0. Add `.editorconfig`. `RuntimeIdentifiers` must include
  `win-x64;linux-x64;osx-x64` for ZenKit's native copy targets.
  **Done when:** `dotnet build VibeOpenGothic.slnx` and `dotnet test
  VibeOpenGothic.slnx` succeed on a clean clone; the test project contains
  scaffold smoke tests that load every required package; README build commands
  work.

- [x] **T0.2 Application shell** (2026-11-20)
  MonoGame `Game` subclass with a fixed-step logic loop and render loop; window,
  exit handling, frame-time counters. Command line parser with `--help`,
  `--version`, `--probe`, `--devmode`, `-g <path>`, `-game:`, `-window`, `-w`,
  `-save`, `-g1/-g2/-g2c`, unknown-argument warnings. Logging to console and
  `%LOCALAPPDATA%/VibeOpenGothic/log.txt` with categories; settings JSON load and
  save; crash handler that writes the log path and shows a dialog.
  **Done when:** running with no Gothic installation shows a readable error, not
  a stack trace; `--help` works; argument parser and settings round-trip are
  unit tested.

- [x] **T0.3 Install discovery and VFS** (2026-11-20)
  Locate the installation (CLI, `VOG_GOTHIC_PATH`, settings, known paths),
  validate it, detect Gothic version, mount VDFs and `_work` with ZenKit using
  the ordering in Design/02, handle mods via `modfile.ini`, and normalize paths.
  Directory and file names must be resolved case-insensitively: a real Steam
  install uses lowercase `system`, which currently only works on Windows because
  NTFS ignores case. Add `--probe` output: version, archives, file counts,
  resolution of `GOTHIC.DAT`, `newworld.zen`, a sample texture and model.
  Abstract the VFS behind `IGameVfs` with an in-memory fake for tests.
  **Done when:** `--probe` succeeds on a real install; data-free unit tests cover
  version detection from a fake install layout (including `system`/`System`),
  mount ordering and mod file filtering; no Gothic types leak outside `Data`.

- [ ] **T0.4 CI and publishing**
  GitHub Actions workflow: restore, build Release (warnings as errors), run
  tests, publish win-x64 self-contained, upload artifacts. Use the .NET 10 SDK
  (or 9.0.200+) and pin it explicitly; the .NET 8 SDK cannot read `.slnx`.
  Ensure ZenKit native libraries are present in the publish output. Add a `dotnet
  test` TRX artifact.
  **Done when:** CI is green on a clean runner without game data; the published
  zip starts and prints `--version` on Windows.

## M1 - Data and static world

- [x] **T1.0 LHS-to-RHS conversion layer** (2026-11-20)
  Implement `Core.CoordinateSystem` for the Gothic (left-handed, Y-up) to engine
  (right-handed, Y-up) conversion: points, directions, normals, matrices,
  quaternions, scalar yaw and triangle winding. It is a reflection across the XY
  plane (`z` negated, winding reversed), applied exactly once at load.
  **Done when:** unit tests cover involution, matrix/quaternion equivalence, the
  normal/winding relationship and double-conversion detection; the helper is the
  only place in the codebase that flips an axis.

- [x] **T1.1 Mesh conversion core** (2026-11-20)
  Convert `ZenKit.Mesh` (world/static) and `MultiResolutionMesh` (models) to
  engine `MeshData`: positions, normals, UVs, submeshes, material indices,
  triangle indices, bounds. Convert all geometry to engine RHS with T1.0
  (including reversed winding and reflected normals). Skip degenerate triangles,
  count invalid polygons, and handle non-triangular polygons by fan
  triangulation. Keep the highest LOD. Add `--check-meshes` to convert every ZEN
  world mesh reachable from the VFS and report counts/failures.
  **Done when:** synthetic triangles, quads, fans, UV seams, material/lightmap
  groups and degenerate inputs convert correctly in RHS; UVs are preserved;
  `--check-meshes` runs on a real install without crashes and reports triangle
  counts; `MeshData` has no ZenKit or GPU dependencies. Visual orientation/text
  verification happens in T1.5.

- [x] **T1.2 Textures and material mapping** (2026-11-20)
  Decode `.TEX` to RGBA (use ZenKit's RGBA mipmaps), build a texture cache with
  placeholder fallbacks, resolve texture names case-insensitively, and map
  Gothic materials to `MaterialDesc` (color, texture scale, alpha function,
  two-sided, lightmap presence, texture animation, wave). Respect
  `MeshSubMesh.MaterialIndex` semantics: world/static meshes index the source
  material list; model (MDM) meshes use the submesh index and its material.
  Report missing textures once per name.
  **Done when:** decoded real textures have correct dimensions, mipmaps and
  alpha; unit tests cover material mapping and cache behavior; missing textures
  do not crash; `--check-textures` decodes a real sample without failures.
  Visual orientation/alpha is verified in T1.5.

- [x] **T1.3 Skinned mesh conversion (key task)** (2026-11-20)
  Implement the bake described in Design/03: convert influences and bind
  transforms to engine RHS, build bind global transforms from MDH, normalize
  weights, compute one bind position per vertex, compute inverse bind matrices,
  pad to four influences, and produce `SkinnedVertex` data plus per-model
  diagnostics (max bind inconsistency, weight anomalies, bone count). Add
  `--check-models` that walks every MDS reachable from the VFS and prints a
  summary.
  **Done when:** synthetic tests prove equivalence with the Gothic four-position
  formula within `1e-5` in the converted space; inconsistent inputs produce the
  documented warning and weighted-average result; `--check-models` runs on a real
  install and reports no crashes; results are documented in Design/03.
  Result: 117 MDS / 171 skinned meshes, 0 failures; primary bind inconsistency
  <= 2.713 cm raw / 1.569 cm weighted. See Design/03.

- [x] **T1.4 Skeletons and animation clips** (2026-11-20)
  Convert MDH hierarchies and MDS animations to DigitalRiseModel bones, skin
  joints and `AnimationClip`s (translation + rotation channels, frame-to-ms
  timing), converting every keyframe to engine RHS. Preserve node order and
  indices. Add clip lookup by name and loop/transition flags. Implement pose
  evaluation with a `DrModelInstance`.
  **Done when:** a unit test evaluates a two-bone clip at known times, including
  yaw sign after conversion; attachment node lookup works; a real NPC can be
  posed at frame 0 and mid-animation without NaNs.
  Result: 117 MDS scripts, 5,783 clips, 1,852 aliases, 11,566 pose evaluations,
  0 non-finite transforms, 0 failures. See Design/03.

- [x] **T1.4a Simplify the runtime model around DrModel** (2026-11-20)
  OpenGothicSharp shows that a loaded skinned model can be returned as `DrModel`
  directly. Remove `RuntimeModel`; keep the load-time bone mapping only in the
  builder result. Move Gothic clip metadata (loop/transition, next, blend,
  events, root motion) onto `GothicAnimationClip : AnimationClip` so
  `DrModel.Animations` is the single clip store. Build per-mesh skins in T1.7
  from `DrModel` bones, as OpenGothicSharp does, instead of holding a model-level
  skin. Update `--check-animations`, tests and Design/03.
  **Done when:** `--check-animations` and unit tests pass with `DrModel` as the
  returned asset; no behavior regression in clip conversion or pose evaluation.
  Result: runtime asset is `DrModel`; 217 tests pass; `--check-animations` still
  reports 0 non-finite transforms and 0 failures. See Design/03.
  Follow-up (2026-11-21): DigitalRiseModel 0.7.7 exposed `AnimationClip.Tag`;
  `GothicAnimationClip` was removed and its metadata moved to `AnimationClipTag`.

- [x] **T1.5 Renderer bring-up** (2026-11-20)
  MonoGame window, `GraphicsDevice` lifecycle, right-handed `GothicCamera`, depth
  buffer, `BasicEffect` test scene (grid, textured cube, unlit normals debug),
  resize handling, screenshot command. No custom shaders. Use `TextureData` and
  `MaterialDesc` from T1.2; verify texture orientation, alpha modes and the
  two-sided heuristic with real assets.
  **Done when:** the test scene renders correctly (screenshot), converted data is
  verified with an asymmetric texture (readable text, correct winding), and
  `--screenshot` produces a deterministic PNG.
  Result: DigitalRiseModel primitives + `DrMeshPart`/`DrMaterial`, 1280x720
  deterministic PNG, 223 tests. See Design/07.

- [x] **T1.6 World loading and static rendering** (2026-11-20)
  Load a ZEN world, convert the world mesh and VOB transforms/waypoints to engine
  RHS, batch by material/lightmap, spawn static VOBs with transform composition,
  resolve the start point, and render the world from a free camera. Reuse the
  T1.5 `GothicRenderer`, `GothicCamera`, `GpuTextureCache` and `--screenshot`
  path; replace the R0 test scene. Use `AlphaTestEffect` and `DualTextureEffect`
  where applicable. Add loading progress and timings.
  **Done when:** `newworld.zen` renders recognizably from the start point
  (screenshot), nothing is inside-out or offset by a double conversion, no VOB
  type crashes, and a data test asserts counts/bounds within tolerances.
  Result: `newworld.zen` renders from its start point; 473,502 world triangles,
  11,546 static instances from 312 meshes, 2,959 waypoints, 229 tests,
  deterministic 1280x720 PNG, F3 debug overlay and controls HUD. Lightmaps and
  on-screen loading progress remain in the backlog. See Design/04 and Design/07.

- [x] **T1.7 Skinned VOBs and NPCs** (2026-11-21)
  Spawn model VOBs, build `DrModel`s from converted data, draw with
  `SkinnedEffect`, play the default idle animation, and support attachments
  (weapon/armor) by node name. Build one `DrSkin` per mesh from the mesh's node
  list and the DrModel absolute bind transforms (T1.4a), which reduces the joint
  set to the bones the mesh actually uses; still split/remap if a mesh exceeds
  72 joints.
  **Done when:** NPCs from a real world are visible and animate (screenshot at
  two timestamps); a synthetic model over 72 bones is remapped or reported
  without crashing.
  Result: MDS/MDL model VOBs load with per-mesh `DrSkin`, default idle clips and
  static MDM node attachments; >72-bone meshes keep the strongest 72 and warn.
  `newworld.zen` has 61 unique model assets / 883 instances / 15,068 triangles;
  233 tests; `--spawn-model Orc.mds` renders an animated Orc at two timestamps.
  Morph heads, inventory weapon/armor slots and skinned alpha-test remain
  backlog. See Design/03 and Design/07.

- [x] **T1.8 Data diagnostics** (2026-11-21)
  `--check-meshes` (T1.1), `--check-textures` (T1.2), `--check-models` (T1.3),
  `--check-animations` (T1.4) and `--check-world` (T1.6) cover world meshes,
  textures, skinned models, clips and world conversion. Extend the suite with
  `--check-assets` / `--dump-data`: walk models, textures and scripts; report
  cross-references (animations, attachments, missing VOB assets), degenerate
  triangles and unsupported features as a summary and optional JSON. Never write
  into the installation.
  **Done when:** the commands run on a real install and their output is used in
  the task evidence; docs describe the commands and their exit codes.
  Result: `--check-assets` walks models, static meshes, textures, scripts and all
  world VOB visuals; `--json` writes the full report outside the installation.
  `--dump-data` writes OBJ/MTL/BMP output plus a manifest. G2 NotR scan: 117 MDS,
  3,931 animations, 1,852 aliases, 1,321,136 triangles, 3,108 degenerate, 2
  missing MANs, 16 missing textures, 0 missing VOB assets, 0 failures; 237
  tests. See Design/01 and Design/09.

- [x] **T1.9 Visibility and batching baseline** (2026-11-21)
  Frustum culling for static batches and model instances, material-based
  batching, distance culling, and counters. Keep the BSP for later portal
  culling unless measurement demands it.
  **Done when:** a benchmark shows fewer draw calls and no correctness
  regressions; counters are visible in the debug overlay.
  Result: world ranges, static instances and model instances are frustum/distance
  culled; the overlay shows culled batches/instances. Start-view draw calls fell
  from 23,118 to 2,063 (polygons 2,732,917 -> 537,101, 924 culled batches,
  11,335 culled instances). The deterministic screenshot is unchanged; 237 tests.
  See Design/07 and Design/09.

## M2 - Movement and world interaction

- [ ] **T2.1 Static collision world**
  Build the Bepu simulation, convert the world mesh and collidable VOBs into
  static colliders, define collision layers/masks, and expose raycast/overlap
  queries. Sanitize degenerate triangles. Fixed 60 Hz step with substep cap.
  **Done when:** headless tests cover drop/rest, ray masks and a walkable world
  mesh; loading a real world builds collision without errors and reports timings.

- [ ] **T2.2 Player controller and camera**
  Capsule character controller (slope limit, step height, ground snap, wall
  slide), input mapping (walk/run/sneak, look, jump is not a Gothic action),
  first/third-person cameras with collision, and debug free camera. Spawn at the
  world start point.
  **Done when:** the player can walk through Khorinis for several minutes without
  falling through geometry or sticking on stairs (manual checklist + screenshots);
  headless controller tests pass.

- [ ] **T2.3 Waynet and AI movement**
  Convert the waynet to a graph, implement A* pathfinding, waypoint following,
  `AI_GotoWaypoint`-style commands, turn-to-direction, and animation selection
  for walk/run. Handle free points and underwater flags. Consume the
  `RootMotionDelta` recorded by T1.4 in the movement controller so animation and
  movement do not move an NPC twice.
  **Done when:** an NPC walks a multi-waypoint path around a corner in a real
  world; headless graph tests cover pathfinding and unreachable targets.

- [ ] **T2.4 Focus and interactive objects**
  Focus ray and highlight, MOBSI state machines (doors, chests, levers, ladders,
  beds, campfires), use/enter/exit animations, and script callbacks. Focus
  priority follows the original rules.
  **Done when:** doors open/close and chests are usable in a real world; headless
  tests cover focus selection and state transitions.

- [ ] **T2.5 Dynamic props and items**
  Simple rigid bodies for droppable items and movable props, sleeping bodies,
  pick-up/drop, and collision with the player. Do not simulate every item in the
  world.
  **Done when:** an item dropped on the ground rests and can be picked up; no
  physics allocation in steady state.

- [ ] **T2.6 Triggers and zones**
  Trigger VOBs (script, change-level, world-start, untouch, list), zone VOBs
  (fog, music, far plane) and collision zones. Dispatch to the session with
  enter/exit/stay semantics and cooldowns.
  **Done when:** walking through a real trigger fires exactly once and is visible
  in the log/console; headless tests cover overlap and cooldown.

- [ ] **T2.7 Minimal Myra HUD**
  Add a functional Myra overlay: health/mana/stamina bars, focus prompt,
  subtitle area, FPS/debug counters, and a simple pause/exit screen. Use a
  default font and plain styling; do not imitate Gothic UI. Bind it to the
  player/session as those systems land, and make Escape open the pause screen
  instead of exiting directly.
  **Done when:** the HUD is visible in a real world and updates from player
  stats; the pause screen can resume and exit; dev mode toggles the debug
  overlay.

## M3 - Scripts and gameplay

- [ ] **T3.1 Daedalus host and core externals**
  Wrap `ZenKit.DaedalusVm`, load `GOTHIC.DAT`, own symbol lookup and instance
  allocation, register core externals (print, string/number conversion, random,
  time), and implement the default unimplemented-external logger. Add
  `--check-scripts` coverage report.
  **Done when:** the VM loads a real script, core calls work, missing externals
  are reported once, and argument marshaling is unit tested.

- [ ] **T3.2 Game initialization**
  Implement the startup chain (`startup_global`, `startup_<world>`,
  `init_global`, `init_<world>`, `initPerceptions`), allocate and initialize
  instances, spawn the player from `PC_HERO`, and run the first world's startup
  scripts. Audit externals that exchange positions/angles for the LHS-to-RHS
  boundary and log any hard-coded coordinate usage. Log every script call in
  developer mode.
  **Done when:** a new game reaches a controllable player in a real world with no
  fatal script error; repeated world loads do not duplicate globals; no script
  receives Gothic-space geometry.

- [ ] **T3.3 World and AI externals**
  Implement world/script APIs needed for basic play: waypoints, teleport, AI
  output, animations, sounds, focus, time, and chapter changes. Track coverage.
  **Done when:** a scripted sequence (e.g. a simple NPC routine) runs end to end;
  coverage report shows the implemented set and the remaining names.

- [ ] **T3.4 NPC state, perception and routines**
  Attributes and guilds, perception events, daily routines, routine waypoints,
  alert/flee, and script notifications. NPCs must react to the player and to
  script commands.
  **Done when:** NPCs in Khorinis perform their daily routine and react to the
  player at the correct ranges; headless tests cover perception and routine
  transitions.

- [ ] **T3.5 Inventory, equipment and interaction**
  Item instances, containers, take/use/equip/drop, torches, ammo, armor visuals,
  prices and weight. Connect interaction commands from T2.4 to scripts. Add a
  basic functional Myra inventory panel (list/grid and equipment slots) so the
  flow is usable; visual polish belongs to T4.3.
  **Done when:** the player can loot a chest, equip a weapon/armor, use a potion
  and see stats/appearance update; save/load preserves inventory.

- [ ] **T3.6 Dialogs, trade, quests and documents**
  Info instances, dialog choices and voice/subtitle output, trade UI plumbing,
  mission/quest state, journal entries, and document reading. Use an
  `IDialogPresenter` so the flow is testable headless; the first presenter is a
  plain Myra list/panel, not a Gothic visual replica.
  **Done when:** a full dialog with a real NPC can be completed, including a
  trade and a quest update; headless dialog tests pass.

- [ ] **T3.7 Combat**
  Melee hit windows from animation events, damage/hit chance from script data,
  blocking, stagger, ranged projectiles, death, loot and XP. Expand `aniComb`
  combo-window clips from T1.4 here, and fire the stored hit/parry/event
  markers from the animation layer.
  **Done when:** the player can kill a weak monster and be killed; damage numbers
  match a scripted reference scenario within tolerance; seeded tests are
  deterministic.

- [ ] **T3.8 Magic**
  Runes/scrolls, mana, casting animations, projectile/area spells, and script
  callbacks. Spell visuals may use placeholders until T4.4.
  **Done when:** at least one offensive and one utility spell work end to end;
  mana and item consumption match the script.

- [ ] **T3.9 Save and load**
  Implement the ZIP archive format from Design/06 with versioned JSON entries,
  atomic writes, migration hooks, thumbnails, quick save, save list metadata, and
  full session reconstruction (world, NPCs, items, triggers, quests, clock,
  script state).
  **Done when:** save/load round-trips a real session across app restarts and
  version migration tests pass; corrupt/truncated archives fail without losing
  the current session.

- [ ] **T3.10 Developer console and marvin mode**
  In-game console with commands (teleport, spawn item, give XP, heal, set time,
  toggle collision/debug drawing, list externals). Gate cheats behind
  `-devmode`.
  **Done when:** every command is documented in `Design/` and usable in a real
  session; no console command is required for normal play.

## M4 - Presentation

- [ ] **T4.1 Audio: SFX and voice**
  Sound effect loading/caching (WAV first), 3D playback with listener, category
  volumes, instance limits, footstep/impact sounds from materials, and voice
  output from dialogs. Research DLS/SGT music playback and record the decision.
  **Done when:** footsteps, UI sounds, ambient loops and dialog voice play in a
  real world; graphics-free audio tests verify category routing and limits.

- [ ] **T4.2 Full menu, settings and pause UI**
  Plain Myra screens: main menu, settings (video/audio/gameplay), save/load list,
  pause menu, loading/progress screens, UI scale and input routing between UI and
  gameplay. Use a default font and functional styling; do not replicate Gothic
  menus or fonts. Script-created menu externals remain stubbed/logged.
  **Done when:** the game can be started, configured, saved and loaded entirely
  through the UI; every screen is usable with a keyboard and mouse without
  debug commands.

- [ ] **T4.3 Inventory, journal, dialog and document UI**
  Functional Myra layouts: inventory grid/equipment, item tooltips, journal/quest
  log, dialog and trade screens, map (if data is available), book reader. Default
  fonts and plain styling are sufficient. Keyboard/mouse and gamepad navigation.
  If a world map texture is used, convert the marker and map orientation
  consistently with the RHS engine space.
  **Done when:** all core gameplay screens are usable without debug commands;
  the map marker matches the player's position; UI state survives save/load.

- [ ] **T4.4 Visual effects**
  PFX emitters, decals, torch fire, spell effects, weather (rain) and ambient
  visual objects. CPU simulation first, then GPU quads.
  **Done when:** torches and at least one spell look plausible in motion; effects
  are data-driven and can be disabled in settings.

- [ ] **T4.5 Video and chapter screens**
  Implement chapter screens. Evaluate Bink decoding; if no acceptable decoder,
  show static images/text and document the limitation. Never use the
  proprietary Bink SDK.
  **Done when:** chapter transitions do not block progression; the chosen
  approach and licensing are documented.

- [ ] **T4.6 Music**
  Implement the chosen DirectMusic approach (DLS + SGT + synthesizer) or an
  explicitly documented fallback. Include volume, transitions and combat/day
  themes where scripts request them.
  **Done when:** the main theme and at least one location/combat transition play;
  falling back to silence is a setting, not a crash.

- [ ] **T4.7 Visual parity pass (last)**
  Replace stock effects only where needed: accurate material behavior, lighting,
  fog, sky, water waves, shadows (optional), color management, and model detail
  (morphs, LODs). If standard LBS visibly distorts vanilla armor/variant meshes
  whose source bind data is inconsistent, add the optional four-position skinning
  fallback here. Optional original-UI replication (script menus, FNT fonts,
  authentic layouts/art) also belongs here, only if pursued. This is the only
  milestone allowed to prioritize visuals over behavior.
  **Done when:** a side-by-side screenshot review against OpenGothic shows no
  gameplay-relevant visual blockers; all custom effects have fallbacks for
  unsupported hardware.

## M5 - Hardening

- [ ] **T5.1 Performance and memory**
  Instancing, culling, LOD selection, asynchronous/streamed loading, VFS lookup
  indexing/caching, reduced per-frame allocations, pooled buffers, and profiling
  of Khorinis and the add-on world. Target 60 FPS at 1080p on modest hardware.
  **Done when:** benchmark reports and before/after numbers are documented;
  frame-time spikes during world streaming are bounded.

- [ ] **T5.2 Full-game parity pass**
  Play through the main quest and the add-on with a checklist of quests, dialogs,
  triggers, items and enemies; fix divergences; keep a known-issues list.
  **Done when:** both campaigns are completable without developer intervention;
  the checklist and known issues are in `Design/`.

- [ ] **T5.3 Settings, input and accessibility**
  Full key rebinding, gamepad support, UI scale, subtitles, volume sliders,
  invert options, and persistence of all settings.
  **Done when:** a fresh install can be configured without editing files; settings
  survive restarts and are covered by tests.

- [ ] **T5.4 Mods and Gothic 1**
  Validate mod loading (archives, `modfile.ini`, loose `_work` files), language
  variants, and best-effort Gothic 1 support with explicit limitations.
  **Done when:** at least one content mod runs; Gothic 1 either works or fails
  with a clear, documented message.

- [ ] **T5.5 Packaging and release**
  Self-contained win-x64 publish, zip layout, version metadata, license and
  third-party notices, crash log collection instructions, and release checklist.
  **Done when:** a clean machine without .NET installed can run the published
  build with a Gothic installation; the release checklist is documented.

---

## Backlog / ideas (not scheduled)

- GOG/other launcher install detection and an interactive folder picker.
- Texture frame animation (`loadFrames`) for animated materials.
- On-screen world loading/progress screen (T1.6 currently reports progress and
  timings through logs only).
- Remove the default Orc idle showcase from normal runs once script startup
  spawns real NPCs (T3.2).
- Full GPU mip-chain upload and sampler/anisotropic policy (T1.5 uploads the
  top mip only).
- World lightmap UV projection and `DualTextureEffect` (T1.6 renders solid/alpha
  world materials without baked lightmaps).
- Morph-mesh heads (`.MMB`) and script-driven NPC body/armor/weapon attachments
  (T1.7 renders the default body mesh and MDM node attachments; `setVisualBody`
  belongs to script startup).
- Alpha-tested skinned materials (T1.7 uses `SkinnedEffect`, which has no alpha
  test; alpha materials are blended instead of cut out).
- Original Gothic UI replication: script-created menus, `.FNT` fonts, authentic
  inventory/journal/menu layouts and art.
- Portal-based visibility from the BSP tree.
- Hardware skinning alternatives for >72 bones.
- Optional four-position skinning shader for vanilla armor/variant meshes whose
  bind data is inconsistent under standard LBS (see Design/03).
- Import/export of converted assets for debugging (glTF).
- Benchmark mode with a deterministic camera path.
- Optional high-resolution texture override for mods.
- Save format import from OpenGothic (not original Gothic).
