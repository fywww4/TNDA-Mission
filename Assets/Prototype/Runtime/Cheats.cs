namespace TNDA.Prototype
{
    // The cheat menu's amounts and its one lasting switch. Opened with ~ on any screen (ArcadeGame).
    public static class Cheats
    {
        public const int Gold = 1000, Score = 1000;
        // Players take no damage from rams, rockets, cherries or spikes; the timed eliminations still apply.
        public static bool Invincible;
    }
}
