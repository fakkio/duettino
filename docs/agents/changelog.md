# Changelog: Keep a Changelog + semver

`CHANGELOG.md` at the repo root follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), versioned per [Semantic Versioning](https://semver.org/spec/v2.0.0.html) (see `docs/agents/releases.md` for how the version number is picked).

## Structure

```
# Changelog

## [Unreleased]

### Added
- Add a pause button to the recording window.

## [1.1.0] - 2026-01-15

### Fixed
- Fix silent output when the headset reconnects mid-recording.

[Unreleased]: https://github.com/fakkio/duettino/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/fakkio/duettino/compare/v1.0.0...v1.1.0
```

Newest first. Every version section — including `[Unreleased]` — gets a compare-link footer at the bottom of the file, diffed against the tag directly before it.

## Categories

Standard Keep a Changelog headers, in this order when present: `Added`, `Changed`, `Deprecated`, `Removed`, `Fixed`, `Security`. Omit a category entirely when nothing in the version needs it.

## Write entries as you go

Every commit or merge that lands on `develop` — a feature/bugfix branch merging in, or a one-off commit going straight to it (`docs/agents/branching.md`) — adds its entry under `## [Unreleased]` in that same commit or merge. Never defer changelog writing to the release step.

- One entry per user-visible change, not per raw commit — a feature/bugfix branch with many ticket commits gets one entry per ticket, not one per commit.
- Write for the changelog's reader, not the git log: describe the effect ("Add a pause button to the recording window"), not the mechanics ("refactor capture pipeline"). Rephrase the underlying commit(s); don't paste their messages.
- Skip commits with no user-visible effect (tooling, CI, tests, style, internal refactors, comments) — they earn no changelog line.
- Pick the category from what the change does to the user; the commit's gitmoji (`docs/agents/commits.md`) is a hint, not the rule: ✨→Added, 🐛/🚑️/🩹→Fixed, 🔒️/🔐→Security, 🗑️→Deprecated, 🔥/➖→Removed, anything else user-visible→Changed.
- 💥 breaking changes go in whichever category the change itself belongs to, with the line opening on **Breaking:**.

## At release time

Per `docs/agents/releases.md` step 3: rename `## [Unreleased]` to `## [<version>] - <YYYY-MM-DD>` and open a fresh, empty `## [Unreleased]` above it. The entries underneath are already written — there's nothing left to draft.

Hotfix branches start from `main`, which carries no pending `[Unreleased]` entries: add the fix's entry there directly, then rename it the same way. Merging the hotfix into `develop` can conflict with `develop`'s own `[Unreleased]` section on `CHANGELOG.md` — resolve by keeping `[Unreleased]` on top and the hotfix's new version section directly below it.
