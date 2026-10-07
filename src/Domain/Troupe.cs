using System;
using System.Collections.Generic;
using System.Drawing;

namespace IdleWalker.Domain
{
    /// <summary>
    /// The whole cast: brings walkers out one at a time, sends them away,
    /// and lets them meet - greetings between the sociable ones, shoving by
    /// the aggressive ones.
    /// </summary>
    sealed class Troupe
    {
        const double GreetDistanceBodies = 2.2;   // how close two walkers get before saying hello
        const double ContactDistanceBodies = 0.9; // how close a charger gets before the shove lands
        const double GreetCooldownSeconds = 7;

        readonly IWorld world;
        readonly Random rng;
        readonly List<Walker> walkers = new List<Walker>();
        int count = 1;
        double spawnTimer;

        public Troupe(IWorld world, Random rng)
        {
            this.world = world;
            this.rng = rng;
        }

        /// <summary>Every walker created so far, in roster order.</summary>
        public IList<Walker> Walkers { get { return walkers; } }

        /// <summary>How many walkers should be out; the rest are sent away.</summary>
        public int Count
        {
            get { return count; }
            set { count = Math.Max(1, Math.Min(Settings.MaxWalkers, value)); }
        }

        public bool AnyVisible
        {
            get
            {
                foreach (Walker walker in walkers)
                    if (walker.IsVisible) return true;
                return false;
            }
        }

        /// <param name="dt">Seconds since the last step.</param>
        /// <param name="cursor">Where the cursor is resting.</param>
        /// <param name="wantOut">The cursor has been idle long enough and we're not paused.</param>
        /// <param name="disturbed">The cursor just moved, or we were paused.</param>
        public void Step(double dt, Point cursor, bool wantOut, bool disturbed)
        {
            while (walkers.Count < count)
                walkers.Add(new Walker(world, rng, Personality.Roster[walkers.Count]));

            if (disturbed)
            {
                foreach (Walker walker in walkers) walker.Startle();
            }

            // Bring the cast out one at a time, a beat apart.
            if (wantOut)
            {
                spawnTimer -= dt;
                if (spawnTimer <= 0)
                {
                    Walker next = NextToAppear();
                    if (next != null)
                    {
                        next.Spawn(cursor, PickSpawnX(cursor));
                        spawnTimer = 0.7 + rng.NextDouble() * 0.7;
                    }
                }
            }
            else
            {
                spawnTimer = 0;
            }

            for (int i = count; i < walkers.Count; i++) walkers[i].Leave();

            foreach (Walker walker in walkers)
                if (walker.IsVisible) walker.Step(dt, cursor);

            Interact();
        }

        Walker NextToAppear()
        {
            for (int i = 0; i < count; i++)
                if (walkers[i].IsHidden) return walkers[i];
            return null;
        }

        // A random spot on the cursor's monitor, preferably clear of the others.
        double PickSpawnX(Point cursor)
        {
            Rectangle bounds = world.MonitorBoundsAt(cursor);
            double margin = Math.Min(bounds.Width / 4.0, 120);
            double best = bounds.Left + bounds.Width / 2.0;
            double bestGap = -1;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                double x = bounds.Left + margin + rng.NextDouble() * (bounds.Width - 2 * margin);
                double gap = double.MaxValue;
                foreach (Walker other in walkers)
                    if (other.IsVisible) gap = Math.Min(gap, Math.Abs(other.CentreX - x));
                if (gap >= 200) return x;
                if (gap > bestGap)
                {
                    bestGap = gap;
                    best = x;
                }
            }
            return best;
        }

        // ---- Social life ---------------------------------------------------------

