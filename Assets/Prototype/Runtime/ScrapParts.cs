using System.Linq;
using UnityEngine;

namespace TNDA.Prototype
{
    // 零件 (2026-11-21, replaces the on-screen debris splash): one piece of a rammed car lying on the arena floor.
    // Kind is the Debris.png cell it shows (0 body panel, 1 glass, 3 parts); Source is the car it came off.
    public sealed class ScrapPart
    {
        public Vector2 position, velocity;
        public float age;
        public int kind;
        public bool active;
        public ArcadeVehicle Source { get; internal set; }
        // Being pulled in by this player's car; null while it lies free.
        public ArcadeVehicle Magnet { get; internal set; }
    }

    // The scrap rules: a player's hard ram (DebrisSplashed) scatters pieces of the victim across the floor; once they
    // land, any player's car close enough pulls them in and scores partPoints for each. Computer cars never take any.
    public sealed partial class DemolitionMatch
    {
        public const int MaxParts = 24;
        public readonly ScrapPart[] Parts = Enumerable.Range(0, MaxParts).Select(_ => new ScrapPart()).ToArray();
        private static readonly int[] PartKinds = { 0, 1, 3 };

        private void ResetParts()
        {
            foreach (var part in Parts) { part.active = false; part.Source = null; part.Magnet = null; }
        }

        // The pieces burst out of the victim in every direction; the first is always a body panel, so it reads as that car.
        private void ScatterParts(ArcadeVehicle victim)
        {
            int count = Mathf.Clamp(Tuning.partsPerRam, 0, MaxParts);
            float turn = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                var part = Parts.FirstOrDefault(p => !p.active) ?? Parts.OrderByDescending(p => p.age).First();
                float degrees = turn + 360f * i / count + Random.Range(-20f, 20f);
                part.position = victim.position;
                part.velocity = Direction(degrees) * Tuning.partScatterSpeed * Random.Range(.6f, 1f);
                part.age = 0;
                part.kind = i == 0 ? 0 : PartKinds[Random.Range(0, PartKinds.Length)];
                part.active = true;
                part.Source = victim;
                part.Magnet = null;
            }
        }

        private void MoveParts(float dt)
        {
            foreach (var part in Parts)
            {
                if (!part.active) continue;
                part.age += dt;
                if (part.Magnet != null && (!part.Magnet.alive || part.Magnet.eliminated)) part.Magnet = null;
                if (part.Magnet == null && part.age >= Tuning.partLandSeconds) part.Magnet = NearestCollector(part.position);
                if (part.Magnet != null)
                {
                    // Pulled ever faster straight at the car.
                    Vector2 to = part.Magnet.position - part.position;
                    float speed = Tuning.partMagnetSpeed * Mathf.Min(1, (part.age - Tuning.partLandSeconds + .2f) * 2);
                    if (to.magnitude <= Tuning.partCollectRadius + speed * dt) { Collect(part); continue; }
                    part.position += to.normalized * speed * dt;
                    continue;
                }
                if (part.age > Tuning.partLifeSeconds) { part.active = false; continue; }
                // Thrown out, sliding to a stop inside the arena.
                part.position += part.velocity * dt;
                part.velocity *= Mathf.Max(0, 1 - Tuning.partFriction * dt);
                part.position = new Vector2(Mathf.Clamp(part.position.x, Tuning.arenaMin.x, Tuning.arenaMax.x),
                    Mathf.Clamp(part.position.y, Tuning.arenaMin.y, Tuning.arenaMax.y));
            }
        }

        private ArcadeVehicle NearestCollector(Vector2 at)
        {
            ArcadeVehicle best = null;
            float bestDistance = Tuning.partMagnetRadius;
            foreach (var car in Cars)
            {
                if (car.computer || !car.alive || car.eliminated) continue;
                float distance = Vector2.Distance(car.position, at);
                if (distance <= bestDistance) { best = car; bestDistance = distance; }
            }
            return best;
        }

        private void Collect(ScrapPart part)
        {
            var car = part.Magnet;
            part.active = false;
            part.Magnet = null;
            events.Add(new PartCollected(car, part.kind));
            if (Tuning.ramsScore) AddScore(car, Tuning.partPoints);
        }
    }
}
