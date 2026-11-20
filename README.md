# VibeOpenGothic

C# reimplementation of **Gothic II: Night of the Raven**, ported from
[OpenGothic](https://github.com/Try/OpenGothic). It runs the original game data;
no game assets are bundled.

**Status:** pre-alpha (repository bootstrap). The roadmap is in [TASKS.md](TASKS.md).

Key decisions:

- Port behavior and data interpretation, not the original code.
- Convert Gothic's left-handed geometry to MonoGame's right-handed space once,
  at load time.
- Convert Gothic's four-position-per-vertex skinned meshes to standard
  single-position linear blend skinning once, at load time.
- Use stock MonoGame effects first; visual parity is the last milestone.

## Technology

| Area | Choice |
| --- | --- |
| Language / runtime | C# / .NET 8 |
| Platform, input | MonoGame (DesktopGL) |
| Models, skeletons, animation | DigitalRiseModel |
| UI | Myra (FontStashSharp text) |
| Gothic file formats, Daedalus VM | ZenKitCS |
| Collision, character physics | BepuPhysics2 |

## Requirements

- .NET SDK 9.0.200 or newer (10.x recommended). The projects target net8.0; the
  `.slnx` solution format is not supported by the .NET 8 SDK.
- A legitimate Gothic II: Night of the Raven installation (GOG, Steam or retail)
- Windows x64 (ZenKit ships x64 native binaries); Linux x64 is expected to work but untested

## Build and run

```sh
dotnet build VibeOpenGothic.slnx
dotnet test VibeOpenGothic.slnx
dotnet run --project VibeOpenGothic -- -g "C:\Gothic II"
```

The entry point is a stub until T0.2: running it prints a version line and exits.

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
