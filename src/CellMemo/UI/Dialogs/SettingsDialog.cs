using System;
using System.Drawing;
using System.Windows.Forms;
using CellMemo.Models;
using CellMemo.Services;

namespace CellMemo.UI.Dialogs
{
    public class SettingsDialog : Form
    {
        private readonly ThemePalette _theme;
        private readonly LocalizationService _loc = LocalizationService.Instance;
        private readonly AppSettings _settings;

        private readonly ComboBox _cmbTheme;
        private readonly ComboBox _cmbLanguage;
        private readonly ComboBox _cmbFontFamily;
        private readonly NumericUpDown _numFontSize;
        private readonly ComboBox _cmbDefaultEncoding;
        private readonly ComboBox _cmbDefaultLineEnding;
        private readonly CheckBox _chkSaveFormulasAsRawText;
        private readonly CheckBox _chkAutoSaveSession;

        private readonly Button _btnSave;
        private readonly Button _btnCancel;

        public bool IsThemeChanged { get; private set; }
        public bool IsFontChanged { get; private set; }
        public bool IsLanguageChanged { get; private set; }

        public SettingsDialog(AppSettings settings, ThemePalette theme)
        {
            _settings = settings;
            _theme = theme;

            Text = _loc.Get("Settings.Title");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(480, 390);
            BackColor = _theme.MenuBarBackground;
            ForeColor = _theme.TextColor;
            Font = _theme.DefaultFont;

            int labelX = 24;
            int controlX = 220;
            int curY = 20;
            int rowHeight = 36;

            // 1. テーマ
            Controls.Add(CreateLabel(_loc.Get("Settings.Theme"), labelX, curY));
            _cmbTheme = new ComboBox { Location = new Point(controlX, curY), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbTheme.Items.AddRange(new object[] { "Light", "Dark" });
            _cmbTheme.SelectedItem = _settings.Theme;
            Controls.Add(_cmbTheme);
            curY += rowHeight;

            // 2. 言語
            Controls.Add(CreateLabel(_loc.Get("Settings.Language"), labelX, curY));
            _cmbLanguage = new ComboBox { Location = new Point(controlX, curY), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbLanguage.Items.AddRange(new object[] { "日本語 (ja-JP)", "English (en-US)" });
            _cmbLanguage.SelectedIndex = _settings.Language == "ja-JP" ? 0 : 1;
            Controls.Add(_cmbLanguage);
            curY += rowHeight;

            // 3. フォントファミリ
            Controls.Add(CreateLabel(_loc.Get("Settings.Font"), labelX, curY));
            _cmbFontFamily = new ComboBox { Location = new Point(controlX, curY), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbFontFamily.Items.AddRange(new object[] { "Yu Gothic UI", "Segoe UI", "Meiryo", "MS Gothic", "Consolas", "Cascadia Code" });
            _cmbFontFamily.SelectedItem = _settings.FontFamily;
            if (_cmbFontFamily.SelectedIndex < 0) _cmbFontFamily.SelectedIndex = 0;
            Controls.Add(_cmbFontFamily);
            curY += rowHeight;

            // 4. フォントサイズ
            Controls.Add(CreateLabel(_loc.Get("Settings.FontSize"), labelX, curY));
            _numFontSize = new NumericUpDown { Location = new Point(controlX, curY), Width = 100, Minimum = 6, Maximum = 24, Value = (decimal)_settings.FontSize };
            Controls.Add(_numFontSize);
            curY += rowHeight;

            // 5. 既定の文字コード
            Controls.Add(CreateLabel(_loc.Get("Settings.DefaultEncoding"), labelX, curY));
            _cmbDefaultEncoding = new ComboBox { Location = new Point(controlX, curY), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbDefaultEncoding.Items.AddRange(new object[] { "UTF-8", "UTF-8 BOM", "Shift_JIS" });
            _cmbDefaultEncoding.SelectedItem = _settings.DefaultEncoding;
            if (_cmbDefaultEncoding.SelectedIndex < 0) _cmbDefaultEncoding.SelectedIndex = 0;
            Controls.Add(_cmbDefaultEncoding);
            curY += rowHeight;

            // 6. 既定の改行コード
            Controls.Add(CreateLabel(_loc.Get("Settings.DefaultLineEnding"), labelX, curY));
            _cmbDefaultLineEnding = new ComboBox { Location = new Point(controlX, curY), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbDefaultLineEnding.Items.AddRange(new object[] { "CRLF", "LF", "CR" });
            _cmbDefaultLineEnding.SelectedItem = _settings.DefaultLineEnding;
            if (_cmbDefaultLineEnding.SelectedIndex < 0) _cmbDefaultLineEnding.SelectedIndex = 0;
            Controls.Add(_cmbDefaultLineEnding);
            curY += rowHeight;

            // 7. チェックボックス群
            _chkSaveFormulasAsRawText = new CheckBox
            {
                Text = _loc.Get("Settings.SaveFormulasAsRawText"),
                Location = new Point(labelX, curY),
                Width = 430,
                Checked = _settings.SaveFormulasAsRawText,
                AutoSize = true
            };
            Controls.Add(_chkSaveFormulasAsRawText);
            curY += 28;

            _chkAutoSaveSession = new CheckBox
            {
                Text = _loc.Get("Settings.AutoSaveSession"),
                Location = new Point(labelX, curY),
                Width = 430,
                Checked = _settings.AutoSaveSession,
                AutoSize = true
            };
            Controls.Add(_chkAutoSaveSession);
            curY += 36;

            // ボタン
            _btnCancel = new Button
            {
                Text = _loc.Get("Settings.Cancel"),
                Location = new Point(Width - 110 - 24, curY),
                Width = 100,
                Height = 30,
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat
            };
            _btnCancel.FlatAppearance.BorderColor = _theme.GridLine;

            _btnSave = new Button
            {
                Text = _loc.Get("Settings.Save"),
                Location = new Point(Width - 220 - 32, curY),
                Width = 100,
                Height = 30,
                DialogResult = DialogResult.OK,
                FlatStyle = FlatStyle.Flat,
                BackColor = _theme.AccentGreen,
                ForeColor = Color.White
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += OnSaveClicked;

            Controls.Add(_btnCancel);
            Controls.Add(_btnSave);

            AcceptButton = _btnSave;
            CancelButton = _btnCancel;
        }

        private Label CreateLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y + 4),
                AutoSize = true,
                ForeColor = _theme.TextColor
            };
        }

        private void OnSaveClicked(object? sender, EventArgs e)
        {
            string oldTheme = _settings.Theme;
            string oldLang = _settings.Language;
            string oldFont = _settings.FontFamily;
            float oldSize = _settings.FontSize;

            _settings.Theme = _cmbTheme.SelectedItem?.ToString() ?? "Light";
            _settings.Language = _cmbLanguage.SelectedIndex == 0 ? "ja-JP" : "en-US";
            _settings.FontFamily = _cmbFontFamily.SelectedItem?.ToString() ?? "Yu Gothic UI";
            _settings.FontSize = (float)_numFontSize.Value;
            _settings.DefaultEncoding = _cmbDefaultEncoding.SelectedItem?.ToString() ?? "UTF-8";
            _settings.DefaultLineEnding = _cmbDefaultLineEnding.SelectedItem?.ToString() ?? "CRLF";
            _settings.SaveFormulasAsRawText = _chkSaveFormulasAsRawText.Checked;
            _settings.AutoSaveSession = _chkAutoSaveSession.Checked;

            IsThemeChanged = oldTheme != _settings.Theme;
            IsLanguageChanged = oldLang != _settings.Language;
            IsFontChanged = oldFont != _settings.FontFamily || Math.Abs(oldSize - _settings.FontSize) > 0.1f;
        }
    }
}
