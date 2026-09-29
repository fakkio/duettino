## Project

Duettino: a lightweight Windows app that records what you say and what you hear into one MP3 (calls are the main use case, not the only one): any chosen input device (usually a mic) mixed with a loopback capture of any chosen output device (headphones, speakers, HDMI…). The two are independent and may be different hardware. C# / .NET 10, WinForms, NAudio; build, test and publish via the `dotnet` CLI.

Read `docs/BRIEF.md` first: it holds the problem, the decisions taken so far, the known technical pitfalls and the open questions. Once `GLOSSARY.md` and `docs/adr/` exist, they take precedence over the brief wherever the two disagree.

## Agent skills

### Issue tracker

Issues live as GitHub Issues in this repo (uses the `gh` CLI). See `docs/agents/issue-tracker.md`.

### Triage labels

Default five canonical roles, label strings unchanged (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context layout (`GLOSSARY.md` + `docs/adr/` at repo root). See `docs/agents/domain.md`.

### Branching (gitflow)

Complex, multi-ticket work branches from `develop` (`feature/<slug>` / `bugfix/<slug>`) and merges back via gitflow; one-off work commits straight to `develop`. See `docs/agents/branching.md`.

### Commits (gitmoji)

Every commit leads with a gitmoji for its type (`<emoji>: <description>`), Conventional Commits structure otherwise. See `docs/agents/commits.md`.

### Releases (gitflow + semver)

Ship from a `release/<version>` (off `develop`) or `hotfix/<version>` (off `main`) branch, version bumped by semver, then merge into both `main` and `develop` and tag. See `docs/agents/releases.md`.

### Changelog (Keep a Changelog)

Every merge or commit onto `develop` adds its entry under `CHANGELOG.md`'s `[Unreleased]` section as it happens; a release renames that section to the version. See `docs/agents/changelog.md`.
