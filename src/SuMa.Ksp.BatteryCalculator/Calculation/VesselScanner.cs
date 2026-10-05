using System;
using System.Collections.Generic;
using SuMa.Ksp.BatteryCalculator.Handlers;
using SuMa.Ksp.BatteryCalculator.Persistence;
using SuMa.Ksp.BatteryCalculator.Support;

namespace SuMa.Ksp.BatteryCalculator.Calculation
{
    /// <summary>
    /// Walks a craft or vessel part by part and module by module, hands each module to its handler, and puts
    /// the player's saved choices back onto what comes out.
    ///
    /// Only configured rates are read, never what flowed in the last tick. That keeps the result steady when
    /// a vessel has just been loaded or switched to, while converters catch up on the time it was away and
    /// the live flows are anything but typical, and while the game is warping.
    /// </summary>
    internal static class VesselScanner
    {
        private static readonly HashSet<Type> ReportedFailures = new HashSet<Type>();

        /// <summary>
        /// The electric charge the vessel can store, and currently holds, in tanks that are not locked.
        /// </summary>
        public static void MeasureStorage(IList<Part> parts, out double capacity, out double stored)
        {
            capacity = 0.0;
            stored = 0.0;
            foreach (var part in parts)
            {
                if (part == null)
                {
                    continue;
                }

                foreach (var resource in part.Resources)
                {
                    if (resource.info == null || resource.info.id != PartResourceLibrary.ElectricityHashcode || !resource.flowState)
                    {
                        continue;
                    }

                    capacity += resource.maxAmount;
                    stored += resource.amount;
                }
            }
        }

        public static List<EnergyEntry> Scan(EnergyContext context, HandlerRegistry registry)
        {
            var entries = new List<EnergyEntry>();
            var sink = new EntrySink(context, entries);
            var indexByModuleName = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var part in context.Parts)
            {
                if (part == null)
                {
                    continue;
                }

                indexByModuleName.Clear();
                foreach (var module in part.Modules)
                {
                    if (module == null)
                    {
                        continue;
                    }

                    // The key has to count every module of the name, enabled or not, or switching one off would
                    // shift the keys of the ones after it and scramble the saved choices.
                    var moduleName = string.IsNullOrEmpty(module.moduleName) ? module.GetType().Name : module.moduleName;
                    indexByModuleName.TryGetValue(moduleName, out var index);
                    indexByModuleName[moduleName] = index + 1;

                    // B9PartSwitch and its like switch unused modules off this way.
                    if (!module.isEnabled)
                    {
                        continue;
                    }

                    var handler = registry.Find(module.GetType()) ?? registry.Fallback;
                    sink.BeginModule(part, module, moduleName + "#" + index);
                    try
                    {
                        handler.Collect(module, sink);
                    }
                    catch (Exception exception)
                    {
                        // A handler that fails halfway through a converter must not leave its input behind
                        // without the output, so whatever the module reported goes with it.
                        sink.DiscardModule();
                        ReportFailure(module.GetType(), exception);
                    }
                    finally
                    {
                        sink.EndModule();
                    }
                }
            }

            foreach (var vesselHandler in registry.VesselHandlers)
            {
                try
                {
                    vesselHandler.Collect(sink);
                }
                catch (Exception exception)
                {
                    ReportFailure(vesselHandler.GetType(), exception);
                }
            }

            foreach (var entry in entries)
            {
                if (BatteryCalculatorScenario.TryGetChoice(entry.PersistenceId, entry.Key, out var included))
                {
                    entry.IsIncluded = included;
                }
            }

            return entries;
        }

        private static void ReportFailure(Type type, Exception exception)
        {
            // Once per type: this runs every second, and a handler that fails once will fail every time.
            if (ReportedFailures.Add(type))
            {
                Log.Error("Reading " + type.FullName + " failed; its power is left out: " + exception);
            }
        }
    }
}
