const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("floatingClock", {
  getAppVersion: () => ipcRenderer.invoke("app:getVersion"),
  quitApp: () => ipcRenderer.invoke("app:quit"),
  listThemes: () => ipcRenderer.invoke("theme:list"),
  loadTheme: (themeId) => ipcRenderer.invoke("theme:load", themeId),
  pickThemeZip: () => ipcRenderer.invoke("theme:pickZip"),
  importThemeZip: (zipPath) => ipcRenderer.invoke("theme:importZip", zipPath),
  onLocatorPulse: (callback) => {
    if (typeof callback !== "function") {
      return () => {};
    }
    const listener = () => callback();
    ipcRenderer.on("widget:locatorPulse", listener);
    return () => ipcRenderer.removeListener("widget:locatorPulse", listener);
  },
});
