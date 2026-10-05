using System;
using SuMa.Ksp.BatteryCalculator.Calculation;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Stock
{
    /// <summary>
    /// The small environment sensors, which draw their RESOURCE rate while switched on.
    /// </summary>
    internal sealed class EnviroSensorHandler : IEnergyHandler
    {
        public Type ModuleType => typeof(ModuleEnviroSensor);

        public void Collect(PartModule module, EntrySink sink)
        {
            var sensor = (ModuleEnviroSensor)module;
            sink.Add(null, "Sensor", EnergyRole.Consumer, EnergyBehaviour.Switchable, Rates.HandlerInput(sensor), sensor.sensorActive, null);
        }
    }
}
