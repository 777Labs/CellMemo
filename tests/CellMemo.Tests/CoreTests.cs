using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CellMemo.Models;
using CellMemo.Services;
using CellMemo.UI;
using Xunit;

namespace CellMemo.Tests
{
    public class CoreTests
    {
        [Theory]
        [InlineData(1, "A")]
        [InlineData(26, "Z")]
        [InlineData(27, "AA")]
        [InlineData(702, "ZZ")]
        [InlineData(703, "AAA")]
        public void CellAddress_ColumnConversion_Works(int colIndex, string expectedName)
        {
            Assert.Equal(expectedName, CellAddress.ToColumnName(colIndex));
            Assert.Equal(colIndex, CellAddress.FromColumnName(expectedName));
        }

        [Fact]
        public void CellAddress_ParseAndFormat_Works()
        {
            Assert.True(CellAddress.TryParse("A1", out var a1));
            Assert.Equal(1, a1.Row);
            Assert.Equal(1, a1.Col);
            Assert.Equal("A1", a1.FormatAddress());

            Assert.True(CellAddress.TryParse("ZZ100", out var zz100));
            Assert.Equal(100, zz100.Row);
            Assert.Equal(702, zz100.Col);
            Assert.Equal("ZZ100", zz100.FormatAddress());

            Assert.True(CellAddress.TryParse("$C$15", out var c15));
            Assert.Equal(15, c15.Row);
            Assert.Equal(3, c15.Col);
        }

        [Fact]
        public void FormulaEvaluator_Arithmetic_Works()
        {
            var doc = new SheetDocument();
            var visiting = new HashSet<CellAddress>();

            string res1 = FormulaEvaluator.Evaluate("=1 + 2 * 3", doc, new CellAddress(1, 1), visiting);
            Assert.Equal("7", res1);

            string res2 = FormulaEvaluator.Evaluate("=(1 + 2) * 3", doc, new CellAddress(1, 1), visiting);
            Assert.Equal("9", res2);

            string res3 = FormulaEvaluator.Evaluate("=2 ^ 3", doc, new CellAddress(1, 1), visiting);
            Assert.Equal("8", res3);

            string res4 = FormulaEvaluator.Evaluate("=-5 + 10", doc, new CellAddress(1, 1), visiting);
            Assert.Equal("5", res4);

            string div0 = FormulaEvaluator.Evaluate("=10 / 0", doc, new CellAddress(1, 1), visiting);
            Assert.Equal("#DIV/0!", div0);
        }

        [Fact]
        public void FormulaEvaluator_Functions_Works()
        {
            var doc = new SheetDocument();
            doc.SetCellInternal(new CellAddress(1, 1), new CellData("10"));
            doc.SetCellInternal(new CellAddress(2, 1), new CellData("20"));
            doc.SetCellInternal(new CellAddress(3, 1), new CellData("30"));
            var visiting = new HashSet<CellAddress>();

            // SUM
            string sumRes = FormulaEvaluator.Evaluate("=SUM(A1:A3)", doc, new CellAddress(4, 1), visiting);
            Assert.Equal("60", sumRes);

            // AVERAGE
            string avgRes = FormulaEvaluator.Evaluate("=AVERAGE(A1:A3)", doc, new CellAddress(4, 1), visiting);
            Assert.Equal("20", avgRes);

            // MIN / MAX
            string minRes = FormulaEvaluator.Evaluate("=MIN(A1:A3)", doc, new CellAddress(4, 1), visiting);
            Assert.Equal("10", minRes);
            string maxRes = FormulaEvaluator.Evaluate("=MAX(A1:A3)", doc, new CellAddress(4, 1), visiting);
            Assert.Equal("30", maxRes);

            // COUNT / COUNTA
            string countRes = FormulaEvaluator.Evaluate("=COUNT(A1:A3)", doc, new CellAddress(4, 1), visiting);
            Assert.Equal("3", countRes);

            // ROUND
            string roundRes = FormulaEvaluator.Evaluate("=ROUND(3.14159, 2)", doc, new CellAddress(4, 1), visiting);
            Assert.Equal("3.14", roundRes);

            // ABS
            string absRes = FormulaEvaluator.Evaluate("=ABS(-42)", doc, new CellAddress(4, 1), visiting);
            Assert.Equal("42", absRes);

            // IF
            string ifRes = FormulaEvaluator.Evaluate("=IF(A1 > 5, \"Large\", \"Small\")", doc, new CellAddress(4, 1), visiting);
            Assert.Equal("Large", ifRes);

            // CONCAT
            string concatRes = FormulaEvaluator.Evaluate("=CONCAT(A1, \"-\", A2)", doc, new CellAddress(4, 1), visiting);
            Assert.Equal("10-20", concatRes);
        }

