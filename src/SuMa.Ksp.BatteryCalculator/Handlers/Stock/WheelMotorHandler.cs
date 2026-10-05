using System;
using ModuleWheels;
using SuMa.Ksp.BatteryCalculator.Calculation;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Stock
{
    /// <summary>
    /// Wheel motors: their RESOURCE rate at full drive, only while driving.
    /// </summary>
    internal sealed class WheelMotorHandler : IEnergyHandler
    {
        public Type ModuleType => typeof(ModuleWheelMotor);

        public void Collect(PartModule module, EntrySink sink)
        {
            sink.Add(null, "Wheel motor", EnergyRole.Consumer, EnergyBehaviour.OnDemand, Rates.HandlerInput(module), null, "Only while driving, at full drive.");
        }
    }
}
