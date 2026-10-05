using System;

namespace SuMa.Ksp.BatteryCalculator.Calculation
{
    /// <summary>
    /// How long a vessel spends in shadow, for a circular orbit or on the surface.
    ///
    /// Shadows are modelled as cylinders, which is what KSP does too: it decides sunlight with a ray cast to
    /// the Sun, so there is no penumbra and a body's shadow is exactly as wide as the body at any distance.
    /// </summary>
    internal static class Darkness
    {
        /// <summary>
        /// Seconds in the shadow of a body on the worst pass of an elliptical orbit, the one with the apoapsis
        /// in the middle of the shadow: 2ab/h × (asin(R/b) + eR/b). For a circular orbit that is
        /// period × asin(R / r) / π. Zero for an orbit that does not clear the surface or a body without gravity.
        /// </summary>
        public static double EllipticShadowTime(double bodyRadius, double gravParameter, double apoapsisRadius, double periapsisRadius)
        {
            var apoapsis = Math.Max(apoapsisRadius, periapsisRadius);
            var periapsis = Math.Min(apoapsisRadius, periapsisRadius);
            if (bodyRadius <= 0.0 || gravParameter <= 0.0 || periapsis <= bodyRadius)
            {
                return 0.0;
            }

            var semiMajorAxis = (apoapsis + periapsis) / 2.0;
            var semiMinorAxis = Math.Sqrt(apoapsis * periapsis);
            var eccentricity = (apoapsis - periapsis) / (apoapsis + periapsis);
            var semiLatusRectum = 2.0 * apoapsis * periapsis / (apoapsis + periapsis);
            var specificAngularMomentum = Math.Sqrt(semiLatusRectum * gravParameter);
            var ratio = Math.Min(1.0, bodyRadius / semiMinorAxis);

            return 2.0 * semiMajorAxis * semiMinorAxis / specificAngularMomentum * (Math.Asin(ratio) + eccentricity * ratio);
        }

        /// <summary>
        /// The longest unbroken darkness when an eclipse of <paramref name="eclipseTime" /> seconds lands on an
        /// orbit that is dark for <paramref name="darkTime" /> of every <paramref name="period" /> seconds.
        ///
        /// A pass through the orbit's own shadow can join each end of the eclipse. They can both join only if
        /// the two passes are a whole number of orbits apart, n periods, and the result is then n periods plus
        /// one pass; otherwise one pass joins, eclipse plus one pass. The longest possible is the larger of the
        /// two, with the most periods that still fit: n = floor((eclipse + pass) / period).
        /// </summary>
        public static double LongestDarkness(double eclipseTime, double darkTime, double period)
        {
            var withOnePass = eclipseTime + darkTime;
            if (period <= 0.0 || darkTime <= 0.0)
            {
                return withOnePass;
            }

            var periods = Math.Floor(withOnePass / period);
            return Math.Max(withOnePass, periods * period + darkTime);
        }

        public static double OrbitPeriod(double gravParameter, double orbitRadius)
        {
            if (gravParameter <= 0.0 || orbitRadius <= 0.0)
            {
                return 0.0;
            }

            return 2.0 * Math.PI * Math.Sqrt(orbitRadius * orbitRadius * orbitRadius / gravParameter);
        }

        /// <summary>
        /// Seconds in the shadow of a body with this radius and gravitational parameter, per revolution of a
        /// circular orbit of this radius.
        /// </summary>
        public static double OrbitShadowTime(double bodyRadius, double gravParameter, double orbitRadius)
        {
            return EllipticShadowTime(bodyRadius, gravParameter, orbitRadius, orbitRadius);
        }

        /// <summary>
        /// The length of a solar day on a body that turns once per <paramref name="rotationPeriod" /> seconds
        /// and goes round the Sun once per <paramref name="solarOrbitPeriod" /> seconds, both prograde. For a
        /// moon the solar orbit is its planet's, so a tidally locked moon still gets a day and a night.
        /// </summary>
        public static double SolarDay(double rotationPeriod, double solarOrbitPeriod)
        {
            if (rotationPeriod <= 0.0)
            {
                return 0.0;
            }

            if (solarOrbitPeriod <= 0.0)
            {
                return rotationPeriod;
            }

            var relativeRate = 1.0 / rotationPeriod - 1.0 / solarOrbitPeriod;
            return Math.Abs(relativeRate) < 1e-12 ? double.PositiveInfinity : 1.0 / Math.Abs(relativeRate);
        }
    }
}