        [Fact]
        public void FormulaEvaluator_CycleDetection_Works()
        {
            var doc = new SheetDocument();
            // A1 = =B1
            doc.SetCellInternal(new CellAddress(1, 1), new CellData("=B1"));
            // B1 = =A1
            doc.SetCellInternal(new CellAddress(1, 2), new CellData("=A1"));

            doc.RecalculateAllFormulas();

            var a1 = doc.GetCell(new CellAddress(1, 1));
            var b1 = doc.GetCell(new CellAddress(1, 2));

            Assert.True(a1?.HasError);
            Assert.Equal("#CYCLE!", a1?.EvaluatedText);
        }

        [Fact]
        public void CsvTsvSerializer_Rfc4180_QuotesAndNewlines()
        {
            string csvContent = "Name,Description,Age\r\nAlice,\"Hello, \"\"World\"\"!\",25\r\nBob,\"Line1\r\nLine2\",30\r\n";
            var result = CsvTsvSerializer.ParseDelimitedContent(csvContent, ',');

            Assert.Equal(3, result.MaxRow);
            Assert.Equal(3, result.MaxCol);

            // Check Alice
            Assert.Equal("Alice", result.Cells[new CellAddress(2, 1)].RawText);
            Assert.Equal("Hello, \"World\"!", result.Cells[new CellAddress(2, 2)].RawText);
            Assert.Equal("25", result.Cells[new CellAddress(2, 3)].RawText);

            // Check Bob multiline
            Assert.Equal("Bob", result.Cells[new CellAddress(3, 1)].RawText);
            Assert.Equal("Line1\r\nLine2", result.Cells[new CellAddress(3, 2)].RawText);
            Assert.Equal("30", result.Cells[new CellAddress(3, 3)].RawText);
        }

        [Fact]
        public void UndoRedo_CellEdit_Works()
        {
            var doc = new SheetDocument();
            var addr = new CellAddress(1, 1);

            doc.SetCell(addr, "Hello");
            Assert.Equal("Hello", doc.GetCellText(addr));
            Assert.True(doc.UndoRedo.CanUndo);

            doc.UndoRedo.Undo(doc);
            Assert.Empty(doc.GetCellText(addr));
            Assert.True(doc.UndoRedo.CanRedo);

            doc.UndoRedo.Redo(doc);
            Assert.Equal("Hello", doc.GetCellText(addr));
        }

        [Fact]
        public void RowColumn_InsertDelete_Works()
        {
            var doc = new SheetDocument();
            doc.SetCell(new CellAddress(1, 1), "A1");
            doc.SetCell(new CellAddress(2, 1), "A2");
            doc.SetCell(new CellAddress(1, 2), "B1");

            // 行2に上に行を挿入 -> 元のA2がA3へ移動、A2は空
            doc.InsertRow(2);
            Assert.Equal("A1", doc.GetCellText(new CellAddress(1, 1)));
            Assert.Empty(doc.GetCellText(new CellAddress(2, 1)));
            Assert.Equal("A2", doc.GetCellText(new CellAddress(3, 1)));

            // Undo で元に戻る
            doc.UndoRedo.Undo(doc);
            Assert.Equal("A2", doc.GetCellText(new CellAddress(2, 1)));

            // 列1に左に列を挿入 -> 元のA1がB1へ、元B1がC1へ移動
            doc.InsertColumn(1);
            Assert.Empty(doc.GetCellText(new CellAddress(1, 1)));
            Assert.Equal("A1", doc.GetCellText(new CellAddress(1, 2)));
            Assert.Equal("B1", doc.GetCellText(new CellAddress(1, 3)));

            // Undo で元に戻る
            doc.UndoRedo.Undo(doc);
            Assert.Equal("A1", doc.GetCellText(new CellAddress(1, 1)));
            Assert.Equal("B1", doc.GetCellText(new CellAddress(1, 2)));
        }

