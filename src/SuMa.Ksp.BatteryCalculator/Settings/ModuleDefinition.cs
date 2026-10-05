using System;
using System.Collections.Generic;
using System.Globalization;
using SuMa.Ksp.BatteryCalculator.Calculation;
using SuMa.Ksp.BatteryCalculator.Support;

namespace SuMa.Ksp.BatteryCalculator.Settings
{
    /// <summary>
    /// A BATTERY_CALCULATOR_MODULE node: support for a mod's module described in configuration instead of
    /// code, for modules whose power is a field to read and whose state is a field to check.
    ///
    ///   BATTERY_CALCULATOR_MODULE
    ///   {
    ///       name = WBILightController     // the module class; subclasses match too
    ///       role = Consumer               // Consumer or Producer
    ///       behaviour = Switchable        // Constant, Continuous, Switchable, OnDemand, Fueled or Solar
    ///       rate = ecRequired             // a field or property, or "resHandler" for the RESOURCE nodes
    ///       rateScale = 1                 // multiplies the rate; optional
    ///       rateFactor = magPercent       // a second field to multiply by; optional
    ///       rateFactorScale = 0.01        // multiplies rateFactor; optional
    ///       active = lightsOn             // a field or property saying the module is on; optional
    ///       activeValues = Opened         // for a non-boolean active field, the values meaning on; optional
    ///       label = Lights                // what to call it; optional, defaults to the module's name
    ///       detail = ...                  // the row's tooltip; optional
    ///   }
    /// </summary>
    internal sealed class ModuleDefinition
    {
        public const string NodeName = "BATTERY_CALCULATOR_MODULE";

        private const string ResourceHandlerRate = "resHandler";

        private ModuleDefinition()
        {
        }

        public string ActiveMember { get; private set; }

        public HashSet<string> ActiveValues { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public EnergyBehaviour Behaviour { get; private set; }

        public string Detail { get; private set; }

        public string Label { get; private set; }

        public string RateFactorMember { get; private set; }

        public double RateFactorScale { get; private set; } = 1.0;

        public string RateMember { get; private set; }

        public double RateScale { get; private set; } = 1.0;

        public EnergyRole Role { get; private set; }

        public string TypeName { get; private set; }

        public bool UsesResourceHandler => string.Equals(RateMember, ResourceHandlerRate, StringComparison.OrdinalIgnoreCase);

        public static ModuleDefinition Load(ConfigNode node)
        {
            var definition = new ModuleDefinition { TypeName = node.GetValue("name"), RateMember = node.GetValue("rate") };
            if (string.IsNullOrEmpty(definition.TypeName) || string.IsNullOrEmpty(definition.RateMember))
            {
                Log.Error(NodeName + " needs a name and a rate: " + node);
                return null;
            }

            if (!TryParseEnum(node.GetValue("role"), out EnergyRole role) || !TryParseEnum(node.GetValue("behaviour"), out EnergyBehaviour behaviour))
            {
                Log.Error(NodeName + " " + definition.TypeName + " needs a role (Consumer or Producer) and a behaviour (Constant, Switchable, OnDemand, Fueled or Solar).");
                return null;
            }

            definition.Role = role;
            definition.Behaviour = behaviour;
            definition.RateScale = ParseDouble(node.GetValue("rateScale"), 1.0);
            definition.RateFactorMember = node.GetValue("rateFactor");
            definition.RateFactorScale = ParseDouble(node.GetValue("rateFactorScale"), 1.0);
            definition.ActiveMember = node.GetValue("active");
            definition.Label = node.GetValue("label");
            definition.Detail = node.GetValue("detail");

            var activeValues = node.GetValue("activeValues");
            if (!string.IsNullOrEmpty(activeValues))
            {
                foreach (var value in activeValues.Split(','))
                {
                    if (value.Trim().Length > 0)
                    {
                        definition.ActiveValues.Add(value.Trim());
                    }
                }
            }

            return definition;
        }

        private static double ParseDouble(string text, double fallback)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
        }

        private static bool TryParseEnum<TEnum>(string text, out TEnum value) where TEnum : struct
        {
            value = default;
            return !string.IsNullOrEmpty(text) && Enum.TryParse(text.Trim(), true, out value) && Enum.IsDefined(typeof(TEnum), value);
        }
    }
}
