using System;
using System.Collections.Generic;

namespace CellMemo.Services
{
    public class LocalizationService
    {
        private static LocalizationService? _instance;
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        public string CurrentLanguage { get; private set; } = "ja-JP";

        public event EventHandler? LanguageChanged;

        private readonly Dictionary<string, string> _en = new()
        {
            ["Menu.File"] = "File",
            ["Menu.File.NewTab"] = "New Tab",
            ["Menu.File.Open"] = "Open...",
            ["Menu.File.Save"] = "Save",
            ["Menu.File.SaveAs"] = "Save As...",
            ["Menu.File.CloseTab"] = "Close Tab",
            ["Menu.File.Exit"] = "Exit",

            ["Menu.Edit"] = "Edit",
            ["Menu.Edit.Undo"] = "Undo",
            ["Menu.Edit.Redo"] = "Redo",
            ["Menu.Edit.Cut"] = "Cut",
            ["Menu.Edit.Copy"] = "Copy",
            ["Menu.Edit.Paste"] = "Paste",
            ["Menu.Edit.Delete"] = "Clear Contents",
            ["Menu.Edit.InsertRowAbove"] = "Insert Row Above",
            ["Menu.Edit.InsertRowBelow"] = "Insert Row Below",
            ["Menu.Edit.DeleteRow"] = "Delete Row",
            ["Menu.Edit.InsertColLeft"] = "Insert Column Left",
            ["Menu.Edit.InsertColRight"] = "Insert Column Right",
            ["Menu.Edit.DeleteCol"] = "Delete Column",
            ["Menu.Edit.RowHeight"] = "Row Height...",
            ["Menu.Edit.ColWidth"] = "Column Width...",
            ["Menu.Edit.Find"] = "Find & Replace...",

            ["Menu.View"] = "View",
            ["Menu.View.ZoomIn"] = "Zoom In",
            ["Menu.View.ZoomOut"] = "Zoom Out",
            ["Menu.View.ResetZoom"] = "Restore Default Zoom",
            ["Menu.View.ShowFormulaBar"] = "Show Formula Bar",
            ["Menu.View.FreezeTopRow"] = "Freeze Top Row",

            ["Menu.Formulas"] = "Formulas",
            ["Menu.Formulas.Sum"] = "AutoSum (SUM)",
            ["Menu.Formulas.Avg"] = "Average (AVERAGE)",
            ["Menu.Formulas.Count"] = "Count Numbers (COUNT)",
            ["Menu.Formulas.Min"] = "Min (MIN)",
            ["Menu.Formulas.Max"] = "Max (MAX)",
            ["Menu.Formulas.EvaluateToValue"] = "Evaluate Selection to Value",
            ["Menu.Formulas.CopySum"] = "Copy Sum to Clipboard",

            ["Status.Position"] = "Ln {0}, Col {1}",
            ["Status.Characters"] = "{0} characters",
            ["Status.Stats"] = "Sum: {0} | Avg: {1} | Count: {2}",

            ["Settings.Title"] = "Settings",
            ["Settings.Theme"] = "App Theme",
            ["Settings.ThemeLight"] = "Light",
            ["Settings.ThemeDark"] = "Dark",
            ["Settings.Font"] = "Grid Font",
            ["Settings.FontSize"] = "Font Size",
            ["Settings.Language"] = "Language",
            ["Settings.DefaultEncoding"] = "Default Encoding",
            ["Settings.DefaultLineEnding"] = "Default Line Ending",
            ["Settings.SaveFormulasAsRawText"] = "Save Formulas as Raw Text (preserve '=SUM(...)')",
            ["Settings.AutoSaveSession"] = "Auto-save session on exit",
            ["Settings.UseCompactEnglishLabels"] = "Use compact English labels for Menu & Status",
            ["Settings.Save"] = "Save",
            ["Settings.Cancel"] = "Cancel",

            ["Find.Find"] = "Find",
            ["Find.Replace"] = "Replace",
            ["Find.FindNext"] = "Find Next",
            ["Find.FindPrev"] = "Find Prev",
            ["Find.ReplaceOne"] = "Replace",
            ["Find.ReplaceAll"] = "Replace All",
            ["Find.MatchCase"] = "Match Case",
            ["Find.MatchEntireCell"] = "Match Entire Cell",
            ["Find.NotFound"] = "Text not found.",
            ["Find.ReplacedCount"] = "Replaced {0} occurrence(s).",

            ["Dialog.UnsavedPrompt"] = "Do you want to save changes to {0}?",
            ["Dialog.UnsavedTitle"] = "Save Changes",
            ["Dialog.FileLocked"] = "The file is currently locked by another process.\nPlease close it and try again.",
            ["Dialog.ErrorTitle"] = "Error",
            ["Dialog.RowHeight.Title"] = "Row Height",
            ["Dialog.ColWidth.Title"] = "Column Width",
            ["Dialog.Size.Pixel"] = "Height / Width in pixels (px):",
        };

