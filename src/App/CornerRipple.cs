using System;
using System.Windows.Forms;
using IdleWalker.Platform;
using IdleWalker.Rendering;

namespace IdleWalker.App
{
    /// <summary>
    /// The hot-corner ripple: a brief expanding ring (two rings and a soft
    /// central flash) centred on the corner that just fired, so there is
    /// visible feedback for the action. One overlay window, click-through
    /// and focus-free like the walkers, alive for well under a second.
    /// Shown only when the corner's action is not Nothing; the caller
    /// decides that.
    ///
    /// It runs on its own UI timer (like the About card) rather than the
    /// shared frame pump, so it stays smooth even when no walkers are out
    /// and the pump is idling. Timing is wall-clock, so a slow frame never
    /// makes it sticky or slow-motion: each frame shows where the ripple
    /// should be right now.
    /// </summary>
    sealed class CornerRipple : IDisposable
    {
        const double DurationMs = 650;      // start to gone
        const int FrameMs = 25;             // ~40 fps, like the About fade
        const double SecondRingDelay = 0.12;// fraction of the run before the inner ring starts
        const double MaxRadiusAt100 = 56;   // px at 100% display scaling
        const double RingWidthAt100 = 3;

        // Ring blue, flash white: reads on pale and busy desktops alike.
        const int RingR = 80, RingG = 170, RingB = 255;
        const int FlashR = 255, FlashG = 255, FlashB = 255;

        readonly SpriteWindow window = new SpriteWindow();
        readonly Surface surface = new Surface();
        readonly Timer timer = new Timer();

        bool active;
        DateTime start;
        int size;               // the square surface, px
        double sizeFactor = 1;  // scale / BasePixelSize
        int lastX, lastY;
        bool disposed;

        /// <summary>True while the ripple is on screen.</summary>
        public bool Active { get { return active; } }

        public CornerRipple()
        {
            timer.Tick += OnTick;
        }

        /// <summary>
        /// Starts (or restarts) the ripple with its centre on the screen
        /// point (x, y), at the monitor's sprite scale. Presents the first
        /// frame at once so feedback is immediate, then ticks by itself.
        /// </summary>
        public void Show(int x, int y, int scale)
        {
            if (disposed) return;
            sizeFactor = Math.Max(0.5, scale / (double)Settings.BasePixelSize);
            double maxR = MaxRadiusAt100 * sizeFactor;
            size = (int)Math.Ceiling(2 * (maxR + 6));
            if (size < 32) size = 32;
            lastX = x - size / 2;
            lastY = y - size / 2;
            start = DateTime.UtcNow;
            active = true;
            Render(0);
            window.Present(surface, lastX, lastY);
            window.StayOnTop();
            timer.Interval = FrameMs;
            timer.Start();
        }

        void OnTick(object sender, EventArgs e)
        {
            if (!active) return;
            double t = (DateTime.UtcNow - start).TotalMilliseconds / DurationMs;
            if (t >= 1)
            {
                timer.Stop();
                active = false;
                window.Hide();
                return;
            }
            Render(t);
            window.Present(surface, lastX, lastY);
            window.StayOnTop();
        }

        void Render(double t)
        {
            if (t < 0) t = 0; else if (t > 1) t = 1;
            surface.Reset(size, size);
            double cx = size / 2.0, cy = size / 2.0;
            double maxR = MaxRadiusAt100 * sizeFactor;
            double w = Math.Max(2, RingWidthAt100 * sizeFactor);

            // Soft central flash, shrinking and fading fast.
            double flashR = 14 * sizeFactor * (1 - 0.4 * t);
            int flashA = (int)(140 * (1 - t) * (1 - t));
            if (flashA > 0 && flashR > 0) Disc(cx, cy, flashR, flashA);

            // Inner ring, starting a beat later and stopping shorter.
            double t2 = (t - SecondRingDelay) / (1 - SecondRingDelay);
            if (t2 > 0)
            {
                double r2 = 8 * sizeFactor + (maxR * 0.7 - 8 * sizeFactor) * EaseOut(t2);
                int a2 = (int)(190 * Math.Pow(1 - t2, 1.6));
                if (a2 > 0) Ring(cx, cy, r2, w, a2);
            }

            // Outer ring, leading the way to the full radius.
            double r1 = 10 * sizeFactor + (maxR - 10 * sizeFactor) * EaseOut(t);
            int a1 = (int)(230 * Math.Pow(1 - t, 1.6));
            if (a1 > 0) Ring(cx, cy, r1, w, a1);

            surface.Touch(0, 0, size, size);
        }

