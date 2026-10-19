using System;
using UnityEngine;

namespace TNDA.Prototype
{
    // Rocket pool slot, also used for oil slicks (rockets and slicks share the two slots). The match moves it;
    // DemolitionGame mirrors it onto the rocket or slick view each frame.
    [Serializable]
    public class ArcadeProjectile
    {
        public SpriteRenderer view, slickView;
        public Vector2 position, velocity;
        public float age;
        public int owner = -1;
        public bool active, slick;

        public void Spawn(Vector2 point, Vector2 movement, int source = -1)
        {
            position = point; velocity = movement; owner = source; age = 0; active = true; slick = false;
        }

        // An oil slick lying still on the floor.
        public void Drop(Vector2 point, int source)
        {
            position = point; velocity = Vector2.zero; owner = source; age = 0; active = true; slick = true;
        }
        public void Hide() { active = false; }
    }
}
