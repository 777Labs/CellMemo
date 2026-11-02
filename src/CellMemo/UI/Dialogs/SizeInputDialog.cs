using System;
using System.Drawing;
using System.Windows.Forms;
using CellMemo.Services;

namespace CellMemo.UI.Dialogs
{
    public class SizeInputDialog : Form
    {
        private readonly NumericUpDown _numSize;
        private readonly Label _lblPoints;
        private readonly ThemePalette _theme;

        public int Value => (int)_numSize.Value;

        public SizeInputDialog(string title, int currentPx, ThemePalette theme, int minVal = 10, int maxVal = 1000)
        {
            _theme = theme;

            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(320, 140);
            BackColor = _theme.MenuBarBackground;
            ForeColor = _theme.TextColor;
            Font = _theme.DefaultFont;

            var loc = LocalizationService.Instance;

            var lblPrompt = new Label
            {
                Text = loc.Get("Dialog.Size.Pixel"),
                Location = new Point(20, 18),
                AutoSize = true,
                ForeColor = _theme.TextColor
            };
            Controls.Add(lblPrompt);

            _numSize = new NumericUpDown
            {
                Location = new Point(20, 42),
                Width = 130,
                Minimum = minVal,
                Maximum = maxVal,
                Value = Math.Clamp(currentPx, minVal, maxVal),
                BackColor = _theme.CellBackground,
                ForeColor = _theme.TextColor
            };
            _numSize.ValueChanged += (s, e) => UpdatePointsLabel();
            Controls.Add(_numSize);

            _lblPoints = new Label
            {
                Location = new Point(160, 44),
                AutoSize = true,
                ForeColor = _theme.SubTextColor
            };
            Controls.Add(_lblPoints);
            UpdatePointsLabel();

            var btnCancel = new Button
            {
                Text = loc.Get("Settings.Cancel"),
                Location = new Point(ClientSize.Width - 90 - 20, 90),
                Width = 90,
                Height = 30,
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderColor = _theme.GridLine;
            Controls.Add(btnCancel);

            var btnOk = new Button
            {
                Text = "OK",
                Location = new Point(ClientSize.Width - 190 - 25, 90),
                Width = 90,
                Height = 30,
                DialogResult = DialogResult.OK,
                FlatStyle = FlatStyle.Flat,
                BackColor = _theme.AccentGreen,
                ForeColor = Color.White
            };
            btnOk.FlatAppearance.BorderSize = 0;
            Controls.Add(btnOk);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void UpdatePointsLabel()
        {
            double pt = Math.Round((double)_numSize.Value * 72.0 / 96.0, 2);
            _lblPoints.Text = $"(約 {pt} pt)";
        }
    }
}
