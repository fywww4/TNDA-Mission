using System;
using UnityEngine;

namespace TNDA.Prototype
{
    // Rocket pool slot. The match moves it; DemolitionGame mirrors it onto the view each frame.
    [Serializable]
    public class ArcadeProjectile
    {
        public SpriteRenderer view;
        public Vector2 position, velocity;
        public float age;
        public int owner = -1;
        public bool active;

        public void Spawn(Vector2 point, Vector2 movement, int source = -1)
        {
            position = point; velocity = movement; owner = source; age = 0; active = true;
        }
        public void Hide() { active = false; }
    }
}
