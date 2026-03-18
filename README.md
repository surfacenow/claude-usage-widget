# Claude Usage Widget

Claude.ai の使用量（5時間 / 7日）をWindows タスクバー上に常時表示する軽量オーバーレイウィジェット。

![screenshot](screenshot.png)

## Features

- 5時間 / 7日の使用率をプログレスバーで表示
- リセットまでの残り時間を表示
- 使用率に応じて色が変化（シアン → アンバー → コーラル）
- 歩くドットマスコット付き（Clawd Invader）
- ドラッグで移動可能
- 右クリックメニュー（Scan Now / Auto / Session Key 変更 / Exit）

## Setup

### 1. 依存パッケージのインストール

```bash
pip install curl-cffi
```

### 2. セッションキーの取得

1. ブラウザで [claude.ai](https://claude.ai) にログイン
2. F12 → Application → Cookies → `claude.ai` → `sessionKey` をコピー

### 3. 起動

```bash
# run_widget.bat をダブルクリック、または:
pythonw claude_fetcher.py
pythonw claude_overlay.py
```

初回起動時にセッションキーの入力ダイアログが表示されます。

## Files

| File | Description |
|------|-------------|
| `claude_overlay.py` | メインUI（tkinter透過オーバーレイ） |
| `claude_fetcher.py` | API取得（curl_cffi、5分間隔で自動更新） |
| `setup_dialog.py` | セッションキー入力ダイアログ |
| `run_widget.bat` | 起動用バッチ |
| `mascot_patterns.py` | マスコットのドットパターン集 |

## Requirements

- Windows 10/11
- Python 3.10+
- curl-cffi
