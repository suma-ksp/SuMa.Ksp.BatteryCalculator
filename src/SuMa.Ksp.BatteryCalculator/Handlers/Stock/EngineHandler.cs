using System;
using SuMa.Ksp.BatteryCalculator.Calculation;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Stock
{
    /// <summary>
    /// Engines that burn electric charge, such as ion engines: their draw at full throttle within the thrust
    /// limiter, and only while burning.
    /// </summary>
    internal sealed class EngineHandler : IEnergyHandler
    {
        public Type ModuleType => typeof(ModuleEngines);

        public void Collect(PartModule module, EntrySink sink)
        {
            var engine = (ModuleEngines)module;
            var thrust = engine.maxThrust * engine.thrustPercentage / 100.0;
            var rate = PropellantFlow.ElectricCharge(engine.propellants, thrust, engine.atmosphereCurve.Evaluate(0f));
            sink.Add(null, "Engine", EnergyRole.Consumer, EnergyBehaviour.OnDemand, rate, null, "Only while the engine burns, at full throttle.");
        }
    }
}
