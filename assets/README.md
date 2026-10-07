# Assets

`duettino.svg` is Duettino's icon and the single source of every image derived from it: two strands, one per Source, joining into a two-tone ribbon, pumpkin `#D35400` for what you say and green sea `#138D75` for what you hear. The full drawing is used at every size, 16 px included. The same script also draws the README's diagram in the icon's strands and colors.

## Regenerate the derived images

After editing `duettino.svg`, run from this folder (Node on Windows; nothing else to install):

```
npm ci
npm run generate
```

Then commit the regenerated files: neither the build nor the deploy runs the script.

| File | Used by |
|---|---|
| `src/Duettino/Resources/Duettino.ico` | The executable in Explorer, and the window's title bar, taskbar button and Alt+Tab. Frames at 16, 20, 24, 32, 40, 48, 64 and 256 px. |
| `web/icon-light.svg`, `web/icon-dark.svg` | The README's `<picture>` and the landing page: pumpkin/green sea on a light theme, carrot `#E67E22`/turquoise `#1ABC9C` on a dark one. |
| `web/favicon.svg` | The landing's favicon: one SVG that switches to carrot/turquoise under `prefers-color-scheme: dark`, following the browser's own theme. |
| `web/favicon.ico` | The favicon for browsers without SVG favicons, in pumpkin/green sea. Frames at 16, 32 and 48 px. |
| `web/apple-touch-icon.png` | Home screens and bookmarks: 180 px, pumpkin/green sea on white, since iOS fills transparency with black. |
| `web/social-dark.png` | GitHub's social preview and the landing's `og:image`: 1280 × 640, midnight `#2C3E50` ground, pumpkin/green sea bands. |
| `web/social-light.png` | The light social image: clouds `#ECF0F1` ground, carrot/turquoise bands. Unused for now. |
| `web/how-it-works-light.svg`, `web/how-it-works-dark.svg` | The README's "How it works" diagram, through a `<picture>`: the two strands, apart beside "What you say" and "What you hear", joining into one MP3. Transparent ground, the icon's light or dark pair and text colors for GitHub's light or dark theme. |

`web/demo-placeholder.svg` is not generated: drawn by hand, it stands in for the README's GIF until the GIF is recorded.

The social images set their text in the system's Segoe UI, so the script runs on Windows. It prints the contrast of the headline's letters against each band and stops if one falls below 3:1, a check axe can't make on text over a colored band.