        static double EaseOut(double t)
        {
            double u = 1 - t;
            return 1 - u * u;
        }

        void Ring(double cx, double cy, double radius, double width, int alpha)
        {
            int left = Math.Max(0, (int)Math.Floor(cx - radius - width - 2));
            int right = Math.Min(size, (int)Math.Ceiling(cx + radius + width + 2));
            int top = Math.Max(0, (int)Math.Floor(cy - radius - width - 2));
            int bottom = Math.Min(size, (int)Math.Ceiling(cy + radius + width + 2));
            for (int y = top; y < bottom; y++)
            {
                double dy = (y + 0.5) - cy;
                for (int x = left; x < right; x++)
                {
                    double dx = (x + 0.5) - cx;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    double cov = 1 - Math.Abs(d - radius) / width;
                    if (cov <= 0) continue;
                    if (cov > 1) cov = 1;
                    int a = (int)(alpha * cov);
                    if (a <= 0) continue;
                    Plot(x, y, RingR, RingG, RingB, a);
                }
            }
        }

        void Disc(double cx, double cy, double radius, int alpha)
        {
            int left = Math.Max(0, (int)Math.Floor(cx - radius - 2));
            int right = Math.Min(size, (int)Math.Ceiling(cx + radius + 2));
            int top = Math.Max(0, (int)Math.Floor(cy - radius - 2));
            int bottom = Math.Min(size, (int)Math.Ceiling(cy + radius + 2));
            for (int y = top; y < bottom; y++)
            {
                double dy = (y + 0.5) - cy;
                for (int x = left; x < right; x++)
                {
                    double dx = (x + 0.5) - cx;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    double cov = 1 - (d - radius) / 2;
                    if (cov <= 0) continue;
                    if (cov > 1) cov = 1;
                    int a = (int)(alpha * cov);
                    if (a <= 0) continue;
                    Plot(x, y, FlashR, FlashG, FlashB, a);
                }
            }
        }

        void Plot(int x, int y, int r, int g, int b, int a)
        {
            if (a <= 0) return;
            if (a > 255) a = 255;
            int i = y * size + x;
            int[] pixels = surface.Pixels;
            int src = (a << 24) | ((r * a / 255) << 16) | ((g * a / 255) << 8) | (b * a / 255);
            int dst = pixels[i];
            if (dst == 0)
            {
                pixels[i] = src;
                return;
            }
            int sa = a;
            if (sa >= 255)
            {
                pixels[i] = src;
                return;
            }
            int keep = 255 - sa;
            int da = (int)((uint)dst >> 24);
            int na = sa + (da * keep + 127) / 255;
            int nr = ((src >> 16) & 255) + (((dst >> 16) & 255) * keep + 127) / 255;
            int ng = ((src >> 8) & 255) + (((dst >> 8) & 255) * keep + 127) / 255;
            int nb = (src & 255) + ((dst & 255) * keep + 127) / 255;
            if (na > 255) na = 255;
            if (nr > 255) nr = 255;
            if (ng > 255) ng = 255;
            if (nb > 255) nb = 255;
            pixels[i] = (na << 24) | (nr << 16) | (ng << 8) | nb;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            active = false;
            timer.Stop();
            timer.Dispose();
            window.Dispose();
        }
    }
}
