using System;
using System.Collections.Generic;
using System.Drawing;

namespace IdleWalker.Domain
{
    /// <summary>
    /// The whole cast: brings walkers out one at a time, sends them away,
    /// and lets them meet - greetings and dances between the sociable ones,
    /// shoving and shouting matches by the aggressive ones, games of tag,
    /// onlookers reacting, and yawns and dances that spread.
    /// </summary>
    sealed class Troupe
    {
        const double GreetDistanceBodies = 2.2;   // how close two walkers get before saying hello
        const double ContactDistanceBodies = 0.9; // how close a charger gets before the shove (or the tag) lands
        const double GreetCooldownSeconds = 12;

        readonly IWorld world;
        readonly Random rng;
        readonly List<Walker> walkers = new List<Walker>();
        int count = 1;
        int dress = Settings.DressColors[0];
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

        /// <summary>
        /// The overalls colour, 0xRRGGBB, or Settings.RandomDress for a fresh
        /// random colour per Minion per appearance. A change shows at once.
        /// </summary>
        public int Dress
        {
            get { return dress; }
            set
            {
                dress = value;
                foreach (Walker walker in walkers) walker.Dress = PickDress();
            }
        }

        int PickDress()
        {
            return dress == Settings.RandomDress ? Look.RandomDress(rng) : dress;
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
                        next.Dress = PickDress();
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
            Spread();
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
            // Tell everyone who their nearest neighbour on the same ground is.
            foreach (Walker walker in walkers)
            {
                if (!walker.IsVisible) continue;
                Walker nearest = null;
                double nearestDistance = double.MaxValue;
                foreach (Walker other in walkers)
                {
                    if (other == walker || !other.IsVisible || other.IsLeaving) continue;
                    if (Math.Abs(other.FeetY - walker.FeetY) > 2) continue;
                    double distance = Math.Abs(other.CentreX - walker.CentreX);
                    if (distance < nearestDistance)
                    {
                        nearest = other;
                        nearestDistance = distance;
                    }
                }
                walker.SetCompany(nearest);
            }

            for (int i = 0; i < walkers.Count; i++)
            {
                Walker a = walkers[i];
                if (!a.IsGrounded) continue;

                // Somebody coming to tag you: run for it.
                if (a.IsCharging && a.IsPlayful)
                {
                    Walker quarry = a.ChargeTarget;
                    if (quarry != null && quarry.IsGrounded && !quarry.IsFleeing)
                        quarry.Scamper(quarry.CentreX >= a.CentreX ? 1 : -1);
                }

                // A bully bearing down on the timid one: it sees him coming and bolts.
                if (a.IsCharging && !a.IsPlayful)
                {
                    Walker prey = a.ChargeTarget;
                    if (prey != null && prey.Temperament == Temperament.Timid && prey.IsGrounded && !prey.IsFleeing &&
                        Math.Abs(prey.CentreX - a.CentreX) < 300 * a.Scale / Settings.BasePixelSize)
                        prey.Panic(prey.CentreX >= a.CentreX ? 1 : -1);
                }

                for (int j = i + 1; j < walkers.Count; j++)
                {
                    Walker b = walkers[j];
                    if (!b.IsGrounded) continue;
                    if (Math.Abs(a.FeetY - b.FeetY) > 2) continue; // different monitors' ground levels

                    double dx = b.CentreX - a.CentreX;
                    double distance = Math.Abs(dx);
                    int towardsB = dx >= 0 ? 1 : -1;

                    if (ResolveContact(a, b, distance, towardsB)) continue;
                    TryGreet(a, b, distance, towardsB);
                }

                if (a.Temperament == Temperament.Aggressive) ConsiderCharging(a);
            }
        }

