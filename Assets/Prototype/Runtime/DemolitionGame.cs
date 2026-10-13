using System;
using System.Linq;
using UnityEngine;
using TMPro;

namespace TNDA.Prototype
{
    [Serializable]
    public class BreakableProp
    {
        public Transform view;
        public Vector2 point;
        public int points = 100;
        public float respawn;
        public bool broken;
    }

    [Serializable]
    public class ArenaPickup
    {
        public Transform view;
        public Vector2 point;
        [Tooltip("1 = shield; 2 = rocket.")]
        public int item = 1;
        public float respawn;
        public bool available;
    }

    public class DemolitionGame : ArcadeGame
    {
        public ArcadeVehicle[] cars;
        public BreakableProp[] props;
        public ArenaPickup[] pickups = Array.Empty<ArenaPickup>();
        public ArcadeProjectile[] rockets = Array.Empty<ArcadeProjectile>();
        public TMP_Text arenaBoard, leaderboard;
        public TMP_Text[] carLabels;
        public Transform[] healthBars, shieldViews;

        [Header("Elimination arena tuning")]
        public float firstElimination = 60, secondElimination = 90, roundSeconds = 120;
        public float driveSpeed = 2.8f;
        [Tooltip("Speed gained per second while gas is held (reaches top speed in about 2s).")]
        public float accelerationPerSecond = 1.4f;
        [Tooltip("Speed lost per second after gas is released.")]
        public float coastDownPerSecond = 2.2f;
        public float enemyRespawnSeconds = 3;
        public int wreckPenalty = 250, wreckBonus = 300;
        public float shieldDuration = 6, pickupRespawnSeconds = 8;

        [Header("AI aggression")]
        [Tooltip("Seconds after GO during which AI only targets other AI, steers around players and cannot damage them.")]
        public float aiGraceSeconds = 10;
        [Tooltip("How much AI prefers high-scoring targets over nearby ones (0 = nearest only).")]
        public float leaderBias = 1.5f;
        [Tooltip("A car this many points ahead of an AI is hunted on purpose.")]
        public float leaderHuntMargin = 300;

        [Header("Cherry: crate item only the last-place car can roll")]
        [Tooltip("While active the car is faster and wrecks any car it drives into, but any ram or rocket wrecks it.")]
        public float cherryDuration = 8;
        public float cherrySpeedMultiplier = 1.35f;
        [Range(0, 1)] public float trailingCherryChance = .5f;
        public SpriteRenderer[] cherryViews;

        [Header("Rocket explosion")]
        public SpriteRenderer[] explosions;
        public float explosionSeconds = .45f;
        private readonly float[] explosionAge = { -1, -1, -1, -1 };
        private MaterialPropertyBlock solidBlock;
        private readonly float[] cherrySeconds = new float[4];

        [Header("Elimination spin-out")]
        [Tooltip("Eliminated cars spin and skid off the screen instead of vanishing.")]
        public float spinOutTurnsPerSecond = 4;
        public float spinOutStartSpeed = 4, spinOutAcceleration = 14;
        private readonly float[] spinOutSpeed = new float[4];

        [Header("Start messages")]
        [Tooltip("Shown one after another in the centre right after GO.")]
        public string[] startMessages = { "活下來！", "盡量取得最高分！" };
        public float startMessageSeconds = 1.5f;
        private float countdownBaseSize;
        private bool showingStartMessage;

        [Header("Bumper-car knockback")]
        [Tooltip("Sliding push given to cars that touch, so they bounce apart instead of sticking.")]
        public float contactBump = 1.4f;
        [Tooltip("Extra push for the car that gets rammed, scaled by impact speed.")]
        public float ramBump = 2.4f;
        [Tooltip("How fast the sliding push fades (per second).")]
        public float bumpFriction = 4.5f;
        private readonly Vector2[] knock = new Vector2[4];
        private readonly Vector2[] spinOutDirection = new Vector2[4];
        public bool HasCherry(ArcadeVehicle car) => car.alive && cherrySeconds[car.index] > 0;
        public Vector2 arenaMin = new Vector2(-4.35f, -3.85f);
        public Vector2 arenaMax = new Vector2(4.35f, 1.65f);
        public AudioClip engineClip;

        private readonly ArcadeEngineAudio engine = new ArcadeEngineAudio();
        private readonly float[,] contactCooldowns = new float[4, 4];
        private readonly float[] aiThink = new float[4];
        private readonly Vector2[] aiDestination = new Vector2[4];
        private readonly int[] aiTarget = new int[4];
        private readonly int[] finalPlaces = new int[4];
        private int eliminationStage;

        public float Remaining => Mathf.Max(0, roundSeconds - Elapsed);
        public float NextElimination => eliminationStage == 0 ? firstElimination : eliminationStage == 1 ? secondElimination : roundSeconds;
        public int Contenders => cars.Count(c => !c.eliminated);
        public int LivePlayers => cars.Count(c => !c.computer && c.alive);
        public bool Survived { get; private set; }
        public int Wrecks { get; private set; }
        public int Champion { get; private set; } = -1;

