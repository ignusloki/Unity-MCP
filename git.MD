# Git Workflow Guide

This repository is intentionally used in two modes:

- `main` is the clean upstream-sync branch for contributing back to `IvanMurzak/Unity-MCP`.
- `custom/main` is the private integration branch that contains upstream plus local custom changes.

Future agents should treat this setup as intentional and avoid collapsing the two flows together.

## Remotes

The remotes are configured like this:

- `upstream` -> `git@github.com:IvanMurzak/Unity-MCP.git`
- `origin` -> `git@github.com:ignusloki/Unity-MCP.git`

Rules:

- Pull upstream updates from `upstream`.
- Push private branches to `origin`.
- Do not repoint these remotes unless the user explicitly asks.

## Branch Roles

### `main`

Purpose:

- Keep a clean branch that matches `upstream/main`.
- Use it as the base for upstream contribution branches and PRs.

Rules:

- Keep `main` fast-forwardable to `upstream/main`.
- Do not merge `custom/main` into `main`.
- Do not land private-only features directly on `main`.

Typical update flow:

```bash
git switch main
git fetch upstream
git merge --ff-only upstream/main
git push origin main
```

### `custom/main`

Purpose:

- Keep the user's private integrated version of the project.
- Accumulate custom features that should not automatically go to upstream.

Rules:

- Merge upstream changes into `custom/main`.
- Push `custom/main` to `origin/custom/main`.
- Use `custom/*` branches for feature work when a separate branch is useful.

Typical update flow:

```bash
git switch custom/main
git fetch upstream
git merge upstream/main
git push origin custom/main
```

### Short-lived Feature Branches

Use branch prefixes to keep intent clear:

- `feat/*` or `fix/*` for upstream-targeted work based on `main`
- `custom/*` for private work based on `custom/main`
- `codex/*` for temporary agent work branches when needed

Rules:

- Upstream PR branches should start from `main`.
- Private feature branches should start from `custom/main`.
- Temporary agent branches may be merged into `custom/main` or discarded after use.

## Single-Folder Workflow

The current preferred setup is a single working directory:

- `/Users/suporte/Unity-MCP`

This one repo contains both long-lived branches:

- `main`
- `custom/main`

The user switches branches in the same folder depending on the task.

Why:

- It keeps the filesystem layout simple.
- It avoids maintaining parallel working copies.
- It still preserves the clean split between upstream-safe work and private work.

Typical usage:

```bash
git switch main
git fetch upstream
git merge --ff-only upstream/main
```

```bash
git switch custom/main
git fetch upstream
git merge upstream/main
git push origin custom/main
```

Important rule:

- Before starting private feature work, make sure the repo is on `custom/main` or a branch created from it.
- Before starting upstream contribution work, make sure the repo is on `main` or a branch created from it.

## Optional Worktrees

Worktrees are optional convenience only. They are not required for this repo's intended workflow.

If the user later asks for separate clean/custom folders again, `git worktree` is acceptable, but do not assume it should exist by default.

## Safe Practices

- Never assume `main` should contain all current local work. Verify whether the work belongs on `main` or `custom/main`.
- Before merging anything into `main`, inspect `git log upstream/main..HEAD` and confirm that every commit is suitable for upstream.
- Before pushing, verify the target remote and branch.
- If a branch contains mixed upstream-safe and private changes, stop and separate them before pushing to `main`.

## Recovering the Intended Structure

If `main` accidentally picks up private changes:

1. Preserve the current integrated state on `custom/main` or another custom branch.
2. Move `main` back to `upstream/main`.
3. Continue private work from `custom/main`.

Example:

```bash
git branch custom/main
git push -u origin custom/main
git switch <some-other-branch>
git branch -f main upstream/main
```

## Notes For Future Agents

- Read this file before changing remotes, worktrees, or branch topology.
- Prefer minimal, explicit branch operations.
- Do not create or delete worktrees unless the user asks.
- If the user says "sync with IvanMurzak/Unity-MCP", that means `upstream/main`, not `origin/custom/main`.
- If the user says "my version" or "custom version", that means `custom/main`.
