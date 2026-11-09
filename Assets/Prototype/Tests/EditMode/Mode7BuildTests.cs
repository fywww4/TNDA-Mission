using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace TNDA.Prototype.Tests
{
    public class Mode7BuildTests
    {
        [Test]
        public void PlayerLaunchesOnlyTheMode7Scene()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.That(scenes.Select(s => s.path), Is.EqualTo(new[] { "Assets/Scenes/GAME3_Mode7.unity" }));
            Assert.That(scenes[0].enabled, Is.True);
        }

        [Test]
        public void GroundShaderIsAReferencedSceneDependency()
        {
            var dependencies = AssetDatabase.GetDependencies("Assets/Scenes/GAME3_Mode7.unity", true);
            Assert.That(dependencies, Does.Contain("Assets/Prototype/Game3/Art/Mode7Ground.shader"),
                "The build must include the shader via a scene material reference.");
        }
    }
}
