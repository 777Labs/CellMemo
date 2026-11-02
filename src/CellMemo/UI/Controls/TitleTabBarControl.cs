using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CellMemo.Models;
using CellMemo.Native;

namespace CellMemo.UI.Controls
{
    public class TitleTabBarControl : Control
    {
        private ThemePalette _theme;
        private Image? _appIconImage;
        private readonly List<SheetDocument> _tabs = new();
        private int _selectedTabIndex = 0;

        // ボタン領域のトラッキング
        private Rectangle _newTabButtonRect;
        private Rectangle _minButtonRect;
        private Rectangle _maxButtonRect;
        private Rectangle _closeButtonRect;

        private int _hoveredTabIndex = -1;
        private int _hoveredTabCloseIndex = -1;
        private bool _isNewTabHovered = false;
        private bool _isMinHovered = false;
        private bool _isMaxHovered = false;
        private bool _isCloseHovered = false;

        private Point _dragStartPoint = Point.Empty;
        private bool _isTabPressed = false;

        public event EventHandler<int>? TabSelected;
        public event EventHandler<int>? TabCloseRequested;
        public event EventHandler? NewTabRequested;
        public event EventHandler<int>? TabTitleDoubleClicked;

        public TitleTabBarControl(ThemePalette theme)
        {
            _theme = theme;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            Height = 40;
        }

        public void SetTheme(ThemePalette theme)
        {
            _theme = theme;
            Invalidate();
        }

        public void SetAppIcon(Image? icon)
        {
            _appIconImage = icon;
            Invalidate();
        }

