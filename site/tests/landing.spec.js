import AxeBuilder from "@axe-core/playwright";
import {expect, test} from "@playwright/test";
import {SITE} from "../src/utils/site.mjs";
import {gotoWithTheme} from "./color-mode.js";

const PAGES = [
  {name: "index", path: "/"},
  {name: "privacy", path: "/privacy"},
  {name: "it index", path: "/it/"},
  {name: "it privacy", path: "/it/privacy"},
];
const ALL_PATHS = PAGES.map((p) => p.path);
const THEMES = /** @type {const} */ (["light", "dark"]);
const WCAG_TAGS = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"];

for (const theme of THEMES) {
  test.describe(theme, () => {
    test.use({colorScheme: theme});

    for (const target of PAGES) {
      test(`${target.name} has no accessibility violations`, async ({page}) => {
        await gotoWithTheme(page, target.path, theme);
        // Open every FAQ answer too: axe skips collapsed content.
        await page.evaluate(() =>
          document.querySelectorAll("details").forEach((d) => (d.open = true)),
        );
        const results = await new AxeBuilder({page})
          .withTags(WCAG_TAGS)
          .analyze();
        expect(results.violations).toEqual([]);
      });
    }

    test("the theme toggle is keyboard-operable", async ({page}) => {
      await gotoWithTheme(page, "/", theme);
      const toggle = page.locator("[data-dark-toggle]");
      const before = await toggle.getAttribute("aria-pressed");

      await toggle.press("Enter");

      await expect(toggle).not.toHaveAttribute("aria-pressed", before);
      await expect
        .poll(() => page.evaluate(() => document.documentElement.dataset.theme))
        .toBe(before === "true" ? "light" : "dark");
    });

    test("a keyboard-focused link shows its focus like hover", async ({
      page,
    }) => {
      await gotoWithTheme(page, "/", theme);
      const link = page.locator("main a:not(.onlyIcon):not(.btn)").first();
      await link.focus();
      await expect
        .poll(() =>
          link.evaluate((el) => getComputedStyle(el, "::after").opacity),
        )
        .toBe("1");
    });

    test("each FAQ question opens from the keyboard", async ({page}) => {
      await gotoWithTheme(page, "/", theme);
      const questions = page.locator("details > summary");
      const count = await questions.count();
      expect(count).toBeGreaterThan(0);
      for (let i = 0; i < count; i++) {
        await questions.nth(i).focus();
        await questions.nth(i).press("Enter");
        await expect(page.locator("details").nth(i)).toHaveJSProperty(
          "open",
          true,
        );
      }
    });
  });
}

test("the theme follows the system on the first visit", async ({browser, baseURL}) => {
  for (const scheme of THEMES) {
    const context = await browser.newContext({colorScheme: scheme, baseURL});
    const page = await context.newPage();
    await page.goto("/");
    await expect(page.locator("html")).toHaveAttribute("data-theme", scheme);
    await context.close();
  }
});

test("the home page shows the README's content", async ({page}) => {
  await page.goto("/");
  await expect(page.locator("html")).toHaveAttribute("lang", "en");
  await expect(
    page.getByRole("heading", {level: 1, name: "Duettino"}),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", {level: 2, name: "Download"}),
  ).toBeVisible();
  await expect(page.getByText("Why not just use Stereo Mix?")).toBeVisible();
});

test("no <picture> is left: the page's theme toggle picks the image", async ({
  page,
}) => {
  await page.goto("/");
  await expect(page.locator("picture, source")).toHaveCount(0);
  await expect(page.locator("img.theme-light")).not.toHaveCount(0);
  await expect(page.locator("img.theme-dark")).not.toHaveCount(0);
});

test("the Download note renders as a note, with no literal marker", async ({
  page,
}) => {
  await page.goto("/");
  const note = page.getByRole("note");
  await expect(note.locator(".note-label")).toHaveText("Note");
  await expect(note).toContainText("Windows protected your PC");
  expect(await page.locator("body").innerText()).not.toContain("[!NOTE]");
});

