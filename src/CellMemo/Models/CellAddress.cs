using System;
using System.Text;
using System.Text.RegularExpressions;

namespace CellMemo.Models
{
    public readonly record struct CellAddress(int Row, int Col) : IComparable<CellAddress>
    {
        public static readonly CellAddress Empty = new(0, 0);

        public bool IsValid => Row >= 1 && Col >= 1;

        public string ColumnName => ToColumnName(Col);

        public string FormatAddress() => $"{ColumnName}{Row}";

        public string ToA1() => FormatAddress();

        public override string ToString() => FormatAddress();

        public int CompareTo(CellAddress other)
        {
            int rowCmp = Row.CompareTo(other.Row);
            if (rowCmp != 0) return rowCmp;
            return Col.CompareTo(other.Col);
        }

        public static string ToColumnName(int colIndex)
        {
            if (colIndex < 1) return string.Empty;
            var sb = new StringBuilder();
            while (colIndex > 0)
            {
                colIndex--;
                sb.Insert(0, (char)('A' + (colIndex % 26)));
                colIndex /= 26;
            }
            return sb.ToString();
        }

        public static int FromColumnName(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName)) return 0;
            int sum = 0;
            foreach (char c in columnName.ToUpperInvariant())
            {
                if (c < 'A' || c > 'Z') return 0;
                sum = sum * 26 + (c - 'A' + 1);
            }
            return sum;
        }

        private static readonly Regex CellAddressRegex = new(@"^\$?([A-Za-z]+)\$?([1-9][0-9]*)$", RegexOptions.Compiled);

        public static bool TryParse(string text, out CellAddress address)
        {
            address = Empty;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var match = CellAddressRegex.Match(text.Trim());
            if (!match.Success) return false;

            int col = FromColumnName(match.Groups[1].Value);
            if (col < 1 || !int.TryParse(match.Groups[2].Value, out int row) || row < 1)
            {
                return false;
            }

            address = new CellAddress(row, col);
            return true;
        }

        public static CellAddress Parse(string text)
        {
            if (TryParse(text, out var addr))
                return addr;
            throw new FormatException($"Invalid cell reference: '{text}'");
        }
    }
}
