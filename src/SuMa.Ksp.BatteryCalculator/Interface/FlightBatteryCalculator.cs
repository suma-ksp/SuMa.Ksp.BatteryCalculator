using System.Collections.Generic;
using KSP.UI.Screens;
using SuMa.Ksp.BatteryCalculator.Calculation;
using SuMa.Ksp.BatteryCalculator.Persistence;
using SuMa.Ksp.BatteryCalculator.Settings;

namespace SuMa.Ksp.BatteryCalculator.Interface
{
    /// <summary>
    /// The calculator in flight, for the active vessel. It starts from where the vessel actually is - its
    /// body, and its orbit or the surface it stands on - and from what is switched on right now, and is worked
    /// out again whenever the player switches vessel, docks, undocks or stages.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    internal sealed class FlightBatteryCalculator : BatteryCalculatorController
    {
        protected override bool IsFlight => true;

        protected override double RefreshInterval => BatteryCalculatorSettings.Current.FlightRefreshInterval;

        protected override ApplicationLauncher.AppScenes Scenes => ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW;

        protected override int CrewCount()
        {
            var vessel = FlightGlobals.ActiveVessel;
            return vessel != null ? vessel.GetCrewCount() : 0;
        }

        protected override IList<Part> CurrentParts()
        {
            var vessel = FlightGlobals.ActiveVessel;
            return vessel != null && vessel.loaded && !vessel.isEVA ? vessel.parts : null;
        }

        /// <summary>
        /// Where the vessel is: landed where it stands, or its current orbit as a circle with the same period.
        /// A vessel still on the launch pad is planned like a new craft in the editor, and one that is not yet
        /// in an orbit that clears the atmosphere - still climbing, or escaping - for the configured altitude
        /// above its body.
        /// </summary>
        protected override PlanningTarget DefaultTarget()
        {
            var vessel = FlightGlobals.ActiveVessel;
            if (vessel.situation == Vessel.Situations.PRELAUNCH)
            {
                return ConfiguredTarget();
            }

            var body = vessel.mainBody;
            if (vessel.LandedOrSplashed)
            {
                return new PlanningTarget(body.bodyName, 0.0, true);
            }

            var orbit = vessel.orbit;
            if (orbit != null && orbit.eccentricity < 1.0 && DarknessProfile.OrbitProblem(body, orbit.PeA) == null)
            {
                var altitude = orbit.semiMajorAxis - body.Radius;
                if (DarknessProfile.OrbitProblem(body, altitude) == null)
                {
                    return new PlanningTarget(body.bodyName, altitude, false);
                }
            }

            return new PlanningTarget(body.bodyName, DarknessProfile.SuggestedOrbitAltitude(body, BatteryCalculatorSettings.Current.DefaultAltitudeKilometres * 1000.0), false);
        }

        protected override void OnDestroy()
        {
            GameEvents.onVesselChange.Remove(OnVesselChanged);
            GameEvents.onVesselWasModified.Remove(OnVesselChanged);
            GameEvents.onVesselSOIChanged.Remove(OnSphereOfInfluenceChanged);
            base.OnDestroy();
        }

        protected override void Start()
        {
            base.Start();
            GameEvents.onVesselChange.Add(OnVesselChanged);
            GameEvents.onVesselWasModified.Add(OnVesselChanged);
            GameEvents.onVesselSOIChanged.Add(OnSphereOfInfluenceChanged);
        }

        protected override uint VesselId()
        {
            var vessel = FlightGlobals.ActiveVessel;
            return vessel != null ? vessel.persistentId : 0;
        }

        private void OnSphereOfInfluenceChanged(GameEvents.HostedFromToAction<Vessel, CelestialBody> change)
        {
            MarkDirty();
        }

        private void OnVesselChanged(Vessel vessel)
        {
            MarkDirty();
        }
    }
}
