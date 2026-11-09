using System;
using NUnit.Framework;
using UnityEngine;

namespace TNDA.Prototype.Tests
{
    public class GoldWalletTests
    {
        private string saveKey;

        [SetUp]
        public void CreateEmptySave() => saveKey = "TNDA.Gold.Tests." + Guid.NewGuid().ToString("N");

        [TearDown]
        public void RemoveTestSave() { PlayerPrefs.DeleteKey(saveKey); PlayerPrefs.Save(); }

        [TestCase(1, 0, 100)]
        [TestCase(2, 0, 60)]
        [TestCase(3, 0, 30)]
        [TestCase(4, 0, 30)]
        [TestCase(5, 0, 10)]
        [TestCase(6, 0, 10)]
        [TestCase(7, 0, 10)]
        [TestCase(8, 0, 10)]
        [TestCase(1, 199.9f, 101)]
        [TestCase(2, 200, 62)]
        [TestCase(4, 999.9f, 39)]
        public void PlaceAndScoreDetermineGoldWithTheScoreBonusRoundedDown(int place, float score, int expected)
        {
            Assert.AreEqual(expected, GoldWallet.RewardFor(place, score));
        }

        [Test]
        public void TwoPlayersPutBothRewardsIntoTheSharedWallet()
        {
            var wallet = new GoldWallet(saveKey);
            Assert.AreEqual(0, wallet.Total);
            Assert.AreEqual(197, wallet.AwardRound((1, 2599), (2, 1299)));
            Assert.AreEqual(197, wallet.Total);
        }

        [Test]
        public void ReopeningTheWalletKeepsRewardsFromMultipleRounds()
        {
            var wallet = new GoldWallet(saveKey);
            wallet.AwardRound((1, 2599));
            wallet.AwardRound((4, 199));
            Assert.AreEqual(156, new GoldWallet(saveKey).Total);
        }

        [Test]
        public void ClearingTheSaveResetsPersistedGoldAndAllowsEarningAgain()
        {
            var wallet = new GoldWallet(saveKey);
            wallet.AwardRound((2, 1299));
            wallet.Clear();
            Assert.AreEqual(0, wallet.Total);
            Assert.AreEqual(0, new GoldWallet(saveKey).Total);
            Assert.AreEqual(10, wallet.AwardRound((8, 99)));
            Assert.AreEqual(10, new GoldWallet(saveKey).Total);
        }
    }
}
