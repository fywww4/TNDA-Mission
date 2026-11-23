using UnityEngine;

namespace TNDA.Prototype
{
    // Campaign stage 3's night derby terrain (ticket 25): a loader parked against the left wall and three wrecked hulks,
    // all solid. Laid out for the default Mode 7 arena so they miss the six starting spots, the four authored crate
    // spawns, and the straight line from the boss's start to the player's (its first charge). The scene keeps its own
    // copy in DemolitionGame.derbyObstacles (written by TNDA/Mode 7/Apply boss arena); tune it there.
    public static class DerbyLayout
    {
        public static ArenaObstacle[] Obstacles(Vector2 arenaMin, Vector2 arenaMax)
        {
            Vector2 centre = (arenaMin + arenaMax) / 2;
            ArenaObstacle At(ObstacleKind kind, Vector2 point, Vector2 size, float angle, float height) =>
                new ArenaObstacle { kind = kind, point = point, size = size, angle = angle, height = height };
            return new[] {
                // Side on to the arena, its length along the wall.
                At(ObstacleKind.Loader, new Vector2(arenaMin.x + 1.1f, centre.y), new Vector2(1.9f, 1.6f), 90, 1),
                At(ObstacleKind.Hulk, centre + new Vector2(3.3f, 4.2f), new Vector2(1.5f, 1.1f), 15, .6f),
                At(ObstacleKind.Hulk, centre + new Vector2(-3.5f, 2), new Vector2(1.5f, 1.1f), -20, .6f),
                At(ObstacleKind.Hulk, centre + new Vector2(3.2f, -3.3f), new Vector2(1.5f, 1.1f), 160, .6f)
            };
        }
    }
}
