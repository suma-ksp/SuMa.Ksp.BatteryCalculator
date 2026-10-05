using System;
using SuMa.Ksp.BatteryCalculator.Calculation;
using SuMa.Ksp.BatteryCalculator.Settings;
using SuMa.Ksp.BatteryCalculator.Support;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Mods
{
    /// <summary>
    /// A handler assembled from a <see cref="ModuleDefinition" />: the rate is a field, the on/off state is a
    /// field, and the defaults follow from the configured behaviour. Only created when the module's type is
    /// loaded, so a definition for a mod that is not installed costs nothing.
    /// </summary>
    internal sealed class ConfiguredModuleHandler : IEnergyHandler
    {
        private readonly MemberReader _active;
        private readonly ModuleDefinition _definition;
        private readonly MemberReader _rate;
        private readonly MemberReader _rateFactor;

        private ConfiguredModuleHandler(Type moduleType, ModuleDefinition definition)
        {
            ModuleType = moduleType;
            _definition = definition;
            _rate = definition.UsesResourceHandler ? null : MemberReader.For(moduleType, definition.RateMember);
            _rateFactor = string.IsNullOrEmpty(definition.RateFactorMember) ? null : MemberReader.For(moduleType, definition.RateFactorMember);
            _active = string.IsNullOrEmpty(definition.ActiveMember) ? null : MemberReader.For(moduleType, definition.ActiveMember);
        }

        public Type ModuleType { get; }

        public static ConfiguredModuleHandler TryCreate(ModuleDefinition definition)
        {
            var type = definition != null ? TypeFinder.Find(definition.TypeName) : null;
            if (type == null)
            {
                return null;
            }

            var handler = new ConfiguredModuleHandler(type, definition);
            if (!definition.UsesResourceHandler && handler._rate == null)
            {
                Log.Warning(ModuleDefinition.NodeName + " " + definition.TypeName + ": the type has no member '" + definition.RateMember + "'; ignored.");
                return null;
            }

            if (!string.IsNullOrEmpty(definition.ActiveMember) && handler._active == null)
            {
                Log.Warning(ModuleDefinition.NodeName + " " + definition.TypeName + ": the type has no member '" + definition.ActiveMember + "'; its on/off state is not read.");
            }

            return handler;
        }

        public void Collect(PartModule module, EntrySink sink)
        {
            double rate;
            if (_definition.UsesResourceHandler)
            {
                rate = _definition.Role == EnergyRole.Consumer ? Rates.HandlerInput(module) : Rates.HandlerOutput(module);
            }
            else
            {
                rate = _rate.ReadDouble(module) ?? 0.0;
            }

            rate *= _definition.RateScale;
            if (_rateFactor != null)
            {
                rate *= (_rateFactor.ReadDouble(module) ?? 0.0) * _definition.RateFactorScale;
            }

            sink.Add(null, _definition.Label, _definition.Role, _definition.Behaviour, Math.Abs(rate), IsOn(module), _definition.Detail);
        }

        private bool? IsOn(PartModule module)
        {
            if (_active == null)
            {
                return null;
            }

            if (_definition.ActiveValues.Count == 0)
            {
                return _active.ReadBoolean(module);
            }

            var value = _active.ReadString(module);
            return value != null && _definition.ActiveValues.Contains(value);
        }
    }
}
