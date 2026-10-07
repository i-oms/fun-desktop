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
        public readonly Look Look;

        /// <summary>The face it wears when nothing in particular is happening.</summary>
        public readonly Expression BaseFace;

        /// <summary>Multiplier on the base walking speed.</summary>
        public readonly double SpeedFactor;

        /// <summary>Seconds between hops (random in this range).</summary>
        public readonly double HopMin, HopMax;

        /// <summary>Seconds between idle antics (random in this range).</summary>
        public readonly double IdleMin, IdleMax;

        /// <summary>Chance an idle moment is spent looking around / waving; the rest is resting.</summary>
        public readonly double LookWeight, WaveWeight;

        Personality(string name, Temperament temperament, Look look,
                    double speedFactor, double hopMin, double hopMax, double idleMin, double idleMax,
                    double lookWeight, double waveWeight)
        {
            Name = name;
            Temperament = temperament;
            Look = look;
            BaseFace = temperament == Temperament.Friendly ? Expression.Happy :
                       temperament == Temperament.Aggressive ? Expression.Grumpy : Expression.Neutral;
            SpeedFactor = speedFactor;
            HopMin = hopMin;
            HopMax = hopMax;
            IdleMin = idleMin;
            IdleMax = idleMax;
            LookWeight = lookWeight;
            WaveWeight = waveWeight;
        }

        /// <summary>
        /// The Minions, in the order they join as the count goes up. Kevin is
        /// the first out and behaves exactly as the original walker did.
        /// </summary>
        public static readonly Personality[] Roster =
        {
            // Two eyes, a few sprouts of hair, keeps to himself.
            new Personality("Kevin", Temperament.Calm, new Look(false, HairStyle.Sprouts),
                            1.0, 4, 9, 5, 11, 0.40, 0.35),

            // One eye, hair combed flat, the troublemaker.
            new Personality("Stuart", Temperament.Aggressive, new Look(true, HairStyle.Combed),
                            1.2, 2.5, 6, 6, 14, 0.50, 0.00),

            // Bald, one green eye and one brown, loves everybody.
            new Personality("Bob", Temperament.Friendly, new Look(false, HairStyle.None, Look.Green, Look.Brown),
                            0.9, 4, 9, 5, 11, 0.25, 0.55),

            // Two eyes, hair parted in the middle, always waving.
            new Personality("Dave", Temperament.Friendly, new Look(false, HairStyle.Parted),
                            0.95, 4, 9, 5, 11, 0.25, 0.55),

            // Two eyes, crew cut, picks fights.
            new Personality("Mel", Temperament.Aggressive, new Look(false, HairStyle.Crew),
                            1.15, 2.5, 6, 6, 14, 0.50, 0.00),
        };
    }
}
