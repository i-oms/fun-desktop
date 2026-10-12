using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using IdleWalker.Rendering;

namespace IdleWalker.Platform
{
    /// <summary>
    /// A borderless, always-on-top, click-through window that shows a Surface
    /// with per-pixel transparency.
    /// </summary>
    sealed class SpriteWindow : NativeWindow, IDisposable
    {
        IntPtr memoryDc;
        IntPtr dib;
        IntPtr dibBits;
        IntPtr previousBitmap;
        int dibWidth, dibHeight;
        bool visible;

        public SpriteWindow()
        {
            CreateParams cp = new CreateParams();
            cp.Caption = "IdleWalker";
            cp.Style = unchecked((int)Native.WS_POPUP);
            cp.ExStyle = Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW |
                         Native.WS_EX_TOPMOST | Native.WS_EX_NOACTIVATE;
            CreateHandle(cp);
            // Visible on screen, missing from screenshots / screen captures.
            Native.ExcludeFromCapture(Handle);
        }

        /// <summary>
        /// Shows the surface with its top-left corner at (x, y). Only the
        /// part of the surface that has content is uploaded and shown; the
        /// window shrinks to fit it.
        /// </summary>
        public void Present(Surface surface, int x, int y)
        {
            EnsureBitmap(surface.Width, surface.Height);
            if (dib == IntPtr.Zero) return;

            Rectangle bounds = surface.Bounds;
            if (bounds.IsEmpty) bounds = new Rectangle(0, 0, 1, 1); // a single transparent pixel

            // Copy just the rows that matter into the DIB.
            int start = bounds.Top * surface.Width;
            Marshal.Copy(surface.Pixels, start, IntPtr.Add(dibBits, start * 4), bounds.Height * surface.Width);

            Native.POINT destination = new Native.POINT(x + bounds.Left, y + bounds.Top);
            Native.POINT source = new Native.POINT(bounds.Left, bounds.Top);
            Native.SIZE size = new Native.SIZE(bounds.Width, bounds.Height);
            Native.BLENDFUNCTION blend = new Native.BLENDFUNCTION();
            blend.SourceConstantAlpha = 255;
            blend.AlphaFormat = Native.AC_SRC_ALPHA;
            Native.UpdateLayeredWindow(Handle, IntPtr.Zero, ref destination, ref size, memoryDc,
                                       ref source, 0, ref blend, Native.ULW_ALPHA);

            if (!visible)
            {
                Native.ShowWindow(Handle, Native.SW_SHOWNOACTIVATE);
                visible = true;
                StayOnTop();
            }
        }

        void EnsureBitmap(int width, int height)
        {
            if (width == dibWidth && height == dibHeight && dib != IntPtr.Zero) return;
            ReleaseBitmap();
            if (memoryDc == IntPtr.Zero) memoryDc = Native.CreateCompatibleDC(IntPtr.Zero);

            Native.BITMAPINFOHEADER header = new Native.BITMAPINFOHEADER();
            header.biSize = Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER));
            header.biWidth = width;
            header.biHeight = -height; // top-down
            header.biPlanes = 1;
            header.biBitCount = 32;
            dib = Native.CreateDIBSection(IntPtr.Zero, ref header, 0, out dibBits, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero) return;
            previousBitmap = Native.SelectObject(memoryDc, dib);
            dibWidth = width;
            dibHeight = height;
        }

        void ReleaseBitmap()
        {
            if (dib == IntPtr.Zero) return;
            Native.SelectObject(memoryDc, previousBitmap);
            Native.DeleteObject(dib);
            dib = IntPtr.Zero;
        }

        /// <summary>Re-asserts topmost; the taskbar is topmost too and likes to win.</summary>
        public void StayOnTop()
        {
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        public void Hide()
        {
            if (!visible) return;
            Native.ShowWindow(Handle, Native.SW_HIDE);
            visible = false;
        }

        public void Dispose()
        {
            ReleaseBitmap();
            if (memoryDc != IntPtr.Zero)
            {
                Native.DeleteDC(memoryDc);
                memoryDc = IntPtr.Zero;
            }
            DestroyHandle();
        }
    }
}
