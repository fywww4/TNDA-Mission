using System.Collections.Generic;
using UnityEngine;

namespace TNDA.Prototype
{
    // Where a campaign stage is played (ticket 25). The match only swaps its terrain; each Mode 7 view component
    // decides for itself what to draw (Mode7TrainingGround, Mode7DerbyArena, Mode7Audience, Mode7Obstacles).
    public enum CampaignArena { Standard, TrainingGround, Derby }

    // One campaign stage (ticket 15): a single-player match with its own field, rules and opening story. Like a
    // challenge it changes the match only through MatchTuning; a stage is cleared when the player wins the round.
    public sealed class CampaignStage
    {
        public int Number { get; }
        public string Name { get; }
        public string Goal { get; }
        public int CarCount { get; }
        public CampaignArena Arena { get; }
        // The black-box training ground: no audience, terrain or spike balls.
        public bool TrainingGround => Arena == CampaignArena.TrainingGround;
        public bool Tutorial { get; }
        // The opening story, one "speaker｜line" per page.
        public IReadOnlyList<string> Story { get; }
        private readonly System.Action<MatchTuning, TutorialCoach> rules;

        public CampaignStage(int number, string name, string goal, int carCount, string[] story,
            System.Action<MatchTuning, TutorialCoach> rules, CampaignArena arena = CampaignArena.Standard, bool tutorial = false)
        {
            Number = number; Name = name; Goal = goal; CarCount = carCount; Story = story; this.rules = rules;
            Arena = arena; Tutorial = tutorial;
        }

        // coach: the running tutorial (null outside it). Applied after the Inspector tuning, so a stage may change any value.
        public void ApplyRules(MatchTuning tuning, TutorialCoach coach) => rules?.Invoke(tuning, coach);
    }

    public static class Campaign
    {
        // How long the tutorial's fight may last before the final elimination settles it (the player goes last).
        public const float TutorialFightSeconds = 150;
        public const float BossSeconds = 180;
        // Seats in the boss stage: the player and two allies (team 1) against the boss and two minions (team 2).
        public const int BossSeat = 3;
        // The two allies are 50% stronger than an ordinary car (2026-11-23: the boss stage was too hard).
        public const float AllyStrength = 1.5f;

        // 1: four cars on the training ground. The steps first (the computers cannot hurt the player, no eliminations),
        // then a fight where a wrecked computer car is out for good and a wrecked player loses.
        public static readonly CampaignStage Tutorial = new CampaignStage(1, CabinetText.Stage1Name, CabinetText.Stage1Goal, 4,
            CabinetText.Stage1Story, (tuning, coach) =>
            {
                bool fighting = coach != null && coach.Fighting;
                tuning.seatPersonalities = new[] { AiPersonality.Bumper, AiPersonality.Bumper, AiPersonality.Bumper, AiPersonality.Bumper };
                tuning.aiSparesPlayers = !fighting;
                tuning.wrecksKnockOut = fighting;
                tuning.wreckEndsRound = fighting;
                tuning.aiTopSpeed = tuning.aiLateTopSpeed = .75f;
                tuning.aiGraceSeconds = 0;
                tuning.eliminations = new[] { new EliminationStep(fighting ? coach.FightStartedAt + TutorialFightSeconds : 1e6f, 1) };
            }, CampaignArena.TrainingGround, tutorial: true);

        // 2: the arcade's eight-car pyramid, won by taking the crown.
        public static readonly CampaignStage Pyramid = new CampaignStage(2, CabinetText.Stage2Name, CabinetText.Stage2Goal, 8,
            CabinetText.Stage2Story, null);

        // 3: three against three in the night derby (ticket 25). Wrecking the boss wins; a wrecked player or the clock
        // running out loses.
        public static readonly CampaignStage BossFight = new CampaignStage(3, CabinetText.Stage3Name, CabinetText.Stage3Goal, 6,
            CabinetText.Stage3Story, (tuning, coach) =>
            {
                tuning.seatTeams = new[] { 1, 1, 1, 2, 2, 2 };
                tuning.seatPersonalities = new[] { AiPersonality.Bumper, AiPersonality.Bumper, AiPersonality.Bumper,
                    AiPersonality.Boss, AiPersonality.Hunter, AiPersonality.Bumper };
                tuning.seatNames = new[] { null, CabinetText.AllyName, CabinetText.AllyName, CabinetText.BossName,
                    CabinetText.MinionName, CabinetText.MinionName };
                tuning.bossSeat = BossSeat;
                tuning.allyStrength = AllyStrength;
                tuning.wrecksKnockOut = true;
                tuning.wreckEndsRound = true;
                tuning.eliminations = new[] { new EliminationStep(BossSeconds, 1) };
            }, CampaignArena.Derby);

