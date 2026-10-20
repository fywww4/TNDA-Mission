using System;
using System.Collections.Generic;
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
        // This prop's slot became the lava pool: it stays broken for the rest of the match.
        [NonSerialized] public bool molten;
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
        [Tooltip("Breakable obstacles appear at a random clear spot at the start and on every respawn.")]
        public bool randomPropSpots = Defaults.randomPropSpots;

        [Header("AI aggression")]
        [Tooltip("Seconds after GO during which AI only targets other AI, steers around players and cannot damage them.")]
        public float aiGraceSeconds = Defaults.aiGraceSeconds;
        [Tooltip("How much a computer cherry car prefers high-scoring prey over nearby ones (0 = nearest only).")]
        public float leaderBias = Defaults.leaderBias;
        [Tooltip("Computer top speed as a share of the players' until the first elimination, then from it on.")]
        public float aiTopSpeed = Defaults.aiTopSpeed, aiLateTopSpeed = Defaults.aiLateTopSpeed;
        [Tooltip("The 迷糊 computer car drives at this share of the other computer cars' top speed.")]
        public float wandererTopSpeedScale = Defaults.wandererTopSpeedScale;
        [Tooltip("追王 chases a leader this long, then scatters to props and crates (ramming its quarry ends the chase early).")]
        public float hunterChaseSeconds = Defaults.hunterChaseSeconds, hunterScatterSeconds = Defaults.hunterScatterSeconds;
        [Tooltip("Chance each 追王 chase goes for first place; otherwise second place.")]
        [Range(0, 1)] public float hunterLeaderChance = Defaults.hunterLeaderChance;

        [Header("Cherry: crate item only the last-place car can roll")]
        [Tooltip("While active the car is faster and wrecks any car it drives into, but any ram or rocket wrecks it.")]
        public float cherryDuration = Defaults.cherryDuration;
        public float cherrySpeedMultiplier = Defaults.cherrySpeedMultiplier;
        [Range(0, 1)] public float trailingCherryChance = Defaults.trailingCherryChance;
        [Tooltip("One per car: the held item's icon trails behind the car (index 0 shield, 1 rocket, 2 cherry, 3 oil in itemSprites).")]
        public SpriteRenderer[] itemViews;
        public Sprite[] itemSprites;
        [Tooltip("How far behind the car's centre the held-item icon trails.")]
        public float itemTrail = .7f;
        [Tooltip("While the cherry power lasts the car shows cyan and yellow stripes (not plain yellow, which is P2).")]
        public Color cherryStripeA = Cyan, cherryStripeB = Yellow;

        [Header("Lava and oil")]
        [Tooltip("The lava pool forms at this time (title demo: demoLavaSeconds) and stays; touching it wrecks a car, shield or not.")]
        public float lavaSeconds = Defaults.lavaSeconds, demoLavaSeconds = Defaults.demoLavaSeconds;
        public SpriteRenderer lavaView;
        private bool lavaJustFormed;
        [Tooltip("A car on an oil slick spins this long; the dropper scores oilBonus; an untouched slick dries after oilLifeSeconds.")]
        public float oilSlipSeconds = Defaults.oilSlipSeconds, oilLifeSeconds = Defaults.oilLifeSeconds;
        public int oilBonus = Defaults.oilBonus;

        [Header("Rocket explosion")]
        public SpriteRenderer[] explosions;
        public float explosionSeconds = .45f;
        private float[] explosionAge = Array.Empty<float>();
        private MaterialPropertyBlock solidBlock;

        [Header("Score pop-ups")]
        [Tooltip("One per car. Every score change pops up over the car at double size, floats up, then blinks out (no alpha: the palette stays four solid colours).")]
        public TMP_Text[] scorePopups;
        public float popupSeconds = .9f;
        private float[] popupAge = Array.Empty<float>();
        private int[] popupAmount = Array.Empty<int>();
        private Vector2[] popupOrigin = Array.Empty<Vector2>();
        private float popupBaseSize;
        private readonly List<Vector2> placedPopups = new List<Vector2>();

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
                t.aiGraceSeconds = aiGraceSeconds; t.leaderBias = leaderBias;
                t.aiTopSpeed = aiTopSpeed; t.aiLateTopSpeed = aiLateTopSpeed; t.wandererTopSpeedScale = wandererTopSpeedScale;
                t.lavaSeconds = lavaSeconds; t.demoLavaSeconds = demoLavaSeconds;
                t.oilSlipSeconds = oilSlipSeconds; t.oilLifeSeconds = oilLifeSeconds; t.oilBonus = oilBonus;
                t.hunterChaseSeconds = hunterChaseSeconds; t.hunterScatterSeconds = hunterScatterSeconds; t.hunterLeaderChance = hunterLeaderChance;
                t.cherryDuration = cherryDuration; t.cherrySpeedMultiplier = cherrySpeedMultiplier; t.trailingCherryChance = trailingCherryChance;
                t.spinOutTurnsPerSecond = spinOutTurnsPerSecond; t.spinOutStartSpeed = spinOutStartSpeed; t.spinOutAcceleration = spinOutAcceleration;
                t.contactBump = contactBump; t.ramBump = ramBump; t.bumpFriction = bumpFriction;
                t.arenaMin = arenaMin; t.arenaMax = arenaMax; t.randomPropSpots = randomPropSpots;
                return match;
            }
        }

        public float Remaining => Mathf.Max(0, roundSeconds - Elapsed);
        public float NextElimination => Match.NextElimination;
        public int Contenders => Match.Contenders;
        public bool Survived => Match.Survived;
        public int Wrecks => Match.Wrecks;
        public int Champion => Match.Champion;

        [Header("Continue (after a timed elimination)")]
        [Tooltip("A player eliminated at the first or second timed elimination gets this long to insert a coin (5) and press their item button to rejoin.")]
        public float continueSeconds = 10;
        [Tooltip("GAME OVER shows this long once no player is left, then the title demo returns.")]
        public float gameOverSeconds = 2;
        public GameObject continuePanel;
        public TMP_Text continueTitle, continueCount, continueHint;
        private readonly float[] continueLeft = { -1, -1 };
        private float gameOverLeft = -1;

        [Header("Victory picture (a player won)")]
        [Tooltip("Shown full screen for victorySeconds when a player is champion (the match stands still), then the results.")]
        public float victorySeconds = 3;
        public GameObject victoryScreen;
        public UnityEngine.UI.Image victoryImage;
        [Tooltip("Top-left line on the victory picture (你活下來了), in the winner's car colour.")]
        public TMP_Text victoryBanner, victoryScore;
        [Tooltip("Index 0: Player1's cyan car; 1: the same picture with Player2's yellow car.")]
        public Sprite[] victorySprites;
        private float victoryLeft = -1;
        private ArcadeVehicle victor;

        [Header("Attract mode (title screen demo)")]
        public GameObject attractScreen;
        public TMP_Text attractPrompt;
        [Tooltip("The demo eliminates last place at this time (to show the spin-out) and restarts at demoLoopSeconds.")]
        public float demoEliminationSeconds = 20, demoLoopSeconds = 30;
        private bool demo, startingDemo;

        protected override void Tick(float dt, ArcadePhase startPhase, ArcadeInput p1, ArcadeInput p2)
        {
            if (startPhase == ArcadePhase.Playing && victoryLeft >= 0)
            {
                // The match stands still behind the victory picture, then the results take over.
                victoryLeft -= dt;
                if (victoryLeft <= 0)
                {
                    victoryLeft = -1;
                    victoryScreen.SetActive(false);
                    ShowResults(victor);
                }
            }
            else if (startPhase == ArcadePhase.Playing)
            {
                Simulate(dt, p1, p2);
                UpdateContinues(dt, p1, p2);
            }
            else if (startPhase == ArcadePhase.Attract) TickDemo(dt);
            // Every phase: spin-outs fly off, views follow, explosions fade, and the engine loop follows the current
            // Phase (silent from the frame the match ends).
            Match.AdvanceSpinOuts(dt, sceneCamera.transform.position, sceneCamera.orthographicSize + 1);
            SyncViews();
            UpdateExplosions(dt);
            UpdateScorePopups(dt);
            engine.Update(this, engineClip, cars, Phase == ArcadePhase.Playing || Phase == ArcadePhase.Countdown, driveSpeed);
        }

        // Title screen: four AI cars play a silent demo match behind the coin prompt.
        private void TickDemo(float dt)
        {
            if (!demo || Elapsed >= demoLoopSeconds) StartDemo();
            Elapsed += dt;
            Simulate(dt, new ArcadeInput(), new ArcadeInput());
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
            continueLeft[0] = continueLeft[1] = gameOverLeft = -1;
            victoryLeft = -1;
            victor = null;
            if (victoryScreen != null) victoryScreen.SetActive(false);
            Match.Reset(demo ? 0 : PlayerCount, demo);
            foreach (var car in cars) car.body.color = car.index == 0 ? Cyan : car.index == 1 && !car.computer ? Yellow : White;
            for (int i = 0; i < cars.Length; i++)
                if (healthBars != null && i < healthBars.Length && healthBars[i] != null)
                    healthBars[i].GetComponentInChildren<SpriteRenderer>().color = cars[i].body.color;
            explosionAge = new float[explosions == null ? 0 : explosions.Length];
            for (int i = 0; i < explosionAge.Length; i++) explosionAge[i] = -1;
            if (explosions != null) foreach (var blast in explosions) if (blast != null) blast.gameObject.SetActive(false);
            int popups = scorePopups == null ? 0 : scorePopups.Length;
            popupAge = new float[popups];
            popupAmount = new int[popups];
            popupOrigin = new Vector2[popups];
            for (int i = 0; i < popups; i++)
            {
                popupAge[i] = -1;
                if (scorePopups[i] != null) scorePopups[i].gameObject.SetActive(false);
            }
            SyncViews();
        }

        private void Simulate(float dt, ArcadeInput p1, ArcadeInput p2)
        {
            Match.Tick(dt, p1, p2);
            Present();
        }

        public void DamageVehicle(ArcadeVehicle car, float damage)
        {
            Match.ApplyDamage(car, damage);
            Present();
        }

        // Shows what the match reported, in order, then forgets it. The attract demo plays the picture and the score
        // pop-ups (so passers-by see what scores) but no notice text and no sound, like a cabinet waiting for a coin.
        private void Present()
        {
            bool quiet = Phase == ArcadePhase.Attract;
            foreach (var e in Match.Events)
            {
                if (e is RocketHit hit) Explode(hit.Point);
                else if (e is ScoreChanged score) PopScore(score);
                // A player knocked out at the first or second timed elimination (45 or 80 s) may pay to continue; the final elimination ends the match.
                else if (e is VehicleEliminated knockedOut && !quiet && !demo && !knockedOut.Car.computer && !Match.Finished
                    && knockedOut.Car.index < continueLeft.Length) continueLeft[knockedOut.Car.index] = continueSeconds;
                else if (e is MatchEnded end)
                {
                    if (!end.Winner.computer && victoryScreen != null && victorySeconds > 0) ShowVictory(end.Winner);
                    else ShowResults(end.Winner);
                }
                if (!quiet) Cue(e);
            }
            Match.ClearEvents();
            lavaJustFormed = false;
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
                    PlayHit(Mathf.Clamp(.3f + ram.Impact * .1f, .3f, .75f));
                    break;
                case CherryStrike _:
                    PlayHit(.9f);
                    break;
                case WallHit _:
                    PlayHit(.2f);
                    break;
                case PropSmashed _:
                    Sound(2);
                    break;
                case ItemPicked picked:
                    Sound(2);
                    if (!picked.Car.computer) Announce(ArenaItems.PickupMessage(picked.Car, picked.Item), picked.Car.body.color, 2);
                    break;
                case ItemUsed used:
                    if (used.Item != ArenaItem.Oil) Sound(2); // lava and oil stay silent (2026-10-16)
                    AnnounceItemUsed(used.Car, used.Item);
                    break;
                case LavaFormed _:
                    Announce(CabinetText.LavaFormed, Yellow, 2.5f);
                    lavaJustFormed = true;
                    break;
                case RocketsBusy busy:
                    if (!busy.Car.computer) Announce(CabinetText.RocketBusy, Yellow, 1);
                    break;
                case ShieldBlocked blocked:
                    Announce(string.Format(CabinetText.ShieldBlocked, blocked.Car.DriverName), Cyan, .75f);
                    break;
                case VehicleRevived revived:
                    Sound(2);
                    Announce(string.Format(CabinetText.Revived, revived.Car.DriverName), revived.Car.body.color, 2);
                    break;
                case VehicleEliminated eliminated:
                    PlayHit(1);
                    // At 80 s the lava and the second elimination arrive together; keep both on the feedback line.
                    Announce(string.Format(CabinetText.Eliminated, eliminated.Car.DriverName)
                        + (lavaJustFormed ? "　" + CabinetText.LavaFormed : ""), Yellow, 4);
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
                case ArenaItem.Oil: Announce(ArenaItems.UsedMessage(car, item, 0), car.body.color, 1); break;
            }
        }

        private void ShowVictory(ArcadeVehicle winner)
        {
            victor = winner;
            victoryLeft = victorySeconds;
            if (victorySprites != null && victorySprites.Length > 0)
                victoryImage.sprite = victorySprites[winner.index == 1 && victorySprites.Length > 1 ? 1 : 0];
            if (victoryBanner != null)
            {
                victoryBanner.text = CabinetText.VictorySurvived;
                victoryBanner.color = winner.body.color;
            }
            victoryScore.text = string.Format(CabinetText.VictoryScore, Mathf.RoundToInt(winner.score).ToString("00000"));
            victoryScreen.SetActive(true);
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
            foreach (var prop in props)
            {
                prop.view.gameObject.SetActive(!prop.broken);
                if (!prop.broken) prop.view.position = PixelPosition(prop.point);
            }
            foreach (var pickup in pickups) pickup.view.gameObject.SetActive(pickup.available);
            if (lavaView != null)
            {
                lavaView.gameObject.SetActive(Match.LavaActive);
                if (Match.LavaActive)
                {
                    lavaView.transform.position = PixelPosition(Match.LavaPoint);
                    // Mirrored back and forth so the pool seems to bubble (and never reads as a still oil slick).
                    int bubble = Mathf.FloorToInt(Match.Elapsed * 3) % 4;
                    lavaView.flipX = bubble == 1 || bubble == 2;
                    lavaView.flipY = bubble >= 2;
                }
            }
            foreach (var rocket in rockets)
            {
                if (rocket.slickView != null)
                {
                    rocket.slickView.gameObject.SetActive(rocket.active && rocket.slick);
                    if (rocket.active && rocket.slick) rocket.slickView.transform.position = PixelPosition(rocket.position);
                }
                rocket.view.gameObject.SetActive(rocket.active && !rocket.slick);
                if (!rocket.active || rocket.slick) continue;
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

        // Each knocked-out player's 10-second continue: the item button spends a coin and rejoins. Once no player is left
        // and no countdown runs, GAME OVER shows briefly and the title demo returns.
        private void UpdateContinues(float dt, ArcadeInput p1, ArcadeInput p2)
        {
            if (Phase != ArcadePhase.Playing || Match.Finished)
            {
                continueLeft[0] = continueLeft[1] = gameOverLeft = -1;
                return;
            }
            for (int seat = 0; seat < continueLeft.Length; seat++)
            {
                if (continueLeft[seat] < 0) continue;
                if (seat == 0 ? p1.actionPressed : p2.actionPressed)
                {
                    if (SpendCredits(1))
                    {
                        continueLeft[seat] = -1;
                        Match.Revive(cars[seat]);
                        Present();
                        continue;
                    }
                    Announce(CabinetText.InsertCoinFirst, Yellow, 1.5f);
                }
                int shown = Mathf.CeilToInt(continueLeft[seat]);
                continueLeft[seat] -= dt;
                if (continueLeft[seat] <= 0) continueLeft[seat] = -1;
                else if (Mathf.CeilToInt(continueLeft[seat]) != shown) Sound(0);
            }
            bool waiting = continueLeft[0] >= 0 || continueLeft[1] >= 0;
            if (gameOverLeft < 0 && !waiting && cars.Where(c => !c.computer).All(c => c.eliminated)) gameOverLeft = gameOverSeconds;
            if (gameOverLeft >= 0 && (gameOverLeft -= dt) <= 0) PreparePreview();
        }

        private void ShowContinue()
        {
            if (continuePanel == null) return;
            int seat = continueLeft[0] >= 0 ? 0 : continueLeft[1] >= 0 ? 1 : -1;
            bool over = seat < 0 && gameOverLeft >= 0;
            continuePanel.SetActive(Phase == ArcadePhase.Playing && (seat >= 0 || over));
            if (seat >= 0)
            {
                string key = seat == 0 ? CabinetText.P1ContinueKey : CabinetText.P2ContinueKey;
                continueTitle.text = string.Format(CabinetText.ContinueTitle, cars[seat].DriverName);
                continueCount.text = Mathf.CeilToInt(continueLeft[seat]).ToString();
                continueHint.text = Credits > 0 ? string.Format(CabinetText.ContinuePress, key, Credits) : string.Format(CabinetText.ContinueNeedCoin, key);
            }
            else if (over)
            {
                continueTitle.text = CabinetText.GameOver;
                continueCount.text = continueHint.text = "";
            }
        }

        // Points scored or lost show over the car that changed score. Changes in quick succession (a ram that also
        // wrecks) add up in one pop-up; a later change replaces it.
        private void PopScore(ScoreChanged change)
        {
            int i = change.Car.index;
            if (i >= popupAge.Length || scorePopups[i] == null) return;
            bool merge = popupAge[i] >= 0 && popupAge[i] < .15f;
            popupAmount[i] = merge ? popupAmount[i] + change.Amount : change.Amount;
            if (!merge) popupOrigin[i] = change.Point;
            popupAge[i] = 0;
            var text = scorePopups[i];
            text.text = string.Format(popupAmount[i] >= 0 ? CabinetText.ScoreGain : CabinetText.ScoreLoss, Mathf.Abs(popupAmount[i]));
            text.color = change.Car.body.color;
            text.gameObject.SetActive(true);
        }

        private void UpdateScorePopups(float dt)
        {
            placedPopups.Clear();
            for (int i = 0; i < popupAge.Length; i++)
            {
                var text = scorePopups[i];
                if (popupAge[i] < 0 || text == null) continue;
                popupAge[i] += dt;
                float t = popupAge[i] / popupSeconds;
                if (t >= 1) { popupAge[i] = -1; text.gameObject.SetActive(false); continue; }
                if (popupBaseSize <= 0) popupBaseSize = text.fontSize;
                // Pops in at double size (still a whole multiple of the 11px pixel font), then floats up 16px and
                // blinks out. Whole-pixel positions keep the pixel font sharp.
                text.fontSize = popupAge[i] < .12f ? popupBaseSize * 2 : popupBaseSize;
                Vector2 screen = (popupOrigin[i] + Vector2.up * .88f - (Vector2)sceneCamera.transform.position) * 32;
                var spot = new Vector2(Mathf.Round(screen.x), Mathf.Round(screen.y + 6 + 16 * t));
                // Cars that touch score together; stack their pop-ups a line apart instead of overlapping.
                while (placedPopups.Any(p => Mathf.Abs(p.x - spot.x) < 44 && Mathf.Abs(p.y - spot.y) < 13)) spot.y += 13;
                placedPopups.Add(spot);
                text.rectTransform.anchoredPosition = spot;
                text.enabled = t < .6f || Mathf.FloorToInt(popupAge[i] * 16) % 2 == 0;
            }
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
                if (attractPrompt != null)
                {
                    attractPrompt.text = Credits == 0 ? CabinetText.CoinPrompt
                        : string.Format(Credits == 1 ? CabinetText.CoinPromptOne : CabinetText.CoinPromptTwo, Credits);
                    attractPrompt.enabled = Mathf.FloorToInt(Time.unscaledTime * 2) % 2 == 0;
                }
            }
            for (int i = 0; i < cars.Length; i++)
            {
                var car = cars[i];
                if (carLabels != null && i < carLabels.Length && carLabels[i] != null)
                {
                    carLabels[i].text = car.DriverName;
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
                if (itemViews != null && i < itemViews.Length && itemViews[i] != null)
                {
                    // The held item rides behind the car, upright so it reads at a glance (like a kart racer's item).
                    int slot = car.item == ArenaItem.Shield ? 0 : car.item == ArenaItem.Rocket ? 1 : car.item == ArenaItem.Cherry ? 2
                        : car.item == ArenaItem.Oil ? 3 : -1;
                    bool holding = car.alive && slot >= 0 && itemSprites != null && slot < itemSprites.Length;
                    itemViews[i].gameObject.SetActive(holding);
                    if (holding)
                    {
                        itemViews[i].sprite = itemSprites[slot];
                        itemViews[i].transform.position = PixelPosition(car.position - car.Forward * itemTrail);
                        itemViews[i].transform.rotation = Quaternion.identity;
                    }
                }
                // Cherry power: the car turns cyan-and-yellow striped, blinking in the last two seconds as a warning.
                bool cherryOn = HasCherry(car) && (car.CherrySeconds > 2 || Mathf.FloorToInt(Elapsed * 8) % 2 == 0);
                if (solidBlock == null) solidBlock = new MaterialPropertyBlock();
                car.body.GetPropertyBlock(solidBlock);
                solidBlock.SetFloat("_Solid", cherryOn ? 1 : 0);
                solidBlock.SetColor("_SolidColor", cherryStripeA);
                solidBlock.SetColor("_StripeColor", cherryStripeB);
                car.body.SetPropertyBlock(solidBlock);
            }
            header.text = CabinetText.Title;
            arenaBoard.text = Phase == ArcadePhase.Results ? string.Format(CabinetText.Champion, cars[Champion].DriverName)
                : string.Format(CabinetText.NextElimination, Mathf.CeilToInt(Mathf.Max(0, NextElimination - Elapsed)), Contenders);
            var order = Phase == ArcadePhase.Results ? cars.OrderBy(c => c.FinalPlace)
                : cars.OrderBy(c => c.eliminated).ThenBy(c => c.eliminated ? c.FinalPlace : -c.score);
            if (leaderboard != null)
                leaderboard.text = string.Join("\n", order.Select((c, rank) =>
                    "<color=#" + ColorUtility.ToHtmlStringRGB(c.body.color) + ">" + (rank + 1) + " " + c.DriverName.PadRight(7)
                    + " " + Mathf.RoundToInt(c.score).ToString("00000") + "  " + DriverStatus(c) + "</color>"));
            hud.text = string.Format(CabinetText.PlayerStatus, cars[0].DriverName, ActionStatus(cars[0]))
                + (PlayerCount == 2 ? "\n" + string.Format(CabinetText.PlayerStatus, cars[1].DriverName, ActionStatus(cars[1])) : "")
                + "\n" + string.Format(CabinetText.TimeAndWrecks, Mathf.CeilToInt(Remaining), Wrecks)
                + (Phase == ArcadePhase.Playing && cars.Where(c => !c.computer).All(c => c.eliminated) ? "\n" + CabinetText.Spectating : "");
            // One 11px line fits under the arena; in 2P it alternates between the two players every 3 seconds.
            bool showP2 = PlayerCount == 2 && Mathf.FloorToInt(Time.unscaledTime / 3) % 2 == 1;
            footer.text = showP2 ? CabinetText.P2Controls : CabinetText.P1Controls;
            if (Phase == ArcadePhase.Attract) overlayHint.text = CabinetText.AttractHint;
            ShowStartMessages();
            ShowContinue();
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
