const api = window.floatingClock;

const panel = document.getElementById("clock-panel");
const timeLabel = document.getElementById("clock-time");
const dateLabel = document.getElementById("clock-date");
const appVersionLabel = document.getElementById("app-version");
const togglePerformanceButton = document.getElementById("toggle-performance");
const exitAppButton = document.getElementById("exit-app");
const statusLine = document.getElementById("status-line");
const themeSelect = document.getElementById("theme-select");
const openImportButton = document.getElementById("open-import");

const modal = document.getElementById("import-modal");
const closeImportButton = document.getElementById("close-import");
const selectedZipLabel = document.getElementById("selected-zip");
const checkList = document.getElementById("check-list");
const hintList = document.getElementById("ai-hints");
const pickZipButton = document.getElementById("pick-zip");
const runImportButton = document.getElementById("run-import");

const motionQuery = window.matchMedia("(prefers-reduced-motion: reduce)");
const STORAGE_LITE_MODE_KEY = "fcw-lite-mode";

const CHECK_LABELS = {
  "manifest-schema": "manifest schema",
  "tokens-schema": "tokens schema",
  "required-app-version": "required app version",
  "preview-file": "preview file",
  "entry-css": "entry css",
  "entry-tokens": "entry tokens",
  "theme-id-unique": "theme id unique",
};

const state = {
  selectedZipPath: null,
  reduceMotion: motionQuery.matches,
  liteMode: false,
  autoLiteReason: "",
  locatorTimerId: null,
  clockTimerId: null,
  currentThemeId: null,
  currentMotion: {
    driftPx: 4,
    driftCycleSec: 14,
    locatorPulseSec: 8,
  },
};

function setStatus(message, isError = false) {
  if (!statusLine) {
    return;
  }
  statusLine.textContent = message;
  statusLine.classList.toggle("error", Boolean(isError));
}

function detectLowEndHardware() {
  const cores = Number(navigator.hardwareConcurrency) || 4;
  const memory =
    typeof navigator.deviceMemory === "number" ? Number(navigator.deviceMemory) : 8;

  if (cores <= 4) {
    return { lowEnd: true, reason: `CPU cores ${cores}` };
  }
  if (memory <= 4) {
    return { lowEnd: true, reason: `device memory ${memory}GB` };
  }

  return { lowEnd: false, reason: "" };
}

function loadLiteModePreference() {
  try {
    const saved = window.localStorage.getItem(STORAGE_LITE_MODE_KEY);
    if (saved === "1") {
      return true;
    }
    if (saved === "0") {
      return false;
    }
  } catch {
    // ignore storage errors
  }

  const result = detectLowEndHardware();
  state.autoLiteReason = result.reason;
  return result.lowEnd;
}

function saveLiteModePreference(value) {
  try {
    window.localStorage.setItem(STORAGE_LITE_MODE_KEY, value ? "1" : "0");
  } catch {
    // ignore storage errors
  }
}

function updateLiteButtonLabel() {
  if (!togglePerformanceButton) {
    return;
  }
  togglePerformanceButton.textContent = state.liteMode ? "LITE ON" : "LITE OFF";
  togglePerformanceButton.classList.toggle("active", state.liteMode);
}

function formatNow() {
  const now = new Date();
  const hours = String(now.getHours()).padStart(2, "0");
  const minutes = String(now.getMinutes()).padStart(2, "0");
  const seconds = String(now.getSeconds()).padStart(2, "0");
  const year = now.getFullYear();
  const month = String(now.getMonth() + 1).padStart(2, "0");
  const day = String(now.getDate()).padStart(2, "0");

  return {
    time: `${hours}:${minutes}:${seconds}`,
    date: `${year}-${month}-${day}`,
  };
}

function renderClock() {
  if (!timeLabel || !dateLabel) {
    return;
  }
  const output = formatNow();
  timeLabel.textContent = output.time;
  dateLabel.textContent = output.date;
}

function stopClockLoop() {
  if (state.clockTimerId) {
    window.clearTimeout(state.clockTimerId);
    state.clockTimerId = null;
  }
}

function startClockLoop() {
  stopClockLoop();

  const tick = () => {
    renderClock();
    const now = Date.now();
    const nextDelay = 1000 - (now % 1000);
    state.clockTimerId = window.setTimeout(tick, nextDelay);
  };

  tick();
}

function clearLocatorTimer() {
  if (state.locatorTimerId) {
    window.clearInterval(state.locatorTimerId);
    state.locatorTimerId = null;
  }
}

function triggerLocatorPulse() {
  if (!panel || state.reduceMotion) {
    return;
  }

  panel.classList.remove("locator-pulse");
  void panel.offsetWidth;
  panel.classList.add("locator-pulse");
}

function refreshMotionState() {
  if (!panel) {
    return;
  }

  clearLocatorTimer();

  const shouldDisableMotion =
    state.reduceMotion || state.liteMode || document.visibilityState !== "visible";

  if (shouldDisableMotion) {
    panel.classList.remove("drift-on");
    return;
  }

  panel.classList.add("drift-on");
  const locatorSec = Number(state.currentMotion.locatorPulseSec) || 8;
  state.locatorTimerId = window.setInterval(
    () => triggerLocatorPulse(),
    Math.max(locatorSec, 2) * 1000,
  );
}

