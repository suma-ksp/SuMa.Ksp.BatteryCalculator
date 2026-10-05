using System;
using SuMa.Ksp.BatteryCalculator.Calculation;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Stock
{
    /// <summary>
    /// An engine's alternator: its rate at full thrust, and only while the engine burns, which is never the
    /// case while coasting through the dark.
    /// </summary>
    internal sealed class AlternatorHandler : IEnergyHandler
    {
        public Type ModuleType => typeof(ModuleAlternator);

        public void Collect(PartModule module, EntrySink sink)
        {
            sink.Add(null, "Alternator", EnergyRole.Producer, EnergyBehaviour.OnDemand, Rates.HandlerOutput(module), null, "Only while the engine is running, at full thrust.");
        }
    }
}