        [Fact]
        public void VirtualGridControl_SelectionModes_Works()
        {
            var theme = new ThemePalette(isDark: false, "Yu Gothic UI", 10.0f);
            var grid = new UI.Controls.VirtualGridControl(theme);
            var doc = new SheetDocument();
            grid.SetDocument(doc);

            // 列選択
            grid.SelectColumns(2, 4);
            Assert.Equal(UI.Controls.VirtualGridControl.GridSelectionType.Columns, grid.CurrentSelectionType);
            var (colStart, colEnd) = grid.GetNormalizedSelection();
            Assert.Equal(2, colStart.Col);
            Assert.Equal(4, colEnd.Col);
            Assert.Equal(1, colStart.Row);
            Assert.Equal(doc.DisplayRows, colEnd.Row);

            // 行選択
            grid.SelectRows(3, 5);
            Assert.Equal(UI.Controls.VirtualGridControl.GridSelectionType.Rows, grid.CurrentSelectionType);
            var (rowStart, rowEnd) = grid.GetNormalizedSelection();
            Assert.Equal(3, rowStart.Row);
            Assert.Equal(5, rowEnd.Row);
            Assert.Equal(1, rowStart.Col);
            Assert.Equal(doc.DisplayCols, rowEnd.Col);

            // 全選択
            grid.SelectAll();
            Assert.Equal(UI.Controls.VirtualGridControl.GridSelectionType.All, grid.CurrentSelectionType);
            var (allStart, allEnd) = grid.GetNormalizedSelection();
            Assert.Equal(1, allStart.Row);
            Assert.Equal(1, allStart.Col);
            Assert.Equal(doc.DisplayRows, allEnd.Row);
            Assert.Equal(doc.DisplayCols, allEnd.Col);
        }

        [Fact]
        public void SizeInputDialog_InitializesCorrectly()
        {
            var theme = new ThemePalette(isDark: true, "Segoe UI", 10.0f);
            using var dlg = new UI.Dialogs.SizeInputDialog("行の高さ", 45, theme, minVal: 10, maxVal: 500);
            Assert.Equal(45, dlg.Value);
            Assert.Equal("行の高さ", dlg.Text);
        }

        [Fact]
        public void VirtualGridControl_ContextMenu_SeparatesShortcutsAndMatchesState()
        {
            var theme = new ThemePalette(isDark: false, "Yu Gothic UI", 10.0f);
            var grid = new UI.Controls.VirtualGridControl(theme);
            var doc = new SheetDocument();
            grid.SetDocument(doc);

            // 1. セル選択時
            var cellMenu = grid.BuildContextMenu();
            var cutItem = cellMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(i => i.ShortcutKeyDisplayString == "Ctrl+X");
            Assert.NotNull(cutItem);
            Assert.Equal("切り取り", cutItem.Text); // 密着していない純粋な項目名
            Assert.Equal("Ctrl+X", cutItem.ShortcutKeyDisplayString);

            var deleteItem = cellMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(i => i.ShortcutKeyDisplayString == "Delete");
            Assert.NotNull(deleteItem);
            Assert.Equal("セルの内容を消去", deleteItem.Text);
            Assert.Equal("Delete", deleteItem.ShortcutKeyDisplayString);

            // セル選択時は行の高さ・列の幅の両方が存在
            Assert.Contains(cellMenu.Items.OfType<ToolStripMenuItem>(), i => i.Text == "行の高さ...");
            Assert.Contains(cellMenu.Items.OfType<ToolStripMenuItem>(), i => i.Text == "列の幅...");

            // 2. 行選択時
            grid.SelectRows(2, 3);
            var rowMenu = grid.BuildContextMenu();
            Assert.Contains(rowMenu.Items.OfType<ToolStripMenuItem>(), i => i.Text == "行の高さ...");
            Assert.DoesNotContain(rowMenu.Items.OfType<ToolStripMenuItem>(), i => i.Text == "列の幅...");

            // 3. 列選択時
            grid.SelectColumns(2, 4);
            var colMenu = grid.BuildContextMenu();
            Assert.DoesNotContain(colMenu.Items.OfType<ToolStripMenuItem>(), i => i.Text == "行の高さ...");
            Assert.Contains(colMenu.Items.OfType<ToolStripMenuItem>(), i => i.Text == "列の幅...");
        }
    }
}
