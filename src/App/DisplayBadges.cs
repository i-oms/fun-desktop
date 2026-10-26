using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using IdleWalker.Platform;
using IdleWalker.Rendering;

namespace IdleWalker.App
{
    /// <summary>
    /// Large "Display N" cards, one centred on every monitor, so the sliders
    /// in the brightness panel can be matched to the screens they move. N is
    /// the monitor's place in Screen.AllScreens, the same order and numbering
    /// <see cref="BrightnessDisplay.Enumerate"/> uses for its labels.
    ///
    /// Each card is a click-through, focus-free layered window (like the
    /// walkers), drawn once when shown and again only when its highlight
    /// changes: nothing ticks while they are up. With a single monitor there
    /// is nothing to tell apart, so no cards are shown.
    /// </summary>
    sealed class DisplayBadges : IDisposable
    {
        const int SizeAt100 = 150;              // the square card, px at 100% display scaling
        const int CornerAt100 = 18;

        static readonly Color Back = Color.FromArgb(0xE6, 0x16, 0x18, 0x22);
        static readonly Color Rim = Color.FromArgb(0x3A, 0x3E, 0x4C);
        static readonly Color Dim = Color.FromArgb(0x8A, 0x8F, 0x9E);
        static readonly Color Accent = Color.FromArgb(0xFA, 0xD6, 0x1C);

        readonly Screen[] screens;
        readonly SpriteWindow[] windows;
        readonly bool[] lit;
        readonly Surface surface = new Surface();   // shared: Present copies it out at once
        bool disposed;

        public DisplayBadges()
        {
            screens = Screen.AllScreens;
            int count = screens.Length > 1 ? screens.Length : 0;
            windows = new SpriteWindow[count];
            lit = new bool[count];
        }

        /// <summary>Puts a card on every monitor, none highlighted.</summary>
        public void Show()
        {
            for (int i = 0; i < windows.Length; i++)
            {
                if (windows[i] == null) windows[i] = new SpriteWindow();
                Draw(i);
            }
        }

        /// <summary>
        /// Lights the card of the display at <paramref name="index"/> (as in
        /// Screen.AllScreens) and dims the rest; -1 lights them all, for the
        /// slider that moves every display. Only cards that change are redrawn.
        /// </summary>
        public void Highlight(int index)
        {
            if (disposed) return;
            for (int i = 0; i < windows.Length; i++)
            {
                bool on = index < 0 || i == index;
                if (lit[i] == on || windows[i] == null) continue;
                lit[i] = on;
                Draw(i);
            }
        }

        void Draw(int i)
        {
            Screen screen = screens[i];
            double scale = AboutToast.DpiScale(screen);
            int size = (int)Math.Round(SizeAt100 * scale);

            using (Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppPArgb))
            {
                Paint(bitmap, (i + 1).ToString(), lit[i], scale);
                surface.Reset(size, size);
                BitmapData data = bitmap.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.ReadOnly, bitmap.PixelFormat);
                for (int y = 0; y < size; y++)
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), surface.Pixels, y * size, size);
                bitmap.UnlockBits(data);
                surface.Touch(0, 0, size, size);
            }

            Rectangle b = screen.Bounds;
            windows[i].Present(surface, b.Left + (b.Width - size) / 2, b.Top + (b.Height - size) / 2);
        }

        static void Paint(Bitmap bitmap, string number, bool on, double scale)
        {
            int size = bitmap.Width;
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                // ClearType needs an opaque background; the card is translucent.
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);

                int radius = (int)Math.Round(CornerAt100 * scale);
                float rimWidth = (float)Math.Max(1, (on ? 3 : 1) * scale);
                using (GraphicsPath path = RoundedPath(size - 1, size - 1, radius))
                {
                    using (SolidBrush fill = new SolidBrush(Back)) g.FillPath(fill, path);
                    using (Pen rim = new Pen(on ? Accent : Rim, rimWidth)) g.DrawPath(rim, path);
                }

                using (StringFormat centre = new StringFormat())
                using (Font caption = new Font("Segoe UI", (float)(15 * scale), FontStyle.Regular, GraphicsUnit.Pixel))
                using (Font digits = new Font("Segoe UI", (float)(76 * scale), FontStyle.Bold, GraphicsUnit.Pixel))
                using (SolidBrush captionBrush = new SolidBrush(on ? Color.White : Dim))
                using (SolidBrush digitBrush = new SolidBrush(on ? Accent : Color.White))
                {
                    centre.Alignment = StringAlignment.Center;
                    centre.LineAlignment = StringAlignment.Center;
                    float captionTop = (float)(14 * scale);
                    g.DrawString("Display", caption, captionBrush, new RectangleF(0, captionTop, size, caption.Height), centre);
                    float digitsTop = captionTop + caption.Height;
                    g.DrawString(number, digits, digitBrush, new RectangleF(0, digitsTop, size, size - digitsTop - (float)(10 * scale)), centre);
                }
            }
        }

        static GraphicsPath RoundedPath(int width, int height, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(0, 0, d, d, 180, 90);
            path.AddArc(width - d, 0, d, d, 270, 90);
            path.AddArc(width - d, height - d, d, d, 0, 90);
            path.AddArc(0, height - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (SpriteWindow w in windows)
                if (w != null) w.Dispose();
        }
    }
}
