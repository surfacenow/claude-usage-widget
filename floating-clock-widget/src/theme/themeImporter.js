const fs = require("node:fs");
const fsp = require("node:fs/promises");
const path = require("node:path");
const crypto = require("node:crypto");
const yauzl = require("yauzl");

const { validateThemeBundle } = require("./themeValidator");

function sanitizeZipEntryName(entryName) {
  const normalized = String(entryName).replace(/\\/g, "/").replace(/^\/+/, "");
  if (!normalized) {
    return null;
  }

  const isDirectory = normalized.endsWith("/");
  const trimmed = normalized.replace(/\/+$/, "");
  if (!trimmed) {
    return null;
  }

  const segments = trimmed.split("/");
  if (
    segments.some((segment) => segment.length === 0) ||
    segments.includes(".") ||
    segments.includes("..")
  ) {
    throw new Error(`unsafe zip entry path: ${entryName}`);
  }

  return {
    relativePath: segments.join(path.sep),
    isDirectory,
  };
}

function isSymlinkEntry(entry) {
  const mode = (entry.externalFileAttributes >>> 16) & 0o170000;
  return mode === 0o120000;
}

function openZip(zipPath) {
  return new Promise((resolve, reject) => {
    yauzl.open(zipPath, { lazyEntries: true }, (error, zipFile) => {
      if (error) {
        reject(error);
        return;
      }
      resolve(zipFile);
    });
  });
}

async function extractZipSecure(zipPath, destinationDir) {
  const zipFile = await openZip(zipPath);
  const destinationRoot = path.resolve(destinationDir);

  await new Promise((resolve, reject) => {
    let settled = false;

    const fail = (error) => {
      if (settled) {
        return;
      }
      settled = true;
      try {
        zipFile.close();
      } catch {
        // no-op
      }
      reject(error);
    };

    const complete = () => {
      if (settled) {
        return;
      }
      settled = true;
      resolve();
    };

    zipFile.once("end", complete);
    zipFile.once("error", fail);

    zipFile.on("entry", async (entry) => {
      if (settled) {
        return;
      }

      try {
        if (isSymlinkEntry(entry)) {
          throw new Error(`symlink is not allowed: ${entry.fileName}`);
        }

        const entryInfo = sanitizeZipEntryName(entry.fileName);
        if (!entryInfo) {
          zipFile.readEntry();
          return;
        }

        const outputPath = path.resolve(destinationRoot, entryInfo.relativePath);
        const outputPrefix = `${destinationRoot}${path.sep}`;
        if (outputPath !== destinationRoot && !outputPath.startsWith(outputPrefix)) {
          throw new Error(`path traversal rejected: ${entry.fileName}`);
        }

        if (entryInfo.isDirectory) {
          await fsp.mkdir(outputPath, { recursive: true });
          zipFile.readEntry();
          return;
        }

        await fsp.mkdir(path.dirname(outputPath), { recursive: true });
        zipFile.openReadStream(entry, (streamError, readStream) => {
          if (streamError) {
            fail(streamError);
            return;
          }

          const writeStream = fs.createWriteStream(outputPath, { mode: 0o644 });
          readStream.once("error", fail);
          writeStream.once("error", fail);
          writeStream.once("close", () => {
            zipFile.readEntry();
          });
          readStream.pipe(writeStream);
        });
      } catch (error) {
        fail(error);
      }
    });

    zipFile.readEntry();
  });
}

async function pathExists(targetPath) {
  try {
    await fsp.access(targetPath);
    return true;
  } catch {
    return false;
  }
}

