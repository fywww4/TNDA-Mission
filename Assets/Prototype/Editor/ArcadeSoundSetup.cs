using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Editor
{
    // Wires the recorded sounds and the 3-2-1-GO countdown into GAME1 / GAME2. Safe to run repeatedly.
    public static class ArcadeSoundSetup
    {
        private const string AudioPath = "Assets/Prototype/Audio/";
        // Countdown.mp3 beeps at 0, 1, 2 and 3 seconds; the last beep lands on GO.
        public const float CountdownSeconds = 3;

        public static void Apply(ArcadeGame game)
        {
            game.countdownSeconds = CountdownSeconds;
            game.countdownClip = Clip("Countdown");
            game.hitClip = Clip("Hit");
            if (game is BossClimbGame boss) boss.shootClip = Clip("Shooting");
            if (game is EliminationRaceGame race) race.engineClip = Clip("Speed");
            if (game is DemolitionGame demolition) demolition.engineClip = Clip("Speed");
            if (game.countdownLabel == null) game.countdownLabel = CreateCountdownLabel(game);
            EditorUtility.SetDirty(game);
        }

        // Applies the setup to an already built scene without rebuilding it.
        public static object PatchScene(string scenePath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before patching " + scenePath);
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool newlyOpened = !scene.isLoaded;
            if (newlyOpened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            var game = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<ArcadeGame>(true)).FirstOrDefault(g => g != null);
            if (game == null) throw new InvalidOperationException(scenePath + " has no ArcadeGame.");
            Apply(game);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var summary = new
            {
                scene = scenePath, game = game.GetType().Name, game.countdownSeconds,
                countdown = game.countdownClip != null, hit = game.hitClip != null,
                shoot = (game as BossClimbGame)?.shootClip != null,
                engine = (game as EliminationRaceGame)?.engineClip != null || (game as DemolitionGame)?.engineClip != null,
                label = game.countdownLabel != null, saved = !scene.isDirty
            };
            if (newlyOpened) EditorSceneManager.CloseScene(scene, true);
            return summary;
        }

        private static AudioClip Clip(string name)
        {
            string path = AudioPath + name + ".mp3";
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("Missing sound " + path);
            var settings = importer.defaultSampleSettings;
            // Short effects stay decompressed in memory so they fire without latency (and the engine loop can read samples).
            if (settings.loadType != AudioClipLoadType.DecompressOnLoad || !importer.forceToMono)
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static TMP_Text CreateCountdownLabel(ArcadeGame game)
        {
            var cabinet = game.header.transform.parent;
            var rect = new GameObject("Countdown", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(cabinet, false);
            rect.SetAsLastSibling();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(60, 105);
            rect.sizeDelta = new Vector2(200, 110);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = game.header.font;
            label.fontSize = 64;
            label.fontStyle = FontStyles.Bold;
            label.color = ArcadeGame.Yellow;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.text = "";
            return label;
        }
    }
}
