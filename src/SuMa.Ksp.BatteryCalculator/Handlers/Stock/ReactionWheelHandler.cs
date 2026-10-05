using System;
using SuMa.Ksp.BatteryCalculator.Calculation;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Stock
{
    /// <summary>
    /// Reaction wheels, including mod wheels built on them such as CactEye's gyros and the procedural reaction
    /// wheel. Stock only draws while torque is applied: rate × (|pitch| + |yaw| + |roll|), so nothing while the
    /// vessel coasts and up to three times the rate while it turns on every axis.
    /// </summary>
    internal sealed class ReactionWheelHandler : IEnergyHandler
    {
        public Type ModuleType => typeof(ModuleReactionWheel);

        public void Collect(PartModule module, EntrySink sink)
        {
            sink.Add(null, "Reaction wheel", EnergyRole.Consumer, EnergyBehaviour.OnDemand, Rates.HandlerInput(module), null, "Only while turning the vessel, per axis; up to three times this on all three.");
        }
    }
}
