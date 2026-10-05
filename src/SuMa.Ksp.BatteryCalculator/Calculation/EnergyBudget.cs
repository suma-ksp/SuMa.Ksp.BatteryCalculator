using System;
using System.Collections.Generic;

namespace SuMa.Ksp.BatteryCalculator.Calculation
{
    /// <summary>
    /// The verdict: does the vessel get through the dark on its batteries, and does the sunlit part of each
    /// orbit or day put back what the dark took.
    ///
    /// In the dark only the non-solar producers help, so the batteries have to cover
    /// (consumption - non-solar production) for the longest darkness. In sunlight the surplus has to refill
    /// what one ordinary dark pass drained, within the sunlit time of that same orbit or day; otherwise the
    /// batteries lose a little more every revolution and are empty sooner or later.
    /// </summary>
    internal sealed class EnergyBudget
    {
        private EnergyBudget()
        {
        }

        public double Capacity { get; private set; }

        public double Consumption { get; private set; }

        /// <summary>
        /// How fast the batteries drain in the dark; zero when non-solar production covers everything.
        /// </summary>
        public double DarkDeficit { get; private set; }

        public bool HasEnoughCapacity => Capacity >= RequiredCapacity;

        /// <summary>
        /// Whether the numbers mean anything: the selected orbit or surface can exist. An orbit below the
        /// surface or inside the atmosphere has no dark side worth sizing for, and must not read as enough.
        /// </summary>
        public bool IsCalculable { get; private set; }

        public bool IsSufficient => IsCalculable && HasEnoughCapacity && Recharges;

        /// <summary>
        /// Capacity left over after the longest darkness; negative when the batteries fall short.
        /// </summary>
        public double Margin => Capacity - RequiredCapacity;

        public double OtherProduction { get; private set; }

        public DarknessProfile Profile { get; private set; }

        /// <summary>
        /// Whether sunlight refills the batteries: the surplus over the sunlit time of one cycle covers what one
        /// ordinary dark pass drains.
        /// </summary>
        public bool Recharges { get; private set; }

        /// <summary>
        /// Seconds of sunlight needed to refill the batteries after the longest darkness; infinite when there
        /// is no surplus to refill them with.
        /// </summary>
        public double RechargeTime { get; private set; }

        public double RequiredCapacity { get; private set; }

        public double SolarProduction { get; private set; }

        public double Stored { get; private set; }

        /// <summary>
        /// Net power while the Sun is up: all production minus consumption.
        /// </summary>
        public double SunlitSurplus => OtherProduction + SolarProduction - Consumption;

        public double TotalProduction => OtherProduction + SolarProduction;

        public static EnergyBudget Calculate(IEnumerable<EnergyEntry> entries, double capacity, double stored, DarknessProfile profile)
        {
            var budget = new EnergyBudget { Capacity = capacity, Stored = stored, Profile = profile };
            foreach (var entry in entries)
            {
                if (!entry.IsIncluded)
                {
                    continue;
                }

                if (entry.Role == EnergyRole.Consumer)
                {
                    budget.Consumption += entry.Rate;
                }
                else if (entry.Behaviour == EnergyBehaviour.Solar)
                {
                    budget.SolarProduction += entry.Rate;
                }
                else
                {
                    budget.OtherProduction += entry.Rate;
                }
            }

            budget.IsCalculable = profile != null && profile.IsValid;
            if (!budget.IsCalculable)
            {
                return budget;
            }

            budget.DarkDeficit = Math.Max(0.0, budget.Consumption - budget.OtherProduction);
            budget.RequiredCapacity = budget.DarkDeficit * profile.WorstDarkTime;

            var surplus = budget.SunlitSurplus;
            if (budget.DarkDeficit <= 0.0)
            {
                budget.Recharges = true;
                budget.RechargeTime = 0.0;
            }
            else if (surplus <= 0.0)
            {
                budget.Recharges = false;
                budget.RechargeTime = double.PositiveInfinity;
            }
            else
            {
                budget.Recharges = budget.DarkDeficit * profile.DarkTime <= surplus * profile.SunlitTime;
                budget.RechargeTime = budget.RequiredCapacity / surplus;
            }

            return budget;
        }
    }
}
