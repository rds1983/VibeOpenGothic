# Testing

## Goals

- Catch data-conversion errors (the riskiest area) without a GPU.
- Keep gameplay deterministic and headless-testable.
- Make "it works on my machine with game data" reproducible through explicit
  diagnostics and screenshots.
- Never require Gothic data in CI.

## Test levels

| Level | Runs in CI | Requires GPU | Requires game data |
| --- | --- | --- | --- |
| Unit: math, conversion, parsing helpers, state machines | yes | no | no |
| Headless integration: session, world model, physics, save/load | yes | no | optional |
| Data integration: real VDF/ZEN/DAT/models | no (developer) | no | yes |
| Visual: screenshots, renderer smoke test | no | yes | yes |
| Performance: frame time, load time, allocations | no | yes | yes |

## Structure

```
VibeOpenGothic.Tests/
  Unit/            pure tests
  Integration/     multi-component, headless
  Data/            [Trait("Category","RequiresGameData")]
  Fixtures/        paths, temp dirs, clocks, fake services
    GothicDataFixture.cs
    ManualClock.cs
    FakePresenter.cs
```

Test naming: `Method_Scenario_ExpectedResult`.
One behavior per test; use `[Theory]` for data tables.

## Handling game data

`GothicDataFixture` resolves `-g`/`VOG_GOTHIC_PATH`/settings and exposes the
mount point. Tests that need it:

```csharp
[Trait("Category", "RequiresGameData")]
public void Loads_newworld()
{
    if (!_data.IsAvailable) Assert.Skip(_data.SkipReason);
    ...
}
```

(`Assert.Skip` is xUnit v3; if the project stays on xUnit 2, add
`Xunit.SkippableFact` and use `[SkippableFact]`.)

Rules:

- Never download, copy or commit game data.
- Data tests must be read-only and must not write into the installation.
- A data test that crashes must report which asset was being processed. Use the
  logging scope from `AssetLibrary`.
- Keep a small "known worlds" list in one fixture so version changes are handled
  in one place.

## Designing for testability

- Conversion code takes plain arrays/structs (positions, weights, faces), not
  ZenKit objects, so tests do not need to construct native wrappers.
- ZenKit-to-DTO adapters are thin and covered by data tests.
- Time, randomness, file system and graphics device are injected interfaces.
- Gameplay sessions can run without a renderer (`NullRenderer`) and without
  audio (`NullAudio`).
- Physics scenarios run with a fixed timestep and a seeded simulation.

## What to test per area

| Area | Tests |
| --- | --- |
| Coordinate conversion | Involution, point/normal/matrix/quaternion equivalence, winding/normal relationship, double-conversion detection |
| Skinning conversion | Bind consistency, weight normalization, padding, bone limits, equivalence with the four-position formula |
| Animation | Frame-to-time conversion, loop/transition timing, event firing, overlay blending |
| World loading | VOB type mapping, transform composition, waynet integrity, missing assets |
| Physics | Drop/rest, slopes, steps, wall slide, moving platforms, ray masks |
| Scripts | External arity, marshaling, dialog flow, quest state, save/load of symbols |
| Gameplay | Inventory transfer, trade price, combat with fixed seeds, clock advance |
| Save/load | Round-trip each entity, version migration, atomic write, corrupt archive |
| UI/fonts | FNT glyph metrics, atlas bounds, missing-glyph fallback, StaticSpriteFont creation |
| Rendering | Screenshot smoke test (window opens, non-black frame, expected object count) |

## Golden data

- Synthetic golden files for pure algorithms (mesh packing, skin baking,
  animation sampling). Store them as small, human-readable JSON/binary under
  `VibeOpenGothic.Tests/Golden/`.
- Do not hash real game assets as golden values; game versions differ. Use
  diagnostics with thresholds (`max error < 0.5 cm`, node count, triangle count
  within a percentage) instead.
- Update golden files only with a doc note explaining why the behavior changed.

## Manual verification

Every task that changes behavior has a checklist in its PR/commit description:

1. Exact commands to run (including `-g` path or a test name).
2. What to look for (screen, log lines, counters).
3. Screenshot for visual tasks (`--screenshot` output path).
4. Known issues and expected log noise.

If a task cannot be verified without game data, say so and list what the
developer should check.

## Performance checks

- `--benchmark <world>` loads a world, runs a fixed camera path for N frames and
  prints: average/99th percentile frame time, update time, draw calls, triangles,
  managed allocations per frame, loaded asset counts.
- Run before/after renderer and world-loading changes; record numbers in the
  task notes. Do not fail CI on performance yet.

## CI

- `dotnet build VibeOpenGothic.slnx -c Release` and
  `dotnet test VibeOpenGothic.slnx -c Release` on Windows.
- Warnings are errors; nullable warnings are errors.
- Publish a test TRX artifact and the game build artifacts.
- Add Linux CI once the project runs there; ZenKit and MonoGame both support it.
