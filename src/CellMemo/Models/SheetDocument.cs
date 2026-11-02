using System;
using System.Collections.Generic;
using System.Text;
using CellMemo.Services;

namespace CellMemo.Models
{
    public class SheetDocument
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = "Memo1";
        public string? FilePath { get; set; }
        public bool IsDirty { get; set; }

        public string LineEnding { get; set; } = "CRLF"; // "CRLF", "LF", "CR"
        public Encoding Encoding { get; set; } = new UTF8Encoding(false);
        public bool HasBom { get; set; }

        public int DisplayRows { get; set; } = 1000;
        public int DisplayCols { get; set; } = 26;

        public const int MaxAllowedRows = 1048576;
        public const int MaxAllowedCols = 16384;

        public Dictionary<int, int> CustomColumnWidths { get; } = new();
        public Dictionary<int, int> CustomRowHeights { get; } = new();

        private readonly Dictionary<CellAddress, CellData> _cells = new();
        public IReadOnlyDictionary<CellAddress, CellData> Cells => _cells;

        public UndoRedoManager UndoRedo { get; } = new();

        public event EventHandler? DocumentChanged;

        public SheetDocument(string title = "Memo1")
        {
            Title = title;
        }

        public CellData? GetCell(CellAddress address)
        {
            if (_cells.TryGetValue(address, out var data))
                return data;
            return null;
        }

        public string GetCellText(CellAddress address)
        {
            if (_cells.TryGetValue(address, out var data))
                return data.EvaluatedText;
            return string.Empty;
        }

        public string GetCellRawText(CellAddress address)
        {
            if (_cells.TryGetValue(address, out var data))
                return data.RawText;
            return string.Empty;
        }

        public void EnsureGridBounds(int row, int col)
        {
            if (row >= DisplayRows)
            {
                DisplayRows = Math.Min(MaxAllowedRows, Math.Max(DisplayRows + 500, row + 50));
            }
            if (col >= DisplayCols)
            {
                DisplayCols = Math.Min(MaxAllowedCols, Math.Max(DisplayCols + 10, col + 5));
            }
        }

