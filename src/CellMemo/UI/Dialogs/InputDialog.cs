using System;
using System.Drawing;
using System.Windows.Forms;
using CellMemo.UI;

namespace CellMemo.UI.Dialogs
{
    public class InputDialog : Form
    {
        private readonly TextBox _textBox;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;

        public string InputText => _textBox.Text;

        public InputDialog(string prompt, string title, string defaultValue, ThemePalette theme)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(380, 140);
            BackColor = theme.MenuBarBackground;
            ForeColor = theme.TextColor;
            Font = theme.DefaultFont;

            var lbl = new Label
            {
                Text = prompt,
                Location = new Point(16, 16),
                AutoSize = true,
                ForeColor = theme.TextColor
            };

            _textBox = new TextBox
            {
                Location = new Point(16, 44),
                Width = 348,
                Text = defaultValue,
                BackColor = theme.CellBackground,
                ForeColor = theme.TextColor,
                Font = theme.DefaultFont
            };

            _btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(284, 88),
                Width = 80,
                Height = 28,
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat
            };
            _btnCancel.FlatAppearance.BorderColor = theme.GridLine;

            _btnOk = new Button
            {
                Text = "OK",
                Location = new Point(194, 88),
                Width = 80,
                Height = 28,
                DialogResult = DialogResult.OK,
                FlatStyle = FlatStyle.Flat,
                BackColor = theme.AccentGreen,
                ForeColor = Color.White
            };
            _btnOk.FlatAppearance.BorderSize = 0;

            Controls.AddRange(new Control[] { lbl, _textBox, _btnOk, _btnCancel });

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
        }

        public static string? Show(IWin32Window owner, string prompt, string title, string defaultValue, ThemePalette theme)
        {
            using var dlg = new InputDialog(prompt, title, defaultValue, theme);
            if (dlg.ShowDialog(owner) == DialogResult.OK)
            {
                return dlg.InputText;
            }
            return null;
        }
    }
}
