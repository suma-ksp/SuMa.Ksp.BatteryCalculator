using System;
using SuMa.Ksp.BatteryCalculator.Calculation;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Stock
{
    /// <summary>
    /// Active radiators, including SystemHeat's, USI's heat pump and Universal Storage's radiator: stock draws
    /// the full RESOURCE rate while the radiator is cooling, however much heat there is. They exist to keep a
    /// reactor or converter running, so they are counted like the constant load they are; in flight they
    /// follow whether they are cooling.
    /// </summary>
    internal sealed class RadiatorHandler : IEnergyHandler
    {
        public Type ModuleType => typeof(ModuleActiveRadiator);

        public void Collect(PartModule module, EntrySink sink)
        {
            var radiator = (ModuleActiveRadiator)module;
            bool? isOn = sink.Context.IsFlight ? radiator.IsCooling : (bool?)null;
            sink.Add(null, "Radiator", EnergyRole.Consumer, EnergyBehaviour.Constant, Rates.HandlerInput(radiator), isOn, null);
        }
    }
}