        [Header("Attract mode (title screen demo)")]
        public GameObject attractScreen;
        public TMP_Text attractPrompt;
        [Tooltip("The demo eliminates last place at this time (to show the spin-out) and restarts at demoLoopSeconds.")]
        public float demoEliminationSeconds = 20, demoLoopSeconds = 30;
        private bool demo, startingDemo;

        // Title screen: four AI cars play a silent demo match behind the coin prompt.
        protected override void TickIdle(float dt)
        {
            if (Phase != ArcadePhase.Attract) return;
            if (!demo || Elapsed >= demoLoopSeconds) StartDemo();
            Elapsed += dt;
            Tick(dt, new ArcadeInput(), new ArcadeInput());
            if (eliminationStage == 0 && Elapsed >= demoEliminationSeconds) EliminateLowest();
        }

        private void StartDemo()
        {
            startingDemo = true;
            Elapsed = 0;
            ResetMatch();
            startingDemo = false;
        }

        protected override void ResetMatch()
        {
            demo = startingDemo;
            Survived = false;
            Wrecks = eliminationStage = 0;
            Champion = -1;
            Vector2[] starts = { new Vector2(-3.3f, -3.1f), new Vector2(3.3f, -3.1f), new Vector2(3.3f, 1.1f), new Vector2(-3.3f, 1.1f) };
            for (int i = 0; i < cars.Length; i++)
            {
                cars[i].ResetVehicle(starts[i], i < 2 ? 90 : -90, startingDemo ? 0 : PlayerCount);
                cars[i].spawnProtection = 2;
                aiThink[i] = .2f + i * .15f;
                aiTarget[i] = -1;
                aiDestination[i] = new Vector2(i % 2 == 0 ? -1.8f : 1.8f, -.6f);
                finalPlaces[i] = 0;
                if (healthBars != null && i < healthBars.Length && healthBars[i] != null)
                    healthBars[i].GetComponentInChildren<SpriteRenderer>().color = cars[i].body.color;
            }
            foreach (var prop in props)
            {
                prop.broken = false;
                prop.respawn = 0;
                prop.view.gameObject.SetActive(true);
            }
            foreach (var pickup in pickups)
            {
                pickup.available = true;
                pickup.respawn = 0;
                pickup.view.gameObject.SetActive(true);
            }
            foreach (var rocket in rockets) rocket.Hide();
            Array.Clear(contactCooldowns, 0, contactCooldowns.Length);
            Array.Clear(cherrySeconds, 0, cherrySeconds.Length);
            Array.Clear(spinOutSpeed, 0, spinOutSpeed.Length);
            Array.Clear(knock, 0, knock.Length);
            for (int i = 0; i < explosionAge.Length; i++) explosionAge[i] = -1;
            if (explosions != null) foreach (var blast in explosions) if (blast != null) blast.gameObject.SetActive(false);
        }

        protected override void Tick(float dt, ArcadeInput p1, ArcadeInput p2)
        {
            RefreshSpawns(dt);
            foreach (var car in cars)
            {
                if (car.eliminated) continue;
                if (!car.alive)
                {
                    car.respawn -= dt;
                    if (car.respawn <= 0) RespawnVehicle(car);
                    continue;
                }
                car.shieldSeconds = Mathf.Max(0, car.shieldSeconds - dt);
                cherrySeconds[car.index] = Mathf.Max(0, cherrySeconds[car.index] - dt);
                car.spawnProtection = Mathf.Max(0, car.spawnProtection - dt);
                ArcadeInput input = car.computer ? ComputerInput(car, dt) : car.index == 0 ? p1 : p2;
                // The item button only fires a held item; a human item requires a fresh press.
                if (car.item != 0 && car.cooldown <= 0 && (car.computer ? input.action : input.actionPressed)) UseAction(car);
                // Holding gas builds speed gradually; releasing it coasts down a little faster.
                float ramp = input.throttle > .1f ? accelerationPerSecond : coastDownPerSecond;
                float topSpeed = driveSpeed * (car.computer ? .87f : 1) * (HasCherry(car) ? cherrySpeedMultiplier : 1);
                car.Drive(dt, input, topSpeed, ramp);
                // Bumper-car slide from recent hits, fading out.
                car.position += knock[car.index] * dt;
                knock[car.index] *= Mathf.Exp(-bumpFriction * dt);
                ConstrainToArena(car);
                HitProps(car);
                CollectPickup(car);
                car.Sync();
            }
            ResolveContacts(dt);
            MoveRockets(dt);
            while (Phase == ArcadePhase.Playing && Elapsed >= NextElimination) EliminateLowest();
        }

