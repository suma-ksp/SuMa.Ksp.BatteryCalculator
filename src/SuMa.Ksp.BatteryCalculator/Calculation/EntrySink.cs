using System.Collections.Generic;
using SuMa.Ksp.BatteryCalculator.Support;

namespace SuMa.Ksp.BatteryCalculator.Calculation
{
    /// <summary>
    /// Where handlers report what they found. It turns a report into an <see cref="EnergyEntry" />, giving it
    /// its key and deciding whether it is counted by default, so every handler follows the same rules:
    ///
    ///   Constant    counted, unless the module says it is off.
    ///   Continuous  counted in the editor, where it is taken to run as intended; in flight while it runs.
    ///   Switchable  counted in flight while it is switched on; never in the editor, where nothing runs yet.
    ///   OnDemand    never counted: it only draws while the player uses it.
    ///   Fueled      counted only if its fuel lasts at least the configured time, and in flight only while
    ///               it runs.
    ///   Solar       counted, unless the module says it cannot produce, such as a retracted panel.
    /// </summary>
    internal sealed class EntrySink
    {
        private const double NegligibleRate = 1e-9;

        private readonly EnergyContext _context;
        private readonly List<EnergyEntry> _entries;

        private PartModule _module;
        private int _moduleFirstEntry;
        private string _moduleKey;
        private Part _part;

        public EntrySink(EnergyContext context, List<EnergyEntry> entries)
        {
            _context = context;
            _entries = entries;
        }

        public EnergyContext Context => _context;

        /// <summary>
        /// Reports a producer or consumer of the module currently being scanned.
        /// </summary>
        /// <param name="subKey">Tells several entries of one module apart, for instance a converter's input
        /// from its output; null when the module has only one.</param>
        /// <param name="label">What to call it; null for the module's own display name.</param>
        /// <param name="isOn">Whether the module is switched on right now, or null when that is unknown or
        /// does not apply.</param>
        /// <param name="fuelDuration">For a fueled producer, how long the fuel aboard lasts, in seconds.</param>
        public void Add(string subKey, string label, EnergyRole role, EnergyBehaviour behaviour, double rate, bool? isOn, double fuelDuration, string detail)
        {
            if (_module == null || rate <= NegligibleRate)
            {
                return;
            }

            var key = subKey == null ? _moduleKey : _moduleKey + "/" + subKey;
            var included = IsIncludedByDefault(behaviour, isOn, fuelDuration);
            if (behaviour == EnergyBehaviour.Fueled)
            {
                detail = Combine(double.IsPositiveInfinity(fuelDuration) ? null : "Fuel aboard lasts " + Format.Time(fuelDuration) + ".", detail);
            }

            _entries.Add(new EnergyEntry(_part, _part.persistentId, key, label ?? ModuleLabel(_module), role, behaviour, rate, included, detail));
        }

        public void Add(string subKey, string label, EnergyRole role, EnergyBehaviour behaviour, double rate, bool? isOn, string detail)
        {
            Add(subKey, label, role, behaviour, rate, isOn, double.PositiveInfinity, detail);
        }

        /// <summary>
        /// Reports an entry that belongs to the vessel as a whole rather than to a part, such as life support.
        /// </summary>
        public void AddVesselEntry(string key, string label, EnergyRole role, EnergyBehaviour behaviour, double rate, bool? isOn, string detail)
        {
            if (rate <= NegligibleRate)
            {
                return;
            }

            _entries.Add(new EnergyEntry(null, _context.VesselPersistenceId, key, label, role, behaviour, rate, IsIncludedByDefault(behaviour, isOn, double.PositiveInfinity), detail));
        }

        public void BeginModule(Part part, PartModule module, string moduleKey)
        {
            _part = part;
            _module = module;
            _moduleKey = moduleKey;
            _moduleFirstEntry = _entries.Count;
        }

        /// <summary>
        /// Takes back everything the current module reported, for a handler that failed partway.
        /// </summary>
        public void DiscardModule()
        {
            if (_module != null && _entries.Count > _moduleFirstEntry)
            {
                _entries.RemoveRange(_moduleFirstEntry, _entries.Count - _moduleFirstEntry);
            }
        }

        public void EndModule()
        {
            _part = null;
            _module = null;
            _moduleKey = null;
        }

        private static string Combine(string first, string second)
        {
            if (string.IsNullOrEmpty(first))
            {
                return second;
            }

            return string.IsNullOrEmpty(second) ? first : first + " " + second;
        }

        /// <summary>
        /// The name the module gives itself in the part's info, which is localised and usually reads better
        /// than the class name.
        /// </summary>
        private static string ModuleLabel(PartModule module)
        {
            string name = null;
            try
            {
                name = module.GetModuleDisplayName();
            }
            catch
            {
                // A mod's display name reaching for something that only exists in flight is its problem, not
                // a reason to lose the row.
            }

            return string.IsNullOrEmpty(name) ? module.moduleName : name.LocalizeRemoveGender();
        }

        private bool IsIncludedByDefault(EnergyBehaviour behaviour, bool? isOn, double fuelDuration)
        {
            switch (behaviour)
            {
                case EnergyBehaviour.Constant:
                case EnergyBehaviour.Solar:
                    return isOn ?? true;
                case EnergyBehaviour.Continuous:
                    return !_context.IsFlight || (isOn ?? true);
                case EnergyBehaviour.Switchable:
                    return _context.IsFlight && (isOn ?? false);
                case EnergyBehaviour.Fueled:
                    return fuelDuration >= _context.FueledMinimumDuration && (!_context.IsFlight || (isOn ?? true));
                default:
                    return false;
            }
        }
    }
}
