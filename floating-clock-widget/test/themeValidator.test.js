const test = require("node:test");
const assert = require("node:assert/strict");

const {
  compareSemver,
  isVersionCompatible,
  normalizeThemeRelativePath,
  resolveInsideRoot,
} = require("../src/theme/themeValidator");

test("compareSemver compares semantic versions", () => {
  assert.equal(compareSemver("0.1.0", "0.1.0"), 0);
  assert.equal(compareSemver("0.2.0", "0.1.9"), 1);
  assert.equal(compareSemver("1.0.0", "1.1.0"), -1);
});

test("isVersionCompatible validates current >= required", () => {
  assert.equal(isVersionCompatible("0.1.0", "0.1.0"), true);
  assert.equal(isVersionCompatible("0.1.0", "0.3.0"), true);
  assert.equal(isVersionCompatible("0.4.0", "0.3.9"), false);
});

test("normalizeThemeRelativePath rejects traversal", () => {
  assert.equal(normalizeThemeRelativePath("panel.css"), "panel.css");
  assert.equal(normalizeThemeRelativePath("assets\\img.webp"), "assets\\img.webp");
  assert.equal(normalizeThemeRelativePath("../panel.css"), null);
  assert.equal(normalizeThemeRelativePath("/panel.css"), "panel.css");
});

test("resolveInsideRoot keeps paths in root", () => {
  const resolved = resolveInsideRoot("C:\\theme", "panel.css");
  assert.equal(resolved, "C:\\theme\\panel.css");
  assert.equal(resolveInsideRoot("C:\\theme", "../etc/passwd"), null);
});
