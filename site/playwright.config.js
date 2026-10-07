import {defineConfig, devices} from "@playwright/test";

const PORT = process.env.LOCAL_PORT ?? "4321";

// `npm test` builds first; the server only serves what the build produced.
export default defineConfig({
  testDir: "./tests",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: 0,
  reporter: [["list"]],
  use: {
    ...devices["Desktop Chrome"],
    baseURL: `http://localhost:${PORT}`,
  },
  webServer: {
    command: "node scripts/serve-dist.mjs",
    env: {PORT},
    url: `http://localhost:${PORT}`,
    reuseExistingServer: !process.env.CI,
  },
});