test("the metadata points to the dark social image and the favicon set", async ({
  page,
  request,
}) => {
  await page.goto("/");
  const ogImage = await page
    .locator('meta[property="og:image"]')
    .getAttribute("content");
  expect(ogImage).toBe(`${SITE}/assets/web/social-dark.png`);
  expect((await request.get(new URL(ogImage).pathname)).ok()).toBe(true);
  await expect(page.locator('meta[name="twitter:card"]')).toHaveAttribute(
    "content",
    "summary_large_image",
  );

  const icons = await page
    .locator('link[rel~="icon"], link[rel="apple-touch-icon"]')
    .evaluateAll((els) => els.map((e) => e.getAttribute("href")));
  expect(icons.length).toBeGreaterThanOrEqual(3);
  for (const href of icons) {
    expect((await request.get(href)).ok(), href).toBe(true);
  }
});

test("every internal link, image and anchor resolves", async ({
  page,
  request,
}) => {
  for (const path of ALL_PATHS) {
    await page.goto(path);
    const refs = await page.evaluate(() => {
      const out = [];
      for (const el of document.querySelectorAll("[href], [src], [srcset]")) {
        if (el.tagName === "LINK" && el.rel === "canonical") continue;
        for (const attr of ["href", "src"]) {
          if (el.hasAttribute(attr)) out.push(el.getAttribute(attr));
        }
        if (el.hasAttribute("srcset")) {
          for (const part of el.getAttribute("srcset").split(",")) {
            out.push(part.trim().split(/\s+/)[0]);
          }
        }
      }
      return out;
    });
    const ids = await page.evaluate(() =>
      [...document.querySelectorAll("[id]")].map((e) => e.id),
    );

    for (const ref of refs) {
      if (/^(https?:|mailto:|data:)/.test(ref)) continue;
      if (ref.startsWith("#")) {
        expect(ids, `${path}: anchor ${ref}`).toContain(ref.slice(1));
        continue;
      }
      expect(ref.startsWith("/"), `${path}: ${ref} is not site-relative`).toBe(
        true,
      );
      expect((await request.get(ref)).ok(), `${path}: ${ref}`).toBe(true);
    }
  }
});

test("the README's repo-relative links point at the repository", async ({
  page,
}) => {
  await page.goto("/");
  await expect(page.getByRole("link", {name: "MIT"})).toHaveAttribute(
    "href",
    "https://github.com/fakkio/duettino/blob/main/LICENSE",
  );
});

test("the FAQ answers the app's privacy itself", async ({page}) => {
  await page.goto("/");
  const answer = page
    .locator("details", {hasText: "Does it upload anything?"})
    .first();
  // Closed answers aren't "visible", so read the text content.
  const text = await answer.textContent();
  expect(text).toContain("never connects to the network");
  expect(text).toContain("%AppData%");
  expect(text).not.toContain("privacy page");
});

test("the privacy page is about the website, not the app", async ({page}) => {
  await page.goto("/privacy");
  await expect(page).toHaveTitle(/Website privacy/);
  await expect(page.locator("main")).toContainText("GitHub Pages");
  await expect(
    page.locator("main a[href*='github-general-privacy-statement']"),
  ).toBeVisible();
  const text = (await page.locator("main").textContent()) ?? "";
  for (const appOnly of ["recordings", "%AppData%", "telemetry", "microphone"]) {
    expect(text.toLowerCase(), appOnly).not.toContain(appOnly.toLowerCase());
  }
});

test("the privacy page makes no claim GitHub doesn't document", async ({
  page,
}) => {
  await page.goto("/privacy");
  const text = (await page.locator("main").textContent()) ?? "";
  for (const claim of ["no access to those logs", "OVH", "retain", "user agent"]) {
    expect(text, claim).not.toContain(claim);
  }
});

