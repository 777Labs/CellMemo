using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace CellMemo.Native
{
    public static class Win32Api
    {
        public const int WM_SETICON = 0x0080;
        public const int WM_NCHITTEST = 0x0084;
        public const int WM_NCLBUTTONDOWN = 0x00A1;
        public const int WM_SYSCOMMAND = 0x0112;

        public const int HTCLIENT = 1;
        public const int HTCAPTION = 2;
        public const int HTSYSMENU = 3;
        public const int HTLEFT = 10;
        public const int HTRIGHT = 11;
        public const int HTTOP = 12;
        public const int HTTOPLEFT = 13;
        public const int HTTOPRIGHT = 14;
        public const int HTBOTTOM = 15;
        public const int HTBOTTOMLEFT = 16;
        public const int HTBOTTOMRIGHT = 17;
        public const int HTCLOSE = 20;
        public const int HTMINBUTTON = 8;
        public const int HTMAXBUTTON = 9;

        public const int SC_MINIMIZE = 0xF020;
        public const int SC_MAXIMIZE = 0xF030;
        public const int SC_RESTORE = 0xF120;
        public const int SC_CLOSE = 0xF060;

        public const int SW_RESTORE = 9;
        public const int SW_SHOW = 5;

        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        public const int DWMWA_BORDER_COLOR = 34;
        public const int DWMWA_CAPTION_COLOR = 35;
        public const int DWMWA_TEXT_COLOR = 36;

        public enum DWM_WINDOW_CORNER_PREFERENCE
        {
            DWMWCP_DEFAULT = 0,
            DWMWCP_DONOTROUND = 1,
            DWMWCP_ROUND = 2,
            DWMWCP_ROUNDSMALL = 3
        }

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public static void SetWindowCorner(IntPtr hwnd, DWM_WINDOW_CORNER_PREFERENCE corner)
        {
            int val = (int)corner;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref val, sizeof(int));
        }

        public static void SetDarkMode(IntPtr hwnd, bool isDark)
        {
            int val = isDark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref val, sizeof(int));
        }
    }
}
