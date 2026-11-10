using System;
using UnityEngine;

namespace TNDA.Prototype
{
    // One "insert a coin to continue" countdown per player seat. A human knocked out by a timed elimination before the
    // final opens their seat's countdown; each seat runs, pays and expires on its own, so two players knocked out
    // together never share a clock, a coin press or an outcome. The host owns the coins and the panels.
    public sealed class ContinueCountdowns
    {
        private readonly float seconds;
        private readonly float[] left;

        public ContinueCountdowns(int seats, float seconds)
        {
            this.seconds = seconds;
            left = new float[seats];
            Clear();
        }

        public int Seats => left.Length;
        public bool Waiting(int seat) => left[seat] >= 0;
        public bool AnyWaiting { get { foreach (float l in left) if (l >= 0) return true; return false; } }
        public int SecondsShown(int seat) => Mathf.CeilToInt(left[seat]);

        public void Clear() { for (int i = 0; i < left.Length; i++) left[i] = -1; }

        // Opens the car's countdown when it is a human seat knocked out with the match still going (the final
        // elimination ends the match, so its loser never gets one). Returns whether a countdown opened.
        public bool Offer(ArcadeVehicle car, bool matchFinished)
        {
            if (matchFinished || car.computer || car.index >= left.Length) return false;
            left[car.index] = seconds;
            return true;
        }

        // Runs every waiting seat for dt. A seat whose button is pressed with a coin to spend rejoins (revive) and
        // its countdown closes; pressed without a coin it reports noCoin and keeps counting; a seat that reaches zero
        // simply closes. tickSecond fires once for each seat whose displayed second just changed.
        public void Advance(float dt, Func<int, bool> pressed, Func<bool> spendCoin, Action<int> revive, Action<int> noCoin, Action<int> tickSecond)
        {
            for (int seat = 0; seat < left.Length; seat++)
            {
                if (left[seat] < 0) continue;
                if (pressed(seat))
                {
                    if (spendCoin())
                    {
                        left[seat] = -1;
                        revive(seat);
                        continue;
                    }
                    noCoin(seat);
                }
                int shown = Mathf.CeilToInt(left[seat]);
                left[seat] -= dt;
                if (left[seat] <= 0) left[seat] = -1;
                else if (Mathf.CeilToInt(left[seat]) != shown) tickSecond(seat);
            }
        }
    }
}