function applyPerformanceMode(showStatus = false) {
  document.documentElement.classList.toggle("performance-low", state.liteMode);
  updateLiteButtonLabel();
  refreshMotionState();

  if (showStatus) {
    setStatus(state.liteMode ? "LITE mode enabled" : "LITE mode disabled");
  }
}

function toCheckItemClass(check) {
  if (check.pass === true) {
    return "check-item pass";
  }
  if (check.pass === false) {
    return "check-item fail";
  }
  return "check-item pending";
}

function buildPendingChecks() {
  return Object.keys(CHECK_LABELS).map((id) => ({
    id,
    label: CHECK_LABELS[id],
    pass: null,
    detail: "awaiting import",
  }));
}

function renderChecks(checks) {
  if (!checkList) {
    return;
  }

  checkList.innerHTML = "";
  const source = checks.length > 0 ? checks : buildPendingChecks();

  for (const entry of source) {
    const item = document.createElement("li");
    item.className = toCheckItemClass(entry);

    const stateLabel =
      entry.pass === true ? "PASS" : entry.pass === false ? "FAIL" : "PENDING";
    const label = entry.label || CHECK_LABELS[entry.id] || entry.id;
    item.textContent = `${stateLabel} | ${label} | ${entry.detail || ""}`;
    checkList.append(item);
  }
}

function renderHints(hints) {
  if (!hintList) {
    return;
  }

  hintList.innerHTML = "";
  for (const hint of hints) {
    const item = document.createElement("li");
    item.className = "hint-item";
    item.textContent = hint;
    hintList.append(item);
  }
}

function buildAiHints(result) {
  if (result.ok) {
    return [
      "Theme import succeeded. Tune driftPx and glow if you need more visibility.",
      "For lower CPU use, keep LITE mode enabled.",
    ];
  }

  const byCheckId = {
    "manifest-schema": "Fix required keys and semver values in manifest.json.",
    "tokens-schema": "Match tokens.json structure to schema (colors/motion/radius).",
    "required-app-version": "Lower requiredAppVersion to an app-supported version.",
    "preview-file": "Ensure manifest.preview file exists inside the zip.",
    "entry-css": "Ensure manifest.entryCss points to a valid file in the theme root.",
    "entry-tokens": "Ensure manifest.entryTokens points to a valid tokens file.",
    "theme-id-unique": "Change manifest.id because that theme id already exists.",
  };

  const failedChecks = (result.checks || []).filter((entry) => entry.pass === false);
  if (failedChecks.length === 0) {
    return ["Check zip layout. manifest.json and tokens.json are required."];
  }

  const hints = [];
  for (const failed of failedChecks) {
    if (byCheckId[failed.id]) {
      hints.push(byCheckId[failed.id]);
    }
  }

  if (hints.length === 0) {
    hints.push("Re-check schema fields and zip relative paths before retry.");
  }

  return [...new Set(hints)];
}

function applyThemeCss(cssText) {
  let style = document.getElementById("theme-css-overrides");
  if (!style) {
    style = document.createElement("style");
    style.id = "theme-css-overrides";
    document.head.append(style);
  }
  style.textContent = cssText || "";
}

function setTokenVariables(tokens) {
  const rootStyle = document.documentElement.style;
  rootStyle.setProperty("--panel-bg", tokens.colors.panel);
  rootStyle.setProperty("--text-main", tokens.colors.textMain);
  rootStyle.setProperty("--text-sub", tokens.colors.textSub);
  rootStyle.setProperty("--accent-a", tokens.colors.accentA);
  rootStyle.setProperty("--accent-b", tokens.colors.accentB);
  rootStyle.setProperty("--accent-c", tokens.colors.accentC);
  rootStyle.setProperty("--panel-radius", `${tokens.radius.panel}px`);
  rootStyle.setProperty("--panel-shadow", tokens.shadow.panelGlow);
  rootStyle.setProperty("--panel-blur", `${tokens.blur.panelBackdrop}px`);
  rootStyle.setProperty("--clock-font", tokens.typography.clockFont);
  rootStyle.setProperty("--ui-font", tokens.typography.uiFont);
  rootStyle.setProperty("--drift-px", `${tokens.motion.driftPx}px`);
  rootStyle.setProperty("--drift-cycle-sec", `${tokens.motion.driftCycleSec}s`);
}

async function applyTheme(themeId) {
  const bundle = await api.loadTheme(themeId);
  state.currentThemeId = themeId;
  state.currentMotion = bundle.tokens.motion;

  setTokenVariables(bundle.tokens);
  applyThemeCss(bundle.css);
  refreshMotionState();
  setStatus(`Theme applied: ${bundle.manifest.name}`);
}

