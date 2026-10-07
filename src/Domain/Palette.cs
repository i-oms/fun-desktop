using System;

namespace IdleWalker.Domain
{
    /// <summary>
    /// Maps art letters to opaque ARGB colours. Each walker has its own, so
    /// the same art can be recoloured per character.
    /// </summary>
    sealed class Palette
    {
        public static readonly int Outline = Opaque(0x1E1826);

        readonly int[] colors = new int[128];

        Palette()
        {
        }

        /// <summary>Colour for an art letter; 0 for transparent.</summary>
        public int this[char c]
        {
            get { return c < 128 ? colors[c] : 0; }
        }

        /// <summary>A copy of this palette with one letter recoloured.</summary>
        public Palette With(char letter, int rgb)
        {
            Palette copy = new Palette();
            Array.Copy(colors, copy.colors, colors.Length);
            copy.colors[letter] = Opaque(rgb);
            return copy;
        }

        static int Opaque(int rgb)
        {
            return unchecked((int)0xFF000000) | rgb;
        }

        /// <summary>The Minion: yellow body, denim overalls, steel goggles.</summary>
        public static readonly Palette Default = new Palette()
            .With('Y', 0xF9D71C)  // body
            .With('y', 0xE2BE16)  // near arm
            .With('D', 0x2F5BB7)  // overalls / near leg
            .With('d', 0x23468F)  // pocket / far leg
            .With('G', 0xA6ABB3)  // goggle rims
            .With('g', 0x5C6069)  // goggle strap
            .With('W', 0xFFFFFF)  // lenses and the "!"
            .With('I', 0x5C3A1E)  // brown pupil
            .With('E', 0x3E8E4B)  // green pupil
            .With('K', 0x2B2233)  // hair, mouth, brows, gloves
            .With('B', 0x34303D)  // near shoe
            .With('b', 0x262230); // far shoe
    }
}
