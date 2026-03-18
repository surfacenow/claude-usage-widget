const fs = require("node:fs/promises");
const path = require("node:path");

const { resolveInsideRoot } = require("./themeValidator");

async function pathExists(filePath) {
  try {
    await fs.access(filePath);
    return true;
  } catch {
    return false;
  }
}

async function readJson(filePath) {
  const source = await fs.readFile(filePath, "utf8");
  return JSON.parse(source);
}

async function listThemes(themesDir) {
  const entries = await fs.readdir(themesDir, { withFileTypes: true });
  const themes = [];

  for (const entry of entries) {
    if (!entry.isDirectory()) {
      continue;
    }
    if (entry.name.startsWith(".staging-")) {
      continue;
    }

    const themeDir = path.join(themesDir, entry.name);
    const manifestPath = path.join(themeDir, "manifest.json");
    if (!(await pathExists(manifestPath))) {
      continue;
    }

    try {
      const manifest = await readJson(manifestPath);
      themes.push({
        id: manifest.id,
        name: manifest.name,
        version: manifest.version,
        author: manifest.author,
      });
    } catch {
      // skip invalid theme metadata
    }
  }

  themes.sort((left, right) => left.name.localeCompare(right.name));
  return themes;
}

async function loadThemeBundle(themesDir, themeId) {
  const themeDir = path.join(themesDir, themeId);
  const manifestPath = path.join(themeDir, "manifest.json");
  const manifest = await readJson(manifestPath);

  const cssPath = resolveInsideRoot(themeDir, manifest.entryCss);
  const tokensPath = resolveInsideRoot(themeDir, manifest.entryTokens);
  if (!cssPath) {
    throw new Error("invalid entryCss path");
  }
  if (!tokensPath) {
    throw new Error("invalid entryTokens path");
  }

  const tokens = await readJson(tokensPath);
  const css = await fs.readFile(cssPath, "utf8");

  return {
    manifest,
    tokens,
    css,
  };
}

module.exports = {
  listThemes,
  loadThemeBundle,
};
