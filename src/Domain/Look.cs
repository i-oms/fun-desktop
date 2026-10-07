namespace IdleWalker.Domain
{
    enum HairStyle
    {
        None,     // bald
        Sprouts,  // a few strands standing up
        Combed,   // swept flat over the top
        Parted,   // parted in the middle
        Crew      // a short, even crew cut
    }

    /// <summary>What a Minion is born with: how many eyes, their colour, its hair.</summary>
    sealed class Look
    {
        public const int Brown = 0x6B4423;
        public const int Green = 0x3E8E4B;

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

        public Look(bool oneEye, HairStyle hair)
            : this(oneEye, hair, Brown, Brown)
        {
        }
    }
}
