using System;
using SuMa.Ksp.BatteryCalculator.Calculation;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Stock
{
    /// <summary>
    /// RCS thrusters that burn electric charge: every nozzle firing at full power at once, which is the most
    /// they can draw and only ever lasts a moment.
    /// </summary>
    internal sealed class RcsHandler : IEnergyHandler
    {
        public Type ModuleType => typeof(ModuleRCS);

        public void Collect(PartModule module, EntrySink sink)
        {
            var thruster = (ModuleRCS)module;
            var nozzles = thruster.thrusterTransforms != null ? thruster.thrusterTransforms.Count : 1;
            var thrust = thruster.thrusterPower * Math.Max(1, nozzles) * thruster.thrustPercentage / 100.0;
            var rate = PropellantFlow.ElectricCharge(thruster.propellants, thrust, thruster.atmosphereCurve.Evaluate(0f));
            sink.Add(null, "RCS", EnergyRole.Consumer, EnergyBehaviour.OnDemand, rate, null, "Only while thrusting, with every nozzle firing.");
        }
    }
}
