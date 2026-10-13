using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TNDA.Prototype
{
    // What an AI driver can see of the match.
    public interface IAiArena
    {
        IReadOnlyList<ArcadeVehicle> Cars { get; }
        IReadOnlyList<BreakableProp> Props { get; }
        IReadOnlyList<ArenaPickup> Pickups { get; }
        float Elapsed { get; }
        // How much high scores outweigh distance when picking a rival (0 = nearest only).
        float LeaderBias { get; }
        // A car this many points ahead is hunted whatever the current strategy.
        float LeaderHuntMargin { get; }
        bool CanHarm(ArcadeVehicle attacker, ArcadeVehicle victim);
    }

    // One car's AI: picks rivals (leaders first, nearer is better), rotates between ramming, crates and props,
    // steers around spared players, flees cherry cars and times item use. It notices collisions, respawns and
    // eliminations from the match itself and replans; nothing else touches its plan.
    public sealed class AiDriver
    {
        private readonly ArcadeVehicle car;
        private float think;
        private Vector2 destination;
        private ArcadeVehicle target;
        private bool fresh = true;
        private int seenCollisions, seenSpawns, seenContenders;

        public AiDriver(ArcadeVehicle car) { this.car = car; }

        // The car this AI is ramming right now; a wreck hunts nobody.
        public ArcadeVehicle Target => car.alive ? target : null;
        public Vector2 Destination => destination;

        // A new match staggers the first plans so the AI cars do not all decide on the same frame.
        public void BeginMatch() => fresh = true;

        public ArcadeInput Drive(IAiArena arena, float dt)
        {
            NoticeChanges(arena);
            think -= dt;
            if (think <= 0 || Vector2.Distance(car.position, destination) < .4f
                || (target != null && !arena.CanHarm(car, target))) ChooseDestination(arena);
            if (car.HasCherry && target == null) target = PickRival(arena);
            if (target != null) destination = target.position;
            Vector2 delta = destination - car.position;
            float desired = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            var hunter = car.HasCherry ? null : arena.Cars.FirstOrDefault(c => c != car && c.HasCherry
                && Vector2.Distance(c.position, car.position) < 2.2f);
            if (hunter != null)
            {
                Vector2 flee = car.position - hunter.position;
                if (flee.sqrMagnitude < .01f) flee = Vector2.right;
                desired = Mathf.Atan2(flee.y, flee.x) * Mathf.Rad2Deg;
            }
            // Players are steered around unless they are this AI's target after the grace period.
            foreach (var human in arena.Cars.Where(c => !c.computer && c.alive && !(c == target && arena.CanHarm(car, c))))
            {
                Vector2 toHuman = human.position - car.position;
                if (toHuman.magnitude < 1.6f && Vector2.Dot(car.Forward, toHuman.normalized) > -.1f)
                {
                    Vector2 away = car.position - human.position;
                    if (away.sqrMagnitude < .01f) away = Vector2.right;
                    desired = Mathf.Atan2(away.y, away.x) * Mathf.Rad2Deg;
                    break;
                }
            }
            float error = Mathf.DeltaAngle(car.heading, desired);
            return new ArcadeInput {
                steer = -Mathf.Clamp(error / 38, -1, 1),
                throttle = Mathf.Abs(error) > 65 ? .35f : 1,
                action = WantsItem(arena, delta, error)
            };
        }

        // Bumps, a respawn or an elimination since the last frame make the current plan stale.
        private void NoticeChanges(IAiArena arena)
        {
            int contenders = arena.Cars.Count(c => !c.eliminated);
            if (fresh)
            {
                fresh = false;
                think = .2f + car.index * .15f;
                destination = new Vector2(car.index % 2 == 0 ? -1.8f : 1.8f, -.6f);
                target = null;
            }
            else if (car.Collisions != seenCollisions || car.Spawns != seenSpawns || contenders != seenContenders) think = 0;
            seenCollisions = car.Collisions;
            seenSpawns = car.Spawns;
            seenContenders = contenders;
        }

        private bool WantsItem(IAiArena arena, Vector2 delta, float error)
        {
            // Never while a spared player is close by.
            bool humansClear = arena.Cars.All(c => c == car || !c.alive || arena.CanHarm(car, c)
                || Vector2.Distance(c.position, car.position) > 1.8f);
            bool rivalAhead = target != null && delta.magnitude < 2.7f && Mathf.Abs(error) < 22;
            bool hasPropAhead = arena.Props.Any(p => !p.broken && Vector2.Distance(p.point, car.position) < 1.6f
                && Vector2.Dot(car.Forward, (p.point - car.position).normalized) > .8f);
            bool rivalNear = arena.Cars.Any(c => arena.CanHarm(car, c) && Vector2.Distance(c.position, car.position) < 3);
            bool activate = car.item == ArenaItem.Shield ? car.health < .75f || rivalAhead
                : car.item == ArenaItem.Rocket ? rivalAhead || hasPropAhead
                : car.item == ArenaItem.Cherry && rivalNear;
            return humansClear && activate;
        }

        // Which car to ram: high scores first, nearer is better. Harm rules keep players out during the grace period.
        private ArcadeVehicle PickRival(IAiArena arena)
        {
            float top = arena.Cars.Max(c => c.score) + 1;
            return arena.Cars.Where(c => arena.CanHarm(car, c))
                .OrderByDescending(c => (1 + arena.LeaderBias * c.score / top) / (1 + Vector2.Distance(c.position, car.position) * .35f)
                    * Random.Range(.85f, 1.15f))
                .FirstOrDefault();
        }

        private void ChooseDestination(IAiArena arena)
        {
            int i = car.index;
            float elapsed = arena.Elapsed;
            think = 2.2f + i * .35f;
            target = null;
            var rival = PickRival(arena);
            // A car well ahead of this AI is hunted on purpose, whatever the current strategy.
            bool huntLeader = rival != null && rival.score >= car.score + arena.LeaderHuntMargin;
            var prop = arena.Props.Where(p => !p.broken).OrderBy(p => Vector2.Distance(p.point, car.position)).FirstOrDefault();
            var pickup = car.item == ArenaItem.None ? arena.Pickups.Where(p => p.available)
                .OrderBy(p => Vector2.Distance(p.point, car.position)).FirstOrDefault() : null;
            // Rotate priorities so rivals collide as well as collecting and breaking objects.
            int strategy = (Mathf.FloorToInt(elapsed / 4) + i) % 3;
            if (rival != null && (huntLeader || strategy == 0 || car.item == ArenaItem.Rocket || prop == null && pickup == null))
            {
                target = rival;
                destination = rival.position;
            }
            else if (pickup != null && (strategy == 1 || prop == null)) destination = pickup.point;
            else if (prop != null) destination = prop.point;
            else destination = new Vector2(Mathf.Sin(elapsed + i) * 2.8f, Mathf.Cos(elapsed + i) * 1.6f - .6f);
        }
    }
}
