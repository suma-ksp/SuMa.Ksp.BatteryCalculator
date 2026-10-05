using System;
using SuMa.Ksp.BatteryCalculator.Calculation;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Stock
{
    /// <summary>
    /// Probe cores and command pods, which draw their RESOURCE rate all the time.
    ///
    /// A core the player has put into hibernation draws rate × hibernationMultiplier instead. Hibernation
    /// during time warp is not assumed, because the dark side has to be survivable at normal speed too.
    /// </summary>
    internal sealed class CommandHandler : IEnergyHandler
    {
        public Type ModuleType => typeof(ModuleCommand);

        public void Collect(PartModule module, EntrySink sink)
        {
            var command = (ModuleCommand)module;
            var rate = Rates.HandlerInput(command);
            string detail = null;
            if (command.hibernation)
            {
                rate *= command.hibernationMultiplier;
                detail = "Hibernating.";
            }

            sink.Add(null, command.minimumCrew > 0 ? "Command pod" : "Probe core", EnergyRole.Consumer, EnergyBehaviour.Constant, rate, null, detail);
        }
    }
}
