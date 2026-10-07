namespace IdleWalker.Domain
{
    enum Temperament
    {
        /// <summary>Minds its own business; nods at walkers it meets.</summary>
        Calm,

        /// <summary>Waves at everyone, stops to greet.</summary>
        Friendly,

        /// <summary>Walks fast, hops a lot, charges at others and shoves them.</summary>
        Aggressive
    }

    /// <summary>What makes one walker different from another.</summary>
    sealed class Personality
    {
        public readonly string Name;
        public readonly Temperament Temperament;
        public readonly Palette Palette;
        public readonly Art.Faces Faces;

        /// <summary>Multiplier on the base walking speed.</summary>
        public readonly double SpeedFactor;

        /// <summary>Seconds between hops (random in this range).</summary>
        public readonly double HopMin, HopMax;

        /// <summary>Seconds between idle antics (random in this range).</summary>
        public readonly double IdleMin, IdleMax;

        /// <summary>Chance an idle moment is spent looking around / waving; the rest is resting.</summary>
        public readonly double LookWeight, WaveWeight;

        Personality(string name, Temperament temperament, Palette palette, Art.Faces faces,
                    double speedFactor, double hopMin, double hopMax, double idleMin, double idleMax,
                    double lookWeight, double waveWeight)
        {
            Name = name;
            Temperament = temperament;
            Palette = palette;
            Faces = faces;
            SpeedFactor = speedFactor;
            HopMin = hopMin;
            HopMax = hopMax;
            IdleMin = idleMin;
            IdleMax = idleMax;
            LookWeight = lookWeight;
            WaveWeight = waveWeight;
        }

        /// <summary>
        /// The cast, in the order they join as the count goes up. The first
        /// is the original walker, exactly as it always behaved.
        /// </summary>
        public static readonly Personality[] Roster =
        {
            new Personality("Teal", Temperament.Calm, Palette.Default, Art.CalmFaces,
                            1.0, 4, 9, 5, 11, 0.40, 0.35),

            new Personality("Red", Temperament.Aggressive,
                            Palette.Default.With('T', 0xD83A3A).With('t', 0x9C2727).With('Y', 0x5B5B66)
                                           .With('O', 0x3B2A4A).With('N', 0x5C4B3B).With('n', 0x40332A),
                            Art.AngryFaces,
                            1.2, 2.5, 6, 6, 14, 0.50, 0.00),

            new Personality("Pink", Temperament.Friendly,
                            Palette.Default.With('T', 0xE0569B).With('t', 0xA83C73).With('Y', 0xFFF1C1)
                                           .With('O', 0xFFD23F).With('N', 0x2F8F5B).With('n', 0x226841),
                            Art.FriendlyFaces,
                            0.9, 4, 9, 5, 11, 0.25, 0.55),

            new Personality("Blue", Temperament.Friendly,
                            Palette.Default.With('T', 0x3FA7E0).With('t', 0x2C78A6).With('Y', 0xFFD23F)
                                           .With('O', 0xE04545).With('N', 0x6B4FA3).With('n', 0x4C3878),
                            Art.FriendlyFaces,
                            0.95, 4, 9, 5, 11, 0.25, 0.55),

            new Personality("Olive", Temperament.Aggressive,
                            Palette.Default.With('T', 0x8A9A2E).With('t', 0x5F6B1F).With('Y', 0xF2762E)
                                           .With('O', 0x1FA39A).With('N', 0x8B3A4A).With('n', 0x652834),
                            Art.AngryFaces,
                            1.15, 2.5, 6, 6, 14, 0.50, 0.00),
        };
    }
}