        public void SetTabs(List<SheetDocument> tabs, int selectedIndex)
        {
            _tabs.Clear();
            _tabs.AddRange(tabs);
            _selectedTabIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, _tabs.Count - 1));
            Invalidate();
        }

        public int SelectedIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (_selectedTabIndex != value && value >= 0 && value < _tabs.Count)
                {
                    _selectedTabIndex = value;
                    Invalidate();
                }
            }
        }

        private List<Rectangle> GetTabRectangles()
        {
            var list = new List<Rectangle>();
            int startX = 42; // アプリアイコン右
            int tabY = 8;
            int tabHeight = 32;

            int availableWidth = Width - startX - 35 - (46 * 3); // +ボタンと3つのウィンドウボタン分
            int tabCount = Math.Max(1, _tabs.Count);
            int tabWidth = Math.Clamp(availableWidth / tabCount, 100, 200);

            int currentX = startX;
            for (int i = 0; i < _tabs.Count; i++)
            {
                list.Add(new Rectangle(currentX, tabY, tabWidth, tabHeight));
                currentX += tabWidth + 4;
            }

            _newTabButtonRect = new Rectangle(currentX + 2, 8, 28, 28);
            return list;
        }

        private void UpdateSysButtonRects()
        {
            int btnWidth = 46;
            int btnHeight = Height;
            int right = Width;

            _closeButtonRect = new Rectangle(right - btnWidth, 0, btnWidth, btnHeight);
            _maxButtonRect = new Rectangle(right - btnWidth * 2, 0, btnWidth, btnHeight);
            _minButtonRect = new Rectangle(right - btnWidth * 3, 0, btnWidth, btnHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            UpdateSysButtonRects();

            // 1. タイトルバー背景描画
            g.FillRectangle(_theme.TitleBarBrush, ClientRectangle);

            // 2. アプリアイコン描画 (12, 10, 20x20)
            if (_appIconImage != null)
            {
                g.DrawImage(_appIconImage, new Rectangle(12, 10, 20, 20));
            }

            // 3. 各タブの描画
            var tabRects = GetTabRectangles();
            for (int i = 0; i < _tabs.Count; i++)
            {
                var doc = _tabs[i];
                var rect = tabRects[i];
                bool isSelected = (i == _selectedTabIndex);
                bool isHovered = (i == _hoveredTabIndex);

                DrawTab(g, doc, rect, isSelected, isHovered, i);
            }

            // 4. 新規タブ [+] ボタン
            if (_newTabButtonRect.Width > 0)
            {
                if (_isNewTabHovered)
                {
                    using var path = CreateRoundedPath(_newTabButtonRect, 4);
                    g.FillPath(_theme.ControlHoverBrush, path);
                }
                using var plusPen = new Pen(_theme.TextColor, 1.5f);
                int cx = _newTabButtonRect.X + _newTabButtonRect.Width / 2;
                int cy = _newTabButtonRect.Y + _newTabButtonRect.Height / 2;
                g.DrawLine(plusPen, cx - 5, cy, cx + 5, cy);
                g.DrawLine(plusPen, cx, cy - 5, cx, cy + 5);
            }

            // 5. ウィンドウ制御ボタン (最小化、最大化、閉じる)
            DrawWindowControls(g);
        }

        private void DrawTab(Graphics g, SheetDocument doc, Rectangle rect, bool isSelected, bool isHovered, int tabIndex)
        {
            using var path = CreateTopRoundedPath(rect, 6);

            if (isSelected)
            {
                // アクティブタブ: メニューバー背景と同じ色で塗り、下辺を抜く
                g.FillPath(_theme.MenuBarBrush, path);
            }
            else
            {
                if (isHovered)
                {
                    g.FillPath(_theme.TabHoverBrush, path);
                }
                // 非アクティブタブ間のセパレータ
                if (tabIndex < _tabs.Count - 1 && tabIndex + 1 != _selectedTabIndex)
                {
                    int sepX = rect.Right + 2;
                    int sepY = rect.Y + (rect.Height - 16) / 2;
                    g.DrawLine(_theme.SeparatorPen, sepX, sepY, sepX, sepY + 16);
                }
            }

            // タイトルと未保存インジケーター、閉じるボタン
            int textX = rect.X + 10;
            int textY = rect.Y + (rect.Height - 18) / 2;
            int availableTextWidth = rect.Width - 36;

            // 未保存マーク (•)
            if (doc.IsDirty)
            {
                using var dotBrush = new SolidBrush(_theme.AccentGreen);
                g.FillEllipse(dotBrush, rect.Right - 32, rect.Y + (rect.Height - 6) / 2, 6, 6);
            }

            // タイトル描画
            string title = doc.Title;
            var font = isSelected ? _theme.TabActiveFont : _theme.TabFont;
            var textRect = new Rectangle(textX, textY, availableTextWidth, 18);
            TextRenderer.DrawText(g, title, font, textRect, _theme.TextColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // 閉じるボタン (×)
            var closeRect = GetTabCloseRect(rect);
            if (_hoveredTabCloseIndex == tabIndex)
            {
                using var hoverBrush = new SolidBrush(Color.FromArgb(40, 255, 0, 0));
                using var closeBgPath = CreateRoundedPath(closeRect, 3);
                g.FillPath(hoverBrush, closeBgPath);
            }

            using var closePen = new Pen(_theme.SubTextColor, 1.2f);
            int ccx = closeRect.X + closeRect.Width / 2;
            int ccy = closeRect.Y + closeRect.Height / 2;
            g.DrawLine(closePen, ccx - 3, ccy - 3, ccx + 3, ccy + 3);
            g.DrawLine(closePen, ccx + 3, ccy - 3, ccx - 3, ccy + 3);
        }

        private Rectangle GetTabCloseRect(Rectangle tabRect)
        {
            return new Rectangle(tabRect.Right - 22, tabRect.Y + (tabRect.Height - 16) / 2, 16, 16);
        }

        private void DrawWindowControls(Graphics g)
        {
            // 最小化
            if (_isMinHovered)
                g.FillRectangle(_theme.ControlHoverBrush, _minButtonRect);
            using var minPen = new Pen(_theme.TextColor, 1.2f);
            int minX = _minButtonRect.X + (_minButtonRect.Width - 10) / 2;
            int minY = _minButtonRect.Y + _minButtonRect.Height / 2;
            g.DrawLine(minPen, minX, minY, minX + 10, minY);

            // 最大化 / 復元
            if (_isMaxHovered)
                g.FillRectangle(_theme.ControlHoverBrush, _maxButtonRect);
            Form? parent = FindForm();
            bool isMaximized = parent?.WindowState == FormWindowState.Maximized;
            int maxX = _maxButtonRect.X + (_maxButtonRect.Width - 10) / 2;
            int maxY = _maxButtonRect.Y + (_maxButtonRect.Height - 10) / 2;
            using var maxPen = new Pen(_theme.TextColor, 1.2f);

            if (isMaximized)
            {
                // 復元アイコン (重なった四角形)
                g.DrawRectangle(maxPen, maxX + 2, maxY, 8, 8);
                g.DrawRectangle(maxPen, maxX, maxY + 2, 8, 8);
            }
            else
            {
                g.DrawRectangle(maxPen, maxX, maxY, 10, 10);
            }

            // 閉じる
            if (_isCloseHovered)
                g.FillRectangle(_theme.CloseButtonHoverBrush, _closeButtonRect);
            Color closeColor = _isCloseHovered ? Color.White : _theme.TextColor;
            using var closePen = new Pen(closeColor, 1.2f);
            int clX = _closeButtonRect.X + _closeButtonRect.Width / 2;
            int clY = _closeButtonRect.Y + _closeButtonRect.Height / 2;
            g.DrawLine(closePen, clX - 4, clY - 4, clX + 4, clY + 4);
            g.DrawLine(closePen, clX + 4, clY - 4, clX - 4, clY + 4);
        }

        private static GraphicsPath CreateTopRoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddLine(rect.Right, rect.Bottom, rect.X, rect.Bottom);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // タブをドラッグしてウィンドウを移動
            if (_isTabPressed && e.Button == MouseButtons.Left)
            {
                int dx = Math.Abs(e.X - _dragStartPoint.X);
                int dy = Math.Abs(e.Y - _dragStartPoint.Y);
                if (dx > 4 || dy > 4)
                {
                    _isTabPressed = false;
                    Form? form = FindForm();
                    if (form != null)
                    {
                        const int WM_SYSCOMMAND = 0x0112;
                        const int SC_DRAGMOVE = 0xF012;
                        Win32Api.ReleaseCapture();
                        Win32Api.SendMessage(form.Handle, WM_SYSCOMMAND, (IntPtr)SC_DRAGMOVE, IntPtr.Zero);
                        return;
                    }
                }
            }

            var tabRects = GetTabRectangles();

            int oldHoverTab = _hoveredTabIndex;
            int oldHoverClose = _hoveredTabCloseIndex;
            bool oldNew = _isNewTabHovered;
            bool oldMin = _isMinHovered;
            bool oldMax = _isMaxHovered;
            bool oldCl = _isCloseHovered;

            _hoveredTabIndex = -1;
            _hoveredTabCloseIndex = -1;

            for (int i = 0; i < tabRects.Count; i++)
            {
                if (tabRects[i].Contains(e.Location))
                {
                    _hoveredTabIndex = i;
                    var closeRect = GetTabCloseRect(tabRects[i]);
                    if (closeRect.Contains(e.Location))
                    {
                        _hoveredTabCloseIndex = i;
                    }
                    break;
                }
            }

            _isNewTabHovered = _newTabButtonRect.Contains(e.Location);
            _isMinHovered = _minButtonRect.Contains(e.Location);
            _isMaxHovered = _maxButtonRect.Contains(e.Location);
            _isCloseHovered = _closeButtonRect.Contains(e.Location);

            if (oldHoverTab != _hoveredTabIndex || oldHoverClose != _hoveredTabCloseIndex ||
                oldNew != _isNewTabHovered || oldMin != _isMinHovered ||
                oldMax != _isMaxHovered || oldCl != _isCloseHovered)
            {
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredTabIndex = -1;
            _hoveredTabCloseIndex = -1;
            _isNewTabHovered = false;
            _isMinHovered = false;
            _isMaxHovered = false;
            _isCloseHovered = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Left)
            {
                // ウィンドウ制御ボタン
                Form? form = FindForm();
                if (_minButtonRect.Contains(e.Location))
                {
                    if (form != null) form.WindowState = FormWindowState.Minimized;
                    return;
                }
                if (_maxButtonRect.Contains(e.Location))
                {
                    if (form != null)
                        form.WindowState = form.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
                    return;
                }
                if (_closeButtonRect.Contains(e.Location))
                {
                    form?.Close();
                    return;
                }

                // 新規タブボタン
                if (_newTabButtonRect.Contains(e.Location))
                {
                    NewTabRequested?.Invoke(this, EventArgs.Empty);
                    return;
                }

                // タブクリック
                var tabRects = GetTabRectangles();
                for (int i = 0; i < tabRects.Count; i++)
                {
                    if (tabRects[i].Contains(e.Location))
                    {
                        var closeRect = GetTabCloseRect(tabRects[i]);
                        if (closeRect.Contains(e.Location))
                        {
                            TabCloseRequested?.Invoke(this, i);
                        }
                        else
                        {
                            SelectedIndex = i;
                            TabSelected?.Invoke(this, i);
                            _isTabPressed = true;
                            _dragStartPoint = e.Location;
                        }
                        return;
                    }
                }

                // タブ以外の空白領域ドラッグでウィンドウ移動 (WM_SYSCOMMAND, SC_MOVE + HTCAPTION)
                if (form != null)
                {
                    const int WM_SYSCOMMAND = 0x0112;
                    const int SC_DRAGMOVE = 0xF012; // SC_MOVE (0xF010) | HTCAPTION (0x0002)
                    Win32Api.ReleaseCapture();
                    Win32Api.SendMessage(form.Handle, WM_SYSCOMMAND, (IntPtr)SC_DRAGMOVE, IntPtr.Zero);
                }
            }
            else if (e.Button == MouseButtons.Middle)
            {
                // 中クリックでタブを閉じる
                var tabRects = GetTabRectangles();
                for (int i = 0; i < tabRects.Count; i++)
                {
                    if (tabRects[i].Contains(e.Location))
                    {
                        TabCloseRequested?.Invoke(this, i);
                        return;
                    }
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isTabPressed = false;
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left)
            {
                var tabRects = GetTabRectangles();
                for (int i = 0; i < tabRects.Count; i++)
                {
                    if (tabRects[i].Contains(e.Location))
                    {
                        TabTitleDoubleClicked?.Invoke(this, i);
                        return;
                    }
                }

                // タイトルバー空白部のダブルクリックで最大化トグル
                Form? form = FindForm();
                if (form != null)
                {
                    form.WindowState = form.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
                }
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTTRANSPARENT = -1;
            const int HTCLIENT = 1;

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

                    // 上辺、左上・右上角、左右の端であれば親フォームに透過させてリサイズを発動
                    if (ptForm.Y <= border || ptForm.X <= border || ptForm.X >= form.ClientSize.Width - border ||
                        (ptForm.Y <= corner && (ptForm.X <= corner || ptForm.X >= form.ClientSize.Width - corner)))
                    {
                        m.Result = (IntPtr)HTTRANSPARENT;
                        return;
                    }
                }

                // リサイズ境界以外の領域（タブ、各種ボタン、タイトルバー空白）はすべて HTCLIENT を返し、
                // OnMouseDown での正確なボタン押下判定および WM_SYSCOMMAND (SC_MOVE) ウィンドウ移動を実行
                m.Result = (IntPtr)HTCLIENT;
                return;
            }

            base.WndProc(ref m);
        }
    }
}
