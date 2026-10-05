namespace SuMa.Ksp.BatteryCalculator.Calculation
{
    /// <summary>
    /// One producer or consumer of electric charge: a row in the window's lists.
    ///
    /// Most belong to a part module, and a part can have several - a probe core with a solar panel, or a
    /// converter with an input and an output. A few belong to the vessel as a whole, such as life support for
    /// the crew; those have no <see cref="Part" />.
    ///
    /// <see cref="PersistenceId" /> and <see cref="Key" /> together are what a player's choice is saved under,
    /// so both have to stay the same from one load to the next: the part's persistentId, or the craft's or
    /// vessel's for a vessel-wide entry, and the module's name and position within the part.
    /// </summary>
    internal sealed class EnergyEntry
    {
        public EnergyEntry(Part part, uint persistenceId, string key, string label, EnergyRole role, EnergyBehaviour behaviour, double rate, bool isIncludedByDefault, string detail)
        {
            Part = part;
            PersistenceId = persistenceId;
            Key = key;
            Label = label;
            Role = role;
            Behaviour = behaviour;
            Rate = rate;
            IsIncludedByDefault = isIncludedByDefault;
            Detail = detail;
            IsIncluded = isIncludedByDefault;
        }

        public EnergyBehaviour Behaviour { get; }

        /// <summary>
        /// Why the entry is what it is, shown as the row's tooltip: the fuel it would run out of, what the rate
        /// assumes, and so on. May be null.
        /// </summary>
        public string Detail { get; }

        /// <summary>
        /// The rows that are the same thing on the same kind of part - eight identical solar panels - are shown
        /// as one line. This is what they are grouped by.
        /// </summary>
        public string GroupKey => (Part != null && Part.partInfo != null ? Part.partInfo.name : string.Empty) + "|" + Key + "|" + Role;

        /// <summary>
        /// Whether the entry is counted: the player's choice if they made one, the default otherwise.
        /// </summary>
        public bool IsIncluded { get; set; }

        public bool IsIncludedByDefault { get; }

        /// <summary>
        /// Unique within its part and stable across loads, for instance "ModuleDeployableSolarPanel#0".
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// What the entry is, as the player would call it: "Solar panel", "Fuel Cell", a converter's name.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Null for an entry that belongs to the vessel as a whole.
        /// </summary>
        public Part Part { get; }

        public uint PersistenceId { get; }

        /// <summary>
        /// Electric charge per second, always positive; <see cref="Role" /> says which way it flows.
        /// </summary>
        public double Rate { get; }

        public EnergyRole Role { get; }

        /// <summary>
        /// The name the player knows the owner by: the part's title, or the vessel for a vessel-wide entry.
        /// </summary>
        public string Title => Part != null && Part.partInfo != null ? Part.partInfo.title : "Vessel";
    }
}
