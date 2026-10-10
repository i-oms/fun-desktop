namespace IdleWalker.Domain
{
    enum HairStyle
    {
        None,     // bald
        Sprouts,  // a few strands standing up
        Combed,   // swept flat over the top
        Parted,   // parted in the middle
        Crew,     // a short, even crew cut
        Spiky,    // short spikes standing straight out all over the top
        Quiff,    // a tall wave swept up and back from the front
        Tuft,     // one long curl on top
        Wild      // long strands sticking out every which way, as if it had a fright
    }

    /// <summary>What kind of figure a walker is: which painter draws it and which body it has.</summary>
    enum Figure
    {
        Minion,   // a yellow capsule in overalls and goggles
        Bean,     // a tall thin man in a tweed jacket: Mr. Bean
        Bird,     // a round Angry Bird with a beak, crest and tail
        Panther   // a tall sleek pink panther, bipedal like the cartoon
    }

    /// <summary>Which Angry Bird: its colours and feathers come from this.</summary>
    enum BirdKind
    {
        Red,      // the red leader with black brows and two head feathers
        Blues,    // Jay, Jake and Jim: small, blue, no brows, two big head feathers
        Chuck,    // yellow and fast, with a black mohawk
        Bomb,     // black and round, with a fuse on top
        Matilda,  // white and motherly, with a pink-cheeked head feather
        Hal,      // green with a long orange beak
        Terence,  // huge and dark red with freckles
        Bubbles,  // small and orange, blows up big
        Stella,   // pink and agile, with a feather bow
        Silver    // grey falcon with a red-tipped beak and slicked-back crest
    }

    /// <summary>
    /// The measurements of a kind of figure that the behaviour needs: how
    /// tall it is, where its eyes and shoulders are, how wide it stands,
    /// how far underground it hides. All in units (one unit is one sprite
    /// cell on screen).
    /// </summary>
    sealed class Anatomy
    {
        public readonly double Height;        // soles to crown
        public readonly double EyeV;          // height of the eyes
        public readonly double MouthV;        // height of the mouth
        public readonly double ShoulderV;     // height of the shoulders, for aiming an arm
        public readonly double SeatV;         // height a toot comes from
        public readonly int HalfWidth;        // half the body width in cells, for keeping a distance
        public readonly int HiddenDepth;      // feet this far underground hides everything
        public readonly int PeekDepth;        // feet this far underground shows the eyes
        public readonly double StrideCells;   // distance covered by one full walk cycle
        public readonly int CanvasRows;       // rows of the frame canvas it is drawn in
        public readonly double ShadowRadius;  // half-width of the shadow under the feet

        Anatomy(double height, double eyeV, double mouthV, double shoulderV, double seatV, int halfWidth,
                int hiddenDepth, int peekDepth, double strideCells, int canvasRows, double shadowRadius)
        {
            Height = height;
            EyeV = eyeV;
            MouthV = mouthV;
            ShoulderV = shoulderV;
            SeatV = seatV;
            HalfWidth = halfWidth;
            HiddenDepth = hiddenDepth;
            PeekDepth = peekDepth;
            StrideCells = strideCells;
            CanvasRows = canvasRows;
            ShadowRadius = shadowRadius;
        }

        /// <summary>Short legs, quick steps, eyes in the middle of the body.</summary>
        public static readonly Anatomy Minion = new Anatomy(18, 12.5, 8, 10.2, 3, 9, 20, 10, 10, 48, 6.4);

        /// <summary>Tall and thin, long legs, a head on a neck.</summary>
        public static readonly Anatomy Bean = new Anatomy(28, 24.9, 23.0, 19.4, 11, 8, 30, 22, 16, 56, 5.0);

        /// <summary>Round and hoppy: a ball of feathers with its eyes up top and a beak in front.</summary>
        public static readonly Anatomy Bird = new Anatomy(14, 10.2, 7.6, 8.4, 2.5, 8, 16, 9, 8, 48, 5.2);

        /// <summary>Tall and sleek like the cartoon Pink Panther: long legs, a narrow waist, a cat head on a neck.</summary>
        public static readonly Anatomy Panther = new Anatomy(26, 22.6, 20.6, 17.8, 10, 8, 28, 20, 15, 56, 5.2);
    }

    /// <summary>What a walker is born with: its kind of figure and, for a Minion, how many eyes, their colour, its hair.</summary>
    sealed class Look
    {
        public const int Brown = 0x6B4423;
        public const int Green = 0x3E8E4B;
        public const int Blue = 0x4A72B5;
        public const int Amber = 0xB07A2A;

        public readonly Figure Figure;
        public readonly bool OneEye;
        public readonly HairStyle Hair;
        public readonly BirdKind Kind;

        /// <summary>Iris colours (RGB) of the eye further from the viewer and the nearer one.</summary>
        public readonly int FarIris, NearIris;

        public Look(bool oneEye, HairStyle hair, int farIris, int nearIris)
        {
            Figure = Figure.Minion;
            OneEye = oneEye;
            Hair = hair;
            Kind = BirdKind.Red;
            FarIris = farIris;
            NearIris = nearIris;
        }

        public Look(BirdKind kind)
        {
            Figure = Figure.Bird;
            OneEye = false;
            Hair = HairStyle.None;
            Kind = kind;
            FarIris = NearIris = Brown;
        }

        public Look(bool oneEye, HairStyle hair, int iris)
            : this(oneEye, hair, iris, iris)
        {
        }

        public Look(bool oneEye, HairStyle hair)
            : this(oneEye, hair, Brown, Brown)
        {
        }

        Look(Figure figure)
        {
            Figure = figure;
            OneEye = false;
            Hair = HairStyle.None;
            Kind = BirdKind.Red;
            FarIris = NearIris = Brown;
        }

        /// <summary>Mr. Bean: the painter knows what he looks like.</summary>
        public static readonly Look Bean = new Look(Figure.Bean);

        /// <summary>The Pink Panther: the painter knows what he looks like.</summary>
        public static readonly Look Panther = new Look(Figure.Panther);

        /// <summary>The body this figure has.</summary>
        public Anatomy Anatomy
        {
            get { return Figure == Figure.Bean ? Anatomy.Bean : Figure == Figure.Bird ? Anatomy.Bird : Figure == Figure.Panther ? Anatomy.Panther : Anatomy.Minion; }
        }

        /// <summary>
        /// A random overalls colour (0xRRGGBB): any hue but yellow, strong enough to read
        /// against the yellow skin and dark enough for the straps to show.
        /// </summary>
        public static int RandomDress(System.Random rng)
        {
            // Hue sector, 0..6, skipping the yellows that vanish against the skin.
            double h = 1.3 + rng.NextDouble() * 5.3;
            if (h >= 6) h -= 6;
            double s = 0.55 + rng.NextDouble() * 0.4;   // saturation
            double v = 0.45 + rng.NextDouble() * 0.4;   // brightness
            int i = (int)h;
            double f = h - i;
            double p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f));
            double r, g, b;
            switch (i)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break;
            }
            return ((int)(r * 255 + 0.5) << 16) | ((int)(g * 255 + 0.5) << 8) | (int)(b * 255 + 0.5);
        }
    }
}
