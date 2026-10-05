using System;
using SuMa.Ksp.BatteryCalculator.Calculation;
using SuMa.Ksp.BatteryCalculator.Support;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Mods
{
    /// <summary>
    /// Near Future Solar's curved panels, which are not stock panels and so need reading separately.
    ///
    /// NFS gives each of the panel's cells TotalEnergyRate / cells at home, scales it by the cosine of that
    /// cell's angle to the Sun, and by (home's distance above the Sun's surface / the vessel's)². It applies no
    /// atmosphere or temperature. The calculator uses the best orientation the cells allow and the distance of
    /// the selected body.
    /// </summary>
    internal sealed class CurvedSolarPanelHandler : IEnergyHandler
    {
        private const string TypeName = "NearFutureSolar.ModuleCurvedSolarPanel";

        private readonly MemberReader _deployable;
        private readonly MemberReader _homeAltitudeAboveSun;
        private readonly MemberReader _panelTransformName;
        private readonly MemberReader _state;
        private readonly MemberReader _totalEnergyRate;

        private CurvedSolarPanelHandler(Type moduleType)
        {
            ModuleType = moduleType;
            _deployable = MemberReader.For(moduleType, "Deployable");
            _homeAltitudeAboveSun = MemberReader.For(moduleType, "homeAltAboveSun");
            _panelTransformName = MemberReader.For(moduleType, "PanelTransformName");
            _state = MemberReader.For(moduleType, "State");
            _totalEnergyRate = MemberReader.For(moduleType, "TotalEnergyRate");
        }

        public Type ModuleType { get; }

        public static CurvedSolarPanelHandler TryCreate()
        {
            var type = TypeFinder.Find(TypeName);
            if (type == null)
            {
                return null;
            }

            var handler = new CurvedSolarPanelHandler(type);
            if (handler._totalEnergyRate != null)
            {
                return handler;
            }

            Log.Warning(TypeName + " has no TotalEnergyRate; curved panels are listed from their resource handler only.");
            return null;
        }

        public void Collect(PartModule module, EntrySink sink)
        {
            var totalRate = _totalEnergyRate.ReadDouble(module) ?? 0.0;
            var transformName = _panelTransformName != null ? _panelTransformName.ReadString(module) : null;
            var exposure = string.IsNullOrEmpty(transformName) ? 1.0 : SolarExposure.Best(module.part.FindModelTransforms(transformName));

            var context = sink.Context;
            var homeAltitude = _homeAltitudeAboveSun != null ? _homeAltitudeAboveSun.ReadDouble(module) ?? 0.0 : 0.0;
            if (homeAltitude <= 0.0)
            {
                homeAltitude = HomeAltitudeAboveSun();
            }

            var distanceFactor = context.Solar.SurfaceDistance > 0.0 ? homeAltitude * homeAltitude / (context.Solar.SurfaceDistance * context.Solar.SurfaceDistance) : 0.0;

            bool? isOn = null;
            if (context.IsFlight)
            {
                var deployable = _deployable != null && (_deployable.ReadBoolean(module) ?? false);
                var state = _state != null ? _state.ReadString(module) : null;
                isOn = !deployable || state == "EXTENDED";
            }

            sink.Add(null, "Solar panel", EnergyRole.Producer, EnergyBehaviour.Solar, totalRate * exposure * distanceFactor, isOn, "Curved panel at its best orientation.");
        }

        private static double HomeAltitudeAboveSun()
        {
            var home = FlightGlobals.GetHomeBody();
            var sun = DarknessProfile.Sun();
            if (home == null || sun == null || home.orbit == null)
            {
                return 0.0;
            }

            return home.orbit.semiMajorAxis - sun.Radius;
        }
    }
}
