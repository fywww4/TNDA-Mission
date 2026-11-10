using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace TNDA.Prototype.Tests
{
    // Unlocking a locked challenge with saved gold, through the public records and wallet. Each test has its own save keys.
    public class ChallengeUnlockTests
    {
        private string recordPrefix, goldKey;

        [SetUp]
        public void CreateEmptySave()
        {
            string id = Guid.NewGuid().ToString("N");
            recordPrefix = "TNDA.Challenge.Tests." + id + ".";
            goldKey = "TNDA.Gold.Tests." + id;
        }

        [TearDown]
        public void RemoveTestSave()
        {
            new ChallengeRecords(recordPrefix).Clear();
            new GoldWallet(goldKey).Clear();
        }

        // 100 for first place plus 99 for the score: 199 gold.
        private GoldWallet WalletWith199() { var wallet = new GoldWallet(goldKey); wallet.AwardRound((1, 9900)); return wallet; }

        [TestCase("no-wreck")]
        [TestCase("score-90")]
        public void TooLittleGoldLeavesTheChallengeLockedAndTheGoldUntouched(string challengeId)
        {
            var challenge = Challenges.All.Single(c => c.Id == challengeId);
            var wallet = new GoldWallet(goldKey);
            wallet.AwardRound((2, 8900)); // 60 + 89 = 149
            var records = new ChallengeRecords(recordPrefix);

            Assert.IsFalse(records.TryUnlock(challenge, wallet));
            Assert.IsFalse(records.IsUnlocked(challenge));
            Assert.AreEqual(149, wallet.Total);
            Assert.AreEqual(149, new GoldWallet(goldKey).Total);
        }

        [TestCase("no-wreck")]
        [TestCase("score-90")]
        public void EnoughGoldPays150AndTheUnlockSurvivesReopening(string challengeId)
        {
            var challenge = Challenges.All.Single(c => c.Id == challengeId);
            var wallet = WalletWith199();
            var records = new ChallengeRecords(recordPrefix);

            Assert.IsTrue(records.TryUnlock(challenge, wallet));
            Assert.IsTrue(records.IsUnlocked(challenge));
            Assert.AreEqual(49, wallet.Total);
            Assert.AreEqual(49, new GoldWallet(goldKey).Total);
            Assert.IsTrue(new ChallengeRecords(recordPrefix).IsUnlocked(challenge));
            foreach (var other in Challenges.All.Where(c => c.UnlockPrice > 0 && c != challenge))
                Assert.IsFalse(records.IsUnlocked(other), "only the chosen challenge opens");
        }

        [TestCase("no-wreck")]
        [TestCase("score-90")]
        public void AnOpenChallengeCostsNothingMore(string challengeId)
        {
            var challenge = Challenges.All.Single(c => c.Id == challengeId);
            var wallet = WalletWith199();
            var records = new ChallengeRecords(recordPrefix);
            records.TryUnlock(challenge, wallet);

            Assert.IsTrue(records.TryUnlock(challenge, wallet));
            Assert.IsTrue(records.TryUnlock(Challenges.RocketOnly, wallet));
            Assert.AreEqual(49, wallet.Total);
        }

        [Test]
        public void AllThreeChallengesArePlayable()
        {
            Assert.IsTrue(Challenges.RocketOnly.Playable);
            Assert.IsTrue(Challenges.NoWreck.Playable);
            Assert.IsTrue(Challenges.NinetySecondScore.Playable);
        }

        [TestCase("no-wreck")]
        [TestCase("score-90")]
        public void ClearingTheSaveLocksItAgain(string challengeId)
        {
            var challenge = Challenges.All.Single(c => c.Id == challengeId);
            var records = new ChallengeRecords(recordPrefix);
            records.TryUnlock(challenge, WalletWith199());
            records.Clear();
            Assert.IsFalse(records.IsUnlocked(challenge));
            Assert.IsFalse(new ChallengeRecords(recordPrefix).IsUnlocked(challenge));
            Assert.IsTrue(records.IsUnlocked(Challenges.RocketOnly));
        }
    }
}
