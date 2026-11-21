# Scripting

## Daedalus VM

Gothic's game logic lives in compiled Daedalus scripts (`GOTHIC.DAT`, or the mod's
`[FILES] GAME` equivalent). `ZenKit.DaedalusVm` implements the bytecode VM and
symbol table; we do not write a VM.

`ZenKit.DaedalusVm` provides:

- Loading from a `ZenKit.Vfs` path, plus typed `Call`, `Call<T...>` and
  `InitInstance<T>` helpers.
- Symbol lookup by name, index or address.
- Typed instance classes for NPCs, items, focuses, spells, menus, missions,
  music and particle effects.
- External registration through generic delegates, with up to 10 parameters.
- Globals `GlobalSelf`, `GlobalOther`, `GlobalVictim`, `GlobalHero`,
  `GlobalItem`.

We wrap it in `Game.Scripts.DaedalusHost` so no other layer sees ZenKit types.

## Boot sequence

1. Resolve the script path from the install descriptor (`GOTHIC.DAT` by default).
2. Create the VM and register every external implemented by the current
   milestone. Register a default handler that logs unimplemented calls with
   their name and arguments (rate-limited).
3. Initialize global script state. The original startup order is:
   - `startup_global`, then `startup_<worldName>` on the first world load;
   - `init_global`, then `init_<worldName>` on every world load;
   - `initPerceptions` early (before a world exists).
   Each call is guarded by `hasSymbolName`.
4. Create/allocate instance objects for the script's `instance` definitions as
   they are needed (NPCs from the world, items, interactive objects).
5. Run the script functions that populate the world (`InitAll`-style functions
   are called from the startup scripts themselves; do not duplicate them).

Script names are case-insensitive; preserve the original spelling for logging.

## Instance model

The VM stores instance fields in a flat memory block. We keep two objects per
scripted entity:

- **Script instance** (`ZenKit.Daedalus.*Instance`): owned by the VM, used for
  reading/writing symbols and passing `self`/`other`.
- **Runtime entity** (`Npc`, `Item`, `Interactive`, ...): owns behavior, physics,
  animation and inventory.

`ScriptObjectRegistry` maps instance handles to runtime entities and back. Rules:

- Never keep a raw native pointer beyond a VM call; resolve through the registry.
- A runtime entity may exist without a script instance (e.g. an item dropped by
  a test) and vice versa (an instance defined but not spawned).
- Deleting a world removes runtime entities but not script symbols; script state
  (global variables) survives world changes within a session.
- Attributes (health, mana, strength, ...) are copied to the runtime entity at
  spawn and written back when scripts modify them. Define one direction per
  field to avoid double bookkeeping.

## Externals

Externals are grouped by system and implemented in batches:

| Group | Examples | Milestone |
| --- | --- | --- |
| Core/math/string | `PRINT`, `CONCAT`, `INT_TO_STRING`, `RANDOM` | M2 (T3.1) |
| Startup/spawn | `WLD_INSERTNPC`, `WLD_INSERTITEM` | M2 (T3.2) |
| Appearance | `MDL_SETVISUALBODY`, `MDL_SETMODELFATNESS`, `MDL_SETMODELSCALE` | M2 (T2.9) |
| AI output | `AI_OUTPUT`, `AI_OUTPUTS`, `AI_OUTPUTSVM`, `MOVE_NPC` | M3 (T3.3) |
| World | `WLD_*`, `TRIGGER`, `PLAY_ANIMATION`, `SND_*` | M3 (T3.3) |
| NPC | `NPC_*`, `CREATE_NPC`, `NPC_GETATTR`, inventory | M3/M4 (T3.4, T3.5) |
| Items | `CREATE_ITEM`, `ITEM_*`, `ITM_*` | M4 (T3.5) |
| Combat | `AI_*FIGHT*`, damage, `DEF_HIT*` windows | M4 (T3.7) |
| Magic | `SPELL_*`, runes, mana | M4 (T3.8) |
| UI | `CREATE_MENU`, `CREATE_MENU_ITEM`, `PRINT_*` | M5 (T4.2) |
| Music | `MUSIC_*` | M5 (T4.6) |

The VM host and the startup chain are needed before anything script-owned can be
seen, so `T3.1`/`T3.2` land in M2 with the visibility tasks rather than with the
rest of the script work. Everything else can follow behaviour-first.

Every external implementation:

- Validates its arguments and logs a clear error instead of throwing across the
  native boundary.
- Is registered in one place, with a comment containing the script prototype.
- Has a direct unit test where possible (call the delegate with a fake context).