test("pages load nothing from other origins", async ({page, baseURL}) => {
  const external = [];
  page.on("request", (req) => {
    if (!req.url().startsWith(baseURL) && !req.url().startsWith("data:"))
      external.push(req.url());
  });
  for (const path of ALL_PATHS) {
    await page.goto(path);
    await page.waitForLoadState("networkidle");
  }
  expect(external).toEqual([]);
});

test("the footer links to fabiolazzaroni.dev", async ({page}) => {
  await page.goto("/");
  await expect(
    page.locator("footer a[href='https://fabiolazzaroni.dev']"),
  ).toBeVisible();
});

test("a phone-width page doesn't scroll horizontally", async ({page}) => {
  await page.setViewportSize({width: 360, height: 800});
  for (const path of ALL_PATHS) {
    await page.goto(path);
    const overflow = await page.evaluate(
      () =>
        document.documentElement.scrollWidth -
        document.documentElement.clientWidth,
    );
    expect(overflow, path).toBeLessThanOrEqual(0);
  }
});

test("the headline's two phrases are wrapped for the bands", async ({page}) => {
  await page.goto("/");
  const headline = page.locator("p.bigline");
  await expect(headline).toHaveText("Records what you say and what you hear.");
  await expect(headline.locator(".say")).toHaveText("what you say");
  await expect(headline.locator(".hear")).toHaveText("what you hear");
});

test("the Download button links to the latest executable", async ({page}) => {
  await page.goto("/");
  const button = page.locator("main a.btn");
  await expect(button).toHaveCount(1);
  await expect(button).toHaveText("Download Duettino.exe");
  await expect(button).toHaveAttribute(
    "href",
    "https://github.com/fakkio/duettino/releases/latest/download/Duettino.exe",
  );
});

test("How it works opens with the HTML diagram, not the image", async ({
  page,
}) => {
  await page.goto("/");
  const section = page.locator("h2", {hasText: "How to record"});
  const diagram = page.locator("figure.flow");
  await expect(diagram).toHaveCount(1);
  await expect(diagram).toHaveAttribute("role", "img");
  await expect(diagram).toHaveAttribute("aria-label", /join into one MP3/);
  await expect(page.locator('img[src*="how-it-works"]')).toHaveCount(0);
  // It follows the section's heading directly.
  expect(
    await section.evaluate((h) => h.nextElementSibling?.matches("figure.flow")),
  ).toBe(true);
});

test("on a phone the diagram's box reads One MP3", async ({page}) => {
  await page.setViewportSize({width: 360, height: 800});
  await page.goto("/");
  const box = page.locator(".flow-result");
  await expect(box).toHaveText("One MP3", {useInnerText: true});
  expect(
    await page.evaluate(
      () =>
        document.documentElement.scrollWidth -
        document.documentElement.clientWidth,
    ),
  ).toBeLessThanOrEqual(0);

  await page.setViewportSize({width: 1000, height: 800});
  await expect(box).toContainText("in Documents\\Duettino");
});

for (const [theme, home] of THEMES.flatMap((t) => [[t, "/"], [t, "/it/"]])) {
  test(`the headline's letters measure 3:1 over each band, ${theme}, ${home}`, async ({
    page,
  }) => {
    await gotoWithTheme(page, home, theme);
    const colors = await page.locator("p.bigline").evaluate((p) => {
      const rgb = (css) => css.match(/rgba?\([^)]*\)/)[0].match(/[\d.]+/g).slice(0, 3).map(Number);
      const read = (el) => ({
        text: rgb(getComputedStyle(el).color),
        band: rgb(getComputedStyle(el).backgroundImage),
      });
      return {say: read(p.querySelector(".say")), hear: read(p.querySelector(".hear"))};
    });
    const luminance = (c) => {
      const [r, g, b] = c.map((v) => {
        const s = v / 255;
        return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
      });
      return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    };
    const contrast = (a, b) => {
      const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
      return (hi + 0.05) / (lo + 0.05);
    };
    for (const phrase of ["say", "hear"]) {
      const {text, band} = colors[phrase];
      expect(contrast(text, band), phrase).toBeGreaterThanOrEqual(3);
    }
  });
}

