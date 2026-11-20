# Architecture

## Solution

```
VibeOpenGothic.slnx
  VibeOpenGothic/            WinExe, net8.0, all game code
  VibeOpenGothic.Tests/      xUnit, net8.0, references the game project
.editorconfig               code style and formatting rules
Directory.Build.props        shared compiler settings
Directory.Packages.props     central package versions
```

`VibeOpenGothic.slnx` is the XML solution format supported by .NET SDK 9.0.200+
(10.x recommended). The projects themselves target net8.0. One executable
project is intentional: the engine is a monolith with strict internal layering.
Splitting into assemblies is allowed later if compile times or dependency
enforcement justify it, but not before.

### Package baseline

| Package | Version | Notes |
| --- | --- | --- |
| MonoGame.Framework.DesktopGL | 3.8.0.1641 | Pinned: Myra 1.6.8 and DigitalRiseModel 0.7.6 reference this assembly version |
| Myra | 1.6.8 | UI; text rendering comes from its FontStashSharp dependency |
| DigitalRiseModel.MonoGame | 0.7.6 | Runtime model/bone/animation containers |
| ZenKit | 1.3.0-cs.1 | ZenKitCS; native win-x64/linux-x64/osx-x64/android-arm64 |
| BepuPhysics | 2.4.0 | Brings BepuUtilities |
| xunit | 2.9.3 | Test project only |
| xunit.runner.visualstudio | 2.8.2 | Test project only |
| Microsoft.NET.Test.Sdk | 17.14.2 | Test project only |

ZenKit forces a `RuntimeIdentifier` per OS through its build targets. Set
`RuntimeIdentifiers` to `win-x64;linux-x64;osx-x64` in the game project and let
the package copy the matching native library. Do not add win-arm64; it does not
ship one. Upgrading MonoGame is a separate, explicitly tested task.

## Layers and namespaces

Folders mirror namespaces. Names below are relative to `VibeOpenGothic/`.

| Namespace | Responsibility | May depend on |
| --- | --- | --- |
| `VibeOpenGothic.Core` | Logging, command line, settings, time, math helpers, coordinate conversion, diagnostics | BCL, MonoGame math structs |
| `VibeOpenGothic.Data` | Install discovery, VFS access, version detection, INI reading | Core, ZenKit |
| `VibeOpenGothic.Assets` | ZenKit data to CPU-side engine data: meshes, textures, models, animation | Core, Data, ZenKit, DigitalRiseModel (CPU types only), MonoGame math |
| `VibeOpenGothic.World` | World model: ZEN objects, VOB tree, waynet, triggers, spawning | Core, Data, Assets, Physics |
| `VibeOpenGothic.Physics` | Bepu world, collision geometry, character controller, queries | Core, Bepu |
| `VibeOpenGothic.Game` | Session, NPCs, combat, inventory, dialogs, quests, save/load, script externals | Core, Data, Assets, World, Physics |
| `VibeOpenGothic.Rendering` | Graphics device, camera, effects, draw submission, GPU caches | Core, Assets |
| `VibeOpenGothic.UI` | Myra screens, HUD, menus, fonts | Core, Assets, Game (read-only view models) |
| `VibeOpenGothic.Audio` | Sound effects, music, listener | Core, Assets, Game (read-only) |
| `VibeOpenGothic.Video` | Bink and other video playback (late) | Core |
| `VibeOpenGothic.Tools` | CLI subcommands: probe, dump, benchmark | Core, Data, Assets |
| `VibeOpenGothic.App` | `Program`, `GothicGame`, composition root | Everything |

Hard rules:

- `World` and `Game` never touch `GraphicsDevice`, `Texture2D`, `VertexBuffer`
  or Myra. This keeps gameplay testable headless.
- ZenKit types (`ZenKit.Mesh`, `ZenKit.World`, `ZenKit.DaedalusVm`, ...) do not
  leave `Data`/`Assets` except for the Daedalus VM facade, which is wrapped by
  `Game.Scripts`. Prefer converting to our types at the boundary.
- `Assets` may use MonoGame math (`Vector3`, `Matrix`) and DigitalRiseModel CPU
  types but must not create GPU resources. `Rendering` owns GPU uploads.
- Gothic data is left-handed; engine space is right-handed. All conversion goes
  through `Core.CoordinateSystem` in the loader that reads the data. Gameplay,
  rendering and physics must never flip axes or winding ad hoc.
- No service locator, no mutable statics except logging and pure math helpers.
  Dependencies are constructor-injected from one composition root.
- Reference direction is acyclic. Add a new layer only with a doc update.

## Runtime composition

