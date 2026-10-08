import {defineCollection} from "astro:content";
import {glob} from "astro/loaders";

// The English page is the repo's README, read from outside the site folder.
const readme = defineCollection({
  loader: glob({pattern: "README.md", base: ".."}),
});

// The Italian page is its counterpart, kept beside the site's source: the
// README itself stays English-only.
const readmeIt = defineCollection({
  loader: glob({pattern: "README.it.md", base: "./src/content"}),
});

export const collections = {readme, readmeIt};
