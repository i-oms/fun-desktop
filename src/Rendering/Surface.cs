using System;
using System.Drawing;

namespace IdleWalker.Rendering
{
    /// <summary>
    /// A small software canvas. The character is drawn into its own layer so
    /// it can be given a soft edge, then laid over whatever is behind it
    /// (dust, its shadow). Pixels are premultiplied ARGB, ready for a
    /// layered window.
    ///
    /// The canvas is mostly empty, so it keeps track of the rectangle that
    /// actually has something in it (<see cref="Bounds"/>) and does all its
    /// clearing, outlining and uploading on that rectangle only.
    /// </summary>
    sealed class Surface
    {
        // A translucent dark halo one pixel wide keeps the figure readable on
        // a busy or pale desktop without looking like a drawn outline.
        const int Halo = unchecked((int)0x78101018);

        public int Width { get; private set; }
        public int Height { get; private set; }
        public int[] Pixels = new int[0];

        int[] layer = new int[0];
        bool[] rim = new bool[0];

        // The region with content, as [x0, x1) x [y0, y1); empty when x1 <= x0.
        int x0, y0, x1, y1;

        /// <summary>The part of the canvas that has anything drawn in it.</summary>
        public Rectangle Bounds
        {
            get { return x1 > x0 && y1 > y0 ? Rectangle.FromLTRB(x0, y0, x1, y1) : Rectangle.Empty; }
        }

        /// <summary>The character layer, premultiplied ARGB, Width pixels per row.</summary>
        public int[] Layer { get { return layer; } }

        public void Reset(int width, int height)
        {
            int needed = width * height;
            if (needed > Pixels.Length)
            {
                // Grow once: a pooled canvas keeps its arrays for good, so a
                // Minion's frame after Mr. Bean's costs no allocation.
                Pixels = new int[needed];
                layer = new int[needed];
                rim = new bool[needed];
            }
            else if (width != Width || height != Height)
            {
                // Same arrays, new row stride: whatever was drawn last time
                // lies scattered under the new layout, so clear the whole of
                // the new canvas (a few microseconds).
                Array.Clear(Pixels, 0, needed);
                Array.Clear(layer, 0, needed);
                Array.Clear(rim, 0, needed);
            }
            else if (x1 > x0 && y1 > y0)
            {
                // Only the rows that were drawn on last time need clearing.
                int start = y0 * Width, length = (y1 - y0) * Width;
                Array.Clear(Pixels, start, length);
                Array.Clear(layer, start, length);
                Array.Clear(rim, start, length);
            }
            Width = width;
            Height = height;
            x0 = Width;
            y0 = Height;
            x1 = 0;
            y1 = 0;
        }

        /// <summary>Notes that [left, right) x [top, bottom) may have been drawn on.</summary>
        public void Touch(int left, int top, int right, int bottom)
        {
            if (left < 0) left = 0;
            if (top < 0) top = 0;
            if (right > Width) right = Width;
            if (bottom > Height) bottom = Height;
            if (left >= right || top >= bottom) return;
            if (left < x0) x0 = left;
            if (top < y0) y0 = top;
            if (right > x1) x1 = right;
            if (bottom > y1) y1 = bottom;
        }

        void Fill(int[] target, int left, int top, int right, int bottom, int color)
        {
            if (left < 0) left = 0;
            if (top < 0) top = 0;
            if (right > Width) right = Width;
            if (bottom > Height) bottom = Height;
            if (left >= right || top >= bottom) return;

            for (int y = top; y < bottom; y++)
            {
                int row = y * Width;
                for (int x = left; x < right; x++) target[row + x] = color;
            }
            Touch(left, top, right, bottom);
        }

        /// <summary>A translucent square of dust resting on (x, y).</summary>
        public void Puff(int x, int y, int size, int alpha)
        {
            if (alpha <= 0) return;
            int color = (alpha << 24) | ((214 * alpha / 255) << 16) | ((204 * alpha / 255) << 8) | (190 * alpha / 255);
            Fill(Pixels, x - size / 2, y - size, x - size / 2 + size, y, color);
        }

