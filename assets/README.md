# Assets

`duettino.svg` is Duettino's icon and the single source of every image derived from it: two strands, one per Source, joining into a two-tone ribbon, pumpkin `#D35400` for what you say and green sea `#138D75` for what you hear. The full drawing is used at every size, 16 px included.

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