        private readonly Dictionary<string, string> _ja = new()
        {
            ["Menu.File"] = "ファイル",
            ["Menu.File.NewTab"] = "新しいタブ",
            ["Menu.File.Open"] = "開く...",
            ["Menu.File.Save"] = "保存",
            ["Menu.File.SaveAs"] = "名前を付けて保存...",
            ["Menu.File.CloseTab"] = "タブを閉じる",
            ["Menu.File.Exit"] = "終了",

            ["Menu.Edit"] = "編集",
            ["Menu.Edit.Undo"] = "元に戻す",
            ["Menu.Edit.Redo"] = "やり直し",
            ["Menu.Edit.Cut"] = "切り取り",
            ["Menu.Edit.Copy"] = "コピー",
            ["Menu.Edit.Paste"] = "貼り付け",
            ["Menu.Edit.Delete"] = "セルの内容を消去",
            ["Menu.Edit.InsertRowAbove"] = "上に行を挿入",
            ["Menu.Edit.InsertRowBelow"] = "下に行を挿入",
            ["Menu.Edit.DeleteRow"] = "行を削除",
            ["Menu.Edit.InsertColLeft"] = "左に列を挿入",
            ["Menu.Edit.InsertColRight"] = "右に列を挿入",
            ["Menu.Edit.DeleteCol"] = "列を削除",
            ["Menu.Edit.RowHeight"] = "行の高さ...",
            ["Menu.Edit.ColWidth"] = "列の幅...",
            ["Menu.Edit.Find"] = "検索と置換...",

            ["Menu.View"] = "表示",
            ["Menu.View.ZoomIn"] = "拡大",
            ["Menu.View.ZoomOut"] = "縮小",
            ["Menu.View.ResetZoom"] = "既定の倍率に戻す",
            ["Menu.View.ShowFormulaBar"] = "数式バーを表示",
            ["Menu.View.FreezeTopRow"] = "先頭行を固定",

            ["Menu.Formulas"] = "関数",
            ["Menu.Formulas.Sum"] = "合計 (SUM)",
            ["Menu.Formulas.Avg"] = "平均 (AVERAGE)",
            ["Menu.Formulas.Count"] = "数値の個数 (COUNT)",
            ["Menu.Formulas.Min"] = "最小値 (MIN)",
            ["Menu.Formulas.Max"] = "最大値 (MAX)",
            ["Menu.Formulas.EvaluateToValue"] = "選択範囲を値に変換",
            ["Menu.Formulas.CopySum"] = "合計値をクリップボードへコピー",

            ["Status.Position"] = "行 {0}, 列 {1}",
            ["Status.Characters"] = "{0} 文字",
            ["Status.Stats"] = "合計: {0} | 平均: {1} | 個数: {2}",

            ["Settings.Title"] = "設定",
            ["Settings.Theme"] = "アプリのテーマ",
            ["Settings.ThemeLight"] = "ライト",
            ["Settings.ThemeDark"] = "ダーク",
            ["Settings.Font"] = "グリッドフォント",
            ["Settings.FontSize"] = "フォントサイズ",
            ["Settings.Language"] = "表示言語",
            ["Settings.DefaultEncoding"] = "既定の文字コード",
            ["Settings.DefaultLineEnding"] = "既定の改行コード",
            ["Settings.SaveFormulasAsRawText"] = "数式をテキストとして保存 ('=SUM(...)' を保持)",
            ["Settings.AutoSaveSession"] = "終了時にセッションを自動保存",
            ["Settings.UseCompactEnglishLabels"] = "メニューバーとステータスバーを英語表記にする",
            ["Settings.Save"] = "保存",
            ["Settings.Cancel"] = "キャンセル",

            ["Find.Find"] = "検索",
            ["Find.Replace"] = "置換",
            ["Find.FindNext"] = "次を検索",
            ["Find.FindPrev"] = "前を検索",
            ["Find.ReplaceOne"] = "置換",
            ["Find.ReplaceAll"] = "すべて置換",
            ["Find.MatchCase"] = "大文字/小文字を区別",
            ["Find.MatchEntireCell"] = "セル全体と一致",
            ["Find.NotFound"] = "検索テキストが見つかりません。",
            ["Find.ReplacedCount"] = "{0} 件を置換しました。",

            ["Dialog.UnsavedPrompt"] = "{0} への変更を保存しますか？",
            ["Dialog.UnsavedTitle"] = "変更の保存",
            ["Dialog.FileLocked"] = "ファイルが別のプロセスによってロックされています。\n他のアプリを閉じてから再試行してください。",
            ["Dialog.ErrorTitle"] = "エラー",
            ["Dialog.RowHeight.Title"] = "行の高さ",
            ["Dialog.ColWidth.Title"] = "列の幅",
            ["Dialog.Size.Pixel"] = "サイズ (ピクセル / px):",
        };

        public void SetLanguage(string lang)
        {
            CurrentLanguage = lang;
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        public string Get(string key, params object[] args)
        {
            var dict = CurrentLanguage == "ja-JP" ? _ja : _en;
            if (!dict.TryGetValue(key, out string? template))
            {
                if (!_en.TryGetValue(key, out template))
                {
                    template = key;
                }
            }

            if (args != null && args.Length > 0)
            {
                try
                {
                    return string.Format(template, args);
                }
                catch
                {
                    return template;
                }
            }
            return template;
        }
    }
}
