namespace IdleWalker.Domain
{
    /// <summary>
    /// The character's pixel art, split into parts so that the head, arms,
    /// legs and scarf can be posed independently.
    /// </summary>
    static class Art
    {
        public const int Columns = 16;
        public const int Rows = 18; // the soles of the boots are the bottom of row 17

        /// <summary>A block of art rows; Top is the row its first line sits on.</summary>
        public sealed class Part
        {
            public readonly int Top;
            public readonly string[] Lines;

            public Part(int top, params string[] lines)
            {
                Top = top;
                Lines = lines;
            }
        }

        // Everything is drawn facing right. '.' is transparent.

        static Part Head(string eyes, string lids, string cheeks, string chin)
        {
            return new Part(0,
                "......YY........",
                ".....YYYY.......",
                "....TTTTTTT.....",
                "...TTTTTTTTT....",
                "..TTTTTTTTTTT...",
                "..tttttttttttt..",
                eyes, lids, cheeks, chin);
        }

        public static readonly Part HeadNormal = Head(
            "..SSSSSSKSSKS...",
            "..SSSSSSKSSKS...",
            "..SSSSPPSSSSSS..",
            "...SSSSSSSSSS...");

        public static readonly Part HeadBlink = Head(
            "..SSSSSSSSSSS...",
            "..SSSSSSKSSKS...",
            "..SSSSPPSSSSSS..",
            "...SSSSSSSSSS...");

        public static readonly Part HeadSurprised = Head(
            "..SSSSSSKSSKS...",
            "..SSSSSSKSSKS...",
            "..SSSSPPSSSSSS..",
            "...SSSSSSSKSS...");

        // Scarf plus jacket, with the near arm in different positions.
        static Part Torso(string a, string b, string c)
        {
            return new Part(10, "..OOOOOOOOOOO...", a, b, c);
        }

        public static readonly Part ArmMid = Torso(
            "...NNNNnnNNN....",
            "...NNNNnnNNN....",
            "...NNNNSSNNN....");

        public static readonly Part ArmForward = Torso(
            "...NNNNnnNNN....",
            "...NNNNNnnNN....",
            "...NNNNNNSSN....");

        public static readonly Part ArmBack = Torso(
            "...NNNNnnNNN....",
            "...NNNnnNNNN....",
            "...NNSSNNNNN....");

        public static readonly Part TorsoPlain = Torso(
            "...NNNNNNNNN....",
            "...NNNNNNNNN....",
            "...NNNNNNNNN....");

        // A raised arm, drawn over TorsoPlain and the head.
        public static readonly Part WaveA = new Part(7,
            "..............SS",
            "...............S",
            ".............nn.",
            "............nn..",
            "..........nnn...");

        public static readonly Part WaveB = new Part(6,
            "..............SS",
            "..............SS",
            "..............n.",
            ".............nn.",
            "............nn..",
            "..........nnn...");

        // The first hip row normally hides under the jacket; it fills the gap
        // when the upper body bobs up.
        static Part Legs(string a, string b, string c)
        {
            return new Part(13, "....GGGGGGG.....", "....GGGGGGG.....", a, b, c);
        }

        public static readonly Part LegsStand = Legs(
            "....GGG.ggg.....",
            "....GGG.ggg.....",
            "....BBBBbbbb....");

        public static readonly Part LegsJump = Legs(
            "...GGGG.gggg....",
            "..BBBB...ggg....",
            ".........bbbb...");

        // Six-frame walk: contact, recoil, passing - then the same again with
        // the other leg leading. G/B is the near leg, g/b the far one.
        public static readonly Part[] WalkLegs =
        {
            Legs("...GGGG.gggg....",
                 "..GGG.....ggg...",
                 "..BBBB....bbbb.."),
            Legs("...GGG.ggg......",
                 "..BBB..ggg......",
                 ".......bbbb....."),
            Legs(".....ggGGG......",
                 ".....ggBBBB.....",
                 ".....bbbb......."),
            Legs("...gggg.GGGG....",
                 "..ggg.....GGG...",
                 "..bbbb....BBBB.."),
            Legs("...ggg.GGG......",
                 "..bbb..GGG......",
                 ".......BBBB....."),
            Legs(".....GGggg......",
                 ".....GGbbbb.....",
                 ".....BBBB......."),
        };

        // The near arm swings opposite to the near leg.
        public static readonly Part[] WalkArms = { ArmForward, ArmForward, ArmMid, ArmBack, ArmBack, ArmMid };

        // Upper-body bob per walk frame, in small steps (positive is down).
        public static readonly int[] WalkBob = { 0, 1, -1, 0, 1, -1 };

        // The loose end of the scarf.
        public static readonly Part TailDown = new Part(11, "..O.............", "..O.............");
        public static readonly Part TailA = new Part(11, ".OO.............");
        public static readonly Part TailB = new Part(10, "OO..............");
        public static readonly Part TailUp = new Part(9, "OO..............", ".O..............");

        // "!" shown above the head when startled.
        public static readonly Part Bubble = new Part(-7,
            ".......WW.......",
            ".......WW.......",
            ".......WW.......",
            "................",
            ".......WW.......");

        public static readonly int Outline = Rgb(0x1E1826);

        static int Rgb(int rgb)
        {
            return unchecked((int)0xFF000000) | rgb;
        }

        /// <summary>Opaque ARGB for an art letter; 0 for transparent.</summary>
        public static int ColorOf(char c)
        {
            switch (c)
            {
                case 'Y': return Rgb(0xFFD23F); // pom-pom
                case 'T': return Rgb(0x1FA39A); // beanie
                case 't': return Rgb(0x157A74); // beanie brim
                case 'S': return Rgb(0xF6C9A0); // skin
                case 'P': return Rgb(0xEFA08A); // cheek
                case 'K': return Rgb(0x2B2233); // eyes
                case 'O': return Rgb(0xF2762E); // scarf
                case 'N': return Rgb(0x34508F); // jacket
                case 'n': return Rgb(0x263A6B); // sleeve
                case 'G': return Rgb(0x6B6F82); // near leg
                case 'g': return Rgb(0x4E5163); // far leg
                case 'B': return Rgb(0x6B4226); // near boot
                case 'b': return Rgb(0x4F301B); // far boot
                case 'W': return Rgb(0xFFFFFF); // "!"
                default: return 0;
            }
        }
    }
}
