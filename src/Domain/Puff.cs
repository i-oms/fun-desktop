namespace IdleWalker.Domain
{
    /// <summary>A speck of dust kicked up by a landing or a skid.</summary>
    sealed class Puff
    {
        public double X, Y;       // screen pixels; Y is the bottom of the puff
        public double Vx, Vy;
        public double Life, MaxLife;

        /// <summary>0 when fresh, 1 when about to vanish.</summary>
        public double Age
        {
            get { return 1 - Life / MaxLife; }
        }
    }
}
