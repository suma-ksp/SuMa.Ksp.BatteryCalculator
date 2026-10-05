using System;

namespace SuMa.Ksp.BatteryCalculator.Calculation
{
    /// <summary>
    /// How much sunlight a solar panel gets at the selected body, relative to what it is rated for at home.
    ///
    /// Follows stock exactly where stock has an answer. FlightIntegrator measures the distance to the Sun from
    /// the Sun's surface and works out the flux from PhysicsGlobals.SolarLuminosity, and a panel with a power
    /// curve evaluates it at the distance from the Sun's centre; both are reproduced here. That is also why a
    /// panel at Kerbin makes about 4% more than its rating.
    ///
    /// The atmosphere only matters on the surface, and only for panels rated by flux: stock does not apply it
    /// to a power curve. There the flux is reduced by stock's own CelestialBody.GetSolarAtmosphericEffects at
    /// sea-level density, averaged over the day as the Sun climbs and sets: a panel facing the Sun gets that
    /// average while it is light.
    /// </summary>
    internal sealed class SolarConditions
    {
        private const int DaySamples = 64;

        private SolarConditions()
        {
        }

        /// <summary>
        /// The share of the flux that gets through the atmosphere, averaged over the day. 1 in orbit.
        /// </summary>
        public double AtmosphereMultiplier { get; private set; } = 1.0;

        /// <summary>
        /// Distance from the Sun's centre, in metres, for panels rated by a power curve.
        /// </summary>
        public double CentreDistance { get; private set; }

        /// <summary>
        /// Flux relative to the flux at home, as stock computes it for a panel without a power curve.
        /// </summary>
        public double FluxMultiplier { get; private set; }

        /// <summary>
        /// Distance from the Sun's surface, in metres, which is what stock and Near Future Solar square.
        /// </summary>
        public double SurfaceDistance { get; private set; }

        public static SolarConditions Calculate(DarknessProfile profile)
        {
            var conditions = new SolarConditions();
            var sun = DarknessProfile.Sun();
            if (profile == null || profile.Body == null || sun == null || profile.SunDistance <= 0.0)
            {
                return conditions;
            }

            conditions.CentreDistance = profile.SunDistance;
            conditions.SurfaceDistance = Math.Max(1.0, profile.SunDistance - sun.Radius);
            conditions.FluxMultiplier = SolarLuminosity() / (4.0 * Math.PI * conditions.SurfaceDistance * conditions.SurfaceDistance) / PhysicsGlobals.SolarLuminosityAtHome;

            var body = profile.Body;
            if (profile.IsLanded && body.atmosphere && body != sun)
            {
                conditions.AtmosphereMultiplier = DayAverageAtmosphereMultiplier(body);
            }

            return conditions;
        }

        /// <summary>
        /// The multiplier for a panel that follows the stock rules: its power curve at the distance from the
        /// Sun's centre if it uses one, otherwise the flux, reduced by the atmosphere.
        /// </summary>
        public double PanelMultiplier(bool useCurve, FloatCurve powerCurve)
        {
            if (useCurve && powerCurve != null)
            {
                return powerCurve.Evaluate((float)CentreDistance);
            }

            return FluxMultiplier * AtmosphereMultiplier;
        }

        /// <summary>
        /// At sea level, with the Sun moving across the sky at an even rate from one horizon to the other:
        /// the average of stock's atmospheric multiplier over the lit half of the day.
        /// </summary>
        private static double DayAverageAtmosphereMultiplier(CelestialBody body)
        {
            var density = body.GetDensity(body.GetPressure(0.0), body.GetTemperature(0.0));
            var total = 0.0;
            for (var sample = 0; sample < DaySamples; sample++)
            {
                // Hour angles spread evenly over -90..90 degrees, sampled at the middle of each slice.
                var hourAngle = Math.PI * ((sample + 0.5) / DaySamples - 0.5);
                body.GetSolarAtmosphericEffects(Math.Cos(hourAngle), density, out _, out var multiplier);
                total += multiplier;
            }

            return total / DaySamples;
        }

        /// <summary>
        /// PhysicsGlobals.SolarLuminosity, or the value stock derives it from when it has not been set yet:
        /// the flux at home spread over a sphere as big as the home planet's orbit.
        /// </summary>
        private static double SolarLuminosity()
        {
            if (PhysicsGlobals.SolarLuminosity > 0.0)
            {
                return PhysicsGlobals.SolarLuminosity;
            }

            var home = FlightGlobals.GetHomeBody();
            var homeDistance = home != null && home.orbit != null ? home.orbit.semiMajorAxis : 0.0;
            return homeDistance * homeDistance * 4.0 * Math.PI * PhysicsGlobals.SolarLuminosityAtHome;
        }
    }
}
