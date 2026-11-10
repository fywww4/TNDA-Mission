using System.Collections.Generic;
using UnityEngine;

namespace TNDA.Prototype
{
    // One challenge-mode challenge: a single-condition scoring run (no coins, one player) with a saved best record.
    // It changes the match only through MatchTuning, so the arcade rules stay exactly as they are.
    public sealed class Challenge
    {
        public string Id { get; }
        public string Name { get; }
        public string Condition { get; }
        // Gold needed to unlock it; 0 = open from the start.
        public int UnlockPrice { get; }
        // Challenges keep their own field size when the arcade scene has more cars.
        public int CarCount { get; }
        private readonly System.Action<MatchTuning> rules;
        private readonly System.Func<DemolitionMatch, ArcadeVehicle, int> score;
        private readonly System.Func<int, string> recordText;

        // score: the run's result for the best record, from the finished match and the player's car; by default the
        // player's points. recordText: how a result reads on the list and the results panel; by default 5 digits.
        public Challenge(string id, string name, string condition, int unlockPrice, System.Action<MatchTuning> rules,
            System.Func<DemolitionMatch, ArcadeVehicle, int> score = null, System.Func<int, string> recordText = null, int carCount = 4)
        {
            Id = id; Name = name; Condition = condition; UnlockPrice = unlockPrice; this.rules = rules;
            this.score = score ?? ((match, player) => Mathf.RoundToInt(player.score));
            this.recordText = recordText ?? (result => result.ToString("00000"));
            CarCount = carCount;
        }

        public void ApplyRules(MatchTuning tuning) => rules?.Invoke(tuning);
        // Listed before its ticket gives it rules: shown with its price but it can be neither bought nor played.
        public bool Playable => rules != null;

        // The finished run's result, the number the best record keeps.
        public int ScoreOf(DemolitionMatch match, ArcadeVehicle player) => score(match, player);
        public string RecordText(int result) => recordText(result);
    }

    // The challenge list shown on the title screen, in order.
    public static class Challenges
    {
        // Only rockets come out of crates, rams earn no points, and nobody is knocked out before the 120 s final.
        public static readonly Challenge RocketOnly = new Challenge("rocket-only", CabinetText.ChallengeRocketOnly,
            CabinetText.ChallengeRocketOnlyRule, 0, tuning =>
            {
                tuning.rocketCratesOnly = true;
                tuning.ramsScore = false;
                tuning.eliminations = new[] { new EliminationStep(120, 1) };
            });

        // Listed with their unlock price; the rules arrive with their own tickets.
        public static readonly Challenge NinetySecondScore = new Challenge("score-90", CabinetText.ChallengeScore90,
            CabinetText.ChallengeScore90Rule, 150, null);
        // Four cars, all three computers 追王 that chase the player; a wreck fails at once, lasting 120 s passes. The
        // record is the durability left (percent) on a pass.
        public static readonly Challenge NoWreck = new Challenge("no-wreck", CabinetText.ChallengeNoWreck,
            CabinetText.ChallengeNoWreckRule, 150, tuning =>
            {
                tuning.wreckEndsRound = true;
                tuning.allAiHuntThePlayer = true;
                tuning.eliminations = new[] { new EliminationStep(120, 1) };
            }, (match, player) => match.Survived ? Mathf.CeilToInt(player.health * 100) : 0,
            recordText: result => result > 0 ? string.Format(CabinetText.ChallengeDurabilityLeft, result) : CabinetText.ChallengeNotPassed);

        public static readonly IReadOnlyList<Challenge> All = new[] { RocketOnly, NinetySecondScore, NoWreck };
    }

    // Saved best scores and bought unlocks of the challenges, kept in PlayerPrefs under a key prefix so tests can use
    // their own. Separate from the gold wallet and the inserted coins.
    public sealed class ChallengeRecords
    {
        private readonly string prefix;
        public ChallengeRecords(string keyPrefix = "TNDA.Challenge.") { prefix = keyPrefix; }

        private string BestKey(Challenge challenge) => prefix + challenge.Id + ".Best";
        private string UnlockKey(Challenge challenge) => prefix + challenge.Id + ".Unlocked";

        public bool IsUnlocked(Challenge challenge) => challenge.UnlockPrice <= 0 || PlayerPrefs.GetInt(UnlockKey(challenge), 0) == 1;

        // Buys a locked challenge with its price in gold; true when it is open afterwards (an open one costs nothing).
        public bool TryUnlock(Challenge challenge, GoldWallet wallet)
        {
            if (IsUnlocked(challenge)) return true;
            if (!wallet.TrySpend(challenge.UnlockPrice)) return false;
            PlayerPrefs.SetInt(UnlockKey(challenge), 1);
            PlayerPrefs.Save();
            return true;
        }

        public int Best(Challenge challenge) => Mathf.Max(0, PlayerPrefs.GetInt(BestKey(challenge), 0));

        // Saves the score when it beats the record; true for a new best. A score of 0 is never a record.
        public bool Submit(Challenge challenge, int score)
        {
            if (score <= Best(challenge)) return false;
            PlayerPrefs.SetInt(BestKey(challenge), score);
            PlayerPrefs.Save();
            return true;
        }

        public void Clear()
        {
            foreach (var challenge in Challenges.All)
            {
                PlayerPrefs.DeleteKey(BestKey(challenge));
                PlayerPrefs.DeleteKey(UnlockKey(challenge));
            }
            PlayerPrefs.Save();
        }
    }
}