test("each step is one block beside its number", async ({page}) => {
  await page.goto("/");
  const steps = page.locator("main > ol > li");
  expect(await steps.count()).toBeGreaterThan(0);
  for (const step of await steps.all()) {
    await expect(step.locator("> *")).toHaveCount(1);
    await expect(step.locator("> .step")).toHaveCount(1);
  }
});

test("the icon, the title and the theme toggle share the header's row", async ({
  page,
}) => {
  await page.goto("/");
  const header = page.locator("header");
  const icon = await header.locator("img:visible").boundingBox();
  const title = await header.getByRole("heading", {level: 1, name: "Duettino"}).boundingBox();
  const toggle = await header.locator("[data-dark-toggle]").boundingBox();
  const middle = (b) => b.y + b.height / 2;
  expect(Math.abs(middle(icon) - middle(title))).toBeLessThan(icon.height / 2);
  expect(Math.abs(middle(toggle) - middle(title))).toBeLessThan(icon.height / 2);
  expect(icon.x + icon.width).toBeLessThanOrEqual(title.x + 1);
  const content = await page.locator("main").boundingBox();
  expect(Math.abs(content.x + content.width - (toggle.x + toggle.width))).toBeLessThan(20);
  await expect(page.locator("main h1")).toHaveCount(0);
});

test("the bullets alternate pumpkin and green sea", async ({page}) => {
  await page.goto("/");
  const colors = await page
    .locator("main > ul")
    .first()
    .locator("> li")
    .evaluateAll((lis) => lis.map((li) => getComputedStyle(li, "::marker").color));
  expect(colors.length).toBeGreaterThan(2);
  expect(colors[0]).toBe(colors[2]);
  expect(colors[0]).not.toBe(colors[1]);
});

test("the footer carries the copyright, the credit and the GitHub icon", async ({
  page,
}) => {
  await page.goto("/");
  const footer = page.locator("footer");
  await expect(footer).toContainText(`${new Date().getFullYear() > 2026 ? "2026 - " : "2026"}`);
  await expect(footer).toContainText("Made by Fabio Lazzaroni with ❤ and ☕");
  const github = footer.getByRole("link", {name: /source code on GitHub/});
  await expect(github).toHaveAttribute("href", "https://github.com/fakkio/duettino");
  await expect(footer.getByText("Source code")).toHaveCount(0);
});

test("the GitHub icon stays visible on the footer in both themes", async ({
  page,
}) => {
  for (const theme of THEMES) {
    await gotoWithTheme(page, "/", theme);
    const [fill, ground] = await page.evaluate(() => [
      getComputedStyle(document.querySelector("footer svg path")).fill,
      getComputedStyle(document.querySelector("footer")).backgroundColor,
    ]);
    expect(fill, theme).not.toBe(ground);
  }
});

test("the Download button has no link underline", async ({page}) => {
  await page.goto("/");
  const after = await page
    .locator("main a.btn")
    .evaluate((el) => getComputedStyle(el, "::after").content);
  expect(after).toBe("none");
  await expect(page.locator("main a.btn .front")).toHaveText("Download Duettino.exe");
});

test("no dot separates the Download button from All releases", async ({page}) => {
  await page.goto("/");
  const row = page.locator("main p", {has: page.locator("a.btn")});
  await expect(row.getByRole("link", {name: "All releases"})).toBeVisible();
  expect(await row.innerText()).not.toContain("·");
});

const PAIRS = [
  {lang: "en", path: "/", other: "/it/"},
  {lang: "it", path: "/it/", other: "/"},
  {lang: "en", path: "/privacy", other: "/it/privacy"},
  {lang: "it", path: "/it/privacy", other: "/privacy"},
];

