using UnityEngine;

namespace TNDA.Prototype
{
    // Loader and Hulk (ticket 25): the night derby's parked loader and wrecked cars, solid and unbreakable like a wall.
    public enum ObstacleKind { Fence, Pipe, Wall, Ramp, Loader, Hulk }

    // A fixed footprint on the arena floor. Angle turns its width axis; a ramp rises along its local +Y.
    [System.Serializable]
    public sealed class ArenaObstacle
    {
        public ObstacleKind kind;
        public Vector2 point, size = new Vector2(2, .4f);
        public float angle, height = .8f;
        [System.NonSerialized] public bool broken;
        [System.NonSerialized] public float respawn;

        public Vector2 ToLocal(Vector2 world) => Quaternion.Euler(0, 0, -angle) * (world - point);
        public Vector2 ToWorld(Vector2 local) => point + (Vector2)(Quaternion.Euler(0, 0, angle) * local);

        public bool Contains(Vector2 world, float margin = 0)
        {
            Vector2 p = ToLocal(world), half = size / 2 + Vector2.one * margin;
            return Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.y) <= half.y;
        }

        // Sweeps a car-sized footprint, so fast motion cannot tunnel through a thin fence. The returned point
        // is just outside the first face it crosses (or the nearest exit when landing inside a solid).
        public bool Sweep(Vector2 from, Vector2 to, float radius, out float time, out Vector2 contact, out Vector2 normal)
        {
            Vector2 a = ToLocal(from), b = ToLocal(to), delta = b - a, half = size / 2 + Vector2.one * radius;
            float enter = 0, exit = 1;
            Vector2 face = Vector2.zero;
            time = 0; contact = to; normal = Vector2.zero;
            for (int axis = 0; axis < 2; axis++)
            {
                if (Mathf.Abs(delta[axis]) < .00001f)
                {
                    if (Mathf.Abs(a[axis]) > half[axis]) return false;
                    continue;
                }
                float near = (-half[axis] - a[axis]) / delta[axis], far = (half[axis] - a[axis]) / delta[axis];
                float sign = -Mathf.Sign(delta[axis]);
                if (near > far) { float swap = near; near = far; far = swap; }
                if (near >= enter) { enter = near; face = Vector2.zero; face[axis] = sign; }
                exit = Mathf.Min(exit, far);
                if (enter > exit) return false;
            }
            if (exit < 0 || enter > 1) return false;
            Vector2 local = a + delta * enter;
            if (face == Vector2.zero)
            {
                int axis = half.x - Mathf.Abs(a.x) < half.y - Mathf.Abs(a.y) ? 0 : 1;
                face[axis] = a[axis] < 0 ? -1 : 1;
                local[axis] = face[axis] * half[axis];
                // Already outside and moving away from a contact must not count as another hit.
                if (Mathf.Abs(a[axis]) >= half[axis] - .001f && Vector2.Dot(delta, face) >= 0) return false;
            }
            time = enter;
            contact = ToWorld(local + face * .002f);
            normal = Quaternion.Euler(0, 0, angle) * face;
            return true;
        }
    }
}
