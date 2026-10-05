using System;
using SuMa.Ksp.BatteryCalculator.Calculation;

namespace SuMa.Ksp.BatteryCalculator.Handlers
{
    /// <summary>
    /// Knows how one kind of part module makes or uses electric charge.
    ///
    /// A handler is chosen by type, not by module name, so a mod's subclass of a stock module is understood
    /// without anyone having to list it: CommNet Antennas Consumptor's ModuleGeneratorAntenna is a
    /// ModuleGenerator, Near Future's relay dishes are ModuleDataTransmitters. Where several handlers fit, the
    /// one for the most derived type wins.
    /// </summary>
    internal interface IEnergyHandler
    {
        /// <summary>
        /// The module type handled, subclasses included.
        /// </summary>
        Type ModuleType { get; }

        void Collect(PartModule module, EntrySink sink);
    }
}