        public void SetCell(CellAddress address, string newText, bool recordUndo = true)
        {
            if (!address.IsValid) return;
            newText ??= string.Empty;

            _cells.TryGetValue(address, out var oldCell);
            string oldRaw = oldCell?.RawText ?? string.Empty;

            if (oldRaw == newText)
                return;

            EnsureGridBounds(address.Row, address.Col);

            if (recordUndo)
            {
                var oldMap = new Dictionary<CellAddress, CellData?>
                {
                    [address] = oldCell?.Clone()
                };
                var newCell = new CellData(newText);
                var newMap = new Dictionary<CellAddress, CellData?>
                {
                    [address] = newCell.Clone()
                };
                UndoRedo.Record(new CellChangeCommand($"Edit Cell {address}", oldMap, newMap));
            }

            if (string.IsNullOrEmpty(newText))
            {
                _cells.Remove(address);
            }
            else
            {
                var cell = new CellData(newText);
                _cells[address] = cell;
            }

            IsDirty = true;
            RecalculateAllFormulas();
            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetCellsRange(Dictionary<CellAddress, string> changes, string undoDescription = "Edit Cells")
        {
            if (changes == null || changes.Count == 0) return;

            var oldMap = new Dictionary<CellAddress, CellData?>();
            var newMap = new Dictionary<CellAddress, CellData?>();

            foreach (var kvp in changes)
            {
                var addr = kvp.Key;
                if (!addr.IsValid) continue;
                EnsureGridBounds(addr.Row, addr.Col);

                _cells.TryGetValue(addr, out var oldCell);
                oldMap[addr] = oldCell?.Clone();

                string newText = kvp.Value ?? string.Empty;
                if (string.IsNullOrEmpty(newText))
                {
                    newMap[addr] = null;
                    _cells.Remove(addr);
                }
                else
                {
                    var cell = new CellData(newText);
                    newMap[addr] = cell;
                    _cells[addr] = cell;
                }
            }

            UndoRedo.Record(new CellChangeCommand(undoDescription, oldMap, newMap));
            IsDirty = true;
            RecalculateAllFormulas();
            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }

        internal void SetCellInternal(CellAddress address, CellData data)
        {
            EnsureGridBounds(address.Row, address.Col);
            _cells[address] = data;
        }

        internal void RemoveCellInternal(CellAddress address)
        {
            _cells.Remove(address);
        }

        public void ClearCells(IEnumerable<CellAddress> addresses)
        {
            var oldMap = new Dictionary<CellAddress, CellData?>();
            var newMap = new Dictionary<CellAddress, CellData?>();

            foreach (var addr in addresses)
            {
                if (_cells.TryGetValue(addr, out var cell))
                {
                    oldMap[addr] = cell.Clone();
                    newMap[addr] = null;
                    _cells.Remove(addr);
                }
            }

            if (oldMap.Count > 0)
            {
                UndoRedo.Record(new CellChangeCommand("Clear Cells", oldMap, newMap));
                IsDirty = true;
                RecalculateAllFormulas();
                DocumentChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void EvaluateSelectionToValue(IEnumerable<CellAddress> addresses)
        {
            var oldMap = new Dictionary<CellAddress, CellData?>();
            var newMap = new Dictionary<CellAddress, CellData?>();

            foreach (var addr in addresses)
            {
                if (_cells.TryGetValue(addr, out var cell) && cell.IsFormula)
                {
                    oldMap[addr] = cell.Clone();
                    var newCell = new CellData(cell.EvaluatedText, cell.EvaluatedText);
                    newMap[addr] = newCell;
                    _cells[addr] = newCell;
                }
            }

            if (oldMap.Count > 0)
            {
                UndoRedo.Record(new CellChangeCommand("Evaluate Selection to Value", oldMap, newMap));
                IsDirty = true;
                RecalculateAllFormulas();
                DocumentChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void RecalculateAllFormulas()
        {
            var visiting = new HashSet<CellAddress>();

            foreach (var kvp in _cells)
            {
                var addr = kvp.Key;
                var cell = kvp.Value;
                if (cell.IsFormula)
                {
                    cell.EvaluatedText = FormulaEvaluator.Evaluate(cell.RawText, this, addr, visiting);
                    if (double.TryParse(cell.EvaluatedText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double d))
                    {
                        cell.NumericValue = d;
                    }
                    else
                    {
                        cell.NumericValue = null;
                    }
                }
                else
                {
                    cell.EvaluatedText = cell.RawText;
                    if (double.TryParse(cell.RawText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double d))
                    {
                        cell.NumericValue = d;
                    }
                    else
                    {
                        cell.NumericValue = null;
                    }
                }
            }
        }

        public void InsertRow(int rowIndex)
        {
            if (rowIndex < 1) return;
            var cmd = new RowInsertDeleteCommand(rowIndex, isInsert: true);
            cmd.Redo(this);
            UndoRedo.Record(cmd);
            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }

        public void DeleteRow(int rowIndex)
        {
            if (rowIndex < 1) return;
            var deletedCells = new Dictionary<CellAddress, CellData>();
            foreach (var kvp in _cells)
            {
                if (kvp.Key.Row == rowIndex)
                {
                    deletedCells[kvp.Key] = kvp.Value.Clone();
                }
            }
            var cmd = new RowInsertDeleteCommand(rowIndex, isInsert: false, deletedCells);
            cmd.Redo(this);
            UndoRedo.Record(cmd);
            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }

        internal void InsertRowInternal(int rowIndex)
        {
            var toShift = new List<KeyValuePair<CellAddress, CellData>>();
            foreach (var kvp in _cells)
            {
                if (kvp.Key.Row >= rowIndex)
                {
                    toShift.Add(kvp);
                }
            }
            foreach (var item in toShift)
            {
                _cells.Remove(item.Key);
            }
            foreach (var item in toShift)
            {
                var newAddr = new CellAddress(item.Key.Row + 1, item.Key.Col);
                _cells[newAddr] = item.Value;
            }
            EnsureGridBounds(DisplayRows + 1, DisplayCols);
        }

        internal void DeleteRowInternal(int rowIndex)
        {
            var toShift = new List<KeyValuePair<CellAddress, CellData>>();
            foreach (var kvp in _cells)
            {
                if (kvp.Key.Row == rowIndex)
                {
                    // delete
                }
                else if (kvp.Key.Row > rowIndex)
                {
                    toShift.Add(kvp);
                }
            }
            // 削除行のセルを削除
            var rowKeys = new List<CellAddress>();
            foreach (var k in _cells.Keys)
            {
                if (k.Row == rowIndex) rowKeys.Add(k);
            }
            foreach (var k in rowKeys) _cells.Remove(k);

            foreach (var item in toShift)
            {
                _cells.Remove(item.Key);
            }
            foreach (var item in toShift)
            {
                var newAddr = new CellAddress(item.Key.Row - 1, item.Key.Col);
                _cells[newAddr] = item.Value;
            }
        }

        public void InsertColumn(int colIndex)
        {
            if (colIndex < 1) return;
            var cmd = new ColumnInsertDeleteCommand(colIndex, isInsert: true);
            cmd.Redo(this);
            UndoRedo.Record(cmd);
            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }

        public void DeleteColumn(int colIndex)
        {
            if (colIndex < 1) return;
            var deletedCells = new Dictionary<CellAddress, CellData>();
            foreach (var kvp in _cells)
            {
                if (kvp.Key.Col == colIndex)
                {
                    deletedCells[kvp.Key] = kvp.Value.Clone();
                }
            }
            var cmd = new ColumnInsertDeleteCommand(colIndex, isInsert: false, deletedCells);
            cmd.Redo(this);
            UndoRedo.Record(cmd);
            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }

        internal void InsertColumnInternal(int colIndex)
        {
            var toShift = new List<KeyValuePair<CellAddress, CellData>>();
            foreach (var kvp in _cells)
            {
                if (kvp.Key.Col >= colIndex)
                {
                    toShift.Add(kvp);
                }
            }
            foreach (var item in toShift)
            {
                _cells.Remove(item.Key);
            }
            foreach (var item in toShift)
            {
                var newAddr = new CellAddress(item.Key.Row, item.Key.Col + 1);
                _cells[newAddr] = item.Value;
            }
            EnsureGridBounds(DisplayRows, DisplayCols + 1);
        }

        internal void DeleteColumnInternal(int colIndex)
        {
            var toShift = new List<KeyValuePair<CellAddress, CellData>>();
            var colKeys = new List<CellAddress>();
            foreach (var kvp in _cells)
            {
                if (kvp.Key.Col == colIndex)
                {
                    colKeys.Add(kvp.Key);
                }
                else if (kvp.Key.Col > colIndex)
                {
                    toShift.Add(kvp);
                }
            }
            foreach (var k in colKeys) _cells.Remove(k);
            foreach (var item in toShift) _cells.Remove(item.Key);
            foreach (var item in toShift)
            {
                var newAddr = new CellAddress(item.Key.Row, item.Key.Col - 1);
                _cells[newAddr] = item.Value;
            }
        }

        public (int maxRow, int maxCol) GetDimensions()
        {
            int maxRow = 0;
            int maxCol = 0;
            foreach (var addr in _cells.Keys)
            {
                if (addr.Row > maxRow) maxRow = addr.Row;
                if (addr.Col > maxCol) maxCol = addr.Col;
            }
            return (maxRow, maxCol);
        }

        public void LoadDataFromParsedMatrix(
            Dictionary<CellAddress, CellData> newCells,
            string? filePath,
            Encoding encoding,
            string lineEnding,
            bool hasBom)
        {
            _cells.Clear();
            foreach (var kvp in newCells)
            {
                _cells[kvp.Key] = kvp.Value;
            }

            FilePath = filePath;
            if (!string.IsNullOrEmpty(filePath))
            {
                Title = System.IO.Path.GetFileName(filePath);
            }
            Encoding = encoding;
            LineEnding = lineEnding;
            HasBom = hasBom;

            var (mr, mc) = GetDimensions();
            DisplayRows = Math.Max(1000, mr + 50);
            DisplayCols = Math.Max(26, mc + 5);

            RecalculateAllFormulas();
            UndoRedo.Clear();
            IsDirty = false;
            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
