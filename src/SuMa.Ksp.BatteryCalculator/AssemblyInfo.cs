// Tagged so that SuMa.Ksp.BatteryCalculator.Toolbar can declare a KSPAssemblyDependency on it and be loaded
// after it. The ClickThroughBlocker dependency matches the one direct mod reference this assembly has: without
// it KSP skips the assembly cleanly instead of failing on its first window.

[assembly: KSPAssembly("SuMa.Ksp.BatteryCalculator", 1, 0)]
[assembly: KSPAssemblyDependency("ClickThroughBlocker", 2, 1)]
