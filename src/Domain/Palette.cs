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

        /// <summary>The original teal-and-orange outfit.</summary>
        public static readonly Palette Default = new Palette()
            .With('Y', 0xFFD23F)  // pom-pom
            .With('T', 0x1FA39A)  // beanie
            .With('t', 0x157A74)  // beanie brim
            .With('S', 0xF6C9A0)  // skin
            .With('P', 0xEFA08A)  // cheek
            .With('K', 0x2B2233)  // eyes
            .With('O', 0xF2762E)  // scarf
            .With('N', 0x34508F)  // jacket
            .With('n', 0x263A6B)  // sleeve
            .With('G', 0x6B6F82)  // near leg
            .With('g', 0x4E5163)  // far leg
            .With('B', 0x6B4226)  // near boot
            .With('b', 0x4F301B)  // far boot
            .With('W', 0xFFFFFF); // "!"
    }
}
