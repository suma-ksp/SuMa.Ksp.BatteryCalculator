namespace SuMa.Ksp.BatteryCalculator.Calculation
{
    /// <summary>
    /// How a producer or consumer behaves when nobody touches the vessel, which is what decides whether it is
    /// counted by default. The goal is a battery that gets through the dark with no manual intervention.
    /// </summary>
    internal enum EnergyBehaviour
    {
        /// <summary>
        /// Draws or makes power all the time: a probe core, an RTG, cryogenic tank cooling, an antenna keeping
        /// its connection. Counted by default, unless the module itself says it is switched off.
        /// </summary>
        Constant,

        /// <summary>
        /// Is started once and meant to keep running from then on: a converter, a drill, a lab, a habitat. A
        /// vessel carries one to use it, so it is counted in the editor; in flight counted exactly while it runs.
        /// </summary>
        Continuous,

        /// <summary>
        /// Runs once the player switches it on, for as long as they want it: a scanner, a light, a magnet. Not
        /// counted in the editor, where nothing is running yet; in flight counted exactly when it is switched on.
        /// </summary>
        Switchable,

        /// <summary>
        /// Only draws power while the player is using it: a transmitter sending, a reaction wheel applying
        /// torque, wheels, engines, robotics. Never counted by default.
        /// </summary>
        OnDemand,

        /// <summary>
        /// Makes power by burning something the vessel carries, such as a fuel cell or a reactor. Counted by
        /// default only if the fuel aboard lasts longer than the configured threshold.
        /// </summary>
        Fueled,

        /// <summary>
        /// Makes power from sunlight, so only on the lit side. Counted by default.
        /// </summary>
        Solar
    }
}
