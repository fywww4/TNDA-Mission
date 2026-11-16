using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TNDA.Prototype
{
    // Ticket 21: a spike ball rolling slowly across the arena floor, bouncing off the arena edge and solid terrain.
    // point and heading are where and which way it starts when spots are not random.
    [System.Serializable]
    public sealed class SpikeBall
    {
        public Vector2 point;
        public float heading;
        [System.NonSerialized] public Vector2 position, velocity;
        // Distance rolled so far; the view turns it into the rolling animation.
        [System.NonSerialized] public float rolled;
        // The car that knocked it flying; null once it has slowed back to a roll.
        public ArcadeVehicle Launcher { get; set; }
        public bool Flying => Launcher != null;
        // Cars touching it last tick: a car is hurt once per touch, not every frame it stays against the ball.
        [System.NonSerialized] internal readonly HashSet<ArcadeVehicle> touching = new HashSet<ArcadeVehicle>();
    }

    // The spike-ball rules: rolling, bouncing, the car that hits a ball and the car a flying ball hits.
    public sealed partial class DemolitionMatch
    {
        public readonly SpikeBall[] SpikeBalls;
        private const float CarRadius = .32f;
        // Clear floor a ball needs round it when placed.
        private float SpawnMargin => Tuning.spikeRadius + .3f;

        private void ResetSpikeBalls()
        {
            var taken = new List<Vector2>();
            foreach (var ball in SpikeBalls)
            {
                float heading = ball.heading;
                Vector2 spot = ball.point;
                if (Tuning.randomPropSpots) { spot = RandomBallSpot(spot, taken); heading = Random.Range(0f, 360f); }
                else if (!TerrainClear(spot, SpawnMargin)) spot = ClearFloor(spot, SpawnMargin);
                taken.Add(spot);
                ball.position = spot;
                ball.velocity = Direction(heading) * Tuning.spikeRollSpeed;
                ball.rolled = 0;
                ball.Launcher = null;
                ball.touching.Clear();
            }
        }

        private static Vector2 Direction(float degrees) =>
            new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        // A random spot clear of terrain, start and respawn spots, crates, props and the other balls.
        private Vector2 RandomBallSpot(Vector2 fallback, List<Vector2> taken)
        {
            Vector2 min = Tuning.arenaMin + Vector2.one, max = Tuning.arenaMax - Vector2.one;
            var spawns = StartSpots().Select(s => s.spot).Concat(RespawnSpots).ToArray();
            for (int attempt = 0; attempt < 60; attempt++)
            {
                var spot = new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));
                if (TerrainClear(spot, Tuning.spikeRadius + .5f) && spawns.All(s => Vector2.Distance(s, spot) > 1.6f)
                    && Pickups.All(p => Vector2.Distance(p.point, spot) > 1) && Props.All(p => Vector2.Distance(p.point, spot) > 1)
                    && taken.All(t => Vector2.Distance(t, spot) > 2))
                    return spot;
            }
            return TerrainClear(fallback, SpawnMargin) ? fallback : ClearFloor(fallback, SpawnMargin);
        }

        private void MoveSpikeBalls(float dt)
        {
            foreach (var ball in SpikeBalls)
            {
                // A flying ball slows down until it is back to its slow roll.
                float speed = ball.velocity.magnitude;
                if (ball.Flying)
                {
                    speed = Mathf.Max(Tuning.spikeRollSpeed, speed - Tuning.spikeFlyFriction * dt);
                    if (speed <= Tuning.spikeRollSpeed) ball.Launcher = null;
                    ball.velocity = ball.velocity.normalized * speed;
                }
                Vector2 from = ball.position;
                ball.position += ball.velocity * dt;
                ball.rolled += speed * dt;
                BounceSpikeBall(ball, from);
                TouchCars(ball);
            }
        }

        private void BounceSpikeBall(SpikeBall ball, Vector2 from)
        {
            float r = Tuning.spikeRadius;
            Vector2 min = Tuning.arenaMin + Vector2.one * r, max = Tuning.arenaMax - Vector2.one * r;
            if (ball.position.x < min.x || ball.position.x > max.x) ball.velocity.x = Mathf.Abs(ball.velocity.x) * (ball.position.x < min.x ? 1 : -1);
            if (ball.position.y < min.y || ball.position.y > max.y) ball.velocity.y = Mathf.Abs(ball.velocity.y) * (ball.position.y < min.y ? 1 : -1);
            ball.position = new Vector2(Mathf.Clamp(ball.position.x, min.x, max.x), Mathf.Clamp(ball.position.y, min.y, max.y));
            foreach (var obstacle in Obstacles)
            {
                // A ball rolls over a ramp and through a smashed fence.
                if (obstacle.broken || obstacle.kind == ObstacleKind.Ramp) continue;
                if (!obstacle.Sweep(from, ball.position, r, out _, out var contact, out var normal)) continue;
                ball.position = contact;
                if (Vector2.Dot(ball.velocity, normal) < 0) ball.velocity = Vector2.Reflect(ball.velocity, normal);
            }
        }

        // Moves a ball from a free point toward to, stopping at the arena edge or the first solid terrain on the way.
        private Vector2 ClearOfTerrain(Vector2 from, Vector2 to)
        {
            float r = Tuning.spikeRadius;
            Vector2 min = Tuning.arenaMin + Vector2.one * r, max = Tuning.arenaMax - Vector2.one * r;
            to = new Vector2(Mathf.Clamp(to.x, min.x, max.x), Mathf.Clamp(to.y, min.y, max.y));
            foreach (var obstacle in Obstacles)
                if (!obstacle.broken && obstacle.kind != ObstacleKind.Ramp && obstacle.Sweep(from, to, r, out _, out var contact, out _))
                    to = contact;
            return to;
        }

        private void TouchCars(SpikeBall ball)
        {
            foreach (var car in Cars)
            {
                bool touches = car.alive && !car.Airborne && Vector2.Distance(car.position, ball.position) < CarRadius + Tuning.spikeRadius;
                if (!touches) { ball.touching.Remove(car); continue; }
                if (!ball.touching.Add(car)) continue;
                Vector2 away = ball.position - car.position;
                if (away.sqrMagnitude < .0001f) away = car.Forward;
                away.Normalize();
                // Out of the car's footprint either way, but never into terrain or past the arena edge.
                ball.position = ClearOfTerrain(car.position, car.position + away * (CarRadius + Tuning.spikeRadius + .01f));
                if (ball.Flying && ball.Launcher != car) StruckBySpikeBall(ball, car, away);
                else HitSpikeBall(ball, car, away);
            }
        }

        // A car drove into the ball (or the ball rolled into it): part damage, and the ball flies off by the impact.
        private void HitSpikeBall(SpikeBall ball, ArcadeVehicle car, Vector2 away)
        {
            float impact = Mathf.Max(0, Vector2.Dot(car.Forward * car.speed - ball.velocity, away));
            bool hurt = ApplyDamage(car, Tuning.spikeDamage);
            float launch = Mathf.Clamp(impact * Tuning.spikeLaunchPerSpeed, Tuning.spikeRollSpeed, Tuning.spikeLaunchMax);
            ball.velocity = away * launch;
            // A car the hit wrecked launches nothing: no wreck bonus for a car that is out.
            ball.Launcher = impact >= Tuning.spikeFlyImpact && car.alive ? car : null;
            if (car.alive) { car.speed *= .5f; car.stun = Mathf.Max(car.stun, .12f); car.RecordCollision(); }
            events.Add(new SpikeBallHit(car, impact, hurt, hurt && !car.alive, ball.position));
        }

        // A flying ball reached another car: it wrecks it outright, unless a shield, spawn protection or the opening
        // grace period protects it, and the car that knocked the ball gets the wreck bonus. The ball drops to a roll.
        private void StruckBySpikeBall(SpikeBall ball, ArcadeVehicle victim, Vector2 away)
        {
            var launcher = ball.Launcher;
            bool wrecked = CanHarm(launcher, victim) && ApplyDamage(victim, 1, ignoresStrength: true) && !victim.alive;
            if (wrecked && !launcher.eliminated) AwardWreck(launcher);
            if (victim.alive) victim.RecordCollision();
            ball.velocity = away * Tuning.spikeRollSpeed;
            ball.Launcher = null;
            events.Add(new SpikeBallStruck(launcher, victim, wrecked, ball.position));
        }
    }
}
