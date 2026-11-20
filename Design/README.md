# Design documentation

These documents are the source of truth for behavior and architecture. If code
and docs disagree, fix the docs or the code in the same change.

| Document | Contents |
| --- | --- |
| [00-Vision.md](00-Vision.md) | Goals, non-goals, success criteria, risks |
| [01-Architecture.md](01-Architecture.md) | Projects, layers, composition, conventions |
| [02-Game-Data.md](02-Game-Data.md) | Install discovery, VFS, mods, file formats |
| [03-Models-And-Skinning.md](03-Models-And-Skinning.md) | LHS-to-RHS conversion, Gothic 4-position skinning, animation |
| [04-World-And-Physics.md](04-World-And-Physics.md) | ZEN/VOBs, navigation, Bepu physics, characters |
| [05-Scripting.md](05-Scripting.md) | Daedalus VM, externals, dialogs, AI |
| [06-Gameplay.md](06-Gameplay.md) | Session, NPCs, combat, inventory, saves |
| [07-Rendering.md](07-Rendering.md) | Renderer stages, stock effects first, later visuals |
| [08-UI-Audio-Video.md](08-UI-Audio-Video.md) | Myra UI, fonts, sound, music, video |
| [09-Testing.md](09-Testing.md) | Test strategy, game-data fixtures, manual checks |

Roadmap: [../TASKS.md](../TASKS.md). Workflow: [../AGENTS.md](../AGENTS.md).

Conventions for these documents:

- Describe intended behavior, not the original C++ code.
- Cite the game-data format or observed behavior when a rule is non-obvious.
- Keep each document short enough to read in one sitting; split it when it grows.
- Mark unverified assumptions with **Assumption:** and add a task to verify them.
