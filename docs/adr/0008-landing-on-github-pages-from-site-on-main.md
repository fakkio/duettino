# Landing page on GitHub Pages, from `site/`, published from `main`, at duettino.fabiolazzaroni.dev

The landing page lives in a `site/` folder at the repo root and a GitHub Action publishes it to GitHub Pages on every push to `main`, at `duettino.fabiolazzaroni.dev` (a CNAME at the domain's DNS provider, OVH). `docs/` is ruled out because it holds the brief, the ADRs and the agent docs, which Pages would publish too; a `gh-pages` branch would keep the page away from the README it mirrors and from the code it describes. Publishing from `main` rather than `develop` keeps the page describing the version people can download, not the one in development. The domain is the hard part to reverse: its links end up in forum answers and launch posts, so it is fixed before any promotion. A personal subdomain was chosen over `fakkio.github.io/duettino` (more personal, ties the project to its author's site) and over a dedicated paid domain.

The page is an Astro project, the same tool as fabiolazzaroni.dev, built from the repo's `README.md` (English) and an Italian counterpart in `site/`, so the English page mirrors the README and cannot drift from it. A Node project inside a .NET repo is the price; it is kept to Astro alone, with no integrations, and its dependencies are updated like the NuGet ones.

## Consequences

- The page goes live with the first merge into `main`, i.e. with release 0.1.0; until then it can only be previewed locally.
- `.dev` is HSTS-preloaded, so the page must be served over HTTPS: GitHub Pages' certificate for the custom domain has to be issued before the link is shared.
- The domain should be verified in the GitHub account settings, so no other repository can claim it if Pages is ever turned off.
