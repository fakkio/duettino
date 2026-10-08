# What GitHub Pages collects from visitors, and what the privacy page should say

Whether the Duettino landing site (`https://duettino.fabiolazzaroni.dev`, GitHub Pages with a custom domain, see [ADR 0008](../adr/0008-landing-on-github-pages-from-site-on-main.md)) must name GitHub as a hosting provider on its privacy page. Read on 2026-10-08 from GitHub Docs and the GitHub General Privacy Statement (effective April 27, 2026); the site source is the primary source for question 5. Where GitHub's documents are silent, this says so.

## Verdict

**Yes, name GitHub as the host and link its privacy statement.** GitHub's own Pages documentation says that a visit to a Pages site gets the visitor's IP address logged and stored, whether or not the visitor is signed in to GitHub. That makes the sentence on the current page ("This website uses no cookies and no analytics") true but incomplete: it says nothing about the host's server-side logging, which the site owner cannot switch off. A short hosting disclosure fixes that without overclaiming. The Duettino site itself loads nothing from third parties, so the disclosure only needs to cover GitHub.

What the primary sources do **not** settle: retention period of the Pages logs, the legal basis for them, whether they are visible to the site owner, and whether GitHub is controller or processor for them. Those are listed under open questions; the suggested wording avoids depending on them.

## 1. Does GitHub collect IP, user agent, access logs? Retention? Legal basis?

