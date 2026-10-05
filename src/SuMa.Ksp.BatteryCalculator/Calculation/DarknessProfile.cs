using System;
using SuMa.Ksp.BatteryCalculator.Support;

namespace SuMa.Ksp.BatteryCalculator.Calculation
{
    /// <summary>
    /// The light and dark a vessel sees at the selected body: how long one cycle lasts, how much of it is in
    /// shadow, the longest stretch of darkness to size the batteries for, and how far from the Sun it all
    /// happens.
    /// </summary>
    internal sealed class DarknessProfile
    {
        private const double AtmosphereClearance = 10000.0;

        private DarknessProfile()
        {
        }

        /// <summary>
        /// The body whose shadow is modelled.
        /// </summary>
        public CelestialBody Body { get; private set; }

        /// <summary>
        /// One orbit, or one solar day on the surface, in seconds.
        /// </summary>
        public double CycleTime { get; private set; }

        /// <summary>
        /// Seconds of each cycle spent in the body's own shadow: the dark side of the orbit, or the night.
        /// </summary>
        public double DarkTime { get; private set; }

        public bool IsLanded { get; private set; }

        /// <summary>
        /// Whether the target can exist at all: an orbit above the atmosphere and inside the sphere of
        /// influence, or a body with a surface to land on. An impossible target has no verdict.
        /// </summary>
        public bool IsValid { get; private set; }

        /// <summary>
        /// The body that eclipses <see cref="Body" /> once per orbit of it, for a moon. Null for a planet.
        /// </summary>
        public CelestialBody ParentBody { get; private set; }

        /// <summary>
        /// How long <see cref="Body" /> itself stays in its parent's shadow on its longest pass - the one at the
        /// far end of its orbit - or zero.
        /// </summary>
        public double ParentEclipseTime { get; private set; }

        public double SunlitTime => Math.Max(0.0, CycleTime - DarkTime);

        /// <summary>
        /// The distance from the Sun's centre the solar panels are rated at: the far end of the orbit round
        /// the Sun of the body, or of its planet for a moon, so an eccentric planet is judged at its dimmest.
        /// </summary>
        public double SunDistance { get; private set; }

        /// <summary>
        /// Why the target is impossible, such as an orbit inside the atmosphere, or null.
        /// </summary>
        public string Warning { get; private set; }

        /// <summary>
        /// The longest continuous darkness: the vessel's own shadow, extended by the parent's eclipse of the
        /// moon when that is included. In orbit an eclipse of the moon can join up with passes through the
        /// moon's own shadow at either end; on the surface it falls on the side facing the planet at local
        /// noon, so it never lengthens a night.
        /// </summary>
        public double WorstDarkTime { get; private set; }

        public static DarknessProfile Calculate(CelestialBody body, bool isLanded, double altitude, bool includeParentEclipse)
        {
            var profile = new DarknessProfile { Body = body, IsLanded = isLanded };
            if (body == null)
            {
                profile.Warning = "No body selected.";
                return profile;
            }

            var sun = Sun();
            var solarAncestor = SolarAncestor(body, sun);
            profile.SunDistance = solarAncestor != null && solarAncestor.orbit != null ? solarAncestor.orbit.ApR : 0.0;

            if (body == sun)
            {
                // Nothing casts a shadow on a vessel orbiting the Sun.
                profile.IsLanded = false;
                profile.CycleTime = Darkness.OrbitPeriod(body.gravParameter, body.Radius + altitude);
                profile.SunDistance = body.Radius + altitude;
                profile.Warning = altitude > 0.0 ? null : "The orbit has to be above the surface.";
                profile.IsValid = profile.Warning == null;
                return profile;
            }

            if (isLanded)
            {
                CalculateLanded(profile, body, solarAncestor);
            }
            else
            {
                profile.Warning = OrbitProblem(body, altitude);
                var orbitRadius = body.Radius + altitude;
                profile.CycleTime = Darkness.OrbitPeriod(body.gravParameter, orbitRadius);
                profile.DarkTime = Darkness.OrbitShadowTime(body.Radius, body.gravParameter, orbitRadius);
            }

            var parent = body.referenceBody;
            if (parent != null && parent != sun && body.orbit != null)
            {
                profile.ParentBody = parent;
                profile.ParentEclipseTime = Darkness.EllipticShadowTime(parent.Radius, parent.gravParameter, body.orbit.ApR, body.orbit.PeR);
            }

            profile.WorstDarkTime = profile.DarkTime;
            if (includeParentEclipse && !profile.IsLanded && profile.ParentEclipseTime > 0.0)
            {
                profile.WorstDarkTime = Darkness.LongestDarkness(profile.ParentEclipseTime, profile.DarkTime, profile.CycleTime);
            }

            profile.IsValid = profile.Warning == null && profile.CycleTime > 0.0 && !double.IsInfinity(profile.CycleTime);
            return profile;
        }

