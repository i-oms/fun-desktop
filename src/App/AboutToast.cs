using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using IdleWalker.Domain;
using IdleWalker.Platform;
using IdleWalker.Rendering;

namespace IdleWalker.App
{
    /// <summary>
    /// The "About" card: a small dark panel with a waving Minion and the
    /// creator's name. It appears above the walkers' lane on the monitor
    /// the cursor is on, sits on top of everything (the walkers included),
    /// stays for <see cref="HoldSeconds"/>, then fades and disposes itself.
    /// It never takes focus, so nothing the user is doing is interrupted,
    /// and a click dismisses it early.
    /// </summary>
    sealed class AboutToast : Form
    {
        public const double HoldSeconds = 2.0;
        const int FadeMs = 220;
        const int FadeStepMs = 25;

        const string Title = "IdleWalker";
        const string Message = "Our creator is Sharafas OM";

        readonly Timer timer = new Timer();
        readonly Bitmap minion;
        readonly double scale;       // 1 at 100% display scaling
        readonly Font titleFont, textFont;
        readonly int pad, gap, textLeft;
        readonly Size titleSize, textSize;
        DateTime phaseStart;
        int phase;                   // 0 fading in, 1 holding, 2 fading out

        public AboutToast(Point cursor)
        {
            Screen screen = Screen.FromPoint(cursor);
            scale = DpiScale(screen);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(0x16, 0x18, 0x22);
            ForeColor = Color.White;
            Opacity = 0;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

            // The figure, waving, drawn by the same painter as on screen.
            minion = RenderMinion(3.2 * scale);

            titleFont = new Font("Segoe UI", (float)(15 * scale), FontStyle.Bold, GraphicsUnit.Pixel);
            textFont = new Font("Segoe UI", (float)(13 * scale), FontStyle.Regular, GraphicsUnit.Pixel);
            titleSize = TextRenderer.MeasureText(Title, titleFont);
            textSize = TextRenderer.MeasureText(Message, textFont);

            pad = Px(14);
            gap = Px(6);
            textLeft = pad + minion.Width + Px(10);
            int width = textLeft + Math.Max(titleSize.Width, textSize.Width) + pad;
            int height = Math.Max(minion.Height, titleSize.Height + gap + textSize.Height) + 2 * pad;

            // Centred on the monitor, just above where the walkers walk.
            Rectangle area = screen.WorkingArea;
            int cell = Math.Max(2, (int)Math.Round(Settings.BasePixelSize * scale));
            int left = area.Left + (area.Width - width) / 2;
            int top = area.Bottom - 52 * cell - height;
            if (top < area.Top) top = area.Top;
            SetBounds(left, top, width, height);
            Region = RoundedRegion(width, height, Px(10));

            Click += delegate { BeginFadeOut(); };
            timer.Tick += OnTick;
        }

        // A tool window that never activates: it should not steal the
        // keyboard from whatever the user is doing.
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Visible on screen, missing from screenshots / screen captures.
            Native.ExcludeFromCapture(Handle);
        }

        /// <summary>Shows the card and starts its clock.</summary>
        public void Present()
        {
            phase = 0;
            phaseStart = DateTime.UtcNow;
            timer.Interval = FadeStepMs;
            timer.Start();
            Show();
            StayOnTop();
        }

        void OnTick(object sender, EventArgs e)
        {
            double elapsed = (DateTime.UtcNow - phaseStart).TotalMilliseconds;
            switch (phase)
            {
                case 0:
                    Opacity = Math.Min(1, elapsed / FadeMs);
                    if (elapsed >= FadeMs)
                    {
                        phase = 1;
                        phaseStart = DateTime.UtcNow;
                        timer.Interval = 400; // just often enough to stay above the walkers
                    }
                    break;
                case 1:
                    if (elapsed >= HoldSeconds * 1000) BeginFadeOut();
                    break;
                case 2:
                    Opacity = Math.Max(0, 1 - elapsed / FadeMs);
                    if (elapsed >= FadeMs)
                    {
                        timer.Stop();
                        Close();
                        return;
                    }
                    break;
            }
            StayOnTop();
        }

        void BeginFadeOut()
        {
            if (phase == 2) return;
            phase = 2;
            phaseStart = DateTime.UtcNow;
            timer.Interval = FadeStepMs;
        }

        // The walkers re-assert topmost once a second; so does this, more often.
        void StayOnTop()
        {
            if (!IsHandleCreated) return;
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            // A thin lighter rim so the card reads against a dark desktop.
            using (GraphicsPath path = RoundedPath(Width - 1, Height - 1, Px(10)))
            using (Pen rim = new Pen(Color.FromArgb(0x3A, 0x3E, 0x4C), 1))
                g.DrawPath(rim, path);

            int figureTop = (Height - minion.Height) / 2;
            g.DrawImageUnscaled(minion, pad, figureTop);

            int textHeight = titleSize.Height + gap + textSize.Height;
            int textTop = (Height - textHeight) / 2;
            TextRenderer.DrawText(g, Title, titleFont, new Point(textLeft, textTop), Color.FromArgb(0xFA, 0xD6, 0x1C));
            TextRenderer.DrawText(g, Message, textFont, new Point(textLeft, textTop + titleSize.Height + gap), Color.White);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                minion.Dispose();
                titleFont.Dispose();
                textFont.Dispose();
            }
            base.Dispose(disposing);
        }

        // ---- Helpers ---------------------------------------------------------------

        int Px(double at100)
        {
            return (int)Math.Round(at100 * scale);
        }

        internal static double DpiScale(Screen screen)
        {
            Rectangle b = screen.Bounds;
            Native.POINT centre = new Native.POINT(b.Left + b.Width / 2, b.Top + b.Height / 2);
            IntPtr monitor = Native.MonitorFromPoint(centre, Native.MONITOR_DEFAULTTONEAREST);
            uint dpiX, dpiY;
            if (Native.GetDpiForMonitor(monitor, 0, out dpiX, out dpiY) != 0) dpiX = 96;
            return dpiX / 96.0;
        }

        /// <summary>The first Minion of the cast, waving, as a transparent bitmap.</summary>
        static Bitmap RenderMinion(double unit)
        {
            int width = (int)Math.Ceiling(22 * unit), height = (int)Math.Ceiling(26 * unit);
            Surface surface = new Surface();
            surface.Reset(width, height);

            Pose pose = Pose.Standing();
            pose.Face = Expression.Winking;
            pose.GazeX = 0;
            pose.ArmNear = 150;
            pose.ForeNear = -25;
            FrameRenderer.DrawPose(surface, pose, Personality.Roster[0].Look, Settings.DressColors[0], false, width / 2.0, height - 2, unit);

            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
            for (int y = 0; y < height; y++)
                Marshal.Copy(surface.Pixels, y * width, IntPtr.Add(data.Scan0, y * data.Stride), width);
            bitmap.UnlockBits(data);
            return bitmap;
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

        static Region RoundedRegion(int width, int height, int radius)
        {
            using (GraphicsPath path = RoundedPath(width, height, radius))
                return new Region(path);
        }
    }
}
