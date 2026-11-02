using System;
using System.Collections.Generic;
using CellMemo.Models;

namespace CellMemo.Services
{
    public interface IUndoableCommand
    {
        string Description { get; }
        void Undo(SheetDocument doc);
        void Redo(SheetDocument doc);
    }

    public class CellChangeCommand : IUndoableCommand
    {
        public string Description { get; }
        private readonly Dictionary<CellAddress, CellData?> _oldValues;
        private readonly Dictionary<CellAddress, CellData?> _newValues;

        public CellChangeCommand(string description,
            Dictionary<CellAddress, CellData?> oldValues,
            Dictionary<CellAddress, CellData?> newValues)
        {
            Description = description;
            _oldValues = oldValues;
            _newValues = newValues;
        }

        public void Undo(SheetDocument doc)
        {
            foreach (var kvp in _oldValues)
            {
                if (kvp.Value == null || string.IsNullOrEmpty(kvp.Value.RawText))
                {
                    doc.RemoveCellInternal(kvp.Key);
                }
                else
                {
                    doc.SetCellInternal(kvp.Key, kvp.Value.Clone());
                }
            }
            doc.RecalculateAllFormulas();
            doc.IsDirty = true;
        }

        public void Redo(SheetDocument doc)
        {
            foreach (var kvp in _newValues)
            {
                if (kvp.Value == null || string.IsNullOrEmpty(kvp.Value.RawText))
                {
                    doc.RemoveCellInternal(kvp.Key);
                }
                else
                {
                    doc.SetCellInternal(kvp.Key, kvp.Value.Clone());
                }
            }
            doc.RecalculateAllFormulas();
            doc.IsDirty = true;
        }
    }

    public class RowInsertDeleteCommand : IUndoableCommand
    {
        public string Description { get; }
        private readonly int _rowIndex;
        private readonly bool _isInsert;
        private readonly Dictionary<CellAddress, CellData> _deletedRowCells;

        public RowInsertDeleteCommand(int rowIndex, bool isInsert, Dictionary<CellAddress, CellData>? deletedCells = null)
        {
            _rowIndex = rowIndex;
            _isInsert = isInsert;
            _deletedRowCells = deletedCells ?? new Dictionary<CellAddress, CellData>();
            Description = isInsert ? $"Insert Row {_rowIndex}" : $"Delete Row {_rowIndex}";
        }

        public void Undo(SheetDocument doc)
        {
            if (_isInsert)
            {
                doc.DeleteRowInternal(_rowIndex);
            }
            else
            {
                doc.InsertRowInternal(_rowIndex);
                foreach (var kvp in _deletedRowCells)
                {
                    doc.SetCellInternal(kvp.Key, kvp.Value.Clone());
                }
            }
            doc.RecalculateAllFormulas();
            doc.IsDirty = true;
        }

        public void Redo(SheetDocument doc)
        {
            if (_isInsert)
            {
                doc.InsertRowInternal(_rowIndex);
            }
            else
            {
                doc.DeleteRowInternal(_rowIndex);
            }
            doc.RecalculateAllFormulas();
            doc.IsDirty = true;
        }
    }

    public class ColumnInsertDeleteCommand : IUndoableCommand
    {
        public string Description { get; }
        private readonly int _colIndex;
        private readonly bool _isInsert;
        private readonly Dictionary<CellAddress, CellData> _deletedColCells;

        public ColumnInsertDeleteCommand(int colIndex, bool isInsert, Dictionary<CellAddress, CellData>? deletedCells = null)
        {
            _colIndex = colIndex;
            _isInsert = isInsert;
            _deletedColCells = deletedCells ?? new Dictionary<CellAddress, CellData>();
            Description = isInsert ? $"Insert Column {CellAddress.ToColumnName(_colIndex)}" : $"Delete Column {CellAddress.ToColumnName(_colIndex)}";
        }

        public void Undo(SheetDocument doc)
        {
            if (_isInsert)
            {
                doc.DeleteColumnInternal(_colIndex);
            }
            else
            {
                doc.InsertColumnInternal(_colIndex);
                foreach (var kvp in _deletedColCells)
                {
                    doc.SetCellInternal(kvp.Key, kvp.Value.Clone());
                }
            }
            doc.RecalculateAllFormulas();
            doc.IsDirty = true;
        }

        public void Redo(SheetDocument doc)
        {
            if (_isInsert)
            {
                doc.InsertColumnInternal(_colIndex);
            }
            else
            {
                doc.DeleteColumnInternal(_colIndex);
            }
            doc.RecalculateAllFormulas();
            doc.IsDirty = true;
        }
    }

    public class UndoRedoManager
    {
        private const int MaxHistory = 100;
        private readonly List<IUndoableCommand> _undoList = new();
        private readonly List<IUndoableCommand> _redoList = new();

        public bool CanUndo => _undoList.Count > 0;
        public bool CanRedo => _redoList.Count > 0;

        public event EventHandler? StateChanged;

        public void Record(IUndoableCommand command)
        {
            _undoList.Add(command);
            if (_undoList.Count > MaxHistory)
            {
                _undoList.RemoveAt(0);
            }
            _redoList.Clear();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Undo(SheetDocument doc)
        {
            if (!CanUndo) return;
            var cmd = _undoList[^1];
            _undoList.RemoveAt(_undoList.Count - 1);
            cmd.Undo(doc);
            _redoList.Add(cmd);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Redo(SheetDocument doc)
        {
            if (!CanRedo) return;
            var cmd = _redoList[^1];
            _redoList.RemoveAt(_redoList.Count - 1);
            cmd.Redo(doc);
            _undoList.Add(cmd);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            _undoList.Clear();
            _redoList.Clear();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
