using System.Collections.Generic;

namespace SuMa.Ksp.BatteryCalculator.Calculation
{
    /// <summary>
    /// Reading electric charge rates out of the places stock keeps them.
    /// </summary>
    internal static class Rates
    {
        public const string ElectricCharge = "ElectricCharge";

        /// <summary>
        /// The configured electric charge per second a module's resource handler draws: the RESOURCE nodes of
        /// modules such as ModuleCommand, reaction wheels and radiators. This is the nominal rate, not what
        /// arrived in the last tick, which is zero while the batteries are empty and scaled during warp.
        /// </summary>
        public static double HandlerInput(PartModule module)
        {
            return module != null && module.resHandler != null ? Sum(module.resHandler.inputResources) : 0.0;
        }

        public static double HandlerOutput(PartModule module)
        {
            return module != null && module.resHandler != null ? Sum(module.resHandler.outputResources) : 0.0;
        }

        /// <summary>
        /// The electric charge ratio in a converter's recipe list, per second at full efficiency.
        /// </summary>
        public static double RecipeRatio(List<ResourceRatio> ratios)
        {
            if (ratios == null)
            {
                return 0.0;
            }

            var ratio = 0.0;
            foreach (var resourceRatio in ratios)
            {
                if (resourceRatio.ResourceName == ElectricCharge)
                {
                    ratio += resourceRatio.Ratio;
                }
            }

            return ratio;
        }

        private static double Sum(List<ModuleResource> resources)
        {
            if (resources == null)
            {
                return 0.0;
            }

            var rate = 0.0;
            foreach (var resource in resources)
            {
                if (resource.id == PartResourceLibrary.ElectricityHashcode)
                {
                    rate += resource.rate;
                }
            }

            return rate;
        }
    }
}
