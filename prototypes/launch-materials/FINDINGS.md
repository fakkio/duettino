# Launch materials prototype: findings

Throwaway prototype for the 0.1.0 launch material (icon, landing page, social image). It answered "what should these look like?" by putting variants side by side. Open any page by double-clicking it; no server needed.

- `icons.html`: five icon variants at 16–256 px, on light and dark, in a simulated Windows 11 taskbar and title bar. `icons.js` holds the drawings and palettes, shared by all pages.
- `landing.html`: four layouts of the English copy, switchable with `?variant=A|B|C|D`, the floating bar, or ← / →. The skeleton is copied from fabiolazzaroni.dev.
- `social.html`: 1280×640 social image drafts, with previews as a shared link. `?export=dark|light&palette=…&below=1` renders the bare card; the `social-draft-*.png` files were exported that way.

## Verdict (2026-10-07)

### Icon

- **Variant (a), two waves**: two strands, one per Source, joining into a two-tone ribbon. The full drawing at **every** size, including 16 px; no simplified drawing.
- **One version, used by the `.ico` and the social image:** pumpkin `#D35400` (what you say), green sea `#16A085` (what you hear). It is the only pair near 3:1 on both taskbars (lowest 2.96, against carrot/turquoise's 2.17 on the light taskbar).
- **On the web it follows the theme:** the favicon is one SVG with a `prefers-color-scheme: dark` rule, and the README uses `<picture>`. Pumpkin/green sea on light, carrot `#E67E22`/turquoise `#1ABC9C` on dark.

### Landing: layout D

- **Structure:** layout A, the README top to bottom.
- **Header:** icon and name, then B's big "Records what you say and what you hear." with a colored band behind the bottom of "what you say" and "what you hear".
- **How it works:** opens with C's diagram, redrawn as icon (a): "What you say / Input: your microphone" and "What you hear / Output: your headphones" join into a ribbon that ends in a "One MP3 in Documents\Duettino" box. On phones the box shows "One MP3" only.
- **Accents follow the theme, like fabiolazzaroni.dev:** pumpkin/green sea on light, carrot/turquoise on dark. That covers the Download button, the diagram and the list markers.
- **Exceptions:**
  - the band behind the headline takes the **opposite** pair: carrot/turquoise under dark text, pumpkin/green sea under light text;
  - the step-number circles stay carrot/turquoise in both themes, with dark numbers (5.80 / 6.86).
- **The README can still generate the page:** D is linear. Three spots need the remark plugin:
  - wrapping "what you say" and "what you hear" in the headline;
  - swapping the diagram image for its HTML version;
  - styling the first Download link as a button.

  The rest is plain Markdown plus CSS, including `<details>` for the FAQ.

### Social image (1280×640)

- **Dark:** midnight `#2C3E50` ground, pumpkin/green sea band behind the letters (`social-draft-dark-pumpkin.png`).
- **Light:** clouds `#ECF0F1` ground, carrot/turquoise band behind the letters (`social-draft-light.png`).
- **Layout:** icon and name at the top, the headline with the colored bands, the line "Free and open source · Windows 10/11 · duettino.fabiolazzaroni.dev", and the icon's two strands along the bottom.

## Contrast (WCAG)

3:1 is the bar for large text and graphics; 4.5:1 for body text.

- **Landing text** passes in both themes with the per-theme palette.
- **Bands behind the letters** are a deliberate choice over bands under them:
  - letters over band: 5.5 / 6.6 on light, 3.63 / 2.86 on dark;
  - band against ground: 2.85 / 2.41 on light, 2.63 / 3.35 on dark. These are decorative, since the text reads without them.
- The rejected "under the letters" drafts are kept in `social.html` for the record.

## Not decided here

- GitHub's social preview and `og:image` each take a single image. Which variant goes where is left to the spec.
- The landing copy is a draft for `/writing-shape`. Its claims to verify:
  - Bluetooth hands-free quality ("often 8 kHz");
  - "never connects to the network";
  - Audacity recording one device at a time on Windows.
