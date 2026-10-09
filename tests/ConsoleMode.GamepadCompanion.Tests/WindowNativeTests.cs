using System;
using ConsoleMode.GamepadCompanion.Hardware;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    public class WindowNativeTests
    {
        [Fact]
        public void BringWindowToForeground_ZeroHandle_DoesNotThrow()
        {
            var ex = Record.Exception(() => WindowNative.BringWindowToForeground(IntPtr.Zero));
            Assert.Null(ex);
        }

        [Fact]
        public void WindowNativeConstants_HaveExpectedValues()
        {
            Assert.Equal(9, WindowNative.SW_RESTORE);
            Assert.Equal(5, WindowNative.SW_SHOW);
            Assert.Equal(new IntPtr(-1), WindowNative.HWND_TOPMOST);
            Assert.Equal(new IntPtr(-2), WindowNative.HWND_NOTOPMOST);
            Assert.Equal(0x0001u, WindowNative.SWP_NOSIZE);
            Assert.Equal(0x0002u, WindowNative.SWP_NOMOVE);
            Assert.Equal(0x0040u, WindowNative.SWP_SHOWWINDOW);
        }

        [Fact]
        public void ClipCursorToWindow_ZeroHandle_DoesNotThrow()
        {
            var ex = Record.Exception(() => WindowNative.ClipCursorToWindow(IntPtr.Zero));
            Assert.Null(ex);
        }

        [Fact]
        public void ReleaseCursorClip_DoesNotThrow()
        {
            var ex = Record.Exception(() => WindowNative.ReleaseCursorClip());
            Assert.Null(ex);
        }

        [Fact]
        public void RECT_CalculatesWidthAndHeightCorrectly()
        {
            var rect = new WindowNative.RECT
            {
                Left = 100,
                Top = 50,
                Right = 900,
                Bottom = 650
            };

            Assert.Equal(800, rect.Width);
            Assert.Equal(600, rect.Height);
        }
    }
}
