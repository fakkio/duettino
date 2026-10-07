import {SITE} from "../utils/site.mjs";

// Turns the repo's README into something that works on the landing page.
// A Sätteri mdast plugin (Astro 7's Markdown processor). Besides the paths and
// the <picture>, it carries layout D's three transforms: the headline's bands,
// the HTML diagram and the Download button.

const REPO = "https://github.com/fakkio/duettino";

const isRelative = (url) =>
  !/^([a-z][a-z0-9+.-]*:|\/\/|#|\/)/i.test(url);

// Repo-relative paths: assets/web/* are served by the site (see
// scripts/sync-assets.mjs), anything else (docs/adr, LICENSE) lives on GitHub.
const rewriteUrl = (url) => {
  if (url.startsWith(SITE)) return url.slice(SITE.length) || "/";
  if (!isRelative(url)) return url;
  if (url.startsWith("assets/web/")) return `/${url}`;
  return `${REPO}/blob/main/${url}`;
};

// <picture> follows prefers-color-scheme, but the page follows its own toggle,
// so the dark and light sources become two images shown by [data-theme].
const pictureToThemedImages = (html) =>
  html.replace(
    /<picture>\s*<source[^>]*srcset=["']([^"']+)["'][^>]*>\s*(<img[^>]*>)\s*<\/picture>/g,
    (_, darkSrc, img) =>
      `${img.replace("<img", '<img class="theme-light"')}` +
      `${img.replace(/src=["'][^"']*["']/, `src="${darkSrc}"`).replace("<img", '<img class="theme-dark"')}`,
  );

// The diagram: the two Sources drawn like the icon, apart on the left and
// joined into a two-tone ribbon on the right. viewBox 200x112 is stretched
// horizontally only, so the 8 px strokes keep touching at +-4.
const smoothstep = (t) => {
  const c = Math.min(1, Math.max(0, t));
  return c * c * (3 - 2 * c);
};
const strand = (sign) => {
  const points = [];
  for (let x = 0; x <= 200; x += 2) {
    const sep = 4 + 32 * (1 - smoothstep((x - 8) / 120));
    const wig =
      7 * Math.sin((2 * Math.PI * x) / 70) * (1 - smoothstep((x - 150) / 45));
    points.push(`${x},${(56 + sign * sep + wig).toFixed(2)}`);
  }
  return `M${points.join(" L")}`;
};

const escapeHtml = (text) =>
  text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");

// The README's image carries the description; the drawing is hidden from
// screen readers so it isn't read twice.
const diagramHtml = (description) => `<figure class="flow" role="img" aria-label="${escapeHtml(description)}">
<div class="flow-lanes" aria-hidden="true">
<div class="lane say"><b>What you say</b><span>Input: your microphone</span></div>
<div class="lane hear"><b>What you hear</b><span>Output: your headphones</span></div>
</div>
<svg aria-hidden="true" viewBox="0 0 200 112" preserveAspectRatio="none">
<path d="${strand(1)}" fill="none" stroke="var(--c2)" stroke-width="8" stroke-linecap="round" vector-effect="non-scaling-stroke"/>
<path d="${strand(-1)}" fill="none" stroke="var(--c1)" stroke-width="8" stroke-linecap="round" vector-effect="non-scaling-stroke"/>
</svg>
<div class="flow-result" aria-hidden="true"><b>One MP3</b><span>in Documents\\Duettino</span></div>
</figure>`;

const DIAGRAM_PICTURE =
  /<picture>\s*<source[^>]*how-it-works[^>]*>\s*<img[^>]*alt=["']([^"']*)["'][^>]*>\s*<\/picture>/;

const rewriteHtml = (html) =>
  pictureToThemedImages(html.replace(DIAGRAM_PICTURE, (_, alt) => diagramHtml(alt)))
    .replace(/\b(src|srcset)=["']([^"']*)["']/g, (_, attr, url) => `${attr}="${rewriteUrl(url)}"`)
    .replace(/\bhref=["']([^"']*)["']/g, (_, url) => `href="${rewriteUrl(url)}"`);

const DOWNLOAD_URL = `${REPO}/releases/latest/download/Duettino.exe`;
const HEADLINE = /what you say.*what you hear/;

const NOTE_MARKER = /^\[!NOTE\]\s*/;
const NOTE_OPEN =
  '<div class="note" role="note"><p class="note-label">Note</p>';

export const readmeToPage = {
  name: "readme-to-page",
  image(node, ctx) {
    ctx.setProperty(node, "url", rewriteUrl(node.url));
  },
  link(node, ctx) {
    if (node.url === DOWNLOAD_URL) {
      const label = node.children.map((c) => c.value ?? "").join("");
      ctx.replaceNode(node, {
        type: "html",
        value: `<a class="btn" href="${DOWNLOAD_URL}">${escapeHtml(label)}</a>`,
      });
      return;
    }
    ctx.setProperty(node, "url", rewriteUrl(node.url));
  },
  // "**Records what you say and what you hear.**" is the headline: the two
  // phrases get the bands.
  paragraph(node, ctx) {
    const [only] = node.children;
    const text = only?.children?.[0];
    if (node.children.length !== 1 || only.type !== "strong" || text?.type !== "text") return;
    if (!HEADLINE.test(text.value)) return;
    const wrapped = escapeHtml(text.value).replace(
      /what you (say|hear)/g,
      (phrase, which) => `<span class="${which}">${phrase}</span>`,
    );
    ctx.replaceNode(node, {type: "html", value: `<p class="bigline">${wrapped}</p>`});
  },
  html(node, ctx) {
    ctx.replaceNode(node, {type: "html", value: rewriteHtml(node.value)});
  },
  // GitHub's "> [!NOTE]" alert: plain Markdown would print the marker.
  blockquote(node, ctx) {
    const text = node.children[0]?.children?.[0];
    if (text?.type !== "text" || !NOTE_MARKER.test(text.value)) return;
    ctx.setProperty(text, "value", text.value.replace(NOTE_MARKER, ""));
    ctx.replaceNode(node, [
      {type: "html", value: NOTE_OPEN},
      ...node.children,
      {type: "html", value: "</div>"},
    ]);
  },
};
