using SuMa.Ksp.BatteryCalculator.Interface;
using SuMa.Ksp.BatteryCalculator.Support;
using UnityEngine;

namespace SuMa.Ksp.BatteryCalculator.Toolbar
{
    /// <summary>
    /// The one line this whole assembly exists for: tell the main assembly to hang its button on ToolbarControl
    /// instead of on the stock application launcher.
    ///
    /// KSPAddon.Startup.Instantly runs at game start, long before any editor or flight addon asks for a button,
    /// so the swap is always in place before it matters. If this assembly is not loaded, because ToolbarControl
    /// is not installed, the registry keeps its stock default and everything still works.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    internal class ToolbarHostInstaller : MonoBehaviour
    {
        private void Start()
        {
            ToolbarButtonRegistry.Host = new ToolbarControlHost();

            Log.Info("ToolbarControl is installed, so the button will also appear on Blizzy's toolbar.");
        }
    }
}
