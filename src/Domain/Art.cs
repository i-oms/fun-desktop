namespace IdleWalker.Domain
{
    /// <summary>
    /// The Minion's pixel art, split into parts so that the head, arms and
    /// legs can be posed independently. Colours come from a
    /// <see cref="Palette"/> at draw time.
    /// </summary>
    static class Art
    {
        public const int Columns = 16;
        public const int Rows = 18; // the soles of the shoes are the bottom of row 17

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

        /// <summary>The three expressions a face needs.</summary>
        public sealed class Faces
        {
            public readonly Part Normal, Blink, Surprised;

            public Faces(Part normal, Part blink, Part surprised)
            {
                Normal = normal;
                Blink = blink;
                Surprised = surprised;
            }
        }

        /// <summary>How a face is drawn at rest: the brows and the mouth.</summary>
        public enum Mood
        {
            Calm,
            Friendly,
            Angry
        }

        // Everything is drawn facing right. '.' is transparent.
        //
        // The body is a yellow capsule 11 cells wide (columns 2-12). Row 0 is
        // hair, the dome starts on row 1, the goggles sit on rows 2-6, the
        // mouth on rows 8-9, the overalls bib on rows 10-13 and the legs below.

        // ---- Hair (row 0) ----------------------------------------------------

        public const string HairNone = "................";
        public const string HairSprouts = ".....K..K..K....";  // a few strands standing up
        public const string HairCombed = ".....KKKKK......";   // combed flat across the top
        public const string HairParted = "....KK...KK.....";   // parted in the middle
        public const string HairCrew = "....KKKKKKK.....";     // a flat crew cut

        // ---- Goggles (rows 2-6) ------------------------------------------------
        //
        // g is the strap round the head, G the metal rims, W the lenses. Pupils
        // sit on the right of each lens (looking where the body is going), or
        // in the middle when surprised. Each lens is 3x3 inside its rim.

        const char Brown = 'I';

        static string Lens(char pupil, bool centred)
        {
            return centred ? "W" + pupil + "W" : "WW" + pupil;
        }

        static string[] TwoEyes(char far, char near, bool shut, bool brows, bool centred)
        {
            string lensA = shut ? "YYY" : Lens(far, centred);
            string lensB = shut ? "YYY" : Lens(near, centred);
            string top = brows ? "..YYYKKKYKKKY..." : "..YYYGGGYGGGY...";
            string upper = shut ? "..ggGYYYGYYYG..." : brows ? "..ggGWWKGKWWG..." : "..ggGWWWGWWWG...";
            string middle = "..ggG" + lensA + "G" + lensB + "G...";
            string lower = shut ? "..ggGYYYGYYYG..." : "..ggGWWWGWWWG...";
            return new[] { top, upper, middle, lower, "..YYYGGGYGGGY..." };
        }

        static string[] OneEye(char pupil, bool shut, bool brows, bool centred)
        {
            string lens = shut ? "YYY" : Lens(pupil, centred);
            string top = brows ? "..YYYYYYKKKYY..." : "..YYYYYYGGGYY...";
            string upper = shut ? "..gggggGYYYGg..." : brows ? "..gggggGKKWGg..." : "..gggggGWWWGg...";
            string middle = "..gggggG" + lens + "Gg...";
            string lower = shut ? "..gggggGYYYGg..." : "..gggggGWWWGg...";
            return new[] { top, upper, middle, lower, "..YYYYYYGGGYY..." };
        }

        // ---- Mouths (rows 8-9) -------------------------------------------------

        static readonly string[] MouthFlat = { "..YYYYYYYKKKY...", "..YYYYYYYYYYY..." };
        static readonly string[] MouthSmile = { "..YYYYYYKYYKY...", "..YYYYYYYKKYY..." };
        static readonly string[] MouthFrown = { "..YYYYYYYKKYY...", "..YYYYYYKYYKY..." };
        static readonly string[] MouthGasp = { "..YYYYYYYKKYY...", "..YYYYYYYKKYY..." };

        static Part Head(string hair, string[] eyes, string[] mouth)
        {
            return new Part(0,
                hair,
                "...YYYYYYYYY....",
                eyes[0], eyes[1], eyes[2], eyes[3], eyes[4],
                "..YYYYYYYYYYY...",
                mouth[0], mouth[1]);
        }

        /// <summary>
        /// Builds the three expressions for one Minion: hair on top, one or
        /// two goggle lenses, and brows and mouth to match the mood. The pupil
        /// letters let a character have odd-coloured eyes.
        /// </summary>
        public static Faces MakeFaces(string hair, bool oneEye, Mood mood, char farPupil = Brown, char nearPupil = Brown)
        {
            bool brows = mood == Mood.Angry;
            string[] mouth = mood == Mood.Friendly ? MouthSmile : mood == Mood.Angry ? MouthFrown : MouthFlat;

            string[] open = oneEye ? OneEye(nearPupil, false, brows, false) : TwoEyes(farPupil, nearPupil, false, brows, false);
            string[] shut = oneEye ? OneEye(nearPupil, true, brows, false) : TwoEyes(farPupil, nearPupil, true, brows, false);
            string[] wide = oneEye ? OneEye(nearPupil, false, false, true) : TwoEyes(farPupil, nearPupil, false, false, true);

            return new Faces(
                Head(hair, open, mouth),
                Head(hair, shut, mouth),
                Head(hair, wide, MouthGasp));
        }

        // ---- Body (rows 10-13) -------------------------------------------------
        //
        // Overalls: straps on row 10, the bib with its pocket below, the waist
        // on row 13. The near arm (y) ends in a black glove (K).

        static Part Torso(string a, string b, string c)
        {
            return new Part(10, "..YYDYYYYYDYY...", a, b, c);
        }

        public static readonly Part ArmMid = Torso(
            "..YYDDDyyDDYY...",
            "..YYDDdyyDDYY...",
            "...DDDDKKDDD....");

        public static readonly Part ArmForward = Torso(
            "..YYDDDyyDDYY...",
            "..YYDDdDyyDYY...",
            "...DDDDDDKKD....");

        public static readonly Part ArmBack = Torso(
            "..YYDDDyyDDYY...",
            "..YYDDyyDDDYY...",
            "...DDKKDDDDD....");

        public static readonly Part TorsoPlain = Torso(
            "..YYDDDDDDDYY...",
            "..YYDDdDDDDYY...",
            "...DDDDDDDDD....");

        // A raised arm, drawn over TorsoPlain and the head.
        public static readonly Part WaveA = new Part(7,
            "..............KK",
            "...............K",
            ".............yy.",
            "............yy..",
            "..........yyy...");

        public static readonly Part WaveB = new Part(6,
            "..............KK",
            "..............KK",
            "..............y.",
            ".............yy.",
            "............yy..",
            "..........yyy...");

        // ---- Legs (rows 13-17) -------------------------------------------------
        //
        // The first hip row normally hides under the waist; it fills the gap
        // when the upper body bobs up.

        static Part Legs(string a, string b, string c)
        {
            return new Part(13, "....DDDDDDD.....", "....DDDDDDD.....", a, b, c);
        }

        public static readonly Part LegsStand = Legs(
            "....DDD.ddd.....",
            "....DDD.ddd.....",
            "....BBBBbbbb....");

        public static readonly Part LegsJump = Legs(
            "...DDDD.dddd....",
            "..BBBB...ddd....",
            ".........bbbb...");

        // Six-frame walk: contact, recoil, passing - then the same again with
        // the other leg leading. D/B is the near leg and shoe, d/b the far one.
        public static readonly Part[] WalkLegs =
        {
            Legs("...DDDD.dddd....",
                 "..DDD.....ddd...",
                 "..BBBB....bbbb.."),
            Legs("...DDD.ddd......",
                 "..BBB..ddd......",
                 ".......bbbb....."),
            Legs(".....ddDDD......",
                 ".....ddBBBB.....",
                 ".....bbbb......."),
            Legs("...dddd.DDDD....",
                 "..ddd.....DDD...",
                 "..bbbb....BBBB.."),
            Legs("...ddd.DDD......",
                 "..bbb..DDD......",
                 ".......BBBB....."),
            Legs(".....DDddd......",
                 ".....DDbbbb.....",
                 ".....BBBB......."),
        };

        // The near arm swings opposite to the near leg.
        public static readonly Part[] WalkArms = { ArmForward, ArmForward, ArmMid, ArmBack, ArmBack, ArmMid };

        // Upper-body bob per walk frame, in small steps (positive is down).
        public static readonly int[] WalkBob = { 0, 1, -1, 0, 1, -1 };

        // "!" shown above the head when startled.
        public static readonly Part Bubble = new Part(-7,
            ".......WW.......",
            ".......WW.......",
            ".......WW.......",
            "................",
            ".......WW.......");
    }
}
