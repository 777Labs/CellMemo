using System;
using System.Text.Json.Serialization;

namespace CellMemo.Models
{
    public class WindowBoundsConfig
    {
        public int X { get; set; } = 120;
        public int Y { get; set; } = 100;
        public int Width { get; set; } = 1100;
        public int Height { get; set; } = 700;
        public bool IsMaximized { get; set; } = false;
    }

    public class AppSettings
    {
        public string Theme { get; set; } = "Light"; // "Light" or "Dark"
        public string Language { get; set; } = "ja-JP"; // "ja-JP" or "en-US"
        public string FontFamily { get; set; } = "Yu Gothic UI";
        public float FontSize { get; set; } = 10.0f;
        public string DefaultEncoding { get; set; } = "UTF-8";
        public string DefaultLineEnding { get; set; } = "CRLF";
        public bool SaveFormulasAsRawText { get; set; } = true;
        public bool AutoSaveSession { get; set; } = true;
        public WindowBoundsConfig WindowBounds { get; set; } = new();
    }
}
