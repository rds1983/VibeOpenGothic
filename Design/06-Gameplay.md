# Gameplay

## Session lifecycle

```
Main menu -> New game / Load
  -> GameSession (scripts, player, settings, clock)
       -> World load (ZEN + VOBs + NPCs + waynet)
       -> startup/init script calls
       -> play loop: input -> NPC/AI -> physics -> scripts -> animation -> audio
  -> world change: keep session, unload old world, load new, re-init scripts
  -> save / load: session state <-> archive
  -> exit: flush settings, unload
```

A session owns the VM, the world, the player and the game clock. The renderer
and UI are views over the session; they hold no gameplay state.

## Player and NPCs

An NPC combines a script instance, a runtime entity, a visual (body parts,
armor, weapon), animation state, physics capsule and inventory.

Fields that must round-trip through save/load and script access:

- Attributes: health/max, mana/max, strength, dexterity, level, experience,
  learning points, guild, armor per damage type, fight skills, talents.
- State: AI state and sub-state, current waypoint/routine, dialog partner,
  fight target, last perception, dead/alive, unconscious.
- Appearance: body mesh, head mesh, hair, armor visual, weapon and shield slots,
  skin color, fatness, voice, name.
- Inventory and equipped items.
- Script instance memory for all NPC-owned symbols.

Appearance is **not** stored in `C_NPC`; it is assigned at instance
initialization by `Mdl_SetVisualBody(bodyMesh, bodyTexture, bodyTextureColor,
headMesh, headTexture, teethTexture, armor)` plus `Mdl_SetModelFatness` /
`Mdl_SetModelScale`. The VM host records those arguments during instance init
and the appearance assembler (T2.9) turns them into renderables. Item
appearance works the same way through `C_ITEM.VISUAL`, because world `oCItem`
VOBs carry no visual.

NPC creation: `CREATE_NPC` allocates an instance, the world spawns a runtime
NPC from it. The same NPC can be inserted at a waypoint, teleported, or moved
between worlds.

## Player controls

- First/third person camera, collision-aware.
- Walk/run/sneak; strafe and turn; attack, block, cast, use, take, talk.
- Keyboard/mouse defaults from `Gothic.ini` (`[KEYS]`/`[KEYSDEFAULT*]`), with
  gamepad support later (T5.3).
- Marvin/debug mode (`-devmode`): free camera, teleport, spawn item, full heal.
  Debug commands are not part of the save file.

## Inventory and equipment

- Slot categories: melee, ranged, armor, rune, amulet, ring (two ring slots),
  torches and ammo.
- Stacking, weight, value, condition, flags (mission item, undroppable), visual
  and script instance per item.
- Transfers: pick up, drop, put/take from container, give/take in trade, buy/sell
  with price modifiers from the NPC's trade skill and attitude.
- Using an item calls its script `on_state` function; effects (heal, mana,
  permanent bonus, spell scroll) are scripted, not hard-coded.
- Equipment changes update the visual model (armor/weapon attachments) and stats.

## Combat

Combat is data-driven; formulas and constants come from the scripts and
`FightAiInstance`/`DamageCalculator` data. Port the behavior, not guessed
numbers:

- Melee attacks use hit windows defined by animation events. The attack connects
  only inside the window and only if the target is in range and the hit roll
  succeeds.
- Damage is computed from weapon damage, attributes, skill, armor per damage
  type, criticals and difficulty settings.
- Ranged attacks spawn projectiles with ballistic motion and raycast collision.
- Spells consume mana, check the rune/scroll, and create projectile or area
  effects. Spell FX hooks are optional for the first pass.
- Blocking/parrying, stagger, knockdown, death and unconsciousness are states
  with animations; scripts are notified (`DEF_HIT*`, death callbacks).
- Death: drop loot according to script rules, grant XP, update quest state,
  notify nearby NPCs, keep a corpse or remove it as the original does.
- Friendly fire, guild hostility and monster aggro are perception/AI concerns,
  not special cases in the damage code.

## Magic

Runes, scrolls and spells are item/instance driven. The engine provides mana,
casting animation, target selection, projectile/area execution and script
callbacks. Spell-specific logic belongs to the script data.

## Quests and documents

- `C_MISSION` instances provide quest topics and status; the journal groups
  entries by topic section and shows the current status.
- Dialog `permanent` info flags control what has been said; store them per NPC.
- Documents/books are text pages from item instances; the reader UI supports
  pages and fonts.
- Chapter transitions play a chapter screen and run chapter scripts.

## Game clock

- The world clock advances with play time multiplied by the configured rate.
- Day/night affects sky, light, NPC routines, shop hours and some triggers.
- Sleeping and waiting advance the clock, run script functions and respawn
  eligible NPCs/monsters according to the spawn rules.
- Time is saved with the session and restored on load.

## Save and load

Format: a ZIP archive (`.sav`) written atomically (temp file + rename).

```
manifest.json      format version, app version, game version, mod, world,
                   playtime, timestamp, player summary, thumbnail name
symbols.json       script globals and instance memory
state.json         player, NPCs, items, interactive objects, triggers,
                   quests, chapter, clock, camera mode
screenshot.png     thumbnail (optional)
```

Rules:

- `FormatVersion` is an integer per file, independent of the app version.
  Loading an older format runs explicit migrations; loading a newer format fails
  with a clear message.
- Save only script-visible, gameplay-relevant state. Rebuild derived caches
  (meshes, animation poses, physics) on load.
- Use runtime IDs that are stable within a save (script instance index + world
  spawn index); never native pointers.
- Saves live in `%LOCALAPPDATA%\VibeOpenGothic\saves`; quick save is slot 0,
  matching OpenGothic's naming for familiarity.
- Original Gothic `.SAV` files are not imported in the first release.
- Save creation snapshots on the main thread, then compresses/writes on a
  background task; UI shows progress and disables a second save.

## Settings

- Video: resolution, window mode, vsync, render scale, FOV, AA (stock MSAA).
- Audio: master/music/SFX volume.
- Gameplay: difficulty, invert mouse, interface scale, subtitles, language if
  the installation provides multiple languages.
- Keys: read defaults from `Gothic.ini`, store user overrides in
  `settings.json`. Never modify `Gothic.ini`.

## Failure and edge cases

- Missing voice files, subtitles or textures degrade gracefully.
- A script that teleports the player inside geometry must be recovered by a
  ground snap or a fallback waypoint, with a log entry.
- NPCs without a valid spawn waypoint are logged and skipped, not fatal.
- Loading a save whose mod/world is missing fails before mutating the current
  session.

## Testing

- Headless session tests with synthetic scripts: dialog choice, inventory
  transfer, trade pricing, quest status, day/night advance.
- Save/load round-trip for every entity type, including unknown fields from a
  newer minor version (must be ignored on load).
- Combat scenario tests with fixed random seeds: hit chance, damage, armor,
  death and loot.
- Integration with game data: load a save created at a fixed checkpoint, run N
  ticks, compare deterministic values (player position, HP, NPC count, quest
  state).