**IP address: yes, documented.** [What is GitHub Pages?](https://docs.github.com/en/pages/getting-started-with-github-pages/what-is-github-pages), section "Data collection" (read 2026-10-08):

> "When a GitHub Pages site is visited, the visitor's IP address is logged and stored for security purposes,"
> "regardless of whether the visitor has signed into GitHub or not."

The same section points to the GitHub Privacy Statement for GitHub's security practices.

**User agent, other access-log fields: not documented for Pages.** The Pages docs name only the IP address. The [General Privacy Statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement) (effective April 27, 2026; read 2026-10-08) describes automatic collection for GitHub's services in general, without mentioning Pages:

> "Service Usage Information: We collect data about your interactions with the Services, such as IP address, device information, session details."
> "Website Usage Data: We automatically log data about your Website interactions, including the referring site."

The statement does not mention "user agent" and does not mention GitHub Pages at all. Whether these general categories apply to Pages visitors is an inference, not something GitHub states.

**Retention: not stated for Pages logs.** The statement's only retention sentence is generic and account-centred (section "Security and Retention"):

> "We'll retain your Personal Data as long as your account is active and as needed to fulfill contractual obligations,"

No period is given for visitor IP logs. Cannot be answered from primary sources.

**Legal basis: not stated for Pages visitors.** The statement lists four bases for EEA/UK users (section "Lawful Bases for Processing Personal Data"): Contractual Necessity, Legal Obligation, Legitimate Interests, Consent. It does not say which applies to a non-user visiting a Pages site. The Pages docs give the purpose as "security" ("logged and stored for security purposes"), which would fit legitimate interests, but that mapping is an inference. The statement names the controller as "GitHub, Inc. or GitHub B.V." and says it "stores and processes Personal Data in a variety of locations, including your local region, the United States,".

Also noted: the statement says it "does not apply to services or products that do not display this Statement". The Pages docs link to it, so it is the reference GitHub itself points to.

## 2. Is that data exposed to the site owner?

**Not answered by the primary sources.** Neither the Pages docs read (what-is-github-pages, github-pages-limits, HTTPS page) nor the Terms for Additional Products (Pages section) describe any access-log, visitor-IP or analytics feature for Pages site owners. Absence of a documented feature is not proof of absence; what can be said is that GitHub documents no such feature, and the site owner (this project) has no analytics configured and no log export in the repo. The Pages terms add only: for educational exercises "the site must not collect any user data" (a rule for a specific use case, irrelevant here).

The Pages [limits page](https://docs.github.com/en/pages/getting-started-with-github-pages/github-pages-limits) says nothing about data collection, logs, analytics or cookies.

## 3. Does GitHub Pages set cookies or use analytics on hosted sites?

**Not documented either way for Pages.** The Pages docs do not mention cookies or analytics. The Privacy Statement's cookie text concerns GitHub's own sites: "We use cookies and similar technologies to provide essential functionality like storing settings and recognizing you" and "GitHub will place non-essential cookies on pages where we market products and services to enterprise customers" (marketing pages, not Pages sites).

**Empirical spot check (not a primary source, only a header observation, 2026-10-08):** `curl -I` against `https://pages.github.com/`, `https://octocat.github.io/` and a 404 on `github.github.io` returned no `Set-Cookie` header. This shows no cookie on a plain request to those pages, nothing more, and GitHub could change it without notice. `duettino.fabiolazzaroni.dev` was not reachable yet (the site goes live with release 0.1.0), so the real site could not be checked. Re-check with the browser dev tools once it is live.

## 4. Does the HTTPS / custom-domain setup make the browser contact third parties?

**Certificate: no visitor-side contact documented.** [Securing your GitHub Pages site with HTTPS](https://docs.github.com/en/pages/getting-started-with-github-pages/securing-your-github-pages-site-with-https) (read 2026-10-08):

> "GitHub queues a job to request a TLS certificate from Let's Encrypt"
> "GitHub automatically uploads it to the servers that handle TLS termination for Pages."

The Let's Encrypt request is made by GitHub when the domain is set up; the visitor's browser talks only to the host serving the page. Let's Encrypt is a certificate issuer, not a party the browser contacts at page view. The page does not say what Let's Encrypt receives, but that concerns the site owner's domain, not visitors. Browsers may do OCSP/CT checks depending on the browser; the docs say nothing on this and Let's Encrypt has stopped OCSP in recent years (not verified here from a primary source; not needed for the privacy page).

**Infrastructure, observed not documented:** responses from Pages carry `Server: GitHub.com`, `Via: 1.1 varnish` and `X-Served-By: cache-mxp...`, which looks like a CDN edge in front of Pages (Fastly-style headers). GitHub's Pages docs read here do not name a CDN or sub-processor. So the visitor's request may be handled by GitHub's CDN provider as well; this is a reason to link GitHub's statement rather than describe the chain.

**DNS:** the CNAME is at OVH (ADR 0008). Visitors' resolvers query DNS for the domain as for any site; that is not something the page can or need disclose.

## 5. Does the Duettino site itself load third-party resources?

**No.** Inspected `site/src`, `site/public` and the build output (`npm run build` in `site/` succeeded: 2 pages, `dist/index.html`, `dist/privacy/index.html`, plus `dist/assets/web/*`), 2026-10-08.

Resources loaded at page view, all same-origin:

- `site/src/layouts/Layout.astro:21-37`: `<link rel="canonical">` (self), `<link rel="icon">` x2 and `apple-touch-icon` at `/assets/web/...`; `og:image` / `twitter:image` meta point to `https://duettino.fabiolazzaroni.dev/assets/web/social-dark.png` (our own domain, and only fetched by link-preview crawlers, not by visitors' browsers).
- Layout inline `<script is:inline>` (reads `localStorage` for the theme; no network) and one Astro `<script type="module">` in the built pages, inlined (no `src`).
- `<img>`: `/assets/web/icon-light.svg`, `icon-dark.svg`, `demo.gif`, all local.
- Fonts: `site/src/styles/base.css:7-16` uses a system font stack (`system-ui`, `"Segoe UI"`, ...), no web fonts, no `@import`, no `url(` in the site styles.
- `site/src/styles/reset.css:3` has a URL (`https://www.joshwcomeau.com/css/custom-css-reset/`) but only in a comment.
- `http://www.w3.org/2000/svg` in `DarkToggle.astro:19` and in built SVGs is an XML namespace, never fetched.

Plain outbound hyperlinks (follow only on click, no request at page view):

- `site/src/components/Footer.astro:11` `https://fabiolazzaroni.dev`; `:17` `https://github.com/fakkio/duettino`.
- From the README rendered by `site/src/plugins/readme-to-page.mjs:8` (`REPO = https://github.com/fakkio/duettino`): `.../releases`, `.../releases/latest/download/Duettino.exe`, `.../issues`, `.../blob/main/LICENSE`, `.../blob/main/docs/adr`, `https://dotnet.microsoft.com/download/dotnet/10.0`, `https://github.com/mattpocock/skills`.

No `<iframe>`, embed, tracker, CDN script or remote stylesheet was found in source or in `dist`. Note: the README is rendered into the page, so any image added to the README with an external URL would become a third-party load; worth a test (the existing `site/tests/readme-to-page.spec.js` is the place).

## 6. Sufficient, accurate wording

**Current page** (`site/src/pages/privacy.astro:19-22`):

> "This website uses no cookies and no analytics. It remembers your light or dark theme choice in your browser, and nowhere else."

Accurate about the site's own code (verified in question 5), but silent about the host. It also reads as "no data at all", which GitHub's IP logging contradicts.

**Reputable examples:** none found from primary sources. The only relevant primary text is GitHub's own. A GitHub community discussion ([#40768](https://github.com/orgs/community/discussions/40768), "Privacy Policy on GitHub Pages") asks exactly this question and has no GitHub staff answer; a community member suggests linking to GitHub's privacy statement, and says it is not legal advice. So the wording below is a proposal, not a quote of a recognised template.

**Suggested wording** (English, to replace or follow the cookies paragraph):

> This website uses no cookies and no analytics, and I do not collect or see any data about its visitors. It remembers your light or dark theme choice in your browser, and nowhere else.
>
> The site is hosted on GitHub Pages. GitHub logs the IP address of everyone who visits a Pages site, to keep the service secure, whether or not they have a GitHub account. I have no access to those logs. How GitHub handles this data is described in the [GitHub General Privacy Statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement).
>
> The site loads nothing from other websites: no external fonts, scripts, images or embeds. Links that leave the site (GitHub, my personal website) only send data to those sites if you click them.

Notes on the wording:

- "GitHub logs the IP address ... to keep the service secure ..." is a close paraphrase of the Pages docs ("logged and stored for security purposes, regardless of whether the visitor has signed into GitHub or not").
- "I have no access to those logs" is **not** backed by a GitHub document (question 2). Either keep it as a statement of fact about this project ("I do not receive...") or drop the clause. A safer variant: "I do not collect or receive any visitor data from GitHub." — still unverifiable if GitHub offers an undocumented feature, but true for what the owner actually does.
- Do not state a retention period or a legal basis: GitHub does not state them for Pages.
- The "loads nothing from other websites" sentence is true only as long as question 5 stays true; a test guarding it would keep the page honest.
- An Italian version of the page (planned `/it/`) needs the same paragraph.

## Open questions and uncertainties

- Retention of Pages visitor IP logs, and the legal basis for them: not in any primary source read. Could be asked of GitHub Support; no public answer found.
- Whether GitHub logs more than the IP (user agent, URL, referrer) for Pages: the Pages docs name only the IP; the general statement lists device information and referring site for "Services", without naming Pages.
- Whether site owners have any view of visitor data: no documented feature; not proven absent.
- Controller or processor: for Pages visitors the statement says nothing; a third-party issue thread (sovinityAI/website #13, not read in full, not primary) reports that the GitHub DPA coverage of Free/Pages was unconfirmed. Not needed for a non-commercial static site without an owner-side data flow, but unresolved.
- Cookies on the live domain and CDN: only a header spot check on other Pages sites; the real domain was not yet online. Re-check once the site is live (browser dev tools: Network and Application tabs, plus `curl -I`).
- The CDN in front of Pages is inferred from response headers, not documented in the pages read.
- This is research, not legal advice; whether the wording satisfies GDPR for the owner's situation is not assessed.

## Sources read (all 2026-10-08)

- https://docs.github.com/en/pages/getting-started-with-github-pages/what-is-github-pages (section "Data collection")
- https://docs.github.com/en/pages/getting-started-with-github-pages/github-pages-limits
- https://docs.github.com/en/pages/getting-started-with-github-pages/securing-your-github-pages-site-with-https
- https://docs.github.com/en/site-policy/github-terms/github-terms-for-additional-products-and-features (Pages section)
- https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement (effective April 27, 2026)
- https://github.com/orgs/community/discussions/40768 (community, not authoritative)
- Repo: `site/src/**`, `site/dist/**` (built locally), `docs/adr/0008-landing-on-github-pages-from-site-on-main.md`
