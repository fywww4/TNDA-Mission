using System;
using UnityEngine;

namespace TNDA.Prototype
{
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
            view.gameObject.SetActive(true);
            Sync();
        }
        public void Hide() { active = false; view.gameObject.SetActive(false); }
        public void Sync() { view.transform.position = ArcadeGame.PixelPosition(position); }
    }
}
