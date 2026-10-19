using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Editor
{
    // Random-item crates and the last-place cherry: sprites from the supplied pictures plus scene wiring.
    public static class Game3ItemArt
    {
        private const string ArtPath = "Assets/Prototype/Game3/Art";
        private static readonly Color32 Clear = new Color32(0, 0, 0, 0), Black = new Color32(0, 0, 0, 255);

        public static object PatchScene(string scenePath = "Assets/Scenes/GAME3.unity")
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before patching GAME3 items.");
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool newlyOpened = !scene.isLoaded;
            if (newlyOpened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            var game = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<DemolitionGame>(true)).First(g => g != null);
            var sprites = Apply(game);
            game.instructions = CabinetText.Instructions;
            game.PreparePreview();
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var result = new { scene = scenePath, crate = Size(sprites[0]), cherry = Size(sprites[1]), explosion = Size(sprites[2]), rocket = Size(sprites[3]), shield = Size(sprites[4]), oil = Size(sprites[5]),
                slick = Size(sprites[6]), lava = Size(sprites[7]), saved = !scene.isDirty };
            if (newlyOpened) EditorSceneManager.CloseScene(scene, true);
            return result;
        }

        public static Sprite[] Apply(DemolitionGame game)
        {
            // The supplied crate photo turns into noise at 16px, so the crate is redrawn from its design:
            // a wooden frame around vertical planks (no diagonal brace, which the breakable X box already uses).
            // White and 12px since 2026-10-15 (was yellow, 16px).
            var crate = DrawSprite("ItemCrate", new[] {
                "############",
                "#WWWWWWWWWW#",
                "#W########W#",
                "#W#W#WW#W#W#",
                "#W#W#WW#W#W#",
                "#W#W#WW#W#W#",
                "#W#W#WW#W#W#",
                "#W#W#WW#W#W#",
                "#W#W#WW#W#W#",
                "#W########W#",
                "#WWWWWWWWWW#",
                "############" });
            // Cherry: from the supplied picture with its white backdrop removed; keep the gap between the two fruits.
            var cherry = MakeSprite("Cherry", 14, 1, .34f, new Rect(0, 0, 1, 1), 0);
            Undo.RecordObject(game, "GAME3 crates and cherry");
            // Breakable obstacles are white (2026-10-15).
            foreach (var prop in game.props)
                foreach (var part in prop.view.GetComponentsInChildren<SpriteRenderer>(true))
                    if (part.color != Color.black && part.color != ArcadeGame.White)
                    {
                        Undo.RecordObject(part, "GAME3 crates and cherry");
                        part.color = ArcadeGame.White;
                    }
            foreach (var pickup in game.pickups)
            {
                pickup.item = ArenaItem.None; // random crate
                foreach (Transform child in pickup.view) child.gameObject.SetActive(child.name == "Crate");
                var art = pickup.view.Find("Crate")?.GetComponent<SpriteRenderer>();
                if (art == null)
                {
                    art = new GameObject("Crate", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                    Undo.RegisterCreatedObjectUndo(art.gameObject, "GAME3 crates and cherry");
                    art.transform.SetParent(pickup.view, false);
                }
                art.sprite = crate;
                art.sortingOrder = 4;
                pickup.view.name = "Item crate";
            }
            // Held-item icons trail behind each car (2026-10-16). Rocket: from the supplied pixel picture. Shield: redrawn
            // from the supplied picture's design (spiked rim, blue face, white "!"), whose dot vanishes when reduced to
            // 12px. The cherry reuses its sprite. An eaten cherry shows as the striped car instead of an icon.
            var rocket = MakeSprite("Rocket", 12, 1, .5f, new Rect(0, 0, 1, 1), 0, RocketColour);
            var shield = DrawSprite("Shield", new[] {
                "W....WW....W",
                "WWWWWWWWWWWW",
                "WCCCCWWCCCCW",
                "WCCCCWWCCCCW",
                "WCCCCWWCCCCW",
                "WCCCCWWCCCCW",
                ".WCCCWWCCCW.",
                ".WCCCCCCCCW.",
                "..WCCWWCCW..",
                "...WCCCCW...",
                "....WCCW....",
                ".....WW....." });
            // Oil (2026-10-16): the icon is the supplied black drop, given a white outline and highlight so it shows on
            // the black floor (source Art/Source/OilSource.png); the slick on the floor is the supplied yellow splat.
            var oil = DrawSprite("Oil", new[] {
                "....WW....",
                "...W##W...",
                "...W##W...",
                "..W####W..",
                "..W####W..",
                ".W######W.",
                ".W######W.",
                "W########W",
                "W#W######W",
                "W#W######W",
                ".W#W####W.",
                "..WWWWWW.." });
            var slick = MakeSprite("OilSlick", 12, 1, .5f, new Rect(0, 0, 1, 1), 0, c => 3);
            // Lava: the supplied lava picture in the four colours (red-orange black, orange yellow, bright yellow white),
            // 32px = 1 world unit, the pool's size.
            var lava = MakeSprite("Lava", 32, 1, .5f, new Rect(0, 0, 1, 1), 0, LavaColour);
            game.itemSprites = new[] { shield, rocket, cherry, oil };
            game.itemViews = new SpriteRenderer[game.cars.Length];
            for (int i = 0; i < game.cars.Length; i++)
            {
                var old = game.cars[i].transform.Find("Cherry power");
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
                var icon = game.cars[i].transform.Find("Held item")?.GetComponent<SpriteRenderer>();
                if (icon == null)
                {
                    icon = new GameObject("Held item", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                    Undo.RegisterCreatedObjectUndo(icon.gameObject, "GAME3 held items");
                    icon.transform.SetParent(game.cars[i].transform, false);
                }
                icon.sortingOrder = 21;
                icon.gameObject.SetActive(false);
                game.itemViews[i] = icon;
            }
            // Rocket-hit explosion from the supplied picture: one view per rocket so two hits can overlap.
            var blast = MakeSprite("Explosion", 32, 1, .25f, new Rect(0, 0, 1, 1), 0, ExplosionColour);
            var effects = game.transform.Find("Effects");
            if (effects == null)
            {
                effects = new GameObject("Effects").transform;
                Undo.RegisterCreatedObjectUndo(effects.gameObject, "GAME3 crates and cherry");
                effects.SetParent(game.transform, false);
            }
            SpriteRenderer EffectView(string name, Sprite sprite, int order)
            {
                var view = effects.Find(name)?.GetComponent<SpriteRenderer>();
                if (view == null)
                {
                    view = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                    Undo.RegisterCreatedObjectUndo(view.gameObject, "GAME3 lava and oil");
                    view.transform.SetParent(effects, false);
                }
                view.sprite = sprite;
                view.sortingOrder = order;
                view.gameObject.SetActive(false);
                return view;
            }
            // Floor hazards draw under the cars (order 3): the lava pool and one oil slick per rocket/oil pool slot.
            game.lavaView = EffectView("Lava pool", lava, 3);
            for (int i = 0; i < game.rockets.Length; i++) game.rockets[i].slickView = EffectView("Oil slick " + i, slick, 3);
            game.explosions = new SpriteRenderer[game.rockets.Length];
            for (int i = 0; i < game.explosions.Length; i++)
            {
                var view = effects.Find("Rocket explosion " + i)?.GetComponent<SpriteRenderer>();
                if (view == null)
                {
                    view = new GameObject("Rocket explosion " + i, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                    Undo.RegisterCreatedObjectUndo(view.gameObject, "GAME3 crates and cherry");
                    view.transform.SetParent(effects, false);
                }
                view.sprite = blast;
                view.sortingOrder = 30;
                view.gameObject.SetActive(false);
                game.explosions[i] = view;
            }
            EditorUtility.SetDirty(game);
            return new[] { crate, cherry, blast, rocket, shield, oil, slick, lava };
        }

        // The supplied 128x128 victory picture (Art/Source/VictorySource.png) snapped to the four colours, plus a copy
        // with the cyan car (Player1) turned yellow for a Player2 win.
        public static Sprite[] VictorySprites()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(ArtPath + "/Source/VictorySource.png"));
            int w = texture.width, h = texture.height;
            var pixels = texture.GetPixels32();
            UnityEngine.Object.DestroyImmediate(texture);
            var palette = new Color32[] { Black, ArcadeGame.White, ArcadeGame.Cyan, ArcadeGame.Yellow };
            var p1 = new Color32[pixels.Length];
            var p2 = new Color32[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                int best = 0, bestDistance = int.MaxValue;
                for (int k = 0; k < palette.Length; k++)
                {
                    int dr = c.r - palette[k].r, dg = c.g - palette[k].g, db = c.b - palette[k].b;
                    int distance = dr * dr + dg * dg + db * db;
                    if (distance < bestDistance) { bestDistance = distance; best = k; }
                }
                p1[i] = palette[best];
                p2[i] = best == 2 ? palette[3] : palette[best];
            }
            return new[] { SaveSprite("Victory", p1, w, h), SaveSprite("VictoryP2", p2, w, h) };
        }

        // Rows top to bottom: '#' black, 'Y' yellow, 'W' white, 'C' cyan, anything else transparent.
        private static Sprite DrawSprite(string name, string[] rows)
        {
            int w = rows[0].Length, h = rows.Length;
            var result = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    char c = rows[h - 1 - y][x];
                    result[y * w + x] = c == '#' ? Black : c == 'Y' ? ArcadeGame.Yellow : c == 'W' ? ArcadeGame.White
                        : c == 'C' ? ArcadeGame.Cyan : Clear;
                }
            return SaveSprite(name, result, w, h);
        }

        // Removes the border-connected backdrop, crops, and reduces to the black/white/cyan/yellow palette at 32 PPU.
        private static Sprite MakeSprite(string name, int outHeight, float darkWeight, float clearThreshold, Rect crop, float outlineSaturation,
            Func<Color32, int> classify = null)
        {
            string source = ArtPath + "/Source/" + name + "Source.png";
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(source));
            int w = texture.width, h = texture.height;
            var pixels = texture.GetPixels32();
            UnityEngine.Object.DestroyImmediate(texture);

            var background = new bool[pixels.Length];
            var queue = new Queue<int>();
            bool Backdrop(int p) => pixels[p].a < 128 || Mathf.Min(pixels[p].r, Mathf.Min(pixels[p].g, pixels[p].b)) > 225;
            void Seed(int p) { if (!background[p] && Backdrop(p)) { background[p] = true; queue.Enqueue(p); } }
            for (int x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { Seed(y * w); Seed(y * w + w - 1); }
            while (queue.Count > 0)
            {
                int p = queue.Dequeue(), x = p % w, y = p / w;
                if (x > 0) Seed(p - 1);
                if (x < w - 1) Seed(p + 1);
                if (y > 0) Seed(p - w);
                if (y < h - 1) Seed(p + w);
            }
            int minX = w, minY = h, maxX = -1, maxY = -1;
            int cropX0 = (int)(crop.xMin * w), cropX1 = (int)(crop.xMax * w), cropY0 = (int)(crop.yMin * h), cropY1 = (int)(crop.yMax * h);
            for (int p = 0; p < pixels.Length; p++)
                if (!background[p] && p % w >= cropX0 && p % w < cropX1 && p / w >= cropY0 && p / w < cropY1)
                {
                    int x = p % w, y = p / w;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
            int boxW = maxX - minX + 1, boxH = maxY - minY + 1;
            int outW = Mathf.Max(4, Mathf.RoundToInt(outHeight * boxW / (float)boxH));

            var palette = new Color32[] { Black, ArcadeGame.White, ArcadeGame.Cyan, ArcadeGame.Yellow };
            var result = new Color32[outW * outHeight];
            for (int oy = 0; oy < outHeight; oy++)
                for (int ox = 0; ox < outW; ox++)
                {
                    int x0 = minX + ox * boxW / outW, x1 = minX + (ox + 1) * boxW / outW;
                    int y0 = minY + oy * boxH / outHeight, y1 = minY + (oy + 1) * boxH / outHeight;
                    var votes = new float[5]; // clear, black, white, cyan, yellow
                    for (int y = y0; y < y1; y++)
                        for (int x = x0; x < x1; x++)
                        {
                            int p = y * w + x;
                            votes[background[p] ? 0 : 1 + (classify != null ? classify(pixels[p]) : Classify(pixels[p], outlineSaturation))]++;
                        }
                    float total = votes.Sum();
                    votes[1] *= darkWeight;
                    int pick = votes[0] >= total * clearThreshold ? 0 : Array.IndexOf(votes, votes.Skip(1).Max(), 1);
                    result[oy * outW + ox] = pick == 0 ? Clear : palette[pick - 1];
                }
            return SaveSprite(name, result, outW, outHeight);
        }

        internal static Sprite SaveSprite(string name, Color32[] result, int width, int height)
        {
            string output = ArtPath + "/" + name + ".png";
            var small = new Texture2D(width, height, TextureFormat.RGBA32, false);
            small.SetPixels32(result);
            small.Apply();
            var png = small.EncodeToPNG();
            // Repeated scene patches produce identical art; avoid rewriting a loaded, memory-mapped asset.
            if (!File.Exists(output) || !File.ReadAllBytes(output).SequenceEqual(png))
                File.WriteAllBytes(output, png);
            UnityEngine.Object.DestroyImmediate(small);
            AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(output);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(output);
        }

        // 0 black, 1 white, 2 cyan, 3 yellow. Dull mid tones (wood frame, shading) read as black outline.
        // Explosion: red-orange spikes and gold cloud yellow, darker orange shading black, light-yellow core white.
        private static int ExplosionColour(Color32 c)
        {
            Color.RGBToHSV(c, out float hue, out float sat, out float val);
            float degrees = hue * 360;
            if (val < .3f) return 0;
            if (sat < .2f || degrees >= 48) return 1;
            if (degrees >= 30 && degrees < 40 && val < .93f) return 0;
            return 3;
        }

        // Lava: dark red-orange black, orange and amber yellow, bright yellow and pale yellow white.
        private static int LavaColour(Color32 c)
        {
            Color.RGBToHSV(c, out float hue, out float sat, out float val);
            float degrees = hue * 360;
            if (degrees >= 42 && val > .85f) return 1;
            if (degrees < 16) return 0;
            return 3;
        }

        // Rocket: red nose and fins and the orange-yellow flame yellow, pale-blue body white, blue window cyan, grey nozzle black.
        private static int RocketColour(Color32 c)
        {
            Color.RGBToHSV(c, out float hue, out float sat, out float val);
            float degrees = hue * 360;
            if (sat < .25f) return val > .8f ? 1 : 0;
            if (degrees > 170 && degrees < 260) return 2;
            return 3;
        }

        private static int Classify(Color32 c, float outlineSaturation)
        {
            Color.RGBToHSV(c, out float hue, out float sat, out float val);
            // Weathered (less saturated) wood becomes the black frame on the crate.
            if (val < .3f || val < .74f && sat < .62f || sat < outlineSaturation) return 0;
            if (sat < .25f) return 1;
            float degrees = hue * 360;
            return degrees > 150 && degrees < 260 ? 2 : 3;
        }

        private static string Size(Sprite sprite) => sprite.name + " " + sprite.rect.width + "x" + sprite.rect.height;
    }
}