        private void RefreshSpawns(float dt)
        {
            foreach (var prop in props)
            {
                if (!prop.broken) continue;
                prop.respawn -= dt;
                if (prop.respawn <= 0 && SpawnClear(prop.point, .9f))
                {
                    prop.broken = false;
                    prop.view.gameObject.SetActive(true);
                }
            }
            foreach (var pickup in pickups)
            {
                if (pickup.available) continue;
                pickup.respawn -= dt;
                if (pickup.respawn <= 0 && SpawnClear(pickup.point, .8f))
                {
                    pickup.available = true;
                    pickup.view.gameObject.SetActive(true);
                }
            }
        }

        private bool SpawnClear(Vector2 point, float radius) => cars.All(c => !c.alive || Vector2.Distance(c.position, point) > radius);

        public bool AiGrace => Elapsed < aiGraceSeconds;
        // During the opening grace period AI cannot hurt players by ramming, rockets or cherry.
        public bool CanAiHurt(ArcadeVehicle victim) => victim.computer || !AiGrace;

        // Which car an AI wants to ram: high scores first, nearer is better. During the grace period only other AI.
        private ArcadeVehicle PickRival(ArcadeVehicle car)
        {
            float top = cars.Max(c => c.score) + 1;
            return cars.Where(c => c.alive && !c.eliminated && c != car && CanAiHurt(c))
                .OrderByDescending(c => (1 + leaderBias * c.score / top) / (1 + Vector2.Distance(c.position, car.position) * .35f)
                    * UnityEngine.Random.Range(.85f, 1.15f))
                .FirstOrDefault();
        }

        // AI competes for props/items and rams rivals, preferring the leaders. Players are spared early on.
        private ArcadeInput ComputerInput(ArcadeVehicle car, float dt)
        {
            int i = car.index;
            aiThink[i] -= dt;
            if (aiThink[i] <= 0 || Vector2.Distance(car.position, aiDestination[i]) < .4f
                || (aiTarget[i] >= 0 && !cars[aiTarget[i]].alive)) ChooseDestination(car);
            if (HasCherry(car) && (aiTarget[i] < 0 || !CanAiHurt(cars[aiTarget[i]])))
            {
                var prey = PickRival(car);
                if (prey != null && CanAiHurt(prey)) aiTarget[i] = prey.index;
            }
            if (aiTarget[i] >= 0) aiDestination[i] = cars[aiTarget[i]].position;
            Vector2 delta = aiDestination[i] - car.position;
            float desired = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            var hunter = HasCherry(car) ? null : cars.FirstOrDefault(c => c != car && HasCherry(c)
                && Vector2.Distance(c.position, car.position) < 2.2f);
            if (hunter != null)
            {
                Vector2 flee = car.position - hunter.position;
                if (flee.sqrMagnitude < .01f) flee = Vector2.right;
                desired = Mathf.Atan2(flee.y, flee.x) * Mathf.Rad2Deg;
            }
            // Players are steered around unless they are this AI's target after the grace period.
            foreach (var human in cars.Where(c => !c.computer && c.alive && !(c.index == aiTarget[i] && CanAiHurt(c))))
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
            bool humansClear = !AiGrace || cars.All(c => c.computer || !c.alive || Vector2.Distance(c.position, car.position) > 1.8f);
            bool rivalAhead = aiTarget[i] >= 0 && delta.magnitude < 2.7f && Mathf.Abs(error) < 22;
            bool hasPropAhead = props.Any(p => !p.broken && Vector2.Distance(p.point, car.position) < 1.6f
                && Vector2.Dot(car.Forward, (p.point - car.position).normalized) > .8f);
            bool rivalNear = cars.Any(c => CanAiHurt(c) && c.alive && c != car && Vector2.Distance(c.position, car.position) < 3);
            bool activate = car.item == 1 ? car.health < .75f || rivalAhead
                : car.item == 2 ? rivalAhead || hasPropAhead
                : car.item == 3 && rivalNear;
            return new ArcadeInput {
                steer = -Mathf.Clamp(error / 38, -1, 1),
                throttle = Mathf.Abs(error) > 65 ? .35f : 1,
                action = humansClear && activate
            };
        }

        private void ChooseDestination(ArcadeVehicle car)
        {
            int i = car.index;
            aiThink[i] = 2.2f + i * .35f;
            aiTarget[i] = -1;
            var rival = PickRival(car);
            // A car well ahead of this AI is hunted on purpose, whatever the current strategy.
            bool huntLeader = rival != null && CanAiHurt(rival) && rival.score >= car.score + leaderHuntMargin;
            var prop = props.Where(p => !p.broken).OrderBy(p => Vector2.Distance(p.point, car.position)).FirstOrDefault();
            var pickup = car.item == 0 ? pickups.Where(p => p.available)
                .OrderBy(p => Vector2.Distance(p.point, car.position)).FirstOrDefault() : null;
            // Rotate priorities so rivals collide as well as collecting and breaking objects.
            int strategy = (Mathf.FloorToInt(Elapsed / 4) + i) % 3;
            if (rival != null && (huntLeader || strategy == 0 || car.item == 2 || prop == null && pickup == null))
            {
                aiTarget[i] = rival.index;
                aiDestination[i] = rival.position;
            }
            else if (pickup != null && (strategy == 1 || prop == null)) aiDestination[i] = pickup.point;
            else if (prop != null) aiDestination[i] = prop.point;
            else aiDestination[i] = new Vector2(Mathf.Sin(Elapsed + i) * 2.8f, Mathf.Cos(Elapsed + i) * 1.6f - .6f);
        }

