using System;
using System.Drawing;
using System.Windows.Forms;
using CellMemo.Models;
using CellMemo.Services;

namespace CellMemo.UI.Controls
{
    public class StatusBarControl : Control
    {
        private ThemePalette _theme;
        private readonly LocalizationService _loc = LocalizationService.Instance;

        private CellAddress _activeCell = new(1, 1);
        private int _characterCount = 0;
        private bool _isRangeSelected = false;
        private double _selectionSum = 0;
        private double _selectionAvg = 0;
        private int _selectionCount = 0;

        private int _zoomPercent = 100;
        private string _lineEnding = "Windows (CRLF)";
        private string _encodingName = "UTF-8";

        // セクション矩形
        private Rectangle _posRect;
        private Rectangle _statsRect;
        private Rectangle _zoomRect;
        private Rectangle _lineEndingRect;
        private Rectangle _encodingRect;

        private int _hoveredSectionIndex = -1; // 0: pos, 1: stats, 2: zoom, 3: lineEnding, 4: encoding

        public event EventHandler<int>? ZoomChanged;
        public event EventHandler<string>? LineEndingChanged;
        public event EventHandler<string>? EncodingChanged;

        public StatusBarControl(ThemePalette theme)
        {
            _theme = theme;
            Height = 28;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
        }

        public void SetTheme(ThemePalette theme)
        {
            _theme = theme;
            Invalidate();
        }

        public void UpdatePosition(CellAddress addr)
        {
            _activeCell = addr;
            Invalidate();
        }

        public void UpdateSingleCellStats(int charCount)
        {
            _isRangeSelected = false;
            _characterCount = charCount;
            Invalidate();
        }

        public void UpdateRangeStats(double sum, double avg, int count, int totalChars)
        {
            _isRangeSelected = true;
            _selectionSum = sum;
            _selectionAvg = avg;
            _selectionCount = count;
            _characterCount = totalChars;
            Invalidate();
        }

        public void UpdateZoom(int percent)
        {
            _zoomPercent = percent;
            Invalidate();
        }

        public void UpdateLineEnding(string ending)
        {
            _lineEnding = ending switch
            {
                "LF" => "Unix (LF)",
                "CR" => "Macintosh (CR)",
                _ => "Windows (CRLF)"
            };
            Invalidate();
        }

        public void UpdateEncoding(string enc, bool hasBom)
        {
            if (enc.Contains("utf-8", StringComparison.OrdinalIgnoreCase))
            {
                _encodingName = hasBom ? "UTF-8 BOM" : "UTF-8";
            }
            else if (enc.Contains("932", StringComparison.OrdinalIgnoreCase) || enc.Contains("shift", StringComparison.OrdinalIgnoreCase))
            {
                _encodingName = "Shift_JIS";
            }
            else if (enc.Contains("unicode", StringComparison.OrdinalIgnoreCase) || enc.Contains("utf-16", StringComparison.OrdinalIgnoreCase))
            {
                _encodingName = enc.Contains("big", StringComparison.OrdinalIgnoreCase) ? "UTF-16 BE" : "UTF-16 LE";
            }
            else
            {
                _encodingName = enc;
            }
            Invalidate();
        }

