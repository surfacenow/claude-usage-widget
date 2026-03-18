const fs = require("node:fs/promises");
const path = require("node:path");
const Ajv = require("ajv");

const manifestSchema = require("../schemas/manifest.schema.json");
const tokensSchema = require("../schemas/tokens.schema.json");

const ajv = new Ajv({
  allErrors: true,
  strict: false,
});

const validateManifestSchema = ajv.compile(manifestSchema);
const validateTokensSchema = ajv.compile(tokensSchema);

function compareSemver(left, right) {
  const leftParts = splitSemver(left);
  const rightParts = splitSemver(right);
  for (let index = 0; index < 3; index += 1) {
    if (leftParts[index] > rightParts[index]) {
      return 1;
    }
    if (leftParts[index] < rightParts[index]) {
      return -1;
    }
  }
  return 0;
}

function splitSemver(version) {
  const match = /^(\d+)\.(\d+)\.(\d+)$/.exec(String(version).trim());
  if (!match) {
    return [0, 0, 0];
  }
  return [Number(match[1]), Number(match[2]), Number(match[3])];
}

function isVersionCompatible(requiredAppVersion, currentAppVersion) {
  return compareSemver(currentAppVersion, requiredAppVersion) >= 0;
}

function normalizeThemeRelativePath(relativePath) {
  if (typeof relativePath !== "string") {
    return null;
  }

  const normalized = relativePath.replace(/\\/g, "/").replace(/^\/+/, "");
  if (!normalized) {
    return null;
  }

  const segments = normalized.split("/");
  if (segments.includes("..") || segments.some((segment) => segment.length === 0)) {
    return null;
  }

  return segments.join(path.sep);
}

function resolveInsideRoot(rootDir, relativePath) {
  const normalized = normalizeThemeRelativePath(relativePath);
  if (!normalized) {
    return null;
  }

  const candidatePath = path.resolve(rootDir, normalized);
  const rootPath = path.resolve(rootDir);
  const rootPrefix = `${rootPath}${path.sep}`;
  if (candidatePath !== rootPath && !candidatePath.startsWith(rootPrefix)) {
    return null;
  }
  return candidatePath;
}

function toSchemaErrors(errors) {
  if (!errors || errors.length === 0) {
    return [];
  }

  return errors.map((entry) => {
    const field = entry.instancePath || entry.schemaPath || "(root)";
    const detail = entry.message || "invalid value";
    return `${field}: ${detail}`;
  });
}

async function exists(filePath) {
  try {
    await fs.access(filePath);
    return true;
  } catch {
    return false;
  }
}

async function validateThemeBundle({
  manifest,
  tokens,
  themeRootDir,
  currentAppVersion,
}) {
  const checks = [];

  const manifestOk = validateManifestSchema(manifest);
  checks.push({
    id: "manifest-schema",
    label: "manifest schema",
    pass: manifestOk,
    detail: manifestOk ? "pass" : toSchemaErrors(validateManifestSchema.errors).join("; "),
  });

  const tokensOk = validateTokensSchema(tokens);
  checks.push({
    id: "tokens-schema",
    label: "tokens schema",
    pass: tokensOk,
    detail: tokensOk ? "pass" : toSchemaErrors(validateTokensSchema.errors).join("; "),
  });

  const compatible =
    manifestOk &&
    typeof manifest.requiredAppVersion === "string" &&
    isVersionCompatible(manifest.requiredAppVersion, currentAppVersion);
  checks.push({
    id: "required-app-version",
    label: "required app version",
    pass: compatible,
    detail: compatible
      ? `app ${currentAppVersion} satisfies ${manifest.requiredAppVersion}`
      : `app ${currentAppVersion} does not satisfy ${manifest?.requiredAppVersion ?? "unknown"}`,
  });

  const fileChecks = [
    {
      id: "preview-file",
      label: "preview file",
      relativePath: manifest?.preview,
    },
    {
      id: "entry-css",
      label: "entry css",
      relativePath: manifest?.entryCss,
    },
    {
      id: "entry-tokens",
      label: "entry tokens",
      relativePath: manifest?.entryTokens,
    },
  ];

  for (const fileCheck of fileChecks) {
    const resolvedPath = resolveInsideRoot(themeRootDir, fileCheck.relativePath);
    const found = resolvedPath ? await exists(resolvedPath) : false;
    checks.push({
      id: fileCheck.id,
      label: fileCheck.label,
      pass: found,
      detail: found ? "pass" : "missing or invalid path",
    });
  }

  return {
    valid: checks.every((entry) => entry.pass),
    checks,
  };
}

module.exports = {
  compareSemver,
  isVersionCompatible,
  normalizeThemeRelativePath,
  resolveInsideRoot,
  validateThemeBundle,
};
