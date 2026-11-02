using System;
using System.Drawing;
using System.Windows.Forms;
using CellMemo.Services;

namespace CellMemo.UI.Controls
{
    public class FindReplaceBar : UserControl
    {
        private ThemePalette _theme;
        private readonly LocalizationService _loc = LocalizationService.Instance;

        private readonly TextBox _findBox;
        private readonly TextBox _replaceBox;
        private readonly Button _btnFindNext;
        private readonly Button _btnFindPrev;
        private readonly Button _btnReplace;
        private readonly Button _btnReplaceAll;
        private readonly CheckBox _chkMatchCase;
        private readonly CheckBox _chkEntireCell;
        private readonly Button _btnClose;
        private readonly Label _statusLabel;

        public event EventHandler<(string text, bool forward, bool matchCase, bool entireCell)>? FindRequested;
        public event EventHandler<(string findText, string replaceText, bool matchCase, bool entireCell)>? ReplaceOneRequested;
        public event EventHandler<(string findText, string replaceText, bool matchCase, bool entireCell)>? ReplaceAllRequested;
        public event EventHandler? CloseRequested;

        public bool IsReplaceMode { get; private set; }

        public FindReplaceBar(ThemePalette theme)
        {
            _theme = theme;
            Width = 420;
            Height = 76;
            BackColor = _theme.MenuBarBackground;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);

            _findBox = new TextBox { Width = 160, Location = new Point(10, 10), Font = _theme.DefaultFont };
            _replaceBox = new TextBox { Width = 160, Location = new Point(10, 40), Font = _theme.DefaultFont };

            _btnFindPrev = new Button { Text = "▲", Width = 28, Height = 24, Location = new Point(176, 9), FlatStyle = FlatStyle.Flat };
            _btnFindNext = new Button { Text = "▼", Width = 28, Height = 24, Location = new Point(208, 9), FlatStyle = FlatStyle.Flat };
            _btnReplace = new Button { Text = "Replace", Width = 66, Height = 24, Location = new Point(176, 39), FlatStyle = FlatStyle.Flat };
            _btnReplaceAll = new Button { Text = "All", Width = 40, Height = 24, Location = new Point(246, 39), FlatStyle = FlatStyle.Flat };

            _chkMatchCase = new CheckBox { Text = "Aa", Width = 45, Location = new Point(246, 12), AutoSize = true };
            _chkEntireCell = new CheckBox { Text = "[ ]", Width = 45, Location = new Point(296, 12), AutoSize = true };

            _btnClose = new Button { Text = "✕", Width = 24, Height = 24, Location = new Point(386, 8), FlatStyle = FlatStyle.Flat };
            _statusLabel = new Label { Location = new Point(292, 42), Width = 118, Height = 20, ForeColor = _theme.SubTextColor };

            Controls.AddRange(new Control[]
            {
                _findBox, _replaceBox,
                _btnFindPrev, _btnFindNext,
                _btnReplace, _btnReplaceAll,
                _chkMatchCase, _chkEntireCell,
                _btnClose, _statusLabel
            });

            _findBox.KeyDown += OnBoxKeyDown;
            _replaceBox.KeyDown += OnBoxKeyDown;

            _btnFindNext.Click += (s, e) => TriggerFind(forward: true);
            _btnFindPrev.Click += (s, e) => TriggerFind(forward: false);
            _btnReplace.Click += (s, e) => TriggerReplace(all: false);
            _btnReplaceAll.Click += (s, e) => TriggerReplace(all: true);
            _btnClose.Click += (s, e) => CloseRequested?.Invoke(this, EventArgs.Empty);

            ApplyTheme();
            UpdateLabels();
        }

        public void SetTheme(ThemePalette theme)
        {
            _theme = theme;
            BackColor = _theme.MenuBarBackground;
            ApplyTheme();
            Invalidate();
        }

        private void ApplyTheme()
        {
            _findBox.BackColor = _theme.CellBackground;
            _findBox.ForeColor = _theme.TextColor;
            _replaceBox.BackColor = _theme.CellBackground;
            _replaceBox.ForeColor = _theme.TextColor;

            foreach (var btn in new[] { _btnFindNext, _btnFindPrev, _btnReplace, _btnReplaceAll, _btnClose })
            {
                btn.BackColor = _theme.MenuBarBackground;
                btn.ForeColor = _theme.TextColor;
                btn.FlatAppearance.BorderColor = _theme.GridLine;
            }

            _chkMatchCase.ForeColor = _theme.TextColor;
            _chkEntireCell.ForeColor = _theme.TextColor;
            _statusLabel.ForeColor = _theme.SubTextColor;
        }

        public void UpdateLabels()
        {
            _btnReplace.Text = _loc.Get("Find.ReplaceOne");
            _btnReplaceAll.Text = _loc.Get("Find.ReplaceAll");
            _chkMatchCase.Text = "Aa";
            _chkEntireCell.Text = "1:1";
        }

        public void ShowBar(bool replaceMode)
        {
            IsReplaceMode = replaceMode;
            _replaceBox.Visible = replaceMode;
            _btnReplace.Visible = replaceMode;
            _btnReplaceAll.Visible = replaceMode;
            Height = replaceMode ? 74 : 44;

            Visible = true;
            BringToFront();
            _findBox.Focus();
            _findBox.SelectAll();
        }

        public void SetStatusMessage(string message)
        {
            _statusLabel.Text = message;
        }

        private void OnBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                TriggerFind(forward: !e.Shift);
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void TriggerFind(bool forward)
        {
            if (string.IsNullOrEmpty(_findBox.Text)) return;
            FindRequested?.Invoke(this, (_findBox.Text, forward, _chkMatchCase.Checked, _chkEntireCell.Checked));
        }

        private void TriggerReplace(bool all)
        {
            if (string.IsNullOrEmpty(_findBox.Text)) return;
            if (all)
            {
                ReplaceAllRequested?.Invoke(this, (_findBox.Text, _replaceBox.Text, _chkMatchCase.Checked, _chkEntireCell.Checked));
            }
            else
            {
                ReplaceOneRequested?.Invoke(this, (_findBox.Text, _replaceBox.Text, _chkMatchCase.Checked, _chkEntireCell.Checked));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var borderPen = new Pen(_theme.AccentGreen, 1.5f);
            e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
        }
    }
}
