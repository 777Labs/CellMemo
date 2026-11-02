using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CellMemo.Models;

namespace CellMemo.UI.Controls
{
    public class VirtualGridControl : Control
    {
        private SheetDocument? _doc;
        private ThemePalette _theme;

        // スクロールバー
        private readonly VScrollBar _vScrollBar;
        private readonly HScrollBar _hScrollBar;

        // インライン編集テキストボックス
        private readonly TextBox _editBox;
        private bool _isEditing = false;
        private CellAddress _editingAddress = CellAddress.Empty;

        // ズーム & フォント
        private float _zoom = 1.0f; // 100%
        private const float MinZoom = 0.2f;
        private const float MaxZoom = 4.0f;
        private Font? _zoomedCellFont;
        private Font? _zoomedBoldFont;

        // 標準サイズ (ズーム1.0時)
        private const int BaseColHeaderHeight = 26;
        private const int BaseRowHeaderWidth = 42;
        private const int BaseDefaultColWidth = 88;
        private const int BaseDefaultRowHeight = 22;

        // 固定行
        public bool FreezeTopRow { get; set; } = false;

        // 選択状態
        public enum GridSelectionType { Cells, Rows, Columns, All }
        private enum HeaderHitType { None, Corner, ColumnHeader, RowHeader }

        public GridSelectionType CurrentSelectionType { get; private set; } = GridSelectionType.Cells;
        private CellAddress _activeCell = new(1, 1);
        private CellAddress _selectionAnchor = new(1, 1);
        private CellAddress _selectionEnd = new(1, 1);

        public CellAddress SelectionAnchor => _selectionAnchor;
        public CellAddress SelectionEnd => _selectionEnd;

        // ヘッダードラッグ選択状態
        private bool _isSelectingColumns = false;
        private bool _isSelectingRows = false;
        private int _headerSelectAnchor = 0;

        // リサイズ状態
        private enum ResizeMode { None, Column, Row }
        private ResizeMode _resizeMode = ResizeMode.None;
        private int _resizeIndex = 0;
        private int _resizeStartPos = 0;
        private int _resizeStartSize = 0;
        private int _resizeCurrentSize = 0;
        private Point _resizeMousePos = Point.Empty;

        // フィルハンドルドラッグ状態
        private bool _isDraggingFillHandle = false;
        private Rectangle _fillHandleRect;
        private CellAddress _fillTargetEnd = CellAddress.Empty;

        // 数式参照ドラッグ・セル選択状態（Excelライク参照入力）
        private bool _isFormulaSelecting = false;
        private CellAddress _formulaRangeAnchor = CellAddress.Empty;
        private CellAddress _formulaRangeEnd = CellAddress.Empty;
        private int _formulaRefStartPos = -1;
        private int _formulaRefLength = 0;

        // イベント
        public event EventHandler<CellAddress>? ActiveCellChanged;
        public event EventHandler? SelectionChanged;
        public event EventHandler<int>? ZoomChanged;
        public event EventHandler? CutRequested;
        public event EventHandler? CopyRequested;
        public event EventHandler? PasteRequested;

        public VirtualGridControl(ThemePalette theme)
        {
            _theme = theme;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.Selectable |
                     ControlStyles.ResizeRedraw, true);

            _vScrollBar = new VScrollBar { Dock = DockStyle.Right, SmallChange = 1, LargeChange = 10 };
            _hScrollBar = new HScrollBar { Dock = DockStyle.Bottom, SmallChange = 1, LargeChange = 5 };

            _vScrollBar.ValueChanged += (s, e) => Invalidate();
            _hScrollBar.ValueChanged += (s, e) => Invalidate();

            Controls.Add(_vScrollBar);
            Controls.Add(_hScrollBar);

            _editBox = new TextBox
            {
                Visible = false,
                BorderStyle = BorderStyle.None,
                Multiline = true
            };
            _editBox.KeyDown += OnEditBoxKeyDown;
            Controls.Add(_editBox);

            UpdateZoomFonts();
        }