async function findThemeRoot(extractedRoot) {
  const candidates = [];

  async function walk(currentDir) {
    const entries = await fsp.readdir(currentDir, { withFileTypes: true });
    const entryNames = new Set(entries.map((entry) => entry.name));
    if (entryNames.has("manifest.json") && entryNames.has("tokens.json")) {
      candidates.push(currentDir);
    }

    for (const entry of entries) {
      if (!entry.isDirectory()) {
        continue;
      }
      await walk(path.join(currentDir, entry.name));
    }
  }

  await walk(extractedRoot);

  if (candidates.length === 0) {
    throw new Error("manifest.json and tokens.json were not found in zip");
  }

  candidates.sort((left, right) => {
    const leftDepth = left.split(path.sep).length;
    const rightDepth = right.split(path.sep).length;
    if (leftDepth !== rightDepth) {
      return leftDepth - rightDepth;
    }
    return left.localeCompare(right);
  });

  return candidates[0];
}

async function readJson(filePath) {
  const source = await fsp.readFile(filePath, "utf8");
  try {
    return JSON.parse(source);
  } catch (error) {
    throw new Error(`invalid JSON in ${path.basename(filePath)}: ${error.message}`);
  }
}

function sanitizeFileName(name) {
  return name.replace(/[^a-zA-Z0-9._-]+/g, "_");
}

async function copyZipToFailed(zipPath, failedDir, importId) {
  const safeName = sanitizeFileName(path.basename(zipPath));
  const failedZipPath = path.join(failedDir, `${importId}-${safeName}`);
  try {
    await fsp.copyFile(zipPath, failedZipPath);
  } catch {
    // no-op; import failure reporting should still continue
  }
}

async function importThemeZip({
  zipPath,
  runtimePaths,
  currentAppVersion,
}) {
  const importId = `${Date.now()}-${crypto.randomUUID().slice(0, 8)}`;
  const importWorkDir = path.join(runtimePaths.importsTmpDir, importId);
  const baseFailure = {
    ok: false,
    themeId: null,
    manifest: null,
    tokens: null,
  };

  await fsp.mkdir(importWorkDir, { recursive: true });

  try {
    await extractZipSecure(zipPath, importWorkDir);
    const themeRootDir = await findThemeRoot(importWorkDir);

    const manifest = await readJson(path.join(themeRootDir, "manifest.json"));
    const tokens = await readJson(path.join(themeRootDir, "tokens.json"));
    const validation = await validateThemeBundle({
      manifest,
      tokens,
      themeRootDir,
      currentAppVersion,
    });

    if (!validation.valid) {
      await copyZipToFailed(zipPath, runtimePaths.importsFailedDir, importId);
      return {
        ...baseFailure,
        errorCode: "validation_failed",
        message: "Theme validation failed",
        checks: validation.checks,
      };
    }

    const targetDir = path.join(runtimePaths.themesDir, manifest.id);
    if (await pathExists(targetDir)) {
      await copyZipToFailed(zipPath, runtimePaths.importsFailedDir, importId);
      return {
        ...baseFailure,
        errorCode: "duplicate_theme_id",
        message: `Theme id already exists: ${manifest.id}`,
        checks: [
          ...validation.checks,
          {
            id: "theme-id-unique",
            label: "theme id unique",
            pass: false,
            detail: `theme id '${manifest.id}' already exists`,
          },
        ],
      };
    }

    const stagingDir = path.join(
      runtimePaths.themesDir,
      `.staging-${manifest.id}-${importId}`,
    );
    await fsp.cp(themeRootDir, stagingDir, {
      recursive: true,
      force: false,
      errorOnExist: true,
    });

    try {
      await fsp.rename(stagingDir, targetDir);
    } catch (error) {
      await fsp.rm(stagingDir, { recursive: true, force: true });
      throw error;
    }

    return {
      ok: true,
      themeId: manifest.id,
      manifest,
      tokens,
      checks: validation.checks,
      errorCode: null,
      message: "Theme imported successfully",
    };
  } catch (error) {
    await copyZipToFailed(zipPath, runtimePaths.importsFailedDir, importId);
    return {
      ...baseFailure,
      errorCode: "import_failed",
      message: error.message || "Import failed",
      checks: [],
    };
  } finally {
    await fsp.rm(importWorkDir, { recursive: true, force: true });
  }
}

module.exports = {
  extractZipSecure,
  importThemeZip,
  sanitizeZipEntryName,
};