        private void CalculateLayout()
        {
            int h = Height;
            int right = Width;

            int encWidth = 100;
            int leWidth = 130;
            int zoomWidth = 70;
            int posWidth = 130;

            _encodingRect = new Rectangle(right - encWidth, 0, encWidth, h);
            _lineEndingRect = new Rectangle(right - encWidth - leWidth, 0, leWidth, h);
            _zoomRect = new Rectangle(right - encWidth - leWidth - zoomWidth, 0, zoomWidth, h);
            _posRect = new Rectangle(0, 0, posWidth, h);

            int statsX = posWidth;
            int statsWidth = Math.Max(0, right - encWidth - leWidth - zoomWidth - statsX);
            _statsRect = new Rectangle(statsX, 0, statsWidth, h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            CalculateLayout();

            // 背景描画
            g.FillRectangle(_theme.StatusBarBrush, ClientRectangle);

            // 上辺境界線
            g.DrawLine(_theme.BorderPen, 0, 0, Width, 0);

            // セクション描画
            DrawSection(g, _posRect, _loc.Get("Status.Position", _activeCell.Row, _activeCell.Col), 0, isClickable: false);
            DrawSeparator(g, _posRect.Right);

            string statsText;
            if (_isRangeSelected && _selectionCount > 0)
            {
                statsText = $"{_loc.Get("Status.Characters", _characterCount)}   |   {_loc.Get("Status.Stats", _selectionSum, Math.Round(_selectionAvg, 2), _selectionCount)}";
            }
            else
            {
                statsText = _loc.Get("Status.Characters", _characterCount);
            }
            DrawSection(g, _statsRect, statsText, 1, isClickable: false, alignLeft: true);
            DrawSeparator(g, _statsRect.Right);

            DrawSection(g, _zoomRect, $"{_zoomPercent}%", 2, isClickable: true);
            DrawSeparator(g, _zoomRect.Right);

            DrawSection(g, _lineEndingRect, _lineEnding, 3, isClickable: true);
            DrawSeparator(g, _lineEndingRect.Right);

            DrawSection(g, _encodingRect, _encodingName, 4, isClickable: true);
        }

        private void DrawSection(Graphics g, Rectangle rect, string text, int sectionIndex, bool isClickable, bool alignLeft = false)
        {
            if (isClickable && _hoveredSectionIndex == sectionIndex)
            {
                g.FillRectangle(_theme.ControlHoverBrush, rect);
            }

            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
            if (alignLeft)
            {
                flags |= TextFormatFlags.Left;
                rect.X += 10;
                rect.Width -= 10;
            }
            else
            {
                flags |= TextFormatFlags.HorizontalCenter;
            }

            TextRenderer.DrawText(g, text, _theme.StatusFont, rect, _theme.SubTextColor, flags);
        }

        private void DrawSeparator(Graphics g, int x)
        {
            int sepY = (Height - 14) / 2;
            g.DrawLine(_theme.SeparatorPen, x, sepY, x, sepY + 14);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int old = _hoveredSectionIndex;
            _hoveredSectionIndex = -1;

            if (_zoomRect.Contains(e.Location)) _hoveredSectionIndex = 2;
            else if (_lineEndingRect.Contains(e.Location)) _hoveredSectionIndex = 3;
            else if (_encodingRect.Contains(e.Location)) _hoveredSectionIndex = 4;

            if (old != _hoveredSectionIndex)
            {
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredSectionIndex != -1)
            {
                _hoveredSectionIndex = -1;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_zoomRect.Contains(e.Location))
                {
                    ShowZoomMenu();
                }
                else if (_lineEndingRect.Contains(e.Location))
                {
                    ShowLineEndingMenu();
                }
                else if (_encodingRect.Contains(e.Location))
                {
                    ShowEncodingMenu();
                }
            }
        }

        private void ShowZoomMenu()
        {
            var menu = new ContextMenuStrip { Renderer = new ModernMenuRenderer(_theme), Font = _theme.DefaultFont };
            int[] zooms = { 50, 75, 100, 125, 150, 200 };
            foreach (int z in zooms)
            {
                var item = new ToolStripMenuItem($"{z}%") { Checked = (_zoomPercent == z) };
                int currentZ = z;
                item.Click += (s, e) => ZoomChanged?.Invoke(this, currentZ);
                menu.Items.Add(item);
            }
            menu.Show(this, new Point(_zoomRect.X, 0), ToolStripDropDownDirection.AboveRight);
        }

        private void ShowLineEndingMenu()
        {
            var menu = new ContextMenuStrip { Renderer = new ModernMenuRenderer(_theme), Font = _theme.DefaultFont };
            string[] endings = { "Windows (CRLF)", "Unix (LF)", "Macintosh (CR)" };
            foreach (string le in endings)
            {
                var item = new ToolStripMenuItem(le) { Checked = (_lineEnding == le) };
                string code = le switch
                {
                    "Unix (LF)" => "LF",
                    "Macintosh (CR)" => "CR",
                    _ => "CRLF"
                };
                item.Click += (s, e) => LineEndingChanged?.Invoke(this, code);
                menu.Items.Add(item);
            }
            menu.Show(this, new Point(_lineEndingRect.X, 0), ToolStripDropDownDirection.AboveRight);
        }

        private void ShowEncodingMenu()
        {
            var menu = new ContextMenuStrip { Renderer = new ModernMenuRenderer(_theme), Font = _theme.DefaultFont };
            string[] encs = { "UTF-8", "UTF-8 BOM", "Shift_JIS", "UTF-16 LE", "UTF-16 BE" };
            foreach (string enc in encs)
            {
                var item = new ToolStripMenuItem(enc) { Checked = (_encodingName == enc) };
                string currentEnc = enc;
                item.Click += (s, e) => EncodingChanged?.Invoke(this, currentEnc);
                menu.Items.Add(item);
            }
            menu.Show(this, new Point(_encodingRect.X, 0), ToolStripDropDownDirection.AboveRight);
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
                    int corner = 14;

                    // 下辺、左下角、右下角、左右の端なら親フォームに透過させてリサイズ
                    if (ptForm.Y >= form.ClientSize.Height - border ||
                        ptForm.X <= border || ptForm.X >= form.ClientSize.Width - border ||
                        (ptForm.Y >= form.ClientSize.Height - corner && (ptForm.X <= corner || ptForm.X >= form.ClientSize.Width - corner)))
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
