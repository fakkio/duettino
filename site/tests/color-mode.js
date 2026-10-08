export const COLOR_MODE_KEY = "color-mode";

// The theme is picked by a blocking script that reads localStorage first and
// `prefers-color-scheme` second, so drive both and let them agree.
export async function gotoWithTheme(page, path, theme) {
  await page.addInitScript(
    ([key, value]) => window.localStorage.setItem(key, value),
    [COLOR_MODE_KEY, theme],
  );
  await page.goto(path, {waitUntil: "networkidle"});
}