        // A charging walker that has reached its target shoves it - or, in a
        // game of tag, tags it. Anyone close enough to see a shove reacts.
        bool ResolveContact(Walker a, Walker b, double distance, int towardsB)
        {
            bool aReaches = a.IsCharging && a.ChargeTarget == b && distance < ContactDistanceBodies * a.BodyWidth;
            bool bReaches = b.IsCharging && b.ChargeTarget == a && distance < ContactDistanceBodies * b.BodyWidth;
            if (!aReaches && !bReaches) return false;

            if (aReaches && a.IsPlayful)
            {
                a.Tag(b.CentreX);
                b.Tagged(a.CentreX);
                return true;
            }
            if (bReaches && b.IsPlayful)
            {
                b.Tag(a.CentreX);
                a.Tagged(b.CentreX);
                return true;
            }

            if (aReaches && bReaches)
            {
                // Two bullies collide and both go flying.
                a.Knock(-towardsB);
                b.Knock(towardsB);
                Witness(a, b, (a.CentreX + b.CentreX) / 2);
            }
            else if (aReaches)
            {
                b.Knock(towardsB);
                a.Gloat();
                Witness(a, b, b.CentreX);
            }
            else
            {
                a.Knock(-towardsB);
                b.Gloat();
                Witness(a, b, a.CentreX);
            }
            return true;
        }

        // Everyone else on the same ground within sight of x stops to gasp,
        // tut or jeer, as is their nature.
        void Witness(Walker a, Walker b, double x)
        {
            foreach (Walker other in walkers)
            {
                if (other == a || other == b || !other.IsGrounded) continue;
                if (Math.Abs(other.FeetY - a.FeetY) > 2) continue;
                if (Math.Abs(other.CentreX - x) > 620 * other.Scale / Settings.BasePixelSize) continue;
                other.React(x);
            }
        }

        // Two walkers about to pass each other stop to say hello: a friendly
        // one waves, a calm one nods, two friendly ones sometimes break into
        // a dance together, and two aggressive ones sometimes square up for
        // a shouting match instead of just barging past.
        void TryGreet(Walker a, Walker b, double distance, int towardsB)
        {
            if (distance >= GreetDistanceBodies * Math.Max(a.BodyWidth, b.BodyWidth)) return;
            if (!a.CanGreet || !b.CanGreet) return;
            if (a.GreetCooldown > 0 || b.GreetCooldown > 0) return;

            bool approaching = (a.IsWalking && a.Facing == towardsB) || (b.IsWalking && b.Facing == -towardsB);
            if (!approaching) return;

            a.GreetCooldown = GreetCooldownSeconds;
            b.GreetCooldown = GreetCooldownSeconds;

            bool bothAggressive = a.Temperament == Temperament.Aggressive && b.Temperament == Temperament.Aggressive;
            if (bothAggressive)
            {
                if (rng.NextDouble() < 0.5) a.Argue(b);
                return;
            }

            bool bothFriendly = a.Personality.Warm && b.Personality.Warm;
            bool dance = bothFriendly && rng.NextDouble() < 0.4;
            if (a.Temperament != Temperament.Aggressive) a.Greet(towardsB, StyleFor(a, dance));
            if (b.Temperament != Temperament.Aggressive) b.Greet(-towardsB, StyleFor(b, dance));
        }

        static GreetStyle StyleFor(Walker walker, bool dance)
        {
            if (!walker.Personality.Warm) return GreetStyle.Nod;
            return dance ? GreetStyle.Dance : GreetStyle.Wave;
        }

        // An aggressive walker with somebody in range goes for them - unless
        // it is busy with an errand of its own, or too worn out.
        void ConsiderCharging(Walker bully)
        {
            if (!bully.IsWalking || bully.HasGoal || bully.ChargeCooldown > 0 || bully.Energy < 0.35) return;

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

        // A yawn or a dance that has just begun spreads to whoever is near
        // enough to see it, each according to their nature.
        void Spread()
        {
            foreach (Walker walker in walkers)
            {
                Antic? started = walker.TakeBroadcast();
                if (started == null || !walker.IsVisible) continue;
                bool toot = started.Value == Antic.Toot;   // that one carries less far, and nobody copies it
                double reach = (toot ? 260 : 450) * walker.Scale / Settings.BasePixelSize;
                foreach (Walker other in walkers)
                {
                    if (other == walker || !other.IsVisible || other.IsLeaving) continue;
                    if (Math.Abs(other.FeetY - walker.FeetY) > 2) continue;
                    if (Math.Abs(other.CentreX - walker.CentreX) > reach) continue;
                    if (toot) other.Smell(walker.CentreX);
                    else other.Catch(started.Value);
                }
            }
        }
    }
}