async function loadThemeList(preferredThemeId = null) {
  if (!themeSelect) {
    return;
  }

  const themes = await api.listThemes();
  themeSelect.innerHTML = "";

  for (const theme of themes) {
    const option = document.createElement("option");
    option.value = theme.id;
    option.textContent = `${theme.name} (${theme.version})`;
    themeSelect.append(option);
  }

  if (themes.length === 0) {
    setStatus("No themes found", true);
    return;
  }

  const fallbackThemeId = themes[0].id;
  const resolvedThemeId =
    preferredThemeId && themes.some((theme) => theme.id === preferredThemeId)
      ? preferredThemeId
      : fallbackThemeId;

  themeSelect.value = resolvedThemeId;
  await applyTheme(resolvedThemeId);
}

function openModal() {
  if (!modal) {
    return;
  }

  modal.classList.remove("hidden");
  modal.setAttribute("aria-hidden", "false");

  renderChecks(buildPendingChecks());
  renderHints(["Select a zip to run validation and import."]);

  if (selectedZipLabel) {
    selectedZipLabel.textContent = state.selectedZipPath
      ? `zip: ${state.selectedZipPath}`
      : "zip: none";
  }

  if (runImportButton) {
    runImportButton.disabled = !state.selectedZipPath;
  }
}

function closeModal() {
  if (!modal) {
    return;
  }
  modal.classList.add("hidden");
  modal.setAttribute("aria-hidden", "true");
}

async function handleZipPick() {
  const pickedPath = await api.pickThemeZip();
  if (!pickedPath) {
    return;
  }

  state.selectedZipPath = pickedPath;

  if (selectedZipLabel) {
    selectedZipLabel.textContent = `zip: ${pickedPath}`;
  }
  if (runImportButton) {
    runImportButton.disabled = false;
  }

  renderChecks(buildPendingChecks());
  renderHints([
    "Schema and entry path checks will run during import.",
    "Invalid themes are rejected with no impact to existing themes.",
  ]);
}

async function handleThemeImport() {
  if (!state.selectedZipPath || !runImportButton) {
    return;
  }

  runImportButton.disabled = true;
  setStatus("Importing theme...");

  const result = await api.importThemeZip(state.selectedZipPath);
  renderChecks(result.checks || []);
  renderHints(buildAiHints(result));

  if (!result.ok) {
    setStatus(`Import failed: ${result.message}`, true);
    runImportButton.disabled = false;
    return;
  }

  await loadThemeList(result.themeId);
  closeModal();
  state.selectedZipPath = null;
  setStatus(`Theme imported: ${result.manifest.name}`);
}

function registerInteractions() {
  if (exitAppButton) {
    exitAppButton.addEventListener("click", async () => {
      try {
        await api.quitApp();
      } catch (error) {
        setStatus(`Exit failed: ${error.message}`, true);
      }
    });
  }

  if (togglePerformanceButton) {
    togglePerformanceButton.addEventListener("click", () => {
      state.liteMode = !state.liteMode;
      saveLiteModePreference(state.liteMode);
      applyPerformanceMode(true);
    });
  }

  if (themeSelect) {
    themeSelect.addEventListener("change", async () => {
      const themeId = themeSelect.value;
      if (!themeId) {
        return;
      }
      await applyTheme(themeId);
    });
  }

  if (openImportButton) {
    openImportButton.addEventListener("click", openModal);
  }

  if (closeImportButton) {
    closeImportButton.addEventListener("click", closeModal);
  }

  if (modal) {
    modal.addEventListener("click", (event) => {
      const target = event.target;
      if (target instanceof HTMLElement && target.dataset.closeModal === "true") {
        closeModal();
      }
    });
  }

  window.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      closeModal();
    }
  });

  if (pickZipButton) {
    pickZipButton.addEventListener("click", () => {
      void handleZipPick();
    });
  }

  if (runImportButton) {
    runImportButton.addEventListener("click", () => {
      void handleThemeImport();
    });
  }

  motionQuery.addEventListener("change", (event) => {
    state.reduceMotion = event.matches;
    refreshMotionState();
    if (event.matches) {
      setStatus("Motion disabled by OS preference");
    }
  });

  document.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "visible") {
      startClockLoop();
      refreshMotionState();
    } else {
      stopClockLoop();
      clearLocatorTimer();
    }
  });

  api.onLocatorPulse(() => {
    triggerLocatorPulse();
    setStatus("Locator pulse: Ctrl+Alt+C");
  });
}

async function bootstrap() {
  if (!api) {
    setStatus("preload API missing", true);
    return;
  }

  state.liteMode = loadLiteModePreference();

  const appVersion = await api.getAppVersion();
  if (appVersionLabel) {
    appVersionLabel.textContent = `v${appVersion}`;
  }

  applyPerformanceMode(false);
  registerInteractions();

  renderChecks(buildPendingChecks());
  renderHints(["You can import a theme zip from the Import button."]);

  await loadThemeList();
  startClockLoop();

  if (state.liteMode && state.autoLiteReason) {
    setStatus(`LITE mode enabled automatically (${state.autoLiteReason})`);
  } else {
    setStatus("Ctrl+Alt+C for locator pulse");
  }
}

void bootstrap().catch((error) => {
  setStatus(`Initialization failed: ${error.message}`, true);
});