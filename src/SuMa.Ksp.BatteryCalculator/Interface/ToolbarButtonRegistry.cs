using System;
using SuMa.Ksp.BatteryCalculator.Support;

namespace SuMa.Ksp.BatteryCalculator.Interface
{
    /// <summary>
    /// The one place this mod puts a toolbar button, and the one place another assembly can change how that
    /// happens.
    ///
    /// SuMa.Ksp.BatteryCalculator.Toolbar assigns <see cref="Host" /> from a KSPAddon that runs at
    /// KSPAddon.Startup.Instantly, which is long before any scene addon asks for a button, so there is no race
    /// to lose. Without that assembly the stock launcher host stays in place and everything still works; it is
    /// just the stock toolbar only, with no Blizzy support.
    ///
    /// Every call is guarded, because a toolbar that will not take a button is an annoyance and must never
    /// become an exception escaping into a scene addon's Start.
    /// </summary>
    public static class ToolbarButtonRegistry
    {
        private static IToolbarButtonHost _host;

        public static IToolbarButtonHost Host
        {
            get { return _host ?? (_host = new StockApplicationLauncherHost()); }
            set { _host = value; }
        }

        public static void Add(ToolbarButtonDefinition definition)
        {
            Guard(() => Host.Add(definition), "add");
        }

        public static void Remove(ToolbarButtonDefinition definition)
        {
            Guard(() => Host.Remove(definition), "remove");
        }

        public static void SetState(ToolbarButtonDefinition definition, bool isOn)
        {
            Guard(() => Host.SetState(definition, isOn), "update");
        }

        public static void SetTexture(ToolbarButtonDefinition definition, string largeTexturePath, string smallTexturePath)
        {
            Guard(() => Host.SetTexture(definition, largeTexturePath, smallTexturePath), "recolour");
        }

        private static void Guard(Action action, string verb)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Log.Error("Failed to " + verb + " the toolbar button: " + exception);
            }
        }
    }
}
