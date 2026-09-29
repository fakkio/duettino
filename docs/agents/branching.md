# Branching: gitflow

This repo follows gitflow. `develop` is the integration branch; `main` holds releases.

## Classify the work first

- **Complex**: a feature or fix spanning multiple tickets, typically a spec produced by `to-spec`. Branch it.
- **One-off**: a simple, self-contained change with no ticket breakdown. Commit it straight to `develop`, no branch.

## Complex work: branch, commit, merge

1. Branch from `develop`: `feature/<slug>` for new capability, `bugfix/<slug>` for a fix. Pick the prefix by type, not by how the work arrived.
2. Resolve each ticket under the spec with a commit on that same branch — never split the work across branches.
3. Once every ticket in the spec is closed, merge the branch back into `develop` per gitflow (merge commit, then delete the branch). Do not merge early with tickets still open.
4. As part of that merge, add the branch's entries under `CHANGELOG.md`'s `[Unreleased]` section — see `docs/agents/changelog.md`.

## One-off work

Implement and commit directly on `develop`, adding the change's entry under `CHANGELOG.md`'s `[Unreleased]` section in the same commit (`docs/agents/changelog.md`). No branch, no separate merge step.
