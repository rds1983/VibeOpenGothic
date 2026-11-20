# VibeOpenGothic

C# reimplementation of **Gothic II: Night of the Raven**, ported from
[OpenGothic](https://github.com/Try/OpenGothic). It runs the original game data;
no game assets are bundled.

**Status:** pre-alpha (data loading, static world and model VOB rendering). The roadmap is in [TASKS.md](TASKS.md).

Key decisions:

- Port behavior and data interpretation, not the original code.
- Convert Gothic's left-handed geometry to MonoGame's right-handed space once,
  at load time.
- Convert Gothic's four-position-per-vertex skinned meshes to standard
  single-position linear blend skinning once, at load time.
- Use stock MonoGame effects first; visual parity is the last milestone.
- Use a functional Myra UI first; replicating the original Gothic UI is optional
  and comes last.

## Technology

| Area | Choice |
| --- | --- |
| Language / runtime | C# / .NET 8 |
| Platform, input | MonoGame (DesktopGL) |
| Models, skeletons, animation | DigitalRiseModel |
| UI | Myra (FontStashSharp text) |
| Gothic file formats, Daedalus VM | ZenKitCS |
| Collision, character physics | BepuPhysics2 |

## Source size

Game source only: `VibeOpenGothic/` excluding `bin/`, `obj/` and the test project.

| Metric | Value |
| --- | --- |
| C# files | 125 |
| C# lines (physical / non-blank) | 10,062 / 8,573 |
| Other source files | 3 (2 `.xmmp`, 1 `.csproj`) |
| Total source size | 255,438 bytes (249.5 KiB) |

Counts include generated Myra files. Update this table when source size changes materially.

## Requirements

- .NET SDK 9.0.200 or newer (10.x recommended). The projects target net8.0; the
  `.slnx` solution format is not supported by the .NET 8 SDK.
- A legitimate Gothic II: Night of the Raven installation (GOG, Steam or retail)
- Windows x64 (ZenKit ships x64 native binaries); Linux x64 is expected to work but untested

## Build and run

```sh
dotnet build VibeOpenGothic.slnx
dotnet test VibeOpenGothic.slnx
dotnet run --project VibeOpenGothic -- --help
dotnet run --project VibeOpenGothic -- --probe -g "C:\Gothic II"
dotnet run --project VibeOpenGothic -- -g "C:\Gothic II"
```

The current build loads and renders the default Gothic II world from a free
camera, including static VOB props and model VOBs. Escape or Alt+F4 exits; `F3`
toggles a debug overlay with FPS, draw calls and submitted triangles, and a
bottom-left HUD lists the current controls. Normal runs spawn an Orc idle
showcase in front of the start point; use `--spawn-model <mds>` to choose a
different model or `--screenshot` (which omits the showcase) for deterministic
captures. Gothic II is auto-detected from settings, `VOG_GOTHIC_PATH`, common
paths and Steam library folders; `-g <path>` overrides detection. Settings and
`log.txt` are written to `%LOCALAPPDATA%\VibeOpenGothic` (override with
`--data-dir`).

## Repository layout

```
Design/               design and format documentation
VibeOpenGothic/       game code and project file
VibeOpenGothic.Tests/ xUnit tests
VibeOpenGothic.slnx   solution
TASKS.md              roadmap and current task
AGENTS.md             development workflow
```

## Documentation

Start with [Design/README.md](Design/README.md). The most useful entry points:

- [Design/00-Vision.md](Design/00-Vision.md) - scope, success criteria, risks
- [Design/01-Architecture.md](Design/01-Architecture.md) - projects, layers, conventions
- [Design/03-Models-And-Skinning.md](Design/03-Models-And-Skinning.md) - LHS-to-RHS conversion and Gothic skinned-mesh conversion

## License

MIT, see [LICENSE](LICENSE). OpenGothic is MIT-licensed, copyright (c) 2019 Try.
Gothic and Gothic II are trademarks of their respective owners.
