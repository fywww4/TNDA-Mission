using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

namespace TNDA.Prototype
{
    // A presentation that draws the arena through its own camera(s) (IsoStage, Mode7Stage) tells the arena where the
    // screen-space name tags and score pop-ups go: a world point on the arena floor in cabinet units (centre origin)
    // for one view. Views = how many views show the arena right now; each gets its own set of tags.
    public interface ICabinetProjection
    {
        int Views { get; }
        // False when the point is not on that view (behind its camera, or outside its share of the screen).
        bool ToCabinet(Vector2 world, int view, out Vector2 cabinet);
    }

    [Serializable]
    public class BreakableProp
    {
        public Transform view;
        public Vector2 point;
        public int points = 100;
        public float respawn;
        public bool broken;
        // A bump too slow to smash it dents the prop until it breaks; art (when set) shows intact or dented.
        public bool dented;
        public SpriteRenderer art;
        public Sprite intactSprite, dentedSprite;
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
        public ArenaObstacle[] obstacles = Array.Empty<ArenaObstacle>();
        [Header("Fixed terrain")]
        public float fenceRespawnSeconds = Defaults.fenceRespawnSeconds;
        public float rampSpeed = Defaults.rampSpeed, jumpSeconds = Defaults.jumpSeconds, jumpHeight = Defaults.jumpHeight;
        [Header("Spike balls (ticket 21)")]
        [Tooltip("Rolling spike balls: where and which way each starts while randomPropSpots is off (otherwise a random clear spot and direction).")]
        public SpikeBall[] spikeBalls = Array.Empty<SpikeBall>();
        [Tooltip("Rolling speed, and the share of durability a car loses when it drives into a ball (or one rolls into it).")]
        public float spikeRollSpeed = Defaults.spikeRollSpeed;
        [Range(0, 1)] public float spikeDamage = Defaults.spikeDamage;
        [Tooltip("A knocked ball's speed per unit of impact and its cap; from spikeFlyImpact it flies and wrecks the next car it reaches, slowing by spikeFlyFriction per second back to a roll.")]
        public float spikeLaunchPerSpeed = Defaults.spikeLaunchPerSpeed, spikeLaunchMax = Defaults.spikeLaunchMax;
        public float spikeFlyImpact = Defaults.spikeFlyImpact, spikeFlyFriction = Defaults.spikeFlyFriction;
        [Tooltip("SpikeBall.png: one row of square rolling frames (rolling to the right, ball on the bottom row). Empty = a code-drawn placeholder.")]
        public Texture2D spikeBallSheet;
        [Tooltip("Sheet pixels per world unit (44: the 28-pixel ball is about 0.64 wide, a little over half a car).")]
        public float spikeBallPixelsPerUnit = 44;
        private Sprite[] spikeBallFrames = Array.Empty<Sprite>();
        [Tooltip("Distance a ball rolls per animation frame.")]
        public float spikeFrameDistance = .2f;
        // One view per spike ball, made at runtime like the sparks; Mode7Stage stands them up.
        [NonSerialized] public SpriteRenderer[] spikeBallViews = Array.Empty<SpriteRenderer>();
        // 零件: one view per DemolitionMatch.Parts slot, made and moved by Mode7Debris; Mode7Stage stands them up.
        [NonSerialized] public SpriteRenderer[] partViews = Array.Empty<SpriteRenderer>();
        private Sprite spikePlaceholder;
        public ArcadeProjectile[] rockets = Array.Empty<ArcadeProjectile>();
        public TMP_Text arenaBoard, leaderboard;
        public Mode7StandingsHud standingsHud;
        public TMP_Text[] carLabels;
        public Transform[] healthBars, shieldViews;

        [Header("Elimination arena tuning")]
        [Tooltip("Timed eliminations in order: at this many seconds knock out this many of the lowest scores. The last step is the final and the end of the round: everyone but the champion is knocked out.")]
        public EliminationStep[] eliminationSchedule = MatchTuning.DefaultEliminations();
        public float RoundSeconds => Match.Tuning.RoundSeconds;
        // Scrap parts lying on the floor (or being pulled in), for the views.
        public IReadOnlyList<ScrapPart> Parts => Match.Parts;
        public float PartLifeSeconds => Match.Tuning.partLifeSeconds;
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

        [Header("Car colours and the leader's crown")]
        [Tooltip("P1 cyan and P2 yellow are solid; P3 (SAN) and P4 (Zhima) are striped so every car reads apart from above.")]
        public Color p3StripeA = Cyan, p3StripeB = White;
        public Color p4StripeA = Color.black, p4StripeB = White;
        [Tooltip("Floats over the car currently in first place (hidden while every score is 0).")]
        public SpriteRenderer crownView;
        [Tooltip("Height of the crown's centre above the car, clear of the name tag below it.")]
        public float crownHeight = 1.5f;

        [Header("Oil")]
        [Tooltip("A car on an oil slick spins this long; the dropper scores oilBonus; an untouched slick dries after oilLifeSeconds.")]
        public float oilSlipSeconds = Defaults.oilSlipSeconds, oilLifeSeconds = Defaults.oilLifeSeconds;
        public int oilBonus = Defaults.oilBonus;

        [Header("Rocket explosion")]
        public SpriteRenderer[] explosions;
        public float explosionSeconds = .45f;
        private float[] explosionAge = Array.Empty<float>(), explosionScale = Array.Empty<float>();
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
        [Tooltip("Extra slide for the rammed car per unit of closing speed: a light tap barely pushes, a fast ram sends it flying.")]
        public float ramBumpPerSpeed = Defaults.ramBumpPerSpeed;
        [Tooltip("The rammed car's extra slide never exceeds this, so it stays in the arena.")]
        public float ramBumpMax = Defaults.ramBumpMax;
        [Tooltip("The rammer's rebound per unit of closing speed (capped at a third of ramBumpMax).")]
        public float rammerReboundPerSpeed = Defaults.rammerReboundPerSpeed;
        [Tooltip("How fast the sliding push fades (per second).")]
        public float bumpFriction = Defaults.bumpFriction;

