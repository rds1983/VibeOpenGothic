# Agent workflow

This repository is developed by agents under developer supervision. Read this
file at the start of every session, then read
[Design/README.md](Design/README.md) and the documents relevant to the task.

## Non-negotiables

1. **One task at a time.** Work on exactly one task from [TASKS.md](TASKS.md).
2. **No self-approval.** A task is never finished because you think it is. The
   developer must explicitly confirm it.
3. **No game assets in git.** Never commit VDFs, extracted data, saves,
   screenshots containing assets, or anything from a Gothic installation.
4. **MIT-compatible only.** New dependencies must be MIT/BSD/Apache-2.0 (or
   similarly permissive) and documented in Design/01. Do not copy code from
   copyleft projects.
5. **Behavior over code.** Port functionality. The original C++ is a reference;
   keep C# idiomatic and structured for this repository.
6. **Documentation is part of the change.** If behavior or architecture changes,
   update `Design/` in the same commit.
7. **Leave the tree clean.** Build and test before asking for confirmation. Do
   not leave commented-out experiments or debug output.

## The loop

### 1. Get the next task

- Read `TASKS.md`, find the first unchecked task that is not blocked, respecting
  milestone dependencies. Use `Current focus` if it is set.
- If the developer named a task, use that instead.
- Check whether prerequisites are actually done. If not, stop and tell the
  developer; do not invent missing work.
- Mark the task `[~]` in `TASKS.md` (in progress) before starting. Only one task
  may be `[~]`.
- Read the linked design docs and the relevant original OpenGothic behavior.
  This repository is a port; when a detail is unclear, inspect the original
  project's source or its data, then record the finding in `Design/`.

### 2. Do it

- Implement the smallest complete version that satisfies "Done when".
- Keep the layering and conventions from
  [Design/01-Architecture.md](Design/01-Architecture.md).
- Prefer stock MonoGame effects and simple code until the task explicitly asks
  for more.
- Do not do unrelated refactors or fixes. If you find a bug outside the task,
  add a new task to `TASKS.md` and mention it in your report.
- If a requirement is ambiguous, ask the developer early instead of guessing.
  If you must proceed, record the assumption in the design doc and in your
  report.
- Keep commits out of this phase; the task is not confirmed yet.

### 3. Test thoroughly

- Run the full build and test suite (`dotnet build`, `dotnet test`) and fix every
  failure and warning.
- Add unit tests for new logic. Conversion and gameplay logic must be testable
  headless; do not require a GPU or game data for unit tests.
- If the task needs game data, run the `RequiresGameData` tests and diagnostics
  locally; if the data is unavailable, say exactly what was not verified.
- For rendering or UI tasks, produce a screenshot or a concrete manual checklist
  with commands, expected result and known issues.
- Re-run the tests after the final edit. Never report "tests pass" based on an
  earlier run.
- Report the exact commands you ran and their result.

### 4. Update documentation

- Update the design documents affected by the change. They are the source of
  truth, not an afterthought.
- Update `README.md` only if build/run/layout information changed; it stays
  brief.
- Update task notes in `TASKS.md` only for status, blockers and discovered
  follow-up tasks at this stage.
- If a design decision changed, explain why in the doc and in your report.

### 5. Ask the developer whether the task is finished

Stop and ask. Do not mark the task `[x]` yet. Present:

- Task ID and title.
- What changed, in 3-8 bullets, with file paths.
- How it was verified: commands, tests, diagnostics, screenshots, and what was
  not verified.
- Known limitations, assumptions and follow-up tasks you added.
- The exact manual steps the developer can use to check it.

Wait for the next developer message.

**What counts as confirmation:** an explicit statement that the task is
finished/confirmed/approved, e.g. "T1.3 is done", "confirmed", "approve". If the
reply is ambiguous ("ok", "continue", "looks fine" with no explicit approval),
ask a direct yes/no question:

> Is task T1.3 finished and confirmed, or should I change something first?

**What does not count:** silence, a new unrelated instruction, your own tests
passing, or a request to continue with the next task.

If the developer asks for changes: make them, re-test, update docs, and ask
again. Do not commit the unconfirmed work.

### 6. Close the task and commit

Only after explicit confirmation:

1. Change `[~]`/`[ ]` to `[x]` in `TASKS.md` and add the confirmation date and
   commit hash placeholder if you use one (e.g. `[x] T1.3 ... (2026-11-20)`).
2. Review the remaining unchecked tasks. You are free to reorganize them:
   - split tasks that are too large;
   - add tasks discovered during the work;
   - remove tasks that are no longer relevant, with the reason in the commit
     body;
   - reorder by dependency and risk.
   Do not delete completed tasks, renumber IDs, or mark anything else as done.
3. Stage only files belonging to the task:

   ```sh
   git status
   git diff --stat
   git add <paths>
   git diff --staged
   ```

4. Commit with the task ID:

   ```
   T1.3: convert Gothic four-position skinning to standard LBS

   - Bake bind-pose positions from 4 influences
   - Compute inverse bind matrices and diagnostics
   - Add synthetic equivalence tests and --check-models
   - Update Design/03
   ```

5. Push to the current branch:

   ```sh
   git push origin HEAD
   ```

   If the push is rejected, report it; do not force-push or rewrite history.
6. Report the commit hash and that the push succeeded.

## Rules for task selection

- Respect dependencies: do not start T1.1 before T1.0 (the coordinate conversion
  layer); do not start T1.6 before T1.1/T1.2; do not start T1.7 before
  T1.3/T1.4; do not start UI before gameplay data exists; do not start visual
  polish before M3.
- If a task is too large for one session, split it in `TASKS.md` and do the first
  part; keep the original ID for the first part and add a suffix for the rest.
- If a task is blocked, mark `[!]` and write the blocker and what would unblock
  it. Ask the developer to choose the next task.
- Do not work on the backlog section; it is unscheduled.

## Engineering expectations

- **Build:** `dotnet build VibeOpenGothic.slnx -c Release`
- **Test:** `dotnet test VibeOpenGothic.slnx`
- **Run:** `dotnet run --project VibeOpenGothic -- -g "<Gothic II path>"`
- Keep the core headless. Gameplay, world and conversion code must not depend on
  `GraphicsDevice`.
- Indent C# with tabs (tab width 4). XML, JSON and Markdown use spaces.
- Gothic data is left-handed and the engine is right-handed. Convert once in the
  loader through `Core.CoordinateSystem`; never flip axes, winding or UVs ad hoc
  in gameplay, rendering or physics.
- UI work: build functional, convenient Myra screens first. Do not replicate
  Gothic's UI, fonts or script-created menus unless a task explicitly asks for
  that parity work.
- Prefer deterministic tests: inject the clock and random seed.
- Avoid per-frame allocations in gameplay/render hot paths; measure before
  optimizing.
- Do not add native dependencies without a design note and licensing check.
- Use `// TODO(TaskId): ...` for deferred work, never a bare TODO.

## Working with the developer

- Ask before assuming on game behavior, scope, or destructive operations.
- Report honestly: failing tests, flaky behavior, missing data and unverified
  visuals are all fine to report; claiming success is not.
- Keep replies concise. Use file paths and task IDs. Do not paste large logs;
  summarize and reference files.
- If the developer changes the plan mid-task, update `TASKS.md` first, then
  proceed.

## Subagents

If you are a subagent, never edit `TASKS.md` statuses, never commit, and never
ask the developer for confirmation. Report findings and proposed changes to the
parent agent, which owns the workflow.

## Current state

The repository starts with documentation only. The first implementation task is
**T0.1** (solution scaffold). Until it lands, do not add code outside a task.
