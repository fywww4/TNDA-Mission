using System;
using System.Collections.Generic;
using UnityEngine;

namespace TNDA.Prototype
{
    // Ticket 18 presentation tuning (Inspector, on DemolitionGame): how far and how long a player's chase view shakes,
    // when sparks fly, and how big a wreck's explosion is.
    [Serializable]
    public sealed class ImpactFeelTuning
    {
        [Tooltip("Rams slower than this do not shake the view. Rams at the prop-smashing speed shake hard and throw sparks.")]
        public float shakeMinImpact = 1.5f;
        [Tooltip("Shake size in art pixels (the 256x224 picture): a light ram, a smash-speed ram, a wreck.")]
        public int lightPixels = 1, smashPixels = 2, wreckPixels = 3;
        [Tooltip("Shake length in seconds for the same three.")]
        public float lightSeconds = .15f, smashSeconds = .25f, wreckSeconds = .35f;
        [Tooltip("A wreck's explosion against a rocket hit's (1).")]
        public float wreckExplosionScale = 1.4f;
        [Tooltip("How long a spark burst stays up.")]
        public float sparkSeconds = .2f;
    }

    // One player's view should shake Pixels art pixels for Seconds.
    public readonly struct ShakeKick
    {
        public readonly ArcadeVehicle Car;
        public readonly int Pixels;
        public readonly float Seconds;
        public ShakeKick(ArcadeVehicle car, int pixels, float seconds) { Car = car; Pixels = pixels; Seconds = seconds; }
    }

    // What a ram looks like on screen: only the players in it shake, harder the faster the hit and hardest when it
    // wrecks; computer cars never shake anyone's view. smashSpeed is the match's prop-smashing speed (MatchTuning).
    public static class ImpactFeel
    {
        public static IEnumerable<ShakeKick> Kicks(MatchEvent e, float smashSpeed, ImpactFeelTuning t)
        {
            // Ticket 21: a spike ball shakes the player it hits, hardest when it wrecks.
            if (e is SpikeBallHit hit && !hit.Car.computer)
                yield return hit.Wrecked ? new ShakeKick(hit.Car, t.wreckPixels, t.wreckSeconds)
                    : hit.Hurt ? new ShakeKick(hit.Car, t.smashPixels, t.smashSeconds) : new ShakeKick(hit.Car, t.lightPixels, t.lightSeconds);
            if (e is SpikeBallStruck struck && !struck.Victim.computer)
                yield return struck.Wrecked ? new ShakeKick(struck.Victim, t.wreckPixels, t.wreckSeconds) : new ShakeKick(struck.Victim, t.lightPixels, t.lightSeconds);
            if (!(e is CarRammed ram)) yield break;
            int pixels; float seconds;
            if (ram.VictimWrecked) { pixels = t.wreckPixels; seconds = t.wreckSeconds; }
            else if (ram.Impact >= smashSpeed) { pixels = t.smashPixels; seconds = t.smashSeconds; }
            else if (ram.Impact >= t.shakeMinImpact) { pixels = t.lightPixels; seconds = t.lightSeconds; }
            else yield break;
            if (!ram.Attacker.computer) yield return new ShakeKick(ram.Attacker, pixels, seconds);
            if (!ram.Victim.computer) yield return new ShakeKick(ram.Victim, pixels, seconds);
        }

        // Sparks fly, between the two cars, from rams at smashing speed and from every scrape, and off every spike-ball hit.
        public static bool TrySparks(MatchEvent e, float smashSpeed, out Vector2 point)
        {
            if (e is SpikeBallHit hit) { point = hit.Point; return true; }
            if (e is SpikeBallStruck struck) { point = struck.Point; return true; }
            bool sparks = e is CarRammed ram && (ram.Kind == ImpactKind.Scrape || ram.Impact >= smashSpeed);
            point = sparks ? ((CarRammed)e).Point : Vector2.zero;
            return sparks;
        }
    }

    // One view's shake: whole-art-pixel offsets that jitter back and forth and shrink to nothing. A weaker kick never
    // cuts a stronger shake short.
    public sealed class ScreenShake
    {
        private int peak, frame;
        private float total, left;

        // The size the shake has now, in art pixels (0 when settled).
        public int Pixels => left > 0 && total > 0 ? Mathf.CeilToInt(peak * left / total) : 0;

        public void Kick(int pixels, float seconds)
        {
            if (pixels <= 0 || seconds <= 0 || pixels < Pixels) return;
            peak = pixels; total = left = seconds;
        }

        public void Stop() => left = 0;

        public Vector2Int Advance(float dt)
        {
            left = Mathf.Max(0, left - dt);
            int size = Pixels;
            if (size == 0) return Vector2Int.zero;
            frame++;
            return new Vector2Int(frame % 2 == 0 ? size : -size, ((frame / 2) % 2 == 0 ? 1 : -1) * (size / 2));
        }
    }
}
