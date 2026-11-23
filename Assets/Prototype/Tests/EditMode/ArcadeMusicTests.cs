using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    public class ArcadeMusicTests
    {
        private Scene scene;
        private DemolitionGame game;
        private ArcadeMusic music;

        [SetUp]
        public void OpenMode7()
        {
            scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GAME3_Mode7.unity");
            game = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DemolitionGame>(true)).Single();
            game.PreparePreview();
            music = game.GetComponent<ArcadeMusic>();
            Assert.That(music, Is.Not.Null);
        }

        [TearDown]
        public void CloseMode7() => EditorSceneManager.ClosePreviewScene(scene);

        [Test]
        public void AllTitleTabsAndTheGarageShareTheLobbyMusic()
        {
            Assert.That(music.TrackFor(game)?.name, Is.EqualTo("Week 15 - Sunburn SPF 3000"));

            game.SelectMode(CabinetMode.Challenge);
            Assert.That(music.TrackFor(game)?.name, Is.EqualTo("Week 15 - Sunburn SPF 3000"));

            game.SelectMode(CabinetMode.Campaign);
            Assert.That(music.TrackFor(game)?.name, Is.EqualTo("Week 15 - Sunburn SPF 3000"));

            game.OpenGarage();
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Garage));
            Assert.That(music.TrackFor(game)?.name, Is.EqualTo("Week 15 - Sunburn SPF 3000"));
        }

        [TestCase(1, "Sketchbook 2025-12-11_NEURO")]
        [TestCase(2, "Week 6 - Never Miss FIRE")]
        [TestCase(3, "Ludum Dare 30 04")]
        public void EachStageKeepsItsTrackThroughStoryCountdownAndPlay(int number, string track)
        {
            game.BeginStory(Campaign.Stages.Single(stage => stage.Number == number));
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Story));
            Assert.That(music.TrackFor(game)?.name, Is.EqualTo(track));

            while (game.Phase == ArcadePhase.Story) game.PressStart();
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Countdown));
            Assert.That(music.TrackFor(game)?.name, Is.EqualTo(track));

            FinishCountdown();
            Assert.That(music.TrackFor(game)?.name, Is.EqualTo(track));

            game.Back();
            Assert.That(music.TrackFor(game)?.name, Is.EqualTo("Week 15 - Sunburn SPF 3000"));
        }

        [Test]
        public void StartingAChallengePlaysItsTrackEvenIfTheTitleWasOnAnotherTab()
        {
            game.StartRun(1, Challenges.All[0]);
            Assert.That(music.TrackFor(game)?.name, Is.EqualTo("VGMA Challenge 06"));

            FinishCountdown();
            Assert.That(music.TrackFor(game)?.name, Is.EqualTo("VGMA Challenge 06"));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void RegularMatchesHaveNoAssignedTrack(int players)
        {
            game.StartRun(players);
            Assert.That(music.TrackFor(game), Is.Null);

            FinishCountdown();
            Assert.That(music.TrackFor(game), Is.Null);
        }

        [Test]
        public void SelectingAnUnassignedPlaylistIsSilent()
        {
            var cabinet = new GameObject("Cabinet with no assigned music");
            try
            {
                var emptyPlaylist = cabinet.AddComponent<ArcadeMusic>();
                Assert.That(emptyPlaylist.TrackFor(game), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(cabinet);
            }
        }

        private void FinishCountdown()
        {
            game.Advance(game.CountdownLeft + .05f, default, default);
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Playing));
        }
    }
}
