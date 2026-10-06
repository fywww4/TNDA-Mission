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

    public class DemolitionGame : ArcadeGame
    {
        public ArcadeVehicle[] cars;
        public BreakableProp[] props;
        public TMP_Text arenaBoard;
        public TMP_Text[] carLabels;
        public Transform[] healthBars;
        public SpriteRenderer[] ramTrails;
        [Header("Tuning")]
        public float roundSeconds = 90;
        public float driveSpeed = 2.6f;
        public float enemyRespawnSeconds = 6;
        public Vector2 arenaMin = new Vector2(-4.35f, -3.85f);
        public Vector2 arenaMax = new Vector2(4.35f, 3.05f);
        [Tooltip("Recorded engine rev: looped for human drivers and played as the ram-dash burst.")]
        public AudioClip engineClip;
        private readonly ArcadeEngineAudio engine = new ArcadeEngineAudio();
        private readonly float[,] contactCooldowns = new float[4, 4];
        public float Remaining => Mathf.Max(0, roundSeconds - Elapsed);
        public int LivePlayers => cars.Count(c => !c.computer && c.alive);
        public bool Survived { get; private set; }
        public int Wrecks { get; private set; }

        protected override void ResetMatch()
        {
            Survived = false; Wrecks = 0;
            Vector2[] starts = { new Vector2(-3.3f, -3.1f), new Vector2(3.3f, -3.1f), new Vector2(3.3f, 2.2f), new Vector2(-3.3f, 2.2f) };
            for (int i = 0; i < cars.Length; i++)
            {
                cars[i].ResetVehicle(starts[i], i < 2 ? 90 : -90, PlayerCount);
                if (healthBars != null && i < healthBars.Length && healthBars[i] != null)
                    healthBars[i].GetComponentInChildren<SpriteRenderer>().color = cars[i].body.color;
            }
            foreach (var prop in props) { prop.broken = false; prop.respawn = 0; prop.view.gameObject.SetActive(true); }
            Array.Clear(contactCooldowns, 0, contactCooldowns.Length);
        }

        protected override void Tick(float dt, ArcadeInput p1, ArcadeInput p2)
        {
            foreach (var prop in props)
            {
                if (!prop.broken) continue;
                prop.respawn -= dt;
                if (prop.respawn <= 0 && cars.All(c => !c.alive || Vector2.Distance(c.position, prop.point) > 1))
                { prop.broken = false; prop.view.gameObject.SetActive(true); }
            }
            foreach (var car in cars)
            {
                if (!car.alive)
                {
                    if (car.computer)
                    {
                        car.respawn -= dt;
                        if (car.respawn <= 0) RespawnEnemy(car);
                    }
                    continue;
                }
                ArcadeInput input = car.index == 0 ? p1 : p2;
                if (car.computer)
                {
                    var target = cars.Where(c => !c.computer && c.alive).OrderBy(c => Vector2.Distance(car.position, c.position)).FirstOrDefault();
                    if (target != null)
                    {
                        Vector2 delta = target.position - car.position;
                        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                        input.steer = -Mathf.Clamp(Mathf.DeltaAngle(car.heading, angle) / 45, -1, 1);
                        input.throttle = 1;
                        input.action = delta.magnitude < 2 && Mathf.Abs(Mathf.DeltaAngle(car.heading, angle)) < 30;
                    }
                }
                if (input.action && car.cooldown <= 0)
                {
                    car.boost = .8f;
                    car.cooldown = car.computer ? 5 : 2.5f;
                    PlayClip(engineClip, 0, car.computer ? .3f : .55f);
                }
                car.Drive(dt, input, driveSpeed * (car.computer ? .8f : 1), 4.4f);
                Vector2 bounded = new Vector2(Mathf.Clamp(car.position.x, arenaMin.x, arenaMax.x), Mathf.Clamp(car.position.y, arenaMin.y, arenaMax.y));
                if (bounded != car.position)
                {
                    car.position = bounded; car.heading += 110; car.speed *= .2f; car.stun = .22f;
                    DamageVehicle(car, .035f); PlayHit(.3f);
                }
                foreach (var prop in props)
                {
                    if (prop.broken || Vector2.Distance(car.position, prop.point) > .6f) continue;
                    if (Mathf.Abs(car.speed) >= 2.3f)
                    {
                        prop.broken = true; prop.respawn = 8; prop.view.gameObject.SetActive(false);
                        car.score += prop.points; car.speed *= .75f; Sound(2);
                        if (!car.computer) Announce(car.DriverName + " BREAK! +" + prop.points, car.body.color);
                    }
                    else
                    {
                        Vector2 away = (car.position - prop.point).normalized;
                        if (away.sqrMagnitude < .001f) away = -car.Forward;
                        car.position = prop.point + away * .65f;
                        car.speed *= -.3f; car.stun = .2f;
                    }
                }
                car.Sync();
            }
            for (int i = 0; i < cars.Length; i++) for (int j = i + 1; j < cars.Length; j++)
            {
                contactCooldowns[i, j] = Mathf.Max(0, contactCooldowns[i, j] - dt);
                var a = cars[i]; var b = cars[j];
                if (!a.alive || !b.alive || Vector2.Distance(a.position, b.position) >= .62f) continue;
                Vector2 separation = (a.position - b.position).normalized;
                if (separation.sqrMagnitude < .001f) separation = Vector2.right;
                a.position += separation * .1f; b.position -= separation * .1f;
                if (contactCooldowns[i, j] <= 0)
                {
                    ResolveImpact(a, b);
                    contactCooldowns[i, j] = .7f;
                }
                a.Sync(); b.Sync();
            }
            if (LivePlayers == 0) EndRound(false);
            else if (Remaining <= 0) EndRound(true);
        }

        private void ResolveImpact(ArcadeVehicle a, ArcadeVehicle b)
        {
            float approachA = Vector2.Dot(a.Forward * a.speed, (b.position - a.position).normalized);
            float approachB = Vector2.Dot(b.Forward * b.speed, (a.position - b.position).normalized);
            var attacker = approachA >= approachB ? a : b;
            var victim = attacker == a ? b : a;
            float impact = Mathf.Max(0, Mathf.Max(approachA, approachB));
            if (impact < 1) { a.speed *= .7f; b.speed *= .7f; return; }
            float alignment = Vector2.Dot(attacker.Forward, victim.Forward);
            float rear = Vector2.Dot((attacker.position - victim.position).normalized, victim.Forward);
            float multiplier = Mathf.Abs(alignment) < .5f ? 2 : alignment > .6f && rear < -.25f ? 1.5f : 1;
            float damage = (.04f + impact * .05f) * multiplier;
            attacker.score += Mathf.RoundToInt(impact * 35 * multiplier);
            DamageVehicle(victim, damage);
            DamageVehicle(attacker, damage * .25f);
            attacker.speed *= .55f; victim.speed *= .3f; victim.stun = .4f;
            victim.position += attacker.Forward * .25f;
            if (!victim.alive)
            {
                attacker.score += 500;
                if (!attacker.computer && victim.computer) Wrecks++;
            }
            if (!attacker.computer) Announce(attacker.DriverName + " RAM x" + multiplier.ToString("0.0")
                + (!victim.alive ? "  WRECK +500!" : ""), Yellow);
            PlayHit(Mathf.Clamp(.4f + impact * .12f, .4f, .9f));
        }

        protected override void AfterTick(float dt) =>
            engine.Update(this, engineClip, cars, Phase == ArcadePhase.Playing || Phase == ArcadePhase.Countdown, driveSpeed);

        protected override void OnDestroy()
        {
            base.OnDestroy();
            engine.Dispose();
        }

        public void DamageVehicle(ArcadeVehicle car, float damage)
        {
            if (!car.alive) return;
            car.health = Mathf.Max(0, car.health - damage);
            if (car.health <= 0)
            {
                car.alive = false; car.respawn = enemyRespawnSeconds;
                car.gameObject.SetActive(false);
                Announce(car.DriverName + " WRECKED!", Yellow);
            }
        }

        private void RespawnEnemy(ArcadeVehicle car)
        {
            Vector2[] candidates = { new Vector2(-3.7f, 2.5f), new Vector2(3.7f, 2.5f), new Vector2(3.7f, -3.4f), new Vector2(-3.7f, -3.4f) };
            Vector2 spawn = candidates.OrderByDescending(p => cars.Where(c => !c.computer && c.alive)
                .Select(c => Vector2.Distance(p, c.position)).DefaultIfEmpty(10).Min()).First();
            float score = car.score;
            car.ResetVehicle(spawn, spawn.x > 0 ? 180 : 0, PlayerCount);
            car.score = score; car.health = .75f;
        }

        private void EndRound(bool survived)
        {
            Survived = survived;
            int total = Mathf.RoundToInt(cars.Where(c => !c.computer).Sum(c => c.score));
            int best = Mathf.Max(total, PlayerPrefs.GetInt("WreckBest", 0));
            PlayerPrefs.SetInt("WreckBest", best);
            string stats = string.Join("\n", cars.Where(c => !c.computer).OrderByDescending(c => c.score).Select((c, i) => (i + 1) + ". " + c.DriverName + " "
                + ((int)c.score).ToString("00000") + (c.alive ? " SURVIVED" : " WRECKED")));
            Finish(survived ? "SURVIVED!" : "GAME OVER", stats + "\n\nTOTAL " + total + "\nBEST " + best + "\n\nHIT SIDES / REAR FOR BONUS POINTS.");
        }

        protected override void RefreshHud()
        {
            for (int i = 0; i < cars.Length; i++)
            {
                var car = cars[i];
                if (carLabels != null && i < carLabels.Length && carLabels[i] != null)
                {
                    carLabels[i].text = car.DriverName;
                    carLabels[i].color = car.body.color;
                    carLabels[i].transform.position = PixelPosition(car.position + Vector2.up * .68f);
                }
                if (healthBars != null && i < healthBars.Length && healthBars[i] != null)
                    healthBars[i].localScale = new Vector3(Mathf.Clamp01(car.health), 1, 1);
                if (ramTrails != null && i < ramTrails.Length && ramTrails[i] != null)
                {
                    ramTrails[i].enabled = Phase == ArcadePhase.Playing && car.alive && car.boost > 0;
                    ramTrails[i].transform.position = PixelPosition(car.position - car.Forward * .57f);
                    ramTrails[i].transform.rotation = Quaternion.Euler(0, 0, car.heading - 90);
                }
            }
            if (Phase == ArcadePhase.Attract)
                overlayHint.text = "1 / 2 SELECT PLAYERS  //  C INSERT COIN";
            header.text = "03 / SCRAPYARD SMASH";
            hud.text = "P1 " + Bar(cars[0].health, 6) + " " + ((int)cars[0].score).ToString("00000");
            if (PlayerCount == 2) hud.text += "   P2 " + Bar(cars[1].health, 6) + " " + ((int)cars[1].score).ToString("00000");
            arenaBoard.text = "TIME " + Mathf.CeilToInt(Remaining).ToString("00") + "   WRECKS " + Wrecks.ToString("00");
            footer.text = "P1 WASD+SPACE  P2 ARROWS+RCTRL\nRAM " + (cars[0].cooldown <= 0 ? "READY" : cars[0].cooldown.ToString("0.0") + "s")
                + (PlayerCount == 2 ? " / " + (cars[1].cooldown <= 0 ? "READY" : cars[1].cooldown.ToString("0.0") + "s") : "") + "   R RESTART";
        }
    }
}
