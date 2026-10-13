using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Editor
{
    // The game outputs a true 320x320 image, so text must be a pixel font drawn 1:1 on the pixel grid.
    // Cubic 11 is designed on an 11px grid: it is rasterised at exactly 11px (no SDF smoothing) with point
    // filtering, and every text uses 11px or a whole multiple (22, 44) so each glyph pixel is one screen pixel.
    public static class ArcadeFontSetup
    {
        public const int PixelSize = 11;
        public const string FontPath = "Assets/Prototype/Fonts/Cubic_11 Pixel 11px.asset";
        private const string SourcePath = "Assets/Prototype/Fonts/Cubic_11.ttf";
        // Scripts whose string literals are shown on screen; their characters are pre-rasterised.
        private static readonly string[] TextSources = {
            "Assets/Prototype/Runtime/DemolitionGame.cs", "Assets/Prototype/Runtime/ArcadeGame.cs",
            "Assets/Prototype/Runtime/ArcadeVehicle.cs", "Assets/Prototype/Editor/Game3Text.cs" };

        public static TMP_FontAsset GetOrCreate()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            if (source == null) throw new InvalidOperationException("Import " + SourcePath + " before creating the pixel font.");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                font = TMP_FontAsset.CreateFontAsset(source, PixelSize, 1, GlyphRenderMode.RASTER_HINTED, 512, 512,
                    AtlasPopulationMode.Dynamic, true);
                if (font == null) throw new InvalidOperationException("Could not create the Cubic 11 pixel font.");
                font.name = "Cubic_11 Pixel 11px";
                var face = font.faceInfo;
                face.scale = 1;
                font.faceInfo = face;
                SaveFont(font, FontPath);
            }
            // Whole-pixel line metrics: a 12.83px line height puts every other line on a half pixel and drops strokes.
            var metrics = font.faceInfo;
            metrics.lineHeight = Mathf.Ceil(metrics.lineHeight);
            metrics.ascentLine = Mathf.Round(metrics.ascentLine);
            metrics.descentLine = Mathf.Round(metrics.descentLine);
            metrics.capLine = Mathf.Round(metrics.capLine);
            metrics.meanLine = Mathf.Round(metrics.meanLine);
            metrics.baseline = 0;
            font.faceInfo = metrics;
            // Pre-rasterise every character the game prints so nothing is generated at runtime.
            string characters = UsedCharacters();
            if (!font.HasCharacters(characters) && !font.TryAddCharacters(characters, out string missing) && missing.Trim().Length > 0)
                Debug.LogWarning("Cubic 11 lacks glyphs: " + missing);
            foreach (var atlas in font.atlasTextures)
            {
                atlas.filterMode = FilterMode.Point;
                if (!AssetDatabase.Contains(atlas)) { atlas.name = font.name + " Atlas"; AssetDatabase.AddObjectToAsset(atlas, font); }
                EditorUtility.SetDirty(atlas);
            }
            var serialized = new SerializedObject(font);
            serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }

        public static void Apply(ArcadeGame game)
        {
            var font = GetOrCreate();
            foreach (var text in game.GetComponentsInChildren<TMP_Text>(true))
            {
                Undo.RecordObject(text, "Use Cubic 11 pixel font");
                text.font = font;
                text.fontSharedMaterial = font.material;
                text.enableAutoSizing = false;
                text.fontStyle &= ~FontStyles.Bold; // synthetic bold smears a pixel font
                // Snap to the 11px grid: 11 for body text, whole multiples for larger text.
                text.fontSize = Mathf.Max(1, Mathf.RoundToInt(text.fontSize / PixelSize)) * PixelSize;
                text.characterSpacing = 0;
                text.UpdateMeshPadding();
                EditorUtility.SetDirty(text);
            }
        }

        public static object PatchScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before changing fonts.");
            const string path = "Assets/Scenes/GAME3.unity";
            var scene = SceneManager.GetSceneByPath(path);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var game = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DemolitionGame>(true)).Single();
            Apply(game);
            game.PreparePreview();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            return new { scene = path, font = FontPath, glyphs = font.characterTable.Count, texts = game.GetComponentsInChildren<TMP_Text>(true).Length, saved = !scene.isDirty };
        }

        private static string UsedCharacters()
        {
            var chars = new HashSet<char>(Enumerable.Range(32, 95).Select(i => (char)i));
            foreach (char c in "←→↑↓／：，、。！？（）［］　") chars.Add(c);
            foreach (var file in TextSources)
            {
                if (!File.Exists(file)) continue;
                var code = File.ReadAllText(file);
                // Characters inside string literals only.
                bool inString = false;
                for (int i = 0; i < code.Length; i++)
                {
                    if (code[i] == '"' && (i == 0 || code[i - 1] != '\\')) { inString = !inString; continue; }
                    if (inString && code[i] > 127) chars.Add(code[i]);
                }
            }
            return new string(chars.ToArray());
        }

        private static void SaveFont(TMP_FontAsset font, string path)
        {
            AssetDatabase.CreateAsset(font, path);
            font.material.name = font.name + " Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            for (int i = 0; i < font.atlasTextures.Length; i++)
            {
                font.atlasTextures[i].name = font.name + " Atlas " + i;
                AssetDatabase.AddObjectToAsset(font.atlasTextures[i], font);
            }
            EditorUtility.SetDirty(font);
        }
    }
}
