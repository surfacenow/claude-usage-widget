const path = require("node:path");

const APP_RUNTIME_FOLDER = "FloatingClockWidget";

function resolveRuntimePaths(localAppDataPath) {
  const rootDir = path.join(localAppDataPath, APP_RUNTIME_FOLDER);

  return {
    rootDir,
    themesDir: path.join(rootDir, "themes"),
    importsTmpDir: path.join(rootDir, "imports", "tmp"),
    importsFailedDir: path.join(rootDir, "imports", "failed"),
    logsDir: path.join(rootDir, "logs"),
  };
}

module.exports = {
  APP_RUNTIME_FOLDER,
  resolveRuntimePaths,
};
