// The README points at assets/web/*; the site serves the same files, copied
// (not duplicated in git) into public/ before every dev or build.
import {cpSync, mkdirSync, rmSync} from "node:fs";
import {dirname, join} from "node:path";
import {fileURLToPath} from "node:url";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const target = join(root, "public", "assets", "web");

rmSync(target, {recursive: true, force: true});
mkdirSync(target, {recursive: true});
cpSync(join(root, "..", "assets", "web"), target, {recursive: true});

// Astro caches the README's rendered Markdown and doesn't notice when the
// remark plugin changes, so a stale page could survive a plugin edit.
for (const cache of [".astro", join("node_modules", ".astro")]) {
  rmSync(join(root, cache), {recursive: true, force: true});
}
