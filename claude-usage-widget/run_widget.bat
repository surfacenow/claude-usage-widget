@echo off
cd /d "%~dp0"
start "" pythonw claude_fetcher.py
timeout /t 2 /nobreak >nul
start "" pythonw claude_overlay.py