        private void ConstrainToArena(ArcadeVehicle car)
        {
            Vector2 bounded = new Vector2(Mathf.Clamp(car.position.x, arenaMin.x, arenaMax.x), Mathf.Clamp(car.position.y, arenaMin.y, arenaMax.y));
            if (bounded == car.position) return;
            bool hitX = !Mathf.Approximately(car.position.x, bounded.x);
            bool hitY = !Mathf.Approximately(car.position.y, bounded.y);
            car.position = bounded;
            // A bumper slide into a wall rebounds softly instead of pinning the car.
            if (hitX) knock[car.index].x *= -.3f;
            if (hitY) knock[car.index].y *= -.3f;
            // Walls slow players without draining durability. AI turns back into the arena.
            if (car.computer)
            {
                if (hitX) car.heading = 180 - car.heading;
                if (hitY) car.heading = -car.heading;
                aiThink[car.index] = 0;
            }
            if (Mathf.Abs(car.speed) > 1) PlayHit(.2f);
            car.speed = 0;
            car.stun = .12f;
        }

        private void HitProps(ArcadeVehicle car)
        {
            foreach (var prop in props)
            {
                if (prop.broken || Vector2.Distance(car.position, prop.point) > .6f) continue;
                if (Mathf.Abs(car.speed) >= 2.1f)
                {
                    prop.broken = true;
                    prop.respawn = 6;
                    prop.view.gameObject.SetActive(false);
                    car.score += prop.points;
                    car.speed *= .75f;
                    Sound(2);
                    Announce(car.DriverName + " 撞碎 +" + prop.points, car.body.color, 1.2f);
                    aiThink[car.index] = 0;
                }
                else
                {
                    Vector2 away = (car.position - prop.point).normalized;
                    if (away.sqrMagnitude < .001f) away = -car.Forward;
                    car.position = prop.point + away * .65f;
                    car.speed *= -.3f;
                    car.stun = .15f;
                    if (car.computer) aiThink[car.index] = 0;
                }
            }
        }

        private void CollectPickup(ArcadeVehicle car)
        {
            if (car.item != 0) return;
            foreach (var pickup in pickups)
            {
                if (!pickup.available || Vector2.Distance(car.position, pickup.point) > .58f) continue;
                car.item = pickup.item != 0 ? pickup.item : RollItem(car);
                pickup.available = false;
                pickup.respawn = pickupRespawnSeconds;
                pickup.view.gameObject.SetActive(false);
                Sound(2);
                if (!car.computer) Announce(car.DriverName + " 拿到" + ItemName(car.item) + "，按道具鍵使用", car.body.color);
                break;
            }
        }

        private void UseAction(ArcadeVehicle car)
        {
            if (car.item == 1)
            {
                car.shieldSeconds = shieldDuration;
                car.item = 0;
                car.cooldown = .35f;
                Sound(2);
                Announce(car.DriverName + " 防護罩 " + shieldDuration.ToString("0") + " 秒", Cyan);
            }
            else if (car.item == 2)
            {
                var rocket = rockets.FirstOrDefault(r => !r.active);
                if (rocket == null)
                {
                    if (!car.computer) Announce("火箭還在飛，道具先保留", Yellow, 1);
                    return;
                }
                rocket.Spawn(car.position + car.Forward * .62f, car.Forward * 6, car.index);
                rocket.view.color = car.body.color;
                rocket.view.transform.rotation = Quaternion.Euler(0, 0, car.heading - 90);
                car.item = 0;
                car.cooldown = .5f;
                Sound(2);
                Announce(car.DriverName + " 發射火箭！", Yellow, 1);
            }
            else if (car.item == 3)
            {
                cherrySeconds[car.index] = cherryDuration;
                car.item = 0;
                car.cooldown = .35f;
                Sound(2);
                Announce(car.DriverName + " 吃下櫻桃！" + cherryDuration.ToString("0") + " 秒內碰到就撞毀", car.body.color, 2.5f);
            }
        }

        private void CherryWreck(ArcadeVehicle attacker, ArcadeVehicle victim)
        {
            PlayHit(.9f);
            // AI never harms a human player, even with a cherry.
            if (attacker.computer && !CanAiHurt(victim)) { attacker.speed *= .3f; return; }
            if (ApplyDamage(victim, 1) && !victim.alive)
            {
                AwardWreck(attacker);
                Announce(attacker.DriverName + " 一碰撞毀 " + victim.DriverName + "！", attacker.body.color, 2);
            }
        }

