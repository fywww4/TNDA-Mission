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
        [Tooltip("None = random item crate (see ArenaItems.Roll); any other value always gives that item.")]
        public ArenaItem item = ArenaItem.None;
        public float respawn;
        public bool available;
    }

    // The GAME3 scene adapter: Inspector tuning, input and cabinet phases go into DemolitionMatch, and the match's
    // state and events come back out as car, prop, crate and rocket views, the explosion, notices and sounds.
    public class DemolitionGame : ArcadeGame
    {
        private static readonly MatchTuning Defaults = new MatchTuning();

        public ArcadeVehicle[] cars;
        public BreakableProp[] props;
        public ArenaPickup[] pickups = Array.Empty<ArenaPickup>();
        public ArcadeProjectile[] rockets = Array.Empty<ArcadeProjectile>();
        public TMP_Text arenaBoard, leaderboard;
        public TMP_Text[] carLabels;
        public Transform[] healthBars, shieldViews;

        [Header("Elimination arena tuning")]
        public float firstElimination = Defaults.firstElimination, secondElimination = Defaults.secondElimination, roundSeconds = Defaults.roundSeconds;
        public float driveSpeed = Defaults.driveSpeed;
        [Tooltip("Speed gained per second while gas is held (reaches top speed in about 2s).")]
        public float accelerationPerSecond = Defaults.accelerationPerSecond;
        [Tooltip("Speed lost per second after gas is released.")]
        public float coastDownPerSecond = Defaults.coastDownPerSecond;
        public float enemyRespawnSeconds = Defaults.respawnSeconds;
        public int wreckPenalty = Defaults.wreckPenalty, wreckBonus = Defaults.wreckBonus;
        public float shieldDuration = Defaults.shieldDuration, pickupRespawnSeconds = Defaults.pickupRespawnSeconds;

        [Header("AI aggression")]
        [Tooltip("Seconds after GO during which AI only targets other AI, steers around players and cannot damage them.")]
        public float aiGraceSeconds = Defaults.aiGraceSeconds;
        [Tooltip("How much AI prefers high-scoring targets over nearby ones (0 = nearest only).")]
        public float leaderBias = Defaults.leaderBias;
        [Tooltip("A car this many points ahead of an AI is hunted on purpose.")]
        public float leaderHuntMargin = Defaults.leaderHuntMargin;

        [Header("Cherry: crate item only the last-place car can roll")]
        [Tooltip("While active the car is faster and wrecks any car it drives into, but any ram or rocket wrecks it.")]
        public float cherryDuration = Defaults.cherryDuration;
        public float cherrySpeedMultiplier = Defaults.cherrySpeedMultiplier;
        [Range(0, 1)] public float trailingCherryChance = Defaults.trailingCherryChance;
        public SpriteRenderer[] cherryViews;

        [Header("Rocket explosion")]
        public SpriteRenderer[] explosions;
        public float explosionSeconds = .45f;
        private float[] explosionAge = Array.Empty<float>();
        private MaterialPropertyBlock solidBlock;

        [Header("Elimination spin-out")]
        [Tooltip("Eliminated cars spin and skid off the screen instead of vanishing.")]
        public float spinOutTurnsPerSecond = Defaults.spinOutTurnsPerSecond;
        public float spinOutStartSpeed = Defaults.spinOutStartSpeed, spinOutAcceleration = Defaults.spinOutAcceleration;

        [Header("Start messages")]
        [Tooltip("Shown one after another in the centre right after GO.")]
        public string[] startMessages = (string[])CabinetText.StartMessages.Clone();
        public float startMessageSeconds = 1.5f;
        private float countdownBaseSize;
        private bool showingStartMessage;

        [Header("Bumper-car knockback")]
        [Tooltip("Sliding push given to cars that touch, so they bounce apart instead of sticking.")]
        public float contactBump = Defaults.contactBump;
        [Tooltip("Extra push for the car that gets rammed, scaled by impact speed.")]
        public float ramBump = Defaults.ramBump;
        [Tooltip("How fast the sliding push fades (per second).")]
        public float bumpFriction = Defaults.bumpFriction;
        public bool HasCherry(ArcadeVehicle car) => car.HasCherry;
        public Vector2 arenaMin = Defaults.arenaMin;
        public Vector2 arenaMax = Defaults.arenaMax;
        public AudioClip engineClip;

        private readonly ArcadeEngineAudio engine = new ArcadeEngineAudio();
        private DemolitionMatch match;

        // Rebuilt when scene tooling swaps an array; the Inspector tuning is copied in on every use.
        private DemolitionMatch Match
        {
            get
            {
                if (match == null || match.Cars != cars || match.Props != props || match.Pickups != pickups || match.Rockets != rockets)
                    match = new DemolitionMatch(cars, props, pickups, rockets, new MatchTuning());
                var t = match.Tuning;
                t.firstElimination = firstElimination; t.secondElimination = secondElimination; t.roundSeconds = roundSeconds;
                t.driveSpeed = driveSpeed; t.accelerationPerSecond = accelerationPerSecond; t.coastDownPerSecond = coastDownPerSecond;
                t.respawnSeconds = enemyRespawnSeconds; t.wreckPenalty = wreckPenalty; t.wreckBonus = wreckBonus;
                t.shieldDuration = shieldDuration; t.pickupRespawnSeconds = pickupRespawnSeconds;
                t.aiGraceSeconds = aiGraceSeconds; t.leaderBias = leaderBias; t.leaderHuntMargin = leaderHuntMargin;
                t.cherryDuration = cherryDuration; t.cherrySpeedMultiplier = cherrySpeedMultiplier; t.trailingCherryChance = trailingCherryChance;
                t.spinOutTurnsPerSecond = spinOutTurnsPerSecond; t.spinOutStartSpeed = spinOutStartSpeed; t.spinOutAcceleration = spinOutAcceleration;
                t.contactBump = contactBump; t.ramBump = ramBump; t.bumpFriction = bumpFriction;
                t.arenaMin = arenaMin; t.arenaMax = arenaMax;
                return match;
            }
        }

        public float Remaining => Mathf.Max(0, roundSeconds - Elapsed);
        public float NextElimination => Match.NextElimination;
        public int Contenders => Match.Contenders;
        public int LivePlayers => cars.Count(c => !c.computer && c.alive);
        public bool Survived => Match.Survived;
        public int Wrecks => Match.Wrecks;
        public int Champion => Match.Champion;

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
            if (Match.EliminationStage == 0 && Elapsed >= demoEliminationSeconds)
            {
                Match.EliminateLowest();
                Present();
            }
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
            Match.Reset(demo ? 0 : PlayerCount, demo);
            foreach (var car in cars) car.body.color = car.index == 0 ? Cyan : car.index == 1 && !car.computer ? Yellow : White;
            for (int i = 0; i < cars.Length; i++)
                if (healthBars != null && i < healthBars.Length && healthBars[i] != null)
                    healthBars[i].GetComponentInChildren<SpriteRenderer>().color = cars[i].body.color;
            explosionAge = new float[explosions == null ? 0 : explosions.Length];
            for (int i = 0; i < explosionAge.Length; i++) explosionAge[i] = -1;
            if (explosions != null) foreach (var blast in explosions) if (blast != null) blast.gameObject.SetActive(false);
            SyncViews();
        }

        protected override void Tick(float dt, ArcadeInput p1, ArcadeInput p2)
        {
            Match.Tick(dt, p1, p2);
            Present();
        }

        public void DamageVehicle(ArcadeVehicle car, float damage)
        {
            Match.ApplyDamage(car, damage);
            Present();
        }

        // Shows what the match reported, in order, then forgets it. The attract demo plays the picture only: no notice
        // text and no sound, like a cabinet waiting for a coin.
        private void Present()
        {
            bool quiet = Phase == ArcadePhase.Attract;
            foreach (var e in Match.Events)
            {
                if (e is RocketHit hit) Explode(hit.Point);
                else if (e is MatchEnded end) ShowResults(end.Winner);
                if (!quiet) Cue(e);
            }
            Match.ClearEvents();
            SyncViews();
        }

        // The notice text and sound for one event.
        private void Cue(MatchEvent e)
        {
            switch (e)
            {
                case RocketHit _:
                    PlayHit(.75f);
                    break;
                case CarRammed ram:
                    if (ram.VictimHurt)
                        Announce(string.Format(CabinetText.Rammed, ram.Attacker.DriverName, ram.Multiplier.ToString("0.0")), ram.Attacker.body.color, 1);
                    PlayHit(Mathf.Clamp(.3f + ram.Impact * .1f, .3f, .75f));
                    break;
                case CherryStrike strike:
                    PlayHit(.9f);
                    if (strike.Wrecked)
                        Announce(string.Format(CabinetText.CherryWreck, strike.Attacker.DriverName, strike.Victim.DriverName), strike.Attacker.body.color, 2);
                    break;
                case WallHit _:
                    PlayHit(.2f);
                    break;
                case PropSmashed smash:
                    Sound(2);
                    if (!smash.ByRocket) Announce(string.Format(CabinetText.Smashed, smash.Car.DriverName, smash.Points), smash.Car.body.color, 1.2f);
                    break;
                case ItemPicked picked:
                    Sound(2);
                    if (!picked.Car.computer) Announce(ArenaItems.PickupMessage(picked.Car, picked.Item), picked.Car.body.color, 2);
                    break;
                case ItemUsed used:
                    Sound(2);
                    AnnounceItemUsed(used.Car, used.Item);
                    break;
                case RocketsBusy busy:
                    if (!busy.Car.computer) Announce(CabinetText.RocketBusy, Yellow, 1);
                    break;
                case ShieldBlocked blocked:
                    Announce(string.Format(CabinetText.ShieldBlocked, blocked.Car.DriverName), Cyan, .75f);
                    break;
                case VehicleWrecked wreck:
                    Announce(string.Format(CabinetText.Wrecked, wreck.Car.DriverName, wreck.Deducted, enemyRespawnSeconds.ToString("0")), Yellow, 2.5f);
                    break;
                case WreckBonus bonus:
                    Announce(string.Format(CabinetText.WreckBonus, bonus.Attacker.DriverName, bonus.Points), bonus.Attacker.body.color, 2);
                    break;
                case VehicleEliminated eliminated:
                    PlayHit(1);
                    Announce(string.Format(CabinetText.Eliminated, eliminated.Car.DriverName), Yellow, 4);
                    Sound(3);
                    break;
            }
        }

        private void AnnounceItemUsed(ArcadeVehicle car, ArenaItem item)
        {
            switch (item)
            {
                case ArenaItem.Shield: Announce(ArenaItems.UsedMessage(car, item, shieldDuration), Cyan, 2); break;
                case ArenaItem.Rocket: Announce(ArenaItems.UsedMessage(car, item, 0), Yellow, 1); break;
                case ArenaItem.Cherry: Announce(ArenaItems.UsedMessage(car, item, cherryDuration), car.body.color, 2.5f); break;
            }
        }

        private void ShowResults(ArcadeVehicle winner)
        {
            int best = Mathf.Max(PlayerPrefs.GetInt("WreckBest", 0), Mathf.RoundToInt(cars.Where(c => !c.computer).Max(c => c.score)));
            PlayerPrefs.SetInt("WreckBest", best);
            string standings = string.Join("\n", cars.OrderBy(c => c.FinalPlace)
                .Select(c => string.Format(CabinetText.Standing, c.FinalPlace, c.DriverName, Mathf.RoundToInt(c.score).ToString("00000"))));
            Finish(string.Format(CabinetText.Winner, winner.DriverName), standings
                + "\n\n" + string.Format(CabinetText.ResultsSummary, best, Wrecks) + "\n\n" + CabinetText.EliminationRule);
        }

        // Mirrors match state onto the scene: cars placed and shown or hidden, props and crates shown or hidden, rockets
        // placed and coloured.
        private void SyncViews()
        {
            foreach (var car in cars)
            {
                car.gameObject.SetActive(car.Visible);
                car.Sync();
            }
            foreach (var prop in props) prop.view.gameObject.SetActive(!prop.broken);
            foreach (var pickup in pickups) pickup.view.gameObject.SetActive(pickup.available);
            foreach (var rocket in rockets)
            {
                rocket.view.gameObject.SetActive(rocket.active);
                if (!rocket.active) continue;
                rocket.view.transform.position = PixelPosition(rocket.position);
                if (rocket.owner >= 0 && rocket.owner < cars.Length) rocket.view.color = cars[rocket.owner].body.color;
                if (rocket.velocity.sqrMagnitude > .0001f)
                    rocket.view.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(rocket.velocity.y, rocket.velocity.x) * Mathf.Rad2Deg - 90);
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
            Match.AdvanceSpinOuts(dt, sceneCamera.transform.position, sceneCamera.orthographicSize + 1);
            SyncViews();
            UpdateExplosions(dt);
            engine.Update(this, engineClip, cars, Phase == ArcadePhase.Playing || Phase == ArcadePhase.Countdown, driveSpeed);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            engine.Dispose();
        }

        private string DriverStatus(ArcadeVehicle car)
        {
            if (car.eliminated) return string.Format(CabinetText.StatusEliminated, car.FinalPlace);
            if (!car.alive) return string.Format(CabinetText.StatusRespawn, Mathf.CeilToInt(car.respawn));
            return ArenaItems.Status(car) ?? string.Format(CabinetText.StatusHealth, Mathf.CeilToInt(car.health * 100));
        }

        private string ActionStatus(ArcadeVehicle car) => car.eliminated ? CabinetText.ActionEliminated : !car.alive ? CabinetText.ActionRespawning
            : ArenaItems.ActionStatus(car);

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
                    carLabels[i].text = car.DriverName + ArenaItems.Tag(car.item);
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
                    float left = car.CherrySeconds;
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
            header.text = CabinetText.Title;
            arenaBoard.text = Phase == ArcadePhase.Results ? string.Format(CabinetText.Champion, cars[Champion].DriverName)
                : string.Format(CabinetText.NextElimination, Mathf.CeilToInt(Mathf.Max(0, NextElimination - Elapsed)), Contenders);
            var order = Phase == ArcadePhase.Results ? cars.OrderBy(c => c.FinalPlace)
                : cars.OrderBy(c => c.eliminated).ThenBy(c => c.eliminated ? c.FinalPlace : -c.score);
            if (leaderboard != null)
                leaderboard.text = string.Join("\n", order.Select((c, rank) =>
                    "<color=#" + ColorUtility.ToHtmlStringRGB(c.body.color) + ">" + (rank + 1) + " " + c.DriverName.PadRight(4)
                    + " " + Mathf.RoundToInt(c.score).ToString("00000") + "  " + DriverStatus(c) + "</color>"));
            hud.text = string.Format(CabinetText.PlayerStatus, cars[0].DriverName, ActionStatus(cars[0]))
                + (PlayerCount == 2 ? "\n" + string.Format(CabinetText.PlayerStatus, cars[1].DriverName, ActionStatus(cars[1])) : "")
                + "\n" + string.Format(CabinetText.TimeAndWrecks, Mathf.CeilToInt(Remaining), Wrecks)
                + (Phase == ArcadePhase.Playing && cars.Where(c => !c.computer).All(c => c.eliminated) ? "\n" + CabinetText.Spectating : "");
            // One 11px line fits under the arena; in 2P it alternates between the two players every 3 seconds.
            bool showP2 = PlayerCount == 2 && Mathf.FloorToInt(Time.unscaledTime / 3) % 2 == 1;
            footer.text = showP2 ? CabinetText.P2Controls : CabinetText.P1Footer;
            if (Phase == ArcadePhase.Attract) overlayHint.text = CabinetText.AttractHint;
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
