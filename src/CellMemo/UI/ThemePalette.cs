using System;
using System.Drawing;

namespace CellMemo.UI
{
    public class ThemePalette : IDisposable
    {
        public bool IsDark { get; }

        // Colors
        public Color TitleBarBackground { get; }
        public Color MenuBarBackground { get; }
        public Color CellBackground { get; }
        public Color HeaderBackground { get; }
        public Color HeaderActiveBackground { get; }
        public Color AccentGreen { get; }
        public Color HeaderActiveText { get; }
        public Color SelectionOverlay { get; }
        public Color GridLine { get; }
        public Color TextColor { get; }
        public Color SubTextColor { get; }
        public Color StatusBarBackground { get; }
        public Color InactiveTabBackground { get; }
        public Color TabHoverBackground { get; }
        public Color ControlHoverBackground { get; }
        public Color CloseButtonHoverBackground { get; }

        // Cached GDI Objects
        public SolidBrush TitleBarBrush { get; }
        public SolidBrush MenuBarBrush { get; }
        public SolidBrush CellBrush { get; }
        public SolidBrush HeaderBrush { get; }
        public SolidBrush HeaderActiveBrush { get; }
        public SolidBrush SelectionOverlayBrush { get; }
        public SolidBrush TextBrush { get; }
        public SolidBrush SubTextBrush { get; }
        public SolidBrush StatusBarBrush { get; }
        public SolidBrush InactiveTabBrush { get; }
        public SolidBrush TabHoverBrush { get; }
        public SolidBrush AccentGreenBrush { get; }
        public SolidBrush ControlHoverBrush { get; }
        public SolidBrush CloseButtonHoverBrush { get; }

        public Pen GridLinePen { get; }
        public Pen AccentGreenPen2px { get; }
        public Pen AccentGreenPen1px { get; }
        public Pen SeparatorPen { get; }
        public Pen BorderPen { get; }

        public Font DefaultFont { get; }
        public Font BoldFont { get; }
        public Font HeaderFont { get; }
        public Font HeaderActiveFont { get; }
        public Font TabFont { get; }
        public Font TabActiveFont { get; }
        public Font StatusFont { get; }

