const fs = require("node:fs/promises");
const path = require("node:path");
const {
  app,
  BrowserWindow,
  dialog,
  globalShortcut,
  ipcMain,
} = require("electron");

const { ensureDefaultTheme, DEFAULT_THEME_ID } = require("./theme/defaultTheme");
const { importThemeZip } = require("./theme/themeImporter");
const { resolveRuntimePaths } = require("./theme/runtimePaths");
const { listThemes, loadThemeBundle } = require("./theme/themeStore");

const APP_VERSION = "0.1.0";
const LOCATOR_SHORTCUT = "CommandOrControl+Alt+C";

let mainWindow = null;
let runtimePaths = null;

const hasSingleInstanceLock = app.requestSingleInstanceLock();
if (!hasSingleInstanceLock) {
  app.quit();
  process.exit(0);
}

function getLocalAppDataPath() {
  const fromEnv = process.env.LOCALAPPDATA;
  if (fromEnv && fromEnv.trim().length > 0) {
    return fromEnv;
  }
  return path.join(app.getPath("home"), "AppData", "Local");
}

async function ensureRuntimeDirectories(paths) {
  await fs.mkdir(paths.themesDir, { recursive: true });
  await fs.mkdir(paths.importsTmpDir, { recursive: true });
  await fs.mkdir(paths.importsFailedDir, { recursive: true });
  await fs.mkdir(paths.logsDir, { recursive: true });
}

async function logToFile(message) {
  if (!runtimePaths) {
    return;
  }
  const line = `[${new Date().toISOString()}] ${message}\n`;
  const target = path.join(runtimePaths.logsDir, "widget.log");
  try {
    await fs.appendFile(target, line, "utf8");
  } catch {
    // no-op
  }
}

function createMainWindow() {
  mainWindow = new BrowserWindow({
    width: 360,
    height: 220,
    minWidth: 280,
    minHeight: 160,
    useContentSize: true,
    frame: false,
    transparent: true,
    alwaysOnTop: true,
    hasShadow: false,
    resizable: true,
    maximizable: false,
    webPreferences: {
      preload: path.join(__dirname, "preload.js"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false,
      spellcheck: false,
    },
  });

  mainWindow.setMenuBarVisibility(false);
  mainWindow.loadFile(path.join(__dirname, "renderer", "index.html"));

  mainWindow.on("closed", () => {
    mainWindow = null;
  });
}

function sendLocatorPulse() {
  if (!mainWindow || mainWindow.isDestroyed()) {
    return;
  }
  mainWindow.webContents.send("widget:locatorPulse");
}

function registerIpcHandlers() {
  ipcMain.handle("app:getVersion", async () => APP_VERSION);
  ipcMain.handle("app:quit", async () => {
    setImmediate(() => app.quit());
    return true;
  });

  ipcMain.handle("theme:list", async () => {
    return listThemes(runtimePaths.themesDir);
  });

  ipcMain.handle("theme:load", async (_, themeId) => {
    if (!themeId || typeof themeId !== "string") {
      throw new Error("themeId is required");
    }
    return loadThemeBundle(runtimePaths.themesDir, themeId);
  });

  ipcMain.handle("theme:pickZip", async () => {
    const result = await dialog.showOpenDialog({
      title: "Select Theme Zip",
      properties: ["openFile"],
      filters: [{ name: "Theme Zip", extensions: ["zip"] }],
    });

    if (result.canceled || result.filePaths.length === 0) {
      return null;
    }
    return result.filePaths[0];
  });

  ipcMain.handle("theme:importZip", async (_, zipPath) => {
    if (!zipPath || typeof zipPath !== "string") {
      throw new Error("zip path is required");
    }
    if (path.extname(zipPath).toLowerCase() !== ".zip") {
      return {
        ok: false,
        errorCode: "not_zip",
        message: "Only .zip files are supported",
        checks: [],
      };
    }

    const result = await importThemeZip({
      zipPath,
      runtimePaths,
      currentAppVersion: APP_VERSION,
    });
    if (!result.ok) {
      await logToFile(`Import failed: ${result.errorCode} ${result.message}`);
    } else {
      await logToFile(`Imported theme: ${result.themeId}`);
    }
    return result;
  });
}

function registerGlobalShortcuts() {
  const registered = globalShortcut.register(LOCATOR_SHORTCUT, sendLocatorPulse);
  if (!registered) {
    void logToFile(`Failed to register shortcut: ${LOCATOR_SHORTCUT}`);
  }
}

async function bootstrap() {
  runtimePaths = resolveRuntimePaths(getLocalAppDataPath());
  await ensureRuntimeDirectories(runtimePaths);
  await ensureDefaultTheme(runtimePaths.themesDir, APP_VERSION);
  registerIpcHandlers();
  createMainWindow();
  registerGlobalShortcuts();
}

app.whenReady().then(bootstrap).catch(async (error) => {
  await dialog.showMessageBox({
    type: "error",
    title: "Floating Clock Widget",
    message: "Failed to initialize application",
    detail: error?.stack || String(error),
  });
  app.quit();
});

app.on("second-instance", () => {
  if (!mainWindow || mainWindow.isDestroyed()) {
    return;
  }
  if (mainWindow.isMinimized()) {
    mainWindow.restore();
  }
  mainWindow.show();
  mainWindow.focus();
});

app.on("activate", () => {
  if (BrowserWindow.getAllWindows().length === 0) {
    createMainWindow();
  }
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") {
    app.quit();
  }
});

app.on("will-quit", () => {
  globalShortcut.unregisterAll();
});

module.exports = {
  APP_VERSION,
  DEFAULT_THEME_ID,
};
