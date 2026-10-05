using System;
using System.Collections.Generic;
using System.Globalization;

namespace SuMa.Ksp.BatteryCalculator.Persistence
{
    /// <summary>
    /// The little the calculator remembers between sessions, kept in the save rather than on the parts, and
    /// only where it differs from the defaults:
    ///
    ///   CHOICE { part = 1234567  entry = ModuleDataTransmitter#0  included = True }
    ///       a producer or consumer the player ticked or unticked against its default, keyed by the part's
    ///       persistentId. A launch keeps its parts' ids, so a choice made in the VAB is still there in flight.
    ///       Ticking a row back to its default removes the choice.
    ///   TARGET { id = 7654321  body = Mun  altitude = 30000  landed = False }
    ///       the body and orbit a craft or vessel is planned for, keyed by the ShipConstruct's or the vessel's
    ///       persistentId, written only once the player picks something other than the default.
    ///
    /// Nothing is added to any part, so removing the mod leaves only this one SCENARIO node behind, which KSP
    /// ignores. While no scenario is running - in scenes it is not added to, or before it has loaded - the
    /// same data is held in memory for the rest of the session.
    /// </summary>
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, GameScenes.EDITOR, GameScenes.FLIGHT)]
    public class BatteryCalculatorScenario : ScenarioModule
    {
        private const string AltitudeValueName = "altitude";
        private const string BodyValueName = "body";
        private const string ChoiceNodeName = "CHOICE";
        private const string EntryValueName = "entry";
        private const string IdValueName = "id";
        private const string IncludedValueName = "included";
        private const string LandedValueName = "landed";
        private const string PartValueName = "part";
        private const string TargetNodeName = "TARGET";

        private static readonly Dictionary<ChoiceKey, bool> Choices = new Dictionary<ChoiceKey, bool>();
        private static readonly Dictionary<uint, PlanningTarget> Targets = new Dictionary<uint, PlanningTarget>();

        public static void ClearChoices(IEnumerable<uint> partIds)
        {
            var ids = new HashSet<uint>(partIds);
            var keys = new List<ChoiceKey>();
            foreach (var key in Choices.Keys)
            {
                if (ids.Contains(key.PartId))
                {
                    keys.Add(key);
                }
            }

            foreach (var key in keys)
            {
                Choices.Remove(key);
            }
        }

        public static void ClearTarget(uint vesselId)
        {
            Targets.Remove(vesselId);
        }

        /// <summary>
        /// Records that the player set an entry to <paramref name="included" />, or forgets the entry's choice
        /// when that is its default anyway.
        /// </summary>
        public static void SetChoice(uint partId, string entryKey, bool included, bool isIncludedByDefault)
        {
            if (partId == 0 || string.IsNullOrEmpty(entryKey))
            {
                return;
            }

            var key = new ChoiceKey(partId, entryKey);
            if (included == isIncludedByDefault)
            {
                Choices.Remove(key);
            }
            else
            {
                Choices[key] = included;
            }
        }

        public static void SetTarget(uint vesselId, PlanningTarget target)
        {
            if (vesselId == 0 || target == null)
            {
                return;
            }

            Targets[vesselId] = target;
        }

        public static bool TryGetChoice(uint partId, string entryKey, out bool included)
        {
            return Choices.TryGetValue(new ChoiceKey(partId, entryKey), out included);
        }

        public static bool TryGetTarget(uint vesselId, out PlanningTarget target)
        {
            return Targets.TryGetValue(vesselId, out target);
        }

        /// <summary>
        /// A save is the whole truth about its own game, so whatever an earlier game left in memory goes. Done
        /// here rather than in OnLoad because KSP does not call OnLoad on a scenario it has just created for a
        /// save that never had one, while OnAwake always runs, and runs first.
        /// </summary>
        public override void OnAwake()
        {
            base.OnAwake();
            Choices.Clear();
            Targets.Clear();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            foreach (var choiceNode in node.GetNodes(ChoiceNodeName))
            {
                var entry = choiceNode.GetValue(EntryValueName);
                if (string.IsNullOrEmpty(entry) || !uint.TryParse(choiceNode.GetValue(PartValueName), out var partId) || !bool.TryParse(choiceNode.GetValue(IncludedValueName), out var included))
                {
                    continue;
                }

                Choices[new ChoiceKey(partId, entry)] = included;
            }

            foreach (var targetNode in node.GetNodes(TargetNodeName))
            {
                var bodyName = targetNode.GetValue(BodyValueName);
                if (string.IsNullOrEmpty(bodyName) || !uint.TryParse(targetNode.GetValue(IdValueName), out var vesselId) || !double.TryParse(targetNode.GetValue(AltitudeValueName), NumberStyles.Float, CultureInfo.InvariantCulture, out var altitude))
                {
                    continue;
                }

                bool.TryParse(targetNode.GetValue(LandedValueName), out var landed);
                Targets[vesselId] = new PlanningTarget(bodyName, altitude, landed);
            }
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            foreach (var choice in Choices)
            {
                var choiceNode = node.AddNode(ChoiceNodeName);
                choiceNode.AddValue(PartValueName, choice.Key.PartId);
                choiceNode.AddValue(EntryValueName, choice.Key.EntryKey);
                choiceNode.AddValue(IncludedValueName, choice.Value);
            }

            foreach (var target in Targets)
            {
                var targetNode = node.AddNode(TargetNodeName);
                targetNode.AddValue(IdValueName, target.Key);
                targetNode.AddValue(BodyValueName, target.Value.BodyName);
                targetNode.AddValue(AltitudeValueName, target.Value.Altitude.ToString("R", CultureInfo.InvariantCulture));
                targetNode.AddValue(LandedValueName, target.Value.IsLanded);
            }
        }

        private readonly struct ChoiceKey : IEquatable<ChoiceKey>
        {
            public ChoiceKey(uint partId, string entryKey)
            {
                PartId = partId;
                EntryKey = entryKey;
            }

            public string EntryKey { get; }

            public uint PartId { get; }

            public bool Equals(ChoiceKey other)
            {
                return PartId == other.PartId && string.Equals(EntryKey, other.EntryKey, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is ChoiceKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)PartId * 397) ^ (EntryKey != null ? StringComparer.Ordinal.GetHashCode(EntryKey) : 0);
                }
            }
        }
    }
}
