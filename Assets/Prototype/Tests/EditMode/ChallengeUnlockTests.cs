using System;
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

        [Test]
        public void TooLittleGoldLeavesTheChallengeLockedAndTheGoldUntouched()
        {
            var wallet = new GoldWallet(goldKey);
            wallet.AwardRound((2, 8900)); // 60 + 89 = 149
            var records = new ChallengeRecords(recordPrefix);

            Assert.IsFalse(records.TryUnlock(Challenges.NoWreck, wallet));
            Assert.IsFalse(records.IsUnlocked(Challenges.NoWreck));
            Assert.AreEqual(149, wallet.Total);
            Assert.AreEqual(149, new GoldWallet(goldKey).Total);
        }

        [Test]
        public void EnoughGoldPays150AndTheUnlockSurvivesReopening()
        {
            var wallet = WalletWith199();
            var records = new ChallengeRecords(recordPrefix);

            Assert.IsTrue(records.TryUnlock(Challenges.NoWreck, wallet));
            Assert.IsTrue(records.IsUnlocked(Challenges.NoWreck));
            Assert.AreEqual(49, wallet.Total);
            Assert.AreEqual(49, new GoldWallet(goldKey).Total);
            Assert.IsTrue(new ChallengeRecords(recordPrefix).IsUnlocked(Challenges.NoWreck));
            Assert.IsFalse(records.IsUnlocked(Challenges.NinetySecondScore), "only the chosen challenge opens");
        }

        [Test]
        public void AnOpenChallengeCostsNothingMore()
        {
            var wallet = WalletWith199();
            var records = new ChallengeRecords(recordPrefix);
            records.TryUnlock(Challenges.NoWreck, wallet);

            Assert.IsTrue(records.TryUnlock(Challenges.NoWreck, wallet));
            Assert.IsTrue(records.TryUnlock(Challenges.RocketOnly, wallet));
            Assert.AreEqual(49, wallet.Total);
        }

        [Test]
        public void OnlyChallengesWithRulesArePlayable()
        {
            Assert.IsTrue(Challenges.RocketOnly.Playable);
            Assert.IsTrue(Challenges.NoWreck.Playable);
            Assert.IsFalse(Challenges.NinetySecondScore.Playable, "its rules arrive with ticket 11");
        }

        [Test]
        public void ClearingTheSaveLocksItAgain()
        {
            var records = new ChallengeRecords(recordPrefix);
            records.TryUnlock(Challenges.NoWreck, WalletWith199());
            records.Clear();
            Assert.IsFalse(records.IsUnlocked(Challenges.NoWreck));
            Assert.IsFalse(new ChallengeRecords(recordPrefix).IsUnlocked(Challenges.NoWreck));
            Assert.IsTrue(records.IsUnlocked(Challenges.RocketOnly));
        }
    }
}
