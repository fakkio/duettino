// PROTOTYPE (throwaway): Duettino icon variants for the 0.1.0 launch material.
// Shared by icons.html, landing.html and social.html. Not production code.
//
// Every variant has a full drawing (viewBox 0 0 256 256) and a simplified one for
// small sizes (viewBox 0 0 16 16). Colors are CSS variables, so the palette can be
// swapped live: --c1 = what you say (Input), --c2 = what you hear (Output),
// --c3 = the Mix (only where two shapes overlap; never on an outer edge, so it
// can be dark without vanishing on a dark taskbar).

window.DUETTINO = (() => {
  // Colors from the Flat UI "defo" palette (flatuicolors.com/palette/defo), the same family
  // fabiolazzaroni.dev already uses (midnight blue, wet asphalt, clouds, carrot).
  const PALETTES = {
    carrot: { name: "Carrot & turquoise", c1: "#E67E22", c2: "#1ABC9C", c3: "#2C3E50" },
    pumpkin: { name: "Pumpkin & green sea", c1: "#D35400", c2: "#16A085", c3: "#2C3E50" },
    alizarin: { name: "Alizarin & peter river", c1: "#E74C3C", c2: "#3498DB", c3: "#2C3E50" },
    sunflower: { name: "Sun flower & amethyst", c1: "#F1C40F", c2: "#9B59B6", c3: "#34495E" },
  };

  const f = (n) => Math.round(n * 100) / 100;
  const smooth = (t) => {
    const c = Math.min(1, Math.max(0, t));
    return c * c * (3 - 2 * c);
  };

  // (a) Two strands, one per Source, converging into a two-tone ribbon.
  function strand(sign) {
    const pts = [];
    for (let x = 22; x <= 234; x += 2) {
      const sep = 11 + 54 * (1 - smooth((x - 40) / 110));
      const wig = 17 * Math.sin((2 * Math.PI * (x - 22)) / 92);
      pts.push(`${x},${f(128 + sign * sep + wig)}`);
    }
    return "M" + pts.join(" L");
  }
  const WAVES_FULL = `
    <path d="${strand(1)}" fill="none" stroke="var(--c2)" stroke-width="22" stroke-linecap="round" stroke-linejoin="round"/>
    <path d="${strand(-1)}" fill="none" stroke="var(--c1)" stroke-width="22" stroke-linecap="round" stroke-linejoin="round"/>`;
  const WAVES_SMALL = `
    <path d="M1.6 13 C5 13 5 9 8 9 L14.4 9" fill="none" stroke="var(--c2)" stroke-width="2" stroke-linecap="round"/>
    <path d="M1.6 3 C5 3 5 7 8 7 L14.4 7" fill="none" stroke="var(--c1)" stroke-width="2" stroke-linecap="round"/>`;

  // (b) Two beamed eighth notes: one note per Source, the beam split between them.
  const NOTES_FULL = `
    <g transform="translate(8 0)">
      <ellipse cx="78" cy="192" rx="40" ry="30" transform="rotate(-24 78 192)" fill="var(--c1)"/>
      <rect x="100" y="70" width="18" height="122" fill="var(--c1)"/>
      <polygon points="100,58 161,47 161,87 100,98" fill="var(--c1)"/>
      <ellipse cx="182" cy="172" rx="40" ry="30" transform="rotate(-24 182 172)" fill="var(--c2)"/>
      <rect x="204" y="50" width="18" height="122" fill="var(--c2)"/>
      <polygon points="161,47 222,36 222,76 161,87" fill="var(--c2)"/>
    </g>`;
  const NOTES_SMALL = `
    <ellipse cx="3.9" cy="12.4" rx="2.9" ry="2.2" transform="rotate(-22 3.9 12.4)" fill="var(--c1)"/>
    <rect x="5.3" y="4" width="1.5" height="8.4" fill="var(--c1)"/>
    <polygon points="5.3,3.3 9.8,2.5 9.8,5.3 5.3,6.1" fill="var(--c1)"/>
    <ellipse cx="11.6" cy="11" rx="2.9" ry="2.2" transform="rotate(-22 11.6 11)" fill="var(--c2)"/>
    <rect x="13" y="2.6" width="1.5" height="8.4" fill="var(--c2)"/>
    <polygon points="9.8,2.5 14.5,1.7 14.5,4.5 9.8,5.3" fill="var(--c2)"/>`;

  // (c) Two overlapping circles; the overlap is the Mix.
  const CIRCLES_FULL = `
    <circle cx="96" cy="128" r="80" fill="var(--c1)"/>
    <circle cx="160" cy="128" r="80" fill="var(--c2)"/>
    <path d="M128 54.7 A80 80 0 0 1 128 201.3 A80 80 0 0 1 128 54.7 Z" fill="var(--c3)"/>`;
  const CIRCLES_SMALL = `
    <circle cx="5.5" cy="8" r="5" fill="var(--c1)"/>
    <circle cx="10.5" cy="8" r="5" fill="var(--c2)"/>
    <path d="M8 3.67 A5 5 0 0 1 8 12.33 A5 5 0 0 1 8 3.67 Z" fill="var(--c3)"/>`;

  // (d) A microphone (what you say) inside headphones (what you hear).
  const MIC_FULL = `
    <path d="M38 156 A90 90 0 0 1 218 156" fill="none" stroke="var(--c2)" stroke-width="20" stroke-linecap="round"/>
    <rect x="20" y="136" width="44" height="78" rx="18" fill="var(--c2)"/>
    <rect x="192" y="136" width="44" height="78" rx="18" fill="var(--c2)"/>
    <rect x="104" y="80" width="48" height="86" rx="24" fill="var(--c1)"/>
    <path d="M86 136 v6 a42 42 0 0 0 84 0 v-6" fill="none" stroke="var(--c1)" stroke-width="14" stroke-linecap="round"/>
    <path d="M128 184 V212 M102 214 H154" fill="none" stroke="var(--c1)" stroke-width="14" stroke-linecap="round"/>`;
  const MIC_SMALL = `
    <path d="M2.8 9.6 A5.2 5.2 0 0 1 13.2 9.6" fill="none" stroke="var(--c2)" stroke-width="1.6" stroke-linecap="round"/>
    <rect x="1" y="8.4" width="3.2" height="5.6" rx="1.2" fill="var(--c2)"/>
    <rect x="11.8" y="8.4" width="3.2" height="5.6" rx="1.2" fill="var(--c2)"/>
    <rect x="6.3" y="5" width="3.4" height="6.4" rx="1.7" fill="var(--c1)"/>
    <rect x="7.3" y="11" width="1.4" height="2.4" fill="var(--c1)"/>
    <rect x="5.8" y="12.8" width="4.4" height="1.4" rx="0.7" fill="var(--c1)"/>`;

  // (e) Free variant: the Record dot, split down the middle by a sound wave.
  function seam(cx, top, bottom, amp, periods, step) {
    const pts = [];
    for (let y = top; y <= bottom + 1e-9; y += step) {
      const x = cx + amp * Math.sin((2 * Math.PI * periods * (y - top)) / (bottom - top));
      pts.push(`${f(x)},${f(y)}`);
    }
    return pts.join(" L");
  }
  const DOT_FULL = `
    <circle cx="128" cy="128" r="100" fill="var(--c2)"/>
    <path d="M${seam(128, 28, 228, 24, 1.5, 2)} A100 100 0 0 1 128 28 Z" fill="var(--c1)"/>`;
  const DOT_SMALL = `
    <circle cx="8" cy="8" r="7" fill="var(--c2)"/>
    <path d="M${seam(8, 1, 15, 2, 1, 0.25)} A7 7 0 0 1 8 1 Z" fill="var(--c1)"/>`;

  const VARIANTS = [
    { key: "a", name: "Two waves", full: WAVES_FULL, small: WAVES_SMALL,
      note: "Chosen: the full drawing at every size. Your voice and theirs, two strands that join into one ribbon." },
    { key: "b", name: "Beamed notes ♫", full: NOTES_FULL, small: NOTES_SMALL,
      note: "A duet, literally: one note per Source, the beam split between them. Reads as “music” first." },
    { key: "c", name: "Two circles", full: CIRCLES_FULL, small: CIRCLES_SMALL,
      note: "Two Sources and their Mix in the overlap (third color). Scales down with no changes." },
    { key: "d", name: "Mic + headphones", full: MIC_FULL, small: MIC_SMALL,
      note: "Says exactly what it does. Busiest at 16 px: the stand is the first thing to go." },
    { key: "e", name: "Split Record dot", full: DOT_FULL, small: DOT_SMALL,
      note: "Free variant: the Record button, cut in two by a sound wave. Same shape at every size." },
  ];

  // An <svg> for a variant at a pixel size. Below `smallUpTo` px the simplified drawing is used.
  function svg(key, size, { smallUpTo = 20, forceFull = false, forceSmall = false, label = true } = {}) {
    const v = VARIANTS.find((x) => x.key === key);
    const small = forceSmall || (!forceFull && size <= smallUpTo);
    const vb = small ? "0 0 16 16" : "0 0 256 256";
    const aria = label ? `role="img" aria-label="Duettino icon, variant ${v.key}"` : `aria-hidden="true"`;
    return `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="${vb}" ${aria}>${small ? v.small : v.full}</svg>`;
  }

  function applyPalette(el, key) {
    const p = PALETTES[key] ?? PALETTES.carrot;
    el.style.setProperty("--c1", p.c1);
    el.style.setProperty("--c2", p.c2);
    el.style.setProperty("--c3", p.c3);
  }

  return { PALETTES, VARIANTS, svg, applyPalette };
})();
