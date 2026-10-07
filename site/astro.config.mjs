// @ts-check
import {defineConfig} from "astro/config";
import {satteri} from "@astrojs/markdown-satteri";
import {SITE} from "./src/utils/site.mjs";
import {readmeToPage} from "./src/plugins/readme-to-page.mjs";

export default defineConfig({
  site: SITE,
  scopedStyleStrategy: "where",
  build: {inlineStylesheets: "always"},
  markdown: {processor: satteri({mdastPlugins: [readmeToPage]})},
});
