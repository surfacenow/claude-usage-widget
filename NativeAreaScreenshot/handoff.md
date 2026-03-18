# Area Screenshot (Native C# WPF App) - Handoff Document

## プロジェクトの経緯 (Project Background)
最初はChrome拡張機能として「選択範囲のスクリーンショットを自動で画像保存するツール」を開発していました。
しかし、**「Chrome拡張機能のセキュリティ都合上、ユーザーのダウンロードフォルダ以外にファイルを自動保存できない」**という制約に直面しました。
ユーザーの「任意のフォルダ（Cドライブやデスクトップなど）に直接即効で保存したい」という要件を満たすため、Chrome拡張機能をやめ、**C# WPFを用いたWindowsネイティブアプリ (.exe) の開発へとピボット**しました。

## 現在の実装状況 (Current Status)
WPFの透明なフルスクリーンウィンドウを用いて、背景を暗くし、マウスドラッグでキャプチャ領域を選択するUIの基盤実装は完了しています。
また、タスクトレイ（System Tray）に常駐し、設定画面を開いたりするための実装も進めました。

- **UI/UX:**
  - `MainWindow.xaml`: 画面全体を覆う透明なボーダーレスウィンドウ。マウス操作（MouseDown, MouseMove, MouseUp）をフックして選択矩形（青い点線枠）を描画。
  - `SettingsWindow.xaml`: ユーザーが保存先のカスタムフォルダをGUIで選択・設定できるウィンドウ。
- **ロジック:**
  - `App.xaml.cs`: アプリの起動時に画面を表示させず、タスクトレイアイコン(`Hardcodet.NotifyIcon.Wpf`)とグローバルホットキー(`NHotkey.Wpf`)を初期化。ホットキー（`Ctrl+Shift+S`）でキャプチャ画面を起動。
  - `SettingsManager.cs`: `Newtonsoft.Json` を使い、設定ファイル(`%APPDATA%\NativeAreaScreenshot\settings.json`)へ保存先パスを永続化。
  - キャプチャ処理: `MainWindow.xaml.cs` にて、マウスを離した瞬間に `System.Drawing.Graphics.CopyFromScreen` を利用してスクリーンショットを取得しPNG保存する処理を実装。

## 発生している問題 / 次のタスク (Current Issues / Next Steps)
現在、`dotnet build` を実行すると以下のコンパイルエラーが発生しています。

```
CS0117: 'TaskbarIcon'
```

**原因の推測と修正方針:**
`App.xaml.cs` にて `Hardcodet.NotifyIcon.Wpf.TaskbarIcon` を使用してタスクトレイアイコンを生成しようとしていますが、WPF（XAML側）での定義が必要なコンポーネントをコードビハインドで直接 `new TaskbarIcon()` とインスタンス化しようとしている、あるいは名前空間/参照の使い方が `Hardcodet.Wpf.TaskbarNotification` ライブラリの仕様と合致していないことが原因と思われます。

**引き継いだAIへの指示:**
1. `App.xaml.cs` の `TaskbarIcon` の初期化コードを修正し、ビルドエラー (CS0117) を解決してください。必要であれば `App.xaml` 側でリソースとして定義する方式に変更してください。
2. マルチモニター環境でのDPIスケーリング問題（`Graphics.CopyFromScreen` の座標ズレ）が発生する可能性が高いため、その補正処理を追加してください。
3. デスクトップアプリとしてビルド・実行し、問題なくホットキーで起動・保存できることを確認してください。

## 依存パッケージ (Nuget Packages)
- `System.Drawing.Common`
- `Newtonsoft.Json`
- `NHotkey.Wpf`
- `Hardcodet.NotifyIcon.Wpf`