`--check-scripts` runs the VM symbol table against the registry and prints:
missing externals, unused implementations, and symbols with signature mismatches.
This is the coverage evidence for script tasks.

## Dialogs and Info instances

Dialogue is driven by `C_INFO` instances stored in the script's instance symbol
list:

- A dialog offer is an info instance with `description`, `information`,
  `permanent`, `important`, condition functions and a script function.
- The dialog UI asks the script for available choices (condition evaluation in
  the appropriate `self`/`other` context), then calls the selected function.
- `AI_OUTPUT`/`AI_OUTPUTSVM` route voice and subtitle output through an
  `IAiOutputPipe` implemented by the dialog UI.
- Trade is a dialog mode with its own inventory exchange; do not model it as a
  separate conversation.
- Cutscene output units (`OU.DAT`/`OU.BIN`) map voice IDs to text; load once per
  language.

Dialogs must be scriptable without UI in tests: the UI consumes an
`IDialogPresenter` abstraction, and a headless presenter records the choice
sequence.

## AI, routines and state

Gothic AI is partly script (`AI_*` externals, routines, `TA_*` daily schedules)
and partly engine state (perception, fight state, path following). The engine
implements:

- A state machine per NPC (`AIState`: stand, walk, goto, follow, wait, alert,
  fight, dead, ...), driven by script commands and perception.
- Perception (see/hear/feel events) with ranges and priorities from script
  symbols.
- Daily routines: walk to a waypoint, perform an animation for a time window,
  repeat. Routines are script data, not hard-coded.
- Fight AI: choose attacks, block, flee based on the `fightai` definitions
  (`FIGHTAI.DAT` / `FightAiInstance`) and the NPC's combat state.
- Dialog lock: while in a dialog, other AI commands are queued or ignored as in
  the original.

Pathfinding is the authored waynet plus a Dijkstra over its edges, matching the
original; there is no navmesh, Recast or A* (see
[04-World-And-Physics.md](04-World-And-Physics.md)). `AI_GotoWaypoint` resolves
a name to a node and then behaves like `AI_GoToPoint`. Off-graph movement is
direct steering; blocked steps retry by turning the velocity in 5 degree
increments up to about 35 degrees each way, which is how the original avoids
obstacles. Free points (beds, anvils, teaching slots) are a separate exclusive
list.

## Menus and UI scripts

Gothic's main menu and settings menus are script-created (`CREATE_MENU`,
`CREATE_MENU_ITEM`). Replicating that system is **not** required initially.
The client supplies plain Myra screens; the menu externals are registered as
no-ops that log once, so scripts do not crash when they call them.

If script-created menus are implemented later as optional parity work, build the
widgets from instances and send selection events back into the VM. Do not
hard-code Gothic's menu tree.

## Coordinates and angles at the script boundary

Daedalus code was written for Gothic's left-handed space; the engine stores
right-handed data. Externals that exchange geometry must therefore:

- return engine-space positions and directions, and accept engine-space inputs;
- negate scalar yaw/heading angles if an API uses them, and convert rotations
  through `Core.CoordinateSystem`;
- keep distances, dot products and relative directions unchanged (they are
  invariant under the reflection).

Most scripts use waypoint names and distances, which are unaffected. Audit for
scripts that hard-code absolute coordinates or rotations during T3.2 and shim
them at the boundary, with a log entry. Do not expose a mixed coordinate space.

## Save state

Only script-visible state needs to be saved:

- Global variables (including arrays) and symbol state that scripts mutate.
- Per-entity instance memory for spawned NPCs/items, keyed by runtime ID.
- Quest/mission state, dialog history (`permanent` info flags), guild, chapter.

Do not serialize native VM addresses or pointers. The save format is described in
[06-Gameplay.md](06-Gameplay.md).

## Error policy

- Script errors must not crash the process. Log the function, symbol, arguments
  and a short stack trace (`PrintStackTrace`), then return a default value.
- An unimplemented external logs once per name at warning level and returns a
  type-appropriate default.
- Infinite script loops are the script's bug; the VM has no timeout. Add a
  watchdog in developer mode (`--devmode`) that logs if one call exceeds a
  threshold.

## Testing

Data-free:

- Registry/signature tests: every registered external's delegate arity matches
  its declared parameter count.
- Argument marshaling tests for ints, floats, strings, instance handles and
  return values.
- Dialog flow with a scripted presenter and a fake VM context.
- Quest log state transitions.

With game data (`RequiresGameData`):

- Load `GOTHIC.DAT`, assert symbol count and that core symbols exist.
- Run `startup_global`/`init_global` on a fresh VM and assert no external is
  missing for the base script.
- Walk every `C_INFO` instance and verify its condition function has a valid
  address.