        /// <summary>
        /// Why a circular orbit at this altitude cannot exist - below the surface, inside the atmosphere, or
        /// outside the sphere of influence - or null when it can.
        /// </summary>
        public static string OrbitProblem(CelestialBody body, double altitude)
        {
            if (altitude <= 0.0)
            {
                return "The orbit has to be above the surface.";
            }

            if (body.atmosphere && altitude < body.atmosphereDepth)
            {
                return "The orbit is inside the atmosphere, which ends at " + Format.Distance(body.atmosphereDepth) + ".";
            }

            if (!double.IsInfinity(body.sphereOfInfluence) && body.Radius + altitude > body.sphereOfInfluence)
            {
                return "The orbit is outside the sphere of influence, which ends at " + Format.Distance(body.sphereOfInfluence - body.Radius) + ".";
            }

            return null;
        }

        /// <summary>
        /// A circular orbit altitude that exists at this body: the preferred one if it does, otherwise just
        /// above the atmosphere, and never beyond the sphere of influence.
        /// </summary>
        public static double SuggestedOrbitAltitude(CelestialBody body, double preferredAltitude)
        {
            if (body == null || OrbitProblem(body, preferredAltitude) == null)
            {
                return preferredAltitude;
            }

            var altitude = Math.Max(preferredAltitude, body.atmosphere ? body.atmosphereDepth + AtmosphereClearance : AtmosphereClearance);
            if (!double.IsInfinity(body.sphereOfInfluence) && body.Radius + altitude > body.sphereOfInfluence)
            {
                altitude = (body.sphereOfInfluence - body.Radius) / 2.0;
            }

            return altitude;
        }

        /// <summary>
        /// The Sun of the system, as the rest of KSP takes it.
        /// </summary>
        public static CelestialBody Sun()
        {
            if (Planetarium.fetch != null && Planetarium.fetch.Sun != null)
            {
                return Planetarium.fetch.Sun;
            }

            return FlightGlobals.Bodies != null && FlightGlobals.Bodies.Count > 0 ? FlightGlobals.Bodies[0] : null;
        }

        private static void CalculateLanded(DarknessProfile profile, CelestialBody body, CelestialBody solarAncestor)
        {
            if (!body.hasSolidSurface)
            {
                profile.Warning = body.bodyDisplayName.LocalizeRemoveGender() + " has no surface to land on.";
            }

            var solarOrbitPeriod = solarAncestor != null && solarAncestor.orbit != null ? solarAncestor.orbit.period : 0.0;
            profile.CycleTime = Darkness.SolarDay(body.rotationPeriod, solarOrbitPeriod);

            // With no axial tilt in KSP, the terminator always runs through the poles, so every latitude but
            // the poles themselves gets half a solar day of night.
            profile.DarkTime = profile.CycleTime / 2.0;
        }

        /// <summary>
        /// The body, or the planet above it, that goes round the Sun directly.
        /// </summary>
        private static CelestialBody SolarAncestor(CelestialBody body, CelestialBody sun)
        {
            var current = body;
            while (current != null && current.referenceBody != null && current.referenceBody != sun && current.referenceBody != current)
            {
                current = current.referenceBody;
            }

            return current == sun ? null : current;
        }
    }
}
