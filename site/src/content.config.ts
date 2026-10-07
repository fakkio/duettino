import {defineCollection} from "astro:content";
import {glob} from "astro/loaders";

// The English page is the repo's README, read from outside the site folder.
const readme = defineCollection({
  loader: glob({pattern: "README.md", base: ".."}),
});

export const collections = {readme};
