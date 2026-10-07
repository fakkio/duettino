import AxeBuilder from "@axe-core/playwright";
import {expect, test} from "@playwright/test";
import {gotoWithTheme} from "./color-mode.js";

const PAGES = [
  {name: "index", path: "/"},
  {name: "privacy", path: "/privacy"},
];
const THEMES = /** @type {const} */ (["light", "dark"]);
const WCAG_TAGS = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"];

for (const theme of THEMES) {
  test.describe(theme, () => {
    test.use({colorScheme: theme});

    for (const target of PAGES) {
      test(`${target.name} has no accessibility violations`, async ({page}) => {
        await gotoWithTheme(page, target.path, theme);
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
      const link = page.locator("main a:not(.onlyIcon)").first();
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

test("the theme follows the system on the first visit", async ({browser}) => {
  for (const scheme of THEMES) {
    const context = await browser.newContext({colorScheme: scheme});
    const page = await context.newPage();
    await page.goto("http://localhost:" + (process.env.LOCAL_PORT ?? "4321"));
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

test("the Download note renders as a note, with no literal marker", async ({
  page,
}) => {
  await page.goto("/");
  const note = page.getByRole("note");
  await expect(note).toContainText("Note");
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
  expect(ogImage).toBe(
    "https://duettino.fabiolazzaroni.dev/assets/web/social-dark.png",
  );
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
  for (const path of ["/", "/privacy"]) {
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
  // Inside a closed FAQ answer, so not "visible": look it up by its target.
  await expect(page.locator('a[href="/privacy"]', {hasText: "privacy page"})).toHaveCount(1);
});

test("a phone-width page doesn't scroll horizontally", async ({page}) => {
  await page.setViewportSize({width: 360, height: 800});
  for (const path of ["/", "/privacy"]) {
    await page.goto(path);
    const overflow = await page.evaluate(
      () =>
        document.documentElement.scrollWidth -
        document.documentElement.clientWidth,
    );
    expect(overflow, path).toBeLessThanOrEqual(0);
  }
});
