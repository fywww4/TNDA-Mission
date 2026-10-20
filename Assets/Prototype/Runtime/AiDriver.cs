using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TNDA.Prototype
{
    // The three fixed computer-car personalities. Each drives by one simple rule a player can learn in a few seconds;
    // the match rules (damage, scores, wrecks, respawns) are the same for all of them.
    public enum AiPersonality
    {
        // 碰碰: rams the nearest car, bounces off and goes for the next nearest; smashes props or grabs crates when nobody is in reach.
        Bumper,
        // 追王: chases a leader for a while (first place, sometimes second; never itself), then scatters to props and
        // crates for a while, like the ghosts' chase/scatter rhythm. A car it rams is left alone for the next chase.
        Hunter,
        // 迷糊: wanders about at random, never rams on purpose and picks up props and crates that happen to be on its way.
        Wanderer
    }

    // What an AI driver can see of the match.
    public interface IAiArena
    {
        IReadOnlyList<ArcadeVehicle> Cars { get; }
        IReadOnlyList<BreakableProp> Props { get; }
        IReadOnlyList<ArenaPickup> Pickups { get; }
        float Elapsed { get; }
        // The lava pool, once it has formed (a square of LavaHalfSize either side of LavaPoint).
        bool LavaActive { get; }
        Vector2 LavaPoint { get; }
        float LavaHalfSize { get; }
        Vector2 ArenaMin { get; }
        Vector2 ArenaMax { get; }
        // How much high scores outweigh distance when a cherry car picks its prey (0 = nearest only).
        float LeaderBias { get; }
        // 追王 rhythm: seconds chasing, seconds scattering, and the chance a chase goes for first rather than second place.
        float HunterChaseSeconds { get; }
        float HunterScatterSeconds { get; }
        float HunterLeaderChance { get; }
        bool CanHarm(ArcadeVehicle attacker, ArcadeVehicle victim);
    }

    // One car's AI: its personality picks where to drive and when to use the held item. Every personality flees cherry
    // cars, steers around spared players and hunts with a cherry of its own. It notices collisions, respawns and
    // eliminations from the match itself and replans; nothing else touches its plan.
    public sealed class AiDriver
    {
        private readonly ArcadeVehicle car;
        private float think, itemDelay = -1, bumpedSeconds;
        private Vector2 destination;
        private ArcadeVehicle target, bumped;
        private bool fresh = true;
        // 追王 rhythm state: time left in the current chase or scatter, and the car it rammed last chase.
        private float huntPhase;
        private bool scattering, rammedTarget, wantLeader = true;
        private ArcadeVehicle spared, sparedNext;
        private int seenCollisions, seenSpawns, seenContenders;
        // Getting unstuck (2026-10-16): a car that has barely moved for a second backs out toward open floor; a slow bump
        // into a prop it was heading for sends it back for a run-up and a charge fast enough to smash it, and a prop that
        // was merely in the way is sidestepped.
        private Vector2 stuckAnchor, detour;
        private float stuckClock, escapeLeft, escapeHeading, detourLeft, chargeLeft;
        private BreakableProp charging;
        // A run-up this long reaches the 2.1 speed the match needs to smash a prop.
        private const float RunUp = 1.9f;

        public AiDriver(ArcadeVehicle car) { this.car = car; }

        // Fixed by seat so a car keeps its personality in both modes: 1P has all three, 2P keeps 追王 and 碰碰, and the
        // demo's fourth car (the P1 seat) is a second 碰碰.
        public AiPersonality Personality => car.index == 1 ? AiPersonality.Wanderer : car.index == 2 ? AiPersonality.Hunter : AiPersonality.Bumper;

        // The car this AI is ramming right now; a wreck hunts nobody.
        public ArcadeVehicle Target => car.alive ? target : null;
        public Vector2 Destination => destination;

        // A new match staggers the first plans so the AI cars do not all decide on the same frame.
        public void BeginMatch() => fresh = true;

        public ArcadeInput Drive(IAiArena arena, float dt)
        {
            NoticeChanges(arena);
            if (Personality == AiPersonality.Hunter) UpdateHuntRhythm(arena, dt);
            think -= dt;
            bumpedSeconds -= dt;
            if (Escaping(arena, dt, out var escape)) return escape;
            if (car.HasCherry) HuntWithCherry(arena);
            else if (detourLeft <= 0 && chargeLeft <= 0 && (think <= 0 || Vector2.Distance(car.position, destination) < .4f
                || (target != null && !arena.CanHarm(car, target)))) ChooseDestination(arena);
            if (target != null) destination = target.position;
            FollowDetour(dt);
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
            // Lava is steered around, except by a cherry car: faster and careless, it can be lured in.
            if (arena.LavaActive && !car.HasCherry) desired = AroundLava(arena, desired);
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
                action = WantsItem(arena, delta, error, dt)
            };
        }

        // Only lava actually in the way matters: when the straight line toward the wanted heading passes over the pool,
        // the car aims past the pool's nearer edge on the side away from it, and takes the other side if that would run
        // into a wall. Right next to the pool it simply drives away from it. (2026-10-20: the old rule always swerved
        // 100 degrees, which in a gap beside a wall bounced a car between wall and lava for good.)
        private float AroundLava(IAiArena arena, float desired)
        {
            Vector2 toLava = arena.LavaPoint - car.position;
            // The pool is square: its corners reach half * 1.41 from the centre, plus a car's half width.
            float reach = arena.LavaHalfSize * 1.42f + .45f, distance = toLava.magnitude;
            if (distance < reach)
            {
                Vector2 away = distance > .01f ? -toLava : -car.Forward;
                return Mathf.Atan2(away.y, away.x) * Mathf.Rad2Deg;
            }
            Vector2 want = new Vector2(Mathf.Cos(desired * Mathf.Deg2Rad), Mathf.Sin(desired * Mathf.Deg2Rad));
            float along = Vector2.Dot(toLava, want);
            float across = want.x * toLava.y - want.y * toLava.x; // > 0: the pool lies to the left of the wanted line
            if (along <= 0 || along > reach + 1.7f || Mathf.Abs(across) > reach + .2f) return desired;
            // Clear the pool's corners with room to spare: the angle grows as the car gets closer.
            float clear = Mathf.Asin(Mathf.Clamp01((reach + .15f) / distance)) * Mathf.Rad2Deg;
            float lavaAngle = Mathf.Atan2(toLava.y, toLava.x) * Mathf.Rad2Deg;
            float side = across >= 0 ? -1 : 1; // pass on the side away from the pool
            float pass = lavaAngle + side * (clear + 8);
            if (RunsIntoWall(arena, pass)) pass = lavaAngle - side * (clear + 8);
            return pass;
        }

        private bool RunsIntoWall(IAiArena arena, float heading)
        {
            Vector2 ahead = car.position + new Vector2(Mathf.Cos(heading * Mathf.Deg2Rad), Mathf.Sin(heading * Mathf.Deg2Rad)) * 1.1f;
            return ahead.x < arena.ArenaMin.x + .3f || ahead.x > arena.ArenaMax.x - .3f
                || ahead.y < arena.ArenaMin.y + .3f || ahead.y > arena.ArenaMax.y - .3f;
        }

        // Bumps, a respawn or an elimination since the last frame make the current plan stale. A bump into the car
        // being rammed sends a 碰碰 off to another car.
        private void NoticeChanges(IAiArena arena)
        {
            int contenders = arena.Cars.Count(c => !c.eliminated);
            if (fresh)
            {
                fresh = false;
                think = .2f + car.index * .15f;
                destination = new Vector2(car.index % 2 == 0 ? -1.8f : 1.8f, -.6f);
                target = bumped = null;
                itemDelay = -1;
                huntPhase = 0;
                scattering = true; // so the first rhythm step starts a chase
                rammedTarget = false;
                spared = sparedNext = null;
                stuckAnchor = car.position;
                stuckClock = escapeLeft = detourLeft = chargeLeft = 0;
                charging = null;
            }
            if (car.Spawns != seenSpawns)
            {
                stuckAnchor = car.position;
                stuckClock = escapeLeft = detourLeft = chargeLeft = 0;
                charging = null;
            }
            else if (car.Collisions != seenCollisions || car.Spawns != seenSpawns || contenders != seenContenders)
            {
                if (car.Collisions != seenCollisions && target != null && Vector2.Distance(target.position, car.position) < 1)
                {
                    bumped = target;
                    bumpedSeconds = 2;
                    rammedTarget = true;
                }
                if (car.Collisions != seenCollisions) NoticeSlowPropBump(arena);
                think = 0;
            }
            if (car.Spawns != seenSpawns || car.item == ArenaItem.None) itemDelay = -1;
            seenCollisions = car.Collisions;
            seenSpawns = car.Spawns;
            seenContenders = contenders;
        }

        // A car that has moved less than 0.3 in the last second (not while spinning on oil or stunned) turns toward the
        // arena centre, away from lava, and drives out for 0.8 s before replanning.
        private bool Escaping(IAiArena arena, float dt, out ArcadeInput input)
        {
            input = default;
            if (escapeLeft > 0)
            {
                escapeLeft -= dt;
                if (escapeLeft <= 0) { think = 0; stuckAnchor = car.position; stuckClock = 0; }
                float turn = Mathf.DeltaAngle(car.heading, escapeHeading);
                input = new ArcadeInput { steer = -Mathf.Clamp(turn / 38, -1, 1), throttle = 1 };
                return true;
            }
            stuckClock += dt;
            if (stuckClock < 1) return false;
            bool stuck = Vector2.Distance(car.position, stuckAnchor) < .3f && !car.IsSlipping && car.stun <= 0;
            stuckAnchor = car.position;
            stuckClock = 0;
            if (!stuck) return false;
            Vector2 open = (arena.ArenaMin + arena.ArenaMax) / 2 - car.position;
            if (open.sqrMagnitude < .01f) open = -car.Forward;
            float heading = Mathf.Atan2(open.y, open.x) * Mathf.Rad2Deg + Random.Range(-45f, 45f);
            if (arena.LavaActive)
            {
                Vector2 toLava = arena.LavaPoint - car.position;
                float off = Mathf.DeltaAngle(heading, Mathf.Atan2(toLava.y, toLava.x) * Mathf.Rad2Deg);
                if (toLava.magnitude < 2.5f && Mathf.Abs(off) < 50) heading -= Mathf.Sign(off == 0 ? 1 : off) * 90;
            }
            escapeHeading = heading;
            escapeLeft = .8f;
            target = null;
            detourLeft = chargeLeft = 0;
            charging = null;
            input = new ArcadeInput { steer = -Mathf.Clamp(Mathf.DeltaAngle(car.heading, heading) / 38, -1, 1), throttle = 1 };
            return true;
        }

        // A slow bump into an unbroken prop (the match bounced the car back). Heading for it: back off for a run-up and
        // charge. Just passing: sidestep it on the side of where the car is going.
        private void NoticeSlowPropBump(IAiArena arena)
        {
            if (car.speed >= 0 || car.HasCherry) return;
            var prop = arena.Props.Where(p => !p.broken).OrderBy(p => Vector2.Distance(p.point, car.position)).FirstOrDefault();
            if (prop == null || Vector2.Distance(prop.point, car.position) > .8f) return;
            Vector2 away = car.position - prop.point;
            if (away.sqrMagnitude < .001f) away = -car.Forward;
            away.Normalize();
            if (target == null && Vector2.Distance(destination, prop.point) < .5f)
            {
                // Back off along the approach (or 60 degrees either side, whichever leaves the longest run-up).
                Vector2 best = prop.point;
                foreach (float turn in new[] { 0f, 60f, -60f })
                {
                    Vector2 point = Inside(arena, prop.point + (Vector2)(Quaternion.Euler(0, 0, turn) * away) * RunUp);
                    if (Vector2.Distance(point, prop.point) > Vector2.Distance(best, prop.point)) best = point;
                }
                detour = best;
                detourLeft = 2.5f;
                charging = prop;
            }
            else
            {
                Vector2 goal = (target != null ? target.position : destination) - car.position;
                Vector2 side = new Vector2(-away.y, away.x);
                if (Vector2.Dot(side, goal) < 0) side = -side;
                detour = Inside(arena, prop.point + side * 1.1f + away * .3f);
                detourLeft = 1.2f;
                charging = null;
            }
        }

        // Drives to the detour point first; after a run-up, charges the prop until it breaks or 1.6 s pass.
        private void FollowDetour(float dt)
        {
            if (detourLeft > 0)
            {
                detourLeft -= dt;
                destination = detour;
                if (Vector2.Distance(car.position, detour) < .45f || detourLeft <= 0)
                {
                    detourLeft = 0;
                    if (charging != null) chargeLeft = 1.6f;
                    else think = 0;
                }
                return;
            }
            if (chargeLeft <= 0) return;
            chargeLeft -= dt;
            if (charging == null || charging.broken || chargeLeft <= 0)
            {
                chargeLeft = 0;
                charging = null;
                think = 0;
                return;
            }
            target = null;
            destination = charging.point;
        }

        private static Vector2 Inside(IAiArena arena, Vector2 point) => new Vector2(
            Mathf.Clamp(point.x, arena.ArenaMin.x + .5f, arena.ArenaMax.x - .5f), Mathf.Clamp(point.y, arena.ArenaMin.y + .5f, arena.ArenaMax.y - .5f));

        private void ChooseDestination(IAiArena arena)
        {
            target = null;
            switch (Personality)
            {
                case AiPersonality.Hunter:
                    think = 1;
                    if (!scattering) target = Quarry(arena);
                    if (target == null) destination = NearestProp(arena) ?? NearestCrate(arena) ?? Roam(arena);
                    break;
                case AiPersonality.Bumper:
                    think = 1.5f;
                    target = Nearest(arena, c => c != bumped || bumpedSeconds <= 0) ?? Nearest(arena, c => true);
                    if (target == null) destination = NearestProp(arena) ?? NearestCrate(arena) ?? Roam(arena);
                    break;
                default:
                    think = Random.Range(2.5f, 4f);
                    destination = OnTheWay(arena) ?? Roam(arena);
                    break;
            }
        }

        // With a cherry every personality hunts, preferring high scores and nearer cars.
        private void HuntWithCherry(IAiArena arena)
        {
            if (target != null && arena.CanHarm(car, target)) return;
            float top = arena.Cars.Max(c => c.score) + 1;
            target = arena.Cars.Where(c => arena.CanHarm(car, c))
                .OrderByDescending(c => (1 + arena.LeaderBias * c.score / top) / (1 + Vector2.Distance(c.position, car.position) * .35f)
                    * Random.Range(.85f, 1.15f))
                .FirstOrDefault();
            if (target == null && think <= 0) ChooseDestination(arena);
        }

        // 追王: a chase ends after its time or as soon as it rams its quarry; the car it rammed is spared next chase.
        // Scatters always last their full time.
        private void UpdateHuntRhythm(IAiArena arena, float dt)
        {
            huntPhase -= dt;
            bool rammed = rammedTarget && !scattering;
            rammedTarget = false;
            if (rammed) sparedNext = bumped;
            if (!rammed && huntPhase > 0) return;
            scattering = !scattering && huntPhase <= 0 || rammed;
            if (scattering) huntPhase = arena.HunterScatterSeconds;
            else
            {
                huntPhase = arena.HunterChaseSeconds;
                wantLeader = Random.value < arena.HunterLeaderChance;
                spared = sparedNext;
                sparedNext = null;
            }
            target = null;
            think = 0;
        }

        // The chase target: first place (or second when this chase rolled second), ranked by score among cars it may
        // hit, itself and the car it rammed last chase left out; nearer wins ties.
        private ArcadeVehicle Quarry(IAiArena arena)
        {
            var ranked = arena.Cars.Where(c => arena.CanHarm(car, c) && c != spared)
                .OrderByDescending(c => c.score).ThenBy(c => Vector2.Distance(c.position, car.position)).ToList();
            if (ranked.Count == 0) return null;
            return ranked[wantLeader || ranked.Count == 1 ? 0 : 1];
        }

        // Read by checks: whether a 追王 is in its scatter half of the rhythm.
        public bool Scattering => Personality == AiPersonality.Hunter && scattering;

        private ArcadeVehicle Nearest(IAiArena arena, System.Func<ArcadeVehicle, bool> allowed) => arena.Cars
            .Where(c => arena.CanHarm(car, c) && allowed(c)).OrderBy(c => Vector2.Distance(c.position, car.position)).FirstOrDefault();

        private Vector2? NearestProp(IAiArena arena) => arena.Props.Where(p => !p.broken)
            .OrderBy(p => Vector2.Distance(p.point, car.position)).Select(p => (Vector2?)p.point).FirstOrDefault();

        private Vector2? NearestCrate(IAiArena arena) => car.item != ArenaItem.None ? null : arena.Pickups.Where(p => p.available)
            .OrderBy(p => Vector2.Distance(p.point, car.position)).Select(p => (Vector2?)p.point).FirstOrDefault();

        // A 迷糊 only detours for a prop or crate that is close and roughly ahead.
        private Vector2? OnTheWay(IAiArena arena)
        {
            bool Ahead(Vector2 point) => Vector2.Distance(point, car.position) < 1.8f
                && Vector2.Dot(car.Forward, (point - car.position).normalized) > .3f;
            var spots = arena.Props.Where(p => !p.broken).Select(p => p.point);
            if (car.item == ArenaItem.None) spots = spots.Concat(arena.Pickups.Where(p => p.available).Select(p => p.point));
            return spots.Where(Ahead).Select(p => (Vector2?)p).FirstOrDefault();
        }

        private Vector2 Roam(IAiArena arena)
        {
            Vector2 min = arena.ArenaMin + Vector2.one * .6f, max = arena.ArenaMax - Vector2.one * .6f;
            return new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));
        }

        private bool WantsItem(IAiArena arena, Vector2 delta, float error, float dt)
        {
            if (car.item == ArenaItem.None) return false;
            // Never while a spared player is close by.
            bool humansClear = arena.Cars.All(c => c == car || !c.alive || arena.CanHarm(car, c)
                || Vector2.Distance(c.position, car.position) > 1.8f);
            if (!humansClear) return false;
            bool targetAhead = target != null && delta.magnitude < 2.7f && Mathf.Abs(error) < 22;
            // Oil goes down when a car is right behind (追王 and 碰碰).
            bool carBehind = arena.Cars.Any(c => c != car && c.alive && Vector2.Distance(c.position, car.position) < 2
                && Vector2.Dot(car.Forward, (c.position - car.position).normalized) < -.7f);
            switch (Personality)
            {
                case AiPersonality.Hunter:
                    // Shield only when nearly wrecked; rockets only at the leader it is chasing.
                    return car.item == ArenaItem.Shield ? car.health < .4f
                        : car.item == ArenaItem.Oil ? carBehind
                        : car.item == ArenaItem.Rocket ? targetAhead
                        : car.item == ArenaItem.Cherry && RivalNear(arena);
                case AiPersonality.Bumper:
                    // Shield just before a ram; a rocket at any car in front.
                    return car.item == ArenaItem.Shield ? target != null && delta.magnitude < 1.5f && Mathf.Abs(error) < 30
                        : car.item == ArenaItem.Oil ? carBehind
                        : car.item == ArenaItem.Rocket ? arena.Cars.Any(c => arena.CanHarm(car, c) && Vector2.Distance(c.position, car.position) < 2.7f
                            && Vector2.Dot(car.Forward, (c.position - car.position).normalized) > .93f)
                        : car.item == ArenaItem.Cherry && RivalNear(arena);
                default:
                    // Uses whatever it holds 2 to 4 seconds after picking it up, aimed at nothing.
                    if (itemDelay < 0) itemDelay = Random.Range(2f, 4f);
                    itemDelay -= dt;
                    return itemDelay <= 0;
            }
        }

        private bool RivalNear(IAiArena arena) => arena.Cars.Any(c => arena.CanHarm(car, c) && Vector2.Distance(c.position, car.position) < 3);
    }
}
