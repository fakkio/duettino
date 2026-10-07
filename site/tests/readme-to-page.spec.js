import {markdownToHtml} from "satteri";
import {expect, test} from "@playwright/test";
import {readmeToPage} from "../src/plugins/readme-to-page.mjs";

const render = (markdown) =>
  markdownToHtml(markdown, {mdastPlugins: [readmeToPage]}).html;

test("a <picture> becomes two themed images, whatever the quotes", () => {
  const html = render(`<picture>
  <source media="(prefers-color-scheme: dark)" srcset='assets/web/a-dark.svg'>
  <img src='assets/web/a-light.svg' alt="A" width="96">
</picture>`);

  expect(html).not.toContain("<picture");
  expect(html).toContain('class="theme-light" src="/assets/web/a-light.svg"');
  expect(html).toContain('class="theme-dark" src="/assets/web/a-dark.svg"');
});

test("repo-relative links and images are rewritten, others are left alone", () => {
  const html = render(
    "[adr](docs/adr) ![x](assets/web/x.svg) [out](https://example.com) [top](#faq) [site](https://duettino.fabiolazzaroni.dev/privacy)",
  );

  expect(html).toContain('href="https://github.com/fakkio/duettino/blob/main/docs/adr"');
  expect(html).toContain('src="/assets/web/x.svg"');
  expect(html).toContain('href="https://example.com"');
  expect(html).toContain('href="#faq"');
  expect(html).toContain('href="/privacy"');
});

test("a [!NOTE] blockquote becomes a labelled note", () => {
  const html = render("> [!NOTE]\n> Careful **here**.");

  expect(html).toContain('role="note"');
  expect(html).toContain('<p class="note-label">Note</p>');
  expect(html).not.toContain("[!NOTE]");
});

// A known limit: the marker is only recognised as the very first text of the
// blockquote, which is how GitHub's own alerts are written.
test("a blockquote that doesn't start with [!NOTE] stays a blockquote", () => {
  const html = render("> **Careful.** [!NOTE] is not the first word.");

  expect(html).toContain("<blockquote>");
  expect(html).not.toContain('role="note"');
});

test("the headline's two phrases are wrapped, only in the bold line", () => {
  const html = render("**Records what you say and what you hear.**\n\nWhat you say & what you hear, plain.");

  expect(html).toContain(
    '<p class="bigline">Records <span class="say">what you say</span> and <span class="hear">what you hear</span>.</p>',
  );
  expect(html).not.toContain('class="say">What');
});

test("only the latest-executable link becomes the button", () => {
  const html = render(
    "**[Download Duettino.exe](https://github.com/fakkio/duettino/releases/latest/download/Duettino.exe)** · [All releases](https://github.com/fakkio/duettino/releases)",
  );

  expect(html).toContain(
    '<a class="btn" href="https://github.com/fakkio/duettino/releases/latest/download/Duettino.exe"><span class="shadow"></span><span class="edge"></span><span class="front">Download Duettino.exe</span></a>',
  );
  expect(html).toContain('<a href="https://github.com/fakkio/duettino/releases">All releases</a>');
});

test("the diagram's picture becomes the HTML diagram, keeping its description", () => {
  const html = render(`<picture>
  <source media="(prefers-color-scheme: dark)" srcset="assets/web/how-it-works-dark.svg">
  <img src="assets/web/how-it-works-light.svg" alt="Two join into one.">
</picture>`);

  expect(html).toContain('<figure class="flow" role="img" aria-label="Two join into one.">');
  expect(html).toContain("in Documents\\Duettino");
  expect(html).not.toContain("how-it-works");
});

test("an ordered item's content is wrapped in one step block", () => {
  const html = render("1. **Press Stop.** Then `a` and `b`.\n\n- plain\n");

  expect(html).toMatch(/<li>\s*<div class="step">\s*<strong>Press Stop\.<\/strong> Then <code>a<\/code> and <code>b<\/code>\.\s*<\/div>\s*<\/li>/);
  expect(html).toContain("<li>plain</li>");
});

test("the README's icon and title are left to the header", () => {
  const html = render(`<picture>
  <source media="(prefers-color-scheme: dark)" srcset="assets/web/icon-dark.svg">
  <img src="assets/web/icon-light.svg" alt="">
</picture>

# Duettino

## Download`);

  expect(html).not.toContain("icon-");
  expect(html).not.toContain("<h1");
  expect(html).toContain("<h2>Download</h2>");
});
