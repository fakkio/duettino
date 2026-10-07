// Turns the icon's SVG source (duettino.svg) into every derived image. How to run it: README.md.
// The generated files are committed: neither the build nor the deploy runs this script.

import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { Resvg } from "@resvg/resvg-js";

const here = (path) => fileURLToPath(new URL(path, import.meta.url));

const icon = readFileSync(here("duettino.svg"), "utf8");

// The icon's two pairs of colors: `say` for the Input's strand, `hear` for the Output's. The source is drawn in
// pumpkin/green sea, the desktop icon's pair (it clears 3:1 on both the light and the dark Windows taskbar) and the
// web's on a light theme; carrot/turquoise is the web's on a dark theme. The social images take them the other way
// round, like the landing's headline bands: pumpkin/green sea under light letters, carrot/turquoise under dark ones.
const PUMPKIN = { say: "#D35400", hear: "#138D75" }; // pumpkin, green sea
const CARROT = { say: "#E67E22", hear: "#1ABC9C" }; // carrot, turquoise

// The sizes Windows asks for between 100% and 250% scaling (title bar, taskbar, Alt+Tab, Explorer's views), and
// Explorer's largest. The full drawing at every size, 16 px included.
const ICO_SIZES = [16, 20, 24, 32, 40, 48, 64, 256];

// The sizes browsers take from a favicon.ico: the tab, and the shortcuts and pinned sites at higher scaling.
const FAVICON_SIZES = [16, 32, 48];

writeFileSync(here("../src/Duettino/Resources/Duettino.ico"), ico(icon, ICO_SIZES));

mkdirSync(here("web"), { recursive: true });
writeFileSync(here("web/icon-light.svg"), recolor(webSvg(icon), PUMPKIN));
writeFileSync(here("web/icon-dark.svg"), recolor(webSvg(icon), CARROT));
writeFileSync(here("web/favicon.svg"), themedFavicon(webSvg(icon)));
writeFileSync(here("web/favicon.ico"), ico(icon, FAVICON_SIZES));
writeFileSync(here("web/apple-touch-icon.png"), touchIcon(icon, 180));
writeFileSync(
  here("web/social-dark.png"),
  // midnight ground, clouds letters, silver facts line
  socialImage(icon, { ground: "#2C3E50", letters: "#ECF0F1", facts: "#BDC3C7", colors: PUMPKIN }),
);
writeFileSync(
  here("web/social-light.png"),
  // clouds ground, midnight letters, asbestos facts line
  socialImage(icon, { ground: "#ECF0F1", letters: "#2C3E50", facts: "#7F8C8D", colors: CARROT }),
);

/** The SVG drawn at `size` × `size` pixels. */
function render(svg, size) {
  return new Resvg(svg, { fitTo: { mode: "width", value: size } }).render();
}

/** The SVG's measures and drawing with its text set in the system's fonts. */
function withSystemFonts(svg) {
  return new Resvg(svg, { font: { loadSystemFonts: true } });
}

/** The icon for the web: the source without its comments, which speak of it as the source. */
function webSvg(svg) {
  return svg.replace(/\s*<!--[\s\S]*?-->/g, "");
}

/** The icon drawn in another pair of colors. */
function recolor(svg, colors) {
  return svg.replaceAll(`"${PUMPKIN.say}"`, `"${colors.say}"`).replaceAll(`"${PUMPKIN.hear}"`, `"${colors.hear}"`);
}

/**
 * The favicon: one SVG that follows the browser's own theme, since the tab strip is browser chrome. Pumpkin/green sea
 * by default, carrot/turquoise under `prefers-color-scheme: dark`.
 */
function themedFavicon(svg) {
  const rules = (c) => `.say{stroke:${c.say}}.hear{stroke:${c.hear}}`;
  const style = `<style>${rules(PUMPKIN)}@media (prefers-color-scheme: dark){${rules(CARROT)}}</style>`;
  return svg
    .replace(`stroke="${PUMPKIN.say}"`, 'class="say"')
    .replace(`stroke="${PUMPKIN.hear}"`, 'class="hear"')
    .replace(/(<svg[^>]*>)/, `$1\n  ${style}`);
}

