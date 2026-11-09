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
