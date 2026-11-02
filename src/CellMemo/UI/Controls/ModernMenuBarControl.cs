using System;
using System.Drawing;
using System.Windows.Forms;
using CellMemo.Services;

namespace CellMemo.UI.Controls
{
    public class ModernMenuBarControl : Control
    {
        private ThemePalette _theme;
        private readonly LocalizationService _loc = LocalizationService.Instance;
        private readonly ToolTip _toolTip = new();

        private Rectangle _gearButtonRect;
        private bool _isGearHovered = false;

        public MenuStrip MenuStrip { get; }
        public ToolStripMenuItem FileMenu { get; }
        public ToolStripMenuItem EditMenu { get; }
        public ToolStripMenuItem ViewMenu { get; }
        public ToolStripMenuItem FormulasMenu { get; }

        public event EventHandler? SettingsClicked;

        public ModernMenuBarControl(ThemePalette theme)
        {
            _theme = theme;
            Height = 32;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            MenuStrip = new MenuStrip
            {
                Dock = DockStyle.None,
                Location = new Point(4, 2),
                Height = 28,
                BackColor = Color.Transparent,
                Font = _theme.DefaultFont
            };

            FileMenu = new ToolStripMenuItem();
            EditMenu = new ToolStripMenuItem();
            ViewMenu = new ToolStripMenuItem();
            FormulasMenu = new ToolStripMenuItem();

            MenuStrip.Items.AddRange(new ToolStripItem[] { FileMenu, EditMenu, ViewMenu, FormulasMenu });
            Controls.Add(MenuStrip);

            ApplyThemeToMenuStrip();
            UpdateLabels();

            _loc.LanguageChanged += (s, e) => UpdateLabels();
        }

        public void SetTheme(ThemePalette theme)
        {
            _theme = theme;
            ApplyThemeToMenuStrip();
            Invalidate();
        }

        public void UpdateLabels()
        {
            FileMenu.Text = _loc.Get("Menu.File");
            EditMenu.Text = _loc.Get("Menu.Edit");
            ViewMenu.Text = _loc.Get("Menu.View");
            FormulasMenu.Text = _loc.Get("Menu.Formulas");
            MenuStrip.Font = _theme.DefaultFont;
        }

        private void ApplyThemeToMenuStrip()
        {
            MenuStrip.Renderer = new ModernMenuRenderer(_theme);
            foreach (ToolStripItem item in MenuStrip.Items)
            {
                item.ForeColor = _theme.TextColor;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // 背景塗りつぶし
            g.FillRectangle(_theme.MenuBarBrush, ClientRectangle);

            // 下辺境界線
            g.DrawLine(_theme.BorderPen, 0, Height - 1, Width, Height - 1);

            // 右端設定歯車ボタン (大型化: Width - 38, 2, 32x28 px)
            _gearButtonRect = new Rectangle(Width - 38, 2, 32, 28);
            using var gearPath = CreateRoundedPath(_gearButtonRect, 4);

            if (_isGearHovered)
            {
                g.FillPath(_theme.ControlHoverBrush, gearPath);
                using var hoverBorderPen = new Pen(_theme.AccentGreen, 1.2f);
                g.DrawPath(hoverBorderPen, gearPath);
            }
            else
            {
                // 通常時も薄い枠線でボタンであることを明示
                using var normalBorderPen = new Pen(Color.FromArgb(50, _theme.TextColor), 1f);
                g.DrawPath(normalBorderPen, gearPath);
            }

            // ベクター歯車の描画
            float cx = _gearButtonRect.X + _gearButtonRect.Width / 2f;
            float cy = _gearButtonRect.Y + _gearButtonRect.Height / 2f;
            Color gearColor = _isGearHovered ? _theme.AccentGreen : _theme.TextColor;
            using var gearIconPath = CreateGearPath(cx, cy, outerR: 7.2f, innerR: 5.2f, holeR: 2.2f, teethCount: 8);
            using var gearBrush = new SolidBrush(gearColor);
            g.FillPath(gearBrush, gearIconPath);
        }

        private static System.Drawing.Drawing2D.GraphicsPath CreateGearPath(float cx, float cy, float outerR, float innerR, float holeR, int teethCount)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.FillMode = System.Drawing.Drawing2D.FillMode.Alternate;

            var points = new System.Collections.Generic.List<PointF>();
            int steps = teethCount * 4;
            double stepAngle = (Math.PI * 2) / steps;

            for (int i = 0; i < steps; i++)
            {
                double angle = i * stepAngle;
                float r = (i % 4 == 1 || i % 4 == 2) ? outerR : innerR;
                points.Add(new PointF(
                    cx + (float)(r * Math.Cos(angle)),
                    cy + (float)(r * Math.Sin(angle))
                ));
            }
            path.AddPolygon(points.ToArray());

            path.AddEllipse(cx - holeR, cy - holeR, holeR * 2, holeR * 2);
            return path;
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
                    int border = 12;
                    if (ptForm.X <= border || ptForm.X >= form.ClientSize.Width - border)
                    {
                        m.Result = (IntPtr)HTTRANSPARENT;
                        return;
                    }
                }
            }

