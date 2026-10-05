namespace SuMa.Ksp.BatteryCalculator.Persistence
{
    /// <summary>
    /// What a vessel is being planned for: a body, and either a circular orbit at an altitude or a spot on
    /// the surface. Immutable, so one instance can be handed to every part of a vessel.
    /// </summary>
    public sealed class PlanningTarget
    {
        public PlanningTarget(string bodyName, double altitude, bool isLanded)
        {
            BodyName = bodyName;
            Altitude = altitude;
            IsLanded = isLanded;
        }

        /// <summary>
        /// Metres above the surface. Ignored while <see cref="IsLanded" />.
        /// </summary>
        public double Altitude { get; }

        /// <summary>
        /// CelestialBody.bodyName, which does not change with the game's language.
        /// </summary>
        public string BodyName { get; }

        public bool IsLanded { get; }
    }
}
