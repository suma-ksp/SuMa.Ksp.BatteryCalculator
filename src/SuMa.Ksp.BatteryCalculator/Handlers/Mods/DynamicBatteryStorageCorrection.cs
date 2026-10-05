using System;
using System.Collections.Generic;
using SuMa.Ksp.BatteryCalculator.Support;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Mods
{
    /// <summary>
    /// DynamicBatteryStorage enlarges one battery of a vessel while it warps at 100x and above, so that a
    /// whole physics step of production fits into it. Read then, the vessel seems to carry far more charge
    /// than it does. The mod keeps the battery's real size, and this takes the difference back off.
    /// </summary>
    internal static class DynamicBatteryStorageCorrection
    {
        private const string TypeName = "DynamicBatteryStorage.ModuleDynamicBatteryStorage";

        private static MemberReader _bufferResource;
        private static bool _isResolved;
        private static Type _moduleType;
        private static MemberReader _savedMaximum;

        /// <summary>
        /// Shrinks <paramref name="capacity" /> back to what the vessel's batteries really hold, and the charge
        /// stored with it, when DynamicBatteryStorage has enlarged one of them.
        /// </summary>
        public static void Apply(IList<Part> parts, ref double capacity, ref double stored)
        {
            if (!Resolve() || parts == null || parts.Count == 0 || parts[0] == null || parts[0].vessel == null)
            {
                return;
            }

            foreach (var vesselModule in parts[0].vessel.vesselModules)
            {
                if (vesselModule == null || !_moduleType.IsInstanceOfType(vesselModule))
                {
                    continue;
                }

                if (!(_bufferResource.Read(vesselModule) is PartResource buffer) || !buffer.flowState)
                {
                    return;
                }

                var savedMaximum = _savedMaximum.ReadDouble(vesselModule) ?? 0.0;
                var enlargement = buffer.maxAmount - savedMaximum;
                if (savedMaximum > 0.0 && enlargement > 0.0)
                {
                    capacity = Math.Max(0.0, capacity - enlargement);
                    stored = Math.Min(stored, capacity);
                }

                return;
            }
        }

        private static bool Resolve()
        {
            if (!_isResolved)
            {
                _isResolved = true;
                _moduleType = TypeFinder.Find(TypeName);
                _bufferResource = MemberReader.For(_moduleType, "BufferResource");
                _savedMaximum = MemberReader.For(_moduleType, "SavedMaxEC");
                if (_moduleType != null && (_bufferResource == null || _savedMaximum == null))
                {
                    Log.Warning(TypeName + " has no BufferResource or SavedMaxEC; battery capacity may read too high while warping fast.");
                    _moduleType = null;
                }
                else if (_moduleType != null)
                {
                    Log.Info("Supporting " + TypeName);
                }
            }

            return _moduleType != null;
        }
    }
}