        public static readonly IReadOnlyList<CampaignStage> Stages = new[] { Tutorial, Pyramid, BossFight };

        public static CampaignStage After(CampaignStage stage)
        {
            int i = IndexOf(stage);
            return i >= 0 && i + 1 < Stages.Count ? Stages[i + 1] : null;
        }

        public static int IndexOf(CampaignStage stage)
        {
            for (int i = 0; i < Stages.Count; i++) if (Stages[i] == stage) return i;
            return -1;
        }
    }

    // Which campaign stages are cleared, kept in PlayerPrefs under a key prefix so tests can use their own. The first
    // stage is always open; each later one opens once the one before is cleared.
    public sealed class CampaignProgress
    {
        private readonly string prefix;
        public CampaignProgress(string keyPrefix = "TNDA.Campaign.") { prefix = keyPrefix; }

        private string ClearedKey(CampaignStage stage) => prefix + "Stage" + stage.Number + ".Cleared";

        public bool IsCleared(CampaignStage stage) => PlayerPrefs.GetInt(ClearedKey(stage), 0) == 1;

        public bool IsUnlocked(CampaignStage stage)
        {
            int i = Campaign.IndexOf(stage);
            return i == 0 || i > 0 && IsCleared(Campaign.Stages[i - 1]);
        }

        public void MarkCleared(CampaignStage stage)
        {
            PlayerPrefs.SetInt(ClearedKey(stage), 1);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            foreach (var stage in Campaign.Stages) PlayerPrefs.DeleteKey(ClearedKey(stage));
            PlayerPrefs.Save();
        }
    }

    public enum TutorialStep { Gas, Steer, Brake, Ram, Crate, Item, Fight }

    // The tutorial's steps for the player's car, in order. Watches the player's own driving and the match's events each
    // frame; a step done early (a crate picked up while learning to steer) counts when its turn comes. After the last
    // step the fight starts.
    public sealed class TutorialCoach
    {
        public const float GasSpeed = 1.5f, SteerDegrees = 270, ReverseSpeed = -.3f;
        public TutorialStep Step { get; private set; }
        public bool Fighting => Step == TutorialStep.Fight;
        public float FightStartedAt { get; private set; } = -1;
        public int StepNumber => (int)Step + 1;
        public const int StepCount = (int)TutorialStep.Fight;
        private readonly bool[] done = new bool[StepCount];
        private float turned, lastHeading;
        private bool headingSeen;

        // Call after each match tick, before its events are cleared. True on the frame the step moved on.
        public bool Observe(ArcadeVehicle player, IReadOnlyList<MatchEvent> events, float elapsed)
        {
            if (Fighting || player == null) return false;
            if (player.alive)
            {
                if (player.speed >= GasSpeed) done[(int)TutorialStep.Gas] = true;
                if (player.speed <= ReverseSpeed) done[(int)TutorialStep.Brake] = true;
                if (headingSeen && Mathf.Abs(player.speed) > .3f) turned += Mathf.Abs(Mathf.DeltaAngle(lastHeading, player.heading));
                if (turned >= SteerDegrees) done[(int)TutorialStep.Steer] = true;
                lastHeading = player.heading;
                headingSeen = true;
            }
            foreach (var e in events)
            {
                if (e is CarRammed ram && ram.Attacker == player && ram.Kind != ImpactKind.Scrape) done[(int)TutorialStep.Ram] = true;
                if (e is ItemPicked picked && picked.Car == player) done[(int)TutorialStep.Crate] = true;
                if (e is ItemUsed used && used.Car == player) done[(int)TutorialStep.Item] = true;
            }
            var before = Step;
            while (!Fighting && done[(int)Step]) Step++;
            if (Fighting && FightStartedAt < 0) FightStartedAt = elapsed;
            return Step != before;
        }
    }
}
