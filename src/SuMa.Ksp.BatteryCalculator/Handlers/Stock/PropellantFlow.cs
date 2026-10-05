using System.Collections.Generic;
using SuMa.Ksp.BatteryCalculator.Calculation;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Stock
{
    /// <summary>
    /// The electric charge an engine or thruster burns as propellant, the way stock meters it: the mass flow
    /// thrust / (Isp × g0) is shared out over the propellants by ratio, with electric charge - which has no
    /// mass, and is marked ignoreForIsp - riding along on its ratio.
    /// </summary>
    internal static class PropellantFlow
    {
        private const double StandardGravity = 9.80665;

        /// <summary>
        /// Electric charge per second at this thrust, in kN, and vacuum Isp, in seconds; zero when the
        /// propellants include no electric charge.
        /// </summary>
        public static double ElectricCharge(List<Propellant> propellants, double thrust, double vacuumIsp)
        {
            if (propellants == null || thrust <= 0.0 || vacuumIsp <= 0.0 || PartResourceLibrary.Instance == null)
            {
                return 0.0;
            }

            var electricRatio = 0.0;
            var mixtureDensity = 0.0;
            foreach (var propellant in propellants)
            {
                if (propellant.name == Rates.ElectricCharge)
                {
                    electricRatio += propellant.ratio;
                }

                if (propellant.ignoreForIsp)
                {
                    continue;
                }

                var definition = PartResourceLibrary.Instance.GetDefinition(propellant.name);
                if (definition != null)
                {
                    mixtureDensity += propellant.ratio * definition.density;
                }
            }

            if (electricRatio <= 0.0 || mixtureDensity <= 0.0)
            {
                return 0.0;
            }

            return thrust / (vacuumIsp * StandardGravity) * electricRatio / mixtureDensity;
        }
    }
}
