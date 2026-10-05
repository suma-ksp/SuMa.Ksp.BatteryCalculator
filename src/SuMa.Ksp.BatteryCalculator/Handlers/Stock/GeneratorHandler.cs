using System;
using System.Collections.Generic;
using SuMa.Ksp.BatteryCalculator.Calculation;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Stock
{
    /// <summary>
    /// ModuleGenerator in both directions.
    ///
    /// A generator that makes electric charge from nothing - an RTG - is a constant producer. One that burns
    /// something to do it is fueled, and only counted when that fuel lasts. A generator whose only input is
    /// electric charge is a consumer, which is how CommNet Antennas Consumptor charges an antenna for keeping
    /// its connection: its ModuleGeneratorAntenna runs whenever the antenna is deployed or switched on, so it
    /// is counted, and in flight it follows the module's own on/off state.
    /// </summary>
    internal sealed class GeneratorHandler : IEnergyHandler
    {
        public Type ModuleType => typeof(ModuleGenerator);

        public void Collect(PartModule module, EntrySink sink)
        {
            var generator = (ModuleGenerator)module;
            bool? isOn = sink.Context.IsFlight ? generator.isAlwaysActive || generator.generatorIsActive : (bool?)null;

            var output = Rates.HandlerOutput(generator);
            if (output > 0.0)
            {
                var fuel = FuelInputs(generator);
                if (fuel.Count > 0)
                {
                    sink.Add("out", null, EnergyRole.Producer, EnergyBehaviour.Fueled, output, isOn, sink.Context.FuelDuration(generator.part, fuel), null);
                }
                else
                {
                    sink.Add("out", null, EnergyRole.Producer, EnergyBehaviour.Constant, output, isOn, null);
                }
            }

            var input = Rates.HandlerInput(generator);
            if (input > 0.0)
            {
                sink.Add("in", null, EnergyRole.Consumer, EnergyBehaviour.Constant, input, isOn, null);
            }
        }

        private static List<ResourceRatio> FuelInputs(ModuleGenerator generator)
        {
            var fuel = new List<ResourceRatio>();
            foreach (var input in generator.resHandler.inputResources)
            {
                if (input.id != PartResourceLibrary.ElectricityHashcode && input.rate > 0.0)
                {
                    fuel.Add(new ResourceRatio(input.name, input.rate, false) { FlowMode = input.flowMode });
                }
            }

            return fuel;
        }
    }
}
