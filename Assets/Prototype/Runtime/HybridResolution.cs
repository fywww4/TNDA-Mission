using UnityEngine;

namespace TNDA.Prototype
{
    // Mode 7 hybrid resolution (Docs/adr/0001): the cabinet is 512x448 for text and HUD, the scene art is drawn at
    // 256x224 and shown at 2x2 coarse pixels. The whole cabinet is shown at a whole-number scale.
    public static class HybridResolution
    {
        public const int CabinetWidth = 512, CabinetHeight = 448;
        public const int ArtWidth = 256, ArtHeight = 224;
        // Cabinet pixels per art pixel.
        public const int ArtScale = CabinetWidth / ArtWidth;

        // The largest whole multiple of the cabinet that fits the screen, at least 1 (1024x896 or larger shows 2x).
        public static int DisplayScale(int screenWidth, int screenHeight) =>
            Mathf.Max(1, Mathf.Min(screenWidth / CabinetWidth, screenHeight / CabinetHeight));

        // A chase camera's share of the screen (normalised): 1P full screen; 2P P1 on top, P2 below, 512x224 each.
        public static Rect ViewRect(int seat, bool twoPlayers) =>
            !twoPlayers ? new Rect(0, 0, 1, 1) : new Rect(0, seat == 0 ? .5f : 0, 1, .5f);

        // A point on the art texture (pixels, bottom-left origin) in cabinet units (512x448, centre origin), the space
        // the name tags and score pop-ups are placed in.
        public static Vector2 ArtToCabinet(Vector2 artPixel) =>
            artPixel * ArtScale - new Vector2(CabinetWidth, CabinetHeight) * .5f;

        // Orthographic size that shows the whole area (world units) in a view of the given width / height.
        public static float FitOrthographicSize(Vector2 area, float aspect) => Mathf.Max(area.y / 2, area.x / 2 / aspect);
    }
}
