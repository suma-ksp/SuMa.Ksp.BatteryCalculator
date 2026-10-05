using SuMa.Ksp.BatteryCalculator.Calculation;

namespace SuMa.Ksp.BatteryCalculator.Handlers
{
    /// <summary>
    /// Knows about electric charge that is used by the vessel as a whole rather than by any part module, such
    /// as USI Life Support's per-kerbal draw, which lives in a VesselModule.
    /// </summary>
    internal interface IVesselEnergyHandler
    {
        void Collect(EntrySink sink);
    }
}
