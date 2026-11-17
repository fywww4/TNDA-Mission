using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TNDA.Prototype.Editor
{
    // Ticket 16: the four 外觀 pattern sheets. Each is a 768x48 transparent layer laid out exactly like CarSheet.png
    // (16 frames of 48x48, frame 0 seen from behind, clockwise) and drawn over the car in white/black.
    // Until the user's GPT art arrives, simple placeholders are generated from CarSheet's body pixels (its four key
    // reds), so they always line up. Replace a PNG with the real art (same size) and re-run; the slicing stays.
    public static class Game3Decals
    {
        public const string ArtFolder = "Assets/Prototype/Game3/Art/";
        public static readonly string[] Sheets = { "DecalTwinStripes", "DecalFlames", "DecalLightning", "DecalChecker" };
        private const int Frame = 48, Frames = 16;

        // Generates any missing placeholder, imports all four like CarSheet, and returns their 64 frames in
        // CarDecal order (TwinStripes 0-15, Flames 16-31, Lightning 32-47, Checker 48-63).
        public static Sprite[] LoadFrames()
        {
            bool made = false;
            for (int d = 0; d < Sheets.Length; d++)
            {
                string path = ArtFolder + Sheets[d] + ".png";
                if (File.Exists(path)) continue;
                File.WriteAllBytes(path, Placeholder((CarDecal)(d + 1)).EncodeToPNG());
                made = true;
            }
            if (made) AssetDatabase.Refresh();
            var frames = new Sprite[Sheets.Length * Frames];
            for (int d = 0; d < Sheets.Length; d++)
            {
                string path = ArtFolder + Sheets[d] + ".png";
                Slice(path, Sheets[d]);
                foreach (var sprite in AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>())
                    frames[d * Frames + int.Parse(sprite.name.Substring(Sheets[d].Length + 1))] = sprite;
            }
            return frames;
        }

        // Same import as CarSheet: 32 PPU, point, uncompressed, pivot on the wheels' ground line.
        private static void Slice(string path, string name)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var metas = new SpriteMetaData[Frames];
            for (int i = 0; i < Frames; i++)
                metas[i] = new SpriteMetaData { name = name + "_" + i.ToString("00"), rect = new Rect(i * Frame, 0, Frame, Frame),
                    alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(.5f, 7f / 48) };
#pragma warning disable 618
            bool same = importer.spriteImportMode == SpriteImportMode.Multiple && importer.spritePixelsPerUnit == 32
                && importer.filterMode == FilterMode.Point && importer.textureCompression == TextureImporterCompression.Uncompressed
                && importer.spritesheet.Length == Frames && importer.spritesheet.Select(m => m.name).SequenceEqual(metas.Select(m => m.name));
            if (same) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritesheet = metas;
#pragma warning restore 618
            importer.SaveAndReimport();
        }

        // ---- Placeholders ----

        // Overwrites one sheet with its placeholder (keeps the .meta, so scene references survive). Not for real art.
        public static void RewritePlaceholder(CarDecal decal)
        {
            string path = ArtFolder + Sheets[(int)decal - 1] + ".png";
            File.WriteAllBytes(path, Placeholder(decal).EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static readonly Color32[] Keys =
            { new Color32(248, 120, 88, 255), new Color32(224, 64, 32, 255), new Color32(176, 24, 0, 255), new Color32(96, 0, 0, 255) };
        private static readonly Color32 Clear = new Color32(0, 0, 0, 0), Ink = new Color32(255, 255, 255, 255), Edge = new Color32(16, 16, 24, 255);

        private static Texture2D Placeholder(CarDecal decal)
        {
            var car = new Texture2D(2, 2);
            car.LoadImage(File.ReadAllBytes(ArtFolder + "CarSheet.png"));
            if (car.width != Frame * Frames || car.height != Frame) throw new InvalidOperationException("CarSheet.png must be 768x48.");
            var src = car.GetPixels32();
            var dst = new Color32[src.Length];
            for (int f = 0; f < Frames; f++)
            {
                // Body mask for this frame, in top-down rows (y = 0 is the top).
                var body = new bool[Frame, Frame];
                int x0 = Frame, x1 = -1, y0 = Frame, y1 = -1;
                for (int y = 0; y < Frame; y++)
                    for (int x = 0; x < Frame; x++)
                    {
                        var c = src[(Frame - 1 - y) * car.width + f * Frame + x];
                        if (c.a < 128 || !Keys.Any(k => Math.Abs(k.r - c.r) < 6 && Math.Abs(k.g - c.g) < 6 && Math.Abs(k.b - c.b) < 6)) continue;
                        body[x, y] = true;
                        x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
                    }
                if (x1 < 0) continue;
                var ink = new bool[Frame, Frame];
                var dark = new bool[Frame, Frame];
                Paint(decal, f, body, ink, dark, x0, x1, y0, y1);
                for (int y = 0; y < Frame; y++)
                    for (int x = 0; x < Frame; x++)
                    {
                        // White pattern; black where the pattern says so, or a one-pixel outline round flames and bolts.
                        Color32 c = Clear;
                        if (ink[x, y]) c = Ink;
                        else if (body[x, y] && (dark[x, y] || Outlines(decal) && Near(ink, x, y))) c = Edge;
                        dst[(Frame - 1 - y) * car.width + f * Frame + x] = c;
                    }
            }
            UnityEngine.Object.DestroyImmediate(car);
            var sheet = new Texture2D(Frame * Frames, Frame, TextureFormat.RGBA32, false);
            sheet.SetPixels32(dst);
            sheet.Apply();
            return sheet;
        }

        private static bool Outlines(CarDecal decal) => decal == CarDecal.Flames || decal == CarDecal.Lightning;

        private static bool Near(bool[,] ink, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if ((dx != 0 || dy != 0) && nx >= 0 && ny >= 0 && nx < Frame && ny < Frame && ink[nx, ny]) return true;
                }
            return false;
        }

        // Frame f is seen from f * 22.5 degrees: 0 from behind, 4 from the car's left (nose to the right), 8 head on.
        private static void Paint(CarDecal decal, int f, bool[,] body, bool[,] ink, bool[,] dark, int x0, int x1, int y0, int y1)
        {
            float angle = f * 22.5f * Mathf.Deg2Rad;
            float across = Mathf.Abs(Mathf.Cos(angle)); // 1: we look along the car's length
            float noseRight = Mathf.Sin(angle);          // +1: nose to the right, -1: to the left
            int w = x1 - x0 + 1, h = y1 - y0 + 1;
            float cx = (x0 + x1) * .5f;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (!body[x, y]) continue;
                    float u = (x - x0) / (float)Math.Max(1, w - 1), v = (y - y0) / (float)Math.Max(1, h - 1);
                    bool on = false;
                    switch (decal)
                    {
                        case CarDecal.TwinStripes:
                            if (across > .38f)
                            {
                                // Two bands either side of the centre line, closer together as the car turns side on.
                                float gap = Mathf.Max(1.5f, w * .12f * across), off = Mathf.Abs(x - cx);
                                on = off >= gap - 1 && off < gap + 1.5f;
                            }
                            else
                                // Side on, the stripes run along the top of the body.
                                on = y - TopOf(body, x, y0, y1) < 2;
                            break;
                        case CarDecal.Flames:
                        {
                            // Tongues licking back from the nose along the lower body.
                            float fromNose = noseRight > .38f ? 1 - u : noseRight < -.38f ? u : .5f;
                            float tongue = Mathf.Abs(Mathf.Repeat(x * .5f, 2) - 1); // 0..1 zigzag
                            float reach = (1 - fromNose) * .45f + tongue * .2f;
                            on = v > 1 - reach && v > .55f;
                            break;
                        }
                        case CarDecal.Lightning:
                        {
                            // A two-pixel zigzag bolt across the body.
                            float yBolt = y0 + h * (.35f + .3f * Mathf.Abs(Mathf.Repeat(u * 3, 2) - 1));
                            on = Mathf.Abs(y - yBolt) < 1.1f;
                            break;
                        }
                        case CarDecal.Checker:
                            // A racing-flag band through the middle of the body, 2-pixel white and black squares.
                            if (v >= .3f && v <= .62f)
                            {
                                on = ((x >> 1) + (y >> 1)) % 2 == 0;
                                dark[x, y] = !on;
                            }
                            break;
                    }
                    ink[x, y] = on;
                }
        }

        private static int TopOf(bool[,] body, int x, int y0, int y1)
        {
            for (int y = y0; y <= y1; y++) if (body[x, y]) return y;
            return y1;
        }
    }
}
