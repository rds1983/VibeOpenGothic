# Vision

## Product

A native C# client for Gothic II: Night of the Raven that loads the original
game installation and its mods. It is a functional port of
[OpenGothic](https://github.com/Try/OpenGothic), rebuilt on MonoGame instead of
Tempest, and is not a new game or a remake.

## Success criteria

The project is "done" when, using only original Gothic II: Night of the Raven
data plus optional mod files:

1. A new game can be started, the main quest completed and the add-on completed.
2. Worlds load, characters and creatures animate, dialogue and trade work.
3. Combat, magic, inventory, quests, journals, documents and triggers behave
   like the original closely enough that a player notices no difference beyond
   graphics quality and UI presentation. A functional, convenient Myra UI is
   acceptable; replicating the original UI is not required.
4. Saves can be created, listed and loaded reliably across application restarts,
   including world changes and script state.
5. Content mods that only rely on regular scripting work (see
   [02-Game-Data.md](02-Game-Data.md)).
6. The frame rate is stable on modest hardware (target: 60 FPS at 1080p in
   Khorinis), with loading times comparable to OpenGothic.

Intermediate milestones are in [../TASKS.md](../TASKS.md). The first playable
vertical slice is: load a ZEN world, walk around with collision, see NPCs and
animate them, and interact with a door.

## Non-goals for the first release

- Ray tracing, mesh shaders, bindless rendering, GPU-driven rendering.
- Vulkan/DX12/Metal backends. MonoGame's DesktopGL backend is the only target.
- Union, Ikarus/LeGo or AST emulation. These rely on 32-bit memory hacking and
  are out of scope until the base game is complete.
- Android, iOS, consoles.
- Shipping any Gothic assets, extracted data, or a Gothic installer.
- Pixel-perfect graphics parity. Rendering is intentionally the last priority
  (see [07-Rendering.md](07-Rendering.md)).
- Replicating Gothic's UI look, fonts or script-created menu tree. The initial
  client uses a straightforward Myra UI; authentic UI is an optional late
  parity task.

## Principles

1. **Functionality over code.** We reproduce observable behavior and data
   interpretation. The original source is a reference, not a template; we are
   free to structure C# differently.
2. **The data is the specification.** Prefer reading Gothic files and script
   constants over hard-coded guesses. When a constant must be guessed, document
   it and add a test.
3. **Headless core.** Gameplay, world loading and conversion must be testable
   without a graphics device.
4. **Stock effects first.** Use MonoGame's `BasicEffect` and `SkinnedEffect`
   until gameplay is complete. Custom shaders only replace them at the end.
5. **Small, verifiable steps.** Every task in TASKS.md has a "done when" clause
   and is confirmed by the developer before it is checked off.
6. **Function before fidelity for UI.** Build a usable Myra HUD, menus and
   dialogs first. Gothic fonts, script menus and original layouts are optional
   and come last, if at all.
7. **No accidental asset distribution.** The repository contains code and docs
   only. Tests that need Gothic data use an explicitly configured local path and
   are skipped when it is absent.

## Constraints

- MIT-compatible dependencies and contributions only.
- Windows x64 is the primary platform. ZenKitCS ships native libraries for
  win-x64, linux-x64, osx-x64 and android-arm64; no win-arm64.
- Gothic data is 32-bit-era, mixed-encoding text (Windows-1252/1250/1251),
  centimetre-scale and **left-handed**. The engine is right-handed (MonoGame).
  All geometry and transforms are converted once at load time, never per draw.
  Treat encoding and units as engine concerns at the same boundary.

## Known risks

| Risk | Mitigation |
| --- | --- |
| Daedalus externals number in the hundreds; each game system needs its own subset | Implement in vertical slices; ship an "unimplemented external" coverage report and console log |
| Gothic skinned meshes use 4 positions per vertex (see 03) | Convert once at load time, keep the original as a debug fallback, validate with per-model error metrics |
| MonoGame `SkinnedEffect` supports at most 72 bones and 4 weights | Check bone counts at load; split meshes or add a custom effect if real skeletons exceed it |
| DirectMusic-style adaptive music and Bink video have no .NET runtime | Defer; implement an offline conversion or a minimal player late in the project |
| ZenKit is a native library; crashes are process-fatal | Fence all native calls, validate inputs, keep game-data access behind one layer, and log the current asset |
| Mod data violates assumptions (weights not summing to 1, inconsistent bind poses) | Validate and normalize on load, log per-asset diagnostics, never fail the whole world |
| LHS-to-RHS conversion mistakes (winding, normals, bone transforms) can make models inside-out or mirrored | One `CoordinateSystem` helper, conversion tests, and an asymmetric textured test model checked in T1.0/T1.5 |
| Scripts and UI assets were authored for LHS; absolute coordinates, angles or map orientation may disagree with engine space | Convert at the script/UI boundary, audit hard-coded coordinates (T3.2), verify the map marker (T4.3) |
| Large world + GC pauses | Pool transient objects, avoid per-frame allocations in hot paths, profile in M5 |

## Assumption log

Assumptions that are not yet verified are listed here and referenced by a task.

- **Assumption A1:** Gothic world and model data are Y-up, centimetres and
  left-handed, with horizontal headings in the XZ plane. Y-up and the XZ
  headings match OpenGothic's camera, gravity and heading math; handedness is
  taken from the Gothic data format and is handled by the conversion layer.
  Verified visually by T1.6 when a real world is rendered.
- **Assumption A2:** `ZenKit`'s managed wrappers are not thread-safe. Confirmed
  behaviorally in T0.3; assets are loaded on one worker at a time.
