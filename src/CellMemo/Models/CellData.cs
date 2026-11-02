using System;

namespace CellMemo.Models
{
    public class CellData
    {
        public string RawText { get; set; } = string.Empty;
        public string EvaluatedText { get; set; } = string.Empty;
        public bool IsFormula => RawText.StartsWith("=") && RawText.Length > 1;
        public double? NumericValue { get; set; }
        public bool HasError => EvaluatedText.StartsWith("#");

        public CellData() { }

        public CellData(string rawText, string? evaluatedText = null)
        {
            RawText = rawText ?? string.Empty;
            EvaluatedText = evaluatedText ?? rawText ?? string.Empty;
        }

        public CellData Clone()
        {
            return new CellData
            {
                RawText = this.RawText,
                EvaluatedText = this.EvaluatedText,
                NumericValue = this.NumericValue
            };
        }
    }
}
