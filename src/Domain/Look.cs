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

    /// <summary>What a Minion is born with: how many eyes, their colour, its hair.</summary>
    sealed class Look
    {
        public const int Brown = 0x6B4423;
        public const int Green = 0x3E8E4B;
        public const int Blue = 0x4A72B5;
        public const int Amber = 0xB07A2A;

        public readonly bool OneEye;
        public readonly HairStyle Hair;

        /// <summary>Iris colours (RGB) of the eye further from the viewer and the nearer one.</summary>
        public readonly int FarIris, NearIris;

        public Look(bool oneEye, HairStyle hair, int farIris, int nearIris)
        {
            OneEye = oneEye;
            Hair = hair;
            FarIris = farIris;
            NearIris = nearIris;
        }

        public Look(bool oneEye, HairStyle hair, int iris)
            : this(oneEye, hair, iris, iris)
        {
        }

        public Look(bool oneEye, HairStyle hair)
            : this(oneEye, hair, Brown, Brown)
        {
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
