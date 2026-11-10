using System;
using UnityEngine;

namespace TNDA.Prototype
{
    // One "press START to continue" countdown per player seat. A human knocked out by a timed elimination before the
    // final opens their seat's countdown; each seat runs and expires on its own, so two players knocked out together
    // never share a clock, a press or an outcome. A continue is free but each seat gets only one per match.
    public sealed class ContinueCountdowns
    {
        private readonly float seconds;
        private readonly float[] left;
        private readonly bool[] used;

        public ContinueCountdowns(int seats, float seconds)
        {
            this.seconds = seconds;
            left = new float[seats];
            used = new bool[seats];
            NewMatch();
        }

        public int Seats => left.Length;
        public bool Waiting(int seat) => left[seat] >= 0;
        public bool AnyWaiting { get { foreach (float l in left) if (l >= 0) return true; return false; } }
        public int SecondsShown(int seat) => Mathf.CeilToInt(left[seat]);
        // Whether the seat already spent its one continue this match.
        public bool Used(int seat) => used[seat];

        // Closes every countdown; the continues already spent this match stay spent.
        public void Clear() { for (int i = 0; i < left.Length; i++) left[i] = -1; }

        // A new match: no countdowns, and every seat has its continue again.
        public void NewMatch()
        {
            Clear();
            for (int i = 0; i < used.Length; i++) used[i] = false;
        }

        // Opens the car's countdown when it is a human seat knocked out with the match still going (the final
        // elimination ends the match, so its loser never gets one) that has not used its continue yet. Returns whether
        // a countdown opened.
        public bool Offer(ArcadeVehicle car, bool matchFinished)
        {
            if (matchFinished || car.computer || car.index >= left.Length || used[car.index]) return false;
            left[car.index] = seconds;
            return true;
        }

        // Runs every waiting seat for dt. A seat whose START is pressed rejoins (revive), spends its continue and its
        // countdown closes; a seat that reaches zero simply closes. tickSecond fires once for each seat whose displayed
        // second just changed.
        public void Advance(float dt, Func<int, bool> pressed, Action<int> revive, Action<int> tickSecond)
        {
            for (int seat = 0; seat < left.Length; seat++)
            {
                if (left[seat] < 0) continue;
                if (pressed(seat))
                {
                    left[seat] = -1;
                    used[seat] = true;
                    revive(seat);
                    continue;
                }
                int shown = Mathf.CeilToInt(left[seat]);
                left[seat] -= dt;
                if (left[seat] <= 0) left[seat] = -1;
                else if (Mathf.CeilToInt(left[seat]) != shown) tickSecond(seat);
            }
        }
    }
}
