// Copied from fabiolazzaroni.dev. Logo ASCII art generated from its logo (background masked out).
const LOGO_ART = [
  "",
  "",
  "                ##########",
  "           ##*==----------==#@",
  "         #+-::::::::::::::::*@@",
  "       %*-::::::::::::::::::*@@",
  "      %*::::::::::::::::::::#@@",
  "      %-::::::::::::::::::::#@@",
  "     %*::::::::::::=*#%%%###%@@",
  "     %=:::::::::::=@@@%%%%%%%@%",
  "     %-:::::::::::#@@",
  "     %-:::::::::::%@@",
  "     %-::::::::::-%@%",
  "     %-::::::::::-%@%",
  "     %-::::::::::-%@%",
  "     %-:::::::::::====*@%",
  "     %-:::::::::::::::+@@",
  "     %-:::::::::::::::+@@",
  "     %-:::::::::::::::+@@",
  "     %::::::::::::====*@@",
  "     #:::::::::::-%@@@@@@",
  "     #:::::::::::-@@%",
  "     #:::::::::::-@@",
  "     #:::::::::::-@@",
  "     #:::::::::::-@@",
  "     #:::::::::::-@@",
  "     #:::::::::::-@@",
  "     #:::::::::::-@@",
  "     #:::::::::::-@@",
  "     #:::::::::::=@@",
  "     #-::::::::::=@@",
  "     %%%%%%%%%%%%%@@",
  "       %%%%%%%%%%%%%",
  "",
];

const ART_TEXT_GAP = "     ";

function lastUpdate() {
  const now = new Date();
  const year = now.getFullYear();
  const month = String(now.getMonth() + 1).padStart(2, "0");
  const day = String(now.getDate()).padStart(2, "0");
  return `${year}/${month}/${day}`;
}

function buildHumansTxt() {
  const text = [
    "",
    "/* TEAM */",
    "Role: Software Engineer",
    "Name: Fabio Lazzaroni",
    "Site: http://fabiolazzaroni.dev/",
    "Contact: lazzaroni@jeflab.it",
    "Twitter: @Fakkio84",
    "Location: 25030 Castelcovati (BS), Italy",
    "",
    "/* SITE */",
    `Last update: ${lastUpdate()}`,
    "Standards: HTML5, CSS3",
    "Components: Astro",
    "Page: Duettino, a Windows app that records what you say and what you hear",
    "",
    "Copyright (C) 2026 Fabio Lazzaroni. Released under the MIT License.",
  ];

  const padTop = Math.floor((LOGO_ART.length - text.length) / 2);
  const artWidth = Math.max(...LOGO_ART.map((line) => line.length));

  const lines = LOGO_ART.map((artLine, i) => {
    const line = text[i - padTop];
    return line ? `${artLine.padEnd(artWidth)}${ART_TEXT_GAP}${line}` : artLine;
  });

  return lines.join("\n") + "\n";
}

export function GET() {
  return new Response(buildHumansTxt(), {
    headers: {"Content-Type": "text/plain; charset=utf-8"},
  });
}
