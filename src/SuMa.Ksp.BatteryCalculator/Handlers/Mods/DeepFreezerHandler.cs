using System;
using SuMa.Ksp.BatteryCalculator.Calculation;
using SuMa.Ksp.BatteryCalculator.Support;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Mods
{
    /// <summary>
    /// DeepFreeze's freezers, which keep their frozen kerbals alive on FrznChargeRequired electric charge per
    /// minute each - but only when the game's DeepFreeze settings ask for power at all. Nobody can be frozen
    /// in the editor, so this only ever shows up in flight.
    /// </summary>
    internal sealed class DeepFreezerHandler : IEnergyHandler
    {
        private const string SettingsTypeName = "DF.DeepFreeze";
        private const string TypeName = "DF.DeepFreezer";

        private readonly MemberReader _chargePerMinute;
        private readonly MemberReader _electricChargeRequired;
        private readonly MemberReader _frozen;
        private readonly MemberReader _instance;

        private DeepFreezerHandler(Type moduleType, Type settingsType)
        {
            ModuleType = moduleType;
            _chargePerMinute = MemberReader.For(moduleType, "FrznChargeRequired");
            _frozen = MemberReader.For(moduleType, "TotalFrozen");
            _instance = MemberReader.ForStatic(settingsType, "Instance");
            _electricChargeRequired = MemberReader.For(settingsType, "DFIECReqd");
        }

        public Type ModuleType { get; }

        public static DeepFreezerHandler TryCreate()
        {
            var type = TypeFinder.Find(TypeName);
            var settingsType = TypeFinder.Find(SettingsTypeName);
            if (type == null || settingsType == null)
            {
                return null;
            }

            var handler = new DeepFreezerHandler(type, settingsType);
            if (handler._chargePerMinute != null && handler._frozen != null)
            {
                return handler;
            }

            Log.Warning(TypeName + " has no FrznChargeRequired or TotalFrozen; freezers are not counted.");
            return null;
        }

        public void Collect(PartModule module, EntrySink sink)
        {
            var settings = _instance != null ? _instance.Read(null) : null;
            if (settings != null && _electricChargeRequired != null && !(_electricChargeRequired.ReadBoolean(settings) ?? true))
            {
                return;
            }

            var frozen = _frozen.ReadDouble(module) ?? 0.0;
            var rate = (_chargePerMinute.ReadDouble(module) ?? 0.0) / 60.0 * frozen;
            sink.Add(null, "Freezer (" + frozen.ToString("F0") + " frozen)", EnergyRole.Consumer, EnergyBehaviour.Constant, rate, null, null);
        }
    }
}
