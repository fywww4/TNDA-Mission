using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    // The pyramid's continues, through the public match and the per-seat countdowns, with the production scene's cars.
    public class ContinueCountdownTests
    {
        private const float Dt = 1f / 60;
        private Scene scene;
        private DemolitionGame game;
        private DemolitionMatch match;
        private ContinueCountdowns continues;

        [SetUp]
        public void LoadArcadeMatch()
        {
            scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GAME3_Mode7.unity");
            game = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DemolitionGame>(true)).Single();
            match = new DemolitionMatch(game.cars, game.props, game.pickups, game.rockets, new MatchTuning {
                eliminations = game.eliminationSchedule, arenaMin = game.arenaMin, arenaMax = game.arenaMax,
                randomPropSpots = false
            });
            continues = new ContinueCountdowns(2, game.continueSeconds);
        }

        [TearDown]
        public void CloseScene() => EditorSceneManager.ClosePreviewScene(scene);

        // Humans on seats 0 and 1 hold the lowest scores; the computer cars score higher in seat order.
        private void TwoHumansAtTheBottom()
        {
            match.Reset(2);
            foreach (var car in match.Cars) car.score = 1000 + car.index * 100;
            match.Cars[0].score = 10;
            match.Cars[1].score = 20;
        }

        // Runs the next timed elimination (40 s, 80 s, then the 120 s final) and opens a countdown for each knocked-out
        // human the way the game does after a tick.
        private void NextElimination()
        {
            match.ClearEvents();
            match.EliminateNextStep();
            foreach (var e in match.Events.OfType<VehicleEliminated>()) continues.Offer(e.Car, match.Finished);
        }

        private void Advance(float dt, bool p1Pressed = false, bool p2Pressed = false)
        {
            continues.Advance(dt, seat => seat == 0 ? p1Pressed : p2Pressed,
                seat => match.Revive(match.Cars[seat]), seat => { });
        }

        // The computer cars score 1000 + 100 x seat; the human on seat 0 gets the given score.
        private void OneHumanScoring(float score)
        {
            match.Reset(1);
            foreach (var car in match.Cars) car.score = 1000 + car.index * 100;
            match.Cars[0].score = score;
        }

        [TestCase(10f, 1)]     // lowest: knocked out at 40 s
        [TestCase(1450f, 2)]   // survives 40 s, among the lowest two of the last four at 80 s
        public void AHumanKnockedOutAt40Or80SecondsCanContinueAndComesBackWithTheLowestSurvivingScore(float score, int steps)
        {
            OneHumanScoring(score);
            for (int i = 0; i < steps; i++) NextElimination();
            Assert.IsTrue(match.Cars[0].eliminated);
            Assert.IsFalse(match.Finished);
            Assert.IsTrue(continues.Waiting(0));

            Advance(Dt, p1Pressed: true);

            Assert.IsFalse(continues.Waiting(0));
            Assert.IsFalse(match.Cars[0].eliminated);
            Assert.AreEqual(match.Cars.Where(c => !c.eliminated && c != match.Cars[0]).Min(c => c.score), match.Cars[0].score);
            Assert.AreEqual(1, match.Cars[0].health);
            Assert.That(match.Cars[0].spawnProtection, Is.GreaterThan(0));
            Assert.AreEqual(ArenaItem.None, match.Cars[0].item);
        }

        [Test]
        public void TheLoserOfTheFinalCannotContinue()
        {
            OneHumanScoring(5000);            // survives 40 s and 80 s, then loses the final to a computer car
            match.Cars[7].score = 6000;
            for (int i = 0; i < 3; i++) NextElimination();

            Assert.IsTrue(match.Finished);
            Assert.AreEqual(7, match.Champion);
            Assert.IsTrue(match.Cars[0].eliminated);
            Assert.IsFalse(continues.Waiting(0));
            Assert.IsFalse(match.Revive(match.Cars[0]));
        }

        [Test]
        public void TwoHumansKnockedOutTogetherEachGetTheirOwnCountdown()
        {
            TwoHumansAtTheBottom();
            NextElimination();
            Assert.IsTrue(continues.Waiting(0));
            Assert.IsTrue(continues.Waiting(1));

            Advance(3);
            Assert.AreEqual(7, continues.SecondsShown(0));
            Assert.AreEqual(7, continues.SecondsShown(1));
        }

        [Test]
        public void OnePlayerRejoiningLeavesTheOthersCountdownRunning()
        {
            TwoHumansAtTheBottom();
            NextElimination();

            Advance(1, p2Pressed: true);

            Assert.IsFalse(continues.Waiting(1), "P2 rejoined");
            Assert.IsFalse(match.Cars[1].eliminated);
            Assert.IsTrue(continues.Waiting(0));
            Assert.IsTrue(match.Cars[0].eliminated);
            Assert.AreEqual(9, continues.SecondsShown(0), "P1 kept counting from its own start");
        }

        [Test]
        public void ContinuingIsFreeSoBothPlayersPressingTogetherBothRejoin()
        {
            TwoHumansAtTheBottom();
            NextElimination();

            Advance(Dt, p1Pressed: true, p2Pressed: true);

            Assert.IsFalse(match.Cars[0].eliminated);
            Assert.IsFalse(match.Cars[1].eliminated);
            Assert.IsFalse(continues.AnyWaiting);
        }

        [Test]
        public void WithoutPressingStartTheCountdownRunsOutAndNobodyRejoins()
        {
            OneHumanScoring(10);
            NextElimination();

            for (int i = 0; i < 10 * 60 + 5; i++) Advance(Dt);

            Assert.IsFalse(continues.AnyWaiting);
            Assert.IsFalse(continues.Used(0), "an unused countdown does not spend the continue");
            Assert.IsTrue(match.Cars[0].eliminated);
        }

        [Test]
        public void EachPlayerGetsOnlyOneContinueAMatchAndANewMatchGivesItBack()
        {
            OneHumanScoring(10);
            NextElimination();                        // out at 40 s
            Advance(Dt, p1Pressed: true);             // the one continue
            Assert.IsFalse(match.Cars[0].eliminated);
            Assert.IsTrue(continues.Used(0));

            match.Cars[0].score = 0;                  // lowest again at 80 s
            NextElimination();
            Assert.IsTrue(match.Cars[0].eliminated);
            Assert.IsFalse(match.Finished);
            Assert.IsFalse(continues.Waiting(0), "no second continue in the same match");

            continues.NewMatch();
            OneHumanScoring(10);
            NextElimination();
            Assert.IsTrue(continues.Waiting(0), "a new match gives the continue back");
        }

        [Test]
        public void AComputerCarKnockedOutNeverGetsACountdown()
        {
            OneHumanScoring(9000);
            NextElimination();
            Assert.AreEqual(4, match.Contenders);
            Assert.IsFalse(continues.AnyWaiting);
        }

        [Test]
        public void EachSeatHasItsOwnPanelCentredInItsHalfBelowTheSky()
        {
            Assert.AreEqual(2, game.continuePanels.Length);
            Assert.AreNotSame(game.continuePanels[0].panel, game.continuePanels[1].panel);

            var one = HybridResolution.ContinueCentre(0, false);
            Assert.AreEqual(new Vector2(256, 224), one);
            var top = HybridResolution.ContinueCentre(0, true);
            var bottom = HybridResolution.ContinueCentre(1, true);
            Assert.AreEqual(256, top.x);
            Assert.That(top.y - 58, Is.GreaterThanOrEqualTo(224), "P1's panel stays in P1's half");
            Assert.That(top.y + 58, Is.LessThanOrEqualTo(448 - 448 / 6), "and under the HUD sky");
            Assert.That(bottom.y - 58, Is.GreaterThanOrEqualTo(0));
            Assert.That(bottom.y + 58, Is.LessThanOrEqualTo(224 - 224 / 3 + 1), "P2's panel stays in P2's half, under P2's sky");
        }
    }
}
