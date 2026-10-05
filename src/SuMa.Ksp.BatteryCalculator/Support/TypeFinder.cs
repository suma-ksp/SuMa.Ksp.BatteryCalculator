using System;

namespace SuMa.Ksp.BatteryCalculator.Support
{
    /// <summary>
    /// Finds another mod's types by name at runtime, which is how this assembly supports mods without
    /// referencing them: a mod that is not installed simply has no type to find.
    /// </summary>
    internal static class TypeFinder
    {
        /// <summary>
        /// A type by its full name, such as "SystemHeat.ModuleSystemHeatFissionReactor", or a part module by its
        /// bare class name, the way part configs name modules. Null when no loaded assembly has it.
        /// </summary>
        public static Type Find(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (name.IndexOf('.') < 0)
            {
                return AssemblyLoader.GetClassByName(typeof(PartModule), name);
            }

            foreach (var loadedAssembly in AssemblyLoader.loadedAssemblies)
            {
                Type type;
                try
                {
                    type = loadedAssembly.assembly.GetType(name, false);
                }
                catch (Exception)
                {
                    // An assembly that cannot be asked is not the one we are looking for.
                    continue;
                }

                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
