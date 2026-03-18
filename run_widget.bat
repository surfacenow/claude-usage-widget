@echo off
start "" pythonw C:\Users\Sonico\Dev\Widgets\claude_fetcher.py
timeout /t 2 /nobreak >nul
start "" pythonw C:\Users\Sonico\Dev\Widgets\claude_overlay.py
