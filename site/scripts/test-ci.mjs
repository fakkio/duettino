// One command: build the site, then serve the build and run Playwright.
import {spawnSync} from "node:child_process";

for (const args of [["run", "build"], ["run", "test:browser"]]) {
  const {status} = spawnSync("npm", args, {stdio: "inherit", shell: true});
  if (status !== 0) process.exit(status ?? 1);
}
