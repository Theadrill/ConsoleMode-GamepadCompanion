using System;
using System.Runtime.InteropServices;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>
    /// P/Invoke isolado de chamadas Win32 (user32.dll / kernel32.dll) para gerenciamento de janelas,
    /// Z-order e foco em primeiro plano (Foreground Window). Sem regras de negócio.
    /// </summary>
    internal static class WindowNative
    {
        public const int SW_HIDE = 0;
        public const int SW_SHOWNORMAL = 1;
        public const int SW_SHOWMINIMIZED = 2;
        public const int SW_SHOWMAXIMIZED = 3;
        public const int SW_SHOW = 5;
        public const int SW_RESTORE = 9;

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        public static readonly IntPtr HWND_TOP = new IntPtr(0);

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_SHOWWINDOW = 0x0040;

        public const uint LSFW_UNLOCK = 2;
        public const int ASFW_ANY = -1;

        private const byte VK_MENU = 0x12; // Alt key
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool LockSetForegroundWindow(uint uCode);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AllowSetForegroundWindow(int dwProcessId);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ClipCursor(ref RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ClipCursor(IntPtr lpRect);

        private static RECT _lastClippedRect;
        private static IntPtr _lastClippedHwnd = IntPtr.Zero;
        private static readonly object _clipLock = new object();

        /// <summary>
        /// Prende o cursor do mouse dentro dos limites da janela fornecida.
        /// Se a janela for inválida, minimizada ou invisível, libera o cursor.
        /// </summary>
        public static void ClipCursorToWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || IsIconic(hWnd))
            {
                ReleaseCursorClip();
                return;
            }

            if (GetWindowRect(hWnd, out RECT rect))
            {
                if (rect.Right > rect.Left && rect.Bottom > rect.Top)
                {
                    lock (_clipLock)
                    {
                        if (_lastClippedHwnd != hWnd ||
                            _lastClippedRect.Left != rect.Left ||
                            _lastClippedRect.Top != rect.Top ||
                            _lastClippedRect.Right != rect.Right ||
                            _lastClippedRect.Bottom != rect.Bottom)
                        {
                            _lastClippedHwnd = hWnd;
                            _lastClippedRect = rect;
                            ClipCursor(ref rect);
                        }
                    }
                    return;
                }
            }

            ReleaseCursorClip();
        }

        /// <summary>
        /// Libera o cursor do mouse para mover-se livremente em todos os monitores.
        /// </summary>
        public static void ReleaseCursorClip()
        {
            lock (_clipLock)
            {
                if (_lastClippedHwnd != IntPtr.Zero)
                {
                    _lastClippedHwnd = IntPtr.Zero;
                    _lastClippedRect = default;
                    ClipCursor(IntPtr.Zero);
                }
            }
        }

        /// <summary>
        /// Traz a janela especificada para o primeiro plano (Foreground) acima de todas as outras janelas,
        /// restaurando-a se estiver minimizada e superando restrições do LockSetForegroundWindow do Windows.
        /// </summary>
        public static void BringWindowToForeground(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return;

            LockSetForegroundWindow(LSFW_UNLOCK);
            AllowSetForegroundWindow(ASFW_ANY);

            IntPtr currentForeground = GetForegroundWindow();
            if (currentForeground == hWnd)
            {
                ShowWindow(hWnd, SW_RESTORE);
                BringWindowToTop(hWnd);
                return;
            }

            uint currentForegroundThread = 0;
            if (currentForeground != IntPtr.Zero)
            {
                currentForegroundThread = GetWindowThreadProcessId(currentForeground, out _);
            }
            uint appThread = GetCurrentThreadId();

            bool threadsAttached = false;
            if (currentForegroundThread != 0 && currentForegroundThread != appThread)
            {
                threadsAttached = AttachThreadInput(currentForegroundThread, appThread, true);
            }

            try
            {
                // Libera a restrição de foco da Win32 simulando evento neutro de teclado
                keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
                keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                ShowWindow(hWnd, SW_RESTORE);
                SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                BringWindowToTop(hWnd);
                SetForegroundWindow(hWnd);
            }
            finally
            {
                if (threadsAttached)
                {
                    AttachThreadInput(currentForegroundThread, appThread, false);
                }
            }
        }
    }
}
