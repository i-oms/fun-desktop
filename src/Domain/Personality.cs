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

    /// <summary>The things a walker gets up to when it stops walking for a moment.</summary>
    enum Antic
    {
        Look,     // stop and look around
        Wave,     // wave at the cursor or at company
        Rest,     // stand and breathe
        Dance,    // a jig on the spot, arms pumping, a note or two
        Twirl,    // arms out, one full spin, ta-da
        Point,    // point at the cursor and giggle
        Yawn,     // a big stretch and a yawn
        Doze,     // yawn, then fall asleep standing up until it jerks awake
        Scratch,  // scratch the side of the head, thinking, then: an idea!
        Whistle,  // whistle a tune, notes floating up
        Sneeze,   // wind up and sneeze
        Box,      // shadow boxing
        Taunt,    // tongue out, beckoning: come and have a go
        Stomp,    // a two-footed tantrum
        Flex,     // flex both arms and admire them
        Joy,      // two hops with the arms thrown up
        Laugh     // hold the belly and laugh at the viewer
    }

    /// <summary>One antic and how often, relative to the others, the walker picks it.</summary>
    struct Habit
    {
        public readonly Antic Antic;
        public readonly double Weight;

        public Habit(Antic antic, double weight)
        {
            Antic = antic;
            Weight = weight;
        }
    }

    /// <summary>What makes one walker different from another.</summary>
    sealed class Personality
    {
        public readonly string Name;
        public readonly Temperament Temperament;
        public readonly Look Look;

        /// <summary>The face it wears when nothing in particular is happening.</summary>
        public readonly Expression BaseFace;

        /// <summary>The face it settles into on a long rest.</summary>
        public readonly Expression RestFace;

        /// <summary>Multiplier on the base walking speed.</summary>
        public readonly double SpeedFactor;

        /// <summary>How much the upper body bobs with each step, in units, and how far forward it leans.</summary>
        public readonly double WalkBob, WalkLean;

        /// <summary>Seconds between hops (random in this range).</summary>
        public readonly double HopMin, HopMax;

        /// <summary>Seconds between idle antics (random in this range).</summary>
        public readonly double IdleMin, IdleMax;

        /// <summary>What it does with an idle moment, each weighted; the weights need not add up to anything.</summary>
        public readonly Habit[] Habits;
        public readonly double HabitTotal;

        Personality(string name, Temperament temperament, Look look, Expression baseFace, Expression restFace,
                    double speedFactor, double walkBob, double walkLean,
                    double hopMin, double hopMax, double idleMin, double idleMax, params Habit[] habits)
        {
            Name = name;
            Temperament = temperament;
            Look = look;
            BaseFace = baseFace;
            RestFace = restFace;
            SpeedFactor = speedFactor;
            WalkBob = walkBob;
            WalkLean = walkLean;
            HopMin = hopMin;
            HopMax = hopMax;
            IdleMin = idleMin;
            IdleMax = idleMax;
            Habits = habits;
            foreach (Habit habit in habits) HabitTotal += habit.Weight;
        }

        static Habit H(Antic antic, double weight)
        {
            return new Habit(antic, weight);
        }

        /// <summary>
        /// The Minions, in the order they join as the count goes up. Kevin is
        /// the first out.
        /// </summary>
        public static readonly Personality[] Roster =
        {
            // Two eyes, a few sprouts of hair, keeps to himself. Whistles,
            // scratches his head over things, and nods off if left alone.
            new Personality("Kevin", Temperament.Calm, new Look(false, HairStyle.Sprouts),
                            Expression.Neutral, Expression.Sleepy,
                            1.0, 0.30, 0.0, 4, 9, 5, 11,
                            H(Antic.Look, 3), H(Antic.Rest, 2), H(Antic.Whistle, 2), H(Antic.Scratch, 2),
                            H(Antic.Doze, 1.5), H(Antic.Yawn, 1), H(Antic.Wave, 1), H(Antic.Sneeze, 0.5)),

            // One eye, hair combed flat, the troublemaker. Shadow-boxes,
            // sticks his tongue out at you and throws tantrums.
            new Personality("Stuart", Temperament.Aggressive, new Look(true, HairStyle.Combed),
                            Expression.Grumpy, Expression.Bored,
                            1.2, 0.38, 0.2, 2.5, 6, 6, 14,
                            H(Antic.Look, 2.5), H(Antic.Box, 2), H(Antic.Taunt, 2), H(Antic.Stomp, 1),
                            H(Antic.Laugh, 0.8), H(Antic.Rest, 0.8)),

            // Bald, one green eye and one brown, loves everybody. Waves,
            // dances, points at you and giggles.
            new Personality("Bob", Temperament.Friendly, new Look(false, HairStyle.None, Look.Green, Look.Brown),
                            Expression.Happy, Expression.Happy,
                            0.9, 0.40, 0.05, 4, 9, 5, 11,
                            H(Antic.Wave, 3), H(Antic.Dance, 2.5), H(Antic.Point, 1.5), H(Antic.Joy, 1.5),
                            H(Antic.Laugh, 1), H(Antic.Rest, 1), H(Antic.Look, 1), H(Antic.Sneeze, 0.3)),

            // Two eyes, hair parted in the middle, the show-off: twirls,
            // jumps for joy, always waving.
            new Personality("Dave", Temperament.Friendly, new Look(false, HairStyle.Parted),
                            Expression.Cheerful, Expression.Happy,
                            0.95, 0.42, 0.0, 4, 9, 5, 11,
                            H(Antic.Wave, 2.5), H(Antic.Twirl, 2.5), H(Antic.Point, 2), H(Antic.Joy, 1.5),
                            H(Antic.Dance, 1), H(Antic.Look, 1), H(Antic.Rest, 0.8)),

            // Two eyes, crew cut, picks fights. Flexes, stomps, boxes.
            new Personality("Mel", Temperament.Aggressive, new Look(false, HairStyle.Crew),
                            Expression.Stern, Expression.Bored,
                            1.15, 0.34, 0.25, 2.5, 6, 6, 14,
                            H(Antic.Look, 2), H(Antic.Flex, 2.5), H(Antic.Stomp, 1.5), H(Antic.Box, 1.5),
                            H(Antic.Taunt, 0.7), H(Antic.Rest, 0.8)),
        };
    }
}
