using System;
using System.Collections.Generic;
using SuMa.Ksp.BatteryCalculator.Calculation;

namespace SuMa.Ksp.BatteryCalculator.Handlers.Stock
{
    /// <summary>
    /// Everything built on BaseConverter: stock ISRU, fuel cells, drills and labs, and the mods that build on
    /// them - every USI converter and harvester, SystemHeat's converters, WOLF's hoppers.
    ///
    /// The recipe's electric charge ratio is the rate at full efficiency. Stock scales it by the converter's
    /// efficiency, and that includes the specialist bonus: an ISRU with no engineer aboard draws only a
    /// twentieth of it. In flight the converter's efficiency bonus and crew bonus are used, but not its heat
    /// throttle: that one sags while a converter warms up or catches up after a vessel is loaded, and
    /// planning with it would make a running converter look nearly free. The editor has no crew bonus to go
    /// on and so plans at full rate, which errs towards needing more battery rather than less.
    ///
    /// USI converters need nothing special: their swap options replace the converter's recipe lists in the
    /// editor and in flight alike, so the recipe read here is the loadout that is selected.
    ///
    /// A converter that makes electric charge out of something else is a fueled producer, counted only when
    /// its fuel lasts. One that uses electric charge is carried to be run, so it is counted in the editor and
    /// in flight while it runs - and always, when it is always active, which makes it a constant load.
    /// </summary>
    internal sealed class ConverterHandler : IEnergyHandler
    {
        public Type ModuleType => typeof(BaseConverter);

        public void Collect(PartModule module, EntrySink sink)
        {
            var converter = (BaseConverter)module;
            var context = sink.Context;
            bool? isRunning = context.IsFlight ? converter.AlwaysActive || converter.IsActivated : (bool?)null;
            var consumerBehaviour = converter.AlwaysActive ? EnergyBehaviour.Constant : EnergyBehaviour.Continuous;
            var label = string.IsNullOrEmpty(converter.ConverterName) ? null : converter.ConverterName;
            var efficiency = context.IsFlight ? FlightEfficiency(converter) : 1.0;
            var detail = context.IsFlight ? "At the converter's current efficiency." : "At full efficiency.";

            var input = Rates.RecipeRatio(converter.inputList);
            if (input <= 0.0)
            {
                input = PowerRequirement(converter);
            }

            if (input > 0.0)
            {
                sink.Add("in", label, EnergyRole.Consumer, consumerBehaviour, input * efficiency, isRunning, detail);
            }

            var output = Rates.RecipeRatio(converter.outputList);
            if (output <= 0.0)
            {
                return;
            }

            var fuel = new List<ResourceRatio>();
            foreach (var ratio in converter.inputList)
            {
                if (ratio.ResourceName != Rates.ElectricCharge)
                {
                    fuel.Add(ratio);
                }
            }

            if (fuel.Count > 0)
            {
                sink.Add("out", label, EnergyRole.Producer, EnergyBehaviour.Fueled, output * efficiency, isRunning, context.FuelDuration(converter.part, fuel), detail);
            }
            else
            {
                sink.Add("out", label, EnergyRole.Producer, consumerBehaviour, output * efficiency, isRunning, detail);
            }
        }

        private static double FlightEfficiency(BaseConverter converter)
        {
            try
            {
                var efficiency = converter.EfficiencyBonus * converter.GetCrewEfficiencyBonus();
                return efficiency > 0.0 ? efficiency : 1.0;
            }
            catch (Exception)
            {
                // A mod's crew bonus that fails is not worth losing the row for; full efficiency is the
                // cautious answer for a consumer.
                return 1.0;
            }
        }

        /// <summary>
        /// The converters whose recipe is built in code rather than from INPUT_RESOURCE nodes, and so has no
        /// electric charge in inputList: labs, and the asteroid and comet drills.
        /// </summary>
        private static double PowerRequirement(BaseConverter converter)
        {
            switch (converter)
            {
                case ModuleScienceConverter lab:
                    return lab.powerRequirement;
                case ModuleAsteroidDrill asteroidDrill:
                    return asteroidDrill.PowerConsumption;
                case ModuleCometDrill cometDrill:
                    return cometDrill.PowerConsumption;
                default:
                    return 0.0;
            }
        }
    }
}
