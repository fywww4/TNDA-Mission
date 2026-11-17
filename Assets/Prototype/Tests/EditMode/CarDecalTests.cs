using NUnit.Framework;
using UnityEngine;

namespace TNDA.Prototype.Tests
{
    public class CarDecalTests
    {
        private const string Prefix = "TNDA.Test.Decals.", GoldKey = "TNDA.Test.DecalGold";

        [SetUp, TearDown]
        public void ClearTestSave()
        {
            new CarDecals(Prefix).Clear();
            new GoldWallet(GoldKey).Clear();
        }

        private static GoldWallet Wallet(int gold)
        {
            var wallet = new GoldWallet(GoldKey);
            wallet.Clear();
            PlayerPrefs.SetInt(GoldKey, gold);
            return new GoldWallet(GoldKey);
        }

        [Test]
        public void FourPatternsCost100To500Gold()
        {
            Assert.That(CarDecals.Price(CarDecal.TwinStripes), Is.EqualTo(100));
            Assert.That(CarDecals.Price(CarDecal.Flames), Is.EqualTo(200));
            Assert.That(CarDecals.Price(CarDecal.Lightning), Is.EqualTo(300));
            Assert.That(CarDecals.Price(CarDecal.Checker), Is.EqualTo(500));
        }

        [Test]
        public void BuyingSpendsThePriceOnceAndTooLittleGoldBuysNothing()
        {
            var decals = new CarDecals(Prefix);
            var wallet = Wallet(250);
            Assert.That(decals.TryBuy(CarDecal.Lightning, wallet), Is.False);
            Assert.That(decals.Owns(CarDecal.Lightning), Is.False);
            Assert.That(wallet.Total, Is.EqualTo(250));
            Assert.That(decals.TryBuy(CarDecal.Flames, wallet), Is.True);
            Assert.That(wallet.Total, Is.EqualTo(50));
            Assert.That(decals.Owns(CarDecal.Flames), Is.True);
            Assert.That(decals.TryBuy(CarDecal.Flames, Wallet(1000)), Is.False, "owned patterns are not sold twice");
        }

        [Test]
        public void EachSeatEquipsItsOwnOwnedPatternOrNone()
        {
            var decals = new CarDecals(Prefix);
            Assert.That(decals.Equipped(0), Is.EqualTo(CarDecal.None));
            Assert.That(decals.Equip(0, CarDecal.Checker), Is.False, "cannot wear a pattern nobody bought");
            var wallet = Wallet(1000);
            decals.TryBuy(CarDecal.TwinStripes, wallet);
            decals.TryBuy(CarDecal.Checker, wallet);
            Assert.That(decals.Equip(0, CarDecal.Checker), Is.True);
            Assert.That(decals.Equip(1, CarDecal.TwinStripes), Is.True);
            Assert.That(decals.Equipped(0), Is.EqualTo(CarDecal.Checker));
            Assert.That(decals.Equipped(1), Is.EqualTo(CarDecal.TwinStripes));
            Assert.That(decals.Equip(1, CarDecal.None), Is.True);
            Assert.That(decals.Equipped(1), Is.EqualTo(CarDecal.None));
        }

        [Test]
        public void CollectionAndEquipmentSurviveReloadAndClearEmptiesBoth()
        {
            var decals = new CarDecals(Prefix);
            decals.TryBuy(CarDecal.Flames, Wallet(1000));
            decals.Equip(1, CarDecal.Flames);
            var reloaded = new CarDecals(Prefix);
            Assert.That(reloaded.Owns(CarDecal.Flames), Is.True);
            Assert.That(reloaded.Equipped(1), Is.EqualTo(CarDecal.Flames));
            Assert.That(reloaded.Equipped(0), Is.EqualTo(CarDecal.None));
            reloaded.Clear();
            var cleared = new CarDecals(Prefix);
            Assert.That(cleared.Owns(CarDecal.Flames), Is.False);
            Assert.That(cleared.Equipped(1), Is.EqualTo(CarDecal.None));
        }
    }
}
