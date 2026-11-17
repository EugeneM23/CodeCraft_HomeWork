---
name: perf-check
description: Run the project's performance and order check (agent perf-check) on a branch or working copy - static rules from Docs/Rules plus a VoxelSandbox benchmark in the Unity editor - and get a report in Docs/Reports. Use when the user types /perf-check or asks to check performance, FPS, budgets, naming or project tidiness before a merge.
argument-hint: "[branch | path] [static | measure | all]"
---

# /perf-check

Checks a branch against `Docs/Rules/Performance.md` and `Docs/Rules/Project.md` with the `perf-check` agent (`.claude/agents/perf-check.md`). Talk to the user in Russian.

## 1. What to check

Arguments: `$ARGUMENTS`.

- The first argument is a branch (`game/ui`) or a path to a working copy. Without it, check the current working copy.
- The second argument is the mode:
  - `static` — static rules only, about 1 min;
  - `measure` — benchmark only;
  - `all` — both, the default.
- **Branch → working copy.** Find it in `git worktree list --porcelain`.
  - If the branch has no working copy and the mode is `static`, make a temporary one: `git worktree add --detach "<scratchpad>/perfcheck-<branch>" <branch>`. Remove it afterwards with `git worktree remove`.
  - A benchmark needs an editor open on that working copy. If there is none, tell the user and run `static` only.

## 2. Editor and Play (only for `measure` and `all`)

1. Run `unity status --json` and take the editor whose `projectPath` equals the working copy. If there is none, the benchmark is skipped.
2. Check its state in one call:
   ```
   unity cmd --port <N> script eval --code "return UnityEditor.EditorApplication.isPlaying + \" \" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path + \" dirty=\" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty;"
   ```
3. Ask the user one question (AskUserQuestion), depending on the state:
   - **In Play:** «Редактор в Play. Снять замер прямо в нём? ~45 с, танк поедет сам — не трогай клавиатуру». Answers: yes → `play: user-in-play`; no → `measure: no`.
   - **Not in Play, VoxelSandbox open and not dirty:** «Можно войти в Play на ~1 мин для замера?». Yes → `play: free`.
   - **Another scene, or the scene is dirty:** do not ask about switching. The benchmark is skipped with the reason.
4. With the user's answer, the agent gets `measure: yes, port <N>`.

## 3. Run the agent

Start the `perf-check` agent (Agent tool, `subagent_type: perf-check`) in the foreground. Give it:
- `repo: <absolute path>`;
- `base: <ref>`, if the user named one;
- `measure: …`;
- `play: …`;
- the mode.

## 4. Answer

When the agent is done:
- Read its report in `Docs/Reports/`.
- Tell the user, in Russian:
  - the verdict (can merge or not);
  - what blocks;
  - warnings;
  - the link to the report.
- Do not commit the report yourself. Offer to commit it with the branch, or that the team lead keeps it in `game/develop`.
- If a temporary working copy was made, remove it.
