using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms.Integration;

namespace Wox.Previewer.WindowsPreview
{
    public class NoFlickerWindowsFormsHost : WindowsFormsHost
    {
        const uint SWP_NOZORDER = 0x0004;
        const uint SWP_NOACTIVATE = 0x0010;
        const uint SWP_ASYNCWINDOWPOS = 0x4000;
        public static readonly Int32 GWL_STYLE = -16;
        public static readonly UInt32 WS_CHILD = 0x40000000;

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        internal static extern IntPtr SetParent(IntPtr hWnd, IntPtr hWndParent);
        [DllImport("user32.dll")]
        internal static extern int GetWindowLong(IntPtr hWnd, Int32 nIndex);
        [DllImport("user32.dll")]
        internal static extern UInt32 SetWindowLong(IntPtr hWnd, Int32 nIndex, UInt32 dwNewLong);

        internal int GetWindowStyle(IntPtr hWnd)
        {
            return (int)GetWindowLong(hWnd, GWL_STYLE);
        }

        internal void SetWindowStyle(IntPtr hWnd, int windowStyle)
        {
            SetWindowLong(hWnd, GWL_STYLE, (UInt32)windowStyle);
        }
        [DllImport("user32.dll")]
        extern static bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            SetWindowStyle(hwndParent.Handle, 0x02000000 | GetWindowStyle(hwndParent.Handle));
            return base.BuildWindowCore(hwndParent);
        }
        protected override void OnWindowPositionChanged(Rect rcBoundingBox)
        {
            if (Handle != IntPtr.Zero)
            {
                SetWindowPos(Handle,
                  IntPtr.Zero,
                  (int)rcBoundingBox.X,
                  (int)rcBoundingBox.Y,
                  (int)rcBoundingBox.Width,
                  (int)rcBoundingBox.Height,
                  SWP_ASYNCWINDOWPOS |
                  SWP_NOZORDER
                  | SWP_NOACTIVATE);
            }
        }

        protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0014)
            {
                handled = true;
                return IntPtr.Zero;
            }
            return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
        }
    }
}