            base.WndProc(ref m);
        }

        private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
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
            bool oldGear = _isGearHovered;
            _isGearHovered = _gearButtonRect.Contains(e.Location);
            if (oldGear != _isGearHovered)
            {
                if (_isGearHovered)
                {
                    _toolTip.SetToolTip(this, _loc.Get("Settings.Title"));
                }
                else
                {
                    _toolTip.RemoveAll();
                }
                Invalidate(_gearButtonRect);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isGearHovered)
            {
                _isGearHovered = false;
                Invalidate(_gearButtonRect);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && _gearButtonRect.Contains(e.Location))
            {
                SettingsClicked?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public class ModernMenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly ThemePalette _theme;

        public ModernMenuRenderer(ThemePalette theme) : base(new ModernColorTable(theme))
        {
            _theme = theme;
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected || e.Item.Pressed)
            {
                var rect = new Rectangle(Point.Empty, e.Item.Size);
                e.Graphics.FillRectangle(_theme.ControlHoverBrush, rect);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (!e.Item.Enabled)
            {
                e.TextColor = _theme.SubTextColor;
            }
            else if (e.Item is ToolStripMenuItem mi && !string.IsNullOrEmpty(mi.ShortcutKeyDisplayString) && e.Text == mi.ShortcutKeyDisplayString)
            {
                e.TextColor = _theme.SubTextColor;
            }
            else
            {
                e.TextColor = _theme.TextColor;
            }
            base.OnRenderItemText(e);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (e.ToolStrip is ToolStripDropDown)
            {
                var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
                e.Graphics.DrawRectangle(_theme.BorderPen, rect);
            }
        }
    }

    public class ModernColorTable : ProfessionalColorTable
    {
        private readonly ThemePalette _theme;

        public ModernColorTable(ThemePalette theme)
        {
            _theme = theme;
        }

        public override Color ToolStripDropDownBackground => _theme.MenuBarBackground;
        public override Color MenuBorder => _theme.GridLine;
        public override Color MenuItemSelected => _theme.ControlHoverBackground;
        public override Color MenuItemSelectedGradientBegin => _theme.ControlHoverBackground;
        public override Color MenuItemSelectedGradientEnd => _theme.ControlHoverBackground;
        public override Color MenuItemPressedGradientBegin => _theme.ControlHoverBackground;
        public override Color MenuItemPressedGradientEnd => _theme.ControlHoverBackground;
        public override Color ImageMarginGradientBegin => _theme.MenuBarBackground;
        public override Color ImageMarginGradientMiddle => _theme.MenuBarBackground;
        public override Color ImageMarginGradientEnd => _theme.MenuBarBackground;
        public override Color SeparatorDark => _theme.GridLine;
        public override Color SeparatorLight => _theme.GridLine;
    }
}
