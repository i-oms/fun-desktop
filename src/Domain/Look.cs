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
    }
}
