const test = require("node:test");
const assert = require("node:assert/strict");

const { sanitizeZipEntryName } = require("../src/theme/themeImporter");

test("sanitizeZipEntryName supports file and directory entries", () => {
  const fileEntry = sanitizeZipEntryName("theme/panel.css");
  assert.equal(fileEntry.isDirectory, false);
  assert.equal(fileEntry.relativePath, "theme\\panel.css");

  const dirEntry = sanitizeZipEntryName("theme/assets/");
  assert.equal(dirEntry.isDirectory, true);
  assert.equal(dirEntry.relativePath, "theme\\assets");
});

test("sanitizeZipEntryName rejects traversal", () => {
  assert.throws(() => sanitizeZipEntryName("../evil.txt"), /unsafe zip entry path/);
  assert.throws(() => sanitizeZipEntryName("theme/../../evil.txt"), /unsafe zip entry path/);
});
