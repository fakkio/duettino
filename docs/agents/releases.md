# Releases: gitflow + semver

Releases ship via gitflow's `release`/`hotfix` branches (see `docs/agents/branching.md` for gitflow basics), versioned per [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Pick the branch

- **Release** (🔖): shipping what's accumulated on `develop`. Branch `release/<version>` from `develop`.
- **Hotfix** (🚑️): a critical fix needed on production now. Branch `hotfix/<version>` from `main`.

The emoji in parentheses is that branch kind's marker — use it for both the version-bump commit and the tag below.

## Pick the version

Semver: `MAJOR.MINOR.PATCH`. Bump by the highest-impact change type among the commits since the last release, reading the gitmoji catalog in `docs/agents/commits.md`:

- 💥 present anywhere in the range → bump MAJOR.
- No 💥, but ✨ present → bump MINOR.
- Neither (🐛 and the rest) → bump PATCH.

Before the first `1.0.0`, the project is under initial development (semver §4): version however fits, since anything may still change.

## On the release/hotfix branch

Always these three steps, in this order, before merging anywhere:

1. Bump `<Version>` in `Directory.Build.props` at the repo root — the single source of truth for every project in the solution; never set a version in an individual `.csproj`. If an MSIX manifest (`Package.appxmanifest`) exists, set its `Identity Version` to the same number as `MAJOR.MINOR.PATCH.0` (four parts; the Microsoft Store requires the fourth to be `0`). Commit with the branch kind's marker and label: `🔖: Release v<version>` or `🚑️: Hotfix v<version>`.
2. Update NuGet dependencies: list them with `dotnet list package --outdated`, bump each with `dotnet add package <id>` (or in `Directory.Packages.props` if central package management is enabled). If `packages.lock.json` files exist, regenerate them with `dotnet restore --force-evaluate` — never hand-edit a lock file. Then `dotnet build -c Release` and `dotnet test` must both pass. Do the same for the landing page's npm dependencies: in `site/`, `npm outdated`, `npm update` (or `npm install <pkg>@latest` for a major), then `npm test` must pass.
3. Promote `CHANGELOG.md`'s `[Unreleased]` section to this version (see `docs/agents/changelog.md`) — its entries are already there from each merge into `develop`/`main`, so this step renames the section rather than drafting it.

## Merge and tag

Merge the branch into both `main` and `develop`, per gitflow. On `main`, tag the merge commit `v<version>`, annotated with the same marker and label as the bump commit: `🔖: Release v<version>` or `🚑️: Hotfix v<version>`.

## Ship the binaries

From the tagged commit, build the release executable and attach it to a GitHub Release for `v<version>`:

```
dotnet publish src/Duettino -p:PublishProfile=win-x64
```

The profile (`src/Duettino/Properties/PublishProfiles/win-x64.pubxml`) makes one self-contained, compressed, untrimmed win-x64 executable, about 55 MB, that runs without .NET installed (ADR-0007). It lands alone in `src/Duettino/bin/publish/win-x64/Duettino.exe`: symbols are embedded, so no `.pdb` ships beside it. Check that its file properties (Details tab) show the new version before uploading.

The same executable goes to winget and Scoop. The Microsoft Store (MSIX) comes with the first stable release, not v1 — see `docs/BRIEF.md` §12; once it does, record how the MSIX is built here.

## The GitHub Release

- Title it with "beta" while the version is 0.x, e.g. "Duettino 0.1.0 beta".
- Put the executable's SHA-256 in the release notes (`Get-FileHash Duettino.exe`).
- Name the asset `Duettino.exe`, always: the README's Download button points at `/releases/latest/download/Duettino.exe`.
- **Never** mark a release as pre-release: `/releases/latest/download/` ignores pre-releases, so the Download button would break.

## The landing page

Merging into `main` runs `.github/workflows/site.yml`, which builds, tests and deploys the landing page to GitHub Pages (ADR-0008). After the merge, check that the run succeeded and that `https://duettino.fabiolazzaroni.dev`, `/it/`, `/privacy` and `/it/privacy` load over HTTPS.
