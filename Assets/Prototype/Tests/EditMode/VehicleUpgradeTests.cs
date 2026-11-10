using System;
using NUnit.Framework;
using UnityEngine;

namespace TNDA.Prototype.Tests
{
    // Agreed boundaries: shared wallet/save, public driving/damage, and the real cabinet workflow.
    public class VehicleUpgradeTests
    {
        private string prefix;
        private GoldWallet wallet;
        private VehicleUpgrades upgrades;

        [SetUp]
        public void EmptySave()
        {
            prefix = "TNDA.Garage.Tests." + Guid.NewGuid().ToString("N") + ".";
            wallet = new GoldWallet(prefix + "Gold");
            upgrades = new VehicleUpgrades(prefix);
        }

        [TearDown]
        public void DeleteTestSave()
        {
            foreach (VehicleAbility ability in Enum.GetValues(typeof(VehicleAbility))) PlayerPrefs.DeleteKey(prefix + ability);
            wallet.Clear();
            PlayerPrefs.Save();
        }

        [Test]
        public void BuyingFirstSpeedUpgradeCostsFiftyGoldAndSurvivesReopening()
        {
            wallet.AwardRound((1, 0));
            Assert.That(upgrades.TryBuy(VehicleAbility.TopSpeed, wallet), Is.True);
            Assert.That(wallet.Total, Is.EqualTo(50));
            Assert.That(new GoldWallet(prefix + "Gold").Total, Is.EqualTo(50));
            Assert.That(new VehicleUpgrades(prefix).Level(VehicleAbility.TopSpeed), Is.EqualTo(1));
            Assert.That(upgrades.Bonus(VehicleAbility.TopSpeed), Is.EqualTo(.05f));
        }

        [TestCase(VehicleAbility.TopSpeed)]
        [TestCase(VehicleAbility.Acceleration)]
        [TestCase(VehicleAbility.Handling)]
        [TestCase(VehicleAbility.Strength)]
        public void EveryAbilityHasFourFivePercentStepsWithExactPricesAndCannotBeBoughtAgain(VehicleAbility ability)
        {
            Assert.That(upgrades.TryBuy(ability, wallet), Is.False, "empty wallet");
            wallet.AwardRound((1, 65000)); // 750 total, exactly enough for all four steps
            int[] balances = { 700, 600, 400, 0 };
            float[] bonuses = { .05f, .10f, .15f, .20f };
            int[] prices = { 50, 100, 200, 400 };
            Assert.That(upgrades.Bonus(ability), Is.Zero);
            for (int i = 0; i < 4; i++)
            {
                Assert.That(upgrades.Price(ability), Is.EqualTo(prices[i]));
                Assert.That(upgrades.TryBuy(ability, wallet), Is.True);
                Assert.That(wallet.Total, Is.EqualTo(balances[i]));
                Assert.That(upgrades.Bonus(ability), Is.EqualTo(bonuses[i]).Within(.0001f));
                Assert.That(new VehicleUpgrades(prefix).Level(ability), Is.EqualTo(i + 1));
            }
            wallet.AwardRound((1, 0));
            Assert.That(upgrades.TryBuy(ability, wallet), Is.False, "max level must never take gold");
            Assert.That(wallet.Total, Is.EqualTo(100));
            upgrades.Clear();
            Assert.That(new VehicleUpgrades(prefix).Level(ability), Is.Zero);
        }

        [Test]
        public void BeingOneGoldShortDoesNotSpendOrChangeTheSavedLevel()
        {
            wallet.AwardRound((1, 0));
            upgrades.TryBuy(VehicleAbility.Handling, wallet);
            wallet.TrySpend(1); // 49 left
            Assert.That(upgrades.TryBuy(VehicleAbility.TopSpeed, wallet), Is.False);
            Assert.That(wallet.Total, Is.EqualTo(49));
            Assert.That(new VehicleUpgrades(prefix).Level(VehicleAbility.TopSpeed), Is.Zero);
        }
    }
}