/**
 * The touch icon (home screens, bookmarks): opaque, since iOS fills transparency with black, with the icon a little
 * inset from the rounded corners the system cuts.
 */
function touchIcon(svg, size) {
  const side = 256; // the source's viewBox
  const inset = 0.08 * side;
  const scale = (side - 2 * inset) / side;
  const tile = `<svg xmlns="http://www.w3.org/2000/svg" width="${side}" height="${side}" viewBox="0 0 ${side} ${side}">
  <rect width="${side}" height="${side}" fill="#FFFFFF"/>
  <g transform="translate(${inset} ${inset}) scale(${scale})">${paths(svg)}</g>
</svg>`;
  return render(tile, size).asPng();
}

/** The icon's drawing: its `<path>` elements. */
function paths(svg) {
  return svg.match(/<path[^>]*\/>/g).join("");
}

/**
 * The 1280 × 640 social image (GitHub's social preview, the landing's og:image): icon and name at the top, the
 * headline with a band of the icon's colors behind the bottom of "what you say" and "what you hear", the facts line,
 * and the icon's two strands along the bottom. Text in the system's Segoe UI, so the script runs on Windows.
 */
function socialImage(icon, { ground, letters, facts, colors }) {
  const font = 'font-family="Segoe UI"';
  const headline = `${font} font-size="66" font-weight="700" letter-spacing="-1.5"`;
  const baselines = [278, 352];
  const bands = [
    band("Records ", "what you say", "", baselines[0], colors.say),
    band("and ", "what you hear", ".", baselines[1], colors.hear),
  ];
  for (const { color } of bands) {
    const ratio = contrast(letters, color);
    console.log(`${ground} social image: letters ${letters} over band ${color}: ${ratio.toFixed(2)}:1`);
    // Large text over a colored band needs 3:1 (WCAG 1.4.3); axe can't see it, so it's checked here.
    if (ratio < 3) throw new Error(`The headline's letters over ${color} measure ${ratio.toFixed(2)}:1, below 3:1.`);
  }
  const strand = (sign, color) =>
    `<path d="${socialStrand(sign)}" fill="none" stroke="${color}" stroke-width="26" stroke-linecap="round"` +
    ` stroke-linejoin="round"/>`;
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="1280" height="640" viewBox="0 0 1280 640">
  <rect width="1280" height="640" fill="${ground}"/>
  <svg x="80" y="72" width="112" height="112" viewBox="0 0 256 256">${recolor(paths(icon), colors)}</svg>
  <text x="220" y="160" ${font} font-size="76" font-weight="600" letter-spacing="-1" fill="${letters}">Duettino</text>
  ${bands.map((b) => b.rect).join("\n  ")}
  <text ${headline} fill="${letters}">
    <tspan x="80" y="${baselines[0]}">Records what you say</tspan>
    <tspan x="80" y="${baselines[1]}">and what you hear.</tspan>
  </text>
  <text x="80" y="429" ${font} font-size="27" fill="${facts}">
    Free and open source · Windows 10/11 · duettino.fabiolazzaroni.dev
  </text>
  ${strand(1, colors.hear)}
  ${strand(-1, colors.say)}
</svg>`;
  return withSystemFonts(svg).render().asPng();

  /**
   * The band behind the bottom of `phrase`, between `before` and `after` on the line: from where the phrase starts to
   * where what follows it starts, like a CSS background on a <span>, 18 px tall from 11 px above the baseline.
   */
  function band(before, phrase, after, baseline, color) {
    const left = 80 + advance(before);
    const right = 80 + advance(before + phrase + after) - advance(after);
    const [x, width] = [left, right - left].map((n) => n.toFixed(1));
    return { color, rect: `<rect x="${x}" y="${baseline - 11}" width="${width}" height="18" fill="${color}"/>` };
  }

  /**
   * How far the headline's `words` move the pen. resvg only measures ink, and its box is loose around curves, so
   * this measures where a "|" lands after the words instead.
   */
  function advance(words) {
    const inkRight = (line) => {
      const probe =
        `<svg xmlns="http://www.w3.org/2000/svg" width="1280" height="200">` +
        `<text x="0" y="100" ${headline} xml:space="preserve">${line}</text></svg>`;
      const box = withSystemFonts(probe).getBBox();
      return box.x + box.width;
    };
    return inkRight(words + "|") - inkRight("|");
  }
}

/** One of the icon's strands stretched along the social image's bottom: apart on the left, a ribbon on the right. */
function socialStrand(sign) {
  const smooth = (t) => {
    const c = Math.min(1, Math.max(0, t));
    return c * c * (3 - 2 * c);
  };
  const points = [];
  for (let x = -20; x <= 1300; x += 4) {
    const separation = 13 + 58 * (1 - smooth((x - 60) / 640));
    const wiggle = 20 * Math.sin((2 * Math.PI * (x + 40)) / 330);
    points.push(`${x},${(552 + sign * separation + wiggle).toFixed(1)}`);
  }
  return "M" + points.join(" L");
}

/** The WCAG contrast ratio between two `#RRGGBB` colors. */
function contrast(a, b) {
  const luminance = (hex) => {
    const [red, green, blue] = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255);
    const linear = (c) => (c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4);
    return 0.2126 * linear(red) + 0.7152 * linear(green) + 0.0722 * linear(blue);
  };
  const [light, dark] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (light + 0.05) / (dark + 0.05);
}

