import {SITE} from "../utils/site.mjs";

// Turns the repo's README into something that works on the landing page.
// A Sätteri mdast plugin (Astro 7's Markdown processor); ticket #20 adds the
// layout-D transforms here.

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

const rewriteHtml = (html) =>
  pictureToThemedImages(html)
    .replace(/\b(src|srcset)=["']([^"']*)["']/g, (_, attr, url) => `${attr}="${rewriteUrl(url)}"`)
    .replace(/\bhref=["']([^"']*)["']/g, (_, url) => `href="${rewriteUrl(url)}"`);

const NOTE_MARKER = /^\[!NOTE\]\s*/;
const NOTE_OPEN =
  '<div class="note" role="note"><p class="note-label">Note</p>';

export const readmeToPage = {
  name: "readme-to-page",
  image(node, ctx) {
    ctx.setProperty(node, "url", rewriteUrl(node.url));
  },
  link(node, ctx) {
    ctx.setProperty(node, "url", rewriteUrl(node.url));
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
