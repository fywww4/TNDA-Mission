using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TNDA.Prototype
{
    // One timed elimination: at this many seconds, the lowest-scoring cars still in are knocked out, this many of them.
    [System.Serializable]
    public struct EliminationStep
    {
        public float at;
        public int count;
        public EliminationStep(float at, int count) { this.at = at; this.count = count; }
    }

    // Gameplay values the match rules read. Defaults are the prototype values; DemolitionGame copies its Inspector
    // tuning in before every reset and tick.
    public sealed class MatchTuning
    {
        // The timed eliminations in order. The last step is the final: it ends the round, knocking out every car but
        // the champion whatever its count says (more cars than that when continues brought some back). Four cars: 45 s, 80 s, then the final two at 120 s.
        public EliminationStep[] eliminations = DefaultEliminations();
        public static EliminationStep[] DefaultEliminations() =>
            new[] { new EliminationStep(45, 1), new EliminationStep(80, 1), new EliminationStep(120, 1) };
        public float RoundSeconds => eliminations[eliminations.Length - 1].at;
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
        // Driving into a prop at least this fast smashes it; a player ramming a car this fast splashes its debris.
        public float smashSpeed = 2.1f;
        public Vector2 arenaMin = new Vector2(-4.35f, -3.85f), arenaMax = new Vector2(4.35f, 1.65f);
        public bool randomPropSpots = true;
        // Challenge switches (default = the arcade match): crates give only rockets; rams earn no points or wreck bonus.
        public bool rocketCratesOnly = false, ramsScore = true;
        // 不被撞毀: a player's wreck ends the round at once as a loss, and a player still in at the final wins it.
        public bool wreckEndsRound = false;
        // Every computer car drives as a 追王 whose chases go only for players (and keep going for the one it rammed).
        public bool allAiHuntThePlayer = false;
        // Every challenge switch back to the arcade match.
        public void ResetChallengeSwitches()
        {
            rocketCratesOnly = false; ramsScore = true; wreckEndsRound = false; allAiHuntThePlayer = false;
        }
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
        // Cars knocked out so far (by the clock or by the host).
        public int CarsKnockedOut { get; private set; }
        // Schedule steps already run.
        private int stepsDone;
        private EliminationStep CurrentStep => Tuning.eliminations[Mathf.Min(stepsDone, Tuning.eliminations.Length - 1)];
        public bool Finished { get; private set; }
        public bool Survived { get; private set; }
        public int Wrecks { get; private set; }
        public int Champion { get; private set; } = -1;
        public float NextElimination => CurrentStep.at;
        public int Contenders => Cars.Count(c => !c.eliminated);

        // Respawn spots and starting spots sit a fixed distance in from the arena edges, so a bigger arena (the Mode 7
        // scene) keeps them in its corners. Both are two rows spread evenly between the corners, one spot per car (and at
        // least the four corners for respawns). Four cars with the default arena: respawns at (±3.5, 1.1)/(±3.5, -3.2),
        // starts at (±3.3, -3.1) facing up and (±3.3, 1.1) facing down.
        private Vector2[] RespawnSpots
        {
            get
            {
                Vector2 min = Tuning.arenaMin, max = Tuning.arenaMax;
                // Top row left to right, then the bottom row right to left.
                return TwoRows(Mathf.Max(4, Cars.Length), min.x + .85f, max.x - .85f, max.y - .55f, min.y + .65f);
            }
        }

        // Bottom row left to right facing up, then the top row right to left facing down.
        private (Vector2 spot, float heading)[] StartSpots()
        {
            Vector2 min = Tuning.arenaMin, max = Tuning.arenaMax;
            var spots = TwoRows(Cars.Length, min.x + 1.05f, max.x - 1.05f, min.y + .75f, max.y - .55f);
            int firstRow = (Cars.Length + 1) / 2;
            return spots.Select((spot, i) => (spot, i < firstRow ? 90f : -90f)).ToArray();
        }

        // count spots: the first half (rounded up) on the first row from left to right, the rest on the second row from
        // right to left, each row spread evenly from left to right edge.
        private static Vector2[] TwoRows(int count, float left, float right, float firstY, float secondY)
        {
            int first = (count + 1) / 2, second = count - first;
            var spots = new Vector2[count];
            for (int i = 0; i < first; i++) spots[i] = new Vector2(Spread(left, right, i, first), firstY);
            for (int i = 0; i < second; i++) spots[first + i] = new Vector2(Spread(right, left, i, second), secondY);
            return spots;
        }

        // The row's ends land exactly on the edge offsets.
        private static float Spread(float from, float to, int i, int count) =>
            i == 0 || count <= 1 ? from : i == count - 1 ? to : Mathf.Lerp(from, to, i / (float)(count - 1));

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
            Wrecks = CarsKnockedOut = stepsDone = 0;
            Champion = -1;
            events.Clear();
            var starts = StartSpots();
            for (int i = 0; i < Cars.Length; i++)
            {
                Cars[i].ResetVehicle(starts[i].spot, starts[i].heading, humans);
                Cars[i].Ai.BeginMatch(Tuning.allAiHuntThePlayer ? AiPersonality.Hunter : (AiPersonality?)null, cars: Cars.Length);
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
            while (!demo && !Finished && Elapsed >= NextElimination) EliminateNextStep();
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
        bool IAiArena.HuntersChasePlayersOnly => Tuning.allAiHuntThePlayer;
        Vector2 IAiArena.ArenaMin => Tuning.arenaMin;
        Vector2 IAiArena.ArenaMax => Tuning.arenaMax;

        // Late in the match (from the first elimination) the computer cars speed up; personalities stay the same.
        public float AiTopSpeed(ArcadeVehicle car) => (CarsKnockedOut > 0 ? Tuning.aiLateTopSpeed : Tuning.aiTopSpeed)
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
                if (Mathf.Abs(car.speed) >= Tuning.smashSpeed)
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
                    : Tuning.rocketCratesOnly ? ArenaItem.Rocket
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

        // ram: how fast the attacker was driving into the victim. A cherry car that was only touched still wrecks the
        // other car, but throws no debris: only a real ram (1 or more, as for an ordinary ram) does.
        private void CherryWreck(ArcadeVehicle attacker, ArcadeVehicle victim, float ram)
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
            if (wrecked && ram >= 1) SplashDebris(attacker, victim);
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
                if (otherRam >= 1 && otherRam > cherryRam) CherryWreck(other, cherry, otherRam);
                else CherryWreck(cherry, other, cherryRam);
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
            if (damaged && Tuning.ramsScore)
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
            if (damaged && (!victim.alive || impact >= Tuning.smashSpeed)) SplashDebris(attacker, victim);
        }

        // Only a player's own rams throw debris, onto that player's screen; computer rams never do.
        private void SplashDebris(ArcadeVehicle rammer, ArcadeVehicle victim)
        {
            if (!rammer.computer) events.Add(new DebrisSplashed(rammer, victim));
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
                if (Tuning.wreckEndsRound && !car.computer && !demo && !Finished) EndRoundOnWreck(car);
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
            Vector2 spawn = RespawnSpots.OrderByDescending(p => Cars.Where(c => c.alive)
                .Select(c => Vector2.Distance(p, c.position)).DefaultIfEmpty(10).Min()).First();
            car.Respawn(spawn, spawn.x > 0 ? 180 : 0);
        }

        // Runs the next schedule step now: knocks out its count of cars, or everyone but the champion in the final. The
        // clock calls it on time; a demo match's host calls it at its own pace.
        public void EliminateNextStep()
        {
            bool final = stepsDone >= Tuning.eliminations.Length - 1;
            int count = CurrentStep.count;
            stepsDone++;
            for (int i = 0; (final || i < count) && !Finished; i++) EliminateLowest();
        }

        // Ties: lower durability loses first, then the higher car index. Never random. In 不被撞毀 the computer cars go
        // first whatever their score, so a player who lasts wins the final.
        public void EliminateLowest()
        {
            int count = Contenders;
            if (count <= 1) { EndRound(); return; }
            var loser = Cars.Where(c => !c.eliminated).OrderBy(c => Tuning.wreckEndsRound && !c.computer).ThenBy(c => c.score).ThenBy(c => c.health).ThenByDescending(c => c.index).First();
            loser.Eliminate(count, LifecycleRules);
            foreach (var rocket in Rockets.Where(r => r.active && r.owner == loser.index)) rocket.Hide();
            CarsKnockedOut++;
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
            Survived = !winner.computer;
            if (!winner.alive) RespawnVehicle(winner);
            Crown(winner);
        }

        // 不被撞毀: the wrecked player comes last; the cars still in take the places above by score (the elimination
        // tie order), and the first of them is the champion.
        private void EndRoundOnWreck(ArcadeVehicle wrecked)
        {
            var others = Cars.Where(c => !c.eliminated && c != wrecked)
                .OrderByDescending(c => c.score).ThenByDescending(c => c.health).ThenBy(c => c.index).ToList();
            wrecked.FinalPlace = others.Count + 1;
            for (int i = 1; i < others.Count; i++) others[i].FinalPlace = i + 1;
            Survived = false;
            Crown(others[0]);
        }

        private void Crown(ArcadeVehicle winner)
        {
            Champion = winner.index;
            winner.FinalPlace = 1;
            foreach (var rocket in Rockets) rocket.Hide();
            Finished = true;
            events.Add(new MatchEnded(winner));
        }
    }
}
