using System.Collections.Generic;
using UnityEngine;

namespace TNDA.Prototype
{
    // A tiny visibility graph around the fixed solid footprints. Corners have enough clearance for a car to turn;
    // ramps are drivable and fences become a straight-through shortcut once the driver is fast enough to break them.
    internal static class ObstacleNavigation
    {
        public static Vector2 Next(Vector2 from, Vector2 goal, ArenaObstacle[] terrain, Vector2 min, Vector2 max, bool breakFences)
        {
            bool Solid(ArenaObstacle o) => !o.broken && o.kind != ObstacleKind.Ramp && !(breakFences && o.kind == ObstacleKind.Fence);
            bool Clear(Vector2 a, Vector2 b)
            {
                foreach (var o in terrain)
                {
                    // A real contact sits 0.32 from the footprint. Let a car leave the wider turning margin, and
                    // let it approach a target parked against a wall, without declaring every escape line blocked.
                    float margin = o.Contains(a, .55f) || o.Contains(b, .55f) ? .30f : .55f;
                    if (Solid(o) && o.Sweep(a, b, margin, out _, out _, out _)) return false;
                }
                return true;
            }
            if (Clear(from, goal)) return goal;
            var nodes = new List<Vector2> { from, goal };
            foreach (var o in terrain)
            {
                if (!Solid(o)) continue;
                Vector2 half = o.size / 2 + Vector2.one * .95f;
                foreach (var corner in new[] { new Vector2(-half.x, -half.y), new Vector2(half.x, -half.y), half, new Vector2(-half.x, half.y) })
                {
                    Vector2 p = o.ToWorld(corner);
                    if (p.x >= min.x + .4f && p.x <= max.x - .4f && p.y >= min.y + .4f && p.y <= max.y - .4f)
                        nodes.Add(p);
                }
            }
            var distance = new float[nodes.Count];
            var first = new int[nodes.Count];
            var visited = new bool[nodes.Count];
            for (int i = 1; i < nodes.Count; i++) distance[i] = float.PositiveInfinity;
            for (int step = 0; step < nodes.Count; step++)
            {
                int current = -1;
                for (int i = 0; i < nodes.Count; i++)
                    if (!visited[i] && (current < 0 || distance[i] < distance[current])) current = i;
                if (current < 0 || float.IsPositiveInfinity(distance[current])) break;
                if (current == 1) return nodes[first[current]];
                visited[current] = true;
                for (int i = 1; i < nodes.Count; i++)
                {
                    if (visited[i]) continue;
                    float cost = distance[current] + Vector2.Distance(nodes[current], nodes[i]);
                    if (cost >= distance[i] || !Clear(nodes[current], nodes[i])) continue;
                    distance[i] = cost;
                    first[i] = current == 0 ? i : first[current];
                }
            }
            // A target may be close against a wall, inside the turning margin. Move to the visible corner nearest
            // it, then ordinary contact avoidance and the existing unsticking behaviour handle the final approach.
            Vector2 best = from;
            float closest = float.PositiveInfinity;
            for (int i = 2; i < nodes.Count; i++)
            {
                float cost = Vector2.Distance(nodes[i], goal);
                if (cost < closest && Clear(from, nodes[i])) { best = nodes[i]; closest = cost; }
            }
            return best;
        }
    }
}
