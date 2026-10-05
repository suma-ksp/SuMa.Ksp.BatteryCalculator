using System.Collections.Generic;
using KSP.UI.Screens;
using SuMa.Ksp.BatteryCalculator.Persistence;
using SuMa.Ksp.BatteryCalculator.Settings;

namespace SuMa.Ksp.BatteryCalculator.Interface
{
    /// <summary>
    /// The calculator in the VAB and SPH, planning the craft on the bench.
    ///
    /// The crew count comes from ShipConstruction.ShipManifest, which the editor keeps up to date as kerbals are
    /// assigned. It deliberately does not ask the crew dialog for its manifest: stock builds that copy seat by
    /// seat, firing an editor event for every kerbal, which is what makes a crowded crew tab slow.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    internal sealed class EditorBatteryCalculator : BatteryCalculatorController
    {
        protected override bool IsFlight => false;

        protected override double RefreshInterval => BatteryCalculatorSettings.Current.EditorRefreshInterval;

        protected override ApplicationLauncher.AppScenes Scenes => ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH;

        protected override int CrewCount()
        {
            return ShipConstruction.ShipManifest != null ? ShipConstruction.ShipManifest.CrewCount : 0;
        }

        protected override IList<Part> CurrentParts()
        {
            return EditorLogic.fetch != null && EditorLogic.fetch.ship != null ? EditorLogic.fetch.ship.parts : null;
        }

        protected override PlanningTarget DefaultTarget()
        {
            return ConfiguredTarget();
        }

        protected override void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(OnEditorShipModified);
            GameEvents.onEditorShipCrewModified.Remove(OnEditorShipCrewModified);
            base.OnDestroy();
        }

        protected override void Start()
        {
            base.Start();
            GameEvents.onEditorShipModified.Add(OnEditorShipModified);
            GameEvents.onEditorShipCrewModified.Add(OnEditorShipCrewModified);
        }

        protected override uint VesselId()
        {
            return EditorLogic.fetch != null && EditorLogic.fetch.ship != null ? EditorLogic.fetch.ship.persistentId : 0;
        }

        private void OnEditorShipCrewModified(VesselCrewManifest manifest)
        {
            MarkDirty();
        }

        private void OnEditorShipModified(ShipConstruct ship)
        {
            MarkDirty();
        }
    }
}
