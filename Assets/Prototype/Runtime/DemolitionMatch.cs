using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TNDA.Prototype
{
    // Gameplay values the match rules read. Defaults are the prototype values; DemolitionGame copies its Inspector
    // tuning in before every reset and tick.
    public sealed class MatchTuning
    {
        public float firstElimination = 45, secondElimination = 80, roundSeconds = 120;
        public float driveSpeed = 2.8f, accelerationPerSecond = 1.4f, coastDownPerSecond = 2.2f;
        public float respawnSeconds = 3;
        public int wreckPenalty = 250, wreckBonus = 300;
        public float shieldDuration = 6, pickupRespawnSeconds = 8;
        public float aiGraceSeconds = 10, leaderBias = 1.5f;
        // Computer top speed as a share of the players' (after the first elimination it rises); a 迷糊 drives slower still.
        public float aiTopSpeed = .87f, aiLateTopSpeed = .95f, wandererTopSpeedScale = .85f;
        // 追王 rhythm (2026-10-16): chase seconds, scatter seconds, chance a chase targets first rather than second place.
        public float hunterChaseSeconds = 6, hunterScatterSeconds = 4, hunterLeaderChance = .6f;
        public float cherryDuration = 8, cherrySpeedMultiplier = 1.35f, trailingCherryChance = .5f;
        public float spinOutTurnsPerSecond = 4, spinOutStartSpeed = 4, spinOutAcceleration = 14;
        public float contactBump = 1.4f, ramBump = 2.4f, bumpFriction = 4.5f;
        public Vector2 arenaMin = new Vector2(-4.35f, -3.85f), arenaMax = new Vector2(4.35f, 1.65f);
        public bool randomPropSpots = true;
        // Oil: the slick spins a car out for oilSlipSeconds, earns its dropper oilBonus, and dries after oilLifeSeconds.
        public float oilSlipSeconds = .8f, oilLifeSeconds = 12;
        public int oilBonus = 50;
    }

    // The elimination match rules: driving, props, crates, contacts, rockets, wrecks, timed eliminations and the
    // champion. It needs no scene objects besides the cars, and reports what happened as Events for an adapter to show.
    public sealed class DemolitionMatch : IItemArena, IAiArena
    {
        public readonly ArcadeVehicle[] Cars;
        public readonly BreakableProp[] Props;
        public readonly ArenaPickup[] Pickups;
        public readonly ArcadeProjectile[] Rockets;
        public readonly MatchTuning Tuning;

        private readonly List<MatchEvent> events = new List<MatchEvent>();
        private readonly Dictionary<(ArcadeVehicle, ArcadeVehicle), float> contactCooldowns = new Dictionary<(ArcadeVehicle, ArcadeVehicle), float>();
        private bool demo;

        public DemolitionMatch(ArcadeVehicle[] cars, BreakableProp[] props, ArenaPickup[] pickups, ArcadeProjectile[] rockets, MatchTuning tuning)
        {
            Cars = cars; Props = props; Pickups = pickups; Rockets = rockets; Tuning = tuning;
        }

        // Everything reported since the last ClearEvents (or Reset), oldest first.
        public IReadOnlyList<MatchEvent> Events => events;
        public void ClearEvents() => events.Clear();

        public float Elapsed { get; private set; }
        public int EliminationStage { get; private set; }
        public bool Finished { get; private set; }
        public bool Survived { get; private set; }
        public int Wrecks { get; private set; }
        public int Champion { get; private set; } = -1;
        public float NextElimination => EliminationStage == 0 ? Tuning.firstElimination : EliminationStage == 1 ? Tuning.secondElimination : Tuning.roundSeconds;
        public int Contenders => Cars.Count(c => !c.eliminated);

        private static readonly Vector2[] RespawnCorners = { new Vector2(-3.5f, 1.1f), new Vector2(3.5f, 1.1f), new Vector2(3.5f, -3.2f), new Vector2(-3.5f, -3.2f) };

        private VehicleLifecycleRules LifecycleRules => new VehicleLifecycleRules {
            wreckPenalty = Tuning.wreckPenalty, respawnSeconds = Tuning.respawnSeconds,
            spinStartSpeed = Tuning.spinOutStartSpeed, spinAcceleration = Tuning.spinOutAcceleration,
            spinTurnsPerSecond = Tuning.spinOutTurnsPerSecond, arenaCentre = (Tuning.arenaMin + Tuning.arenaMax) / 2
        };

        // A demo match (all AI) has no timed eliminations; its host eliminates at its own pace.
        public void Reset(int humans, bool demoMatch = false)
        {
            demo = demoMatch;
            Elapsed = 0;
            Finished = Survived = false;
            Wrecks = EliminationStage = 0;
            Champion = -1;
            events.Clear();
            Vector2[] starts = { new Vector2(-3.3f, -3.1f), new Vector2(3.3f, -3.1f), new Vector2(3.3f, 1.1f), new Vector2(-3.3f, 1.1f) };
            for (int i = 0; i < Cars.Length; i++)
            {
                Cars[i].ResetVehicle(starts[i], i < 2 ? 90 : -90, humans);
                Cars[i].Ai.BeginMatch();
            }
            foreach (var pickup in Pickups)
            {
                pickup.available = true;
                pickup.respawn = 0;
            }
            foreach (var prop in Props)
            {
                prop.broken = false;
                prop.respawn = 0;
                if (Tuning.randomPropSpots) prop.point = RandomPropSpot(prop, prop.point);
            }
            foreach (var rocket in Rockets) rocket.Hide();
            contactCooldowns.Clear();
        }

        public void Tick(float dt, ArcadeInput p1, ArcadeInput p2)
        {
            Elapsed += dt;
            RefreshSpawns(dt);
            foreach (var car in Cars)
            {
                car.AdvanceTimers(dt);
                if (car.eliminated) continue;
                if (!car.alive)
                {
                    if (car.ReadyToRespawn) RespawnVehicle(car);
                    continue;
                }
                ArcadeInput input = car.computer ? car.Ai.Drive(this, dt) : car.index == 0 ? p1 : p2;
                // The item button only fires a held item; a human item requires a fresh press.
                var held = car.item;
                if (held != ArenaItem.None && car.cooldown <= 0 && (car.computer ? input.action : input.actionPressed))
                {
                    if (ArenaItems.Use(car, this)) events.Add(new ItemUsed(car, held));
                    else if (held == ArenaItem.Rocket) events.Add(new RocketsBusy(car));
                }
                // Holding gas builds speed gradually; releasing it coasts down a little faster.
                float ramp = input.throttle > .1f ? Tuning.accelerationPerSecond : Tuning.coastDownPerSecond;
                float topSpeed = Tuning.driveSpeed * (car.computer ? AiTopSpeed(car) : 1) * (car.HasCherry ? Tuning.cherrySpeedMultiplier : 1);
                car.Drive(dt, input, topSpeed, ramp);
                // Bumper-car slide from recent hits, fading out.
                car.AdvanceSlide(dt, Tuning.bumpFriction);
                ConstrainToArena(car);
                HitProps(car);
                CollectPickup(car);
            }
            ResolveContacts(dt);
            MoveRockets(dt);
            UpdateSlicks(dt);
            while (!demo && !Finished && Elapsed >= NextElimination) EliminateLowest();
        }

        // Wrecked and eliminated cars keep spinning off screen in every phase until they leave the view.
        public void AdvanceSpinOuts(float dt, Vector2 viewCentre, float viewHalfSize)
        {
            foreach (var car in Cars) car.AdvanceSpinOut(dt, LifecycleRules, viewCentre, viewHalfSize);
        }

        private void RefreshSpawns(float dt)
        {
            foreach (var prop in Props)
            {
                if (!prop.broken) continue;
                prop.respawn -= dt;
                if (prop.respawn > 0) continue;
                if (Tuning.randomPropSpots) prop.point = RandomPropSpot(prop, prop.point);
                if (SpawnClear(prop.point, .9f)) prop.broken = false;
            }
            foreach (var pickup in Pickups)
            {
                if (pickup.available) continue;
                pickup.respawn -= dt;
                if (pickup.respawn <= 0 && SpawnClear(pickup.point, .8f)) pickup.available = true;
            }
        }

        private bool SpawnClear(Vector2 point, float radius) => Cars.All(c => !c.alive || Vector2.Distance(c.position, point) > radius);

        // A random arena spot clear of cars, crates and the other props; keeps the fallback when none turns up.
        private Vector2 RandomPropSpot(BreakableProp prop, Vector2 fallback)
        {
            Vector2 min = Tuning.arenaMin + Vector2.one * .5f, max = Tuning.arenaMax - Vector2.one * .5f;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                var spot = new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));
                if (SpawnClear(spot, 1.2f) && Pickups.All(p => Vector2.Distance(p.point, spot) > 1)
                    && Props.All(p => p == prop || Vector2.Distance(p.point, spot) > 1.4f))
                    return spot;
            }
            return fallback;
        }

        private bool CanHarm(ArcadeVehicle attacker, ArcadeVehicle victim) =>
            VehicleHarmPolicy.CanHarm(attacker, victim, Elapsed, Tuning.aiGraceSeconds);

        IReadOnlyList<ArcadeVehicle> IAiArena.Cars => Cars;
        IReadOnlyList<BreakableProp> IAiArena.Props => Props;
        IReadOnlyList<ArenaPickup> IAiArena.Pickups => Pickups;
        float IAiArena.LeaderBias => Tuning.leaderBias;
        float IAiArena.HunterChaseSeconds => Tuning.hunterChaseSeconds;
        float IAiArena.HunterScatterSeconds => Tuning.hunterScatterSeconds;
        float IAiArena.HunterLeaderChance => Tuning.hunterLeaderChance;
        Vector2 IAiArena.ArenaMin => Tuning.arenaMin;
        Vector2 IAiArena.ArenaMax => Tuning.arenaMax;

        // Late in the match (from the first elimination) the computer cars speed up; personalities stay the same.
        public float AiTopSpeed(ArcadeVehicle car) => (EliminationStage > 0 ? Tuning.aiLateTopSpeed : Tuning.aiTopSpeed)
            * (car.Ai.Personality == AiPersonality.Wanderer ? Tuning.wandererTopSpeedScale : 1);
        bool IAiArena.CanHarm(ArcadeVehicle attacker, ArcadeVehicle victim) => CanHarm(attacker, victim);

        private void ConstrainToArena(ArcadeVehicle car)
        {
            Vector2 min = Tuning.arenaMin, max = Tuning.arenaMax;
            Vector2 bounded = new Vector2(Mathf.Clamp(car.position.x, min.x, max.x), Mathf.Clamp(car.position.y, min.y, max.y));
            if (bounded == car.position) return;
            bool hitX = !Mathf.Approximately(car.position.x, bounded.x);
            bool hitY = !Mathf.Approximately(car.position.y, bounded.y);
            car.position = bounded;
            // A bumper slide into a wall rebounds softly instead of pinning the car.
            car.ReboundSlide(hitX, hitY);
            car.RecordCollision();
            // Walls slow players without draining durability. AI cars bounce back into the arena.
            if (car.computer)
            {
                if (hitX) car.heading = 180 - car.heading;
                if (hitY) car.heading = -car.heading;
            }
            if (Mathf.Abs(car.speed) > 1) events.Add(new WallHit(car));
            car.speed = 0;
            car.stun = .12f;
        }

        private void HitProps(ArcadeVehicle car)
        {
            foreach (var prop in Props)
            {
                if (prop.broken || Vector2.Distance(car.position, prop.point) > .6f) continue;
                if (Mathf.Abs(car.speed) >= 2.1f)
                {
                    prop.broken = true;
                    prop.respawn = 6;
                    car.speed *= .75f;
                    events.Add(new PropSmashed(car, prop.points, false));
                    AddScore(car, prop.points);
                    car.RecordCollision();
                }
                else
                {
                    Vector2 away = (car.position - prop.point).normalized;
                    if (away.sqrMagnitude < .001f) away = -car.Forward;
                    car.position = prop.point + away * .65f;
                    car.speed *= -.3f;
                    car.stun = .15f;
                    car.RecordCollision();
                }
            }
        }

        private void CollectPickup(ArcadeVehicle car)
        {
            if (car.item != ArenaItem.None) return;
            foreach (var pickup in Pickups)
            {
                if (!pickup.available || Vector2.Distance(car.position, pickup.point) > .58f) continue;
                car.item = pickup.item != ArenaItem.None ? pickup.item
                    : ArenaItems.Roll(ArenaItems.IsTrailing(car, Cars), Tuning.trailingCherryChance, () => Random.value);
                pickup.available = false;
                pickup.respawn = Tuning.pickupRespawnSeconds;
                events.Add(new ItemPicked(car, car.item));
                break;
            }
        }

        float IItemArena.ShieldSeconds => Tuning.shieldDuration;
        float IItemArena.CherrySeconds => Tuning.cherryDuration;

        bool IItemArena.LaunchRocket(ArcadeVehicle car)
        {
            var rocket = Rockets.FirstOrDefault(r => !r.active);
            if (rocket == null) return false;
            rocket.Spawn(car.position + car.Forward * .62f, car.Forward * 6, car.index);
            return true;
        }

        bool IItemArena.DropOil(ArcadeVehicle car)
        {
            var slot = Rockets.FirstOrDefault(r => !r.active);
            if (slot == null) return false;
            slot.Drop(car.position - car.Forward * .55f, car.index);
            return true;
        }

        // Oil slicks: the first car onto one spins out (its dropper is immune for a second) and the slick is gone.
        private void UpdateSlicks(float dt)
        {
            foreach (var slick in Rockets)
            {
                if (!slick.active || !slick.slick) continue;
                slick.age += dt;
                if (slick.age > Tuning.oilLifeSeconds) { slick.Hide(); continue; }
                var dropper = slick.owner >= 0 && slick.owner < Cars.Length ? Cars[slick.owner] : null;
                var victim = Cars.FirstOrDefault(c => c.alive && !c.IsSlipping && !(c == dropper && slick.age < 1)
                    && Vector2.Distance(c.position, slick.position) < .4f);
                if (victim == null) continue;
                victim.Slip(Tuning.oilSlipSeconds);
                victim.RecordCollision();
                slick.Hide();
                events.Add(new CarSlipped(victim, dropper));
                if (dropper != null && dropper != victim && !dropper.eliminated) AddScore(dropper, Tuning.oilBonus);
            }
        }

        private void CherryWreck(ArcadeVehicle attacker, ArcadeVehicle victim)
        {
            if (!CanHarm(attacker, victim))
            {
                attacker.speed *= .3f;
                events.Add(new CherryStrike(attacker, victim, false));
                return;
            }
            bool wrecked = ApplyDamage(victim, 1) && !victim.alive;
            if (wrecked) AwardWreck(attacker);
            events.Add(new CherryStrike(attacker, victim, wrecked));
        }

        private void ResolveContacts(float dt)
        {
            for (int i = 0; i < Cars.Length; i++) for (int j = i + 1; j < Cars.Length; j++)
            {
                var a = Cars[i];
                var b = Cars[j];
                var pair = (a, b);
                contactCooldowns.TryGetValue(pair, out float cooldown);
                contactCooldowns[pair] = Mathf.Max(0, cooldown - dt);
                if (!a.alive || !b.alive || Vector2.Distance(a.position, b.position) >= .62f) continue;
                Vector2 separation = (a.position - b.position).normalized;
                if (separation.sqrMagnitude < .001f) separation = Vector2.right;
                float overlap = .63f - Vector2.Distance(a.position, b.position);
                a.position += separation * overlap * .5f;
                b.position -= separation * overlap * .5f;
                if (contactCooldowns[pair] <= 0)
                {
                    // Every new touch bounces both cars apart a little; a real ram adds more in ResolveImpact.
                    a.ApplySlide(separation * Tuning.contactBump);
                    b.ApplySlide(-separation * Tuning.contactBump);
                    a.RecordCollision();
                    b.RecordCollision();
                    ResolveImpact(a, b);
                    contactCooldowns[pair] = .8f;
                }
            }
        }

        private void ResolveImpact(ArcadeVehicle a, ArcadeVehicle b)
        {
            float approachA = Vector2.Dot(a.Forward * a.speed, (b.position - a.position).normalized);
            float approachB = Vector2.Dot(b.Forward * b.speed, (a.position - b.position).normalized);
            if (a.HasCherry != b.HasCherry)
            {
                // Cherry: any touch wrecks the other car, unless that car actually rammed the cherry car first.
                var cherry = a.HasCherry ? a : b;
                var other = cherry == a ? b : a;
                float otherRam = cherry == a ? approachB : approachA, cherryRam = cherry == a ? approachA : approachB;
                if (otherRam >= 1 && otherRam > cherryRam) CherryWreck(other, cherry);
                else CherryWreck(cherry, other);
                return;
            }
            var attacker = approachA >= approachB ? a : b;
            var victim = attacker == a ? b : a;
            float impact = Mathf.Max(0, Mathf.Max(approachA, approachB));
            if (impact < 1) return;
            float alignment = Vector2.Dot(attacker.Forward, victim.Forward);
            float rear = Vector2.Dot((attacker.position - victim.position).normalized, victim.Forward);
            float multiplier = Mathf.Abs(alignment) < .5f ? 2 : alignment > .6f && rear < -.25f ? 1.5f : 1;
            float damage = (.035f + impact * .035f) * multiplier;
            if (!CanHarm(attacker, victim)) damage = 0;
            bool damaged = ApplyDamage(victim, damage);
            if (damaged)
            {
                AddScore(attacker, Mathf.RoundToInt(impact * 30 * multiplier));
                if (!victim.alive) AwardWreck(attacker);
            }
            ApplyDamage(attacker, damage * .15f);
            attacker.speed *= .6f;
            if (damaged && victim.alive) { victim.speed *= .4f; victim.stun = .12f; }
            // Bumper cars: the rammed car slides away and its nose is knocked aside; the rammer rebounds.
            Vector2 push = (victim.position - attacker.position).normalized;
            victim.ApplySlide(push * Tuning.ramBump * (.6f + impact * .4f));
            attacker.ApplySlide(-push * Tuning.ramBump * .35f);
            victim.heading += Random.Range(15f, 30f) * (Random.value < .5f ? -1 : 1);
            events.Add(new CarRammed(attacker, victim, impact, multiplier, damaged && victim.alive));
        }

        private void MoveRockets(float dt)
        {
            foreach (var rocket in Rockets)
            {
                if (!rocket.active || rocket.slick) continue;
                var owner = Cars[rocket.owner];
                if (owner.eliminated) { rocket.Hide(); continue; }
                Vector2 from = rocket.position;
                rocket.position += rocket.velocity * dt;
                rocket.age += dt;
                // Swept hit detection avoids skipping cars at high projectile speed.
                var victim = Cars.Where(c => CanHarm(owner, c))
                    .Where(c => SegmentDistance(c.position, from, rocket.position) < .42f)
                    .OrderBy(c => Vector2.Distance(from, c.position)).FirstOrDefault();
                if (victim != null)
                {
                    if (ApplyDamage(victim, victim.HasCherry ? 1 : .38f))
                    {
                        AddScore(owner, 120);
                        if (!victim.alive) AwardWreck(owner);
                        if (victim.alive)
                        {
                            victim.stun = .5f;
                            victim.speed *= .2f;
                        }
                    }
                    events.Add(new RocketHit(owner, victim, victim.position));
                    rocket.Hide();
                    continue;
                }
                var prop = Props.FirstOrDefault(p => !p.broken && SegmentDistance(p.point, from, rocket.position) < .4f);
                if (prop != null)
                {
                    prop.broken = true;
                    prop.respawn = 6;
                    rocket.Hide();
                    events.Add(new PropSmashed(owner, prop.points, true));
                    AddScore(owner, prop.points);
                    continue;
                }
                if (rocket.age > 1.6f || rocket.position.x < Tuning.arenaMin.x || rocket.position.x > Tuning.arenaMax.x
                    || rocket.position.y < Tuning.arenaMin.y || rocket.position.y > Tuning.arenaMax.y) rocket.Hide();
            }
        }

        private static float SegmentDistance(Vector2 point, Vector2 from, Vector2 to)
        {
            Vector2 segment = to - from;
            float t = segment.sqrMagnitude > .00001f ? Mathf.Clamp01(Vector2.Dot(point - from, segment) / segment.sqrMagnitude) : 0;
            return Vector2.Distance(point, from + segment * t);
        }

        public bool ApplyDamage(ArcadeVehicle car, float damage)
        {
            float previousScore = car.score;
            Vector2 point = car.position;
            if (!car.TakeDamage(damage, LifecycleRules))
            {
                if (car.alive && damage > 0 && car.shieldSeconds > 0) events.Add(new ShieldBlocked(car));
                return false;
            }
            if (!car.alive)
            {
                Wrecks++;
                int deducted = Mathf.FloorToInt(previousScore - car.score);
                events.Add(new VehicleWrecked(car, deducted));
                if (deducted > 0) events.Add(new ScoreChanged(car, -deducted, point));
            }
            return true;
        }

        private void AwardWreck(ArcadeVehicle attacker)
        {
            events.Add(new WreckBonus(attacker, Tuning.wreckBonus));
            AddScore(attacker, Tuning.wreckBonus);
        }

        private void AddScore(ArcadeVehicle car, int points)
        {
            if (points == 0) return;
            car.score += points;
            events.Add(new ScoreChanged(car, points, car.position));
        }

        private void RespawnVehicle(ArcadeVehicle car)
        {
            Vector2 spawn = RespawnCorners.OrderByDescending(p => Cars.Where(c => c.alive)
                .Select(c => Vector2.Distance(p, c.position)).DefaultIfEmpty(10).Min()).First();
            car.Respawn(spawn, spawn.x > 0 ? 180 : 0);
        }

        // Ties: lower durability loses first, then the higher car index. Never random.
        public void EliminateLowest()
        {
            int count = Contenders;
            if (count <= 1) { EndRound(); return; }
            var loser = Cars.Where(c => !c.eliminated).OrderBy(c => c.score).ThenBy(c => c.health).ThenByDescending(c => c.index).First();
            loser.Eliminate(count, LifecycleRules);
            foreach (var rocket in Rockets.Where(r => r.active && r.owner == loser.index)) rocket.Hide();
            EliminationStage++;
            events.Add(new VehicleEliminated(loser, count));
            if (Contenders == 1) EndRound();
        }

        // A continue: an eliminated car rejoins (not after the match is over) with the lowest surviving score, so it
        // must beat at least one car to survive the next elimination, and respawns in the emptiest corner.
        public bool Revive(ArcadeVehicle car)
        {
            if (Finished || !car.eliminated) return false;
            float lowest = Cars.Where(c => !c.eliminated).Select(c => c.score).DefaultIfEmpty(0).Min();
            car.Rejoin(lowest);
            RespawnVehicle(car);
            events.Add(new VehicleRevived(car));
            return true;
        }

        private void EndRound()
        {
            var winner = Cars.First(c => !c.eliminated);
            Champion = winner.index;
            Survived = !winner.computer;
            if (!winner.alive) RespawnVehicle(winner);
            winner.FinalPlace = 1;
            foreach (var rocket in Rockets) rocket.Hide();
            Finished = true;
            events.Add(new MatchEnded(winner));
        }
    }
}