        /// <summary>
        /// A soft, dark ellipse on the canvas: the shadow under the feet.
        /// Centred on (cx, cy) with radii rx, ry and `alpha` (0..1) at the centre.
        /// </summary>
        public void Shadow(double cx, double cy, double rx, double ry, double alpha)
        {
            if (alpha <= 0.01 || rx <= 0 || ry <= 0) return;
            int left = Math.Max(0, (int)Math.Floor(cx - rx));
            int right = Math.Min(Width, (int)Math.Ceiling(cx + rx));
            int top = Math.Max(0, (int)Math.Floor(cy - ry));
            int bottom = Math.Min(Height, (int)Math.Ceiling(cy + ry));
            if (left >= right || top >= bottom) return;

            for (int y = top; y < bottom; y++)
            {
                double dy = (y + 0.5 - cy) / ry;
                int row = y * Width;
                for (int x = left; x < right; x++)
                {
                    double dx = (x + 0.5 - cx) / rx;
                    double t = dx * dx + dy * dy;
                    if (t >= 1) continue;
                    double f = 1 - t;
                    int a = (int)(alpha * f * f * 255 + 0.5);
                    if (a <= 0) continue;
                    int color = (a << 24) | ((0x14 * a / 255) << 16) | ((0x10 * a / 255) << 8) | (0x18 * a / 255);
                    Pixels[row + x] = OverPremul(color, Pixels[row + x]);
                }
            }
            Touch(left, top, right, bottom);
        }

        /// <summary>
        /// Softens the edge of the character layer with a thin dark halo and
        /// lays the layer over the canvas.
        /// </summary>
        public void CommitCharacter(int thickness)
        {
            if (x1 <= x0 || y1 <= y0) return;

            // The halo can reach `thickness` beyond the drawn region.
            int left = Math.Max(0, x0 - thickness);
            int top = Math.Max(0, y0 - thickness);
            int right = Math.Min(Width, x1 + thickness);
            int bottom = Math.Min(Height, y1 + thickness);

            // A transparent pixel is rim if a character pixel lies within
            // `thickness` pixels of it along its row or its column.
            for (int y = top; y < bottom; y++)
            {
                int row = y * Width;
                int gap = thickness;
                for (int x = left; x < right; x++)
                {
                    if (layer[row + x] != 0) gap = 0;
                    else if (gap < thickness) { gap++; rim[row + x] = true; }
                }
                gap = thickness;
                for (int x = right - 1; x >= left; x--)
                {
                    if (layer[row + x] != 0) gap = 0;
                    else if (gap < thickness) { gap++; rim[row + x] = true; }
                }
            }
            for (int x = left; x < right; x++)
            {
                int gap = thickness;
                for (int y = top; y < bottom; y++)
                {
                    int i = y * Width + x;
                    if (layer[i] != 0) gap = 0;
                    else if (gap < thickness) { gap++; rim[i] = true; }
                }
                gap = thickness;
                for (int y = bottom - 1; y >= top; y--)
                {
                    int i = y * Width + x;
                    if (layer[i] != 0) gap = 0;
                    else if (gap < thickness) { gap++; rim[i] = true; }
                }
            }

            for (int y = top; y < bottom; y++)
            {
                int row = y * Width;
                for (int x = left; x < right; x++)
                {
                    int i = row + x;
                    int src = layer[i];
                    if (src != 0)
                    {
                        // Anti-aliased edges get the halo behind them too.
                        int alpha = (int)((uint)src >> 24);
                        Pixels[i] = OverPremul(src, alpha < 255 ? OverPremul(Halo, Pixels[i]) : Pixels[i]);
                    }
                    else if (rim[i])
                    {
                        Pixels[i] = OverPremul(Halo, Pixels[i]);
                    }
                }
            }

            x0 = left;
            y0 = top;
            x1 = right;
            y1 = bottom;
        }

        /// <summary>src over dst, both premultiplied.</summary>
        static int OverPremul(int src, int dst)
        {
            if (dst == 0) return src;
            int sa = (int)((uint)src >> 24);
            if (sa >= 255) return src;
            int keep = 255 - sa;
            int a = sa + ((int)((uint)dst >> 24) * keep + 127) / 255;
            int r = ((src >> 16) & 255) + (((dst >> 16) & 255) * keep + 127) / 255;
            int g = ((src >> 8) & 255) + (((dst >> 8) & 255) * keep + 127) / 255;
            int b = (src & 255) + ((dst & 255) * keep + 127) / 255;
            if (a > 255) a = 255;
            if (r > 255) r = 255;
            if (g > 255) g = 255;
            if (b > 255) b = 255;
            return (a << 24) | (r << 16) | (g << 8) | b;
        }
    }
}
