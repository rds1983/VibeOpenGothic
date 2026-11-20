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

Discovery uses the command line, `VOG_GOTHIC_PATH`, settings, and finally
common locations (including Steam library folders). The interactive folder
picker is deferred until a UI exists.

Validation: the root must contain `System/Gothic.ini`, `Data/`, `_work/Data`
and `_work/Data/Scripts/_compiled`, and at least one non-empty `Data/*.vdf`.
Directory and file names are resolved case-insensitively because retail installs
use `system` (lowercase). On failure, print the searched paths and exit with a
readable error. `--probe` prints the resolved root without starting a game.

### Version detection

Detect `Gothic1`, `Gothic2Classic` or `Gothic2NotR` from `Gothic.ini`
(`[GAME] PATCHVERSION`) plus the presence of the add-on data (`OU.DAT`/`OU.BIN`
under `_work/Data/Scripts/content/CUTSCENE`, add-on worlds). Allow explicit
overrides `-g1`, `-g2`, `-g2c` and log the detected version. The first release
targets Gothic2NotR; Gothic 1 support is best-effort (T5.4).

## Virtual file system

All game assets are addressed by a normalized, case-insensitive path
(`/`-separated, no leading slash in documentation; the implementation accepts
both separators and a leading slash). `IGameVfs` wraps `ZenKit.Vfs`:

```csharp
bool FileExists(string nameOrPath);
bool DirectoryExists(string path);
Stream OpenRead(string nameOrPath);
VfsFileEntry? Find(string nameOrPath);
IEnumerable<VfsFileEntry> EnumerateFiles();
```

Bare names (e.g. `GOTHIC.DAT`) resolve by file name anywhere in the VFS, like
the original engine. Paths resolve from the root.

Mount order and precedence:

1. Enumerate `Data/` recursively for non-empty `.vdf` and `.mod` files,
   skipping language-neutral `.NX.` directories.
2. Include `.mod` files only when a `-game:` manifest is active (see Mods).
3. Sort by descending priority: mods first, then newer VDF header timestamps,
   then enumeration order. Mount with ZenKit's `Older` overwrite behavior, so
   the first archive to provide a path wins.
4. Mount the `_work` host directory at `/_work` with `None`, so loose files
   fill gaps but never override archives. This matches OpenGothic.

Mount errors are logged and skipped. The game refuses to start when
`GOTHIC.DAT` (or the mod's `GAME` file) cannot be resolved. `--probe` lists every
archive with size and timestamp, the total VFS file count, the resolved version
and sample assets; it is the first thing to check for "missing asset" bug
reports.

## ZenKit usage notes

These are binding-specific behaviors that have already caused hangs or crashes:

- List-valued properties (`mesh.Positions`, `mesh.Features`, `world.Mesh.Polygons`,
  `submesh.Wedges`, ...) rebuild a managed list on **every** access. Never index
  such a property inside a loop; materialize it once:
  `var positions = mesh.Positions;`.
- `World.Mesh` is borrowed from the world. Keep the `World` alive while reading
  and converting the mesh (`GC.KeepAlive(world)` after the conversion), otherwise
  the native mesh can be freed underneath the wrapper.
- Native wrappers are not thread-safe. Confine them to the loader thread.

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
- Without `-game:`, `.mod` files in `Data` are ignored, matching OpenGothic and
  avoiding accidental G2 Classic overrides on NotR installs.
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