        // Crates are random; only the car currently in last place can roll the cherry.
        private int RollItem(ArcadeVehicle car)
        {
            bool trailing = cars.Where(c => !c.eliminated).All(c => c == car || c.score >= car.score);
            if (trailing && UnityEngine.Random.value < trailingCherryChance) return 3;
            return UnityEngine.Random.value < .5f ? 1 : 2;
        }

        private void ResolveContacts(float dt)
        {
            for (int i = 0; i < cars.Length; i++) for (int j = i + 1; j < cars.Length; j++)
            {
                contactCooldowns[i, j] = Mathf.Max(0, contactCooldowns[i, j] - dt);
                var a = cars[i];
                var b = cars[j];
                if (!a.alive || !b.alive || Vector2.Distance(a.position, b.position) >= .62f) continue;
                Vector2 separation = (a.position - b.position).normalized;
                if (separation.sqrMagnitude < .001f) separation = Vector2.right;
                float overlap = .63f - Vector2.Distance(a.position, b.position);
                a.position += separation * overlap * .5f;
                b.position -= separation * overlap * .5f;
                if (contactCooldowns[i, j] <= 0)
                {
                    // Every new touch bounces both cars apart a little; a real ram adds more in ResolveImpact.
                    knock[i] += separation * contactBump;
                    knock[j] -= separation * contactBump;
                    if (a.computer) aiThink[i] = 0;
                    if (b.computer) aiThink[j] = 0;
                    ResolveImpact(a, b);
                    contactCooldowns[i, j] = .8f;
                }
                a.Sync();
                b.Sync();
            }
        }

