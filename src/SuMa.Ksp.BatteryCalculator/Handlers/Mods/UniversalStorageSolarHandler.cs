using System;
using System.Globalization;
using SuMa.Ksp.BatteryCalculator.Calculation;
using SuMa.Ksp.BatteryCalculator.Support;
using UnityEngine;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Mods
{
    /// <summary>
    /// Universal Storage 2's switchable solar wedges. The rate belongs to the selected variant - chargeRate is
    /// a ';' separated list indexed by CurrentSelection - and like stock it is scaled by the flux, while the
    /// exposure is the average over the wedge's sun catcher transforms.
    /// </summary>
    internal sealed class UniversalStorageSolarHandler : IEnergyHandler
    {
        private const string TypeName = "UniversalStorage2.USSolarSwitch";

        private readonly MemberReader _chargeRates;
        private readonly MemberReader _currentChargeRate;
        private readonly MemberReader _currentSelection;
        private readonly MemberReader _isActive;
        private readonly MemberReader _isDeployed;
        private readonly MemberReader _isFixed;
        private readonly MemberReader _sunCatchers;

        private UniversalStorageSolarHandler(Type moduleType)
        {
            ModuleType = moduleType;
            _chargeRates = MemberReader.For(moduleType, "chargeRate");
            _currentChargeRate = MemberReader.For(moduleType, "_currentChargeRate");
            _currentSelection = MemberReader.For(moduleType, "CurrentSelection");
            _isActive = MemberReader.For(moduleType, "IsActive");
            _isDeployed = MemberReader.For(moduleType, "IsDeployed");
            _isFixed = MemberReader.For(moduleType, "IsFixed");
            _sunCatchers = MemberReader.For(moduleType, "_sunCatchers");
        }

        public Type ModuleType { get; }

        public static UniversalStorageSolarHandler TryCreate()
        {
            var type = TypeFinder.Find(TypeName);
            return type != null ? new UniversalStorageSolarHandler(type) : null;
        }

        public void Collect(PartModule module, EntrySink sink)
        {
            if (_isActive == null || !(_isActive.ReadBoolean(module) ?? false))
            {
                // No panels fitted in the selected variant.
                return;
            }

            var rate = CurrentChargeRate(module);
            var exposure = SolarExposure.Best(_sunCatchers != null ? _sunCatchers.Read(module) as Transform[] : null);

            bool? isOn = null;
            if (sink.Context.IsFlight)
            {
                isOn = (_isDeployed != null && (_isDeployed.ReadBoolean(module) ?? false)) || (_isFixed != null && (_isFixed.ReadBoolean(module) ?? false));
            }

            sink.Add(null, "Solar panel", EnergyRole.Producer, EnergyBehaviour.Solar, rate * exposure * sink.Context.Solar.PanelMultiplier(false, null), isOn, "At the panel's best orientation.");
        }

        private double CurrentChargeRate(PartModule module)
        {
            var current = _currentChargeRate != null ? _currentChargeRate.ReadDouble(module) ?? 0.0 : 0.0;
            if (current > 0.0)
            {
                return current;
            }

            var rates = _chargeRates != null ? _chargeRates.ReadString(module) : null;
            var selection = _currentSelection != null ? (int)(_currentSelection.ReadDouble(module) ?? 0.0) : 0;
            if (string.IsNullOrEmpty(rates))
            {
                return 0.0;
            }

            var values = rates.Split(';');
            return selection >= 0 && selection < values.Length && double.TryParse(values[selection].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var rate) ? rate : 0.0;
        }
    }
}
