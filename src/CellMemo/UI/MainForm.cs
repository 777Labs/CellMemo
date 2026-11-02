using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CellMemo.Models;
using CellMemo.Native;
using CellMemo.Services;
using CellMemo.UI.Controls;
using CellMemo.UI.Dialogs;

namespace CellMemo.UI
{
    public class MainForm : Form
    {
        private readonly SettingsService _settingsService;
        private readonly LocalizationService _loc = LocalizationService.Instance;

        private ThemePalette _theme;
        private readonly List<SheetDocument> _tabs = new();
        private int _currentTabIndex = 0;

        // UI コントロール群
        private readonly TitleTabBarControl _titleBar;
        private readonly ModernMenuBarControl _menuBar;
        private readonly VirtualGridControl _grid;
        private readonly StatusBarControl _statusBar;
        private readonly FindReplaceBar _findReplaceBar;

        // メニュー項目参照 (動的有効/無効化用)
        private ToolStripMenuItem _menuItemUndo = null!;
        private ToolStripMenuItem _menuItemRedo = null!;
        private ToolStripMenuItem _menuItemCut = null!;
        private ToolStripMenuItem _menuItemCopy = null!;
        private ToolStripMenuItem _menuItemPaste = null!;
        private ToolStripMenuItem _menuItemDelete = null!;
        private ToolStripMenuItem _menuItemInsertRowAbove = null!;
        private ToolStripMenuItem _menuItemInsertRowBelow = null!;
        private ToolStripMenuItem _menuItemDeleteRow = null!;
        private ToolStripMenuItem _menuItemInsertColLeft = null!;
        private ToolStripMenuItem _menuItemInsertColRight = null!;
        private ToolStripMenuItem _menuItemDeleteCol = null!;
        private ToolStripMenuItem _menuItemRowHeight = null!;
        private ToolStripMenuItem _menuItemColWidth = null!;

        public MainForm(SettingsService settingsService)
        {
            _settingsService = settingsService;
            var settings = _settingsService.CurrentSettings;

            // ローカライズの初期設定
            _loc.SetLanguage(settings.Language);

            // テーマパレット初期化
            _theme = new ThemePalette(settings.Theme == "Dark", settings.FontFamily, settings.FontSize);

            // コントロールの初期化（Bounds設定より前に行う）
            _titleBar = new TitleTabBarControl(_theme);
            _menuBar = new ModernMenuBarControl(_theme);
            _grid = new VirtualGridControl(_theme);
            _statusBar = new StatusBarControl(_theme);
            _findReplaceBar = new FindReplaceBar(_theme) { Visible = false };

            // フォーム基本設定
            Text = "Cell Memo";
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(680, 440);
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = true;
            SetStyle(ControlStyles.ResizeRedraw, true);

            // ウィンドウサイズ復元（画面外防止クランプ）
            var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            var b = settings.WindowBounds;
            int initialW = Math.Clamp(b.Width > 0 ? b.Width : 1100, 800, workingArea.Width);
            int initialH = Math.Clamp(b.Height > 0 ? b.Height : 700, 500, workingArea.Height);
            int initialX = Math.Clamp(b.X, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - initialW));
            int initialY = Math.Clamp(b.Y, workingArea.Top, Math.Max(workingArea.Top, workingArea.Bottom - initialH));

            Bounds = new Rectangle(initialX, initialY, initialW, initialH);
            if (b.IsMaximized)
            {
                WindowState = FormWindowState.Maximized;
            }

            Controls.Add(_findReplaceBar);
            Controls.Add(_grid);
            Controls.Add(_statusBar);
            Controls.Add(_menuBar);
            Controls.Add(_titleBar);

            UpdateLayoutPositions();

            // イベントバインド
            WireEvents();

            // アプリアイコンロード
            ApplyApplicationIcon();
        }