/**
 * A multi-resolution .ico: 32-bit bitmaps below 256 px, which every Windows reader takes, and a PNG at 256 px,
 * the format Windows expects for that size.
 */
function ico(svg, sizes) {
  const frames = sizes.map((size) => (size >= 256 ? render(svg, size).asPng() : bitmapFrame(render(svg, size))));
  const header = Buffer.alloc(6 + 16 * frames.length);
  header.writeUInt16LE(0, 0); // reserved
  header.writeUInt16LE(1, 2); // icon
  header.writeUInt16LE(frames.length, 4);
  let offset = header.length;
  frames.forEach((frame, i) => {
    const entry = 6 + 16 * i;
    header.writeUInt8(sizes[i] % 256, entry); // width, 0 meaning 256
    header.writeUInt8(sizes[i] % 256, entry + 1); // height
    header.writeUInt8(0, entry + 2); // no palette
    header.writeUInt8(0, entry + 3); // reserved
    header.writeUInt16LE(1, entry + 4); // color planes
    header.writeUInt16LE(32, entry + 6); // bits per pixel
    header.writeUInt32LE(frame.length, entry + 8);
    header.writeUInt32LE(offset, entry + 12);
    offset += frame.length;
  });
  return Buffer.concat([header, ...frames]);
}

/**
 * An icon frame as a 32-bit BGRA bitmap: a BITMAPINFOHEADER of twice the height, the pixels bottom-up, then the
 * 1-bit AND mask, set where the pixel is fully transparent.
 */
function bitmapFrame(image) {
  const { width, height } = image;
  const rgba = image.pixels; // premultiplied by alpha
  const maskStride = Math.ceil(width / 32) * 4;
  const header = Buffer.alloc(40);
  header.writeUInt32LE(40, 0);
  header.writeInt32LE(width, 4);
  header.writeInt32LE(height * 2, 8); // the pixels and the mask
  header.writeUInt16LE(1, 12);
  header.writeUInt16LE(32, 14);
  header.writeUInt32LE(0, 16); // uncompressed
  header.writeUInt32LE(width * height * 4 + maskStride * height, 20);

  const pixels = Buffer.alloc(width * height * 4);
  const mask = Buffer.alloc(maskStride * height);
  for (let y = 0; y < height; y++) {
    const row = height - 1 - y; // bottom-up
    for (let x = 0; x < width; x++) {
      const from = (y * width + x) * 4;
      const to = (row * width + x) * 4;
      const alpha = rgba[from + 3];
      const straight = (c) => (alpha === 0 ? 0 : Math.min(255, Math.round((c * 255) / alpha)));
      pixels[to] = straight(rgba[from + 2]);
      pixels[to + 1] = straight(rgba[from + 1]);
      pixels[to + 2] = straight(rgba[from]);
      pixels[to + 3] = alpha;
      if (alpha === 0) mask[row * maskStride + (x >> 3)] |= 0x80 >> (x & 7);
    }
  }
  return Buffer.concat([header, pixels, mask]);
}
