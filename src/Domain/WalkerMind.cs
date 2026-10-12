using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace IdleWalker.Domain
{
    /// <summary>
    /// The same class, continued: each walker's own little mind. No web, no
    /// libraries, no APIs - just two tiny ideas that together read as
    /// intelligence:
    ///
    /// 1. A learner. Every idle choice (each habit, or setting off to
    ///    visit, prank or inspect) has a learned value per situation, and
    ///    the walker mostly picks what has paid off before, sometimes trying
    ///    something unexpected. What counts as "paid off" is its own drives:
    ///    company kept, curiosity satisfied, energy well spent - weighted by
    ///    its personality, so the same events teach a friendly one and an
    ///    aggressive one different lessons. It starts from its personality's
    ///    habits, so a newborn behaves like its cast entry and only drifts
    ///    towards its own taste with experience.
    ///
    /// 2. A memory. It keeps a fondness per fellow walker: chats, dances,
    ///    games of tag and comfortings warm it, pranks and shoves chill it.
    ///    It then prefers visiting walkers it likes - and bullies take a
    ///    shine to pranking walkers they dislike.
    ///
    /// Both live only in this session and cost almost nothing: one small
    /// table per walker, updated when it next stops to think.
    /// </summary>
    partial class Walker
    {
        const double Gamma = 0.9;           // how much the future matters to the learner
        const double MaxReward = 1.5;       // clamp on one lesson, so a single thrill cannot rewire it

        // Situations: three levels each of energy, the wish for company and
        // curiosity, times who is near (nobody, somebody harmless, a bully):
        // 81 in all. A finer table learns each cell more slowly, so one
        // lesson is shared with the neighbouring situations (same tired /
        // lonely / curious reading, other levels) at a fraction, the
        // choice before the judged one gets a decaying share (an eligibility
        // trace), and the net replays recent lessons. Together they learn
        // faster than the old eight-way table did, not slower.
        const int StateCount = 81;
        const double SiblingShare = 0.35;   // what a lesson teaches the neighbouring situations
        const double TraceDecay = 0.5;      // what the choice before the judged one gets of the lesson
        const int ReplaySize = 24;          // recent lessons the net keeps to replay
        const int ReplayPerLesson = 3;      // how many of them it replays each time it learns

        double[,] values;                   // learned value [situation, choice], starts at zero
        int olderState = -1, olderChoice = -1;   // the choice judged last time, for the trace
        double[] olderFeat;
        double[][] replayFeat;              // the replay memory: features, choice, net target
        int[] replayChoice;
        double[] replayTarget;
        int replayCount, replayPos;
        readonly Dictionary<Walker, double> fondness = new Dictionary<Walker, double>();
        readonly double[] place = new double[8];  // how good each stretch of the desktop proved, left to right
        double steerCooldown, bondCooldown;

        bool hasPrev;                       // there is a previous choice to learn from
        int prevState, prevChoice;
        double prevSocial, prevCuriosity, prevEnergy, prevCheer, prevFear;
        double pendingBonus;                // extra lessons banked by events since the last think
        double wantAtVisit;                 // how lonely the trek over was, for hunger's sake

        // Lifelike layers: lingering cheer/dread, novelty counts, anti-routine.
        double cheer;                       // -1..1, warm after chats/tag/applause, cold after pranks/shoves
        double fear;                        // 0..1, dread near bullies; the timid feel it most
        int[] useCount;                     // how often each choice has been tried (curiosity bonus)
        int lastChoice = -1;                // for habituation: repeats of the same act go stale
        int repeatCount;

        // Episodic memory (the session's diary) and life attitude (persisted):
        // a few recent hurts/kindnesses with who and when, and a slow
        // optimism that a good or hard life builds up over days.
        const int EvHurt = 0;
        const int EvKind = 1;
        struct MemoryEvent
        {
            public int Kind;
            public Walker Who;
            public double At;
        }
        readonly MemoryEvent[] log = new MemoryEvent[6];
        int logPos;
        double optimism;                    // -1..1, the life-attitude under the moods

        // Upgrade v2: lifetime stats for annealing + dashboard. Persisted.
        int lifetimeDecisions;              // how many choices have been judged so far
        double totalReward;                 // sum of clamped rewards, for AvgReward

        // Upgrade v3: tiny MLP-Q generalizer (12 feats -> 12 tanh -> N actions).
        // Hybrid with the table: total = table + mlp. Table learns fast discrete,
        // MLP learns slow continuous so new combos still feel motivated, never random.
        const int MlpFeats = 12;
        const int MlpHidden = 12;
        double[,] mlpW1;                    // [feat, hidden], small init
        double[] mlpB1;                     // [hidden]
        double[,] mlpW2;                    // [hidden, action]
        double[] mlpB2;                     // [action]
        double[] prevFeat;                  // continuous features at previous choice

        int HabitCount { get { return personality.Habits.Length; } }
        int VisitChoice { get { return HabitCount; } }
        int PrankChoice { get { return HabitCount + 1; } }
        int InspectChoice { get { return HabitCount + 2; } }

        void EnsureTable()
        {
            if (values == null) values = new double[StateCount, HabitCount + 3];
            if (useCount == null || useCount.Length != HabitCount + 3)
            {
                int n = HabitCount + 3;
                int[] grown = new int[n];
                if (useCount != null)
                {
                    int m = useCount.Length < n ? useCount.Length : n;
                    for (int i = 0; i < m; i++) grown[i] = useCount[i];
                }
                useCount = grown;
            }
            EnsureMlp();
        }

        void EnsureMlp()
        {
            int n = HabitCount + 3;
            if (mlpW1 != null && mlpB1 != null && mlpW2 != null && mlpB2 != null)
            {
                if (mlpW1.GetLength(0) == MlpFeats && mlpW1.GetLength(1) == MlpHidden &&
                    mlpB1.Length == MlpHidden && mlpW2.GetLength(0) == MlpHidden &&
                    mlpW2.GetLength(1) == n && mlpB2.Length == n)
                    return;
            }
            mlpW1 = new double[MlpFeats, MlpHidden];
            mlpB1 = new double[MlpHidden];
            mlpW2 = new double[MlpHidden, n];
            mlpB2 = new double[n];
            for (int f = 0; f < MlpFeats; f++)
                for (int h = 0; h < MlpHidden; h++)
                    mlpW1[f, h] = (rng.NextDouble() + rng.NextDouble() - 1) * 0.05;
            for (int h = 0; h < MlpHidden; h++)
                for (int a = 0; a < n; a++)
                    mlpW2[h, a] = (rng.NextDouble() + rng.NextDouble() - 1) * 0.05;
            if (prevFeat == null || prevFeat.Length != MlpFeats)
                prevFeat = new double[MlpFeats];
        }

        void FillFeatures(double[] f)
        {
            f[0] = energy;
            f[1] = social;
            f[2] = curiosity;
            f[3] = cheer;
            f[4] = fear;
            f[5] = NightTiredness();
            f[6] = optimism;
            f[7] = Boredom();
            bool hasCompany = company != null && company.IsVisible && !company.IsLeaving;
            f[8] = hasCompany ? 1 : 0;
            bool bullyNear = hasCompany &&
                (company.Temperament == Temperament.Aggressive || company.Temperament == Temperament.Feisty) &&
                fear > 0.15;
            f[9] = bullyNear ? 1 : 0;
            bool upsetNear = hasCompany && company.IsUpset;
            f[10] = upsetNear ? 1 : 0;
            double q = place[BestPlace()];
            if (q > 1) q = 1; else if (q < -1) q = -1;
            f[11] = q;
        }

        double MlpFor(double[] feat, int choice)
        {
            EnsureMlp();
            int n = HabitCount + 3;
            if (choice < 0 || choice >= n) return 0;
            double[] hid = new double[MlpHidden];
            for (int h = 0; h < MlpHidden; h++)
            {
                double s = mlpB1[h];
                for (int f = 0; f < MlpFeats; f++) s += feat[f] * mlpW1[f, h];
                // tanh, clamped input for safety
                if (s > 8) s = 8; else if (s < -8) s = -8;
                double e2 = Math.Exp(2 * s);
                hid[h] = (e2 - 1) / (e2 + 1);
            }
            double q = mlpB2[choice];
            for (int h = 0; h < MlpHidden; h++) q += hid[h] * mlpW2[h, choice];
            if (q > MaxReward) q = MaxReward;
            else if (q < -MaxReward) q = -MaxReward;
            return q;
        }

        double MaxLearned(int state, double[] featNow)
        {
            EnsureTable();
            int n = HabitCount + 3;
            double best = double.MinValue;
            bool first = true;
            for (int a = 0; a < n; a++)
            {
                double q = values[state, a] + MlpFor(featNow, a);
                if (first || q > best) { best = q; first = false; }
            }
            return best;
        }

        void MlpLearn(double[] feat, int choice, double error, double rate)
        {
            EnsureMlp();
            int n = HabitCount + 3;
            if (choice < 0 || choice >= n) return;
            if (error > MaxReward) error = MaxReward;
            else if (error < -MaxReward) error = -MaxReward;
            double[] hid = new double[MlpHidden];
            for (int h = 0; h < MlpHidden; h++)
            {
                double s = mlpB1[h];
                for (int f = 0; f < MlpFeats; f++) s += feat[f] * mlpW1[f, h];
                if (s > 8) s = 8; else if (s < -8) s = -8;
                double e2 = Math.Exp(2 * s);
                hid[h] = (e2 - 1) / (e2 + 1);
            }
            // Output layer: only the taken action moves (Q-learning).
            for (int h = 0; h < MlpHidden; h++)
                mlpW2[h, choice] += rate * error * hid[h];
            mlpB2[choice] += rate * error;
            // Hidden layer backprop through tanh.
            for (int h = 0; h < MlpHidden; h++)
            {
                double dh = error * mlpW2[h, choice] * (1 - hid[h] * hid[h]);
                // Clamp the backprop signal so one thrill cannot rewire the net.
                if (dh > 1) dh = 1; else if (dh < -1) dh = -1;
                for (int f = 0; f < MlpFeats; f++)
                    mlpW1[f, h] += rate * dh * feat[f];
                mlpB1[h] += rate * dh * 0.5;
            }
            // Gentle decay + clamps: the net generalises, the table remembers.
            for (int f = 0; f < MlpFeats; f++)
                for (int h = 0; h < MlpHidden; h++)
                {
                    mlpW1[f, h] *= 0.9995;
                    if (mlpW1[f, h] > 2) mlpW1[f, h] = 2;
                    else if (mlpW1[f, h] < -2) mlpW1[f, h] = -2;
                }
            for (int h = 0; h < MlpHidden; h++)
            {
                if (mlpB1[h] > 2) mlpB1[h] = 2;
                else if (mlpB1[h] < -2) mlpB1[h] = -2;
                for (int a = 0; a < n; a++)
                {
                    mlpW2[h, a] *= 0.9995;
                    if (mlpW2[h, a] > 2) mlpW2[h, a] = 2;
                    else if (mlpW2[h, a] < -2) mlpW2[h, a] = -2;
                }
            }
            for (int a = 0; a < n; a++)
            {
                if (mlpB2[a] > 2) mlpB2[a] = 2;
                else if (mlpB2[a] < -2) mlpB2[a] = -2;
            }
        }

        /// <summary>
        /// Banks a lesson for the net (features, choice, what the net should
        /// have said) and replays a few earlier ones at a gentle rate.
        /// </summary>
        void Replay(double[] feat, int choice, double netTarget, double rate)
        {
            if (replayFeat == null || replayFeat.Length != ReplaySize)
            {
                replayFeat = new double[ReplaySize][];
                replayChoice = new int[ReplaySize];
                replayTarget = new double[ReplaySize];
                replayCount = 0;
                replayPos = 0;
            }
            if (netTarget > MaxReward) netTarget = MaxReward;
            else if (netTarget < -MaxReward) netTarget = -MaxReward;
            if (replayFeat[replayPos] == null) replayFeat[replayPos] = new double[MlpFeats];
            Array.Copy(feat, replayFeat[replayPos], MlpFeats);
            replayChoice[replayPos] = choice;
            replayTarget[replayPos] = netTarget;
            replayPos = (replayPos + 1) % ReplaySize;
            if (replayCount < ReplaySize) replayCount++;

            int n = HabitCount + 3;
            for (int k = 0; k < ReplayPerLesson && k < replayCount; k++)
            {
                int i = rng.Next(replayCount);
                int c = replayChoice[i];
                if (c < 0 || c >= n) continue;
                double error = replayTarget[i] - MlpFor(replayFeat[i], c);
                MlpLearn(replayFeat[i], c, error, rate);
            }
        }

        internal double MlpWeightNorm()
        {
            if (mlpW1 == null || mlpW2 == null) return 0;
            double s = 0;
            foreach (double v in mlpW1) s += v * v;
            foreach (double v in mlpW2) s += v * v;
            return Math.Sqrt(s);
        }

        /// <summary>
        /// The situation in one number (0..80): how tired, how lonely, how
        /// curious, each in three steps, and who is near - nobody, somebody
        /// harmless, or a bully within sight on the same ground.
        /// </summary>
        int MindState()
        {
            int e = energy < 0.45 ? 0 : energy < 0.75 ? 1 : 2;
            int s = social < 0.25 ? 0 : social <= 0.5 ? 1 : 2;
            int c = curiosity < 0.3 ? 0 : curiosity <= 0.55 ? 1 : 2;
            int k = 0;
            Walker other = company;
            if (other != null && other.IsVisible && !other.IsLeaving &&
                Math.Abs(other.FeetY - feetY) <= 2 && Math.Abs(other.CentreX - cx) < 500 * sizeFactor)
                k = other.Temperament == Temperament.Aggressive || other.Temperament == Temperament.Feisty ? 2 : 1;
            return e + 3 * s + 9 * c + 27 * k;
        }

        /// <summary>
        /// The old eight-way reading of a situation (tired, lonely, curious
        /// bits): the family a fine situation belongs to, for sharing
        /// lessons and for reading v3 files. The level boundaries were
        /// chosen so the old thresholds fall exactly on them.
        /// </summary>
        static int CoarseOf(int state)
        {
            int e = state % 3, s = (state / 3) % 3, c = (state / 9) % 3;
            return (e == 0 ? 1 : 0) | (s == 2 ? 2 : 0) | (c == 2 ? 4 : 0);
        }

        /// <summary>
        /// One lesson for a cell. The other situations in the same family
        /// learn a share of it too, so the finer table fills in quickly and
        /// a walker that has only ever been tired-and-alone still has a
        /// view on tired-with-a-friend-near.
        /// </summary>
        void Teach(int state, int choice, double delta)
        {
            values[state, choice] += delta;
            int family = CoarseOf(state);
            double share = delta * SiblingShare;
            for (int s = 0; s < StateCount; s++)
                if (s != state && CoarseOf(s) == family) values[s, choice] += share;
        }

        double MaxValue(int state)
        {
            EnsureTable();
            double best = values[state, 0];
            int n = HabitCount + 3;
            for (int a = 1; a < n; a++)
                if (values[state, a] > best) best = values[state, a];
            return best;
        }

        /// <summary>What the personality thinks of a choice before any experience: the inborn taste.</summary>
        double PriorFor(int choice)
        {
            // Errands must compete with the best habits from the start, or
            // the eager ones (bullies prowling, friends visiting, the
            // curious snooping) would starve before learning anything.
            if (choice < HabitCount) return personality.Habits[choice].Weight * 0.15;
            if (choice == VisitChoice) return 0.15 + 0.45 * personality.Sociable;
            if (choice == PrankChoice)
                return personality.Temperament == Temperament.Aggressive ? 0.8 : 0.55;
            return 0.15 + 0.45 * personality.Curious;
        }

        double FondOf(Walker other)
        {
            double v;
            return other != null && fondness.TryGetValue(other, out v) ? v : 0;
        }

        void Like(Walker other, double amount)
        {
            if (other == null || other == this) return;
            double v = FondOf(other) + amount;
            if (v > 1) v = 1; else if (v < -1) v = -1;
            fondness[other] = v;
            if (fondness.Count > 40) PruneMemory();
        }

        void PruneMemory()
        {
            Walker[] keys = new Walker[fondness.Count];
            fondness.Keys.CopyTo(keys, 0);
            foreach (Walker key in keys)
            {
                double v;
                if (fondness.TryGetValue(key, out v) && Math.Abs(v) < 0.05) fondness.Remove(key);
            }
        }

        void DecayMemory()
        {
            if (fondness.Count == 0) return;
            Walker[] keys = new Walker[fondness.Count];
            fondness.Keys.CopyTo(keys, 0);
            foreach (Walker key in keys)
            {
                double v;
                if (!fondness.TryGetValue(key, out v)) continue;
                v *= 0.999;
                if (Math.Abs(v) < 0.02) fondness.Remove(key);
                else fondness[key] = v;
            }
        }

        /// <summary>Coming out again: a fresh situation to learn from, but the taste and memories stay.</summary>
        void ResetMind()
        {
            hasPrev = false;
            olderState = -1;
            pendingBonus = 0;
            prevSocial = social;
            prevCuriosity = curiosity;
            prevEnergy = energy;
            prevCheer = cheer;
            prevFear = fear;
            EnsureMlp();
            FillFeatures(prevFeat);
        }

        /// <summary>
        /// Settles the previous choice before picking the next: the reward
        /// is how the drives moved since (company kept, curiosity met,
        /// energy well spent, fear relieved, cheer gained) plus whatever
        /// events banked meanwhile. Weighted per temperament so the same
        /// events teach a friendly one and a bully different lessons.
        /// </summary>
        void SettlePrevious()
        {
            if (!hasPrev)
            {
                pendingBonus = 0;
                return;
            }
            hasPrev = false;
            EnsureTable();
            double wSocial = 0.4 + 0.6 * personality.Sociable;
            double wCurious = 0.4 + 0.6 * personality.Curious;
            double wEnergy = personality.Temperament == Temperament.Aggressive ? 0.3 : 0.5;
            if (personality.Temperament == Temperament.Calm) wEnergy = 0.65;
            double reward = wSocial * (prevSocial - social) +
                            wCurious * (prevCuriosity - curiosity) +
                            wEnergy * (energy - prevEnergy) +
                            FearWeight() * (prevFear - fear) +
                            0.35 * (cheer - prevCheer) + pendingBonus;
            pendingBonus = 0;
            if (reward > MaxReward) reward = MaxReward;
            else if (reward < -MaxReward) reward = -MaxReward;
            double rate = LearnRateFor();
            // Upgrade v3: hybrid TD - total = table + MLP. Bootstrap from total,
            // update table fast + MLP slow so behaviour stays smooth while generalising.
            int nextState = MindState();
            double[] featNow = new double[MlpFeats];
            FillFeatures(featNow);
            double prevMlp = MlpFor(prevFeat, prevChoice);
            double prevTotal = values[prevState, prevChoice] + prevMlp;
            double target = reward + MindGamma() * MaxLearned(nextState, featNow);
            double error = target - prevTotal;
            if (error > MaxReward * 2) error = MaxReward * 2;
            else if (error < -MaxReward * 2) error = -MaxReward * 2;
            Teach(prevState, prevChoice, rate * error);
            MlpLearn(prevFeat, prevChoice, error, rate * 0.4);
            // The trace: the choice before this one set the scene for it, so
            // it earns a decaying share of the same lesson.
            if (olderState >= 0 && olderFeat != null)
            {
                Teach(olderState, olderChoice, rate * error * TraceDecay);
                MlpLearn(olderFeat, olderChoice, error * TraceDecay, rate * 0.4);
            }
            // Replay: the net goes over a few recent lessons again, which is
            // what lets a dozen weights settle on a trend after a handful of
            // real events instead of hundreds.
            Replay(prevFeat, prevChoice, target - values[prevState, prevChoice], rate * 0.2);
            olderState = prevState;
            olderChoice = prevChoice;
            if (olderFeat == null || olderFeat.Length != MlpFeats) olderFeat = new double[MlpFeats];
            Array.Copy(prevFeat, olderFeat, MlpFeats);
            // Upgrade v2: track lifetime stats for annealing + dashboard.
            lifetimeDecisions++;
            totalReward += reward;
            if (lifetimeDecisions < 0) lifetimeDecisions = int.MaxValue;
            if (totalReward > 1000000) totalReward = 1000000;
            else if (totalReward < -1000000) totalReward = -1000000;
            // Upgrade v2: slow forgetting so stale tastes fade and values cannot saturate.
            // Global decay is tiny per lesson; the touched cell was just reinforced above.
            for (int r = 0; r < values.GetLength(0); r++)
                for (int c = 0; c < values.GetLength(1); c++)
                    values[r, c] *= 0.9995;
            // The same lesson stains the map where it happened: hangouts form.
            int bin = PlaceIndex();
            place[bin] += rate * (reward - place[bin]);
            for (int i = 0; i < place.Length; i++) place[i] *= 0.999;
            DecayMemory();
        }

        /// <summary>Remembers a choice just made, so the next think can judge it.</summary>
        void RememberChoice(int state, int choice)
        {
            EnsureTable();
            hasPrev = true;
            prevState = state;
            prevChoice = choice;
            prevSocial = social;
            prevCuriosity = curiosity;
            prevEnergy = energy;
            prevCheer = cheer;
            prevFear = fear;
            FillFeatures(prevFeat);
            if (choice >= 0 && choice < useCount.Length) useCount[choice]++;
            if (choice == lastChoice) repeatCount++;
            else
            {
                lastChoice = choice;
                repeatCount = 0;
            }
        }

        void AddBonus(double amount)
        {
            pendingBonus += amount;
            if (pendingBonus > MaxReward) pendingBonus = MaxReward;
            else if (pendingBonus < -MaxReward) pendingBonus = -MaxReward;
            trialBonus += amount;   // the body trial judges by the same good news
            cheer += amount * 0.25;
            if (cheer > 1) cheer = 1;
            else if (cheer < -1) cheer = -1;
            if (amount < -0.2) fear += -amount * 0.2;
            if (fear > 1) fear = 1;
            optimism += amount * 0.02;   // a good or hard life shifts the attitude, very slowly
            if (optimism > 1) optimism = 1;
            else if (optimism < -1) optimism = -1;
        }

        /// <summary>How far ahead it connects acts to consequences: a child lives in the moment.</summary>
        double MindGamma()
        {
            switch (personality.Temperament)
            {
                case Temperament.Calm: return 0.92;
                case Temperament.Friendly: return 0.88;
                case Temperament.Aggressive: return 0.82;
                case Temperament.Timid: return 0.75;
                case Temperament.Goofy: return 0.85;
                case Temperament.Eccentric: return 0.7;
                case Temperament.Feisty: return 0.8;
                case Temperament.Suave: return 0.87;
                default: return Gamma;
            }
        }

        /// <summary>How fast this individual rewires after one lesson: the nervous learn fast, the calm slow.</summary>
        double LearnRateFor()
        {
            double baseRate;
            switch (personality.Temperament)
            {
                case Temperament.Calm: baseRate = 0.08; break;
                case Temperament.Friendly: baseRate = 0.12; break;
                case Temperament.Aggressive: baseRate = 0.10; break;
                case Temperament.Timid: baseRate = 0.18; break;
                case Temperament.Goofy: baseRate = 0.14; break;
                case Temperament.Eccentric: baseRate = 0.16; break;
                case Temperament.Feisty: baseRate = 0.11; break;
                case Temperament.Suave: baseRate = 0.13; break;
                default: baseRate = Settings.LearnRate; break;
            }
            if (personality.Name == "Phil") baseRate += 0.02;
            else if (personality.Name == "Carl") baseRate += 0.03;
            else if (personality.Name == "Kevin") baseRate -= 0.02;
            else if (personality.Name == "Blues") baseRate += 0.02;
            else if (personality.Name == "Terence") baseRate -= 0.03;
            // Quicker on the uptake since the finer table (v4): a quarter
            // more per lesson, and the ageing is slower, so an elder still
            // moves at a third of a newborn's pace after a thousand choices.
            baseRate *= 1.25;
            if (lifetimeDecisions > 0)
                baseRate *= 1.0 / (1.0 + lifetimeDecisions / 1500.0);
            if (baseRate < 0.04) baseRate = 0.04;
            else if (baseRate > 0.3) baseRate = 0.3;
            return baseRate;
        }

        /// <summary>How often it tries something unexpected: clowns and children experiment, the timid do not.</summary>
        double Explore()
        {
            double baseRate;
            switch (personality.Temperament)
            {
                case Temperament.Calm: baseRate = 0.08; break;
                case Temperament.Friendly: baseRate = 0.14; break;
                case Temperament.Aggressive: baseRate = 0.10; break;
                case Temperament.Timid: baseRate = 0.06; break;
                case Temperament.Goofy: baseRate = 0.24; break;
                case Temperament.Eccentric: baseRate = 0.22; break;
                case Temperament.Feisty: baseRate = 0.16; break;
                case Temperament.Suave: baseRate = 0.18; break;
                default: baseRate = Settings.ExploreRate; break;
            }
            if (personality.Name == "Blues") baseRate += 0.04;
            else if (personality.Name == "Chuck") baseRate += 0.02;
            else if (personality.Name == "Terence") baseRate -= 0.05;
            else if (personality.Name == "Tim") baseRate -= 0.03;
            // Boredom and curiosity drive novelty-seeking (curiosity-driven exploration).
            baseRate += 0.10 * curiosity + 0.05 * Boredom() + 0.03 * optimism;
            // Upgrade v2: anneal exploration with age so elders mostly exploit.
            if (lifetimeDecisions > 0)
                baseRate *= 1.0 / (1.0 + lifetimeDecisions / 400.0);
            if (fear > 0.5) baseRate *= 1 - 0.6 * fear;   // scared: no experiments
            if (baseRate < 0.02) baseRate = 0.02;
            else if (baseRate > 0.35) baseRate = 0.35;
            return baseRate;
        }

        double FearWeight()
        {
            switch (personality.Temperament)
            {
                case Temperament.Timid: return 1.2;
                case Temperament.Friendly: return 0.5;
                case Temperament.Calm: return 0.4;
                case Temperament.Goofy: return 0.3;
                case Temperament.Eccentric: return 0.4;
                case Temperament.Feisty: return 0.2;
                case Temperament.Suave: return 0.3;
                default: return 0.1;
            }
        }

        /// <summary>0..1: how stale life feels - repeats of the same act in a row (hedonic treadmill).</summary>
        double Boredom()
        {
            if (repeatCount <= 0) return 0;
            double b = repeatCount * 0.25;
            return b > 1 ? 1 : b;
        }

        /// <summary>Small intrinsic bonus for rarely-tried acts: curiosity for the unknown.</summary>
        double NoveltyBonus(int choice)
        {
            if (useCount == null || choice < 0 || choice >= useCount.Length) return 0;
            int n = useCount[choice];
            return 0.20 * personality.Curious / (1 + n * 0.3);
        }

        /// <summary>Penalty for doing the same thing over and over: habituation.</summary>
        double HabituationPenalty(int choice)
        {
            if (choice != lastChoice || repeatCount <= 0) return 0;
            double p = repeatCount * 0.18;
            return p > 0.6 ? 0.6 : p;
        }

        /// <summary>One frame of feeling: cheer settles towards the life-attitude, fear tracks nearby threats.</summary>
        void StepEmotion(double dt)
        {
            double baseCheer = optimism * 0.3;
            if (baseCheer < -0.3) baseCheer = -0.3;
            else if (baseCheer > 0.3) baseCheer = 0.3;
            if (cheer > baseCheer) cheer = Math.Max(baseCheer, cheer - dt * 0.05);
            else if (cheer < baseCheer) cheer = Math.Min(baseCheer, cheer + dt * 0.05);
            double threat = 0;
            Walker other = company;
            if (other != null && other.IsVisible && !other.IsLeaving &&
                (other.Temperament == Temperament.Aggressive || other.Temperament == Temperament.Feisty))
            {
                double d = Math.Abs(other.CentreX - cx);
                double reach = 380 * sizeFactor;
                if (d < reach && Math.Abs(other.FeetY - feetY) <= 2)
                    threat = 1 - d / reach;
            }
            double feel = 1.0;
            if (personality.Temperament == Temperament.Timid) feel = 2.2;
            else if (personality.Temperament == Temperament.Friendly) feel = 1.0;
            else if (personality.Temperament == Temperament.Calm) feel = 0.7;
            else if (personality.Temperament == Temperament.Aggressive) feel = 0.25;
            else if (personality.Temperament == Temperament.Goofy) feel = 0.6;
            else if (personality.Temperament == Temperament.Feisty) feel = 0.4;
            else if (personality.Temperament == Temperament.Suave) feel = 0.5;
            else if (personality.Temperament == Temperament.Eccentric) feel = 0.8;
            double rate = threat > fear ? 2.5 * feel : 0.8;
            fear += (threat - fear) * Math.Min(1, dt * rate);
            if (fear < 0) fear = 0;
            else if (fear > 1) fear = 1;
        }

        // ---- Episodic memory, reflection, rhythm ----------------------------------
        //
        // The diary (who hurt / was kind to me lately) fades within a minute
        // or two; the reflections distilled from it - best friend, rival,
        // optimism - live on. Same idea as the memory-stream architectures,
        // but a diary of six entries instead of a language model.

        /// <summary>Writes one line in the diary.</summary>
        void RememberEvent(int kind, Walker who)
        {
            if (who == null || who == this) return;
            MemoryEvent e = new MemoryEvent();
            e.Kind = kind;
            e.Who = who;
            e.At = time;
            log[logPos % log.Length] = e;
            logPos++;
        }

        /// <summary>How strongly kind/hurt events with someone still sting or warm (0..1).</summary>
        double RecentFeeling(Walker who, int kind, double window)
        {
            if (who == null) return 0;
            double sum = 0;
            for (int i = 0; i < log.Length; i++)
            {
                if (log[i].Who != who || log[i].Kind != kind) continue;
                double age = time - log[i].At;
                if (age < 0) age = 0;
                if (age < window) sum += 1 - age / window;
            }
            if (sum > 1) sum = 1;
            return sum;
        }

        /// <summary>The fondest remembered walker past affection, if any: the best friend.</summary>
        Walker BestFond()
        {
            Walker best = null;
            double bv = 0.35;
            foreach (KeyValuePair<Walker, double> kv in fondness)
            {
                if (kv.Key == null || kv.Key == this) continue;
                if (kv.Value > bv)
                {
                    bv = kv.Value;
                    best = kv.Key;
                }
            }
            return best;
        }

        /// <summary>The most resented remembered walker past dislike, if any: the rival.</summary>
        Walker Rival()
        {
            Walker worst = null;
            double wv = -0.35;
            foreach (KeyValuePair<Walker, double> kv in fondness)
            {
                if (kv.Key == null || kv.Key == this) continue;
                if (kv.Value < wv)
                {
                    wv = kv.Value;
                    worst = kv.Key;
                }
            }
            return worst;
        }

        internal string BestFriendName()
        {
            Walker best = BestFond();
            return best == null ? "-" : best.personality.Name;
        }

        internal string RivalName()
        {
            Walker worst = Rival();
            return worst == null ? "-" : worst.personality.Name;
        }

        /// <summary>0 by day, up to 1 in the deep night: the world has a rhythm too.</summary>
        static double NightTiredness()
        {
            int hour;
            try
            {
                hour = DateTime.Now.Hour;
            }
            catch (Exception)
            {
                return 0;
            }
            if (hour >= 7 && hour < 21) return 0;
            if (hour == 21) return 0.25;
            if (hour == 22 || hour == 6) return 0.5;
            return hour == 23 ? 0.75 : 1.0;
        }

        // ---- What the world teaches it ------------------------------------------

        /// <summary>A chat began with partner (as the one who came over).</summary>
        void NoteChatBegun(Walker partner, ChatStyle style)
        {
            if (style == ChatStyle.Argue)
            {
                Like(partner, -0.1);
                if (personality.Temperament == Temperament.Aggressive) AddBonus(0.15);
                return;
            }
            // Hunger: contact after a long lonely trek teaches more.
            double bonus = style == ChatStyle.Pat ? 0.6 : 0.5;
            if (style == ChatStyle.Pat && personality.Temperament == Temperament.Eccentric) bonus += 0.2;
            Like(partner, style == ChatStyle.Pat ? 0.2 : 0.15);
            RememberEvent(EvKind, partner);
            AddBonus(bonus * (0.5 + wantAtVisit));
        }

        /// <summary>Someone came over and a chat with them began (called before the relief).</summary>
        void NoteChatJoined(Walker partner, ChatStyle style)
        {
            if (style == ChatStyle.Argue)
            {
                Like(partner, -0.1);
                if (personality.Temperament == Temperament.Aggressive) AddBonus(0.15);
                return;
            }
            double bonus = style == ChatStyle.Pat ? 0.6 : 0.5;
            if (style == ChatStyle.Pat && personality.Temperament == Temperament.Eccentric) bonus += 0.2;
            Like(partner, style == ChatStyle.Pat ? 0.2 : 0.15);
            RememberEvent(EvKind, partner);
            AddBonus(bonus * (0.5 + social));
        }

        /// <summary>Stopped to greet: a dance together delights, a nod pleases.</summary>
        void NoteGreet(GreetStyle style)
        {
            AddBonus(style == GreetStyle.Dance ? 0.3 : 0.15);
        }

        /// <summary>The prank landed: the victim jumped. Bean revels in his schemes.</summary>
        void NotePrankLanded(Walker target)
        {
            Like(target, -0.1);
            AddBonus(personality.Temperament == Temperament.Eccentric ? 0.65 : 0.5);
        }

        /// <summary>Pranked by someone: remembers who.</summary>
        public void NotePranked(Walker by)
        {
            Like(by, -0.25);
            RememberEvent(EvHurt, by);
            AddBonus(-0.25);
        }

        /// <summary>Shoved by someone: remembers who, and it stings.</summary>
        public void NoteShove(Walker by)
        {
            Like(by, -0.35);
            RememberEvent(EvHurt, by);
            AddBonus(-0.4);
        }

        /// <summary>Shoved someone: bullies relish it.</summary>
        public void NoteShoveLanded(Walker victim)
        {
            Like(victim, -0.1);
            AddBonus(0.3);
        }

        /// <summary>Tagged someone in play: good fun, and friends grow fonder.</summary>
        public void NoteTagFun(Walker other)
        {
            Like(other, 0.1);
            RememberEvent(EvKind, other);
            AddBonus(0.25);
        }

        /// <summary>Tagged in play: no hard feelings, rather the opposite.</summary>
        public void NoteTaggedFun(Walker other)
        {
            Like(other, 0.1);
            RememberEvent(EvKind, other);
            AddBonus(0.2);
        }

        /// <summary>Danced, showed off Teddy, laid an egg before a crowd: applause.</summary>
        public void NoteAudience(double amount)
        {
            AddBonus(amount);
        }

        /// <summary>Covering ground side by side with someone: walking together bonds.</summary>
        public void NoteWalkTogether(Walker other)
        {
            Like(other, 0.03);
            AddBonus(0.03);
            bondCooldown = 2.0;
        }

        /// <summary>True when it is ready to bond over another shared stretch.</summary>
        public bool BondReady { get { return bondCooldown <= 0; } }

        /// <summary>Which stretch of the desktop has treated it best.</summary>
        int BestPlace()
        {
            int best = 0;
            for (int i = 1; i < place.Length; i++)
                if (place[i] > place[best]) best = i;
            return best;
        }

        int PlaceIndex()
        {
            int span = worldRight - worldLeft;
            if (span <= 0) return 0;
            int bin = (int)((cx - worldLeft) / span * place.Length);
            if (bin < 0) bin = 0; else if (bin >= place.Length) bin = place.Length - 1;
            return bin;
        }

        double BinCenter(int bin)
        {
            int span = worldRight - worldLeft;
            if (span <= 0) return cx;
            return worldLeft + (bin + 0.5) / place.Length * span;
        }

        // ---- Remembering between runs -------------------------------------------
        //
        // Everything worth keeping (learned taste, friendships, hangouts, the
        // body's walk, novelty counts) packs into plain key=value lines so the
        // program can save them to a file and reload them next launch.
        // Moods (cheer, fear, energy) are the moment, not the character, and
        // start fresh. Corrupt or mismatched data is ignored, never fatal.

        static string Dbl(double v)
        {
            return v.ToString("R", CultureInfo.InvariantCulture);
        }

        static bool TryDbl(string s, out double v)
        {
            try
            {
                v = double.Parse(s, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception)
            {
                v = 0;
                return false;
            }
        }

        static double Clamp(double v, double lo, double hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }

        /// <summary>Packs this walker's lasting memory into key=value lines (no header).</summary>
        internal string PackMind()
        {
            StringBuilder sb = new StringBuilder();
            if (values != null)
            {
                // Sparse since v4: the fine table is mostly zero for a long
                // time, so only the cells that have learned something are
                // written, as index:value pairs.
                sb.Append("vrows=").Append(values.GetLength(0)).Append("\n");
                sb.Append("vcols=").Append(values.GetLength(1)).Append("\n");
                StringBuilder v = new StringBuilder();
                int cols = values.GetLength(1);
                for (int r = 0; r < values.GetLength(0); r++)
                    for (int c = 0; c < cols; c++)
                    {
                        double x = values[r, c];
                        if (x == 0) continue;
                        if (v.Length > 0) v.Append(",");
                        v.Append((r * cols + c).ToString(CultureInfo.InvariantCulture)).Append(":").Append(Dbl(x));
                    }
                sb.Append("vs=").Append(v.ToString()).Append("\n");
            }
            if (fondness.Count > 0)
            {
                StringBuilder f = new StringBuilder();
                bool first = true;
                foreach (KeyValuePair<Walker, double> kv in fondness)
                {
                    if (kv.Key == null || kv.Key == this) continue;
                    if (!first) f.Append(";");
                    first = false;
                    f.Append(kv.Key.personality.Name).Append(",").Append(Dbl(kv.Value));
                }
                if (f.Length > 0) sb.Append("fond=").Append(f.ToString()).Append("\n");
            }
            StringBuilder p = new StringBuilder();
            for (int i = 0; i < place.Length; i++)
            {
                if (i != 0) p.Append(",");
                p.Append(Dbl(place[i]));
            }
            sb.Append("place=").Append(p.ToString()).Append("\n");
            if (styleReady)
            {
                StringBuilder s = new StringBuilder();
                for (int i = 0; i < StyleDims; i++)
                {
                    if (i != 0) s.Append(",");
                    s.Append(Dbl(style[i]));
                }
                sb.Append("style=").Append(s.ToString()).Append("\n");
                sb.Append("seeds=").Append(Dbl(swaggerSeed)).Append(",").Append(Dbl(fidgetSeed))
                    .Append(",").Append(Dbl(fidgetFreq)).Append(",").Append(Dbl(trialBest))
                    .Append(",").Append(styleTrials.ToString(CultureInfo.InvariantCulture)).Append("\n");
            }
            if (useCount != null)
            {
                StringBuilder u = new StringBuilder();
                for (int i = 0; i < useCount.Length; i++)
                {
                    if (i != 0) u.Append(",");
                    u.Append(useCount[i].ToString(CultureInfo.InvariantCulture));
                }
                sb.Append("use=").Append(u.ToString()).Append("\n");
            }
            sb.Append("last=").Append(lastChoice.ToString(CultureInfo.InvariantCulture))
                .Append(",").Append(repeatCount.ToString(CultureInfo.InvariantCulture)).Append("\n");
            sb.Append("opt=").Append(Dbl(optimism)).Append("\n");
            sb.Append("exp=").Append(lifetimeDecisions.ToString(CultureInfo.InvariantCulture)).Append("\n");
            sb.Append("rew=").Append(Dbl(totalReward)).Append("\n");
            if (mlpW1 != null && mlpB1 != null && mlpW2 != null && mlpB2 != null)
            {
                int mn = mlpB2.Length;
                sb.Append("mshape=").Append(MlpFeats.ToString(CultureInfo.InvariantCulture))
                    .Append(",").Append(MlpHidden.ToString(CultureInfo.InvariantCulture))
                    .Append(",").Append(mn.ToString(CultureInfo.InvariantCulture)).Append("\n");
                StringBuilder w1 = new StringBuilder();
                for (int f = 0; f < MlpFeats; f++)
                    for (int h = 0; h < MlpHidden; h++)
                    {
                        if (f != 0 || h != 0) w1.Append(",");
                        w1.Append(Dbl(mlpW1[f, h]));
                    }
                sb.Append("mw1=").Append(w1.ToString()).Append("\n");
                StringBuilder b1 = new StringBuilder();
                for (int h = 0; h < MlpHidden; h++)
                {
                    if (h != 0) b1.Append(",");
                    b1.Append(Dbl(mlpB1[h]));
                }
                sb.Append("mb1=").Append(b1.ToString()).Append("\n");
                StringBuilder w2 = new StringBuilder();
                for (int h = 0; h < MlpHidden; h++)
                    for (int a = 0; a < mn; a++)
                    {
                        if (h != 0 || a != 0) w2.Append(",");
                        w2.Append(Dbl(mlpW2[h, a]));
                    }
                sb.Append("mw2=").Append(w2.ToString()).Append("\n");
                StringBuilder b2 = new StringBuilder();
                for (int a = 0; a < mn; a++)
                {
                    if (a != 0) b2.Append(",");
                    b2.Append(Dbl(mlpB2[a]));
                }
                sb.Append("mb2=").Append(b2.ToString()).Append("\n");
            }
            return sb.ToString();
        }

        /// <summary>How many learned tastes have moved from zero: a rough "how much it knows".</summary>
        internal int LearnedEntries()
        {
            if (values == null) return 0;
            int n = 0;
            foreach (double v in values)
                if (v > 0.000000001 || v < -0.000000001) n++;
            return n;
        }

        /// <summary>How many friendships and grudges it holds.</summary>
        internal int Friendships()
        {
            return fondness.Count;
        }

        /// <summary>Whether its walk has been personalised beyond the cast default.</summary>
        internal bool HasOwnWalk()
        {
            return styleReady;
        }

        /// <summary>How many judged choices this walker has lived through.</summary>
        internal int Experience()
        {
            return lifetimeDecisions;
        }

        /// <summary>Mean clamped reward per judged choice: is life going well?</summary>
        internal double AvgReward()
        {
            return lifetimeDecisions > 0 ? totalReward / lifetimeDecisions : 0;
        }

        /// <summary>Forgets everything learned: taste, friendships, hangouts, walk, novelty.</summary>
        internal void ForgetMind()
        {
            values = null;
            fondness.Clear();
            for (int i = 0; i < place.Length; i++) place[i] = 0;
            styleReady = false;
            for (int i = 0; i < StyleDims; i++) style[i] = 0;
            styleTrials = 0;
            trialBest = -10;
            useCount = null;
            lastChoice = -1;
            repeatCount = 0;
            cheer = 0;
            fear = 0;
            optimism = 0;
            lifetimeDecisions = 0;
            totalReward = 0;
            mlpW1 = null;
            mlpB1 = null;
            mlpW2 = null;
            mlpB2 = null;
            if (prevFeat != null)
                for (int i = 0; i < prevFeat.Length; i++) prevFeat[i] = 0;
            logPos = 0;
            for (int i = 0; i < log.Length; i++) log[i].Who = null;
            pendingBonus = 0;
            hasPrev = false;
        }

        /// <summary>Restores lasting memory from key=value fields; bad entries are skipped.</summary>
        internal void UnpackMind(Dictionary<string, string> fields, Dictionary<string, Walker> byName)
        {
            EnsureTable();
            string rows, cols, flat;
            int r = 0, c = 0;
            bool sparse = fields.TryGetValue("vs", out flat);
            if (fields.TryGetValue("vrows", out rows) && fields.TryGetValue("vcols", out cols) &&
                (sparse || fields.TryGetValue("v", out flat)))
            {
                try
                {
                    r = int.Parse(rows, CultureInfo.InvariantCulture);
                    c = int.Parse(cols, CultureInfo.InvariantCulture);
                }
                catch (Exception)
                {
                    r = 0;
                    c = 0;
                }
                // The file's table: v4's 81 situations, or v3's 8 (dense or
                // sparse either way). A v3 table is spread over the fine
                // situations in its family, so nothing learned is lost.
                if ((r == StateCount || r == 8) && c == HabitCount + 3)
                {
                    bool ok = true;
                    double[,] loaded = new double[r, c];
                    string[] nums = flat.Split(',');
                    if (sparse)
                    {
                        for (int i = 0; i < nums.Length && ok; i++)
                        {
                            string item = nums[i].Trim();
                            if (item.Length == 0) continue;
                            int colon = item.IndexOf(':');
                            int index;
                            double v;
                            if (colon <= 0 || !int.TryParse(item.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out index) ||
                                index < 0 || index >= r * c ||
                                !TryDbl(item.Substring(colon + 1), out v) || double.IsNaN(v) || double.IsInfinity(v))
                            {
                                ok = false;
                                break;
                            }
                            loaded[index / c, index % c] = Clamp(v, -MaxReward * 4, MaxReward * 4);
                        }
                    }
                    else if (nums.Length == r * c)
                    {
                        for (int i = 0; i < nums.Length; i++)
                        {
                            double v;
                            if (!TryDbl(nums[i], out v) || double.IsNaN(v) || double.IsInfinity(v))
                            {
                                ok = false;
                                break;
                            }
                            loaded[i / c, i % c] = Clamp(v, -MaxReward * 4, MaxReward * 4);
                        }
                    }
                    else
                    {
                        ok = false;
                    }
                    if (ok && r == 8)
                    {
                        double[,] spread = new double[StateCount, c];
                        for (int s = 0; s < StateCount; s++)
                        {
                            int family = CoarseOf(s);
                            for (int a = 0; a < c; a++) spread[s, a] = loaded[family, a];
                        }
                        loaded = spread;
                    }
                    if (ok) values = loaded;
                }
            }
            string fond;
            if (fields.TryGetValue("fond", out fond))
            {
                string[] pairs = fond.Split(';');
                for (int i = 0; i < pairs.Length; i++)
                {
                    string[] kv = pairs[i].Split(',');
                    if (kv.Length != 2) continue;
                    Walker other;
                    if (!byName.TryGetValue(kv[0], out other) || other == this) continue;
                    double v;
                    if (!TryDbl(kv[1], out v)) continue;
                    fondness[other] = Clamp(v, -1, 1);
                }
            }
            string pl;
            if (fields.TryGetValue("place", out pl))
            {
                string[] nums = pl.Split(',');
                if (nums.Length == place.Length)
                {
                    bool ok = true;
                    double[] loaded = new double[place.Length];
                    for (int i = 0; i < nums.Length; i++)
                    {
                        if (!TryDbl(nums[i], out loaded[i]) || double.IsNaN(loaded[i]) || double.IsInfinity(loaded[i]))
                        {
                            ok = false;
                            break;
                        }
                        loaded[i] = Clamp(loaded[i], -MaxReward * 4, MaxReward * 4);
                    }
                    if (ok) for (int i = 0; i < place.Length; i++) place[i] = loaded[i];
                }
            }
            string st;
            if (fields.TryGetValue("style", out st))
            {
                string[] nums = st.Split(',');
                if (nums.Length == StyleDims)
                {
                    bool ok = true;
                    double[] loaded = new double[StyleDims];
                    for (int i = 0; i < nums.Length; i++)
                    {
                        if (!TryDbl(nums[i], out loaded[i]) || double.IsNaN(loaded[i]) || double.IsInfinity(loaded[i]))
                        {
                            ok = false;
                            break;
                        }
                    }
                    if (ok)
                    {
                        for (int i = 0; i < StyleDims; i++) style[i] = ClampDim(i, loaded[i]);
                        styleReady = true;
                        trialTimer = TrialSeconds;
                        trialBonus = 0;
                        trialDist = 0;
                        trialEnergy0 = energy;
                    }
                }
            }
            string sd;
            if (styleReady && fields.TryGetValue("seeds", out sd))
            {
                string[] nums = sd.Split(',');
                double a, b, fr, tb;
                if ((nums.Length == 4 || nums.Length == 5) && TryDbl(nums[0], out a) && TryDbl(nums[1], out b) &&
                    TryDbl(nums[2], out fr) && TryDbl(nums[3], out tb))
                {
                    swaggerSeed = a;
                    fidgetSeed = b;
                    if (fr >= 1.0 && fr <= 5.0) fidgetFreq = fr;
                    if (tb >= -2 && tb <= 2) trialBest = tb;
                    if (nums.Length == 5)
                    {
                        try
                        {
                            int tr = int.Parse(nums[4], CultureInfo.InvariantCulture);
                            if (tr >= 0 && tr <= 1000000) styleTrials = tr;
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }
            string us;
            if (fields.TryGetValue("use", out us))
            {
                string[] nums = us.Split(',');
                if (nums.Length == HabitCount + 3)
                {
                    bool ok = true;
                    int[] loaded = new int[nums.Length];
                    for (int i = 0; i < nums.Length; i++)
                    {
                        try
                        {
                            loaded[i] = int.Parse(nums[i], CultureInfo.InvariantCulture);
                            if (loaded[i] < 0 || loaded[i] > 1000000)
                            {
                                ok = false;
                                break;
                            }
                        }
                        catch (Exception)
                        {
                            ok = false;
                            break;
                        }
                    }
                    if (ok) useCount = loaded;
                }
            }
            string last;
            if (fields.TryGetValue("last", out last))
            {
                string[] nums = last.Split(',');
                if (nums.Length == 2)
                {
                    try
                    {
                        int lc = int.Parse(nums[0], CultureInfo.InvariantCulture);
                        int rp = int.Parse(nums[1], CultureInfo.InvariantCulture);
                        if (lc >= -1 && lc < HabitCount + 3 && rp >= 0 && rp <= 40)
                        {
                            lastChoice = lc;
                            repeatCount = rp;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            string opt;
            if (fields.TryGetValue("opt", out opt))
            {
                double o;
                if (TryDbl(opt, out o) && !double.IsNaN(o) && !double.IsInfinity(o))
                    optimism = Clamp(o, -1, 1);
            }
            string exp;
            if (fields.TryGetValue("exp", out exp))
            {
                try
                {
                    int e = int.Parse(exp, CultureInfo.InvariantCulture);
                    if (e >= 0 && e <= 1000000) lifetimeDecisions = e;
                }
                catch (Exception)
                {
                }
            }
            string rw;
            if (fields.TryGetValue("rew", out rw))
            {
                double t;
                if (TryDbl(rw, out t) && !double.IsNaN(t) && !double.IsInfinity(t))
                    totalReward = Clamp(t, -1000000, 1000000);
            }
            // Upgrade v3: MLP weights. Missing (v1/v2 files) means fresh small net -
            // the table still carries the old taste, so migration stays smooth.
            string mshape;
            string mw1, mb1, mw2, mb2;
            if (fields.TryGetValue("mshape", out mshape) &&
                fields.TryGetValue("mw1", out mw1) && fields.TryGetValue("mb1", out mb1) &&
                fields.TryGetValue("mw2", out mw2) && fields.TryGetValue("mb2", out mb2))
            {
                try
                {
                    string[] dims = mshape.Split(',');
                    int mf = int.Parse(dims[0], CultureInfo.InvariantCulture);
                    int mh = int.Parse(dims[1], CultureInfo.InvariantCulture);
                    int mn = int.Parse(dims[2], CultureInfo.InvariantCulture);
                    if (mf == MlpFeats && mh == MlpHidden && mn == HabitCount + 3)
                    {
                        string[] sW1 = mw1.Split(',');
                        string[] sB1 = mb1.Split(',');
                        string[] sW2 = mw2.Split(',');
                        string[] sB2 = mb2.Split(',');
                        if (sW1.Length == mf * mh && sB1.Length == mh &&
                            sW2.Length == mh * mn && sB2.Length == mn)
                        {
                            double[,] nw1 = new double[mf, mh];
                            double[] nb1 = new double[mh];
                            double[,] nw2 = new double[mh, mn];
                            double[] nb2 = new double[mn];
                            bool ok = true;
                            for (int i = 0; i < sW1.Length && ok; i++)
                            {
                                double v;
                                if (!TryDbl(sW1[i], out v) || double.IsNaN(v) || double.IsInfinity(v)) ok = false;
                                else nw1[i / mh, i % mh] = Clamp(v, -2, 2);
                            }
                            for (int i = 0; i < sB1.Length && ok; i++)
                            {
                                double v;
                                if (!TryDbl(sB1[i], out v) || double.IsNaN(v) || double.IsInfinity(v)) ok = false;
                                else nb1[i] = Clamp(v, -2, 2);
                            }
                            for (int i = 0; i < sW2.Length && ok; i++)
                            {
                                double v;
                                if (!TryDbl(sW2[i], out v) || double.IsNaN(v) || double.IsInfinity(v)) ok = false;
                                else nw2[i / mn, i % mn] = Clamp(v, -2, 2);
                            }
                            for (int i = 0; i < sB2.Length && ok; i++)
                            {
                                double v;
                                if (!TryDbl(sB2[i], out v) || double.IsNaN(v) || double.IsInfinity(v)) ok = false;
                                else nb2[i] = Clamp(v, -2, 2);
                            }
                            if (ok)
                            {
                                mlpW1 = nw1;
                                mlpB1 = nb1;
                                mlpW2 = nw2;
                                mlpB2 = nb2;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
