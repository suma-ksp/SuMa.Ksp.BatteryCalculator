using System;
using System.Collections.Generic;

namespace SuMa.Ksp.BatteryCalculator.Calculation
{
    /// <summary>
    /// What a handler needs to know besides its module: where the vessel is being planned for, whether this is
    /// the editor or flight, how much of each resource is aboard, and how many kerbals.
    /// </summary>
    internal sealed class EnergyContext
    {
        private readonly Dictionary<string, double> _vesselAmounts = new Dictionary<string, double>(StringComparer.Ordinal);

        public EnergyContext(IList<Part> parts, bool isFlight, DarknessProfile profile, SolarConditions solar, int crewCount, uint vesselPersistenceId, double fueledMinimumDuration)
        {
            Parts = parts;
            IsFlight = isFlight;
            Profile = profile;
            Solar = solar;
            CrewCount = crewCount;
            VesselPersistenceId = vesselPersistenceId;
            FueledMinimumDuration = fueledMinimumDuration;

            foreach (var part in parts)
            {
                foreach (var resource in part.Resources)
                {
                    _vesselAmounts.TryGetValue(resource.resourceName, out var amount);
                    _vesselAmounts[resource.resourceName] = amount + resource.amount;
                }
            }
        }

        public int CrewCount { get; }

        /// <summary>
        /// How long a fueled producer's fuel has to last to be counted by default, in seconds.
        /// </summary>
        public double FueledMinimumDuration { get; }

        public bool IsFlight { get; }

        public IList<Part> Parts { get; }

        public DarknessProfile Profile { get; }

        public SolarConditions Solar { get; }

        /// <summary>
        /// The craft's or vessel's persistentId, which vessel-wide entries are saved under.
        /// </summary>
        public uint VesselPersistenceId { get; }

        /// <summary>
        /// How long a producer can keep running on what the vessel carries, in seconds: the first of its
        /// inputs to run out decides. Infinite for a producer with no inputs other than electric charge.
        /// </summary>
        public double FuelDuration(Part part, IEnumerable<ResourceRatio> inputs)
        {
            var duration = double.PositiveInfinity;
            foreach (var input in inputs)
            {
                if (input.ResourceName == Rates.ElectricCharge || input.Ratio <= 0.0)
                {
                    continue;
                }

                duration = Math.Min(duration, AvailableAmount(part, input.ResourceName, input.FlowMode) / input.Ratio);
            }

            return duration;
        }

        /// <summary>
        /// How much of a resource a module on this part can draw: the part's own for a resource that does not
        /// flow, the whole vessel's otherwise.
        /// </summary>
        private double AvailableAmount(Part part, string resourceName, ResourceFlowMode flowMode)
        {
            if (flowMode == ResourceFlowMode.NO_FLOW && part != null)
            {
                var resource = part.Resources.Get(resourceName);
                return resource != null ? resource.amount : 0.0;
            }

            return _vesselAmounts.TryGetValue(resourceName, out var amount) ? amount : 0.0;
        }
    }
}