        [Header("Side hits and scrapes")]
        [Tooltip("A side hit spins the victim this many degrees at impact 1, up to the max at sideSpinFullSpeed.")]
        public float sideSpinMinDegrees = Defaults.sideSpinMinDegrees, sideSpinMaxDegrees = Defaults.sideSpinMaxDegrees;
        public float sideSpinFullSpeed = Defaults.sideSpinFullSpeed;
        [Tooltip("The side-hit spin's length; the victim is stunned meanwhile.")]
        public float sideSpinSeconds = Defaults.sideSpinSeconds;
        [Tooltip("Scrape: the two headings' dot product at least this (0.87 is about 30 degrees apart).")]
        [Range(0, 1)] public float scrapeAlignment = Defaults.scrapeAlignment;
        [Tooltip("Scrape: the contact line's share along either heading below this (side to side, not nose to tail).")]
        [Range(0, 1)] public float scrapeSideways = Defaults.scrapeSideways;
        [Tooltip("Scrape: the faster car's speed must reach this.")]
        public float scrapeMinSpeed = Defaults.scrapeMinSpeed;
        [Tooltip("Scrape: outward push for both cars, the share of speed both keep, and the share of a ram's damage and points.")]
        public float scrapeBump = Defaults.scrapeBump;
        [Range(0, 1)] public float scrapeSlowdown = Defaults.scrapeSlowdown, scrapeMultiplier = Defaults.scrapeMultiplier;

        [Header("Impact feel: shake, sparks, wreck explosion")]
        public ImpactFeelTuning impactFeel = new ImpactFeelTuning();
        // Runtime spark bursts (code-drawn four-colour star), styled like the explosions; Mode7Stage stands them up.
        [NonSerialized] public SpriteRenderer[] sparks = Array.Empty<SpriteRenderer>();
        private float[] sparkAge = Array.Empty<float>();
        private const int SparkSlots = 6;
        public bool HasCherry(ArcadeVehicle car) => car.HasCherry;
        // Ticket 16: the pattern drawn over this car. Only P1/P2 driven by a person wear one; cherry stripes cover it.
        public CarDecal ShownDecal(ArcadeVehicle car) =>
            car == null || car.computer || car.HasCherry ? CarDecal.None : Decals.Equipped(car.index);
        public Vector2 arenaMin = Defaults.arenaMin;
        public Vector2 arenaMax = Defaults.arenaMax;
        public AudioClip engineClip;

        private readonly ArcadeEngineAudio engine = new ArcadeEngineAudio();
        private DemolitionMatch match;
        private ArcadeVehicle[] matchSource;

        // Rebuilt when scene tooling swaps an array; the Inspector tuning is copied in on every use.
        private DemolitionMatch Match
        {
            get
            {
                int count = ActiveChallenge == null ? cars.Length : Mathf.Min(cars.Length, ActiveChallenge.CarCount);
                if (match == null || matchSource != cars || match.Cars.Length != count
                    || match.Props != props || match.Pickups != pickups || match.Rockets != rockets || match.Obstacles != obstacles || match.SpikeBalls != spikeBalls)
                {
                    match = new DemolitionMatch(count == cars.Length ? cars : cars.Take(count).ToArray(), props, pickups, rockets, new MatchTuning(), obstacles, spikeBalls);
                    matchSource = cars;
                }
                var t = match.Tuning;
                if (eliminationSchedule == null || eliminationSchedule.Length == 0) eliminationSchedule = MatchTuning.DefaultEliminations();
                t.eliminations = eliminationSchedule;
                t.ResetChallengeSwitches();
                ActiveChallenge?.ApplyRules(t); // a challenge being played changes the rules; the title demo never does
                t.driveSpeed = driveSpeed; t.accelerationPerSecond = accelerationPerSecond; t.coastDownPerSecond = coastDownPerSecond;
                t.playerUpgrades = Upgrades;
                t.respawnSeconds = enemyRespawnSeconds; t.wreckPenalty = wreckPenalty; t.wreckBonus = wreckBonus;
                t.shieldDuration = shieldDuration; t.pickupRespawnSeconds = pickupRespawnSeconds;
                t.aiGraceSeconds = aiGraceSeconds; t.leaderBias = leaderBias;
                t.aiTopSpeed = aiTopSpeed; t.aiLateTopSpeed = aiLateTopSpeed; t.wandererTopSpeedScale = wandererTopSpeedScale;
                t.oilSlipSeconds = oilSlipSeconds; t.oilLifeSeconds = oilLifeSeconds; t.oilBonus = oilBonus;
                t.hunterChaseSeconds = hunterChaseSeconds; t.hunterScatterSeconds = hunterScatterSeconds; t.hunterLeaderChance = hunterLeaderChance;
                t.cherryDuration = cherryDuration; t.cherrySpeedMultiplier = cherrySpeedMultiplier; t.trailingCherryChance = trailingCherryChance;
                t.spinOutTurnsPerSecond = spinOutTurnsPerSecond; t.spinOutStartSpeed = spinOutStartSpeed; t.spinOutAcceleration = spinOutAcceleration;
                t.contactBump = contactBump; t.ramBumpPerSpeed = ramBumpPerSpeed; t.ramBumpMax = ramBumpMax;
                t.rammerReboundPerSpeed = rammerReboundPerSpeed; t.bumpFriction = bumpFriction;
                t.sideSpinMinDegrees = sideSpinMinDegrees; t.sideSpinMaxDegrees = sideSpinMaxDegrees;
                t.sideSpinFullSpeed = sideSpinFullSpeed; t.sideSpinSeconds = sideSpinSeconds;
                t.scrapeAlignment = scrapeAlignment; t.scrapeSideways = scrapeSideways; t.scrapeMinSpeed = scrapeMinSpeed;
                t.scrapeBump = scrapeBump; t.scrapeSlowdown = scrapeSlowdown; t.scrapeMultiplier = scrapeMultiplier;
                t.arenaMin = arenaMin; t.arenaMax = arenaMax; t.randomPropSpots = randomPropSpots;
                t.fenceRespawnSeconds = fenceRespawnSeconds; t.rampSpeed = rampSpeed;
                t.jumpSeconds = jumpSeconds; t.jumpHeight = jumpHeight;
                t.spikeRollSpeed = spikeRollSpeed; t.spikeDamage = spikeDamage; t.spikeLaunchPerSpeed = spikeLaunchPerSpeed;
                t.spikeLaunchMax = spikeLaunchMax; t.spikeFlyImpact = spikeFlyImpact; t.spikeFlyFriction = spikeFlyFriction;
                return match;
            }
        }

