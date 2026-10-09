namespace IdleWalker.Domain
{
    enum Temperament
    {
        /// <summary>Minds its own business; nods at walkers it meets.</summary>
        Calm,

        /// <summary>Waves at everyone, stops to greet.</summary>
        Friendly,

        /// <summary>Walks fast, charges at others and shoves them.</summary>
        Aggressive,

        /// <summary>Frightened of everything: hides, runs from the bullies, faints when pranked.</summary>
        Timid,

        /// <summary>The clown: everything is a joke, and it copies whatever it sees.</summary>
        Goofy,

        /// <summary>Mr. Bean: a child in a grown man's body - fidgets, sneaks, pokes, shows off Teddy.</summary>
        Eccentric
    }

    /// <summary>The things a walker gets up to when it stops walking for a moment.</summary>
    enum Antic
    {
        Look,      // stop and look around
        Wave,      // wave at the cursor or at company
        Rest,      // stand and breathe
        Dance,     // a jig on the spot, arms pumping, a note or two
        Twirl,     // arms out, one full spin, ta-da
        Point,     // point at the cursor and giggle
        Yawn,      // a big stretch and a yawn
        Doze,      // yawn, then fall asleep standing up until it jerks awake
        Scratch,   // scratch the side of the head, thinking, then: an idea!
        Whistle,   // whistle a tune, notes floating up
        Sneeze,    // wind up and sneeze
        Box,       // shadow boxing
        Taunt,     // tongue out, beckoning: come and have a go
        Stomp,     // a two-footed tantrum
        Flex,      // flex both arms and admire them
        Joy,       // two hops with the arms thrown up
        Laugh,     // hold the belly and laugh at the viewer
        Hiccups,   // a run of hiccups, each one a jolt
        Shrug,     // arms out: oh well
        Trip,      // stumble and fall flat while walking, then get up embarrassed
        Boo,       // shout at somebody standing quietly, then laugh at them
        Strum,     // air guitar, head banging, notes flying - and whoever is near wants to dance
        Pushups,   // drop and give me ten
        Jog,       // running on the spot, arms pumping
        Salute,    // stand to attention and salute the viewer
        Moonwalk,  // slide backwards, cool as you like
        Toot,      // a little puff from behind, then whistle innocently (the others notice)
        Peekaboo,  // hands over the eyes, then... peekaboo!
        Flap,      // elbows out, strutting like a chicken
        Hide,      // hands over the eyes, trembling, peeking through the fingers
        Fret,      // biting its nails, eyes darting, a drop of sweat
        Faint,     // keel over backwards, out cold, then come round
        Dig,       // dig at the ground in a cloud of dust, find nothing, shrug
        Sleepwalk, // wander forward fast asleep, arms out, Zzz, until it bumps into something
        Daydream,  // gaze off into the distance with a dreamy smile, then snap out of it
        Kiss,      // blow a kiss at the cursor, a heart floating off

        // Mr. Bean's own.
        Teddy,     // bring Teddy out of the jacket, gaze at him, make him wave at you, tuck him back
        Hello,     // the awkward hello: arm straight up beside the head, hand wagging stiffly, up on the toes
        Waggle,    // lean in, hands behind the back, and waggle the eyebrows at you
        Sneak,     // look both ways, then creep forward on tiptoe, hands up, and freeze with an innocent face
        Fuss,      // straighten the tie, brush the lapels, pat the hair
        Ponder,    // finger to the chin, eyes rolled up, then: an idea, finger in the air
        Jive,      // the Bean dance: elbows pumping, hips twisting, knees bouncing
        Poke       // creep up and prod somebody (or the cursor) with one finger, then look innocent
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

        /// <summary>Seconds between hops (random in this range) - and only while it has energy to spare.</summary>
        public readonly double HopMin, HopMax;

        /// <summary>Seconds between idle antics (random in this range).</summary>
        public readonly double IdleMin, IdleMax;

        /// <summary>How slowly it tires: 1 is normal, less tires quickly.</summary>
        public readonly double Stamina;

        /// <summary>How quickly it starts wanting company, 0..1.</summary>
        public readonly double Sociable;

        /// <summary>How quickly the cursor starts to intrigue it, 0..1.</summary>
        public readonly double Curious;

        /// <summary>How often it trips over its own feet: 0 never, 1 every minute or two of walking.</summary>
        public readonly double Clumsy;

        /// <summary>Its build: width and height relative to the standard Minion (1 is standard).</summary>
        public readonly double BuildX, BuildY;

        /// <summary>Copies whatever antic it sees somebody else start.</summary>
        public readonly bool Mimic;

        /// <summary>What it does on reaching the cursor after walking over to inspect it.</summary>
        public readonly Antic CursorAntic;

        /// <summary>What it does with an idle moment, each weighted; the weights need not add up to anything.</summary>
        public readonly Habit[] Habits;

        Personality(string name, Temperament temperament, Look look, Expression baseFace, Expression restFace,
                    double speedFactor, double walkBob, double walkLean,
                    double hopMin, double hopMax, double idleMin, double idleMax,
                    double stamina, double sociable, double curious, double clumsy,
                    double buildX, double buildY, bool mimic, Antic cursorAntic, params Habit[] habits)
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
            Stamina = stamina;
            Sociable = sociable;
            Curious = curious;
            Clumsy = clumsy;
            BuildX = buildX;
            BuildY = buildY;
            Mimic = mimic;
            CursorAntic = cursorAntic;
            Habits = habits;
        }

        /// <summary>Friendly, goofy or Mr. Bean: waves, dances, plays tag, chats.</summary>
        public bool Warm
        {
            get { return Temperament == Temperament.Friendly || Temperament == Temperament.Goofy || Temperament == Temperament.Eccentric; }
        }

        /// <summary>Which painter draws it and which body it has.</summary>
        public Figure Figure
        {
            get { return Look.Figure; }
        }

        public Anatomy Anatomy
        {
            get { return Look.Anatomy; }
        }

        /// <summary>
        /// Mr. Bean. Not one of the Minions: he comes out alongside them when
        /// switched on in the menu. Tall, thin, in his tweed jacket and red
        /// tie. Shows off Teddy (the Minions come to look), says hello with
        /// a stiff wave, waggles his eyebrows at you, creeps about on tiptoe,
        /// fusses with his tie and hair, ponders, does the Bean dance, and
        /// pokes anyone standing quietly - and your cursor - with one finger.
        /// </summary>
        public static readonly Personality Bean =
            new Personality("Mr. Bean", Temperament.Eccentric, Look.Bean,
                            Expression.Gormless, Expression.Knowing,
                            0.95, 0.22, 0.05, 60, 120, 4, 9,
                            0.9, 0.7, 1.0, 0, 1, 1, false, Antic.Poke,
                            H(Antic.Teddy, 3), H(Antic.Hello, 2), H(Antic.Waggle, 2), H(Antic.Sneak, 2),
                            H(Antic.Fuss, 2), H(Antic.Ponder, 1.5), H(Antic.Jive, 1.5), H(Antic.Look, 1.5),
                            H(Antic.Rest, 1), H(Antic.Doze, 1), H(Antic.Whistle, 0.5), H(Antic.Hiccups, 0.5),
                            H(Antic.Sneeze, 0.4));

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
            // Two eyes, a few sprouts of hair, keeps to himself. Tires
            // easily, very curious about the cursor, whistles, scratches his
            // head over things, gets hiccups, and nods off if left alone.
            new Personality("Kevin", Temperament.Calm, new Look(false, HairStyle.Sprouts),
                            Expression.Neutral, Expression.Sleepy,
                            1.0, 0.30, 0.0, 25, 45, 5, 11,
                            0.7, 0.6, 1.0, 0, 1, 1, false, Antic.Scratch,
                            H(Antic.Look, 3), H(Antic.Rest, 2), H(Antic.Whistle, 2), H(Antic.Scratch, 2),
                            H(Antic.Doze, 1.5), H(Antic.Yawn, 1), H(Antic.Hiccups, 0.8), H(Antic.Wave, 1),
                            H(Antic.Sneeze, 0.5)),

            // One eye, hair combed flat, the troublemaker. Shadow-boxes,
            // sticks his tongue out at you, throws tantrums, sneaks up on
            // anyone dozing and shouts, and argues with Mel.
            new Personality("Stuart", Temperament.Aggressive, new Look(true, HairStyle.Combed),
                            Expression.Grumpy, Expression.Bored,
                            1.2, 0.38, 0.2, 12, 25, 6, 14,
                            1.2, 0.3, 0.6, 0, 1, 1, false, Antic.Taunt,
                            H(Antic.Look, 2.5), H(Antic.Box, 2), H(Antic.Taunt, 2), H(Antic.Stomp, 1),
                            H(Antic.Laugh, 0.8), H(Antic.Rest, 0.8)),

            // Bald, one green eye and one brown, loves everybody. The most
            // sociable: visits friends for a chat, comforts anyone who has
            // been shoved, plays tag, dances - and trips over his own feet.
            new Personality("Bob", Temperament.Friendly, new Look(false, HairStyle.None, Look.Green, Look.Brown),
                            Expression.Happy, Expression.Happy,
                            0.9, 0.40, 0.05, 18, 35, 5, 11,
                            1.0, 1.0, 0.7, 1.0, 1, 1, false, Antic.Point,
                            H(Antic.Wave, 3), H(Antic.Dance, 2.5), H(Antic.Point, 1.5), H(Antic.Joy, 1.5),
                            H(Antic.Laugh, 1), H(Antic.Rest, 1), H(Antic.Look, 1), H(Antic.Hiccups, 0.5),
                            H(Antic.Sneeze, 0.3)),

            // Two eyes, hair parted in the middle, the show-off: the most
            // energetic, twirls, jumps for joy, joins every dance, plays tag.
            new Personality("Dave", Temperament.Friendly, new Look(false, HairStyle.Parted),
                            Expression.Cheerful, Expression.Happy,
                            0.95, 0.42, 0.0, 12, 25, 5, 11,
                            1.4, 0.8, 0.8, 0, 1, 1, false, Antic.Point,
                            H(Antic.Wave, 2.5), H(Antic.Twirl, 2.5), H(Antic.Point, 2), H(Antic.Joy, 1.5),
                            H(Antic.Dance, 1), H(Antic.Look, 1), H(Antic.Rest, 0.8)),

            // Two eyes, crew cut, picks fights. Flexes, stomps, boxes,
            // pranks the sleepers, argues with Stuart.
            new Personality("Mel", Temperament.Aggressive, new Look(false, HairStyle.Crew),
                            Expression.Stern, Expression.Bored,
                            1.15, 0.34, 0.25, 15, 30, 6, 14,
                            1.3, 0.3, 0.5, 0, 1, 1, false, Antic.Taunt,
                            H(Antic.Look, 2), H(Antic.Flex, 2.5), H(Antic.Stomp, 1.5), H(Antic.Box, 1.5),
                            H(Antic.Taunt, 0.7), H(Antic.Rest, 0.8)),

            // Short, two eyes, spiky hair: the busker. Plays air guitar
            // (and whoever is near wants to dance), whistles, dances, laughs,
            // and serenades the cursor when it gets curious.
            new Personality("Jerry", Temperament.Friendly, new Look(false, HairStyle.Spiky),
                            Expression.Cool, Expression.Happy,
                            0.95, 0.36, 0.0, 15, 30, 5, 10,
                            1.1, 0.7, 0.6, 0, 1.0, 0.9, false, Antic.Strum,
                            H(Antic.Strum, 3), H(Antic.Dance, 2), H(Antic.Whistle, 1.5), H(Antic.Wave, 1.5),
                            H(Antic.Laugh, 1), H(Antic.Look, 1), H(Antic.Rest, 0.8), H(Antic.Joy, 0.7)),

            // Tall and thin, two blue eyes, a quiff: the drill sergeant.
            // Does push-ups and jogs on the spot, salutes you, never tires,
            // nods at the others and shakes his head at their fights.
            new Personality("Tim", Temperament.Calm, new Look(false, HairStyle.Quiff, Look.Blue),
                            Expression.Serious, Expression.Proud,
                            1.05, 0.26, 0.05, 20, 40, 5, 11,
                            1.6, 0.4, 0.5, 0, 0.94, 1.12, false, Antic.Salute,
                            H(Antic.Pushups, 2.5), H(Antic.Jog, 2.5), H(Antic.Salute, 2.5), H(Antic.Look, 2),
                            H(Antic.Flex, 0.8), H(Antic.Rest, 0.8), H(Antic.Stomp, 0.4)),

            // One eye, a single curl on top: the clown. Moonwalks, lets one
            // off and whistles innocently, plays peekaboo with the cursor,
            // struts like a chicken, trips - and copies whatever anybody
            // else starts doing.
            new Personality("Phil", Temperament.Goofy, new Look(true, HairStyle.Tuft),
                            Expression.Silly, Expression.Happy,
                            1.0, 0.44, 0.0, 10, 22, 4, 9,
                            1.2, 0.8, 0.9, 0.6, 1.0, 1.0, true, Antic.Peekaboo,
                            H(Antic.Moonwalk, 2.2), H(Antic.Toot, 2), H(Antic.Peekaboo, 2), H(Antic.Flap, 2.5),
                            H(Antic.Laugh, 1.5), H(Antic.Dance, 1), H(Antic.Taunt, 0.8), H(Antic.Hiccups, 0.6),
                            H(Antic.Look, 0.8)),

            // Small, two amber eyes, hair standing on end: scared of his own
            // shadow. Hides behind his hands, bites his nails, runs from the
            // bullies before they even arrive, faints when pranked, and peeks
            // at the cursor through his fingers.
            new Personality("Carl", Temperament.Timid, new Look(false, HairStyle.Wild, Look.Amber),
                            Expression.Nervous, Expression.Worried,
                            1.1, 0.34, 0.1, 30, 60, 4, 9,
                            0.9, 0.5, 0.8, 0, 0.96, 0.96, false, Antic.Hide,
                            H(Antic.Look, 3), H(Antic.Fret, 2.5), H(Antic.Hide, 2), H(Antic.Rest, 1.5),
                            H(Antic.Shrug, 0.8), H(Antic.Hiccups, 0.6), H(Antic.Sneeze, 0.4)),

            // Stocky, one eye, bald, away with the fairies. Daydreams, digs
            // for something he has lost, sleepwalks, nods off, and blows
            // kisses at the cursor.
            new Personality("Norbert", Temperament.Calm, new Look(true, HairStyle.None),
                            Expression.Dopey, Expression.Sleepy,
                            0.85, 0.32, 0.0, 30, 60, 5, 12,
                            0.8, 0.5, 0.9, 0.3, 1.1, 0.96, false, Antic.Kiss,
                            H(Antic.Daydream, 3), H(Antic.Dig, 2), H(Antic.Sleepwalk, 1.5), H(Antic.Kiss, 1.5),
                            H(Antic.Doze, 1.2), H(Antic.Scratch, 1), H(Antic.Look, 1), H(Antic.Hiccups, 0.8),
                            H(Antic.Rest, 0.8)),
        };
    }
}