        public void SetDocument(SheetDocument doc)
        {
            if (_doc != null)
            {
                _doc.DocumentChanged -= OnDocumentChanged;
            }

            _doc = doc;
            if (_doc != null)
            {
                _doc.DocumentChanged += OnDocumentChanged;
            }

            _activeCell = new CellAddress(1, 1);
            _selectionAnchor = new CellAddress(1, 1);
            _selectionEnd = new CellAddress(1, 1);

            EndEdit(commit: false);
            UpdateScrollBars();
            Invalidate();

            ActiveCellChanged?.Invoke(this, _activeCell);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public SheetDocument? Document => _doc;

        public CellAddress ActiveCell
        {
            get => _activeCell;
            set
            {
                if (value.IsValid && _activeCell != value)
                {
                    _activeCell = value;
                    _selectionAnchor = value;
                    _selectionEnd = value;
                    EnsureCellVisible(value);
                    Invalidate();
                    ActiveCellChanged?.Invoke(this, _activeCell);
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public (CellAddress start, CellAddress end) GetNormalizedSelection()
        {
            int r1 = Math.Min(_selectionAnchor.Row, _selectionEnd.Row);
            int r2 = Math.Max(_selectionAnchor.Row, _selectionEnd.Row);
            int c1 = Math.Min(_selectionAnchor.Col, _selectionEnd.Col);
            int c2 = Math.Max(_selectionAnchor.Col, _selectionEnd.Col);
            return (new CellAddress(r1, c1), new CellAddress(r2, c2));
        }

        public List<CellAddress> GetSelectedCells()
        {
            var list = new List<CellAddress>();
            var (start, end) = GetNormalizedSelection();
            for (int r = start.Row; r <= end.Row; r++)
            {
                for (int c = start.Col; c <= end.Col; c++)
                {
                    list.Add(new CellAddress(r, c));
                }
            }
            return list;
        }

        public void SelectColumns(int startCol, int endCol)
        {
            if (_doc == null) return;
            EndEdit(commit: true);
            int c1 = Math.Clamp(Math.Min(startCol, endCol), 1, _doc.DisplayCols);
            int c2 = Math.Clamp(Math.Max(startCol, endCol), 1, _doc.DisplayCols);

            CurrentSelectionType = GridSelectionType.Columns;
            _activeCell = new CellAddress(1, startCol);
            _selectionAnchor = new CellAddress(1, c1);
            _selectionEnd = new CellAddress(_doc.DisplayRows, c2);

            EnsureCellVisible(_activeCell);
            Invalidate();
            ActiveCellChanged?.Invoke(this, _activeCell);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SelectRows(int startRow, int endRow)
        {
            if (_doc == null) return;
            EndEdit(commit: true);
            int r1 = Math.Clamp(Math.Min(startRow, endRow), 1, _doc.DisplayRows);
            int r2 = Math.Clamp(Math.Max(startRow, endRow), 1, _doc.DisplayRows);

            CurrentSelectionType = GridSelectionType.Rows;
            _activeCell = new CellAddress(startRow, 1);
            _selectionAnchor = new CellAddress(r1, 1);
            _selectionEnd = new CellAddress(r2, _doc.DisplayCols);

            EnsureCellVisible(_activeCell);
            Invalidate();
            ActiveCellChanged?.Invoke(this, _activeCell);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SelectAll()
        {
            if (_doc == null) return;
            EndEdit(commit: true);
            CurrentSelectionType = GridSelectionType.All;
            _activeCell = new CellAddress(1, 1);
            _selectionAnchor = new CellAddress(1, 1);
            _selectionEnd = new CellAddress(_doc.DisplayRows, _doc.DisplayCols);

            EnsureCellVisible(_activeCell);
            Invalidate();
            ActiveCellChanged?.Invoke(this, _activeCell);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void InsertRowAbove()
        {
            if (_doc == null) return;
            EndEdit(commit: true);
            int targetRow = (CurrentSelectionType == GridSelectionType.Rows)
                ? Math.Min(_selectionAnchor.Row, _selectionEnd.Row)
                : _activeCell.Row;
            _doc.InsertRow(targetRow);
            if (CurrentSelectionType == GridSelectionType.Rows)
            {
                SelectRows(targetRow, targetRow);
            }
            Invalidate();
        }

        public void InsertRowBelow()
        {
            if (_doc == null) return;
            EndEdit(commit: true);
            int targetRow = (CurrentSelectionType == GridSelectionType.Rows)
                ? Math.Max(_selectionAnchor.Row, _selectionEnd.Row) + 1
                : _activeCell.Row + 1;
            _doc.InsertRow(targetRow);
            if (CurrentSelectionType == GridSelectionType.Rows)
            {
                SelectRows(targetRow, targetRow);
            }
            Invalidate();
        }

        public void DeleteSelectedRows()
        {
            if (_doc == null) return;
            EndEdit(commit: false);
            int r1 = Math.Min(_selectionAnchor.Row, _selectionEnd.Row);
            int r2 = Math.Max(_selectionAnchor.Row, _selectionEnd.Row);
            int startRow = (CurrentSelectionType == GridSelectionType.Rows) ? r1 : _activeCell.Row;
            int endRow = (CurrentSelectionType == GridSelectionType.Rows) ? r2 : _activeCell.Row;

            for (int r = endRow; r >= startRow; r--)
            {
                _doc.DeleteRow(r);
            }
            int newActiveRow = Math.Clamp(startRow, 1, _doc.DisplayRows);
            if (CurrentSelectionType == GridSelectionType.Rows)
            {
                SelectRows(newActiveRow, newActiveRow);
            }
            else
            {
                _activeCell = new CellAddress(newActiveRow, _activeCell.Col);
                _selectionAnchor = _activeCell;
                _selectionEnd = _activeCell;
            }
            Invalidate();
            ActiveCellChanged?.Invoke(this, _activeCell);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void InsertColLeft()
        {
            if (_doc == null) return;
            EndEdit(commit: true);
            int targetCol = (CurrentSelectionType == GridSelectionType.Columns)
                ? Math.Min(_selectionAnchor.Col, _selectionEnd.Col)
                : _activeCell.Col;
            _doc.InsertColumn(targetCol);
            if (CurrentSelectionType == GridSelectionType.Columns)
            {
                SelectColumns(targetCol, targetCol);
            }
            Invalidate();
        }

        public void InsertColRight()
        {
            if (_doc == null) return;
            EndEdit(commit: true);
            int targetCol = (CurrentSelectionType == GridSelectionType.Columns)
                ? Math.Max(_selectionAnchor.Col, _selectionEnd.Col) + 1
                : _activeCell.Col + 1;
            _doc.InsertColumn(targetCol);
            if (CurrentSelectionType == GridSelectionType.Columns)
            {
                SelectColumns(targetCol, targetCol);
            }
            Invalidate();
        }

        public void DeleteSelectedColumns()
        {
            if (_doc == null) return;
            EndEdit(commit: false);
            int c1 = Math.Min(_selectionAnchor.Col, _selectionEnd.Col);
            int c2 = Math.Max(_selectionAnchor.Col, _selectionEnd.Col);
            int startCol = (CurrentSelectionType == GridSelectionType.Columns) ? c1 : _activeCell.Col;
            int endCol = (CurrentSelectionType == GridSelectionType.Columns) ? c2 : _activeCell.Col;

            for (int c = endCol; c >= startCol; c--)
            {
                _doc.DeleteColumn(c);
            }
            int newActiveCol = Math.Clamp(startCol, 1, _doc.DisplayCols);
            if (CurrentSelectionType == GridSelectionType.Columns)
            {
                SelectColumns(newActiveCol, newActiveCol);
            }
            else
            {
                _activeCell = new CellAddress(_activeCell.Row, newActiveCol);
                _selectionAnchor = _activeCell;
                _selectionEnd = _activeCell;
            }
            Invalidate();
            ActiveCellChanged?.Invoke(this, _activeCell);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetTheme(ThemePalette theme)
        {
            _theme = theme;
            UpdateZoomFonts();
            _editBox.BackColor = _theme.CellBackground;
            _editBox.ForeColor = _theme.TextColor;
            Invalidate();
        }

        public float Zoom
        {
            get => _zoom;
            set
            {
                float clamped = Math.Clamp(value, MinZoom, MaxZoom);
                if (Math.Abs(_zoom - clamped) > 0.01f)
                {
                    _zoom = clamped;
                    UpdateZoomFonts();
                    UpdateScrollBars();
                    Invalidate();
                    ZoomChanged?.Invoke(this, (int)Math.Round(_zoom * 100));
                }
            }
        }

        private void UpdateZoomFonts()
        {
            _zoomedCellFont?.Dispose();
            _zoomedBoldFont?.Dispose();

            float size = Math.Max(6f, _theme.DefaultFont.Size * _zoom);
            _zoomedCellFont = new Font(_theme.DefaultFont.FontFamily, size, FontStyle.Regular);
            _zoomedBoldFont = new Font(_theme.DefaultFont.FontFamily, size, FontStyle.Bold);
            _editBox.Font = _zoomedCellFont;
        }

        private void OnDocumentChanged(object? sender, EventArgs e)
        {
            UpdateScrollBars();
            Invalidate();
        }

        private int ScalePx(int px) => (int)Math.Round(px * _zoom);

        private int GetColWidth(int col)
        {
            if (_doc != null && _doc.CustomColumnWidths.TryGetValue(col, out int w))
                return ScalePx(w);
            return ScalePx(BaseDefaultColWidth);
        }

        private int GetRowHeight(int row)
        {
            if (_doc != null && _doc.CustomRowHeights.TryGetValue(row, out int h))
                return ScalePx(h);
            return ScalePx(BaseDefaultRowHeight);
        }

        private int GetRowHeaderWidth()
        {
            int maxRow = _doc != null ? _doc.DisplayRows : 1000;
            int digits = Math.Max(2, maxRow.ToString().Length);
            return ScalePx(Math.Max(BaseRowHeaderWidth, digits * 10 + 12));
        }

        private int GetColHeaderHeight() => ScalePx(BaseColHeaderHeight);

        private Rectangle GetGridContentArea()
        {
            return new Rectangle(
                GetRowHeaderWidth(),
                GetColHeaderHeight(),
                Math.Max(0, Width - _vScrollBar.Width - GetRowHeaderWidth()),
                Math.Max(0, Height - _hScrollBar.Height - GetColHeaderHeight())
            );
        }

        private void UpdateScrollBars()
        {
            if (_doc == null) return;

            _vScrollBar.Maximum = Math.Max(1, _doc.DisplayRows);
            _hScrollBar.Maximum = Math.Max(1, _doc.DisplayCols);
        }

        private void EnsureCellVisible(CellAddress addr)
        {
            if (addr.Row < _vScrollBar.Value + 1)
            {
                _vScrollBar.Value = Math.Max(0, addr.Row - 1);
            }
            if (addr.Col < _hScrollBar.Value + 1)
            {
                _hScrollBar.Value = Math.Max(0, addr.Col - 1);
            }
            // 右端・下端の判定（簡易）
            var area = GetGridContentArea();
            int currentY = 0;
            for (int r = _vScrollBar.Value + 1; r <= addr.Row; r++)
            {
                currentY += GetRowHeight(r);
            }
            if (currentY > area.Height)
            {
                _vScrollBar.Value = Math.Min(_vScrollBar.Maximum, Math.Max(0, addr.Row - 10));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // 背景塗りつぶし
            g.FillRectangle(_theme.CellBrush, ClientRectangle);

            if (_doc == null) return;

            int rowHeaderW = GetRowHeaderWidth();
            int colHeaderH = GetColHeaderHeight();
            var contentArea = GetGridContentArea();

            int startRow = _vScrollBar.Value + 1;
            int startCol = _hScrollBar.Value + 1;

            var (selStart, selEnd) = GetNormalizedSelection();

            // 1. 各可視列のX座標と幅を算出
            var colPositions = new List<(int col, int x, int width)>();
            int curX = rowHeaderW;
            for (int c = startCol; c <= _doc.DisplayCols; c++)
            {
                int w = GetColWidth(c);
                colPositions.Add((c, curX, w));
                curX += w;
                if (curX > Width - _vScrollBar.Width) break;
            }

            // 2. 各可視行のY座標と高さを算出
            var rowPositions = new List<(int row, int y, int height)>();
            int curY = colHeaderH;

            // 固定行の処理
            if (FreezeTopRow && startRow > 1)
            {
                int h1 = GetRowHeight(1);
                rowPositions.Add((1, curY, h1));
                curY += h1;
            }

            for (int r = startRow; r <= _doc.DisplayRows; r++)
            {
                if (FreezeTopRow && r == 1) continue;
                int h = GetRowHeight(r);
                rowPositions.Add((r, curY, h));
                curY += h;
                if (curY > Height - _hScrollBar.Height) break;
            }

            // 3. セル背景 & 複数選択オーバーレイの描画
            foreach (var rItem in rowPositions)
            {
                foreach (var cItem in colPositions)
                {
                    var cellRect = new Rectangle(cItem.x, rItem.y, cItem.width, rItem.height);

                    bool inSelection = (rItem.row >= selStart.Row && rItem.row <= selEnd.Row &&
                                        cItem.col >= selStart.Col && cItem.col <= selEnd.Col);
                    bool isActiveCell = (rItem.row == _activeCell.Row && cItem.col == _activeCell.Col);

                    if (inSelection && !isActiveCell)
                    {
                        g.FillRectangle(_theme.SelectionOverlayBrush, cellRect);
                    }

                    // セルテキスト描画
                    var cell = _doc.GetCell(new CellAddress(rItem.row, cItem.col));
                    if (cell != null && !string.IsNullOrEmpty(cell.EvaluatedText))
                    {
                        var textRect = new Rectangle(cellRect.X + 4, cellRect.Y + 2, cellRect.Width - 8, cellRect.Height - 4);
                        var align = cell.NumericValue.HasValue ? TextFormatFlags.Right : TextFormatFlags.Left;
                        var flags = align | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
                        var font = _zoomedCellFont ?? _theme.DefaultFont;
                        Color textColor = cell.HasError ? Color.Red : _theme.TextColor;
                        TextRenderer.DrawText(g, cell.EvaluatedText, font, textRect, textColor, flags);
                    }

                    // グリッド境界線
                    g.DrawRectangle(_theme.GridLinePen, cellRect);
                }
            }

            // 4. アクティブセル太枠および選択範囲外枠太枠の描画
            DrawSelectionBorders(g, colPositions, rowPositions, selStart, selEnd);

            // 5. 列ヘッダーの描画
            g.FillRectangle(_theme.HeaderBrush, 0, 0, Width, colHeaderH);
            foreach (var cItem in colPositions)
            {
                bool isColSelected = CurrentSelectionType != GridSelectionType.Rows && (cItem.col >= selStart.Col && cItem.col <= selEnd.Col);
                var hRect = new Rectangle(cItem.x, 0, cItem.width, colHeaderH);

                if (isColSelected)
                {
                    g.FillRectangle(_theme.HeaderActiveBrush, hRect);
                    // 2px 緑アクセントライン (下辺)
                    g.DrawLine(_theme.AccentGreenPen2px, hRect.X, hRect.Bottom - 1, hRect.Right, hRect.Bottom - 1);
                }

                g.DrawRectangle(_theme.GridLinePen, hRect);

                string colName = CellAddress.ToColumnName(cItem.col);
                var font = isColSelected ? _theme.HeaderActiveFont : _theme.HeaderFont;
                Color colColor = isColSelected ? _theme.HeaderActiveText : _theme.SubTextColor;
                TextRenderer.DrawText(g, colName, font, hRect, colColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            // 6. 行ヘッダーの描画
            g.FillRectangle(_theme.HeaderBrush, 0, 0, rowHeaderW, Height);
            foreach (var rItem in rowPositions)
            {
                bool isRowSelected = CurrentSelectionType != GridSelectionType.Columns && (rItem.row >= selStart.Row && rItem.row <= selEnd.Row);
                var hRect = new Rectangle(0, rItem.y, rowHeaderW, rItem.height);

                if (isRowSelected)
                {
                    g.FillRectangle(_theme.HeaderActiveBrush, hRect);
                    // 2px 緑アクセントライン (右辺)
                    g.DrawLine(_theme.AccentGreenPen2px, hRect.Right - 1, hRect.Y, hRect.Right - 1, hRect.Bottom);
                }

                g.DrawRectangle(_theme.GridLinePen, hRect);

                string rowName = rItem.row.ToString();
                var font = isRowSelected ? _theme.HeaderActiveFont : _theme.HeaderFont;
                Color rowColor = isRowSelected ? _theme.HeaderActiveText : _theme.SubTextColor;
                TextRenderer.DrawText(g, rowName, font, hRect, rowColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            // 7. 左上コーナー四角 (全選択ボタン)
            var cornerRect = new Rectangle(0, 0, rowHeaderW, colHeaderH);
            if (CurrentSelectionType == GridSelectionType.All)
            {
                g.FillRectangle(_theme.HeaderActiveBrush, cornerRect);
            }
            else
            {
                g.FillRectangle(_theme.HeaderBrush, cornerRect);
            }
            g.DrawRectangle(_theme.GridLinePen, cornerRect);

            // 8. オートフィルドラッグ中のプレビュー枠線
            if (_isDraggingFillHandle && _fillTargetEnd.IsValid)
            {
                DrawFillPreview(g, colPositions, rowPositions);
            }

            // 9. 数式参照選択中のプレビュー枠線 (Excel風点線枠)
            if (_isEditing && _formulaRangeAnchor.IsValid && _formulaRangeEnd.IsValid)
            {
                DrawFormulaReferencePreview(g, colPositions, rowPositions);
            }

            // 10. 行/列境界リサイズ中のガイド線とサイズバッジ
            if (_resizeMode != ResizeMode.None)
            {
                DrawResizeGuideAndBadge(g);
            }
        }

        private void DrawSelectionBorders(
            Graphics g,
            List<(int col, int x, int width)> colPositions,
            List<(int row, int y, int height)> rowPositions,
            CellAddress selStart,
            CellAddress selEnd)
        {
            Rectangle? activeRect = GetCellBoundsOnScreen(_activeCell, colPositions, rowPositions);
            Rectangle? rangeRect = GetRangeBoundsOnScreen(selStart, selEnd, colPositions, rowPositions);

            // 複数セル選択のとき外枠を太枠で囲む
            if (rangeRect.HasValue && (selStart != selEnd))
            {
                g.DrawRectangle(_theme.AccentGreenPen2px, rangeRect.Value);
            }

            // アクティブセルの緑太枠
            if (activeRect.HasValue)
            {
                g.DrawRectangle(_theme.AccentGreenPen2px, activeRect.Value);

                // 右下の角のフィルハンドル (5x5 px) - 通常セル選択時のみ表示
                if (CurrentSelectionType == GridSelectionType.Cells)
                {
                    int handleX = activeRect.Value.Right - 3;
                    int handleY = activeRect.Value.Bottom - 3;
                    _fillHandleRect = new Rectangle(handleX, handleY, 6, 6);
                    g.FillRectangle(_theme.AccentGreenBrush, _fillHandleRect);
                }
                else
                {
                    _fillHandleRect = Rectangle.Empty;
                }
            }
            else
            {
                _fillHandleRect = Rectangle.Empty;
            }
        }

        private Rectangle? GetCellBoundsOnScreen(
            CellAddress addr,
            List<(int col, int x, int width)> colPositions,
            List<(int row, int y, int height)> rowPositions)
        {
            int x = -1, w = -1, y = -1, h = -1;
            foreach (var c in colPositions)
            {
                if (c.col == addr.Col) { x = c.x; w = c.width; break; }
            }
            foreach (var r in rowPositions)
            {
                if (r.row == addr.Row) { y = r.y; h = r.height; break; }
            }
            if (x >= 0 && y >= 0)
            {
                return new Rectangle(x, y, w, h);
            }
            return null;
        }

        private Rectangle? GetRangeBoundsOnScreen(
            CellAddress start,
            CellAddress end,
            List<(int col, int x, int width)> colPositions,
            List<(int row, int y, int height)> rowPositions)
        {
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            foreach (var c in colPositions)
            {
                if (c.col >= start.Col && c.col <= end.Col)
                {
                    if (c.x < minX) minX = c.x;
                    if (c.x + c.width > maxX) maxX = c.x + c.width;
                }
            }
            foreach (var r in rowPositions)
            {
                if (r.row >= start.Row && r.row <= end.Row)
                {
                    if (r.y < minY) minY = r.y;
                    if (r.y + r.height > maxY) maxY = r.y + r.height;
                }
            }

            if (minX != int.MaxValue && minY != int.MaxValue && maxX > minX && maxY > minY)
            {
                return new Rectangle(minX, minY, maxX - minX, maxY - minY);
            }
            return null;
        }

        private void DrawFillPreview(
            Graphics g,
            List<(int col, int x, int width)> colPositions,
            List<(int row, int y, int height)> rowPositions)
        {
            var (start, _) = GetNormalizedSelection();
            int r1 = Math.Min(start.Row, _fillTargetEnd.Row);
            int r2 = Math.Max(start.Row, _fillTargetEnd.Row);
            int c1 = Math.Min(start.Col, _fillTargetEnd.Col);
            int c2 = Math.Max(start.Col, _fillTargetEnd.Col);

            var previewRect = GetRangeBoundsOnScreen(new CellAddress(r1, c1), new CellAddress(r2, c2), colPositions, rowPositions);
            if (previewRect.HasValue)
            {
                using var dashPen = new Pen(_theme.AccentGreen, 1.5f) { DashStyle = DashStyle.Dash };
                g.DrawRectangle(dashPen, previewRect.Value);
            }
        }

        private void DrawFormulaReferencePreview(
            Graphics g,
            List<(int col, int x, int width)> colPositions,
            List<(int row, int y, int height)> rowPositions)
        {
            int r1 = Math.Min(_formulaRangeAnchor.Row, _formulaRangeEnd.Row);
            int r2 = Math.Max(_formulaRangeAnchor.Row, _formulaRangeEnd.Row);
            int c1 = Math.Min(_formulaRangeAnchor.Col, _formulaRangeEnd.Col);
            int c2 = Math.Max(_formulaRangeAnchor.Col, _formulaRangeEnd.Col);

            var refRect = GetRangeBoundsOnScreen(new CellAddress(r1, c1), new CellAddress(r2, c2), colPositions, rowPositions);
            if (refRect.HasValue)
            {
                using var refPen = new Pen(Color.FromArgb(0, 120, 215), 2f) { DashStyle = DashStyle.Dash };
                g.DrawRectangle(refPen, refRect.Value);
            }
        }

        private void DrawResizeGuideAndBadge(Graphics g)
        {
            var loc = Services.LocalizationService.Instance;
            double pt = Math.Round(_resizeCurrentSize * 72.0 / 96.0, 1);
            string badgeText = (_resizeMode == ResizeMode.Column)
                ? (loc.CurrentLanguage == "ja-JP" ? $"幅: {_resizeCurrentSize} px ({pt:F1} pt)" : $"Width: {_resizeCurrentSize} px ({pt:F1} pt)")
                : (loc.CurrentLanguage == "ja-JP" ? $"高さ: {_resizeCurrentSize} px ({pt:F1} pt)" : $"Height: {_resizeCurrentSize} px ({pt:F1} pt)");

            using var guidePen = new Pen(_theme.AccentGreen, 1f)
            {
                DashStyle = DashStyle.Dash
            };

            if (_resizeMode == ResizeMode.Column)
            {
                // ガイド線（縦線）: 現在のマウスX位置
                g.DrawLine(guidePen, _resizeMousePos.X, 0, _resizeMousePos.X, Height);
            }
            else
            {
                // ガイド線（横線）: 現在のマウスY位置
                g.DrawLine(guidePen, 0, _resizeMousePos.Y, Width, _resizeMousePos.Y);
            }

            // フローティングバッジ
            var font = _theme.DefaultFont;
            var sz = TextRenderer.MeasureText(g, badgeText, font);
            int badgeW = sz.Width + 14;
            int badgeH = sz.Height + 8;

            int badgeX, badgeY;
            if (_resizeMode == ResizeMode.Column)
            {
                badgeX = _resizeMousePos.X + 10;
                badgeY = Math.Max(5, GetColHeaderHeight() + 5);
                if (badgeX + badgeW > Width - 10)
                {
                    badgeX = _resizeMousePos.X - badgeW - 10;
                }
            }
            else
            {
                badgeX = Math.Max(5, GetRowHeaderWidth() + 10);
                badgeY = _resizeMousePos.Y + 8;
                if (badgeY + badgeH > Height - 10)
                {
                    badgeY = _resizeMousePos.Y - badgeH - 8;
                }
            }

            var badgeRect = new Rectangle(badgeX, badgeY, badgeW, badgeH);
            using var badgeBgBrush = new SolidBrush(Color.FromArgb(235, 30, 30, 30));
            using var badgeBorderPen = new Pen(_theme.AccentGreen, 1.2f);

            g.FillRectangle(badgeBgBrush, badgeRect);
            g.DrawRectangle(badgeBorderPen, badgeRect);

            TextRenderer.DrawText(g, badgeText, font, badgeRect, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        // ==========================================
        // インライン編集
        // ==========================================
        public void BeginEdit(bool overwrite = false, char? initialChar = null)
        {
            if (_doc == null || !_activeCell.IsValid) return;

            var bounds = GetCellRectOnScreen(_activeCell);
            if (!bounds.HasValue) return;

            _isEditing = true;
            _editingAddress = _activeCell;
            _isFormulaSelecting = false;
            _formulaRangeAnchor = CellAddress.Empty;
            _formulaRangeEnd = CellAddress.Empty;
            _formulaRefStartPos = -1;
            _formulaRefLength = 0;

            string initialText = _doc.GetCellRawText(_activeCell);
            if (overwrite)
            {
                initialText = initialChar.HasValue ? initialChar.Value.ToString() : string.Empty;
            }

            _editBox.Bounds = new Rectangle(bounds.Value.X + 2, bounds.Value.Y + 2, bounds.Value.Width - 4, bounds.Value.Height - 4);
            _editBox.Text = initialText;
            _editBox.Visible = true;
            _editBox.BringToFront();
            _editBox.Focus();
            _editBox.SelectionStart = _editBox.Text.Length;
        }

        public void EndEdit(bool commit)
        {
            if (!_isEditing) return;

            if (commit && _doc != null && _editingAddress.IsValid)
            {
                _doc.SetCell(_editingAddress, _editBox.Text);
            }

            _isEditing = false;
            _editBox.Visible = false;
            _isFormulaSelecting = false;
            _formulaRangeAnchor = CellAddress.Empty;
            _formulaRangeEnd = CellAddress.Empty;
            _formulaRefStartPos = -1;
            _formulaRefLength = 0;
            Focus();
            Invalidate();
        }

        private void InsertOrReplaceFormulaReference(string refText)
        {
            if (_formulaRefStartPos >= 0 && _formulaRefStartPos <= _editBox.Text.Length)
            {
                // 直前に挿入した参照アドレスを置き換える
                string before = _editBox.Text.Substring(0, _formulaRefStartPos);
                int afterStart = Math.Min(_editBox.Text.Length, _formulaRefStartPos + _formulaRefLength);
                string after = _editBox.Text.Substring(afterStart);
                _editBox.Text = before + refText + after;
                _formulaRefLength = refText.Length;
            }
            else
            {
                // カーソル位置（または末尾）に新規挿入
                int insertPos = _editBox.SelectionStart >= 0 ? _editBox.SelectionStart : _editBox.Text.Length;
                string before = _editBox.Text.Substring(0, insertPos);
                string after = _editBox.Text.Substring(insertPos);
                _editBox.Text = before + refText + after;
                _formulaRefStartPos = insertPos;
                _formulaRefLength = refText.Length;
            }
            _editBox.SelectionStart = _formulaRefStartPos + _formulaRefLength;
            _editBox.SelectionLength = 0;
        }

        private static string GetRangeAddressString(CellAddress a, CellAddress b)
        {
            if (a == b) return a.ToA1();
            int minRow = Math.Min(a.Row, b.Row);
            int maxRow = Math.Max(a.Row, b.Row);
            int minCol = Math.Min(a.Col, b.Col);
            int maxCol = Math.Max(a.Col, b.Col);
            var start = new CellAddress(minRow, minCol);
            var end = new CellAddress(maxRow, maxCol);
            return $"{start.ToA1()}:{end.ToA1()}";
        }

        private Rectangle? GetCellRectOnScreen(CellAddress addr)
        {
            int rowHeaderW = GetRowHeaderWidth();
            int colHeaderH = GetColHeaderHeight();

            int startRow = _vScrollBar.Value + 1;
            int startCol = _hScrollBar.Value + 1;

            int curX = rowHeaderW;
            int foundX = -1, foundW = -1;
            for (int c = startCol; c <= (_doc?.DisplayCols ?? 100); c++)
            {
                int w = GetColWidth(c);
                if (c == addr.Col) { foundX = curX; foundW = w; break; }
                curX += w;
                if (curX > Width) break;
            }

            int curY = colHeaderH;
            int foundY = -1, foundH = -1;
            for (int r = startRow; r <= (_doc?.DisplayRows ?? 1000); r++)
            {
                int h = GetRowHeight(r);
                if (r == addr.Row) { foundY = curY; foundH = h; break; }
                curY += h;
                if (curY > Height) break;
            }

            if (foundX >= 0 && foundY >= 0)
            {
                return new Rectangle(foundX, foundY, foundW, foundH);
            }
            return null;
        }

        private void OnEditBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (e.Alt)
                {
                    // Alt+Enter: セル内改行
                    int pos = _editBox.SelectionStart;
                    _editBox.Text = _editBox.Text.Insert(pos, "\n");
                    _editBox.SelectionStart = pos + 1;
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else
                {
                    // 編集確定して下へ移動
                    EndEdit(commit: true);
                    MoveActiveCell(e.Shift ? -1 : 1, 0, extendSelection: false);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }
            else if (e.KeyCode == Keys.Tab)
            {
                // 編集確定して右へ移動
                EndEdit(commit: true);
                MoveActiveCell(0, e.Shift ? -1 : 1, extendSelection: false);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                EndEdit(commit: false);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else
            {
                // 通常文字や移動キーが押されたら直前の数式参照置換を確定/リセット
                _formulaRefStartPos = -1;
                _formulaRefLength = 0;
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                case Keys.Tab:
                case Keys.Enter:
                    return true;
                default:
                    return base.IsInputKey(keyData);
            }
        }

        // ==========================================
        // キーボード操作
        // ==========================================
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (_isEditing) return;

            // リクエスト5: セル選択時にEnterを押すと入力モードにする
            if (e.KeyCode == Keys.Enter)
            {
                BeginEdit(overwrite: false);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.F2)
            {
                BeginEdit(overwrite: false);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Delete)
            {
                _doc?.ClearCells(GetSelectedCells());
                e.Handled = true;
                return;
            }

            if (e.Control && e.KeyCode == Keys.A)
            {
                // 全選択
                if (_doc != null)
                {
                    _selectionAnchor = new CellAddress(1, 1);
                    var (mr, mc) = _doc.GetDimensions();
                    _selectionEnd = new CellAddress(Math.Max(1, mr), Math.Max(1, mc));
                    Invalidate();
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
                e.Handled = true;
                return;
            }

            // リクエスト4: カーソルキーでセル移動
            int dRow = 0;
            int dCol = 0;
            if (e.KeyCode == Keys.Up) dRow = -1;
            else if (e.KeyCode == Keys.Down) dRow = 1;
            else if (e.KeyCode == Keys.Left) dCol = -1;
            else if (e.KeyCode == Keys.Right) dCol = 1;
            else if (e.KeyCode == Keys.Tab) { dCol = e.Shift ? -1 : 1; }

            if (dRow != 0 || dCol != 0)
            {
                if (e.Control && e.KeyCode != Keys.Tab)
                {
                    // Ctrl+Arrow 端までジャンプ
                    JumpToDataEdge(dRow, dCol, e.Shift);
                }
                else
                {
                    MoveActiveCell(dRow, dCol, e.Shift);
                }
                e.Handled = true;
                return;
            }

            // 文字直接入力で上書き編集開始
            if (!e.Control && !e.Alt && e.KeyValue >= 32 && e.KeyValue <= 126)
            {
                char c = (char)e.KeyValue;
                if (!char.IsControl(c))
                {
                    BeginEdit(overwrite: true, c);
                    e.Handled = true;
                }
            }
        }

        private void MoveActiveCell(int dRow, int dCol, bool extendSelection)
        {
            if (_doc == null) return;

            int targetRow = Math.Clamp(_activeCell.Row + dRow, 1, _doc.DisplayRows);
            int targetCol = Math.Clamp(_activeCell.Col + dCol, 1, _doc.DisplayCols);
            var newAddr = new CellAddress(targetRow, targetCol);

            if (extendSelection)
            {
                _selectionEnd = newAddr;
            }
            else
            {
                _activeCell = newAddr;
                _selectionAnchor = newAddr;
                _selectionEnd = newAddr;
            }

            EnsureCellVisible(newAddr);
            Invalidate();

            ActiveCellChanged?.Invoke(this, _activeCell);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void JumpToDataEdge(int dRow, int dCol, bool extendSelection)
        {
            if (_doc == null) return;
            int r = _activeCell.Row;
            int c = _activeCell.Col;

            bool currentHasData = _doc.GetCell(new CellAddress(r, c)) != null;

            while (true)
            {
                int nextR = r + dRow;
                int nextC = c + dCol;
                if (nextR < 1 || nextR > _doc.DisplayRows || nextC < 1 || nextC > _doc.DisplayCols)
                    break;

                bool nextHasData = _doc.GetCell(new CellAddress(nextR, nextC)) != null;
                r = nextR;
                c = nextC;

                if (currentHasData && !nextHasData)
                {
                    // データのあるセルの端
                    r -= dRow;
                    c -= dCol;
                    break;
                }
                if (!currentHasData && nextHasData)
                {
                    // 次のデータセルの先頭
                    break;
                }
            }

            var target = new CellAddress(r, c);
            if (extendSelection)
            {
                _selectionEnd = target;
            }
            else
            {
                _activeCell = target;
                _selectionAnchor = target;
                _selectionEnd = target;
            }
            EnsureCellVisible(target);
            Invalidate();
            ActiveCellChanged?.Invoke(this, _activeCell);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        // ==========================================
        // マウス操作 (選択・フィルハンドル・リサイズ)
        // ==========================================
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (e.Button == MouseButtons.Left)
            {
                // リクエスト6: セル入力途中で他のセルをクリックでアドレスを入力する仕様（Excelライク仕様）
                if (_isEditing)
                {
                    string currentText = _editBox.Text;
                    bool isFormulaContext = currentText.StartsWith("=") ||
                                           (currentText.Length > 0 && "=+-*/(,".IndexOf(currentText[^1]) >= 0);

                    if (isFormulaContext)
                    {
                        var hitCell = HitTest(e.Location);
                        if (hitCell.HasValue)
                        {
                            if (hitCell.Value != _editingAddress)
                            {
                                _isFormulaSelecting = true;
                                _formulaRangeAnchor = hitCell.Value;
                                _formulaRangeEnd = hitCell.Value;
                                Capture = true;

                                InsertOrReplaceFormulaReference(hitCell.Value.ToA1());
                                Invalidate();
                                return;
                            }
                        }
                    }

                    // 通常テキスト時または同一セルの場合は編集確定
                    EndEdit(commit: true);
                }

                // フィルハンドルのクリック
                if (!_fillHandleRect.IsEmpty && _fillHandleRect.Contains(e.Location))
                {
                    _isDraggingFillHandle = true;
                    _fillTargetEnd = _selectionEnd;
                    Capture = true;
                    return;
                }

                // 列・行境界リサイズのチェック
                if (CheckResizeHit(e.Location, out var mode, out int idx))
                {
                    _resizeMode = mode;
                    _resizeIndex = idx;
                    _resizeStartPos = (mode == ResizeMode.Column) ? e.X : e.Y;
                    _resizeStartSize = (mode == ResizeMode.Column) ? GetColWidth(idx) : GetRowHeight(idx);
                    _resizeCurrentSize = (int)Math.Round(_resizeStartSize / _zoom);
                    _resizeMousePos = e.Location;
                    Capture = true;
                    Invalidate();
                    return;
                }

                // ヘッダークリック判定 (列・行・コーナー全選択)
                var headerHit = HitTestHeader(e.Location, out int headerIndex);
                if (headerHit == HeaderHitType.Corner)
                {
                    SelectAll();
                    return;
                }
                if (headerHit == HeaderHitType.ColumnHeader)
                {
                    _isSelectingColumns = true;
                    _headerSelectAnchor = headerIndex;
                    SelectColumns(headerIndex, headerIndex);
                    Capture = true;
                    return;
                }
                if (headerHit == HeaderHitType.RowHeader)
                {
                    _isSelectingRows = true;
                    _headerSelectAnchor = headerIndex;
                    SelectRows(headerIndex, headerIndex);
                    Capture = true;
                    return;
                }

                // 通常セルクリック
                var hit = HitTest(e.Location);
                if (hit.HasValue)
                {
                    CurrentSelectionType = GridSelectionType.Cells;
                    _activeCell = hit.Value;
                    _selectionAnchor = hit.Value;
                    _selectionEnd = hit.Value;
                    Invalidate();

                    ActiveCellChanged?.Invoke(this, _activeCell);
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                if (_isEditing)
                {
                    EndEdit(commit: true);
                }

                // ヘッダー上での右クリック
                var headerHit = HitTestHeader(e.Location, out int headerIndex);
                if (headerHit == HeaderHitType.ColumnHeader)
                {
                    int c1 = Math.Min(_selectionAnchor.Col, _selectionEnd.Col);
                    int c2 = Math.Max(_selectionAnchor.Col, _selectionEnd.Col);
                    if (CurrentSelectionType != GridSelectionType.Columns || headerIndex < c1 || headerIndex > c2)
                    {
                        SelectColumns(headerIndex, headerIndex);
                    }
                    ShowContextMenu(e.Location);
                    return;
                }
                if (headerHit == HeaderHitType.RowHeader)
                {
                    int r1 = Math.Min(_selectionAnchor.Row, _selectionEnd.Row);
                    int r2 = Math.Max(_selectionAnchor.Row, _selectionEnd.Row);
                    if (CurrentSelectionType != GridSelectionType.Rows || headerIndex < r1 || headerIndex > r2)
                    {
                        SelectRows(headerIndex, headerIndex);
                    }
                    ShowContextMenu(e.Location);
                    return;
                }

                // セル上での右クリック
                var hit = HitTest(e.Location);
                if (hit.HasValue)
                {
                    var (selStart, selEnd) = GetNormalizedSelection();
                    bool inSel = hit.Value.Row >= selStart.Row && hit.Value.Row <= selEnd.Row &&
                                hit.Value.Col >= selStart.Col && hit.Value.Col <= selEnd.Col;
                    if (!inSel)
                    {
                        CurrentSelectionType = GridSelectionType.Cells;
                        _activeCell = hit.Value;
                        _selectionAnchor = hit.Value;
                        _selectionEnd = hit.Value;
                        Invalidate();
                        ActiveCellChanged?.Invoke(this, _activeCell);
                        SelectionChanged?.Invoke(this, EventArgs.Empty);
                    }
                    ShowContextMenu(e.Location);
                    return;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isSelectingColumns)
            {
                int c = GetColumnAtX(e.X);
                SelectColumns(_headerSelectAnchor, c);
                return;
            }

            if (_isSelectingRows)
            {
                int r = GetRowAtY(e.Y);
                SelectRows(_headerSelectAnchor, r);
                return;
            }

            if (_isFormulaSelecting)
            {
                var hit = HitTest(e.Location);
                if (hit.HasValue && hit.Value != _formulaRangeEnd)
                {
                    _formulaRangeEnd = hit.Value;
                    string rangeRef = GetRangeAddressString(_formulaRangeAnchor, _formulaRangeEnd);
                    InsertOrReplaceFormulaReference(rangeRef);
                    Invalidate();
                }
                return;
            }

            if (_resizeMode != ResizeMode.None)
            {
                int delta = (_resizeMode == ResizeMode.Column) ? (e.X - _resizeStartPos) : (e.Y - _resizeStartPos);
                int newSize = Math.Max(20, _resizeStartSize + delta);
                int unscaled = (int)Math.Round(newSize / _zoom);

                _resizeCurrentSize = unscaled;
                _resizeMousePos = e.Location;

                if (_resizeMode == ResizeMode.Column && _doc != null)
                {
                    _doc.CustomColumnWidths[_resizeIndex] = unscaled;
                }
                else if (_resizeMode == ResizeMode.Row && _doc != null)
                {
                    _doc.CustomRowHeights[_resizeIndex] = unscaled;
                }
                Invalidate();
                return;
            }

            if (_isDraggingFillHandle)
            {
                var hit = HitTest(e.Location);
                if (hit.HasValue && hit.Value != _fillTargetEnd)
                {
                    _fillTargetEnd = hit.Value;
                    Invalidate();
                }
                return;
            }

            if (e.Button == MouseButtons.Left && !_isEditing)
            {
                var hit = HitTest(e.Location);
                if (hit.HasValue && hit.Value != _selectionEnd)
                {
                    _selectionEnd = hit.Value;
                    EnsureCellVisible(hit.Value);
                    Invalidate();
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
                return;
            }

            // カーソルの更新 (リサイズ境界または通常)
            if (CheckResizeHit(e.Location, out var modeHover, out _))
            {
                Cursor = (modeHover == ResizeMode.Column) ? Cursors.SizeWE : Cursors.SizeNS;
            }
            else if (!_fillHandleRect.IsEmpty && _fillHandleRect.Contains(e.Location))
            {
                Cursor = Cursors.Cross;
            }
            else
            {
                Cursor = Cursors.Default;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Capture = false;

            if (_isSelectingColumns)
            {
                _isSelectingColumns = false;
                return;
            }

            if (_isSelectingRows)
            {
                _isSelectingRows = false;
                return;
            }

            if (_isFormulaSelecting)
            {
                _isFormulaSelecting = false;
                if (_editBox.Visible)
                {
                    _editBox.Focus();
                    _editBox.SelectionStart = _formulaRefStartPos + _formulaRefLength;
                    _editBox.SelectionLength = 0;
                }
                Invalidate();
                return;
            }

            if (_resizeMode != ResizeMode.None)
            {
                _resizeMode = ResizeMode.None;
                Cursor = Cursors.Default;
                Invalidate();
            }

            if (_isDraggingFillHandle)
            {
                _isDraggingFillHandle = false;
                ExecuteAutoFill();
                Invalidate();
                Cursor = Cursors.Default;
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left)
            {
                // 列境界ダブルクリックでオートフィット
                if (CheckResizeHit(e.Location, out var mode, out int idx) && mode == ResizeMode.Column)
                {
                    AutoFitColumn(idx);
                    return;
                }

                // セルダブルクリックで編集開始
                var hit = HitTest(e.Location);
                if (hit.HasValue)
                {
                    BeginEdit(overwrite: false);
                }
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (ModifierKeys == Keys.Control)
            {
                // Ctrl+Wheel でズーム
                float step = e.Delta > 0 ? 0.1f : -0.1f;
                Zoom += step;
            }
            else
            {
                // 通常スクロール
                int scrollStep = e.Delta > 0 ? -3 : 3;
                _vScrollBar.Value = Math.Clamp(_vScrollBar.Value + scrollStep, 0, _vScrollBar.Maximum);
            }
        }

        private bool CheckResizeHit(Point pt, out ResizeMode mode, out int index)
        {
            mode = ResizeMode.None;
            index = 0;
            if (_doc == null) return false;

            int rowHeaderW = GetRowHeaderWidth();
            int colHeaderH = GetColHeaderHeight();

            // 列ヘッダー境界チェック (Y <= colHeaderH)
            if (pt.Y <= colHeaderH)
            {
                int curX = rowHeaderW;
                int startCol = _hScrollBar.Value + 1;
                for (int c = startCol; c <= _doc.DisplayCols; c++)
                {
                    curX += GetColWidth(c);
                    if (Math.Abs(pt.X - curX) <= 4)
                    {
                        mode = ResizeMode.Column;
                        index = c;
                        return true;
                    }
                    if (curX > Width) break;
                }
            }

            // 行ヘッダー境界チェック (X <= rowHeaderW)
            if (pt.X <= rowHeaderW)
            {
                int curY = colHeaderH;
                int startRow = _vScrollBar.Value + 1;
                for (int r = startRow; r <= _doc.DisplayRows; r++)
                {
                    curY += GetRowHeight(r);
                    if (Math.Abs(pt.Y - curY) <= 4)
                    {
                        mode = ResizeMode.Row;
                        index = r;
                        return true;
                    }
                    if (curY > Height) break;
                }
            }

            return false;
        }

        private void AutoFitColumn(int col)
        {
            if (_doc == null) return;
            using var g = CreateGraphics();
            int maxWidth = 40;

            foreach (var kvp in _doc.Cells)
            {
                if (kvp.Key.Col == col && !string.IsNullOrEmpty(kvp.Value.EvaluatedText))
                {
                    var sz = TextRenderer.MeasureText(g, kvp.Value.EvaluatedText, _theme.DefaultFont);
                    if (sz.Width + 16 > maxWidth)
                    {
                        maxWidth = sz.Width + 16;
                    }
                }
            }

            _doc.CustomColumnWidths[col] = maxWidth;
            Invalidate();
        }

        private void ExecuteAutoFill()
        {
            if (_doc == null || !_fillTargetEnd.IsValid) return;

            var (start, end) = GetNormalizedSelection();

            // 下方向または右方向への展開
            var changes = new Dictionary<CellAddress, string>();

            if (_fillTargetEnd.Row > end.Row)
            {
                // 下方向フィル
                for (int c = start.Col; c <= end.Col; c++)
                {
                    string sourceVal = _doc.GetCellRawText(new CellAddress(end.Row, c));
                    bool isNumber = double.TryParse(sourceVal, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double num);

                    for (int r = end.Row + 1; r <= _fillTargetEnd.Row; r++)
                    {
                        if (isNumber)
                        {
                            num += 1.0;
                            changes[new CellAddress(r, c)] = num.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            changes[new CellAddress(r, c)] = sourceVal;
                        }
                    }
                }
                _selectionEnd = new CellAddress(_fillTargetEnd.Row, end.Col);
            }
            else if (_fillTargetEnd.Col > end.Col)
            {
                // 右方向フィル
                for (int r = start.Row; r <= end.Row; r++)
                {
                    string sourceVal = _doc.GetCellRawText(new CellAddress(r, end.Col));
                    bool isNumber = double.TryParse(sourceVal, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double num);

                    for (int c = end.Col + 1; c <= _fillTargetEnd.Col; c++)
                    {
                        if (isNumber)
                        {
                            num += 1.0;
                            changes[new CellAddress(r, c)] = num.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            changes[new CellAddress(r, c)] = sourceVal;
                        }
                    }
                }
                _selectionEnd = new CellAddress(end.Row, _fillTargetEnd.Col);
            }

            if (changes.Count > 0)
            {
                _doc.SetCellsRange(changes, "Auto-Fill");
            }
        }

        private HeaderHitType HitTestHeader(Point pt, out int index)
        {
            index = 0;
            if (_doc == null) return HeaderHitType.None;

            int rowHeaderW = GetRowHeaderWidth();
            int colHeaderH = GetColHeaderHeight();

            // 1. 左上コーナー四角 (全選択)
            if (pt.X < rowHeaderW && pt.Y < colHeaderH)
            {
                return HeaderHitType.Corner;
            }

            // 2. 列ヘッダー
            if (pt.Y < colHeaderH && pt.X >= rowHeaderW)
            {
                int startCol = _hScrollBar.Value + 1;
                int curX = rowHeaderW;
                for (int c = startCol; c <= _doc.DisplayCols; c++)
                {
                    int w = GetColWidth(c);
                    if (pt.X >= curX && pt.X < curX + w)
                    {
                        index = c;
                        return HeaderHitType.ColumnHeader;
                    }
                    curX += w;
                    if (curX > Width) break;
                }
            }

            // 3. 行ヘッダー
            if (pt.X < rowHeaderW && pt.Y >= colHeaderH)
            {
                int startRow = _vScrollBar.Value + 1;
                int curY = colHeaderH;
                for (int r = startRow; r <= _doc.DisplayRows; r++)
                {
                    int h = GetRowHeight(r);
                    if (pt.Y >= curY && pt.Y < curY + h)
                    {
                        index = r;
                        return HeaderHitType.RowHeader;
                    }
                    curY += h;
                    if (curY > Height) break;
                }
            }

            return HeaderHitType.None;
        }

        private int GetColumnAtX(int x)
        {
            if (_doc == null) return 1;
            int rowHeaderW = GetRowHeaderWidth();
            if (x < rowHeaderW) return 1;
            int startCol = _hScrollBar.Value + 1;
            int curX = rowHeaderW;
            for (int c = startCol; c <= _doc.DisplayCols; c++)
            {
                int w = GetColWidth(c);
                if (x >= curX && x < curX + w) return c;
                curX += w;
                if (curX > Width) break;
            }
            return _doc.DisplayCols;
        }

        private int GetRowAtY(int y)
        {
            if (_doc == null) return 1;
            int colHeaderH = GetColHeaderHeight();
            if (y < colHeaderH) return 1;
            int startRow = _vScrollBar.Value + 1;
            int curY = colHeaderH;
            for (int r = startRow; r <= _doc.DisplayRows; r++)
            {
                int h = GetRowHeight(r);
                if (y >= curY && y < curY + h) return r;
                curY += h;
                if (curY > Height) break;
            }
            return _doc.DisplayRows;
        }

        public void PromptSetRowHeight()
        {
            if (_doc == null) return;
            var (start, end) = GetNormalizedSelection();
            int targetRow = _activeCell.IsValid ? _activeCell.Row : start.Row;
            int currentPx = _doc.CustomRowHeights.TryGetValue(targetRow, out int h) ? h : 24;

            var loc = Services.LocalizationService.Instance;
            using var dlg = new Dialogs.SizeInputDialog(loc.Get("Dialog.RowHeight.Title"), currentPx, _theme, minVal: 10, maxVal: 500);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                int newHeight = dlg.Value;
                for (int r = start.Row; r <= end.Row; r++)
                {
                    _doc.CustomRowHeights[r] = newHeight;
                }
                _doc.IsDirty = true;
                Invalidate();
            }
        }

        public void PromptSetColumnWidth()
        {
            if (_doc == null) return;
            var (start, end) = GetNormalizedSelection();
            int targetCol = _activeCell.IsValid ? _activeCell.Col : start.Col;
            int currentPx = _doc.CustomColumnWidths.TryGetValue(targetCol, out int w) ? w : 80;

            var loc = Services.LocalizationService.Instance;
            using var dlg = new Dialogs.SizeInputDialog(loc.Get("Dialog.ColWidth.Title"), currentPx, _theme, minVal: 15, maxVal: 1000);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                int newWidth = dlg.Value;
                for (int c = start.Col; c <= end.Col; c++)
                {
                    _doc.CustomColumnWidths[c] = newWidth;
                }
                _doc.IsDirty = true;
                Invalidate();
            }
        }

        private static ToolStripMenuItem CreateMenuItem(string text, string? shortcut, EventHandler onClick)
        {
            var item = new ToolStripMenuItem(text, null, onClick);
            if (!string.IsNullOrEmpty(shortcut))
            {
                item.ShortcutKeyDisplayString = shortcut;
                item.ShowShortcutKeys = true;
            }
            return item;
        }

        public void ShowContextMenu(Point clientLocation)
        {
            var menu = BuildContextMenu();
            menu.Show(this, clientLocation);
        }

        internal ContextMenuStrip BuildContextMenu()
        {
            var menu = new ContextMenuStrip
            {
                Renderer = new ModernMenuRenderer(_theme),
                Font = _theme.DefaultFont
            };

            var loc = Services.LocalizationService.Instance;

            // クリップボード系
            menu.Items.Add(CreateMenuItem(loc.Get("Menu.Edit.Cut"), "Ctrl+X", (s, e) => CutRequested?.Invoke(this, EventArgs.Empty)));
            menu.Items.Add(CreateMenuItem(loc.Get("Menu.Edit.Copy"), "Ctrl+C", (s, e) => CopyRequested?.Invoke(this, EventArgs.Empty)));
            menu.Items.Add(CreateMenuItem(loc.Get("Menu.Edit.Paste"), "Ctrl+V", (s, e) => PasteRequested?.Invoke(this, EventArgs.Empty)));
            menu.Items.Add(new ToolStripSeparator());

            if (CurrentSelectionType == GridSelectionType.Rows)
            {
                // 行選択専用メニュー
                menu.Items.Add(CreateMenuItem(loc.Get("Menu.Edit.InsertRowAbove"), null, (s, e) => InsertRowAbove()));
                menu.Items.Add(CreateMenuItem(loc.Get("Menu.Edit.InsertRowBelow"), null, (s, e) => InsertRowBelow()));
                menu.Items.Add(CreateMenuItem(loc.Get("Menu.Edit.DeleteRow"), null, (s, e) => DeleteSelectedRows()));
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(CreateMenuItem(loc.Get("Menu.Edit.RowHeight"), null, (s, e) => PromptSetRowHeight()));
            }
            else if (CurrentSelectionType == GridSelectionType.Columns)
            {
                // 列選択専用メニュー
                menu.Items.Add(CreateMenuItem(loc.Get("Menu.Edit.InsertColLeft"), null, (s, e) => InsertColLeft()));
                menu.Items.Add(CreateMenuItem(loc.Get("Menu.Edit.InsertColRight"), null, (s, e) => InsertColRight()));
                menu.Items.Add(CreateMenuItem(loc.Get("Menu.Edit.DeleteCol"), null, (s, e) => DeleteSelectedColumns()));
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(CreateMenuItem(loc.Get("Menu.Edit.ColWidth"), null, (s, e) => PromptSetColumnWidth()));
            }
            else
            {
                // 通常セル選択時メニュー
                menu.Items.Add(CreateMenuItem(loc.Get("Menu.Edit.InsertRowAbove"), null, (s, e) => InsertRowAbove()));
                menu.Items.Add(CreateMenuItem(loc.Get("Menu.Edit.InsertRowBelow"), null, (s, e) => InsertRowBelow()));
                menu.Items.Add(CreateMenuItem(loc.Get("Menu.Edit.DeleteRow"), null, (s, e) => DeleteSelectedRows()));
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(CreateMenuItem(loc.Get("Menu.Edit.InsertColLeft"), null, (s, e) => InsertColLeft()));
                menu.Items.Add(CreateMenuItem(loc.Get("Menu.Edit.InsertColRight"), null, (s, e) => InsertColRight()));
                menu.Items.Add(CreateMenuItem(loc.Get("Menu.Edit.DeleteCol"), null, (s, e) => DeleteSelectedColumns()));
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(CreateMenuItem(loc.Get("Menu.Edit.RowHeight"), null, (s, e) => PromptSetRowHeight()));
                menu.Items.Add(CreateMenuItem(loc.Get("Menu.Edit.ColWidth"), null, (s, e) => PromptSetColumnWidth()));
            }

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(CreateMenuItem(loc.Get("Menu.Edit.Delete"), "Delete", (s, e) =>
            {
                _doc?.ClearCells(GetSelectedCells());
                Invalidate();
            }));

            return menu;
        }

        private CellAddress? HitTest(Point pt)
        {
            if (_doc == null) return null;
            int rowHeaderW = GetRowHeaderWidth();
            int colHeaderH = GetColHeaderHeight();

            if (pt.X < rowHeaderW || pt.Y < colHeaderH)
                return null;

            int startCol = _hScrollBar.Value + 1;
            int curX = rowHeaderW;
            int targetCol = -1;
            for (int c = startCol; c <= _doc.DisplayCols; c++)
            {
                int w = GetColWidth(c);
                if (pt.X >= curX && pt.X < curX + w)
                {
                    targetCol = c;
                    break;
                }
                curX += w;
            }

            int startRow = _vScrollBar.Value + 1;
            int curY = colHeaderH;
            int targetRow = -1;
            for (int r = startRow; r <= _doc.DisplayRows; r++)
            {
                int h = GetRowHeight(r);
                if (pt.Y >= curY && pt.Y < curY + h)
                {
                    targetRow = r;
                    break;
                }
                curY += h;
            }

            if (targetRow >= 1 && targetCol >= 1)
            {
                return new CellAddress(targetRow, targetCol);
            }

            return null;
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTTRANSPARENT = -1;

            if (m.Msg == WM_NCHITTEST)
            {
                Form? form = FindForm();
                if (form != null && form.WindowState == FormWindowState.Normal)
                {
                    int screenX = unchecked((short)(long)m.LParam);
                    int screenY = unchecked((short)((long)m.LParam >> 16));
                    Point ptForm = form.PointToClient(new Point(screenX, screenY));
                    int border = 8;
                    // 親フォームの左端または右端境界付近なら透過してリサイズを発動
                    if (ptForm.X <= border || ptForm.X >= form.ClientSize.Width - border)
                    {
                        m.Result = (IntPtr)HTTRANSPARENT;
                        return;
                    }
                }
            }

            base.WndProc(ref m);
        }
    }
}
