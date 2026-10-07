// A static server for the built site, for Playwright's webServer: unlike
// `astro preview` it stays in the foreground, so Playwright can stop it.
import {createServer} from "node:http";
import {readFile} from "node:fs/promises";
import {dirname, extname, join, normalize} from "node:path";
import {fileURLToPath} from "node:url";

const root = join(dirname(dirname(fileURLToPath(import.meta.url))), "dist");
const port = Number(process.env.PORT ?? 4321);

const TYPES = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css",
  ".js": "text/javascript",
  ".svg": "image/svg+xml",
  ".png": "image/png",
  ".ico": "image/x-icon",
  ".gif": "image/gif",
  ".txt": "text/plain",
};

createServer(async (req, res) => {
  const path = decodeURIComponent(new URL(req.url, "http://x").pathname);
  const candidates = path.endsWith("/")
    ? [join(path, "index.html")]
    : [path, join(path, "index.html")];
  for (const candidate of candidates) {
    const file = normalize(join(root, candidate));
    if (!file.startsWith(root)) break;
    try {
      const body = await readFile(file);
      res.writeHead(200, {
        "content-type": TYPES[extname(file)] ?? "application/octet-stream",
      });
      res.end(body);
      return;
    } catch {
      // try the next candidate
    }
  }
  res.writeHead(404, {"content-type": "text/plain"});
  res.end("Not found");
}).listen(port, () => console.log(`serving dist on http://localhost:${port}`));