        private void ResolveImpact(ArcadeVehicle a, ArcadeVehicle b)
        {
            float approachA = Vector2.Dot(a.Forward * a.speed, (b.position - a.position).normalized);
            float approachB = Vector2.Dot(b.Forward * b.speed, (a.position - b.position).normalized);
            if (HasCherry(a) != HasCherry(b))
            {
                // Cherry: any touch wrecks the other car, unless that car actually rammed the cherry car first.
                var cherry = HasCherry(a) ? a : b;
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
            // AI never damages a human by initiating a collision, even if avoidance failed.
            if (attacker.computer && !CanAiHurt(victim)) damage = 0;
            bool damaged = ApplyDamage(victim, damage);
            if (damaged)
            {
                attacker.score += Mathf.RoundToInt(impact * 30 * multiplier);
                if (!victim.alive) AwardWreck(attacker);
            }
            ApplyDamage(attacker, damage * .15f);
            attacker.speed *= .6f;
            if (damaged) { victim.speed *= .4f; victim.stun = .12f; }
            // Bumper cars: the rammed car slides away and its nose is knocked aside; the rammer rebounds.
            Vector2 push = (victim.position - attacker.position).normalized;
            knock[victim.index] += push * ramBump * (.6f + impact * .4f);
            knock[attacker.index] -= push * ramBump * .35f;
            victim.heading += UnityEngine.Random.Range(15f, 30f) * (UnityEngine.Random.value < .5f ? -1 : 1);
            if (damaged && victim.alive) Announce(attacker.DriverName + " 撞擊 x" + multiplier.ToString("0.0"), attacker.body.color, 1);
            PlayHit(Mathf.Clamp(.3f + impact * .1f, .3f, .75f));
        }

        private void MoveRockets(float dt)
        {
            foreach (var rocket in rockets)
            {
                if (!rocket.active) continue;
                var owner = cars[rocket.owner];
                if (owner.eliminated) { rocket.Hide(); continue; }
                Vector2 from = rocket.position;
                rocket.position += rocket.velocity * dt;
                rocket.age += dt;
                // Swept hit detection avoids skipping cars at high projectile speed.
                var victim = cars.Where(c => c.alive && c != owner && (!owner.computer || CanAiHurt(c)))
                    .Where(c => SegmentDistance(c.position, from, rocket.position) < .42f)
                    .OrderBy(c => Vector2.Distance(from, c.position)).FirstOrDefault();
                if (victim != null)
                {
                    if (ApplyDamage(victim, HasCherry(victim) ? 1 : .38f))
                    {
                        owner.score += 120;
                        if (!victim.alive) AwardWreck(owner);
                        victim.stun = .5f;
                        victim.speed *= .2f;
                    }
                    Explode(victim.position);
                    rocket.Hide();
                    PlayHit(.75f);
                    continue;
                }
                var prop = props.FirstOrDefault(p => !p.broken && SegmentDistance(p.point, from, rocket.position) < .4f);
                if (prop != null)
                {
                    prop.broken = true;
                    prop.respawn = 6;
                    prop.view.gameObject.SetActive(false);
                    owner.score += prop.points;
                    rocket.Hide();
                    Sound(2);
                    continue;
                }
                if (rocket.age > 1.6f || rocket.position.x < arenaMin.x || rocket.position.x > arenaMax.x
                    || rocket.position.y < arenaMin.y || rocket.position.y > arenaMax.y) rocket.Hide();
                else rocket.Sync();
            }
        }

        private static float SegmentDistance(Vector2 point, Vector2 from, Vector2 to)
        {
            Vector2 segment = to - from;
            float t = segment.sqrMagnitude > .00001f ? Mathf.Clamp01(Vector2.Dot(point - from, segment) / segment.sqrMagnitude) : 0;
            return Vector2.Distance(point, from + segment * t);
        }

        public void DamageVehicle(ArcadeVehicle car, float damage) => ApplyDamage(car, damage);

        private bool ApplyDamage(ArcadeVehicle car, float damage)
        {
            if (!car.alive || car.eliminated || damage <= 0) return false;
            if (car.shieldSeconds > 0 || car.spawnProtection > 0)
            {
                if (car.shieldSeconds > 0) Announce(car.DriverName + " 防護罩擋下！", Cyan, .75f);
                return false;
            }
            car.health = Mathf.Max(0, car.health - damage);
            if (car.health <= 0)
            {
                int deducted = Mathf.Min(wreckPenalty, Mathf.FloorToInt(car.score));
                car.score = Mathf.Max(0, car.score - wreckPenalty);
                car.alive = false;
                car.respawn = enemyRespawnSeconds;
                car.speed = car.shieldSeconds = cherrySeconds[car.index] = 0;
                car.item = 0;
                StartSpinOut(car, false); // wrecked cars skid off screen too; the impact already played a hit
                Wrecks++;
                Announce(car.DriverName + " 被撞毀 -" + deducted + "，" + enemyRespawnSeconds.ToString("0") + " 秒後重生", Yellow, 2.5f);
            }
            return true;
        }

        private void AwardWreck(ArcadeVehicle attacker)
        {
            attacker.score += wreckBonus;
            Announce(attacker.DriverName + " 撞毀獎勵 +" + wreckBonus, attacker.body.color, 2);
        }

        private void RespawnVehicle(ArcadeVehicle car)
        {
            Vector2[] corners = { new Vector2(-3.5f, 1.1f), new Vector2(3.5f, 1.1f), new Vector2(3.5f, -3.2f), new Vector2(-3.5f, -3.2f) };
            Vector2 spawn = corners.OrderByDescending(p => cars.Where(c => c.alive)
                .Select(c => Vector2.Distance(p, c.position)).DefaultIfEmpty(10).Min()).First();
            float score = car.score;
            spinOutSpeed[car.index] = 0;
            knock[car.index] = Vector2.zero;
            car.ResetVehicle(spawn, spawn.x > 0 ? 180 : 0, demo ? 0 : PlayerCount);
            car.score = score;
            car.spawnProtection = 2;
            aiThink[car.index] = 0;
        }

        private void EliminateLowest()
        {
            int count = Contenders;
            if (count <= 1) { EndRound(); return; }
            // Ties: lower durability loses first, then the higher car index. Never random.
            var loser = cars.Where(c => !c.eliminated).OrderBy(c => c.score).ThenBy(c => c.health).ThenByDescending(c => c.index).First();
            finalPlaces[loser.index] = count;
            loser.eliminated = true;
            loser.alive = false;
            loser.speed = loser.shieldSeconds = loser.spawnProtection = cherrySeconds[loser.index] = 0;
            loser.item = 0;
            StartSpinOut(loser);
            foreach (var rocket in rockets.Where(r => r.active && r.owner == loser.index)) rocket.Hide();
            eliminationStage++;
            Announce("最後一名 " + loser.DriverName + " 淘汰！", Yellow, 4);
            Sound(3);
            for (int i = 0; i < aiThink.Length; i++) aiThink[i] = 0;
            if (Contenders == 1) EndRound();
        }

        private void EndRound()
        {
            var winner = cars.First(c => !c.eliminated);
            Champion = winner.index;
            finalPlaces[Champion] = 1;
            Survived = !winner.computer;
            if (!winner.alive) RespawnVehicle(winner);
            foreach (var rocket in rockets) rocket.Hide();
            int best = Mathf.Max(PlayerPrefs.GetInt("WreckBest", 0), Mathf.RoundToInt(cars.Where(c => !c.computer).Max(c => c.score)));
            PlayerPrefs.SetInt("WreckBest", best);
            string standings = string.Join("\n", cars.OrderBy(c => finalPlaces[c.index])
                .Select(c => finalPlaces[c.index] + ". " + c.DriverName + "  " + Mathf.RoundToInt(c.score).ToString("00000")));
            Finish(winner.DriverName + " 獲勝！", standings + "\n\n最高分 " + best + "　撞毀 " + Wrecks + " 次"
                + "\n\n60／90／120 秒各淘汰最後一名");
        }

        // The loser skids straight away from the arena centre while spinning, then is hidden once off screen.
        private void StartSpinOut(ArcadeVehicle car, bool playSound = true)
        {
            Vector2 away = car.position - (arenaMin + arenaMax) / 2;
            if (away.sqrMagnitude < .01f) away = car.Forward;
            spinOutDirection[car.index] = away.normalized;
            spinOutSpeed[car.index] = spinOutStartSpeed;
            car.gameObject.SetActive(true); // a car wrecked at that moment reappears to fly out
            car.Sync();
            if (playSound) PlayHit(1);
        }

        private void UpdateSpinOuts(float dt)
        {
            Vector2 view = sceneCamera.transform.position;
            float half = sceneCamera.orthographicSize + 1;
            foreach (var car in cars)
            {
                int i = car.index;
                if (spinOutSpeed[i] <= 0) continue;
                spinOutSpeed[i] += spinOutAcceleration * dt;
                car.position += spinOutDirection[i] * spinOutSpeed[i] * dt;
                car.heading += spinOutTurnsPerSecond * 360 * dt;
                car.Sync();
                if (Mathf.Abs(car.position.x - view.x) > half || Mathf.Abs(car.position.y - view.y) > half)
                {
                    spinOutSpeed[i] = 0;
                    car.gameObject.SetActive(false);
                }
            }
        }

        // Rocket hit: the supplied explosion picture pops up over the car and fades out.
        private void Explode(Vector2 point)
        {
            if (explosions == null || explosions.Length == 0) return;
            int slot = 0;
            for (int i = 0; i < explosions.Length; i++) if (explosionAge[i] < 0) { slot = i; break; }
            explosionAge[slot] = 0;
            explosions[slot].transform.position = PixelPosition(point);
            explosions[slot].gameObject.SetActive(true);
        }

        private void UpdateExplosions(float dt)
        {
            if (explosions == null) return;
            for (int i = 0; i < explosions.Length; i++)
            {
                if (explosionAge[i] < 0 || explosions[i] == null) continue;
                explosionAge[i] += dt;
                float t = explosionAge[i] / explosionSeconds;
                if (t >= 1) { explosionAge[i] = -1; explosions[i].gameObject.SetActive(false); continue; }
                // Quick pop to full size, then flicker out (no alpha: the palette stays four solid colours).
                float scale = t < .25f ? Mathf.Lerp(.5f, 1.15f, t / .25f) : Mathf.Lerp(1.15f, 1f, (t - .25f) / .75f);
                explosions[i].transform.localScale = new Vector3(scale, scale, 1);
                explosions[i].enabled = t < .6f || Mathf.FloorToInt(explosionAge[i] * 20) % 2 == 0;
            }
        }

        protected override void AfterTick(float dt)
        {
            UpdateSpinOuts(dt);
            UpdateExplosions(dt);
            engine.Update(this, engineClip, cars, Phase == ArcadePhase.Playing || Phase == ArcadePhase.Countdown, driveSpeed);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            engine.Dispose();
        }

        private static string ItemName(int item) => item == 1 ? "防護罩" : item == 2 ? "火箭" : item == 3 ? "櫻桃" : "無道具";

        private string DriverStatus(ArcadeVehicle car)
        {
            if (car.eliminated) return "淘汰 第" + finalPlaces[car.index] + "名";
            if (!car.alive) return Mathf.CeilToInt(car.respawn) + "秒後重生";
            if (HasCherry(car)) return "櫻桃 " + Mathf.CeilToInt(cherrySeconds[car.index]);
            if (car.shieldSeconds > 0) return "防護罩 " + Mathf.CeilToInt(car.shieldSeconds);
            return car.item != 0 ? ItemName(car.item) : "耐久 " + Mathf.CeilToInt(car.health * 100);
        }

        private string ActionStatus(ArcadeVehicle car) => car.eliminated ? "已淘汰" : !car.alive ? "重生中"
            : HasCherry(car) ? "櫻桃 " + Mathf.CeilToInt(cherrySeconds[car.index]) + " 秒" : ItemName(car.item);

        protected override void RefreshHud()
        {
            // Attract mode shows only the demo, title, coin prompt and the two players' controls.
            bool attract = Phase == ArcadePhase.Attract && attractScreen != null;
            if (attractScreen != null)
            {
                attractScreen.SetActive(attract);
                if (attract) overlay.SetActive(false);
                foreach (var text in new[] { header, arenaBoard, leaderboard, hud, footer, notice })
                    if (text != null) text.gameObject.SetActive(!attract);
                if (attractPrompt != null) attractPrompt.enabled = Mathf.FloorToInt(Time.unscaledTime * 2) % 2 == 0;
            }
            for (int i = 0; i < cars.Length; i++)
            {
                var car = cars[i];
                if (carLabels != null && i < carLabels.Length && carLabels[i] != null)
                {
                    carLabels[i].text = car.DriverName + (car.item == 1 ? " [盾]" : car.item == 2 ? " [箭]" : car.item == 3 ? " [櫻]" : "");
                    carLabels[i].color = car.body.color;
                    Vector2 tag = car.position + Vector2.up * .68f;
                    if (carLabels[i] is TextMeshProUGUI screenTag)
                    {
                        // Screen-space tag on the 320px cabinet: 1 world unit = 32 cabinet units, origin at centre.
                        // Sits just above the roof health bar.
                        Vector2 screen = (tag + Vector2.up * .2f - (Vector2)sceneCamera.transform.position) * 32;
                        // Whole pixels keep the 11px pixel font sharp.
                        screenTag.rectTransform.anchoredPosition = new Vector2(Mathf.Round(screen.x), Mathf.Round(screen.y));
                        screenTag.gameObject.SetActive(!attract && car.alive && car.gameObject.activeInHierarchy);
                    }
                    else carLabels[i].transform.position = PixelPosition(tag);
                }
                if (healthBars != null && i < healthBars.Length && healthBars[i] != null)
                    healthBars[i].localScale = new Vector3(Mathf.Clamp01(car.health), 1, 1);
                if (shieldViews != null && i < shieldViews.Length && shieldViews[i] != null)
                    shieldViews[i].gameObject.SetActive(car.alive && (car.shieldSeconds > 0
                        || Phase != ArcadePhase.Attract && car.spawnProtection > 0 && Mathf.FloorToInt(Elapsed * 8) % 2 == 0));
                if (cherryViews != null && i < cherryViews.Length && cherryViews[i] != null)
                {
                    // Blinks during the last two seconds as a warning that the power is ending.
                    float left = cherrySeconds[i];
                    bool cherryOn = HasCherry(car) && (left > 2 || Mathf.FloorToInt(Elapsed * 8) % 2 == 0);
                    cherryViews[i].gameObject.SetActive(cherryOn);
                    // The whole car turns yellow while the cherry power lasts (blinking with the icon at the end).
                    if (solidBlock == null) solidBlock = new MaterialPropertyBlock();
                    car.body.GetPropertyBlock(solidBlock);
                    solidBlock.SetFloat("_Solid", cherryOn ? 1 : 0);
                    solidBlock.SetColor("_SolidColor", Yellow);
                    car.body.SetPropertyBlock(solidBlock);
                }
            }
            header.text = "廢車場大亂鬥";
            arenaBoard.text = Phase == ArcadePhase.Results ? "冠軍 " + cars[Champion].DriverName
                : Mathf.CeilToInt(Mathf.Max(0, NextElimination - Elapsed)).ToString("00") + "秒後淘汰／剩" + Contenders + "車";
            var order = Phase == ArcadePhase.Results ? cars.OrderBy(c => finalPlaces[c.index])
                : cars.OrderBy(c => c.eliminated).ThenBy(c => c.eliminated ? finalPlaces[c.index] : -c.score);
            if (leaderboard != null)
                leaderboard.text = string.Join("\n", order.Select((c, rank) =>
                    "<color=#" + ColorUtility.ToHtmlStringRGB(c.body.color) + ">" + (rank + 1) + " " + c.DriverName.PadRight(4)
                    + " " + Mathf.RoundToInt(c.score).ToString("00000") + "  " + DriverStatus(c) + "</color>"));
            hud.text = "P1 " + ActionStatus(cars[0]) + (PlayerCount == 2 ? "\nP2 " + ActionStatus(cars[1]) : "")
                + "\n時間 " + Mathf.CeilToInt(Remaining) + "　撞毀 " + Wrecks
                + (Phase == ArcadePhase.Playing && cars.Where(c => !c.computer).All(c => c.eliminated) ? "\n觀戰中" : "");
            // One 11px line fits under the arena; in 2P it alternates between the two players every 3 seconds.
            bool showP2 = PlayerCount == 2 && Mathf.FloorToInt(Time.unscaledTime / 3) % 2 == 1;
            footer.text = showP2 ? "P2  W油門  AD轉向  數字鍵Enter道具" : "P1  ↑油門  ←→轉向  左Ctrl道具  R重玩";
            if (Phase == ArcadePhase.Attract) overlayHint.text = "按 1／2 選人數　C／空白鍵 投幣";
            ShowStartMessages();
        }

        // After 出發！ (shown for 0.7s by ArcadeGame), the start messages follow in the centre at half the countdown size.
        private void ShowStartMessages()
        {
            if (countdownLabel == null || startMessages == null) return;
            if (countdownBaseSize <= 0) countdownBaseSize = countdownLabel.fontSize;
            float after = Elapsed - .7f;
            int index = Phase == ArcadePhase.Playing && after >= 0 ? Mathf.FloorToInt(after / startMessageSeconds) : -1;
            if (index >= 0 && index < startMessages.Length)
            {
                countdownLabel.fontSize = countdownBaseSize / 2; // still a whole multiple of the 11px pixel font
                countdownLabel.text = startMessages[index];
                showingStartMessage = true;
                return;
            }
            if (showingStartMessage) countdownLabel.text = "";
            showingStartMessage = false;
            countdownLabel.fontSize = countdownBaseSize;
        }
    }
}
