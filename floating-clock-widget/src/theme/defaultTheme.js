const fs = require("node:fs/promises");
const path = require("node:path");

const DEFAULT_THEME_ID = "pop-neon-default";

const DEFAULT_THEME_MANIFEST = {
  id: DEFAULT_THEME_ID,
  name: "Pop Neon Default",
  version: "1.0.0",
  requiredAppVersion: "0.1.0",
  author: "Floating Clock Team",
  preview: "preview.webp",
  entryCss: "panel.css",
  entryTokens: "tokens.json",
};

const DEFAULT_THEME_TOKENS = {
  colors: {
    panel: "#0B0B12DD",
    textMain: "#F7FDFF",
    textSub: "#9CB6C9",
    accentA: "#00E5FF",
    accentB: "#FF3DFF",
    accentC: "#C8FF3D",
  },
  typography: {
    clockFont: "'Orbitron', 'Segoe UI', sans-serif",
    uiFont: "'Rajdhani', 'Segoe UI', sans-serif",
  },
  radius: {
    panel: 22,
  },
  shadow: {
    panelGlow: "0 0 18px rgba(0, 229, 255, 0.2), 0 0 34px rgba(255, 61, 255, 0.14)",
  },
  blur: {
    panelBackdrop: 10,
  },
  motion: {
    driftPx: 4,
    driftCycleSec: 14,
    locatorPulseSec: 8,
  },
};

const DEFAULT_THEME_CSS = `
.clock-panel::after {
  content: "";
  position: absolute;
  inset: 1px;
  border-radius: calc(var(--panel-radius) - 1px);
  border: 1px solid color-mix(in srgb, var(--accent-a) 44%, transparent);
  pointer-events: none;
}
`;

async function ensureDefaultTheme(themesDir, appVersion) {
  const themeDir = path.join(themesDir, DEFAULT_THEME_ID);
  await fs.mkdir(themeDir, { recursive: true });

  const manifestPath = path.join(themeDir, "manifest.json");
  const tokensPath = path.join(themeDir, "tokens.json");
  const cssPath = path.join(themeDir, "panel.css");
  const previewPath = path.join(themeDir, "preview.webp");

  const manifest = {
    ...DEFAULT_THEME_MANIFEST,
    requiredAppVersion: appVersion,
  };

  await writeFileIfMissing(
    manifestPath,
    JSON.stringify(manifest, null, 2) + "\n",
    "utf8",
  );
  await writeFileIfMissing(
    tokensPath,
    JSON.stringify(DEFAULT_THEME_TOKENS, null, 2) + "\n",
    "utf8",
  );
  await writeFileIfMissing(cssPath, DEFAULT_THEME_CSS.trim() + "\n", "utf8");
  await writeFileIfMissing(
    previewPath,
    "placeholder-preview",
    "utf8",
  );
}

async function writeFileIfMissing(filePath, content, encoding) {
  try {
    await fs.access(filePath);
  } catch {
    await fs.writeFile(filePath, content, { encoding });
  }
}

module.exports = {
  DEFAULT_THEME_ID,
  DEFAULT_THEME_TOKENS,
  ensureDefaultTheme,
};
