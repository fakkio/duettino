// Turns the icon's SVG source (duettino.svg) into every derived image. How to run it: README.md.
// The generated files are committed: neither the build nor the deploy runs this script.

import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { Resvg } from "@resvg/resvg-js";

const here = (path) => fileURLToPath(new URL(path, import.meta.url));

const icon = readFileSync(here("duettino.svg"), "utf8");

// The sizes Windows asks for between 100% and 250% scaling (title bar, taskbar, Alt+Tab, Explorer's views), and
// Explorer's largest. The full drawing at every size, 16 px included.
const ICO_SIZES = [16, 20, 24, 32, 40, 48, 64, 256];

writeFileSync(here("../src/Duettino/Resources/Duettino.ico"), ico(icon, ICO_SIZES));

/** The SVG drawn at `size` × `size` pixels. */
function render(svg, size) {
  return new Resvg(svg, { fitTo: { mode: "width", value: size } }).render();
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
