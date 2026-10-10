using System;

namespace IdleWalker.Domain
{
    /// <summary>
    /// The same class, continued: how this individual moves - its own walk,
    /// not just its cast's. Six small numbers on top of the personality's
    /// build: how much it bobs, how far it leans, how fast it goes, how
    /// often it hops, how much it swaggers side to side, and its standing
    /// fidget. Every walker is born with a whisper of noise in these, so no
    /// two start alike, and then each runs little trials: change one thing,
    /// live with it for a while, keep it if life went well (energy kept,
    /// good things happened, ground covered), else go back. A (1+1)
    /// evolution strategy, a few lines, no libraries - the same trick as
    /// the brain, but for the body. Ranges are clamped so it can never
    /// learn anything silly, and everything reads through the existing
    /// pose fields, so no painter changes were needed.
    /// </summary>
    partial class Walker
    {
        const int StyleDims = 6;
        const double TrialSeconds = 24;

        // What each dimension means: 0 bob, 1 lean, 2 speed, 3 hop, 4 swagger, 5 fidget.
        readonly double[] style = new double[StyleDims];
        bool styleReady;
        double swaggerSeed, fidgetSeed, fidgetFreq;

        double trialTimer = TrialSeconds;
        double trialEnergy0, trialBonus, trialDist, trialBest = -10;
        int trialDim;
        double trialSaved;

        /// <summary>Bob amplitude for the stride, from its build plus its own touch.</summary>
        double EffBob()
        {
            return personality.WalkBob * (1 + style[0]);
        }

        /// <summary>Forward lean while walking, from its build plus its own touch.</summary>
        double EffLean()
        {
            return personality.WalkLean + style[1];
        }

        /// <summary>Speed multiplier, from its build plus its own touch.</summary>
        double EffSpeed()
        {
            return personality.SpeedFactor * (1 + style[2]);
        }

        /// <summary>Multiplier on the hop interval: above 1 hops lazier, below 1 livelier.</summary>
        double EffHop()
        {
            double m = 1 + style[3];
            return m < 0.3 ? 0.3 : m;
        }

        /// <summary>Side-to-side swagger with the stride, in degrees.</summary>
        double SwaggerYaw()
        {
            return style[4] * Math.Sin(walkPhase * 2 * Math.PI + swaggerSeed);
        }

        /// <summary>Its fidget while standing about: a small personal sway.</summary>
        double FidgetLean()
        {
            return Quantise(style[5] * Math.Sin(time * fidgetFreq + fidgetSeed));
        }

        static double ClampDim(int dim, double v)
        {
            double lo, hi;
            switch (dim)
            {
                case 0: lo = -0.45; hi = 0.6; break;    // bob
                case 1: lo = -0.2; hi = 0.25; break;    // lean
                case 2: lo = -0.15; hi = 0.25; break;   // speed
                case 3: lo = -0.5; hi = 1.5; break;     // hop interval
                case 4: lo = 0; hi = 7; break;          // swagger, degrees
                default: lo = 0; hi = 0.12; break;      // fidget
            }
            return v < lo ? lo : v > hi ? hi : v;
        }

        static double StepSize(int dim)
        {
            switch (dim)
            {
                case 0: return 0.08;
                case 1: return 0.03;
                case 2: return 0.04;
                case 3: return 0.12;
                case 4: return 0.8;
                default: return 0.02;
            }
        }

        void SeedStyle()
        {
            styleReady = true;
            style[0] = (rng.NextDouble() + rng.NextDouble() - 1) * 0.06;
            style[1] = (rng.NextDouble() + rng.NextDouble() - 1) * 0.02;
            style[2] = (rng.NextDouble() + rng.NextDouble() - 1) * 0.03;
            style[3] = (rng.NextDouble() + rng.NextDouble() - 1) * 0.1;
            style[4] = rng.NextDouble() * 2;
            style[5] = rng.NextDouble() * 0.05;
            for (int i = 0; i < StyleDims; i++) style[i] = ClampDim(i, style[i]);
            swaggerSeed = rng.NextDouble() * 2 * Math.PI;
            fidgetSeed = rng.NextDouble() * 2 * Math.PI;
            fidgetFreq = 2.0 + rng.NextDouble() * 1.4;
            trialEnergy0 = energy;
            trialTimer = TrialSeconds * (0.8 + rng.NextDouble() * 0.4);
        }

        /// <summary>One frame of the body learning: trial, judge, keep or revert, mutate.</summary>
        void StepStyle(double dt)
        {
            if (!styleReady) SeedStyle();
            trialTimer -= dt;
            if (trialTimer > 0) return;

            // Judge the trial: energy kept, good things banked, ground covered.
            double score = 2.0 * trialBonus + (energy - trialEnergy0) + 0.0004 * trialDist / sizeFactor;
            if (score > 2) score = 2; else if (score < -2) score = -2;
            if (score >= trialBest) trialBest = score;
            else style[trialDim] = trialSaved;
            trialBest *= 0.995;   // the world changes, so keep exploring

            // Next trial: change one thing a little.
            trialDim = rng.Next(StyleDims);
            trialSaved = style[trialDim];
            double push = (rng.NextDouble() + rng.NextDouble() + rng.NextDouble() - 1.5) * StepSize(trialDim);
            style[trialDim] = ClampDim(trialDim, trialSaved + push);
            trialEnergy0 = energy;
            trialBonus = 0;
            trialDist = 0;
            trialTimer = TrialSeconds * (0.8 + rng.NextDouble() * 0.4);
        }
    }
}
