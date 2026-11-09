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
        private readonly System.Action<MatchTuning> rules;

        public Challenge(string id, string name, string condition, int unlockPrice, System.Action<MatchTuning> rules)
        {
            Id = id; Name = name; Condition = condition; UnlockPrice = unlockPrice; this.rules = rules;
        }

        public void ApplyRules(MatchTuning tuning) => rules?.Invoke(tuning);
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
        public static readonly Challenge NoWreck = new Challenge("no-wreck", CabinetText.ChallengeNoWreck,
            CabinetText.ChallengeNoWreckRule, 150, null);

        public static readonly IReadOnlyList<Challenge> All = new[] { RocketOnly, NinetySecondScore, NoWreck };
    }

    // Saved best scores (and later unlocks) of the challenges, kept in PlayerPrefs under a key prefix so tests can use
    // their own. Separate from the gold wallet and the inserted coins.
    public sealed class ChallengeRecords
    {
        private readonly string prefix;
        public ChallengeRecords(string keyPrefix = "TNDA.Challenge.") { prefix = keyPrefix; }

        private string BestKey(Challenge challenge) => prefix + challenge.Id + ".Best";

        public bool IsUnlocked(Challenge challenge) => challenge.UnlockPrice <= 0;

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
            foreach (var challenge in Challenges.All) PlayerPrefs.DeleteKey(BestKey(challenge));
            PlayerPrefs.Save();
        }
    }
}
