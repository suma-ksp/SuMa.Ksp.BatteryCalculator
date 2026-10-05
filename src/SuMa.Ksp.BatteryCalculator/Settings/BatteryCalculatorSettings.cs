using System.Collections.Generic;
using System.Globalization;
using SuMa.Ksp.BatteryCalculator.Support;

namespace SuMa.Ksp.BatteryCalculator.Settings
{
    /// <summary>
    /// The BATTERY_CALCULATOR node and every BATTERY_CALCULATOR_MODULE node, read once the game database is
    /// loaded. Several BATTERY_CALCULATOR nodes are applied in load order, so a later patch can change one
    /// value without repeating the rest.
    /// </summary>
    internal sealed class BatteryCalculatorSettings
    {
        private const string NodeName = "BATTERY_CALCULATOR";

        private static BatteryCalculatorSettings _current;

        private BatteryCalculatorSettings()
        {
        }

        /// <summary>
        /// The settings, read on first use.
        /// </summary>
        public static BatteryCalculatorSettings Current => _current ?? (_current = Load());

        /// <summary>
        /// Kilometres above the surface a craft is planned for until the player picks something else.
        /// </summary>
        public double DefaultAltitudeKilometres { get; private set; } = 120.0;

        /// <summary>
        /// CelestialBody.bodyName a craft is planned for until the player picks something else; empty for the
        /// home body.
        /// </summary>
        public string DefaultBodyName { get; private set; } = string.Empty;

        /// <summary>
        /// Seconds between two recalculations while nothing about the craft changes.
        /// </summary>
        public double EditorRefreshInterval { get; private set; } = 1.0;

        public double FlightRefreshInterval { get; private set; } = 1.0;

        /// <summary>
        /// How many years a fueled producer's fuel has to last to be counted by default, in the game's calendar.
        /// </summary>
        public double FueledProducerMinimumYears { get; private set; } = 1.0;

        public bool IncludeParentEclipses { get; private set; } = true;

        public List<ModuleDefinition> ModuleDefinitions { get; } = new List<ModuleDefinition>();

        private static BatteryCalculatorSettings Load()
        {
            var settings = new BatteryCalculatorSettings();
            var database = GameDatabase.Instance;
            if (database == null)
            {
                return settings;
            }

            foreach (var node in database.GetConfigNodes(NodeName))
            {
                settings.DefaultBodyName = node.GetValue("DefaultBody") ?? settings.DefaultBodyName;
                settings.DefaultAltitudeKilometres = ReadDouble(node, "DefaultAltitude", settings.DefaultAltitudeKilometres);
                settings.IncludeParentEclipses = ReadBoolean(node, "IncludeParentEclipses", settings.IncludeParentEclipses);
                settings.FueledProducerMinimumYears = ReadDouble(node, "FueledProducerMinimumYears", settings.FueledProducerMinimumYears);
                settings.EditorRefreshInterval = ReadDouble(node, "EditorRefreshInterval", settings.EditorRefreshInterval);
                settings.FlightRefreshInterval = ReadDouble(node, "FlightRefreshInterval", settings.FlightRefreshInterval);
            }

            foreach (var node in database.GetConfigNodes(ModuleDefinition.NodeName))
            {
                var definition = ModuleDefinition.Load(node);
                if (definition != null)
                {
                    settings.ModuleDefinitions.Add(definition);
                }
            }

            return settings;
        }

        private static bool ReadBoolean(ConfigNode node, string name, bool fallback)
        {
            var text = node.GetValue(name);
            if (text == null)
            {
                return fallback;
            }

            if (bool.TryParse(text.Trim(), out var value))
            {
                return value;
            }

            Log.Error(NodeName + "." + name + " = \"" + text + "\" is not True or False; keeping " + fallback + ".");
            return fallback;
        }

        private static double ReadDouble(ConfigNode node, string name, double fallback)
        {
            var text = node.GetValue(name);
            if (text == null)
            {
                return fallback;
            }

            if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= 0.0)
            {
                return value;
            }

            Log.Error(NodeName + "." + name + " = \"" + text + "\" is not a number of zero or more; keeping " + fallback.ToString(CultureInfo.InvariantCulture) + ".");
            return fallback;
        }
    }
}
