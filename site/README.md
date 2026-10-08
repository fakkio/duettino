# Duettino's landing page

An Astro project (Astro alone, no integrations) that builds `duettino.fabiolazzaroni.dev`. The English page `/` is the repo's root `README.md`, turned into a page by `src/plugins/readme-to-page.mjs`, so the two can't drift apart. The Italian page `/it/` comes the same way from `src/content/README.it.md`, an adaptation of the README (the README itself stays English-only). `/privacy` and `/it/privacy` are plain pages.

```
npm install
npm run dev      # preview locally at http://localhost:4321
npm run build    # build into dist/
npm test         # build, serve the build and run Playwright + axe (WCAG 2.2 AA)
```

`assets/web/` at the repo root is the single source of the page's images and icons: `npm run dev` and `npm run build` copy it into `public/assets/web/` (git-ignored). The first time, run `npx playwright install chromium`.