        public float Remaining => Mathf.Max(0, RoundSeconds - Elapsed);
        public float NextElimination => Match.NextElimination;
        public int Contenders => Match.Contenders;
        public bool Survived => Match.Survived;
        public int Wrecks => Match.Wrecks;
        public int Champion => Match.Champion;

        [Header("Continue (after a timed elimination)")]
        [Tooltip("A player eliminated at a timed elimination before the final gets this long to press START and rejoin, once per match.")]
        public float continueSeconds = 10;
        [Tooltip("GAME OVER shows this long once no player is left, then the title demo returns.")]
        public float gameOverSeconds = 2;
        [System.Serializable]
        public class ContinuePanelView
        {
            public GameObject panel;
            public TMP_Text title, count, hint;
        }
        [Tooltip("One panel per player seat: index 0 is P1's, 1 is P2's. The stage places each in its own half in 2P.")]
        public ContinuePanelView[] continuePanels = new ContinuePanelView[2];
        private ContinueCountdowns continues;
        private ContinueCountdowns Continues => continues ??= new ContinueCountdowns(2, continueSeconds);
        private float gameOverLeft = -1;
        // GAME OVER is showing on the first panel instead of a seat's countdown (the stage then centres it).
        public bool ContinueShowsGameOver => !Continues.AnyWaiting && gameOverLeft >= 0;

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

        [Header("Title: mode tabs and the challenge list")]
        public TMP_Text attractRule, attractModes, attractChallenges;
        [Header("Controls and speed HUD")]
        public TMP_Text attractControlsType;
        public TMP_Text[] attractControls;
        [UnityEngine.Serialization.FormerlySerializedAs("gearLabels")]
        public TMP_Text[] speedLabels;
        [Tooltip("Backing and list shown only while the title is on challenge mode.")]
        public GameObject attractChallengePanel;

        [Header("Saved gold")]
        public TMP_Text attractGold, resetSaveHint;
        public int RoundGold { get; private set; }
        private bool goldAwarded, resetSaveLatched;
        private float resetSaveHeldSeconds;

