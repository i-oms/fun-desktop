using System;
using IdleWalker.Domain;

namespace IdleWalker.Rendering
{
    /// <summary>
    /// A small software canvas. The character is drawn into its own layer so
    /// it can be given an outline, then laid over whatever is behind it.
    /// Pixels are premultiplied ARGB, ready for a layered window.
    /// </summary>
    sealed class Surface
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int[] Pixels = new int[0];

        int[] layer = new int[0];
        bool[] rim = new bool[0];

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
            else
            {
                Array.Clear(Pixels, 0, Pixels.Length);
                Array.Clear(layer, 0, layer.Length);
            }
        }

        void Fill(int[] target, int x0, int y0, int x1, int y1, int color)
        {
            if (x0 < 0) x0 = 0;
            if (y0 < 0) y0 = 0;
            if (x1 > Width) x1 = Width;
            if (y1 > Height) y1 = Height;
            for (int y = y0; y < y1; y++)
            {
                int row = y * Width;
                for (int x = x0; x < x1; x++) target[row + x] = color;
            }
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
        public void DrawPart(Art.Part part, bool flip, double centreX, double soleY,
                             double cellWidth, double cellHeight, int dy)
        {
            for (int i = 0; i < part.Lines.Length; i++)
            {
                string line = part.Lines[i];
                int row = part.Top + i;
                int y0 = Round(soleY - (Art.Rows - row) * cellHeight) + dy;
                int y1 = Round(soleY - (Art.Rows - row - 1) * cellHeight) + dy;
                for (int col = 0; col < Art.Columns; col++)
                {
                    int color = Art.ColorOf(line[col]);
                    if (color == 0) continue;
                    int c = flip ? Art.Columns - 1 - col : col;
                    int x0 = Round(centreX + (c - Art.Columns / 2) * cellWidth);
                    int x1 = Round(centreX + (c + 1 - Art.Columns / 2) * cellWidth);
                    Fill(layer, x0, y0, x1, y1, color);
                }
            }
        }

        /// <summary>
        /// Outlines the character layer (so it reads on any background) and
        /// lays it over the canvas.
        /// </summary>
        public void CommitCharacter(int thickness)
        {
            Array.Clear(rim, 0, rim.Length);

            // A transparent pixel is rim if a character pixel lies within
            // `thickness` pixels of it along its row or its column.
            for (int y = 0; y < Height; y++)
            {
                int row = y * Width;
                int gap = thickness;
                for (int x = 0; x < Width; x++)
                {
                    if (layer[row + x] != 0) gap = 0;
                    else if (gap < thickness) { gap++; rim[row + x] = true; }
                }
                gap = thickness;
                for (int x = Width - 1; x >= 0; x--)
                {
                    if (layer[row + x] != 0) gap = 0;
                    else if (gap < thickness) { gap++; rim[row + x] = true; }
                }
            }
            for (int x = 0; x < Width; x++)
            {
                int gap = thickness;
                for (int y = 0; y < Height; y++)
                {
                    int i = y * Width + x;
                    if (layer[i] != 0) gap = 0;
                    else if (gap < thickness) { gap++; rim[i] = true; }
                }
                gap = thickness;
                for (int y = Height - 1; y >= 0; y--)
                {
                    int i = y * Width + x;
                    if (layer[i] != 0) gap = 0;
                    else if (gap < thickness) { gap++; rim[i] = true; }
                }
            }

            for (int i = 0; i < layer.Length; i++)
            {
                if (layer[i] != 0) Pixels[i] = layer[i];
                else if (rim[i]) Pixels[i] = Art.Outline;
            }
        }
    }
}
