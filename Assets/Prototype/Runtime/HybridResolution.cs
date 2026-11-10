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

        // Where a seat's continue panel is centred (cabinet pixels, bottom-left origin). 1P: the middle of the screen.
        // 2P: the road of that seat's own half, below its sky (the top third of a view holds the HUD).
        public static Vector2 ContinueCentre(int seat, bool twoPlayers)
        {
            var view = ViewRect(seat, twoPlayers);
            float y = twoPlayers ? view.yMin * CabinetHeight + view.height * CabinetHeight / 3 : CabinetHeight / 2f;
            return new Vector2(CabinetWidth / 2f, y);
        }

        // A point on the art texture (pixels, bottom-left origin) in cabinet units (512x448, centre origin), the space
        // the name tags and score pop-ups are placed in.
        public static Vector2 ArtToCabinet(Vector2 artPixel) =>
            artPixel * ArtScale - new Vector2(CabinetWidth, CabinetHeight) * .5f;

        // A tag point as one chase camera sees it (art pixels, z = depth in front of the camera) shows only when it is
        // in front of the camera and inside that camera's own view (in 2P, its own half of the art texture).
        public static bool TagOnView(Vector3 screenPoint, Rect viewPixels, out Vector2 cabinet)
        {
            var art = new Vector2(screenPoint.x, screenPoint.y);
            cabinet = ArtToCabinet(art);
            return screenPoint.z > 0 && viewPixels.Contains(art);
        }

        // World units covered by one art pixel of a perspective view (vertical field of view in degrees, view height in
        // art pixels) at the given depth in front of its camera.
        public static float WorldPerArtPixel(float depth, float fieldOfView, float viewPixels) =>
            2 * depth * Mathf.Tan(fieldOfView * .5f * Mathf.Deg2Rad) / viewPixels;

        // Orthographic size that shows the whole area (world units) in a view of the given width / height.
        public static float FitOrthographicSize(Vector2 area, float aspect) => Mathf.Max(area.y / 2, area.x / 2 / aspect);
    }
}
