using System;
using SuMa.Ksp.BatteryCalculator.Calculation;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Stock
{
    /// <summary>
    /// Lights draw resourceAmount while they are on. Unticked in the editor like anything a player switches on
    /// by hand, and in flight ticked exactly when the light is on.
    /// </summary>
    internal sealed class LightHandler : IEnergyHandler
    {
        public Type ModuleType => typeof(ModuleLight);

        public void Collect(PartModule module, EntrySink sink)
        {
            var light = (ModuleLight)module;
            if (!light.useResources)
            {
                return;
            }

            sink.Add(null, "Light", EnergyRole.Consumer, EnergyBehaviour.Switchable, light.resourceAmount, light.isOn, null);
        }
    }
}
