namespace TNDA.Prototype
{
    public static class VehicleHarmPolicy
    {
        // In-flight rockets can still hit after their owner is wrecked, so only the victim must be alive.
        // All AI attack types share the opening grace period; human attacks are always eligible.
        public static bool CanHarm(ArcadeVehicle attacker, ArcadeVehicle victim, float elapsed, float graceSeconds)
        {
            return victim != null && victim != attacker && victim.alive && !victim.eliminated
                && (!attacker.computer || victim.computer || elapsed >= graceSeconds);
        }
    }
}