```
Program
  -> CommandLine parse -> Log init
  -> GothicInstall.Locate -> GameVfs.Mount
  -> GothicGame : Microsoft.Xna.Framework.Game
       services (constructed once):
         Settings, AssetLibrary, TextureCache, ModelCache
         GameSession (current world/scripts/player)
         Renderer, UiRoot, AudioSystem
       Update(GameTime):  session.UpdateLogic(dt)  -> world, AI, physics, scripts
       Draw(GameTime):    renderer.Render(world, camera) -> ui.Draw()
```

`GameSession` is the unit that a save game maps to. Changing a world keeps the
session and its VM: the player and the visited world's state are snapshotted,
the old world is unloaded, the new world is loaded, script instance pointers are
re-bound, and startup/init functions for the new world run.

## Time and units

- World units are centimetres; time in game logic is milliseconds. Rendering and
  physics convert explicitly (`ToMeters`, `ToSeconds`). Do not mix them.
- The logic update uses a variable delta clamped to 100 ms (a slow frame must not
  teleport NPCs or skip triggers).
- Physics runs at a fixed 60 Hz step, with at most 4 catch-up steps per frame.
- Animation time is milliseconds, matching MDS frame timing and script events.
- Angles are radians inside math code, degrees only at the scripting/UI boundary.
- Engine space is Y-up and right-handed, in centimetres. Gothic files are Y-up
  and left-handed; see below.

### Math and coordinates

- `Core.CoordinateSystem` is the only place that knows Gothic is left-handed. It
  converts points, directions, normals, matrices and quaternions, and reverses
  triangle winding. It is an involution (applying it twice restores the input)
  except for winding, which must be reversed exactly once.
- Conversion happens in the loader, before data is cached: converted meshes,
  skeletons, animations, VOB transforms and waypoints are already in engine
  space. Nothing down the stack needs to know about the source convention.
- Engine math uses MonoGame `Vector3`/`Matrix`/`Quaternion` (row-vector
  convention). Bepu uses `System.Numerics`; conversion is confined to
  `VibeOpenGothic.Physics` and is unit tested.
- The camera uses MonoGame's right-handed helpers directly; there is no
  scene-wide mirror or per-draw sign flip.

## Assets

```
VFS bytes -> ZenKit parse -> AssetConverter -> CPU asset (immutable) -> cache
                                                \-> GPU object (Rendering only)
```

- `AssetLibrary` owns typed caches keyed by normalized VFS path and conversion
  options (e.g. LOD level). Caches are cleared on world change unless a task
  says otherwise.
- Conversion is pure and deterministic. Algorithms take plain input arrays and
  structs so they are unit tested without native wrappers; the thin
  ZenKit-to-struct adapters are covered by data tests.
- Failed optional assets log once per path and return a cached placeholder
  (magenta texture, cube mesh). Missing mandatory files (`GOTHIC.DAT`, the
  current world) fail with a user-readable error.
- Assets are loaded on a single background worker first. ZenKit managed wrappers
  are treated as not thread-safe; concurrency is added only after measurement.

## Error handling and diagnostics

- `ILogSink` with console + `log.txt` sinks. Categories are namespaces.
- Exceptions crossing a native boundary are caught in `Data`/`Assets`, wrapped in
  `AssetException` with the asset path, and logged with context.
- Gameplay errors caused by broken scripts are logged and recovered from where
  possible (the original tolerates many missing externals). Startup errors are
  fatal and show a message box plus the log path.
- `--probe` prints install version, mounted archives, file counts and resolves a
  set of known assets. `--dump-data` extracts converted meshes/textures for
  debugging (developer-only output, never committed).
- Performance counters: frame time, update time, loaded asset count, draw calls,
  triangle count, GC allocations per frame.

## Coding conventions

- C# 12, nullable reference types enabled, warnings treated as errors in CI.
- File-scoped namespaces, one public type per file, `sealed` by default.
- Private fields `_camelCase`; constants `PascalCase`; async methods suffixed
  `Async`; booleans read as predicates (`IsLoaded`, `HasFocus`).
- Prefer immutable records for data crossing layers; mutable classes for runtime
  entities.
- No `TODO` without a task id: `// TODO(T3.3): implement Ta_AddArmor`.
- Every nontrivial algorithm gets a comment explaining the game-data quirk, not
  the C# mechanics.

## Testing hooks

- `GothicGame` accepts a `GothicHost` interface for asset and session factories
  so tests can run a world-free session.
- Time is injected (`IClock`/`ManualClock`) rather than read from `DateTime.Now`.
- Rendering has a null renderer used in headless tests.
- Command line arguments are parsed into a record and unit tested without
  starting MonoGame.
