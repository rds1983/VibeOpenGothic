# Game data access

## Installation

A Gothic installation root contains at least:

```
System/Gothic.ini
System/Gothic2.exe
Data/*.VDF
_work/            (optional, loose files that override VFS content)
```

We never write into the installation. Settings, saves and logs live in
`%LOCALAPPDATA%\VibeOpenGothic\` (override with `--data-dir`).

### Discovery order

1. `-g <path>` command line.
2. `VOG_GOTHIC_PATH` environment variable.
3. `gothicPath` from `settings.json`.
4. Common locations: `C:\Program Files (x86)\JoWooD\Gothic II`,
   `C:\Gothic II`, Steam `steamapps\common\Gothic II`, and the equivalents on
   Linux via Proton.
5. Interactive folder picker (when a UI is available).

Validation: the root must contain `System/Gothic.ini` and at least one non-empty
`Data/*.vdf`. On failure, print the searched paths and exit with a readable
error. `--probe` prints the resolved root without starting a game.

### Version detection

Detect `Gothic1`, `Gothic2Classic` or `Gothic2NotR` from `Gothic.ini`
(`[GAME] PATCHVERSION`) plus the presence of the add-on data (`OU.DAT`/`OU.BIN`
under `_work/Data/Scripts/content/CUTSCENE`, add-on worlds). Allow explicit
overrides `-g1`, `-g2`, `-g2c` and log the detected version. The first release
targets Gothic2NotR; Gothic 1 support is best-effort (T5.4).

## Virtual file system

All game assets are addressed by a normalized, case-insensitive path
(`\`-separated, no leading slash). `GameVfs` wraps `ZenKit.Vfs` and exposes:

```csharp
bool Exists(string path);
Stream OpenRead(string path);
IReadOnlyList<string> List(string directory, string extension); // optional
```

Mount order:

1. Base archives from `Data/`, enumerated recursively, skipping language-neutral
   `.NX.` directories.
2. Archives ordered so that newer files win (ZenKit `VfsOverwriteBehavior`),
   matching OpenGothic.
3. Mod archives from `modfile.ini` (`[FILES] VDF=`), last and in listed order.
   If the key is present, only listed `.mod` files are mounted.
4. `_work` host directory as `/_work` read-only (loose-file overrides).

Mount errors are logged and skipped; the game refuses to start only if
`GOTHIC.DAT` cannot be found. `--probe` lists every mounted archive with file
counts and a timestamp, which is the first thing to check for "missing asset"
bug reports.

## File formats

All format parsing is delegated to ZenKit. Our loaders convert ZenKit objects to
engine data; see [01-Architecture.md](01-Architecture.md).

| Extension | Content | ZenKit type | Used by |
| --- | --- | --- | --- |
| `.VDF`, `.MOD` | Virtual disk archive | `ZenKit.Vfs` | Data |
| `.ZEN` | World: BSP tree, VOB tree, waynet | `ZenKit.World` | World |
| `.DAT` | Compiled Daedalus scripts | `ZenKit.DaedalusScript`, `DaedalusVm` | Game.Scripts |
| `.MDS` | Model script: hierarchy link, animations, events | `ModelScript` | Assets.Models |
| `.MDH` | Model hierarchy / skeleton nodes | `ModelHierarchy` | Assets.Models |
| `.MDM` | Multi-resolution model mesh | `ModelMesh` | Assets.Models |
| `.MMB` | Morph mesh | `MorphMesh` | Assets.Models |
| `.MSH` | Static or world mesh | `Mesh` | Assets.Meshes |
| `.TEX` | Paletted/compressed texture | `Texture` | Assets.Textures |
| `.FNT` | Bitmap font | `Font` | UI (converted to a FontStashSharp `StaticSpriteFont`) |
| `.WAV` | Sound effect | BCL audio decode | Audio |
| `.DLS`, `.SGT` | DirectMusic soundfont and segment | none | Audio (late, T4.6) |
| `.BIK` | Bink video | none | Video (late, T4.5) |
| `OU.DAT`, `OU.BIN` | Cutscene output units | `CutsceneLibrary` | Game.Dialogs |
| `.SAV` | Original save game | `ZenKit.SaveGame` (metadata) | not loaded |
| `.INI` | `Gothic.ini`, `SystemPack.ini`, `modfile.ini` | own parser | Data |

Notes:

- Text encoding is a localization concern. Use strings returned by ZenKit; for
  loose `.ini` files, decode with the encoding selected by the install language
  (Windows-1252 default, 1250/1251 for CEE/Cyrillic builds).
- Gothic units are centimetres, Y-up and left-handed. The engine is Y-up and
  right-handed, so every loader converts geometry and transforms to engine
  space (see [01-Architecture.md](01-Architecture.md) and
  [03-Models-And-Skinning.md](03-Models-And-Skinning.md)).
- Timings are milliseconds.
- Models reference textures and skeletons by name; name resolution is
  case-insensitive and tries `_work` first, then the VFS.
- `.SAV`/ZenKit `SaveGame` is read-only metadata support. VibeOpenGothic uses its
  own save format (see [06-Gameplay.md](06-Gameplay.md)); importing original
  saves is a non-goal.

## Mods

Supported: content mods that ship archives listed in `modfile.ini` and rely on
regular Daedalus scripting.

- `modfile.ini` keys: `[FILES] GAME`, `VDF`, `OUTPUTUNITS`;
  `[SETTINGS] WORLD`, `PLAYER`.
- Command line: `-game:<modfile.ini>`; `--` style aliases are also accepted.
- The selected mod's `GAME` value replaces `GOTHIC.DAT`.
- Never modify the mod or the installation; write caches elsewhere.

Not supported in the first release: Union plugins, Ikarus/LeGo memory patches,
and anything requiring the original 32-bit executable. See
[00-Vision.md](00-Vision.md).

## Asset identity and caching

- Cache keys are the normalized VFS path plus conversion options.
- Textures are cached by resolved texture name (animation frames share one
  entry until animated textures are implemented).
- Models are cached per `MDS` path; instances share GPU buffers but own their
  pose state.
- On world change, world-specific caches are released; globally shared assets
  (fonts, UI textures, common item models) stay.

## Test data

Unit tests must not require an installation. When a test does need real data:

```csharp
[Trait("Category", "RequiresGameData")]
public void Parses_real_world() { ... }
```

`GothicDataFixture` resolves the installation the same way as the game. If it is
missing, the test is skipped with a clear message (custom `Skip` reason), not
failed. CI runs the data-free suite; a developer machine runs both with
`VOG_GOTHIC_PATH` set.

Never commit extracted game data, VDFs, saves or screenshots containing assets.
