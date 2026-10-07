// @ts-check
import {defineConfig} from "astro/config";
import {satteri} from "@astrojs/markdown-satteri";
import {readmeToPage} from "./src/plugins/readme-to-page.mjs";

export default defineConfig({
  site: "https://duettino.fabiolazzaroni.dev",
  scopedStyleStrategy: "where",
  build: {inlineStylesheets: "always"},
  markdown: {processor: satteri({mdastPlugins: [readmeToPage]})},
});
