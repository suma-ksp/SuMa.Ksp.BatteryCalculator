using System;
using System.Globalization;
using System.Reflection;
using SuMa.Ksp.BatteryCalculator.Calculation;
using SuMa.Ksp.BatteryCalculator.Support;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Mods
{
    /// <summary>
    /// CryoTanks' cooling, which draws power all the time a tank holding cryogenic fuel has cooling switched
    /// on - priced by the tank's capacity, not by how full it is, and nothing for an empty tank in flight.
    ///
    /// The module keeps currentCoolingCost up to date itself, but sets it to zero in flight while cooling is
    /// switched off, which would lose the row the player needs to count it back in. The cost is then worked
    /// out the way the module does, from its own totals.
    /// </summary>
    internal sealed class CryoTankHandler : IEnergyHandler
    {
        private const BindingFlags InstanceMethods = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string TypeName = "SimpleBoiloff.ModuleCryoTank";

        private readonly MemberReader _coolingEnabled;
        private readonly MemberReader _currentCoolingCost;
        private readonly MethodInfo _isCoolableMethod;
        private readonly MethodInfo _totalAmountMethod;
        private readonly MethodInfo _totalCoolingCostMethod;
        private readonly MethodInfo _totalMaximumAmountMethod;

        private CryoTankHandler(Type moduleType)
        {
            ModuleType = moduleType;
            _coolingEnabled = MemberReader.For(moduleType, "CoolingEnabled");
            _currentCoolingCost = MemberReader.For(moduleType, "currentCoolingCost");
            _isCoolableMethod = moduleType.GetMethod("IsCoolable", InstanceMethods, null, Type.EmptyTypes, null);
            _totalAmountMethod = moduleType.GetMethod("GetTotalResouceAmount", InstanceMethods, null, Type.EmptyTypes, null);
            _totalCoolingCostMethod = moduleType.GetMethod("GetTotalCoolingCost", InstanceMethods, null, Type.EmptyTypes, null);
            _totalMaximumAmountMethod = moduleType.GetMethod("GetTotalMaxResouceAmount", InstanceMethods, null, Type.EmptyTypes, null);
        }

        public Type ModuleType { get; }

        public static CryoTankHandler TryCreate()
        {
            var type = TypeFinder.Find(TypeName);
            if (type == null)
            {
                return null;
            }

            var handler = new CryoTankHandler(type);
            if (handler._currentCoolingCost != null)
            {
                return handler;
            }

            Log.Warning(TypeName + " has no currentCoolingCost; tank cooling is not counted.");
            return null;
        }

        public void Collect(PartModule module, EntrySink sink)
        {
            var rate = _currentCoolingCost.ReadDouble(module) ?? 0.0;
            if (rate <= 0.0)
            {
                rate = CoolingCostWhileCooling(module, sink.Context.IsFlight);
            }

            bool? isOn = _coolingEnabled != null ? _coolingEnabled.ReadBoolean(module) : null;
            sink.Add(null, "Tank cooling", EnergyRole.Consumer, EnergyBehaviour.Constant, rate, isOn, null);
        }

        private static double Invoke(MethodInfo method, PartModule module)
        {
            return Convert.ToDouble(method.Invoke(module, null), CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// What the tank draws while cooling: its cooling cost per thousand units of capacity, times its
        /// capacity. Zero when it cannot be cooled, or in flight when it is empty, as the module has it.
        /// </summary>
        private double CoolingCostWhileCooling(PartModule module, bool isFlight)
        {
            if (_isCoolableMethod == null || _totalCoolingCostMethod == null || _totalMaximumAmountMethod == null)
            {
                return 0.0;
            }

            try
            {
                if (!(_isCoolableMethod.Invoke(module, null) is bool isCoolable) || !isCoolable)
                {
                    return 0.0;
                }

                if (isFlight && _totalAmountMethod != null && Invoke(_totalAmountMethod, module) <= 0.0)
                {
                    return 0.0;
                }

                return Invoke(_totalCoolingCostMethod, module) * Invoke(_totalMaximumAmountMethod, module) / 1000.0;
            }
            catch (Exception)
            {
                // A tank whose totals cannot be read keeps what the module itself reported.
                return 0.0;
            }
        }
    }
}
