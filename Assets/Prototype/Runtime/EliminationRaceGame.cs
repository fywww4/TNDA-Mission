using System.Linq;
using UnityEngine;
using TMPro;

namespace TNDA.Prototype
{
    public class EliminationRaceGame : ArcadeGame
    {
        public ArcadeVehicle[] cars;
        public SpriteRenderer[] boxes;
        public ArcadeProjectile[] bolts;
        public TMP_Text circuitBoard;
        public TMP_Text[] carLabels;
        public float maxSpeed = 3.3f;
        public float trackHalfWidth = .62f;
        [Tooltip("Recorded engine rev; its steady middle is looped for each human driver.")]
        public AudioClip engineClip;
        private readonly ArcadeEngineAudio engine = new ArcadeEngineAudio();
        public int Eliminations { get; private set; }
        public string Winner { get; private set; }
        private readonly float[] boxTimers = new float[2];
        private float[] spinTimers = new float[4], spinHeadings = new float[4];
        private float collisionSoundDelay;
        private const float SpinSeconds = .85f;
        public int ActiveCars => cars.Count(c => c.alive);
        public int ActiveBolts => bolts.Count(b => b.active);

        public static readonly Vector2 TrackCentre = new Vector2(0, -.35f);
        public static Vector2 TrackPoint(float angle) => new Vector2(3.65f * Mathf.Cos(angle), -.35f + 2.75f * Mathf.Sin(angle));
        public static float TrackAngle(Vector2 point) => Mathf.Atan2((point.y + .35f) / 2.75f, point.x / 3.65f);

        protected override void ResetMatch()
        {
            Eliminations = 0; Winner = ""; collisionSoundDelay = 0;
            for (int i = 0; i < cars.Length; i++)
            {
                float angle = -Mathf.PI / 2 - .28f * i;
                Vector2 tangent = new Vector2(-3.65f * Mathf.Sin(angle), 2.75f * Mathf.Cos(angle));
                cars[i].ResetVehicle(TrackPoint(angle), Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg, PlayerCount);
                cars[i].lastAngle = angle;
                // Car sprites already carry their palette colours, so the renderer stays untinted.
                cars[i].body.color = Color.white;
                if (carLabels != null && i < carLabels.Length && carLabels[i] != null)
                {
                    carLabels[i].text = cars[i].DriverName;
                    carLabels[i].color = DriverColor(cars[i]);
                }
            }
            spinTimers = new float[cars.Length];
            spinHeadings = new float[cars.Length];
            for (int i = 0; i < boxes.Length; i++)
            {
                boxTimers[i] = 0;
                boxes[i].gameObject.SetActive(true);
            }
            foreach (var bolt in bolts) bolt.Hide();
        }