for (const {lang, path, other} of PAIRS) {
  test(`${path} declares ${lang}, its hreflang pair and links to ${other}`, async ({
    page,
  }) => {
    await page.goto(path);
    await expect(page.locator("html")).toHaveAttribute("lang", lang);

    const alternates = await page
      .locator('link[rel="alternate"][hreflang]')
      .evaluateAll((els) =>
        Object.fromEntries(els.map((e) => [e.hreflang, e.getAttribute("href")])),
      );
    const enPath = lang === "en" ? path : other;
    const itPath = lang === "it" ? path : other;
    expect(alternates).toEqual({
      en: `${SITE}${enPath}`,
      it: `${SITE}${itPath}`,
      "x-default": `${SITE}${enPath}`,
    });

    const link = page.locator("header a.lang-switch");
    await expect(link).toHaveAttribute("href", other);
    await expect(link).toHaveAttribute("hreflang", lang === "en" ? "it" : "en");
    await link.focus();
    await expect(link).toBeFocused();
  });

  test(`${path} doesn't redirect by browser language`, async ({browser, baseURL}) => {
    const context = await browser.newContext({
      baseURL,
      locale: lang === "en" ? "it-IT" : "en-US",
    });
    const page = await context.newPage();
    await page.goto(path);
    expect(new URL(page.url()).pathname).toBe(path);
    await context.close();
  });
}

test("the Italian home page is the whole page in Italian, with layout D", async ({
  page,
}) => {
  await page.goto("/it/");
  await expect(page).toHaveTitle(/registra quello che dici/);

  const headline = page.locator("p.bigline");
  await expect(headline).toHaveText("Registra quello che dici e quello che senti.");
  await expect(headline.locator(".say")).toHaveText("quello che dici");
  await expect(headline.locator(".hear")).toHaveText("quello che senti");

  const button = page.locator("main a.btn");
  await expect(button).toHaveCount(1);
  await expect(button).toHaveText("Scarica Duettino.exe");
  await expect(button).toHaveAttribute(
    "href",
    "https://github.com/fakkio/duettino/releases/latest/download/Duettino.exe",
  );

  const diagram = page.locator("figure.flow");
  await expect(diagram).toHaveCount(1);
  await expect(diagram).toHaveAttribute("aria-label", /si uniscono in un solo MP3/);
  await expect(diagram.locator(".say")).toContainText("Ingresso: il tuo microfono");
  await expect(page.locator('img[src*="how-it-works"]')).toHaveCount(0);

  await expect(page.getByRole("note").locator(".note-label")).toHaveText("Nota");
  await expect(page.getByText("Perché non usare il Missaggio stereo?")).toBeVisible();
  await expect(page.locator("main")).toContainText("registrare audio del PC e microfono contemporaneamente");
  await expect(page.locator("main")).not.toContainText("Gaming");
  await expect(page.locator("main")).not.toContainText("[!NOTE]");
});

test("both home pages say the app speaks English and Italian", async ({page}) => {
  await page.goto("/");
  await expect(page.locator("main")).toContainText("English and Italian");
  await page.goto("/it/");
  await expect(page.locator("main")).toContainText("Italiano e inglese");
});

test("on a phone the Italian diagram's box reads Un solo MP3", async ({page}) => {
  await page.setViewportSize({width: 360, height: 800});
  await page.goto("/it/");
  await expect(page.locator(".flow-result")).toHaveText("Un solo MP3", {useInnerText: true});
});

test("the Italian privacy page speaks Italian and the toggle too", async ({page}) => {
  await page.goto("/it/privacy");
  await expect(page).toHaveTitle(/Privacy del sito/);
  await expect(page.locator("main")).toContainText("GitHub Pages");
  await expect(page.locator("[data-dark-toggle]")).toHaveAttribute(
    "aria-label",
    /Passa al tema/,
  );
  await expect(page.locator("footer")).toContainText("Realizzato da Fabio Lazzaroni con ❤ e ☕");
  await expect(page.locator("footer a[href='/it/privacy']")).toBeVisible();
});
