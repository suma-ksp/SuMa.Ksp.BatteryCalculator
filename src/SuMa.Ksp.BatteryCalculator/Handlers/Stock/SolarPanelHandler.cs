using System;
using SuMa.Ksp.BatteryCalculator.Calculation;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Stock
{
    /// <summary>
    /// Stock solar panels, and every mod panel built on them.
    ///
    /// Stock's output is the module's resource rate (its chargeRate) × sun angle × temperature and age
    /// efficiency × efficiencyMult × distance; flowMult only scales the number the part window shows, not the
    /// charge that flows, so it is left out. A planner cannot know the angle, so a flat panel is assumed to face the Sun, which a
    /// tracking panel does and a fixed one only does if the vessel is turned towards it. Cylindrical and
    /// spherical panels get stock's own best case, 1/π and 1/4, because they can never do better.
    /// Temperature and age are left at 1, which is what they are at normal temperatures on a new panel.
    ///
    /// In flight a panel that is retracted or broken starts unticked, since it makes nothing until someone
    /// deploys or repairs it.
    /// </summary>
    internal sealed class SolarPanelHandler : IEnergyHandler
    {
        public Type ModuleType => typeof(ModuleDeployableSolarPanel);

        public void Collect(PartModule module, EntrySink sink)
        {
            var panel = (ModuleDeployableSolarPanel)module;
            var shape = Shape(panel.panelType);
            var ratedOutput = Rates.HandlerOutput(panel);
            var rate = (ratedOutput > 0.0 ? ratedOutput : panel.chargeRate) * shape * panel.efficiencyMult * sink.Context.Solar.PanelMultiplier(panel.useCurve, panel.powerCurve);

            bool? isOn = null;
            if (sink.Context.IsFlight)
            {
                isOn = panel.deployState == ModuleDeployablePart.DeployState.EXTENDED || (panel.deployState != ModuleDeployablePart.DeployState.BROKEN && !panel.useAnimation);
            }

            var detail = shape < 1.0 ? "The best this panel's shape allows." : "Assumes the panel faces the Sun.";
            sink.Add(null, "Solar panel", EnergyRole.Producer, EnergyBehaviour.Solar, rate, isOn, detail);
        }

        private static double Shape(ModuleDeployableSolarPanel.PanelType panelType)
        {
            switch (panelType)
            {
                case ModuleDeployableSolarPanel.PanelType.CYLINDRICAL:
                    return 1.0 / Math.PI;
                case ModuleDeployableSolarPanel.PanelType.SPHERICAL:
                    return 0.25;
                default:
                    return 1.0;
            }
        }
    }
}
