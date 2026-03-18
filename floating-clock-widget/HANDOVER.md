# Floating Clock Widget 引継書

作成日: 2026-02-26

## 1. プロジェクト目的
Windows向けのフローティング時計ウィジェットを作る。
画像のような作り込みUIを目指すが、まずはMVPから段階実装する。

## 2. 決定済み要件
- テイスト: `ポップ`（黒ベース + ネオンアクセント）
- 将来拡張: 後からデザイン(テーマ)を追加可能にする
- テーマ取込: まずは `zip` 限定（安定優先）
- 視認性対策: 時計パネル自体に微小な動きをつける
  - 長針短針ではなく、パネル全体の存在感を出す演出

## 3. UI仕様ドラフト（確定寄り）
### テーマ/見た目
- ベース: `#0B0B12`
- アクセント候補: `#00E5FF`, `#FF3DFF`, `#C8FF3D`
- 角丸: 20-24px
- 発光: 弱め（常時視認性優先）

### 動き（見失い防止）
- 常時 `微小ドリフト`（デフォルトON）
- 推奨値: ±4px、周期12-16秒、transformのみ
- `ロケーターパルス`: 8秒ごとに1回
- ホットキー例: `Ctrl+Alt+C` で「ここにあるよ」演出
- `prefers-reduced-motion` で自動OFF

### テーマ追加フロー
- 「追加」ボタン -> zip選択 -> 取込ポップアップ
- ポップアップは2ペイン:
  - 左: 仕様チェック（必須ファイル/必須キーのPass/Fail）
  - 右: AI相談（不足修正提案、配色/動き調整）
- バリデーション未通過時は追加不可
- 失敗時は既存テーマに影響なし

## 4. テーマ構造（案）
```text
/themes/<theme-id>/manifest.json
/themes/<theme-id>/preview.webp
/themes/<theme-id>/tokens.json
/themes/<theme-id>/panel.css
/themes/<theme-id>/assets/*
```

## 5. スキーマ（最小案）
### manifest.json
必須キー:
- `id`
- `name`
- `version`
- `requiredAppVersion`
- `author`
- `preview`
- `entryCss`
- `entryTokens`

### tokens.json
主な上書き対象:
- `colors`
- `typography`
- `radius`
- `shadow`
- `blur`
- `motion`（`driftPx`, `driftCycleSec`, `locatorPulseSec`）

## 6. フォルダ方針（決定済み）
### 開発フォルダ
`C:\Users\Sonico\Dev\Widgets\floating-clock-widget`

### 実行時データ
`%LOCALAPPDATA%\FloatingClockWidget\`

作成済み:
- `C:\Users\Sonico\AppData\Local\FloatingClockWidget\themes`
- `C:\Users\Sonico\AppData\Local\FloatingClockWidget\imports\tmp`
- `C:\Users\Sonico\AppData\Local\FloatingClockWidget\imports\failed`
- `C:\Users\Sonico\AppData\Local\FloatingClockWidget\logs`

## 7. 未確定項目（次に決める）
- 初期ウィジェットサイズ（S/M/L）
- 秒表示の初期値（表示ON/OFF）
- 使用スタック（Tauri/Electron/他）

## 8. 次のCodexの初手タスク
1. 使用スタックを確定し、最小プロジェクトを初期化
2. `theme schema` を JSON Schema 化（manifest/tokens）
3. zip取込パイプライン実装（tmp展開 -> 検証 -> themes配置）
4. 見失い対策モーション（LOWプリセット）を実装
5. スキン追加ポップアップの土台UIを実装

## 9. 実装時の安全ルール
- zip内の `../` を拒否（パストラバーサル対策）
- 検証失敗時は全破棄してロールバック
- アニメーションは `transform/opacity` のみ

---
この引継書を起点に進めれば、仕様のズレなく再開できます。