        public ThemePalette(bool isDark, string fontFamily = "Yu Gothic UI", float baseFontSize = 10f)
        {
            IsDark = isDark;

            if (isDark)
            {
                TitleBarBackground = ColorTranslator.FromHtml("#1F1F1F");
                MenuBarBackground = ColorTranslator.FromHtml("#2C2C2C");
                CellBackground = ColorTranslator.FromHtml("#1E1E1E");
                HeaderBackground = ColorTranslator.FromHtml("#252526");
                HeaderActiveBackground = ColorTranslator.FromHtml("#1E3A2B");
                AccentGreen = ColorTranslator.FromHtml("#2EA043");
                HeaderActiveText = ColorTranslator.FromHtml("#4CC38A");
                SelectionOverlay = Color.FromArgb(51, 46, 160, 67); // rgba(46, 160, 67, 0.20)
                GridLine = ColorTranslator.FromHtml("#383838");
                TextColor = ColorTranslator.FromHtml("#E6E6E6");
                SubTextColor = ColorTranslator.FromHtml("#CCCCCC");
                StatusBarBackground = ColorTranslator.FromHtml("#252526");
                InactiveTabBackground = Color.FromArgb(0, 0, 0, 0);
                TabHoverBackground = Color.FromArgb(40, 255, 255, 255);
                ControlHoverBackground = Color.FromArgb(40, 255, 255, 255);
                CloseButtonHoverBackground = Color.FromArgb(232, 17, 35);
            }
            else
            {
                TitleBarBackground = ColorTranslator.FromHtml("#E8ECF1");
                MenuBarBackground = ColorTranslator.FromHtml("#F9F9F9");
                CellBackground = ColorTranslator.FromHtml("#FFFFFF");
                HeaderBackground = ColorTranslator.FromHtml("#F3F3F3");
                HeaderActiveBackground = ColorTranslator.FromHtml("#E1EFE6");
                AccentGreen = ColorTranslator.FromHtml("#107C41");
                HeaderActiveText = ColorTranslator.FromHtml("#107C41");
                SelectionOverlay = Color.FromArgb(31, 16, 124, 65); // rgba(16, 124, 65, 0.12)
                GridLine = ColorTranslator.FromHtml("#E0E0E0");
                TextColor = ColorTranslator.FromHtml("#1B1B1B");
                SubTextColor = ColorTranslator.FromHtml("#444444");
                StatusBarBackground = ColorTranslator.FromHtml("#F3F3F3");
                InactiveTabBackground = Color.FromArgb(0, 0, 0, 0);
                TabHoverBackground = Color.FromArgb(30, 0, 0, 0);
                ControlHoverBackground = Color.FromArgb(30, 0, 0, 0);
                CloseButtonHoverBackground = Color.FromArgb(232, 17, 35);
            }

            // GDI Brushes
            TitleBarBrush = new SolidBrush(TitleBarBackground);
            MenuBarBrush = new SolidBrush(MenuBarBackground);
            CellBrush = new SolidBrush(CellBackground);
            HeaderBrush = new SolidBrush(HeaderBackground);
            HeaderActiveBrush = new SolidBrush(HeaderActiveBackground);
            SelectionOverlayBrush = new SolidBrush(SelectionOverlay);
            TextBrush = new SolidBrush(TextColor);
            SubTextBrush = new SolidBrush(SubTextColor);
            StatusBarBrush = new SolidBrush(StatusBarBackground);
            InactiveTabBrush = new SolidBrush(InactiveTabBackground);
            TabHoverBrush = new SolidBrush(TabHoverBackground);
            AccentGreenBrush = new SolidBrush(AccentGreen);
            ControlHoverBrush = new SolidBrush(ControlHoverBackground);
            CloseButtonHoverBrush = new SolidBrush(CloseButtonHoverBackground);

            // GDI Pens
            GridLinePen = new Pen(GridLine, 1f);
            AccentGreenPen2px = new Pen(AccentGreen, 2f);
            AccentGreenPen1px = new Pen(AccentGreen, 1f);
            SeparatorPen = new Pen(GridLine, 1f);
            BorderPen = new Pen(GridLine, 1f);

            // GDI Fonts
            string fontName = string.IsNullOrEmpty(fontFamily) ? "Yu Gothic UI" : fontFamily;
            DefaultFont = new Font(fontName, baseFontSize, FontStyle.Regular);
            BoldFont = new Font(fontName, baseFontSize, FontStyle.Bold);
            HeaderFont = new Font(fontName, 9f, FontStyle.Regular);
            HeaderActiveFont = new Font(fontName, 9f, FontStyle.Bold);
            TabFont = new Font(fontName, 9.5f, FontStyle.Regular);
            TabActiveFont = new Font(fontName, 9.5f, FontStyle.Bold);
            StatusFont = new Font(fontName, 9f, FontStyle.Regular);
        }

        public void Dispose()
        {
            TitleBarBrush.Dispose();
            MenuBarBrush.Dispose();
            CellBrush.Dispose();
            HeaderBrush.Dispose();
            HeaderActiveBrush.Dispose();
            SelectionOverlayBrush.Dispose();
            TextBrush.Dispose();
            SubTextBrush.Dispose();
            StatusBarBrush.Dispose();
            InactiveTabBrush.Dispose();
            TabHoverBrush.Dispose();
            AccentGreenBrush.Dispose();
            ControlHoverBrush.Dispose();
            CloseButtonHoverBrush.Dispose();

            GridLinePen.Dispose();
            AccentGreenPen2px.Dispose();
            AccentGreenPen1px.Dispose();
            SeparatorPen.Dispose();
            BorderPen.Dispose();

            DefaultFont.Dispose();
            BoldFont.Dispose();
            HeaderFont.Dispose();
            HeaderActiveFont.Dispose();
            TabFont.Dispose();
            TabActiveFont.Dispose();
            StatusFont.Dispose();
        }
    }
}
