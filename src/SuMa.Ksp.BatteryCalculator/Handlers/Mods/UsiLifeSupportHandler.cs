using System;
using System.Globalization;
using System.Reflection;
using SuMa.Ksp.BatteryCalculator.Calculation;
using SuMa.Ksp.BatteryCalculator.Support;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Mods
{
    /// <summary>
    /// USI Life Support's own electric charge: ECAmount per kerbal per second, tourists included, drawn by a
    /// VesselModule rather than by any part, so it is a vessel-wide entry.
    ///
    /// The amount is the game's life support setting, read from LifeSupportScenario when it is running and
    /// from the LIFE_SUPPORT_SETTINGS config otherwise.
    /// </summary>
    internal sealed class UsiLifeSupportHandler : IVesselEnergyHandler
    {
        private const string ScenarioTypeName = "LifeSupport.LifeSupportScenario";
        private const string SettingsNodeName = "LIFE_SUPPORT_SETTINGS";

        private readonly MemberReader _instance;
        private readonly MemberReader _settings;

        private UsiLifeSupportHandler(Type scenarioType)
        {
            _instance = MemberReader.ForStatic(scenarioType, "Instance");
            _settings = MemberReader.For(scenarioType, "settings");
        }

        public static UsiLifeSupportHandler TryCreate()
        {
            var type = TypeFinder.Find(ScenarioTypeName);
            if (type == null)
            {
                return null;
            }

            Log.Info("Supporting USI Life Support.");
            return new UsiLifeSupportHandler(type);
        }

        public void Collect(EntrySink sink)
        {
            var crew = sink.Context.CrewCount;
            if (crew <= 0)
            {
                return;
            }

            var perKerbal = PerKerbal();
            sink.AddVesselEntry("USILifeSupport", "Life support (" + crew + " kerbals)", EnergyRole.Consumer, EnergyBehaviour.Constant, perKerbal * crew, null, "USI Life Support draws " + Format.Rate(perKerbal) + " per kerbal.");
        }

        private static double ConfiguredPerKerbal()
        {
            var perKerbal = 0.0;
            if (GameDatabase.Instance == null)
            {
                return perKerbal;
            }

            foreach (var node in GameDatabase.Instance.GetConfigNodes(SettingsNodeName))
            {
                if (double.TryParse(node.GetValue("ECAmount"), NumberStyles.Float, CultureInfo.InvariantCulture, out var amount))
                {
                    perKerbal = Math.Max(perKerbal, amount);
                }
            }

            return perKerbal;
        }

        private double PerKerbal()
        {
            try
            {
                var scenario = _instance != null ? _instance.Read(null) : null;
                var persistence = scenario != null && _settings != null ? _settings.Read(scenario) : null;
                var getSettings = persistence != null ? persistence.GetType().GetMethod("GetSettings", BindingFlags.Instance | BindingFlags.Public) : null;
                var configuration = getSettings != null ? getSettings.Invoke(persistence, null) : null;
                var amount = configuration != null ? MemberReader.For(configuration.GetType(), "ECAmount")?.ReadDouble(configuration) : null;
                if (amount.HasValue && amount.Value > 0.0)
                {
                    return amount.Value;
                }
            }
            catch (Exception)
            {
                // The scenario is not up yet, or has changed shape; the configured default still applies.
            }

            return ConfiguredPerKerbal();
        }
    }
}
