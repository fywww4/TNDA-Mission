using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Editor
{
    // 2026-11-17: the supplied pixel art for the breakable crate and oil barrel (two frames each: intact, dented) and
    // the item crate (star box). Sources are the 8x previews in Art/Source; the Art PNGs are those reduced to 1x.
    // Safe to rerun: imports at 32 PPU like the cars, then swaps the drawn shapes in GAME3_Mode7 for one Art sprite.
    public static class Mode7PropArt
    {
        private const string ArtPath = "Assets/Prototype/Game3/Art/";
        private const string CratePath = ArtPath + "PropCrate.png", BarrelPath = ArtPath + "PropBarrel.png", ItemCratePath = ArtPath + "ItemCrate.png";

        public static Sprite ItemCrate()
        {
            // 20x20; the pivot sits pickupLift (.3 = 9.6px) above the bottom, so the box stands on the floor.
            Import(ItemCratePath, 1, 20, 20, 10f / 20);
            return AssetDatabase.LoadAllAssetRepresentationsAtPath(ItemCratePath).OfType<Sprite>().First();
        }

        [MenuItem("TNDA/Mode 7/Apply prop art")]
        public static void ApplyMenu() => Apply();

        public static object Apply(string scenePath = Mode7Layout.ScenePath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before applying prop art.");
            // Pivots sit propLift (.32 = ~10px) above the bottom row, so the standing sprite touches the floor.
            var crate = Import(CratePath, 2, 24, 24, 10f / 24);
            var barrel = Import(BarrelPath, 2, 20, 24, 10f / 24);
            var item = ItemCrate();

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var game = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<DemolitionGame>(true)).First(g => g != null);
                Undo.RecordObject(game, "Mode 7 prop art");
                int crates = 0, barrels = 0, pickups = 0;
                foreach (var prop in game.props)
                {
                    if (prop.view == null) continue;
                    Sprite[] frames = prop.view.name.StartsWith("CRATE") ? crate : prop.view.name.StartsWith("OIL BARREL") ? barrel : null;
                    if (frames == null) continue;
                    if (frames == crate) crates++; else barrels++;
                    foreach (var part in prop.view.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.name != "Art").ToArray())
                        Undo.DestroyObjectImmediate(part.gameObject);
                    var art = prop.view.Find("Art")?.GetComponent<SpriteRenderer>();
                    if (art == null)
                    {
                        art = new GameObject("Art", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                        Undo.RegisterCreatedObjectUndo(art.gameObject, "Mode 7 prop art");
                        art.transform.SetParent(prop.view, false);
                        art.gameObject.layer = prop.view.gameObject.layer;
                    }
                    art.sprite = frames[0];
                    art.color = Color.white;
                    art.sortingOrder = 4;
                    prop.art = art;
                    prop.intactSprite = frames[0];
                    prop.dentedSprite = frames[1];
                }
                foreach (var pickup in game.pickups)
                {
                    var art = pickup.view != null ? pickup.view.Find("Crate")?.GetComponent<SpriteRenderer>() : null;
                    if (art == null) continue;
                    Undo.RecordObject(art, "Mode 7 prop art");
                    art.sprite = item;
                    pickups++;
                }
                EditorUtility.SetDirty(game);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                return new { scene = scenePath, crates, barrels, pickups, saved = !scene.isDirty };
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        // Point, uncompressed, 32 PPU; frames side by side, named <file>_<n>.
        private static Sprite[] Import(string path, int frames, int w, int h, float pivotY)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            var metas = Enumerable.Range(0, frames).Select(i => new SpriteMetaData { name = name + "_" + i, rect = new Rect(i * w, 0, w, h),
                alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(.5f, pivotY) }).ToArray();
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
#pragma warning disable 618
            importer.spritesheet = metas;
#pragma warning restore 618
            importer.SaveAndReimport();
            var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>().ToArray();
            return Enumerable.Range(0, frames).Select(i => sprites.First(s => s.name == name + "_" + i)).ToArray();
        }
    }
}
