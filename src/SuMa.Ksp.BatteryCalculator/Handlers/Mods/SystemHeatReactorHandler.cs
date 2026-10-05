using System;
using System.Collections.Generic;
using SuMa.Ksp.BatteryCalculator.Calculation;
using SuMa.Ksp.BatteryCalculator.Support;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Mods
{
    /// <summary>
    /// SystemHeat's fission reactors, which Near Future Electrical and USI's reactors run on in this install.
    ///
    /// A reactor follows the load: it throttles itself between its minimum and ElectricalGeneration at 100%,
    /// reduced by core damage. What matters for the dark side is how much it can supply, so the entry is that
    /// ceiling. It is a fueled producer, and its fuel duration is worked out at full throttle from the
    /// reactor's own INPUT_RESOURCE ratios, which are per second at 100%. It also needs its radiators to keep
    /// running, which are listed with the consumers.
    /// </summary>
    internal sealed class SystemHeatReactorHandler : IEnergyHandler
    {
        private const string TypeName = "SystemHeat.ModuleSystemHeatFissionReactor";

        private readonly MemberReader _coreIntegrity;
        private readonly MemberReader _electricalGeneration;
        private readonly MemberReader _enabled;
        private readonly MemberReader _inputs;

        private SystemHeatReactorHandler(Type moduleType)
        {
            ModuleType = moduleType;
            _coreIntegrity = MemberReader.For(moduleType, "CoreIntegrity");
            _electricalGeneration = MemberReader.For(moduleType, "ElectricalGeneration");
            _enabled = MemberReader.For(moduleType, "Enabled");
            _inputs = MemberReader.For(moduleType, "inputs");
        }

        public Type ModuleType { get; }

        public static SystemHeatReactorHandler TryCreate()
        {
            var type = TypeFinder.Find(TypeName);
            if (type == null)
            {
                return null;
            }

            var handler = new SystemHeatReactorHandler(type);
            if (handler._electricalGeneration != null)
            {
                return handler;
            }

            Log.Warning(TypeName + " has no ElectricalGeneration; its reactors are not counted.");
            return null;
        }

        public void Collect(PartModule module, EntrySink sink)
        {
            if (!(_electricalGeneration.Read(module) is FloatCurve curve))
            {
                return;
            }

            var integrity = _coreIntegrity != null ? _coreIntegrity.ReadDouble(module) ?? 100.0 : 100.0;
            var rate = curve.Evaluate(100f) * integrity / 100.0;

            var inputs = _inputs != null ? _inputs.Read(module) as List<ResourceRatio> : null;
            var fuelDuration = inputs != null ? sink.Context.FuelDuration(module.part, inputs) : double.PositiveInfinity;

            bool? isOn = sink.Context.IsFlight && _enabled != null ? _enabled.ReadBoolean(module) : null;
            sink.Add(null, "Reactor", EnergyRole.Producer, EnergyBehaviour.Fueled, rate, isOn, fuelDuration, "Its full output; it throttles down to what is needed.");
        }
    }
}
