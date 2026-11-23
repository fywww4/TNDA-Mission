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
        // Top speed 4.375 = 3.5 + 25% (2026-11-22); AI and cherry speeds scale from it.
        public float driveSpeed = 4.375f, accelerationPerSecond = 2f, coastDownPerSecond = 2.2f;
        public VehicleUpgrades playerUpgrades;
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
        public float contactBump = 1.4f, bumpFriction = 4.5f;
        // Ram knockback (ticket 18): slide speed per unit of closing speed for the rammed car, capped at ramBumpMax so
        // it stays in the arena; the rammer rebounds by rammerReboundPerSpeed per unit, capped at a third of the max.
        public float ramBumpPerSpeed = 1.6f, ramBumpMax = 6f, rammerReboundPerSpeed = .45f;
        // A side hit spins the victim from sideSpinMinDegrees (impact 1) to sideSpinMaxDegrees (sideSpinFullSpeed and
        // up) over sideSpinSeconds, stunned meanwhile.
        public float sideSpinMinDegrees = 180, sideSpinMaxDegrees = 360, sideSpinFullSpeed = 3.5f, sideSpinSeconds = .55f;
        // Scrape (擦撞): both driving forwards, headings within acos(scrapeAlignment) of each other (about 30°),
        // touching mostly side to side (the contact line's share along either heading below scrapeSideways), the
        // faster at scrapeMinSpeed or more. Both get scrapeBump outwards and keep scrapeSlowdown of their speed; harm
        // and points use scrapeMultiplier and their relative speed, from scrapeMinSpeed of it.
        public float scrapeAlignment = .87f, scrapeSideways = .6f, scrapeMinSpeed = 1;
        public float scrapeBump = 1.2f, scrapeSlowdown = .85f, scrapeMultiplier = .5f;
        // Driving into a prop at least this fast smashes it; a player ramming a car this fast splashes its debris.
        public float smashSpeed = 2.1f;
        public float fenceRespawnSeconds = 6;
        // Smashing a wooden fence scores this much (2026-11-21; crates 100, barrels 75).
        public int fencePoints = 50;
        public float rampSpeed = 2.1f, jumpSeconds = .7f, jumpHeight = .85f;
        // Spike balls (ticket 21): roll at spikeRollSpeed; a car driving into one loses spikeDamage of its durability and
        // knocks it away at spikeLaunchPerSpeed per unit of impact (up to spikeLaunchMax). From spikeFlyImpact the ball
        // flies, wrecking the next car it reaches, and slows by spikeFlyFriction per second back to a roll.
        public float spikeRollSpeed = .6f, spikeRadius = .28f, spikeDamage = .25f;
        public float spikeLaunchPerSpeed = 2f, spikeLaunchMax = 7f, spikeFlyImpact = 1f, spikeFlyFriction = 4f;
        public Vector2 arenaMin = new Vector2(-4.35f, -3.85f), arenaMax = new Vector2(4.35f, 1.65f);
        public bool randomPropSpots = true;
        // Challenge switches (default = the arcade match): crates give only rockets; rams earn no points or wreck bonus.
        public bool rocketCratesOnly = false, ramsScore = true;
        // 不被撞毀: a player's wreck ends the round at once as a loss, and a player still in at the final wins it.
        public bool wreckEndsRound = false;
        // Every computer car drives as a 追王 whose chases go only for players (and keep going for the one it rammed).
        public bool allAiHuntThePlayer = false;
        // Campaign switches (ticket 15). aiSparesPlayers: computer cars cannot hurt players (the tutorial steps).
        // wrecksKnockOut: a wrecked computer car is out for good instead of respawning; when only one car is left it wins.
        // seatTeams / seatPersonalities / seatNames: per seat (null = everyone alone / the usual personalities / P1..P8).
        // bossSeat: that seat is the boss (bossSize, bossArmor, bossMass; a rocket every bossRocketSeconds; cruises and
        // charges at those shares of top speed). Wrecking it wins the round for the players; the final at the end of
        // the schedule is a loss instead.
        public bool aiSparesPlayers = false, wrecksKnockOut = false;
        public int[] seatTeams;
        public AiPersonality[] seatPersonalities;
        public string[] seatNames;
        public int bossSeat = -1;
        // 2026-11-23 (the boss was too hard): armour 5 -> 2.5, mass 2.5 -> 1.25, a rocket every 7 -> 14 s.
        public float bossSize = 1.6f, bossArmor = 2.5f, bossMass = 1.25f, bossRocketSeconds = 14;
        // allyStrength: computer cars on a player's team take 1/allyStrength of the damage, ram with allyStrength times
        // the damage and weigh allyStrength (1 = an ordinary car; the boss stage uses 1.5).
        public float allyStrength = 1;
        public float bossCruiseSpeed = .8f, bossChargeSpeed = 1.25f;
        // Every challenge and campaign switch back to the arcade match.
        public void ResetChallengeSwitches()
        {
            rocketCratesOnly = false; ramsScore = true; wreckEndsRound = false; allAiHuntThePlayer = false;
            aiSparesPlayers = wrecksKnockOut = false;
            seatTeams = null; seatPersonalities = null; seatNames = null;
            bossSeat = -1;
            allyStrength = 1;
        }
        // Oil: the slick spins a car out for oilSlipSeconds, earns its dropper oilBonus, and dries after oilLifeSeconds.
        public float oilSlipSeconds = .8f, oilLifeSeconds = 12;
        public int oilBonus = 50;
        // 零件 (2026-11-21): a player's hard ram scatters partsPerRam pieces of the victim at up to partScatterSpeed,
        // sliding to a stop (partFriction). After partLandSeconds a player's car within partMagnetRadius pulls one in at
        // partMagnetSpeed and scores partPoints for it; a piece nobody takes is gone after partLifeSeconds.
        public int partsPerRam = 5, partPoints = 10;
        public float partScatterSpeed = 3f, partFriction = 5f, partLandSeconds = .45f, partMagnetRadius = 2.2f;
        public float partMagnetSpeed = 7f, partCollectRadius = .3f, partLifeSeconds = 8f;
    }

    // The elimination match rules: driving, props, crates, contacts, rockets, wrecks, timed eliminations and the
    // champion. It needs no scene objects besides the cars, and reports what happened as Events for an adapter to show.
    public sealed partial class DemolitionMatch : IItemArena, IAiArena
    {
        public readonly ArcadeVehicle[] Cars;
        public readonly BreakableProp[] Props;
        public readonly ArenaPickup[] Pickups;
        public readonly ArcadeProjectile[] Rockets;
        public readonly MatchTuning Tuning;
        public readonly ArenaObstacle[] Obstacles;

        private readonly List<MatchEvent> events = new List<MatchEvent>();
        private readonly Dictionary<(ArcadeVehicle, ArcadeVehicle), float> contactCooldowns = new Dictionary<(ArcadeVehicle, ArcadeVehicle), float>();
        private bool demo;
        private float bossRocketClock;

        public DemolitionMatch(ArcadeVehicle[] cars, BreakableProp[] props, ArenaPickup[] pickups, ArcadeProjectile[] rockets, MatchTuning tuning,
            ArenaObstacle[] obstacles = null, SpikeBall[] spikeBalls = null)
        {
            Cars = cars; Props = props; Pickups = pickups; Rockets = rockets; Tuning = tuning;
            Obstacles = obstacles ?? System.Array.Empty<ArenaObstacle>();
            SpikeBalls = spikeBalls ?? System.Array.Empty<SpikeBall>();
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
                return TwoRows(Mathf.Max(4, Cars.Length), min.x + .85f, max.x - .85f, max.y - .55f, min.y + .65f)
                    .Select(p => TerrainClear(p, .32f) ? p : ClearFloor(p, .32f)).ToArray();
            }
        }

        // Bottom row left to right facing up, then the top row right to left facing down.
        private (Vector2 spot, float heading)[] StartSpots()
        {
            Vector2 min = Tuning.arenaMin, max = Tuning.arenaMax;
            var spots = TwoRows(Cars.Length, min.x + 1.05f, max.x - 1.05f, min.y + .75f, max.y - .55f);
            int firstRow = (Cars.Length + 1) / 2;
            return spots.Select((spot, i) => (TerrainClear(spot, .32f) ? spot : ClearFloor(spot, .32f), i < firstRow ? 90f : -90f)).ToArray();
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
            foreach (var obstacle in Obstacles) { obstacle.broken = false; obstacle.respawn = 0; }
            var starts = StartSpots();
            bossRocketClock = 0;
            for (int i = 0; i < Cars.Length; i++)
            {
                var car = Cars[i];
                car.ResetVehicle(starts[i].spot, starts[i].heading, humans);
                bool boss = i == Tuning.bossSeat;
                car.team = Tuning.seatTeams != null && i < Tuning.seatTeams.Length ? Tuning.seatTeams[i] : 0;
                bool ally = !boss && car.computer && car.team > 0
                    && Enumerable.Range(0, Mathf.Min(humans, Tuning.seatTeams.Length)).Any(h => Tuning.seatTeams[h] == car.team);
                float allyScale = ally ? Tuning.allyStrength : 1;
                car.size = boss ? Tuning.bossSize : 1;
                car.armor = boss ? Tuning.bossArmor : allyScale;
                car.mass = boss ? Tuning.bossMass : allyScale;
                car.power = allyScale;
                car.nameTag = Tuning.seatNames != null && i < Tuning.seatNames.Length ? Tuning.seatNames[i] : null;
                AiPersonality? personality = Tuning.seatPersonalities != null && i < Tuning.seatPersonalities.Length
                    ? Tuning.seatPersonalities[i] : Tuning.allAiHuntThePlayer ? AiPersonality.Hunter : (AiPersonality?)null;
                car.Ai.BeginMatch(personality, cars: Cars.Length);
            }
            foreach (var pickup in Pickups)
            {
                if (!TerrainClear(pickup.point, .65f)) pickup.point = ClearFloor(pickup.point, .65f);
                pickup.available = true;
                pickup.respawn = 0;
            }
            foreach (var prop in Props)
            {
                prop.broken = false;
                prop.dented = false;
                prop.respawn = 0;
                if (Tuning.randomPropSpots) prop.point = RandomPropSpot(prop, prop.point);
                else if (!TerrainClear(prop.point, .65f)) prop.point = ClearFloor(prop.point, .65f);
            }
            foreach (var rocket in Rockets) rocket.Hide();
            ResetParts();
            ResetSpikeBalls();
            contactCooldowns.Clear();
        }

        public void Tick(float dt, ArcadeInput p1, ArcadeInput p2)
        {
            Elapsed += dt;
            RefreshSpawns(dt);
            ArmBoss(dt);
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
                if (input.throttle > .1f) ramp *= 1 + HumanBonus(car, VehicleAbility.Acceleration);
                float topSpeed = Tuning.driveSpeed * (car.computer ? AiTopSpeed(car) : 1) * (car.HasCherry ? Tuning.cherrySpeedMultiplier : 1);
                topSpeed *= 1 + HumanBonus(car, VehicleAbility.TopSpeed);
                Vector2 from = car.position;
                car.Drive(dt, input, topSpeed, ramp,
                    1 + HumanBonus(car, VehicleAbility.Handling));
                // Bumper-car slide from recent hits, fading out.
                car.AdvanceSlide(dt, Tuning.bumpFriction);
                if (!car.Airborne) ConstrainToArena(car);
                HitObstacles(car, from);
                HitProps(car);
                CollectPickup(car);
            }
            ResolveContacts(dt);
            MoveSpikeBalls(dt);
            MoveRockets(dt);
            UpdateSlicks(dt);
            MoveParts(dt);
            while (!demo && !Finished && Elapsed >= NextElimination) EliminateNextStep();
        }

        // Wrecked and eliminated cars keep spinning off screen in every phase until they leave the view.
        public void AdvanceSpinOuts(float dt, Vector2 viewCentre, float viewHalfSize)
        {
            foreach (var car in Cars) car.AdvanceSpinOut(dt, LifecycleRules, viewCentre, viewHalfSize);
        }

        private void RefreshSpawns(float dt)
        {
            foreach (var obstacle in Obstacles)
            {
                if (!obstacle.broken) continue;
                obstacle.respawn -= dt;
                if (obstacle.respawn <= 0 && Cars.All(c => !c.alive || !obstacle.Contains(c.position, .9f)))
                    obstacle.broken = false;
            }
            foreach (var prop in Props)
            {
                if (!prop.broken) continue;
                prop.respawn -= dt;
                if (prop.respawn > 0) continue;
                if (Tuning.randomPropSpots) prop.point = RandomPropSpot(prop, prop.point);
                if (SpawnClear(prop.point, .9f)) prop.broken = prop.dented = false;
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
                if (TerrainClear(spot, .65f) && SpawnClear(spot, 1.2f) && Pickups.All(p => Vector2.Distance(p.point, spot) > 1)
                    && Props.All(p => p == prop || Vector2.Distance(p.point, spot) > 1.4f))
                    return spot;
            }
            return TerrainClear(fallback, .65f) ? fallback : ClearFloor(fallback, .65f);
        }

        private bool TerrainClear(Vector2 point, float radius) => Obstacles.All(o => !o.Contains(point, radius));

        // A deterministic nearest free tile repairs authored spawn points or an unlucky random fallback. Broken
        // fences still reserve their footprint, so neither a crate nor a prop appears where a fence will regrow.
        private Vector2 ClearFloor(Vector2 from, float radius)
        {
            Vector2 best = from;
            float distance = float.PositiveInfinity;
            for (float y = Tuning.arenaMin.y + .6f; y < Tuning.arenaMax.y - .6f; y += .5f)
                for (float x = Tuning.arenaMin.x + .6f; x < Tuning.arenaMax.x - .6f; x += .5f)
                {
                    var spot = new Vector2(x, y);
                    float d = (spot - from).sqrMagnitude;
                    if (d >= distance || !TerrainClear(spot, radius) || !SpawnClear(spot, 1.2f)
                        || Pickups.Any(p => Vector2.Distance(p.point, spot) < 1)
                        || Props.Any(p => Vector2.Distance(p.point, spot) < 1)) continue;
                    best = spot; distance = d;
                }
            if (float.IsPositiveInfinity(distance)) throw new System.InvalidOperationException("No clear floor remains for an arena spawn.");
            return best;
        }

        private bool CanHarm(ArcadeVehicle attacker, ArcadeVehicle victim) =>
            VehicleHarmPolicy.CanHarm(attacker, victim, Elapsed, Tuning.aiGraceSeconds)
            && !(Tuning.aiSparesPlayers && attacker.computer && !victim.computer);

        // How much further than an ordinary car's a bigger car's footprint reaches (exactly 0 for an ordinary car, so
        // the arcade match's numbers are untouched).
        private static float Grow(ArcadeVehicle car) => .31f * (car.size - 1);

        // The boss gets a rocket every bossRocketSeconds it spends without one.
        private void ArmBoss(float dt)
        {
            if (Tuning.bossSeat < 0 || Tuning.bossSeat >= Cars.Length) return;
            var boss = Cars[Tuning.bossSeat];
            if (!boss.alive || boss.item != ArenaItem.None) { bossRocketClock = 0; return; }
            if ((bossRocketClock += dt) < Tuning.bossRocketSeconds) return;
            boss.item = ArenaItem.Rocket;
            bossRocketClock = 0;
        }

        IReadOnlyList<ArcadeVehicle> IAiArena.Cars => Cars;
        IReadOnlyList<BreakableProp> IAiArena.Props => Props;
        IReadOnlyList<ArenaPickup> IAiArena.Pickups => Pickups;
        IReadOnlyList<SpikeBall> IAiArena.SpikeBalls => SpikeBalls;
        float IAiArena.LeaderBias => Tuning.leaderBias;
        float IAiArena.HunterChaseSeconds => Tuning.hunterChaseSeconds;
        float IAiArena.HunterScatterSeconds => Tuning.hunterScatterSeconds;
        float IAiArena.HunterLeaderChance => Tuning.hunterLeaderChance;
        bool IAiArena.HuntersChasePlayersOnly => Tuning.allAiHuntThePlayer;
        Vector2 IAiArena.ArenaMin => Tuning.arenaMin;
        Vector2 IAiArena.ArenaMax => Tuning.arenaMax;

        // Late in the match (from the first elimination) the computer cars speed up; personalities stay the same.
        public float AiTopSpeed(ArcadeVehicle car) => car.Ai.Personality == AiPersonality.Boss
            ? car.Ai.Charging ? Tuning.bossChargeSpeed : Tuning.bossCruiseSpeed
            : (CarsKnockedOut > 0 ? Tuning.aiLateTopSpeed : Tuning.aiTopSpeed)
            * (car.Ai.Personality == AiPersonality.Wanderer ? Tuning.wandererTopSpeedScale : 1);
        bool IAiArena.CanHarm(ArcadeVehicle attacker, ArcadeVehicle victim) => CanHarm(attacker, victim);
        Vector2 IAiArena.Navigate(ArcadeVehicle driver, Vector2 destination) => driver.Airborne || Obstacles.Length == 0 ? destination
            : ObstacleNavigation.Next(driver.position, destination, Obstacles, Tuning.arenaMin, Tuning.arenaMax,
                Mathf.Abs(driver.speed) >= Tuning.smashSpeed);

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

        private void HitObstacles(ArcadeVehicle car, Vector2 from)
        {
            if (car.Airborne) return;
            foreach (var obstacle in Obstacles)
            {
                if (obstacle.broken) continue;
                if (obstacle.kind == ObstacleKind.Ramp)
                {
                    Vector2 a = obstacle.ToLocal(from), b = obstacle.ToLocal(car.position);
                    float lip = obstacle.size.y / 2;
                    if (a.y < lip && b.y >= lip && b.y > a.y && Mathf.Abs(car.speed) >= Tuning.rampSpeed)
                    {
                        float x = Mathf.Lerp(a.x, b.x, (lip - a.y) / (b.y - a.y));
                        if (Mathf.Abs(x) <= obstacle.size.x / 2) { car.Launch(Tuning.jumpSeconds, Tuning.jumpHeight, obstacle.height); return; }
                    }
                    continue;
                }
                if (!obstacle.Sweep(from, car.position, .32f, out _, out var contact, out var normal)) continue;
                if (obstacle.kind == ObstacleKind.Fence && Mathf.Abs(car.speed) >= Tuning.smashSpeed)
                {
                    obstacle.broken = true;
                    obstacle.respawn = Tuning.fenceRespawnSeconds;
                    car.speed *= .85f;
                    events.Add(new FenceSmashed(car, obstacle, Tuning.fencePoints));
                    AddScore(car, Tuning.fencePoints);
                    continue;
                }
                car.position = contact;
                car.ReboundSlide(Mathf.Abs(normal.x) > .5f, Mathf.Abs(normal.y) > .5f);
                car.RecordCollision();
                if (Mathf.Abs(car.speed) > 1) events.Add(new WallHit(car));
                car.speed = 0;
                car.stun = .12f;
            }
        }

        private void HitProps(ArcadeVehicle car)
        {
            if (car.Airborne) return;
            foreach (var prop in Props)
            {
                if (prop.broken || Vector2.Distance(car.position, prop.point) > .6f + Grow(car)) continue;
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
                    prop.dented = true;
                    car.RecordCollision();
                }
            }
        }

        private void CollectPickup(ArcadeVehicle car)
        {
            if (car.Airborne) return;
            if (car.item != ArenaItem.None) return;
            foreach (var pickup in Pickups)
            {
                if (!pickup.available || Vector2.Distance(car.position, pickup.point) > .58f + Grow(car)) continue;
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
                var victim = Cars.FirstOrDefault(c => c.alive && !c.Airborne && !c.IsSlipping && !(c == dropper && slick.age < 1)
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
            bool wrecked = ApplyDamage(victim, 1, ignoresStrength: true) && !victim.alive;
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
                // Ordinary cars touch within 0.62 (exactly as before sizes existed); a bigger car reaches further.
                float grow = Grow(a) + Grow(b);
                if (!a.alive || !b.alive || a.Airborne || b.Airborne || Vector2.Distance(a.position, b.position) >= .62f + grow) continue;
                Vector2 separation = (a.position - b.position).normalized;
                if (separation.sqrMagnitude < .001f) separation = Vector2.right;
                float overlap = .63f + grow - Vector2.Distance(a.position, b.position);
                // The lighter car gives way more (half each for equal cars).
                float aShare = b.mass / (a.mass + b.mass);
                a.position += separation * overlap * aShare;
                b.position -= separation * overlap * (1 - aShare);
                if (contactCooldowns[pair] <= 0)
                {
                    // Every new touch bounces both cars apart a little; a real ram adds more in ResolveImpact.
                    a.ApplySlide(separation * Tuning.contactBump * 2 * aShare);
                    b.ApplySlide(-separation * Tuning.contactBump * 2 * (1 - aShare));
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
            if (TryScrape(a, b)) return;
            var attacker = approachA >= approachB ? a : b;
            var victim = attacker == a ? b : a;
            float impact = Mathf.Max(0, Mathf.Max(approachA, approachB));
            if (impact < 1) return;
            float alignment = Vector2.Dot(attacker.Forward, victim.Forward);
            float rear = Vector2.Dot((attacker.position - victim.position).normalized, victim.Forward);
            var kind = Mathf.Abs(alignment) < .5f ? ImpactKind.Side : alignment > .6f && rear < -.25f ? ImpactKind.Rear : ImpactKind.Front;
            float multiplier = kind == ImpactKind.Side ? 2 : kind == ImpactKind.Rear ? 1.5f : 1;
            bool damaged = Strike(attacker, victim, impact, multiplier);
            attacker.speed *= .6f;
            if (damaged && victim.alive) { victim.speed *= .4f; victim.stun = .12f; }
            // Bumper cars: the rammed car slides away and its nose is knocked aside; the rammer rebounds.
            Vector2 push = (victim.position - attacker.position).normalized;
            float closing = Mathf.Max(0, approachA + approachB);
            // A heavier rammer sends the victim further and rebounds less (equal cars: as before).
            float weight = attacker.mass / victim.mass;
            victim.ApplySlide(push * Mathf.Min(Tuning.ramBumpPerSpeed * closing * weight, Tuning.ramBumpMax));
            attacker.ApplySlide(-push * Mathf.Min(Tuning.rammerReboundPerSpeed * closing / weight, Tuning.ramBumpMax / 3));
            // Only a victim the ram could hurt spins (not a shielded, protected or grace-period car).
            if (kind == ImpactKind.Side && damaged) SpinVictim(attacker, victim, impact);
            else victim.heading += Random.Range(15f, 30f) * (Random.value < .5f ? -1 : 1);
            events.Add(new CarRammed(attacker, victim, impact, multiplier, damaged && victim.alive, kind, damaged && !victim.alive));
            if (damaged && (!victim.alive || impact >= Tuning.smashSpeed)) SplashDebris(attacker, victim);
        }

        // The ram's harm: damage by impact and multiplier (none where the harm rules protect the victim), the
        // rammer's points and wreck bonus, and a little damage back to the rammer. True when the victim was hurt.
        private bool Strike(ArcadeVehicle attacker, ArcadeVehicle victim, float impact, float multiplier)
        {
            float damage = (.035f + impact * .035f) * multiplier * attacker.power;
            if (!CanHarm(attacker, victim)) damage = 0;
            bool damaged = ApplyDamage(victim, damage);
            if (damaged && Tuning.ramsScore)
            {
                AddScore(attacker, Mathf.RoundToInt(impact * 30 * multiplier));
                if (!victim.alive) AwardWreck(attacker);
            }
            ApplyDamage(attacker, damage * .15f);
            return damaged;
        }

        // Scrape (擦撞): two cars driving forwards the same way touch side to side with one of them really moving.
        // Both are pushed apart and slowed a little. The harm goes by their relative speed, so cars cruising side by
        // side at the same pace only rub: the faster one scrapes the other for scrapeMultiplier of a ram's harm once
        // the relative speed reaches scrapeMinSpeed.
        private bool TryScrape(ArcadeVehicle a, ArcadeVehicle b)
        {
            Vector2 apart = (b.position - a.position).normalized;
            if (a.speed < 0 || b.speed < 0 || Vector2.Dot(a.Forward, b.Forward) < Tuning.scrapeAlignment
                || Mathf.Abs(Vector2.Dot(apart, a.Forward)) >= Tuning.scrapeSideways
                || Mathf.Abs(Vector2.Dot(apart, b.Forward)) >= Tuning.scrapeSideways
                || Mathf.Max(a.speed, b.speed) < Tuning.scrapeMinSpeed) return false;
            var attacker = a.speed >= b.speed ? a : b;
            var victim = attacker == a ? b : a;
            float impact = (a.Forward * a.speed - b.Forward * b.speed).magnitude;
            bool damaged = impact >= Tuning.scrapeMinSpeed && Strike(attacker, victim, impact, Tuning.scrapeMultiplier);
            a.ApplySlide(-apart * Tuning.scrapeBump);
            b.ApplySlide(apart * Tuning.scrapeBump);
            a.speed *= Tuning.scrapeSlowdown;
            b.speed *= Tuning.scrapeSlowdown;
            events.Add(new CarRammed(attacker, victim, impact, Tuning.scrapeMultiplier, damaged && victim.alive, ImpactKind.Scrape, damaged && !victim.alive));
            if (damaged && !victim.alive) SplashDebris(attacker, victim);
            return true;
        }

        // A side hit turns the victim on the spot, half a turn for a slow hit up to a full turn at sideSpinFullSpeed,
        // the way the rammer's nose pushes its flank (either way when hit dead centre).
        private void SpinVictim(ArcadeVehicle attacker, ArcadeVehicle victim, float impact)
        {
            float degrees = Mathf.Lerp(Tuning.sideSpinMinDegrees, Tuning.sideSpinMaxDegrees,
                Mathf.InverseLerp(1, Tuning.sideSpinFullSpeed, impact));
            Vector2 offset = attacker.position - victim.position, shove = attacker.Forward;
            float torque = offset.x * shove.y - offset.y * shove.x;
            float sign = Mathf.Abs(torque) > .01f ? -Mathf.Sign(torque) : Random.value < .5f ? -1 : 1;
            victim.SpinFromHit(sign * degrees, Tuning.sideSpinSeconds);
        }

        // Only a player's own rams throw the victim's parts across the floor; computer rams never do.
        private void SplashDebris(ArcadeVehicle rammer, ArcadeVehicle victim)
        {
            if (rammer.computer) return;
            events.Add(new DebrisSplashed(rammer, victim));
            ScatterParts(victim);
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
                    .Where(c => SegmentDistance(c.position, from, rocket.position) < .42f + Grow(c))
                    .OrderBy(c => Vector2.Distance(from, c.position)).FirstOrDefault();
                if (victim != null)
                {
                    if (ApplyDamage(victim, victim.HasCherry ? 1 : .38f, ignoresStrength: victim.HasCherry))
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

        private float HumanBonus(ArcadeVehicle car, VehicleAbility ability) =>
            car.computer || Tuning.playerUpgrades == null ? 0 : Tuning.playerUpgrades.Bonus(ability);

        public bool ApplyDamage(ArcadeVehicle car, float damage, bool ignoresStrength = false)
        {
            // Cherry contact is an instant wreck, while ordinary rams and rockets use durability damage.
            // Cheat menu: players take no damage at all (the clock can still eliminate them).
            if (Cheats.Invincible && !car.computer && !demo) return false;
            if (!ignoresStrength) damage *= 1 - HumanBonus(car, VehicleAbility.Strength);
            // Armour (the campaign boss) cuts every hit, a cherry's too.
            damage /= Mathf.Max(.01f, car.armor);
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
                if (!demo && !Finished && car.index == Tuning.bossSeat) EndBossRound(true);
                else if (!demo && !Finished && Tuning.wrecksKnockOut && car.computer) KnockOut(car);
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
            // A boss stage that reaches its final has run out of time: the boss wins.
            if (final && Tuning.bossSeat >= 0 && Tuning.bossSeat < Cars.Length) { EndBossRound(false); return; }
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

        // Cheat menu: points for a player straight away, shown like any other score change.
        public void GrantScore(ArcadeVehicle car, int points)
        {
            if (demo || Finished || car == null) return;
            AddScore(car, points);
        }

        // Cheat menu: every computer car that is not on a player's team is knocked out at once. A boss stage is won;
        // a match left with one car crowns it (two players keep playing for the crown).
        public int KnockOutEnemies()
        {
            if (demo || Finished) return 0;
            var humans = Cars.Where(c => !c.computer).ToList();
            var enemies = Cars.Where(c => c.computer && !c.eliminated && !humans.Any(h => VehicleHarmPolicy.Teammates(h, c)))
                .ToList();
            int count = 0;
            foreach (var car in enemies)
            {
                if (Finished) break;
                KnockOut(car);
                count++;
            }
            if (!Finished && Tuning.bossSeat >= 0 && Tuning.bossSeat < Cars.Length) EndBossRound(true);
            return count;
        }

        // wrecksKnockOut: a wrecked computer car is out for good in the place it reached; the last car left wins.
        private void KnockOut(ArcadeVehicle car)
        {
            int place = Contenders;
            car.Eliminate(place, LifecycleRules);
            foreach (var rocket in Rockets.Where(r => r.active && r.owner == car.index)) rocket.Hide();
            CarsKnockedOut++;
            events.Add(new VehicleEliminated(car, place, wrecked: true));
            if (Contenders == 1) EndRound();
        }

        // A boss stage ends: won (the boss was wrecked) crowns the player, lost (time ran out) crowns the boss. The rest
        // still in take the places below by score.
        private void EndBossRound(bool won)
        {
            var winner = won ? Cars.FirstOrDefault(c => !c.computer) ?? Cars[0] : Cars[Tuning.bossSeat];
            int place = 2;
            foreach (var car in Cars.Where(c => c != winner && !c.eliminated).OrderByDescending(c => c.score).ThenBy(c => c.index))
                car.FinalPlace = place++;
            Survived = won;
            if (!winner.alive && !winner.eliminated) RespawnVehicle(winner);
            Crown(winner);
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
            ResetParts();
            Finished = true;
            events.Add(new MatchEnded(winner));
        }
    }
}
