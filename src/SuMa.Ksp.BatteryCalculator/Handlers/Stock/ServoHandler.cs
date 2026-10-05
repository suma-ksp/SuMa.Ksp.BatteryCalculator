using System;
using Expansions.Serenity;
using SuMa.Ksp.BatteryCalculator.Calculation;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Stock
{
    /// <summary>
    /// Breaking Ground's hinges, pistons, rotors and rotation servos: the RESOURCE rate times
    /// baseResourceConsumptionRate at full motor output, only while moving. The classes ship with the game
    /// whether or not the expansion is installed; without it there are simply no such parts.
    /// </summary>
    internal sealed class ServoHandler : IEnergyHandler
    {
        public Type ModuleType => typeof(BaseServo);

        public void Collect(PartModule module, EntrySink sink)
        {
            var servo = (BaseServo)module;
            sink.Add(null, "Robotics", EnergyRole.Consumer, EnergyBehaviour.OnDemand, Rates.HandlerInput(servo) * servo.baseResourceConsumptionRate, null, "Only while moving, at full motor output.");
        }
    }
}
