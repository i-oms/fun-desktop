using System;
using System.Drawing;
using IdleWalker.Domain;

namespace IdleWalker.Rendering
{
    /// <summary>
    /// A small software canvas. The character is drawn into its own layer so
    /// it can be given an outline, then laid over whatever is behind it.
    /// Pixels are premultiplied ARGB, ready for a layered window.
    ///
    /// The canvas is mostly empty, so it keeps track of the rectangle that
    /// actually has something in it (<see cref="Bounds"/>) and does all its
    /// clearing, outlining and uploading on that rectangle only.
    /// </summary>
    sealed class Surface
    {
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

        public void Reset(int width, int height)
        {
            if (width != Width || height != Height)
            {
                Width = width;
                Height = height;
                Pixels = new int[width * height];
                layer = new int[width * height];
                rim = new bool[width * height];
            }
            else if (x1 > x0 && y1 > y0)
            {
                // Only the rows that were drawn on last time need clearing.
                int start = y0 * Width, length = (y1 - y0) * Width;
                Array.Clear(Pixels, start, length);
                Array.Clear(layer, start, length);
                Array.Clear(rim, start, length);
            }
            x0 = Width;
            y0 = Height;
            x1 = 0;
            y1 = 0;
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
            if (left < x0) x0 = left;
            if (top < y0) y0 = top;
            if (right > x1) x1 = right;
            if (bottom > y1) y1 = bottom;
        }

        /// <summary>A translucent square of dust resting on (x, y).</summary>
        public void Puff(int x, int y, int size, int alpha)
        {
            if (alpha <= 0) return;
            int color = (alpha << 24) | ((214 * alpha / 255) << 16) | ((204 * alpha / 255) << 8) | (190 * alpha / 255);
            Fill(Pixels, x - size / 2, y - size, x - size / 2 + size, y, color);
        }

        static int Round(double value)
        {
            return (int)Math.Floor(value + 0.5);
        }

        /// <summary>
        /// Draws one art part into the character layer. Cells are
        /// cellWidth x cellHeight pixels; the part is positioned so that the
        /// bottom of art row 17 lands on soleY, centred on centreX.
        /// </summary>
        public void DrawPart(Art.Part part, Palette palette, bool flip, double centreX, double soleY,
                             double cellWidth, double cellHeight, int dy)
        {
            for (int i = 0; i < part.Lines.Length; i++)
            {
                string line = part.Lines[i];
                int row = part.Top + i;
                int top = Round(soleY - (Art.Rows - row) * cellHeight) + dy;
                int bottom = Round(soleY - (Art.Rows - row - 1) * cellHeight) + dy;
                for (int col = 0; col < Art.Columns; col++)
                {
                    int color = palette[line[col]];
                    if (color == 0) continue;
                    int c = flip ? Art.Columns - 1 - col : col;
                    int left = Round(centreX + (c - Art.Columns / 2) * cellWidth);
                    int right = Round(centreX + (c + 1 - Art.Columns / 2) * cellWidth);
                    Fill(layer, left, top, right, bottom, color);
                }
            }
        }

        /// <summary>
        /// Outlines the character layer (so it reads on any background) and
        /// lays it over the canvas.
        /// </summary>
        public void CommitCharacter(int thickness)
        {
            if (x1 <= x0 || y1 <= y0) return;

            // The outline can reach `thickness` beyond the drawn region.
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
                    if (layer[i] != 0) Pixels[i] = layer[i];
                    else if (rim[i]) Pixels[i] = Palette.Outline;
                }
            }

            x0 = left;
            y0 = top;
            x1 = right;
            y1 = bottom;
        }
    }
}
