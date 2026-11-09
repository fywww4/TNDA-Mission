using NUnit.Framework;
using UnityEngine;

namespace TNDA.Prototype.Tests
{
    // The Mode 7 cabinet is 512x448 (text) over 256x224 art (2x2 coarse pixels), shown at a whole-number scale.
    public class HybridResolutionTests
    {
        [TestCase(1920, 1080, 2)]
        [TestCase(1024, 896, 2)]
        [TestCase(2560, 1440, 3)]
        [TestCase(1023, 896, 1)]
        [TestCase(1280, 720, 1)]
        [TestCase(400, 300, 1)]
        public void Display_scale_is_the_largest_whole_multiple_that_fits(int width, int height, int scale)
        {
            Assert.AreEqual(scale, HybridResolution.DisplayScale(width, height));
        }

        [Test]
        public void One_player_view_fills_the_screen()
        {
            Assert.AreEqual(new Rect(0, 0, 1, 1), HybridResolution.ViewRect(0, false));
        }

        [Test]
        public void Two_player_views_split_top_and_bottom_into_512x224_halves()
        {
            Assert.AreEqual(new Rect(0, .5f, 1, .5f), HybridResolution.ViewRect(0, true), "P1 on top");
            Assert.AreEqual(new Rect(0, 0, 1, .5f), HybridResolution.ViewRect(1, true), "P2 below");
        }

        // Art pixels (bottom-left origin on the 256x224 art) to cabinet units (centre origin, 512x448).
        [TestCase(0, 0, -256, -224)]
        [TestCase(128, 112, 0, 0)]
        [TestCase(256, 224, 256, 224)]
        [TestCase(10.5f, 200, -235, 176)]
        public void Art_pixels_map_to_cabinet_units_at_twice_the_size(float ax, float ay, float cx, float cy)
        {
            Assert.AreEqual(new Vector2(cx, cy), HybridResolution.ArtToCabinet(new Vector2(ax, ay)));
        }

        // A name tag or score pop-up projected by one chase view (art pixels, z = depth in front of the camera) shows
        // only inside that view's own share of the art texture.
        private static readonly Rect P1Half = new Rect(0, 112, 256, 112), P2Half = new Rect(0, 0, 256, 112);

        [Test]
        public void Tag_inside_its_view_shows_at_the_cabinet_point()
        {
            Assert.IsTrue(HybridResolution.TagOnView(new Vector3(128, 50, 4), P2Half, out var cabinet));
            Assert.AreEqual(new Vector2(0, -124), cabinet);
        }

        [Test]
        public void Tag_behind_the_camera_is_hidden()
        {
            Assert.IsFalse(HybridResolution.TagOnView(new Vector3(128, 50, -4), P2Half, out _));
        }

        [Test]
        public void Tag_that_falls_in_the_other_half_is_hidden()
        {
            Assert.IsFalse(HybridResolution.TagOnView(new Vector3(128, 150, 4), P2Half, out _), "P2's camera, P1's half");
            Assert.IsFalse(HybridResolution.TagOnView(new Vector3(128, 100, 4), P1Half, out _), "P1's camera, P2's half");
        }

        [Test]
        public void Tag_off_the_side_of_the_view_is_hidden()
        {
            Assert.IsFalse(HybridResolution.TagOnView(new Vector3(-3, 150, 4), P1Half, out _));
            Assert.IsFalse(HybridResolution.TagOnView(new Vector3(260, 150, 4), P1Half, out _));
        }

        // How tall one art pixel of a perspective view is in world units at a given depth (things at that distance).
        [TestCase(3.2f, 60, 224, .016496f)]
        [TestCase(3.2f, 60, 112, .032991f)]
        [TestCase(12f, 60, 112, .123718f)]
        public void Art_pixel_height_in_world_units_grows_with_depth(float depth, float fov, float viewPixels, float world)
        {
            Assert.AreEqual(world, HybridResolution.WorldPerArtPixel(depth, fov, viewPixels), 1e-5f);
        }

        // The top-down minimap camera shows the whole area in a rect of the given width / height.
        [Test]
        public void Minimap_fits_a_wide_arena_by_its_width()
        {
            Assert.AreEqual(5f, HybridResolution.FitOrthographicSize(new Vector2(20, 6), 2), 1e-5f);
        }

        [Test]
        public void Minimap_fits_a_tall_arena_by_its_height()
        {
            Assert.AreEqual(4f, HybridResolution.FitOrthographicSize(new Vector2(10, 8), 2), 1e-5f);
        }
    }
}
