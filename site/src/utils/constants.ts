export const COLOR_MODE_KEY = "color-mode";

export type Lang = "en" | "it";

// Everything the site writes itself, per language (the README carries the rest).
export const TEXT = {
  en: {
    title: "Duettino: records what you say and what you hear",
    description:
      "A small, free Windows app that records your microphone and what plays in your headphones at the same time, into one MP3. Works with Teams, Zoom, WhatsApp and Discord calls.",
    switchLabel: "Italiano",
    switchAria: "Questa pagina in italiano",
    toggleToDark: "Switch to the dark theme",
    toggleToLight: "Switch to the light theme",
    privacy: "Privacy",
    sourceCode: "Duettino's source code on GitHub",
    madeBy: "Made by",
    with: "with",
    and: "and",
    passion: "Passion",
    coffee: "Coffee",
  },
  it: {
    title: "Duettino: registra quello che dici e quello che senti",
    description:
      "Un piccolo programma gratuito per Windows che registra insieme il microfono e l'audio del PC, quello che senti nelle cuffie, in un unico MP3. Funziona con le call di Teams, Zoom, WhatsApp e Discord.",
    switchLabel: "English",
    switchAria: "This page in English",
    toggleToDark: "Passa al tema scuro",
    toggleToLight: "Passa al tema chiaro",
    privacy: "Privacy",
    sourceCode: "Codice sorgente di Duettino su GitHub",
    madeBy: "Realizzato da",
    with: "con",
    and: "e",
    passion: "Passione",
    coffee: "Caffè",
  },
} as const;

// Kept for the English default.
export const SITE_METADATA = TEXT.en;
