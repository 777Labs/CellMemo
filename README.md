<div align="center">

<img src="assets/icon.png" width="128" height="128" alt="CellMemo Icon" />

# CellMemo (セルメモ)

**軽快に起動し、Excelのように直感操作できるタブ型スプレッドシート・メモアプリ**

[![Release](https://img.shields.io/badge/Release-v1.0.0-238636?style=flat-square)](https://github.com/777Labs/CellMemo/releases)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-0078D4?style=flat-square)](https://github.com/777Labs/CellMemo)
[![License](https://img.shields.io/badge/License-Freeware-blue?style=flat-square)](LICENSE)

[📥 最新版をダウンロード (Releases)](https://github.com/777Labs/CellMemo/releases/latest)

</div>

---

## ✨ 主な特徴

- 🚀 **超高速・軽量起動**: 
  - インストール不要のポータブル設計。単一の実行ファイル（`CellMemo.exe`）だけで即座に起動します。
- 📑 **マルチタブ対応**: 
  - 複数の表・メモをブラウザ感覚でタブ切り替え。Ctrl+N / Ctrl+W / ダブルクリックで自由自在に扱えます。
- 📊 **Excel と同等の直感的な操作感**:
  - カーソルキー移動、Enterでの入力モード開始、Alt+Enterでのセル内改行に対応。
  - 数式入力時にセルや範囲をクリック＆ドラッグするだけでアドレス参照を自動挿入。
  - 行番号・列番号のドラッグ選択、右クリックからの一括挿入・削除に対応。
  - 境界ドラッグ時に **ピクセルとポイントをリアルタイム表示** ＆ 数値指定でのサイズ一括変更。
- 🧮 **便利な計算・ステータス表示**:
  - ステータスバーに現在の行・列位置、選択セルの合計値・平均値・個数、文字数をリアルタイム表示。
  - `=SUM()`, `=AVERAGE()`, `=COUNT()`, `=MIN()`, `=MAX()` などの基本関数をサポート。
- 🌓 **モダンなUI ＆ ダークモード完全対応**:
  - 目に優しいダークテーマと、清潔感のあるライトテーマをワンクリックで切り替え可能。
- 🔤 **多様な文字コード・改行コード対応**:
  - UTF-8、Shift-JIS、EUC-JP、CRLF/LF を自在に切り替え可能。

---

## 📥 ダウンロードと使い方

### 1. ダウンロード
右側の [**Releases**](https://github.com/777Labs/CellMemo/releases) ページから最新の `CellMemo-v1.0.0-win-x64.zip`（または `CellMemo.exe`）をダウンロードしてください。

### 2. 起動方法
1. ダウンロードした ZIP ファイルをお好みのフォルダーに展開（解凍）します。
2. 展開されたフォルダー内の `CellMemo.exe` をダブルクリックするだけで起動します。
   - ※ レジストリを使用しないポータブル仕様のため、USBメモリなどに入れて持ち運ぶことも可能です。

---

> [!IMPORTANT]
> ### 🛡️ Windows SmartScreen 警告が出た場合
> インターネットから新しくダウンロードしたアプリを初めて起動する際、Windows によって「**Windows によって PC が保護されました**」という青い画面が表示される場合があります。
> 
> これは個人開発のフリーソフトにおいてデジタル署名が未知であるために表示される標準的なセキュリティ確認画面です。以下の手順でそのまま起動できます：
> 1. 画面内の「**詳細情報**」リンクをクリックします。
> 2. 右下に表示される「**実行**」ボタンをクリックします。
> （一度実行を許可すれば、次回以降はこの画面は表示されなくなります）

---

## ⌨️ 主なショートカットキー

| ショートカット | 機能 |
| :--- | :--- |
| `Ctrl + N` | 新しいタブを開く |
| `Ctrl + W` | 現在のタブを閉じる |
| `Ctrl + O` | ファイルを開く |
| `Ctrl + S` | 上書き保存 |
| `Ctrl + Shift + S` | 名前を付けて保存 |
| `Ctrl + Z` / `Ctrl + Y` | 元に戻す / やり直し |
| `Ctrl + X` / `C` / `V` | 切り取り / コピー / 貼り付け |
| `Delete` | 選択セルの内容を消去 |
| `Enter` | セルの編集を開始 / 編集を確定して下のセルへ移動 |
| `Tab` | 編集を確定して右のセルへ移動 |
| `Alt + Enter` | セル内改行 |
| `Ctrl + F` | 検索と置換 |
| `Ctrl + マウスホイール` | 表示倍率の拡大・縮小 |

---

## 💻 動作環境

- **OS**: Windows 10 (64-bit) / Windows 11 (64-bit)
- **ランタイム**: 不要（必要な実行エンジンが exe に内包されているため、追加の .NET インストールは不要です）

---

## 🛠️ ビルド手順

本プロジェクトをソースコードからビルドする場合は、.NET 8 SDK が必要です。

```powershell
# 自己完結型（単一実行ファイル）の発行
dotnet publish .\src\CellMemo\CellMemo.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o .\publish
```

---

## 📄 利用規約・ライセンス

本ソフトウェアは **無料（フリーウェア）** として提供されています。個人利用・商用利用を問わず、どなたでも無償でご利用いただけます。
詳細については [LICENSE](LICENSE) をご確認ください。
