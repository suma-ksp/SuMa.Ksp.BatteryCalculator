using System;
using SuMa.Ksp.BatteryCalculator.Calculation;

namespace SuMa.Ksp.BatteryCalculator.Handlers
{
    /// <summary>
    /// For a module no handler knows: whatever electric charge its resource handler is configured to draw or
    /// make, listed but not counted by default. Most mod modules declare their power as a RESOURCE node, so
    /// this puts them in front of the player without the calculator having to know what they are - and
    /// without guessing that an unknown module runs all the time.
    /// </summary>
    internal sealed class ResourceHandlerFallback : IEnergyHandler
    {
        public Type ModuleType => typeof(PartModule);

        public void Collect(PartModule module, EntrySink sink)
        {
            const string Detail = "Not recognised; listed from its configured rate, not counted unless ticked.";
            sink.Add("in", null, EnergyRole.Consumer, EnergyBehaviour.OnDemand, Rates.HandlerInput(module), null, Detail);
            sink.Add("out", null, EnergyRole.Producer, EnergyBehaviour.OnDemand, Rates.HandlerOutput(module), null, Detail);
        }
    }
}
