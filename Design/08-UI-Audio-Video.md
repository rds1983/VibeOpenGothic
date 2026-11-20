# UI, fonts, audio and video

## UI stack

Myra provides the desktop, widgets, skinning and input. Its text rendering is
FontStashSharp-based (`SpriteFontBase`), which fits Gothic fonts well.

### Fonts

Gothic fonts are `.FNT` bitmap fonts (atlas + glyph metrics, sometimes a
palette). Convert them entirely at runtime:

1. Decode the FNT with ZenKit, normalize the atlas to RGBA (white glyphs with
   alpha, so Myra can tint them), and copy glyph rectangles, offsets and
   advances.
2. Create `new FontStashSharp.StaticSpriteFont(fontSize, lineHeight, new Point(
   atlasWidth, atlasHeight))`, populate `Glyphs` (`Int32Map<FontGlyph>`) with
   `Codepoint`, `Texture`, `TextureOffset`, `Size`, `RenderOffset` and
   `XAdvance`, set `DefaultCharacter`, and add kerning with
   `SetGlyphKernAdvance` where the FNT provides it.
3. Wrap it in a Myra `StylesheetFont` by assigning `StylesheetFont.Font`. That
   is the same property Myra uses for content-pipeline fonts.

Notes:

- FNT decoding in `Assets` produces CPU-only atlas and glyph data; the `Texture2D`
  and `StaticSpriteFont` are created in the UI layer so `Assets` stays
  GPU-free.
- No MGCB/content-pipeline step is needed; fonts are game data and are created
  from the mounted VFS.
- Cache one `StaticSpriteFont` per font file and size. Use `ScaledSpriteFont`
  for smooth UI scaling, or build a second instance for crisp text at very large
  UI scales.
- Provide the Gothic small/medium/large variants and a fallback font for
  characters missing from the bitmap (e.g. console text).
- Keep FNT fonts for authentic UI; use FontStashSharp's dynamic TrueType path
  only as a fallback.
- Text is localized by the game data; never hard-code user-visible strings.
- Interface scale is a setting; lay out with a virtual resolution and scale the
  whole UI.

Screens:

| Screen | Source | Milestone |
| --- | --- | --- |
| Loading/progress | App | T1.6 |
| Debug HUD, console | App, Game | T2.2, T3.10 |
| HUD: health, mana, stamina, focus name, subtitles | Game session | T4.2 |
| Main menu, settings, save/load list | Script menus + app | T4.2 |
| Inventory, equipment | Game inventory | T4.3 |
| Dialog and trade | Game.Dialogs | T4.3 |
| Journal, quest log | Game.Quests | T4.3 |
| Document/book reader | Item text pages | T4.3 |
| Chapter screen | Scripts + image/video | T4.5 |
| Video player | Bink files | T4.5 (stretch) |

Rules:

- UI reads gameplay state through read-only view models; it never mutates the
  world directly. Actions go through commands on the session.
- Script-created menus (`CREATE_MENU`) are materialized dynamically; do not
  hard-code Gothic's menu tree.
- Input is routed to UI first when a modal screen is open; gameplay input is
  suppressed, and the cursor is captured only in-game.
- All screens must be constructible in tests with a fake renderer.

## Audio

### Sound effects

- Gothic sound effects are WAV files (sometimes OGG in mods), addressed by name.
- Load via MonoGame `SoundEffect`, which supports PCM WAV on DesktopGL. For OGG
  or other formats, add a decoder behind `ISoundDecoder` rather than spreading
  format checks.
- 3D playback: position, velocity and rolloff per `SoundEffectInstance.Apply3D`,
  listener updated from the camera each frame.
- Categories: SFX, voice, UI, ambient, music. Volumes come from settings.
- Voice output from scripts (`AI_OUTPUT`, `AI_OUTPUTSVM`) maps to WAV names via
  the dialog output-unit data; subtitles are shown by the UI.
- Limit simultaneous instances (e.g. 32) with priority/eviction; log dropped
  sounds in developer mode.

### Music

Gothic music is DirectMusic: DLS soundfonts plus segment files, not plain audio
tracks. There is no .NET DirectMusic runtime. Options, in order of preference:

1. A managed DLS/SGT parser plus a SoundFont synthesizer (e.g. MeltySynth) with
   our own segment scheduler. Large but self-contained and mod-compatible.
2. Offline conversion of the installation's music to OGG at first run using a
   user-provided tool. Not distributable and fragile.
3. No music in the first release.

The first release ships SFX and voice; music is a separate milestone task (T4.6)
with option 1 as the goal. Do not block gameplay on it.

## Video

Chapter screens and intros are Bink videos. There is no managed Bink decoder and
the proprietary Bink SDK must not be used. Options:

- Integrate an FFmpeg-based decoder (LGPL build) as an optional native
  dependency.
- Implement a minimal Bink decoder for the subset used (large effort).
- Skip video and show the chapter screen text/image as a placeholder.

Decision: implement video last (T4.5). The game must be completable without it.

## Text and localization

- Use the language selected by the installation; do not auto-translate.
- Keep the original encoding rules from [02-Game-Data.md](02-Game-Data.md) at the
  data boundary.
- Subtitle timings come from the dialog output units; fall back to a readable
  duration if missing.
- The console and debug overlays are English-only developer tools.

## Performance and accessibility

- UI must not allocate per frame; cache text layouts where the content is static.
- Support UI scale, subtitles, and volume sliders from the start; colorblind
  options and remappable keys come later.
- Audio loading is asynchronous and cancellable; world changes must not leave
  stale sound instances playing.
