// ToolbarControl.dll carries a KSPAssembly attribute, so KSP honours the dependency below and simply does not
// load this assembly when the mod is missing: no error, and the calculator carries on with the stock
// application launcher. Note the declared name is "ToolbarController", not "ToolbarControl" - the attribute
// inside the DLL does not match its file name, and a dependency on the file name would never resolve.
//
// The dependency on the main assembly makes KSP load it first, which this assembly's host registration needs.

[assembly: KSPAssembly("SuMa.Ksp.BatteryCalculator.Toolbar", 1, 0)]
[assembly: KSPAssemblyDependency("ToolbarController", 1, 0)]
[assembly: KSPAssemblyDependency("SuMa.Ksp.BatteryCalculator", 1, 0)]