        private void WireEvents()
        {
            // タイトルバーイベント
            _titleBar.TabSelected += (s, idx) => SwitchToTab(idx);
            _titleBar.TabCloseRequested += (s, idx) => CloseTab(idx);
            _titleBar.NewTabRequested += (s, e) => AddNewTab();
            _titleBar.TabTitleDoubleClicked += (s, idx) => RenameTabPrompt(idx);

            // メニューバーイベント
            SetupMenuItems();
            _menuBar.SettingsClicked += (s, e) => OpenSettingsDialog();

            // グリッドイベント
            _grid.CutRequested += (s, e) => DoCut();
            _grid.CopyRequested += (s, e) => DoCopy();
            _grid.PasteRequested += (s, e) => DoPaste();
            _grid.ActiveCellChanged += (s, addr) =>
            {
                _statusBar.UpdatePosition(addr);
                UpdateStatusStats();
            };
            _grid.SelectionChanged += (s, e) => UpdateStatusStats();
            _grid.ZoomChanged += (s, zoom) => _statusBar.UpdateZoom(zoom);

            // ステータスバーイベント
            _statusBar.ZoomChanged += (s, zoom) => _grid.Zoom = zoom / 100f;
            _statusBar.LineEndingChanged += (s, le) =>
            {
                var doc = CurrentDocument;
                if (doc != null)
                {
                    doc.LineEnding = le;
                    doc.IsDirty = true;
                    _statusBar.UpdateLineEnding(le);
                    _titleBar.Invalidate();
                }
            };
            _statusBar.EncodingChanged += (s, encName) =>
            {
                var doc = CurrentDocument;
                if (doc != null)
                {
                    doc.Encoding = encName switch
                    {
                        "Shift_JIS" => Encoding.GetEncoding(932),
                        "UTF-16 LE" => Encoding.Unicode,
                        "UTF-16 BE" => Encoding.BigEndianUnicode,
                        "UTF-8 BOM" => new UTF8Encoding(true),
                        _ => new UTF8Encoding(false)
                    };
                    doc.HasBom = encName == "UTF-8 BOM";
                    doc.IsDirty = true;
                    _statusBar.UpdateEncoding(encName, doc.HasBom);
                    _titleBar.Invalidate();
                }
            };

            // 検索・置換バーイベント
            _findReplaceBar.FindRequested += (s, req) => DoFind(req.text, req.forward, req.matchCase, req.entireCell);
            _findReplaceBar.ReplaceOneRequested += (s, req) => DoReplace(req.findText, req.replaceText, req.matchCase, req.entireCell, all: false);
            _findReplaceBar.ReplaceAllRequested += (s, req) => DoReplace(req.findText, req.replaceText, req.matchCase, req.entireCell, all: true);
            _findReplaceBar.CloseRequested += (s, e) => _findReplaceBar.Visible = false;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style |= 0x00040000; // WS_THICKFRAME (WS_SIZEBOX)
                cp.Style |= 0x00020000; // WS_MINIMIZEBOX
                cp.Style |= 0x00010000; // WS_MAXIMIZEBOX
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        private void UpdateLayoutPositions()
        {
            if (_titleBar == null || _menuBar == null || _grid == null || _statusBar == null || _findReplaceBar == null)
                return;

            int pad = WindowState == FormWindowState.Maximized ? 8 : 0;
            int w = Math.Max(0, ClientSize.Width - pad * 2);
            int h = Math.Max(0, ClientSize.Height - pad * 2);

            // Zone 1: Title & Tab Bar (40px)
            _titleBar.Bounds = new Rectangle(pad, pad, w, 40);

            // Zone 2: Menu Bar (32px)
            _menuBar.Bounds = new Rectangle(pad, pad + 40, w, 32);

            // Zone 4: Status Bar (28px)
            _statusBar.Bounds = new Rectangle(pad, pad + Math.Max(72, h - 28), w, 28);

            // Zone 3: Grid (Variable)
            int gridY = pad + 72;
            int gridH = Math.Max(0, h - 28 - 72);
            _grid.Bounds = new Rectangle(pad, gridY, w, gridH);

            // Find & Replace Bar
            if (_findReplaceBar.Visible)
            {
                _findReplaceBar.Location = new Point(Math.Max(10, pad + w - _findReplaceBar.Width - 20), pad + 80);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateLayoutPositions();
        }

        private void SetupMenuItems()
        {
            var loc = _loc;

            // File メニュー
            AddMenuItem(_menuBar.FileMenu, loc.Get("Menu.File.NewTab"), Keys.Control | Keys.N, (s, e) => AddNewTab());
            AddMenuItem(_menuBar.FileMenu, loc.Get("Menu.File.Open"), Keys.Control | Keys.O, async (s, e) => await OpenFileDialogAsync());
            AddMenuItem(_menuBar.FileMenu, loc.Get("Menu.File.Save"), Keys.Control | Keys.S, async (s, e) => await SaveCurrentTabAsync(saveAs: false));
            AddMenuItem(_menuBar.FileMenu, loc.Get("Menu.File.SaveAs"), Keys.Control | Keys.Shift | Keys.S, async (s, e) => await SaveCurrentTabAsync(saveAs: true));
            _menuBar.FileMenu.DropDownItems.Add(new ToolStripSeparator());
            AddMenuItem(_menuBar.FileMenu, loc.Get("Menu.File.CloseTab"), Keys.Control | Keys.W, (s, e) => CloseTab(_currentTabIndex));
            AddMenuItem(_menuBar.FileMenu, loc.Get("Menu.File.Exit"), Keys.Alt | Keys.F4, (s, e) => Close());

            // Edit メニュー
            _menuItemUndo = AddMenuItem(_menuBar.EditMenu, loc.Get("Menu.Edit.Undo"), Keys.Control | Keys.Z, (s, e) => CurrentDocument?.UndoRedo.Undo(CurrentDocument));
            _menuItemRedo = AddMenuItem(_menuBar.EditMenu, loc.Get("Menu.Edit.Redo"), Keys.Control | Keys.Y, (s, e) => CurrentDocument?.UndoRedo.Redo(CurrentDocument));
            _menuBar.EditMenu.DropDownItems.Add(new ToolStripSeparator());
            _menuItemCut = AddMenuItem(_menuBar.EditMenu, loc.Get("Menu.Edit.Cut"), Keys.Control | Keys.X, (s, e) => DoCut());
            _menuItemCopy = AddMenuItem(_menuBar.EditMenu, loc.Get("Menu.Edit.Copy"), Keys.Control | Keys.C, (s, e) => DoCopy());
            _menuItemPaste = AddMenuItem(_menuBar.EditMenu, loc.Get("Menu.Edit.Paste"), Keys.Control | Keys.V, (s, e) => DoPaste());
            _menuItemDelete = AddMenuItem(_menuBar.EditMenu, loc.Get("Menu.Edit.Delete"), Keys.Delete, (s, e) => CurrentDocument?.ClearCells(_grid.GetSelectedCells()));
            _menuBar.EditMenu.DropDownItems.Add(new ToolStripSeparator());
            _menuItemInsertRowAbove = AddMenuItem(_menuBar.EditMenu, loc.Get("Menu.Edit.InsertRowAbove"), Keys.None, (s, e) => _grid.InsertRowAbove());
            _menuItemInsertRowBelow = AddMenuItem(_menuBar.EditMenu, loc.Get("Menu.Edit.InsertRowBelow"), Keys.None, (s, e) => _grid.InsertRowBelow());
            _menuItemDeleteRow = AddMenuItem(_menuBar.EditMenu, loc.Get("Menu.Edit.DeleteRow"), Keys.None, (s, e) => _grid.DeleteSelectedRows());
            _menuItemInsertColLeft = AddMenuItem(_menuBar.EditMenu, loc.Get("Menu.Edit.InsertColLeft"), Keys.None, (s, e) => _grid.InsertColLeft());
            _menuItemInsertColRight = AddMenuItem(_menuBar.EditMenu, loc.Get("Menu.Edit.InsertColRight"), Keys.None, (s, e) => _grid.InsertColRight());
            _menuItemDeleteCol = AddMenuItem(_menuBar.EditMenu, loc.Get("Menu.Edit.DeleteCol"), Keys.None, (s, e) => _grid.DeleteSelectedColumns());
            _menuBar.EditMenu.DropDownItems.Add(new ToolStripSeparator());
            _menuItemRowHeight = AddMenuItem(_menuBar.EditMenu, loc.Get("Menu.Edit.RowHeight"), Keys.None, (s, e) => _grid.PromptSetRowHeight());
            _menuItemColWidth = AddMenuItem(_menuBar.EditMenu, loc.Get("Menu.Edit.ColWidth"), Keys.None, (s, e) => _grid.PromptSetColumnWidth());
            _menuBar.EditMenu.DropDownItems.Add(new ToolStripSeparator());
            AddMenuItem(_menuBar.EditMenu, loc.Get("Menu.Edit.Find"), Keys.Control | Keys.F, (s, e) => ShowFindReplace(replace: false));

            _menuBar.EditMenu.DropDownOpening += (s, e) => UpdateEditMenuState();

            // View メニュー
            AddMenuItem(_menuBar.ViewMenu, loc.Get("Menu.View.ZoomIn"), Keys.Control | Keys.Oemplus, (s, e) => _grid.Zoom += 0.1f);
            AddMenuItem(_menuBar.ViewMenu, loc.Get("Menu.View.ZoomOut"), Keys.Control | Keys.OemMinus, (s, e) => _grid.Zoom -= 0.1f);
            AddMenuItem(_menuBar.ViewMenu, loc.Get("Menu.View.ResetZoom"), Keys.Control | Keys.D0, (s, e) => _grid.Zoom = 1.0f);
            _menuBar.ViewMenu.DropDownItems.Add(new ToolStripSeparator());
            var freezeItem = AddMenuItem(_menuBar.ViewMenu, loc.Get("Menu.View.FreezeTopRow"), Keys.None, (s, e) =>
            {
                _grid.FreezeTopRow = !_grid.FreezeTopRow;
                if (s is ToolStripMenuItem mi) mi.Checked = _grid.FreezeTopRow;
                _grid.Invalidate();
            });

            // Formulas メニュー
            AddMenuItem(_menuBar.FormulasMenu, loc.Get("Menu.Formulas.Sum"), Keys.Alt | Keys.Shift | Keys.Oemplus, (s, e) => InsertAutoFormula("SUM"));
            AddMenuItem(_menuBar.FormulasMenu, loc.Get("Menu.Formulas.Avg"), Keys.None, (s, e) => InsertAutoFormula("AVERAGE"));
            AddMenuItem(_menuBar.FormulasMenu, loc.Get("Menu.Formulas.Count"), Keys.None, (s, e) => InsertAutoFormula("COUNT"));
            AddMenuItem(_menuBar.FormulasMenu, loc.Get("Menu.Formulas.Min"), Keys.None, (s, e) => InsertAutoFormula("MIN"));
            AddMenuItem(_menuBar.FormulasMenu, loc.Get("Menu.Formulas.Max"), Keys.None, (s, e) => InsertAutoFormula("MAX"));
            _menuBar.FormulasMenu.DropDownItems.Add(new ToolStripSeparator());
            AddMenuItem(_menuBar.FormulasMenu, loc.Get("Menu.Formulas.EvaluateToValue"), Keys.None, (s, e) => CurrentDocument?.EvaluateSelectionToValue(_grid.GetSelectedCells()));
            AddMenuItem(_menuBar.FormulasMenu, loc.Get("Menu.Formulas.CopySum"), Keys.None, (s, e) => CopySumToClipboard());
        }

        private void UpdateEditMenuState()
        {
            var selType = _grid.CurrentSelectionType;
            if (selType == VirtualGridControl.GridSelectionType.Rows)
            {
                _menuItemInsertRowAbove.Enabled = true;
                _menuItemInsertRowBelow.Enabled = true;
                _menuItemDeleteRow.Enabled = true;
                _menuItemInsertColLeft.Enabled = false;
                _menuItemInsertColRight.Enabled = false;
                _menuItemDeleteCol.Enabled = false;
                _menuItemRowHeight.Enabled = true;
                _menuItemColWidth.Enabled = false;
            }
            else if (selType == VirtualGridControl.GridSelectionType.Columns)
            {
                _menuItemInsertRowAbove.Enabled = false;
                _menuItemInsertRowBelow.Enabled = false;
                _menuItemDeleteRow.Enabled = false;
                _menuItemInsertColLeft.Enabled = true;
                _menuItemInsertColRight.Enabled = true;
                _menuItemDeleteCol.Enabled = true;
                _menuItemRowHeight.Enabled = false;
                _menuItemColWidth.Enabled = true;
            }
            else if (selType == VirtualGridControl.GridSelectionType.All)
            {
                _menuItemInsertRowAbove.Enabled = false;
                _menuItemInsertRowBelow.Enabled = false;
                _menuItemDeleteRow.Enabled = false;
                _menuItemInsertColLeft.Enabled = false;
                _menuItemInsertColRight.Enabled = false;
                _menuItemDeleteCol.Enabled = false;
                _menuItemRowHeight.Enabled = false;
                _menuItemColWidth.Enabled = false;
            }
            else
            {
                // 通常セル選択時：行・列どちらの挿入・削除・サイズ変更も可能
                _menuItemInsertRowAbove.Enabled = true;
                _menuItemInsertRowBelow.Enabled = true;
                _menuItemDeleteRow.Enabled = true;
                _menuItemInsertColLeft.Enabled = true;
                _menuItemInsertColRight.Enabled = true;
                _menuItemDeleteCol.Enabled = true;
                _menuItemRowHeight.Enabled = true;
                _menuItemColWidth.Enabled = true;
            }

            _menuItemUndo.Enabled = CurrentDocument?.UndoRedo.CanUndo ?? false;
            _menuItemRedo.Enabled = CurrentDocument?.UndoRedo.CanRedo ?? false;
        }

        private static ToolStripMenuItem AddMenuItem(ToolStripMenuItem parent, string text, Keys shortcut, EventHandler onClick)
        {
            var item = new ToolStripMenuItem(text, null, onClick);
            if (shortcut != Keys.None)
            {
                item.ShortcutKeys = shortcut;
            }
            parent.DropDownItems.Add(item);
            return item;
        }

        private SheetDocument? CurrentDocument => (_currentTabIndex >= 0 && _currentTabIndex < _tabs.Count) ? _tabs[_currentTabIndex] : null;

        public void AddNewTab(string? title = null)
        {
            title ??= $"Memo{_tabs.Count + 1}";
            var doc = new SheetDocument(title);
            _tabs.Add(doc);
            SwitchToTab(_tabs.Count - 1);
        }

        public void SwitchToTab(int index)
        {
            if (index < 0 || index >= _tabs.Count) return;

            _currentTabIndex = index;
            var doc = _tabs[index];
            _grid.SetDocument(doc);
            _titleBar.SetTabs(_tabs, _currentTabIndex);

            _statusBar.UpdatePosition(_grid.ActiveCell);
            _statusBar.UpdateLineEnding(doc.LineEnding);
            _statusBar.UpdateEncoding(doc.Encoding.WebName, doc.HasBom);
            UpdateStatusStats();
        }

        public void CloseTab(int index)
        {
            if (index < 0 || index >= _tabs.Count) return;

            var doc = _tabs[index];
            if (doc.IsDirty)
            {
                var dr = MessageBox.Show(
                    _loc.Get("Dialog.UnsavedPrompt", doc.Title),
                    _loc.Get("Dialog.UnsavedTitle"),
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Warning);

                if (dr == DialogResult.Cancel) return;
                if (dr == DialogResult.Yes)
                {
                    // 同期的に保存
                    SaveCurrentTabAsync(saveAs: false).GetAwaiter().GetResult();
                }
            }

            _tabs.RemoveAt(index);
            if (_tabs.Count == 0)
            {
                AddNewTab();
            }
            else
            {
                int nextIndex = Math.Clamp(index, 0, _tabs.Count - 1);
                SwitchToTab(nextIndex);
            }
        }

        private void RenameTabPrompt(int index)
        {
            if (index < 0 || index >= _tabs.Count) return;
            var doc = _tabs[index];
            string? input = InputDialog.Show(this, "Enter new tab name:", "Rename Tab", doc.Title, _theme);
            if (!string.IsNullOrWhiteSpace(input))
            {
                doc.Title = input.Trim();
                _titleBar.Invalidate();
            }
        }

        private void UpdateStatusStats()
        {
            var doc = CurrentDocument;
            if (doc == null) return;

            var cells = _grid.GetSelectedCells();
            if (cells.Count <= 1)
            {
                var cell = doc.GetCell(_grid.ActiveCell);
                int len = cell?.EvaluatedText.Length ?? 0;
                _statusBar.UpdateSingleCellStats(len);
            }
            else
            {
                double sum = 0;
                int numCount = 0;
                int totalChars = 0;

                foreach (var addr in cells)
                {
                    var cell = doc.GetCell(addr);
                    if (cell != null)
                    {
                        totalChars += cell.EvaluatedText.Length;
                        if (cell.NumericValue.HasValue)
                        {
                            sum += cell.NumericValue.Value;
                            numCount++;
                        }
                    }
                }

                double avg = numCount > 0 ? (sum / numCount) : 0;
                _statusBar.UpdateRangeStats(sum, avg, numCount, totalChars);
            }
        }

        // ==========================================
        // ファイル I/O
        // ==========================================
        public async Task OpenFileAsync(string filePath)
        {
            // すでに開いているか確認
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (string.Equals(_tabs[i].FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    SwitchToTab(i);
                    return;
                }
            }

            try
            {
                var result = await CsvTsvSerializer.ReadFileAsync(filePath);
                var doc = new SheetDocument();
                doc.LoadDataFromParsedMatrix(result.Cells, filePath, result.Encoding, result.LineEnding, result.HasBom);

                // もし現在のタブが「未編集かつ初期空タブ」なら差し替える
                if (_tabs.Count == 1 && !_tabs[0].IsDirty && _tabs[0].FilePath == null && _tabs[0].Cells.Count == 0)
                {
                    _tabs[0] = doc;
                    SwitchToTab(0);
                }
                else
                {
                    _tabs.Add(doc);
                    SwitchToTab(_tabs.Count - 1);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, _loc.Get("Dialog.ErrorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task OpenFileDialogAsync()
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Supported Files (*.csv;*.tsv;*.txt)|*.csv;*.tsv;*.txt|All Files (*.*)|*.*",
                Title = _loc.Get("Menu.File.Open")
            };
            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                await OpenFileAsync(ofd.FileName);
            }
        }

        private async Task SaveCurrentTabAsync(bool saveAs)
        {
            var doc = CurrentDocument;
            if (doc == null) return;

            string? targetPath = doc.FilePath;
            if (saveAs || string.IsNullOrEmpty(targetPath))
            {
                using var sfd = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv|TSV Files (*.tsv)|*.tsv|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    FileName = doc.Title.EndsWith(".csv") || doc.Title.EndsWith(".tsv") || doc.Title.EndsWith(".txt")
                        ? doc.Title
                        : $"{doc.Title}.csv"
                };
                if (sfd.ShowDialog(this) != DialogResult.OK)
                    return;

                targetPath = sfd.FileName;
            }

            try
            {
                await CsvTsvSerializer.SaveFileAsync(doc, targetPath, _settingsService.CurrentSettings.SaveFormulasAsRawText);
                doc.FilePath = targetPath;
                doc.Title = Path.GetFileName(targetPath);
                doc.IsDirty = false;
                _titleBar.Invalidate();
            }
            catch (IOException)
            {
                MessageBox.Show(_loc.Get("Dialog.FileLocked"), _loc.Get("Dialog.ErrorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, _loc.Get("Dialog.ErrorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ==========================================
        // クリップボード操作
        // ==========================================
        private void DoCopy()
        {
            var doc = CurrentDocument;
            if (doc == null) return;

            var (start, end) = _grid.GetNormalizedSelection();
            var sb = new StringBuilder();

            for (int r = start.Row; r <= end.Row; r++)
            {
                for (int c = start.Col; c <= end.Col; c++)
                {
                    string text = doc.GetCellText(new CellAddress(r, c));
                    sb.Append(CsvTsvSerializer.EscapeField(text, '\t'));
                    if (c < end.Col) sb.Append('\t');
                }
                sb.AppendLine();
            }

            Clipboard.SetText(sb.ToString());
        }

        private void DoCut()
        {
            DoCopy();
            CurrentDocument?.ClearCells(_grid.GetSelectedCells());
        }

        private void DoPaste()
        {
            if (!Clipboard.ContainsText() || CurrentDocument == null) return;

            string text = Clipboard.GetText();
            char delimiter = text.Contains('\t') ? '\t' : ',';
            var parsed = CsvTsvSerializer.ParseDelimitedContent(text, delimiter);

            int baseRow = _grid.ActiveCell.Row;
            int baseCol = _grid.ActiveCell.Col;

            var changes = new Dictionary<CellAddress, string>();
            foreach (var kvp in parsed.Cells)
            {
                int r = baseRow + kvp.Key.Row - 1;
                int c = baseCol + kvp.Key.Col - 1;
                changes[new CellAddress(r, c)] = kvp.Value.RawText;
            }

            if (changes.Count > 0)
            {
                CurrentDocument.SetCellsRange(changes, "Paste");
            }
        }

        // ==========================================
        // 数式自動挿入
        // ==========================================
        private void InsertAutoFormula(string funcName)
        {
            var doc = CurrentDocument;
            if (doc == null) return;

            var (start, end) = _grid.GetNormalizedSelection();

            if (start != end)
            {
                // 複数選択の場合: 直下の空セルに挿入
                int targetRow = end.Row + 1;
                string rangeStr = $"{start.FormatAddress()}:{end.FormatAddress()}";
                string formula = $"={funcName}({rangeStr})";
                doc.SetCell(new CellAddress(targetRow, start.Col), formula);
                _grid.ActiveCell = new CellAddress(targetRow, start.Col);
            }
            else
            {
                // 単一空セル選択: 上方向に連続する数値セルを自動探索
                int r = start.Row - 1;
                int foundStartRow = -1;
                while (r >= 1)
                {
                    var cell = doc.GetCell(new CellAddress(r, start.Col));
                    if (cell != null && cell.NumericValue.HasValue)
                    {
                        foundStartRow = r;
                        r--;
                    }
                    else
                    {
                        break;
                    }
                }

                if (foundStartRow != -1)
                {
                    string rangeStr = $"{new CellAddress(foundStartRow, start.Col)}:{new CellAddress(start.Row - 1, start.Col)}";
                    string formula = $"={funcName}({rangeStr})";
                    doc.SetCell(start, formula);
                }
                else
                {
                    // 上になければ左を探索
                    int c = start.Col - 1;
                    int foundStartCol = -1;
                    while (c >= 1)
                    {
                        var cell = doc.GetCell(new CellAddress(start.Row, c));
                        if (cell != null && cell.NumericValue.HasValue)
                        {
                            foundStartCol = c;
                            c--;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (foundStartCol != -1)
                    {
                        string rangeStr = $"{new CellAddress(start.Row, foundStartCol)}:{new CellAddress(start.Row, start.Col - 1)}";
                        string formula = $"={funcName}({rangeStr})";
                        doc.SetCell(start, formula);
                    }
                    else
                    {
                        doc.SetCell(start, $"={funcName}()");
                    }
                }
            }
        }

        private void CopySumToClipboard()
        {
            var doc = CurrentDocument;
            if (doc == null) return;

            double sum = 0;
            foreach (var addr in _grid.GetSelectedCells())
            {
                var cell = doc.GetCell(addr);
                if (cell != null && cell.NumericValue.HasValue)
                {
                    sum += cell.NumericValue.Value;
                }
            }
            Clipboard.SetText(sum.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        // ==========================================
        // 検索・置換
        // ==========================================
        private void ShowFindReplace(bool replace)
        {
            _findReplaceBar.Location = new Point(Width - _findReplaceBar.Width - 20, _titleBar.Height + _menuBar.Height + 10);
            _findReplaceBar.ShowBar(replace);
        }

        private void DoFind(string query, bool forward, bool matchCase, bool entireCell)
        {
            var doc = CurrentDocument;
            if (doc == null || string.IsNullOrEmpty(query)) return;

            var (mr, mc) = doc.GetDimensions();
            int maxRow = Math.Max(100, mr);
            int maxCol = Math.Max(26, mc);

            int startR = _grid.ActiveCell.Row;
            int startC = _grid.ActiveCell.Col;

            StringComparison comp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            int totalCells = maxRow * maxCol;
            for (int step = 1; step <= totalCells; step++)
            {
                if (forward)
                {
                    startC++;
                    if (startC > maxCol) { startC = 1; startR++; if (startR > maxRow) startR = 1; }
                }
                else
                {
                    startC--;
                    if (startC < 1) { startC = maxCol; startR--; if (startR < 1) startR = maxRow; }
                }

                var addr = new CellAddress(startR, startC);
                string text = doc.GetCellText(addr);

                bool isMatch = entireCell ? string.Equals(text, query, comp) : (text.IndexOf(query, comp) >= 0);
                if (isMatch)
                {
                    _grid.ActiveCell = addr;
                    _findReplaceBar.SetStatusMessage("");
                    return;
                }
            }

            _findReplaceBar.SetStatusMessage(_loc.Get("Find.NotFound"));
        }

        private void DoReplace(string findText, string replaceText, bool matchCase, bool entireCell, bool all)
        {
            var doc = CurrentDocument;
            if (doc == null || string.IsNullOrEmpty(findText)) return;

            StringComparison comp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            if (all)
            {
                int count = 0;
                var changes = new Dictionary<CellAddress, string>();
                foreach (var kvp in doc.Cells)
                {
                    string raw = kvp.Value.RawText;
                    if (entireCell)
                    {
                        if (string.Equals(raw, findText, comp))
                        {
                            changes[kvp.Key] = replaceText;
                            count++;
                        }
                    }
                    else
                    {
                        if (raw.IndexOf(findText, comp) >= 0)
                        {
                            changes[kvp.Key] = raw.Replace(findText, replaceText, comp);
                            count++;
                        }
                    }
                }
                if (changes.Count > 0)
                {
                    doc.SetCellsRange(changes, "Replace All");
                }
                _findReplaceBar.SetStatusMessage(_loc.Get("Find.ReplacedCount", count));
            }
            else
            {
                var curAddr = _grid.ActiveCell;
                string raw = doc.GetCellRawText(curAddr);
                bool isMatch = entireCell ? string.Equals(raw, findText, comp) : (raw.IndexOf(findText, comp) >= 0);
                if (isMatch)
                {
                    string newText = entireCell ? replaceText : raw.Replace(findText, replaceText, comp);
                    doc.SetCell(curAddr, newText);
                    DoFind(findText, forward: true, matchCase, entireCell);
                }
                else
                {
                    DoFind(findText, forward: true, matchCase, entireCell);
                }
            }
        }

        // ==========================================
        // 設定ダイアログ
        // ==========================================
        private void OpenSettingsDialog()
        {
            using var dlg = new SettingsDialog(_settingsService.CurrentSettings, _theme);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _ = _settingsService.SaveSettingsAsync();

                if (dlg.IsLanguageChanged)
                {
                    _loc.SetLanguage(_settingsService.CurrentSettings.Language);
                    _menuBar.UpdateLabels();
                    _findReplaceBar.UpdateLabels();
                }

                if (dlg.IsThemeChanged || dlg.IsFontChanged)
                {
                    ApplyNewTheme(_settingsService.CurrentSettings.Theme == "Dark",
                        _settingsService.CurrentSettings.FontFamily,
                        _settingsService.CurrentSettings.FontSize);
                }
            }
        }

        private void ApplyNewTheme(bool isDark, string fontFamily, float fontSize)
        {
            var oldTheme = _theme;
            _theme = new ThemePalette(isDark, fontFamily, fontSize);

            _titleBar.SetTheme(_theme);
            _menuBar.SetTheme(_theme);
            _grid.SetTheme(_theme);
            _statusBar.SetTheme(_theme);
            _findReplaceBar.SetTheme(_theme);

            // Win32 ダークモード属性更新
            Win32Api.SetDarkMode(Handle, isDark);

            oldTheme.Dispose();
            Invalidate(true);
        }

        // ==========================================
        // アイコン適用 & Win32ウィンドウ制御
        // ==========================================
        private void ApplyApplicationIcon()
        {
            try
            {
                var assembly = typeof(MainForm).Assembly;
                using Stream? iconStream = assembly.GetManifestResourceStream("CellMemo.icon.ico");
                if (iconStream != null)
                {
                    using var ms = new MemoryStream();
                    iconStream.CopyTo(ms);

                    ms.Position = 0;
                    this.Icon = new Icon(ms);

                    using var bmp = this.Icon.ToBitmap();
                    var scaledBmp = new Bitmap(20, 20);
                    using (var g = Graphics.FromImage(scaledBmp))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(bmp, 0, 0, 20, 20);
                    }
                    _titleBar.SetAppIcon(scaledBmp);
                }
                else if (File.Exists(Application.ExecutablePath))
                {
                    this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                    if (this.Icon != null)
                    {
                        using var bmp = this.Icon.ToBitmap();
                        var scaledBmp = new Bitmap(20, 20);
                        using (var g = Graphics.FromImage(scaledBmp))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.DrawImage(bmp, 0, 0, 20, 20);
                        }
                        _titleBar.SetAppIcon(scaledBmp);
                    }
                }
            }
            catch (Exception ex)
            {
                _settingsService.LogError("ApplyApplicationIcon failed", ex);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Win32Api.SetWindowCorner(Handle, Win32Api.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND);
            Win32Api.SetDarkMode(Handle, _theme.IsDark);

            if (this.Icon != null)
            {
                Win32Api.SendMessage(Handle, Win32Api.WM_SETICON, (IntPtr)1, this.Icon.Handle);
                Win32Api.SendMessage(Handle, Win32Api.WM_SETICON, (IntPtr)0, this.Icon.Handle);
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCCALCSIZE = 0x0083;
            if (m.Msg == WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
            {
                // 非クライアント領域（OS標準タイトルバーや枠線）を除去し、全クライアント領域化
                m.Result = IntPtr.Zero;
                return;
            }

            // ウィンドウリサイズ用グリップ処理 & タイトルバードラッグ処理 (WM_NCHITTEST)
            if (m.Msg == Win32Api.WM_NCHITTEST)
            {
                base.WndProc(ref m);

                int screenX = unchecked((short)(long)m.LParam);
                int screenY = unchecked((short)((long)m.LParam >> 16));
                Point pt = PointToClient(new Point(screenX, screenY));

                // 通常ウィンドウ時は外周境界でのリサイズ判定を最優先
                if (WindowState == FormWindowState.Normal)
                {
                    int border = 8;
                    int corner = 14;

                    bool left = pt.X <= border;
                    bool right = pt.X >= ClientSize.Width - border;
                    bool top = pt.Y <= border;
                    bool bottom = pt.Y >= ClientSize.Height - border;

                    bool cornerLeft = pt.X <= corner;
                    bool cornerRight = pt.X >= ClientSize.Width - corner;
                    bool cornerTop = pt.Y <= corner;
                    bool cornerBottom = pt.Y >= ClientSize.Height - corner;

                    if (cornerTop && cornerLeft) { m.Result = (IntPtr)Win32Api.HTTOPLEFT; return; }
                    if (cornerTop && cornerRight) { m.Result = (IntPtr)Win32Api.HTTOPRIGHT; return; }
                    if (cornerBottom && cornerLeft) { m.Result = (IntPtr)Win32Api.HTBOTTOMLEFT; return; }
                    if (cornerBottom && cornerRight) { m.Result = (IntPtr)Win32Api.HTBOTTOMRIGHT; return; }
                    if (left) { m.Result = (IntPtr)Win32Api.HTLEFT; return; }
                    if (right) { m.Result = (IntPtr)Win32Api.HTRIGHT; return; }
                    if (top) { m.Result = (IntPtr)Win32Api.HTTOP; return; }
                    if (bottom) { m.Result = (IntPtr)Win32Api.HTBOTTOM; return; }
                }

                // リサイズ境界外で、かつ上部タイトルバー領域 (Y <= 40) であれば HTCAPTION を返す
                // これにより、Windows OSネイティブなウィンドウ全体の移動および Win11 スナップレイアウトが発動する
                if (pt.Y >= 0 && pt.Y <= 40)
                {
                    m.Result = (IntPtr)Win32Api.HTCAPTION;
                    return;
                }

                return;
            }

            base.WndProc(ref m);
        }

        // ==========================================
        // IPC アクティベーション & 引数処理
        // ==========================================
        public void HandleIpcActivation(IpcPayload payload)
        {
            BeginInvoke(new Action(async () =>
            {
                if (WindowState == FormWindowState.Minimized)
                {
                    Win32Api.ShowWindow(Handle, Win32Api.SW_RESTORE);
                }
                Win32Api.SetForegroundWindow(Handle);
                Win32Api.BringWindowToTop(Handle);

                if (payload.FilePaths != null && payload.FilePaths.Length > 0)
                {
                    foreach (var path in payload.FilePaths)
                    {
                        if (File.Exists(path))
                        {
                            await OpenFileAsync(path);
                        }
                    }
                }
            }));
        }

        // ==========================================
        // セッション復元 & 終了時保存
        // ==========================================
        public void RestoreSessionSync()
        {
            if (!_settingsService.CurrentSettings.AutoSaveSession)
            {
                AddNewTab();
                return;
            }

            var (manifest, tabList) = _settingsService.LoadSessionSync();
            if (manifest != null && tabList.Count > 0)
            {
                _tabs.Clear();
                foreach (var tabData in tabList)
                {
                    var doc = new SheetDocument(tabData.Title)
                    {
                        Id = Guid.TryParse(tabData.Id, out var gid) ? gid : Guid.NewGuid(),
                        FilePath = tabData.FilePath,
                        IsDirty = tabData.IsDirty,
                        LineEnding = tabData.LineEnding,
                        HasBom = tabData.HasBom
                    };

                    foreach (var kvp in tabData.Cells)
                    {
                        if (CellAddress.TryParse(kvp.Key, out var addr))
                        {
                            doc.SetCellInternal(addr, new CellData(kvp.Value));
                        }
                    }

                    foreach (var cw in tabData.ColumnWidths) doc.CustomColumnWidths[cw.Key] = cw.Value;
                    foreach (var rh in tabData.RowHeights) doc.CustomRowHeights[rh.Key] = rh.Value;

                    doc.RecalculateAllFormulas();
                    _tabs.Add(doc);
                }

                int active = Math.Clamp(manifest.ActiveTabIndex, 0, _tabs.Count - 1);
                SwitchToTab(active);
            }
            else
            {
                AddNewTab();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            // 未保存タブの確認
            for (int i = 0; i < _tabs.Count; i++)
            {
                var doc = _tabs[i];
                if (doc.IsDirty)
                {
                    var dr = MessageBox.Show(
                        _loc.Get("Dialog.UnsavedPrompt", doc.Title),
                        _loc.Get("Dialog.UnsavedTitle"),
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Warning);

                    if (dr == DialogResult.Cancel)
                    {
                        e.Cancel = true;
                        return;
                    }
                    if (dr == DialogResult.Yes)
                    {
                        SaveCurrentTabAsync(saveAs: false).GetAwaiter().GetResult();
                    }
                }
            }

            // ウィンドウサイズとセッションの保存
            var settings = _settingsService.CurrentSettings;
            settings.WindowBounds = new WindowBoundsConfig
            {
                X = Bounds.X,
                Y = Bounds.Y,
                Width = Bounds.Width,
                Height = Bounds.Height,
                IsMaximized = WindowState == FormWindowState.Maximized
            };

            if (settings.AutoSaveSession)
            {
                var manifest = new SessionManifest { ActiveTabIndex = _currentTabIndex };
                var tabSessionList = new List<TabSessionData>();

                foreach (var doc in _tabs)
                {
                    manifest.Tabs.Add(new TabManifestEntry
                    {
                        Id = doc.Id.ToString(),
                        Title = doc.Title,
                        FilePath = doc.FilePath,
                        IsDirty = doc.IsDirty,
                        LineEnding = doc.LineEnding,
                        EncodingName = doc.Encoding.WebName,
                        HasBom = doc.HasBom
                    });

                    var tData = new TabSessionData
                    {
                        Id = doc.Id.ToString(),
                        Title = doc.Title,
                        FilePath = doc.FilePath,
                        IsDirty = doc.IsDirty,
                        LineEnding = doc.LineEnding,
                        EncodingName = doc.Encoding.WebName,
                        HasBom = doc.HasBom,
                        ActiveRow = _grid.ActiveCell.Row,
                        ActiveCol = _grid.ActiveCell.Col
                    };

                    foreach (var kvp in doc.Cells)
                    {
                        tData.Cells[kvp.Key.FormatAddress()] = kvp.Value.RawText;
                    }
                    foreach (var cw in doc.CustomColumnWidths) tData.ColumnWidths[cw.Key] = cw.Value;
                    foreach (var rh in doc.CustomRowHeights) tData.RowHeights[rh.Key] = rh.Value;

                    tabSessionList.Add(tData);
                }

                _settingsService.SaveSessionSync(manifest, tabSessionList);
            }

            _settingsService.SaveSettingsSync();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            SingleInstanceIpc.StopServer();
        }
    }
}
