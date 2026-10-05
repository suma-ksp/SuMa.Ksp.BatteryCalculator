using System;
using SuMa.Ksp.BatteryCalculator.Calculation;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Stock
{
    /// <summary>
    /// Antennas while they send science. Stock charges packetResourceCost per packet and sends one packet per
    /// packetInterval, so the drain is their quotient - the figure the part's own info shows. (DataResourceCost
    /// is electric charge per Mit, which is the wrong one to divide by the interval.)
    ///
    /// The drain for staying connected, where CommNet Antennas Consumptor adds one, is a separate generator
    /// module and is handled with the generators.
    /// </summary>
    internal sealed class DataTransmitterHandler : IEnergyHandler
    {
        public Type ModuleType => typeof(ModuleDataTransmitter);

        public void Collect(PartModule module, EntrySink sink)
        {
            var transmitter = (ModuleDataTransmitter)module;
            if (transmitter.packetInterval <= 0f || Rates.HandlerInput(transmitter) <= 0.0)
            {
                return;
            }

            sink.Add(null, "Transmitter", EnergyRole.Consumer, EnergyBehaviour.OnDemand, transmitter.packetResourceCost / transmitter.packetInterval, null, "Only while transmitting science.");
        }
    }
}