        void Interact()
        {
            // Tell everyone who their nearest neighbour is, for waving at.
            foreach (Walker walker in walkers)
            {
                if (!walker.IsVisible) continue;
                Walker nearest = null;
                double nearestDistance = 350 * walker.Scale / Settings.BasePixelSize;
                foreach (Walker other in walkers)
                {
                    if (other == walker || !other.IsVisible || other.IsLeaving) continue;
                    double distance = Math.Abs(other.CentreX - walker.CentreX);
                    if (distance < nearestDistance)
                    {
                        nearest = other;
                        nearestDistance = distance;
                    }
                }
                walker.SetCompany(nearest != null, nearest != null ? nearest.CentreX : 0);
            }

            for (int i = 0; i < walkers.Count; i++)
            {
                Walker a = walkers[i];
                if (!a.IsGrounded) continue;

                for (int j = i + 1; j < walkers.Count; j++)
                {
                    Walker b = walkers[j];
                    if (!b.IsGrounded) continue;
                    if (Math.Abs(a.FeetY - b.FeetY) > 2) continue; // different monitors' ground levels

                    double dx = b.CentreX - a.CentreX;
                    double distance = Math.Abs(dx);
                    int towardsB = dx >= 0 ? 1 : -1;

                    if (ResolveShove(a, b, distance, towardsB)) continue;
                    TryGreet(a, b, distance, towardsB);
                }

                if (a.Temperament == Temperament.Aggressive) ConsiderCharging(a);
            }
        }

        // A charging walker that has reached its target shoves it.
        static bool ResolveShove(Walker a, Walker b, double distance, int towardsB)
        {
            bool aHits = a.IsCharging && a.ChargeTarget == b && distance < ContactDistanceBodies * a.BodyWidth;
            bool bHits = b.IsCharging && b.ChargeTarget == a && distance < ContactDistanceBodies * b.BodyWidth;
            if (!aHits && !bHits) return false;

            if (aHits && bHits)
            {
                // Two bullies collide and both go flying.
                a.Knock(-towardsB);
                b.Knock(towardsB);
            }
            else if (aHits)
            {
                b.Knock(towardsB);
                a.Gloat();
            }
            else
            {
                a.Knock(-towardsB);
                b.Gloat();
            }
            return true;
        }

        // Two walkers about to pass each other stop to say hello - unless
        // they are aggressive, in which case they just keep walking.
        static void TryGreet(Walker a, Walker b, double distance, int towardsB)
        {
            if (distance >= GreetDistanceBodies * Math.Max(a.BodyWidth, b.BodyWidth)) return;
            if (!a.CanGreet || !b.CanGreet) return;
            if (a.GreetCooldown > 0 || b.GreetCooldown > 0) return;

            bool approaching = (a.IsWalking && a.Facing == towardsB) || (b.IsWalking && b.Facing == -towardsB);
            if (!approaching) return;

            a.GreetCooldown = GreetCooldownSeconds;
            b.GreetCooldown = GreetCooldownSeconds;
            if (a.Temperament != Temperament.Aggressive) a.Greet(towardsB, a.Temperament == Temperament.Friendly);
            if (b.Temperament != Temperament.Aggressive) b.Greet(-towardsB, b.Temperament == Temperament.Friendly);
        }

        // An aggressive walker with somebody in range goes for them.
        void ConsiderCharging(Walker bully)
        {
            if (!bully.IsWalking || bully.ChargeCooldown > 0) return;

            double reach = 380 * bully.Scale / Settings.BasePixelSize;
            double behind = 120 * bully.Scale / Settings.BasePixelSize;
            Walker target = null;
            double targetDistance = double.MaxValue;
            foreach (Walker other in walkers)
            {
                if (other == bully || !other.IsGrounded || other.IsLeaving) continue;
                if (Math.Abs(other.FeetY - bully.FeetY) > 2) continue;
                double dx = other.CentreX - bully.CentreX;
                double distance = Math.Abs(dx);
                bool inFront = (dx >= 0 ? 1 : -1) == bully.Facing;
                if (distance > (inFront ? reach : behind) || distance >= targetDistance) continue;
                target = other;
                targetDistance = distance;
            }
            if (target != null) bully.StartCharge(target);
        }
    }
}