        protected override void Tick(float dt, ArcadePhase startPhase, ArcadeInput p1, ArcadeInput p2)
        {
            if (standingsHud != null) standingsHud.ReadInput(p1, p2);
            UpdateSaveReset(dt, p1.resetSaveHeld);
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

        // Title screen: all seats play a silent AI demo match behind the coin prompt.
        private void TickDemo(float dt)
        {
            if (!demo || Elapsed >= demoLoopSeconds) StartDemo();
            Elapsed += dt;
            Simulate(dt, new ArcadeInput(), new ArcadeInput());
            if (Match.CarsKnockedOut == 0 && Elapsed >= demoEliminationSeconds)
            {
                Match.EliminateNextStep();
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
            if (standingsHud != null) standingsHud.ReadInput(default, default);
            demo = startingDemo;
            goldAwarded = false;
            RoundGold = 0;
            Continues.NewMatch();
            gameOverLeft = -1;
            victoryLeft = -1;
            victor = null;
            if (victoryScreen != null) victoryScreen.SetActive(false);
            Match.Reset(demo ? 0 : PlayerCount, demo);
            foreach (var car in cars) car.body.color = Livery(car.index).body;
            for (int i = 0; i < cars.Length; i++)
                if (healthBars != null && i < healthBars.Length && healthBars[i] != null)
                    healthBars[i].GetComponentInChildren<SpriteRenderer>().color = cars[i].body.color;
            explosionAge = new float[explosions == null ? 0 : explosions.Length];
            for (int i = 0; i < explosionAge.Length; i++) explosionAge[i] = -1;
            if (explosions != null) foreach (var blast in explosions) if (blast != null) blast.gameObject.SetActive(false);
            explosionScale = new float[explosionAge.Length];
            MakeSparks();
            MakeSpikeBallViews();
            for (int i = 0; i < sparks.Length; i++) { sparkAge[i] = -1; sparks[i].gameObject.SetActive(false); }
            int popups = scorePopups == null ? 0 : scorePopups.Length;
            popupAge = new float[popups];
            popupAmount = new int[popups];
            popupOrigin = new Vector2[popups];
            for (int i = 0; i < popups; i++)
            {
                popupAge[i] = -1;
                if (scorePopups[i] != null) scorePopups[i].gameObject.SetActive(false);
            }
            foreach (var copies in extraPopups)
                foreach (var copy in copies) if (copy != null) copy.gameObject.SetActive(false);
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

        // Presentation-only listeners also receive wrecks in the silent attract demo.
        public event System.Action WreckPresented;
        public event System.Action<ArenaObstacle> FencePresented;
        // A player's car spun out on oil: smear that player's view (Mode7Debris). Never in the attract demo.
        public event System.Action<ArcadeVehicle> OilPresented;
        // A player's chase view should shake (ticket 18). Never in the attract demo.
        public event System.Action<ShakeKick> ShakePresented;

        // Shows what the match reported, in order, then forgets it. The attract demo plays the picture and the score
        // pop-ups (so passers-by see what scores) but no notice text and no sound, like a cabinet waiting for a coin.
        private void Present()
        {
            bool quiet = Phase == ArcadePhase.Attract;
            foreach (var e in Match.Events)
            {
                if (e is VehicleWrecked) WreckPresented?.Invoke();
                if (e is FenceSmashed fence) FencePresented?.Invoke(fence.Obstacle);
                if (e is CarSlipped slip && !slip.Car.computer && !quiet && !demo) OilPresented?.Invoke(slip.Car);
                if (!quiet && !demo && ShakePresented != null)
                    foreach (var kick in ImpactFeel.Kicks(e, Match.Tuning.smashSpeed, impactFeel)) ShakePresented(kick);
                if (ImpactFeel.TrySparks(e, Match.Tuning.smashSpeed, out var sparkAt)) Spark(sparkAt);
                if (e is VehicleWrecked wrecked) Explode(wrecked.Car.position, impactFeel.wreckExplosionScale);
                // A rocket that wrecks its target shows only the wreck's bigger explosion.
                if (e is RocketHit hit && !Match.Events.Any(w => w is VehicleWrecked v && v.Car == hit.Victim)) Explode(hit.Point);
                else if (e is ScoreChanged score) PopScore(score);
                // A player knocked out before the final (the 40 s or 80 s elimination in arcade mode) may pay to continue; the final elimination ends the match.
                else if (e is VehicleEliminated knockedOut && !quiet && !demo) Continues.Offer(knockedOut.Car, Match.Finished);
                else if (e is MatchEnded end)
                {
                    AwardGold();
                    if (ActiveChallenge != null) ShowChallengeResults();
                    else if (!end.Winner.computer && victoryScreen != null && victorySeconds > 0) ShowVictory(end.Winner);
                    else ShowResults(end.Winner);
                }
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
                    PlayHit(Mathf.Clamp(.3f + ram.Impact * .1f, .3f, .75f));
                    break;
                case CherryStrike _:
                    PlayHit(.9f);
                    break;
                case SpikeBallHit spiked:
                    PlayHit(Mathf.Clamp(.4f + spiked.Impact * .1f, .4f, .8f));
                    break;
                case SpikeBallStruck _:
                    PlayHit(.9f);
                    break;
                case WallHit _:
                    PlayHit(.2f);
                    break;
                case PropSmashed _:
                case FenceSmashed _:
                    Sound(2);
                    break;
                case ItemPicked picked:
                    Sound(2);
                    if (!picked.Car.computer) Announce(ArenaItems.PickupMessage(picked.Car, picked.Item), picked.Car.body.color, 2);
                    break;
                case ItemUsed used:
                    if (used.Item != ArenaItem.Oil) Sound(2); // oil stays silent (2026-10-16)
                    AnnounceItemUsed(used.Car, used.Item);
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
            string standings = string.Join("\n", Match.Cars.OrderBy(c => c.FinalPlace)
                .Select(c => string.Format(CabinetText.Standing, c.FinalPlace, c.DriverName, Mathf.RoundToInt(c.score).ToString("00000"))));
            Finish(string.Format(CabinetText.Winner, winner.DriverName), standings
                + "\n" + string.Format(CabinetText.ResultsSummary, best, Wrecks) + "\n"
                + string.Join("／", eliminationSchedule.Select((step, i) => i == eliminationSchedule.Length - 1
                    ? string.Format(CabinetText.EliminationFinal, step.at)
                    : string.Format(CabinetText.EliminationStep, step.at, step.count)))
                + "\n" + GoldSummary);
        }

        // A challenge counts only the player's own result (its score, or 不被撞毀's durability left), whoever wins the
        // final; the best record is saved first so the panel can say whether it was beaten.
        private void ShowChallengeResults()
        {
            var challenge = ActiveChallenge;
            int score = challenge.ScoreOf(Match, cars[0]);
            bool newBest = Records.Submit(challenge, score);
            string best = string.Format(CabinetText.ChallengeBestLine, challenge.RecordText(Records.Best(challenge)))
                + (newBest ? "　" + CabinetText.ChallengeNewBest : "");
            Finish(challenge.Name, string.Format(CabinetText.ChallengeScoreLine, challenge.RecordText(score)) + "\n" + best
                + "\n" + GoldSummary, CabinetText.ChallengeResultsHint);
        }

        private string GoldSummary => string.Format(CabinetText.GoldEarned, RoundGold)
            + "\n" + string.Format(CabinetText.GoldTotal, Gold);

        // Called once at the actual end, before the victory picture or GAME OVER. A continue never settles a round.
        private void AwardGold()
        {
            if (goldAwarded || demo || Phase != ArcadePhase.Playing) return;
            RoundGold = Wallet.AwardRound(cars.Where(c => !c.computer).Select(c => (c.FinalPlace, c.score)).ToArray());
            goldAwarded = true;
        }

        private void UpdateSaveReset(float dt, bool held)
        {
            if (Phase != ArcadePhase.Attract || !held)
            {
                resetSaveHeldSeconds = 0;
                resetSaveLatched = held; // Holding during play cannot pre-charge a title-screen reset.
                return;
            }
            if (resetSaveLatched) return;
            resetSaveHeldSeconds += dt;
            if (resetSaveHeldSeconds < 3) return;
            Wallet.Clear();
            Records.Clear();
            Upgrades.Clear();
            Decals.Clear();
            PlayerPrefs.DeleteKey("WreckBest");
            PlayerPrefs.DeleteKey(ControlsSaveKey);
            PlayerPrefs.Save();
            resetSaveLatched = true;
        }

        // Mirrors match state onto the scene: cars placed and shown or hidden, props and crates shown or hidden, rockets
        // placed and coloured.
        private void SyncViews()
        {
            foreach (var car in cars)
            {
                car.gameObject.SetActive(car.index < Match.Cars.Length && car.Visible);
                car.Sync();
            }
            foreach (var prop in props)
            {
                prop.view.gameObject.SetActive(!prop.broken);
                if (!prop.broken) prop.view.position = PixelPosition(prop.point);
                if (prop.art != null && prop.intactSprite != null)
                    prop.art.sprite = prop.dented && prop.dentedSprite != null ? prop.dentedSprite : prop.intactSprite;
            }
            foreach (var pickup in pickups)
            {
                pickup.view.gameObject.SetActive(pickup.available);
                pickup.view.position = PixelPosition(pickup.point);
            }
            for (int i = 0; i < spikeBallViews.Length && i < spikeBalls.Length; i++)
            {
                var view = spikeBallViews[i];
                if (view == null) continue;
                view.transform.position = PixelPosition(spikeBalls[i].position);
                if (spikeBallFrames.Length > 0)
                    view.sprite = spikeBallFrames[Mathf.FloorToInt(spikeBalls[i].rolled / Mathf.Max(.01f, spikeFrameDistance)) % spikeBallFrames.Length];
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

        // Rocket hit or wreck: the supplied explosion picture pops up over the car (a wreck's bigger) and fades out.
        private void Explode(Vector2 point, float size = 1)
        {
            if (explosions == null || explosions.Length == 0 || explosionAge.Length != explosions.Length) return;
            int slot = 0;
            for (int i = 0; i < explosions.Length; i++) if (explosionAge[i] < 0) { slot = i; break; }
            explosionAge[slot] = 0;
            explosionScale[slot] = size;
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
                scale *= explosionScale[i] > 0 ? explosionScale[i] : 1;
                explosions[i].transform.localScale = new Vector3(scale, scale, 1);
                explosions[i].enabled = t < .6f || Mathf.FloorToInt(explosionAge[i] * 20) % 2 == 0;
            }
            for (int i = 0; i < sparks.Length; i++)
            {
                if (sparkAge[i] < 0) continue;
                sparkAge[i] += dt;
                if (sparkAge[i] >= impactFeel.sparkSeconds) { sparkAge[i] = -1; sparks[i].gameObject.SetActive(false); continue; }
                // Solid, then flickering out; no alpha, as with explosions.
                sparks[i].enabled = sparkAge[i] < impactFeel.sparkSeconds * .5f || Mathf.FloorToInt(sparkAge[i] * 30) % 2 == 0;
            }
        }

        // A spark burst between two cars that hit hard or scraped.
        private void Spark(Vector2 point)
        {
            if (sparks.Length == 0) return;
            int slot = 0;
            for (int i = 0; i < sparks.Length; i++) if (sparkAge[i] < 0) { slot = i; break; }
            sparkAge[slot] = 0;
            sparks[slot].transform.position = PixelPosition(point);
            sparks[slot].enabled = true;
            sparks[slot].gameObject.SetActive(true);
        }

        // 9x9 four-colour star drawn in code (no art file), on the explosions' parent, layer and sorting.
        private static readonly string[] SparkPicture =
        {
            "R...O...R",
            ".O..Y..O.",
            "..Y.Y.Y..",
            "...YWY...",
            "OYYWWWYYO",
            "...YWY...",
            "..Y.Y.Y..",
            ".O..Y..O.",
            "R...O...R",
        };

        // One sprite per spike ball, on the explosions' parent, layer and sorting (like the sparks).
        private void MakeSpikeBallViews()
        {
            if (spikeBallFrames.Length == 0 && spikeBallSheet != null)
            {
                int side = spikeBallSheet.height, count = Mathf.Max(1, spikeBallSheet.width / side);
                spikeBallFrames = new Sprite[count];
                for (int i = 0; i < count; i++)
                {
                    spikeBallFrames[i] = Sprite.Create(spikeBallSheet, new Rect(i * side, 0, side, side), new Vector2(.5f, 0), spikeBallPixelsPerUnit);
                    spikeBallFrames[i].hideFlags = HideFlags.DontSave;
                }
            }
            if (spikeBallViews.Length == spikeBalls.Length && spikeBallViews.All(v => v != null)) return;
            foreach (var old in spikeBallViews) if (old != null) Destroy(old.gameObject);
            var template = explosions != null && explosions.Length > 0 ? explosions[0] : null;
            spikeBallViews = new SpriteRenderer[spikeBalls.Length];
            for (int i = 0; i < spikeBalls.Length; i++)
            {
                var view = new GameObject("Spike ball " + i, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                view.gameObject.hideFlags = HideFlags.DontSave;
                if (template != null)
                {
                    view.transform.SetParent(template.transform.parent, false);
                    view.gameObject.layer = template.gameObject.layer;
                    view.sharedMaterial = template.sharedMaterial;
                    view.sortingLayerID = template.sortingLayerID;
                    view.sortingOrder = template.sortingOrder;
                }
                view.sprite = spikeBallFrames.Length > 0 ? spikeBallFrames[0] : SpikePlaceholder();
                spikeBallViews[i] = view;
            }
        }

        // Until SpikeBall.png is wired in: a 16x16 grey ball with four yellow spike tips, standing on its bottom edge.
        private Sprite SpikePlaceholder()
        {
            if (spikePlaceholder != null) return spikePlaceholder;
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x - 7.5f, y - 7.5f).magnitude;
                    bool tip = (x == 7 || x == 8) && (y == 0 || y == 15) || (y == 7 || y == 8) && (x == 0 || x == 15);
                    texture.SetPixel(x, y, tip ? Yellow : d < 5 ? new Color32(150, 158, 160, 255) : d < 6.5f ? new Color32(24, 24, 24, 255) : Color.clear);
                }
            texture.Apply();
            spikePlaceholder = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, 0), 26);
            spikePlaceholder.hideFlags = HideFlags.DontSave;
            return spikePlaceholder;
        }

        private void MakeSparks()
        {
            if (sparks.Length > 0 || explosions == null || explosions.Length == 0 || explosions[0] == null) return;
            var template = explosions[0];
            int size = SparkPicture.Length;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var orange = new Color32(255, 140, 40, 255);
            var red = new Color32(208, 48, 16, 255);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    char c = SparkPicture[size - 1 - y][x];
                    texture.SetPixel(x, y, c == 'W' ? White : c == 'Y' ? Yellow : c == 'O' ? (Color)orange : c == 'R' ? (Color)red : Color.clear);
                }
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 32);
            sprite.hideFlags = HideFlags.DontSave;
            sparks = new SpriteRenderer[SparkSlots];
            sparkAge = new float[SparkSlots];
            for (int i = 0; i < SparkSlots; i++)
            {
                var view = new GameObject("Impact sparks " + i, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                view.transform.SetParent(template.transform.parent, false);
                view.gameObject.layer = template.gameObject.layer;
                view.sprite = sprite;
                view.sharedMaterial = template.sharedMaterial;
                view.sortingLayerID = template.sortingLayerID;
                view.sortingOrder = template.sortingOrder;
                view.gameObject.SetActive(false);
                sparks[i] = view;
            }
        }

        // Each knocked-out player's own 10-second continue: their START rejoins, free, once per match. Once no player is
        // left and no countdown runs, GAME OVER shows briefly and the title demo returns.
        private void UpdateContinues(float dt, ArcadeInput p1, ArcadeInput p2)
        {
            if (Phase != ArcadePhase.Playing || Match.Finished)
            {
                Continues.Clear();
                gameOverLeft = -1;
                return;
            }
            Continues.Advance(dt, seat => seat == 0 ? p1.startPressed : p2.startPressed,
                seat => { Match.Revive(cars[seat]); Present(); },
                seat => Sound(0));
            if (gameOverLeft < 0 && !Continues.AnyWaiting && cars.Where(c => !c.computer).All(c => c.eliminated))
            {
                AwardGold();
                gameOverLeft = gameOverSeconds;
            }
            if (gameOverLeft >= 0 && (gameOverLeft -= dt) <= 0) PreparePreview();
        }

        private void ShowContinue()
        {
            if (continuePanels == null) return;
            bool over = ContinueShowsGameOver;
            for (int seat = 0; seat < continuePanels.Length; seat++)
            {
                var view = continuePanels[seat];
                if (view == null || view.panel == null) continue;
                bool waiting = seat < Continues.Seats && Continues.Waiting(seat);
                bool show = Phase == ArcadePhase.Playing && (waiting || over && seat == 0);
                view.panel.SetActive(show);
                if (!show) continue;
                if (waiting)
                {
                    string key = seat == 0 ? CabinetText.P1ContinueKey : CabinetText.P2ContinueKey;
                    view.title.text = string.Format(CabinetText.ContinueTitle, cars[seat].DriverName);
                    view.count.text = Continues.SecondsShown(seat).ToString();
                    view.hint.text = string.Format(CabinetText.ContinuePress, key);
                }
                else
                {
                    view.title.text = CabinetText.GameOver;
                    view.count.text = "";
                    view.hint.text = GoldSummary;
                }
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

        // Optional hook for a tilted or chase presentation (IsoStage, Mode7Stage). Null = the flat top-down mapping.
        public static ICabinetProjection CabinetProjection;

        private int CabinetViews => CabinetProjection != null ? Mathf.Max(1, CabinetProjection.Views) : 1;

        private bool CabinetPoint(Vector2 world, int view, out Vector2 cabinet)
        {
            if (CabinetProjection != null) return CabinetProjection.ToCabinet(world, view, out cabinet);
            cabinet = (world - (Vector2)sceneCamera.transform.position) * 32;
            return true;
        }

        // The scene holds one name tag and one score pop-up per car, for the first view. Further views (P2's half in
        // a Mode 7 2P split) get copies of them, made on first use. Index [view - 1][car].
        private readonly List<TMP_Text[]> extraLabels = new List<TMP_Text[]>(), extraPopups = new List<TMP_Text[]>();

        // How many views have tags: the views in use, plus any earlier views whose copies still need hiding.
        private int TagViews(List<TMP_Text[]> extra) => Mathf.Max(CabinetViews, extra.Count + 1);

        // One car's tag or pop-up in one view (view 0 is the scene's own), made for a view in use if missing. Null
        // where the scene has none for that car, or a view not in use never had one.
        private TMP_Text InView(TMP_Text[] own, List<TMP_Text[]> extra, int view, int car)
        {
            if (view == 0 || own[car] == null) return own[car];
            while (extra.Count < view) extra.Add(new TMP_Text[own.Length]);
            var copies = extra[view - 1];
            if (copies[car] == null && view < CabinetViews)
            {
                copies[car] = Instantiate(own[car], own[car].transform.parent);
                copies[car].name = own[car].name + " (view " + (view + 1) + ")";
                // Same draw order as the scene's own, so HUD panels drawn later still cover it.
                copies[car].transform.SetSiblingIndex(own[car].transform.GetSiblingIndex() + 1);
            }
            return copies[car];
        }

        private void UpdateScorePopups(float dt)
        {
            for (int i = 0; i < popupAge.Length; i++)
                if (popupAge[i] >= 0 && (popupAge[i] += dt) >= popupSeconds) popupAge[i] = -1;
            int views = CabinetViews;
            for (int v = 0; v < TagViews(extraPopups); v++)
            {
                placedPopups.Clear();
                for (int i = 0; i < popupAge.Length; i++)
                {
                    var text = InView(scorePopups, extraPopups, v, i);
                    if (text == null) continue;
                    Vector2 screen = default;
                    bool on = v < views && popupAge[i] >= 0 && CabinetPoint(popupOrigin[i] + Vector2.up * .88f, v, out screen);
                    text.gameObject.SetActive(on);
                    if (!on) continue;
                    var own = scorePopups[i];
                    if (popupBaseSize <= 0) popupBaseSize = own.fontSize;
                    float t = popupAge[i] / popupSeconds;
                    text.text = own.text;
                    text.color = own.color;
                    // Pops in at double size (still a whole multiple of the 11px pixel font), then floats up 16px and
                    // blinks out. Whole-pixel positions keep the pixel font sharp.
                    text.fontSize = popupAge[i] < .12f ? popupBaseSize * 2 : popupBaseSize;
                    var spot = new Vector2(Mathf.Round(screen.x), Mathf.Round(screen.y + 6 + 16 * t));
                    // Cars that touch score together; stack their pop-ups a line apart instead of overlapping.
                    while (placedPopups.Any(p => Mathf.Abs(p.x - spot.x) < 44 && Mathf.Abs(p.y - spot.y) < 13)) spot.y += 13;
                    placedPopups.Add(spot);
                    text.rectTransform.anchoredPosition = spot;
                    text.enabled = t < .6f || Mathf.FloorToInt(popupAge[i] * 16) % 2 == 0;
                }
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            engine.Dispose();
            if (spikePlaceholder != null) { Destroy(spikePlaceholder.texture); Destroy(spikePlaceholder); }
            foreach (var frame in spikeBallFrames) if (frame != null) Destroy(frame);
            if (sparks.Length > 0 && sparks[0] != null && sparks[0].sprite != null)
            {
                Destroy(sparks[0].sprite.texture);
                Destroy(sparks[0].sprite);
            }
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
                bool inGarage = Phase == ArcadePhase.Garage;
                attractScreen.SetActive(attract);
                if (attract) overlay.SetActive(false);
                foreach (var text in new[] { header, arenaBoard, leaderboard, hud, footer, notice })
                    if (text != null) text.gameObject.SetActive(!attract && !inGarage);
                if (attractRule != null) attractRule.text = Mode == CabinetMode.Challenge ? CabinetText.ChallengeRule : CabinetText.NormalRule;
                if (attractModes != null)
                    attractModes.text = string.Format(CabinetText.ModeTabs, ModeTab(CabinetMode.Normal, CabinetText.ModeNormal),
                        ModeTab(CabinetMode.Challenge, CabinetText.ModeChallenge));
                if (attractChallengePanel != null)
                {
                    attractChallengePanel.SetActive(Mode == CabinetMode.Challenge);
                    if (attractChallenges != null) attractChallenges.text = ChallengeListText();
                }
                if (attractPrompt != null && Mode == CabinetMode.Challenge)
                {
                    attractPrompt.text = !SelectedChallenge.Playable ? CabinetText.ChallengeComingSoon
                        : Records.IsUnlocked(SelectedChallenge) ? CabinetText.ChallengePrompt
                        : string.Format(CabinetText.ChallengeLocked, SelectedChallenge.UnlockPrice);
                    attractPrompt.enabled = Mathf.FloorToInt(Time.unscaledTime * 2) % 2 == 0;
                }
                else if (attractPrompt != null && ChoosingPlayers)
                {
                    // The player-count menu holds still; the chosen count is yellow.
                    attractPrompt.text = string.Format(CabinetText.PlayerMenu,
                        Tinted(PlayerChoice == 1 ? Yellow : White, CabinetText.OnePlayer),
                        Tinted(PlayerChoice == 2 ? Yellow : White, CabinetText.TwoPlayers));
                    attractPrompt.enabled = true;
                }
                else if (attractPrompt != null)
                {
                    attractPrompt.text = CabinetText.StartPrompt;
                    attractPrompt.enabled = Mathf.FloorToInt(Time.unscaledTime * 2) % 2 == 0;
                }
                if (attractGold != null) attractGold.text = string.Format(CabinetText.GoldTotal, Gold);
                if (resetSaveHint != null) resetSaveHint.text = resetSaveLatched && resetSaveHeldSeconds >= 3 ? CabinetText.SaveCleared
                    : resetSaveHeldSeconds > 0 ? string.Format(CabinetText.ResetSaveConfirm, Mathf.CeilToInt(3 - resetSaveHeldSeconds))
                    : CabinetText.ResetSaveHint;
            }
            for (int i = 0; i < cars.Length; i++)
            {
                var car = cars[i];
                if (carLabels != null && i < carLabels.Length && carLabels[i] != null)
                {
                    Vector2 tag = car.position + Vector2.up * .68f;
                    if (carLabels[i] is TextMeshProUGUI)
                    {
                        // Screen-space tag, one per view, in cabinet units with the origin at the centre (top-down:
                        // 1 world unit = 32 cabinet units). Sits just above the roof health bar.
                        bool shown = !attract && Phase != ArcadePhase.Garage && car.alive && car.gameObject.activeInHierarchy;
                        int views = CabinetViews;
                        for (int v = 0; v < TagViews(extraLabels); v++)
                        {
                            var screenTag = InView(carLabels, extraLabels, v, i);
                            if (screenTag == null) continue;
                            Vector2 screen = default;
                            bool on = shown && v < views && CabinetPoint(tag + Vector2.up * .2f, v, out screen);
                            screenTag.gameObject.SetActive(on);
                            if (!on) continue;
                            screenTag.text = car.DriverName;
                            screenTag.color = car.body.color;
                            // Whole pixels keep the 11px pixel font sharp.
                            screenTag.rectTransform.anchoredPosition = new Vector2(Mathf.Round(screen.x), Mathf.Round(screen.y));
                        }
                    }
                    else
                    {
                        carLabels[i].text = car.DriverName;
                        carLabels[i].color = car.body.color;
                        carLabels[i].transform.position = PixelPosition(tag);
                    }
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
                // Otherwise P3 and P4 wear their own stripes and P1/P2 stay solid.
                bool cherryOn = HasCherry(car) && (car.CherrySeconds > 2 || Mathf.FloorToInt(Elapsed * 8) % 2 == 0);
                var livery = Livery(car.index);
                if (solidBlock == null) solidBlock = new MaterialPropertyBlock();
                car.body.GetPropertyBlock(solidBlock);
                solidBlock.SetFloat("_Solid", cherryOn || livery.striped ? 1 : 0);
                solidBlock.SetColor("_SolidColor", cherryOn ? cherryStripeA : livery.stripeA);
                solidBlock.SetColor("_StripeColor", cherryOn ? cherryStripeB : livery.stripeB);
                car.body.SetPropertyBlock(solidBlock);
            }
            ShowCrown();
            header.text = CabinetText.Title;
            arenaBoard.text = ActiveChallenge != null ? string.Format(CabinetText.ChallengeBoard, ActiveChallenge.Name)
                : Phase == ArcadePhase.Results ? string.Format(CabinetText.Champion, cars[Champion].DriverName)
                : string.Format(CabinetText.NextElimination, Mathf.CeilToInt(Mathf.Max(0, NextElimination - Elapsed)), Contenders);
            var order = Phase == ArcadePhase.Results ? Match.Cars.OrderBy(c => c.FinalPlace)
                : Match.Cars.OrderBy(c => c.eliminated).ThenBy(c => c.eliminated ? c.FinalPlace : -c.score)
                    .ThenByDescending(c => c.health).ThenBy(c => c.index);
            if (leaderboard != null)
            {
                var lines = order.Select((c, rank) =>
                    Tinted(c.body.color, (rank + 1) + " " + c.DriverName.PadRight(3)
                    + " " + Mathf.RoundToInt(c.score).ToString("00000") + "  " + DriverStatus(c))).ToArray();
                int rows = (lines.Length + 1) / 2;
                leaderboard.text = lines.Length <= 4 ? string.Join("\n", lines)
                    : string.Join("\n", Enumerable.Range(0, rows).Select(i => lines[i]
                        + (i + rows < lines.Length ? "<pos=250>" + lines[i + rows] : "")));
            }
            hud.text = string.Format(CabinetText.PlayerStatus, cars[0].DriverName, ActionStatus(cars[0]))
                + (PlayerCount == 2 ? "\n" + string.Format(CabinetText.PlayerStatus, cars[1].DriverName, ActionStatus(cars[1])) : "")
                + "\n" + string.Format(CabinetText.TimeAndWrecks, Mathf.CeilToInt(Remaining), Wrecks)
                + (Phase == ArcadePhase.Playing && cars.Where(c => !c.computer).All(c => c.eliminated) ? "\n" + CabinetText.Spectating : "");
            if (cars.Length > 4) hud.text = hud.text.Replace("\n", "　");
            // One 11px line fits under the arena; in 2P it alternates between the two players every 3 seconds.
            bool showP2 = PlayerCount == 2 && Mathf.FloorToInt(Time.unscaledTime / 3) % 2 == 1;
            footer.text = ControlsFor(showP2 ? 1 : 0);
            if (Phase == ArcadePhase.Attract) overlayHint.text = CabinetText.AttractHint;
            ShowStartMessages();
            ShowContinue();
            RefreshControlsAndSpeedHud();
            if (standingsHud != null)
                standingsHud.Render(this, order.ToArray(), victoryLeft < 0 && gameOverLeft < 0);
        }

        private void RefreshControlsAndSpeedHud()
        {
            if (attractControlsType != null) attractControlsType.text = string.Format(CabinetText.ControlsOption, Controls == ControlType.TypeA ? "A" : "B");
            if (attractControls != null)
                for (int i = 0; i < attractControls.Length; i++)
                    if (attractControls[i] != null) attractControls[i].text = ControlsFor(i);
            var cabinet = (RectTransform)header.transform.parent;
            if (speedLabels == null) return;
            for (int i = 0; i < speedLabels.Length; i++)
            {
                var label = speedLabels[i];
                if (label == null) continue;
                bool shown = (Phase == ArcadePhase.Playing || Phase == ArcadePhase.Countdown) && i < PlayerCount && victoryLeft < 0 && gameOverLeft < 0;
                label.transform.parent.gameObject.SetActive(shown);
                label.text = string.Format(CabinetText.PlayerSpeed, i + 1, Mathf.RoundToInt(Mathf.Abs(cars[i].speed) * CabinetText.SpeedToKmh));
                // P1's lower-right corner moves up to the top half in a split view; P2 keeps the bottom corner.
                var rect = (RectTransform)label.transform.parent;
                rect.anchoredPosition = new Vector2(cabinet.sizeDelta.x - rect.sizeDelta.x - 6,
                    20 + (PlayerCount == 2 && i == 0 ? cabinet.sizeDelta.y / 2 : 0));
            }
        }

        private string ControlsFor(int player) => player == 0
            ? Controls == ControlType.TypeA ? CabinetText.P1TypeAControls : CabinetText.P1Controls
            : CabinetText.P2Controls;

        private static string Tinted(Color color, string text) => "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";

        private string ModeTab(CabinetMode mode, string name) => Tinted(Mode == mode ? Yellow : White, name);

        // Name, state and best score of every challenge, each followed by its condition; the chosen one is yellow.
        private string ChallengeListText()
        {
            var lines = new List<string>();
            for (int i = 0; i < Challenges.All.Count; i++)
            {
                var challenge = Challenges.All[i];
                bool open = Records.IsUnlocked(challenge), selected = i == ChallengeCursor;
                string state = open ? CabinetText.ChallengeUnlocked + "　" + string.Format(CabinetText.ChallengeBestShort, challenge.RecordText(Records.Best(challenge)))
                    : string.Format(CabinetText.ChallengeLockedShort, challenge.UnlockPrice);
                var color = selected ? Yellow : White;
                lines.Add(Tinted(color, string.Format(CabinetText.ChallengeRow, selected ? ">" : " ", challenge.Name, state)));
                lines.Add(Tinted(color, challenge.Condition));
                if (i < Challenges.All.Count - 1) lines.Add("");
            }
            return string.Join("\n", lines);
        }

        // A seat's colours: P1 cyan and P2 yellow (Player2, or Panda in 1P) are solid (their stripe colours are unused
        // but set as before); P3/P4 are striped. P5..P8 have distinct bright colours in the chase view and map.
        private (Color body, bool striped, Color stripeA, Color stripeB) Livery(int seat)
        {
            switch (seat % 8)
            {
                case 0: return (Cyan, false, p4StripeA, p4StripeB);
                case 1: return (Yellow, false, p4StripeA, p4StripeB);
                case 2: return (White, true, p3StripeA, p3StripeB);
                case 3: return (White, true, p4StripeA, p4StripeB);
                case 4: return (new Color32(255, 128, 172, 255), false, White, White);
                case 5: return (new Color32(96, 224, 160, 255), false, White, White);
                case 6: return (new Color32(181, 160, 255, 255), false, White, White);
                default: return (new Color32(255, 152, 72, 255), false, White, White);
            }
        }

        // The crown rides above the car in first place: highest score among cars still in, ties to the lower seat. Nobody
        // wears it while every score is 0, or while the leader is wrecked and waiting to respawn.
        private void ShowCrown()
        {
            if (crownView == null) return;
            var leader = Match.Cars.Where(c => !c.eliminated).OrderByDescending(c => c.score).ThenBy(c => c.index).FirstOrDefault();
            bool show = leader != null && leader.score > 0 && leader.alive && leader.gameObject.activeInHierarchy;
            crownView.gameObject.SetActive(show);
            if (show) crownView.transform.position = PixelPosition(leader.position + Vector2.up * crownHeight);
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
