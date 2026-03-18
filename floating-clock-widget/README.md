# Floating Clock Widget (MVP)

This repository now contains two implementations:

- `native/` - Windows native app (`.NET 8 WPF`) [recommended]
- `src/` - legacy Electron implementation

## Native build (recommended)

```bash
dotnet build native/FloatingClockWidget.Native/FloatingClockWidget.Native.csproj -c Release
dotnet publish native/FloatingClockWidget.Native/FloatingClockWidget.Native.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o native/dist/win-x64
```

Published exe:

```text
native/dist/win-x64/FloatingClockWidget.Native.exe
```

Current published size is roughly `~215 KB` (framework-dependent).

## Native features

- Borderless always-on-top floating clock
- Single-instance lock
- Lightweight mode (`LITE`) toggle
- Locator pulse and global hotkey (`Ctrl+Alt+C`)
- Theme loading from `%LOCALAPPDATA%\\FloatingClockWidget\\themes`
- Secure zip import pipeline (`../` and symlink rejection)
- Theme validation checks for `manifest.json` and `tokens.json`

## Stack

- Electron (main/preload/renderer)
- Vanilla HTML/CSS/JS
- AJV (JSON Schema validation)
- yauzl (secure zip extraction)

## What is implemented

- Floating clock panel with pop neon default theme
- Reduced motion support via `prefers-reduced-motion`
- Drift motion + locator pulse (`Ctrl+Alt+C`)
- Theme schema validation (`manifest.json`, `tokens.json`)
- Zip import pipeline:
  - extract to `%LOCALAPPDATA%\\FloatingClockWidget\\imports\\tmp`
  - validate schema and required files
  - stage copy and atomic move to `themes`
  - reject traversal (`../`) and symlink entries
  - copy failed zips to `imports\\failed`
- Theme import popup with two panes:
  - left: pass/fail checklist
  - right: AI hint suggestions

## Runtime directories

- `%LOCALAPPDATA%\\FloatingClockWidget\\themes`
- `%LOCALAPPDATA%\\FloatingClockWidget\\imports\\tmp`
- `%LOCALAPPDATA%\\FloatingClockWidget\\imports\\failed`
- `%LOCALAPPDATA%\\FloatingClockWidget\\logs`

## Start

```bash
npm install
npm start
```

## Test

```bash
npm test
```

## Theme structure

```text
/themes/<theme-id>/manifest.json
/themes/<theme-id>/preview.webp
/themes/<theme-id>/tokens.json
/themes/<theme-id>/panel.css
/themes/<theme-id>/assets/*
```