        protected override void Tick(float dt, ArcadeInput p1, ArcadeInput p2)
        {
            collisionSoundDelay = Mathf.Max(0, collisionSoundDelay - dt);
            for (int i = 0; i < boxes.Length; i++)
            {
                boxTimers[i] = Mathf.Max(0, boxTimers[i] - dt);
                boxes[i].gameObject.SetActive(boxTimers[i] <= 0);
            }
            foreach (var car in cars)
            {
                if (!car.alive) continue;
                ArcadeInput input = car.index == 0 ? p1 : p2;
                if (car.computer)
                {
                    float angle = TrackAngle(car.position);
                    Vector2 aim = TrackPoint(angle + .24f);
                    // Swing to the free side of the lane when another car blocks the line ahead.
                    foreach (var other in cars)
                    {
                        Vector2 to = other.position - car.position;
                        if (other == car || !other.alive || to.magnitude > 1.1f || Vector2.Dot(to.normalized, car.Forward) < .6f) continue;
                        Vector2 radial = (aim - TrackCentre).normalized;
                        float side = Vector2.Dot(other.position - TrackPoint(TrackAngle(other.position)), radial);
                        aim += radial * (side > 0 ? -.45f : .45f);
                        break;
                    }
                    Vector2 goal = aim - car.position;
                    float goalHeading = Mathf.Atan2(goal.y, goal.x) * Mathf.Rad2Deg;
                    input.steer = -Mathf.Clamp(Mathf.DeltaAngle(car.heading, goalHeading) / 35, -1, 1);
                    input.throttle = 1;
                    if (car.item != 0 && car.stun <= 0) UseItem(car);
                }
                else if (input.actionPressed) UseItem(car);
                car.Drive(dt, input, maxSpeed * (car.computer ? .84f + car.index * .025f : 1));
                if (spinTimers[car.index] > 0)
                {
                    // Two full turns, then the car faces its original direction again.
                    spinTimers[car.index] = Mathf.Max(0, spinTimers[car.index] - dt);
                    car.heading = spinTimers[car.index] > 0
                        ? spinHeadings[car.index] + (1 - spinTimers[car.index] / SpinSeconds) * 720
                        : spinHeadings[car.index];
                }

                float theta = TrackAngle(car.position);
                Vector2 onTrack = TrackPoint(theta), offTrack = car.position - onTrack;
                bool hitWall = offTrack.magnitude > trackHalfWidth;
                if (hitWall)
                {
                    // Scrubbing along the wall slows the car; only a hard, head-on hit stalls it.
                    car.position = onTrack + offTrack.normalized * trackHalfWidth;
                    float impact = Vector2.Dot(car.Forward * car.speed, offTrack.normalized);
                    if (impact > 0) car.speed *= 1 - Mathf.Clamp01(impact) * .6f * dt * 10;
                    if (impact > 1.2f) { car.speed *= .4f; car.stun = .18f; CollisionSound(); }
                }
                theta = TrackAngle(car.position);
                float delta = Mathf.DeltaAngle(car.lastAngle * Mathf.Rad2Deg, theta * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                car.lastAngle = theta;
                // A high-water mark prevents reverse/forward oscillation from farming points.
                if (Mathf.Abs(delta) < .2f)
                {
                    float metric = new Vector2(-3.65f * Mathf.Sin(theta), 2.75f * Mathf.Cos(theta)).magnitude;
                    car.progress += delta * metric;
                    if (!hitWall) car.bestProgress = Mathf.Max(car.bestProgress, car.progress);
                    car.score = Mathf.Floor(car.bestProgress * 100);
                }
                for (int i = 0; i < boxes.Length; i++)
                    if (car.item == 0 && boxTimers[i] <= 0 && Vector2.Distance(car.position, boxes[i].transform.position) < .5f)
                    {
                        car.item = i + 1; boxTimers[i] = 5;
                        boxes[i].gameObject.SetActive(false); Sound(0);
                        if (!car.computer) Announce(car.DriverName + " PICKED " + (car.item == 1 ? "NITRO" : "BOLT"), DriverColor(car));
                    }
                car.Sync();
            }
            for (int i = 0; i < cars.Length; i++) for (int j = i + 1; j < cars.Length; j++)
            {
                if (!cars[i].alive || !cars[j].alive) continue;
                Vector2 apart = cars[i].position - cars[j].position;
                if (apart.magnitude >= .6f) continue;
                if (apart.sqrMagnitude < .0001f) apart = Vector2.right;
                Vector2 push = apart.normalized * ((.6f - apart.magnitude) * .5f + .005f);
                cars[i].position += push; cars[j].position -= push;
                // Only a real closing impact costs speed; resting contact must never lock cars in place.
                float closing = Vector2.Dot(cars[i].Forward * cars[i].speed - cars[j].Forward * cars[j].speed, apart.normalized);
                if (closing < -.6f)
                {
                    cars[i].speed *= .8f; cars[j].speed *= .8f;
                    cars[i].stun = Mathf.Max(cars[i].stun, .12f); cars[j].stun = Mathf.Max(cars[j].stun, .12f);
                    CollisionSound();
                }
                cars[i].Sync(); cars[j].Sync();
            }
            foreach (var bolt in bolts)
            {
                if (!bolt.active) continue;
                bolt.age += dt;
                bolt.position += bolt.velocity * dt;
                foreach (var car in cars)
                    if (car.alive && car.index != bolt.owner && Vector2.Distance(car.position, bolt.position) < .38f)
                    {
                        if (spinTimers[car.index] <= 0) spinHeadings[car.index] = car.heading;
                        car.stun = SpinSeconds; car.speed = 0; spinTimers[car.index] = SpinSeconds;
                        bolt.Hide(); PlayHit(.8f); Announce(car.DriverName + " HIT // NO POINTS DEDUCTED", Yellow); break;
                    }
                if (bolt.age > 2 || Mathf.Abs(bolt.position.x) > 4.8f || Mathf.Abs(bolt.position.y) > 4.2f) bolt.Hide();
                if (bolt.active) bolt.Sync();
            }
            float[] schedule = { 60, 90, 120 };
            while (Eliminations < schedule.Length && Elapsed >= schedule[Eliminations]) EliminateLowest();
        }

        public void UseItem(ArcadeVehicle car)
        {
            if (Phase != ArcadePhase.Playing || !car.alive || car.item == 0) return;
            if (car.item == 1)
            {
                car.boost = 1.5f; car.item = 0;
                Sound(0); Announce(car.DriverName + " NITRO!", DriverColor(car));
                return;
            }
            var available = bolts.FirstOrDefault(b => !b.active);
            if (available == null) return;
            var target = cars.Where(c => c.alive && c != car).OrderBy(c => Vector2.Distance(car.position, c.position)).FirstOrDefault();
            Vector2 direction = target == null ? car.Forward : (target.position - car.position).normalized;
            available.Spawn(car.position + direction * .4f, direction * 5.5f, car.index);
            car.item = 0; Sound(0);
        }

        private void EliminateLowest()
        {
            var loser = cars.Where(c => c.alive).OrderBy(c => c.score).ThenByDescending(c => c.index).First();
            loser.alive = false; loser.gameObject.SetActive(false); Eliminations++;
            Announce(loser.DriverName + " ELIMINATED // " + ActiveCars + " REMAIN", Yellow, 3);
            Sound(3);
            if (Eliminations == 3)
            {
                var champion = cars.First(c => c.alive); Winner = champion.DriverName;
                foreach (var bolt in bolts) bolt.Hide();
                foreach (var box in boxes) box.gameObject.SetActive(false);
                string standings = string.Join("\n", cars.OrderByDescending(c => c.score).Select(c => c.DriverName.PadRight(5) + "   " + ((int)c.score).ToString("00000")));
                Finish(Winner + " WINS!", "FINAL DISTANCE POINTS\n\n" + standings + "\n\nRACE COMPLETE AT 120s");
            }
        }

        protected override void AfterTick(float dt) =>
            engine.Update(this, engineClip, cars, Phase == ArcadePhase.Playing || Phase == ArcadePhase.Countdown, maxSpeed);

        protected override void OnDestroy()
        {
            base.OnDestroy();
            engine.Dispose();
        }

        public static Color DriverColor(ArcadeVehicle car) => car.computer ? White : car.index == 0 ? Cyan : Yellow;

        private void CollisionSound()
        {
            if (collisionSoundDelay <= 0) { PlayHit(.3f); collisionSoundDelay = .3f; }
        }

        protected override void RefreshHud()
        {
            header.text = "02 / ELIMINATION CIRCUIT";
            hud.text = string.Join("  ", cars.Select(c => !c.alive ? "<s>" + c.DriverName + " OUT</s>"
                : "<color=#" + ColorUtility.ToHtmlStringRGB(DriverColor(c)) + ">" + c.DriverName + "</color> " + ((int)c.score).ToString("00000")));
            footer.text = "P1 WASD+SPACE  P2 ARROWS+RCTRL  R RESTART";
            float next = Eliminations == 0 ? 60 : Eliminations == 1 ? 90 : 120;
            // The infield belongs to the big 3-2-1-GO while it is showing.
            if (countdownLabel != null && countdownLabel.text != "") { circuitBoard.text = ""; return; }
            circuitBoard.text ="NEXT CUT\n" + Mathf.CeilToInt(Mathf.Max(0, next - Elapsed)).ToString("00") + "s\n" + ActiveCars + " CARS LEFT";
            if (Phase == ArcadePhase.Playing)
            {
                circuitBoard.text += "\nP1 " + (cars[0].item == 1 ? "NITRO" : cars[0].item == 2 ? "BOLT" : "NO ITEM");
                if (PlayerCount == 2) circuitBoard.text += "\nP2 " + (cars[1].item == 1 ? "NITRO" : cars[1].item == 2 ? "BOLT" : "NO ITEM");
            }
        }
    }
}
