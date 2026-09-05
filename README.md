# MinecraftResourceManager

Minecraftの複数インスタンス間で、Resource PacksとShader Packsを共有するためのWindows向けツールです。

## 機能

- Resource Packsの共有
- Shader Packsの共有
- Minecraftインスタンスごとに共有設定を変更
- `options.txt` を検索してMinecraftインスタンスを検出
- フォルダ単位のシンボリックリンクを使用
- 設定を自動保存
- 共有解除時に元のフォルダをバックアップ・復元

## 動作環境

- Windows 10 / 11
- x64
- .NET 10 Runtimeは不要（Self-contained版）

## 使い方

1. MinecraftのResource Packs共有元フォルダを指定
2. Shader Packs共有元フォルダを指定
3. Minecraftインスタンスを追加
4. インスタンスごとに共有する項目を選択
5. 「共有を適用」をクリック

## 注意事項

本アプリはMinecraftインスタンスのフォルダにシンボリックリンクを作成します。

共有設定を適用する際は、管理者権限が必要です。

Resource Packsの共有元フォルダ名は `resourcepacks`、
Shader Packsの共有元フォルダ名は `shaderpacks` としてください。

## ダウンロード

正式版は GitHub Releases からダウンロードできます。

## ライセンス

TBD
