using System;
using KSP.UI.Screens;

namespace SuMa.Ksp.BatteryCalculator.Interface
{
    /// <summary>
    /// Everything a toolbar needs to know about our button, in a form no particular toolbar owns.
    ///
    /// Both texture paths are carried because the two hosts want different things: the stock launcher shows
    /// only the 38x38 icon, while Blizzy's toolbar is 24x24. Paths are GameData relative and extensionless,
    /// which is what both <see cref="GameDatabase.GetTexture" /> and ToolbarControl expect.
    ///
    /// Public, rather than internal like most of this assembly, because the satellite assembly that adds
    /// ToolbarControl support has to be able to see it.
    /// </summary>
    public class ToolbarButtonDefinition
    {
        public ToolbarButtonDefinition(string modId, string buttonId, string displayName, string tooltip, string largeTexturePath, string smallTexturePath, ApplicationLauncher.AppScenes scenes)
        {
            ModId = modId;
            ButtonId = buttonId;
            DisplayName = displayName;
            Tooltip = tooltip;
            LargeTexturePath = largeTexturePath;
            SmallTexturePath = smallTexturePath;
            Scenes = scenes;
        }

        /// <summary>
        /// Unique within <see cref="ModId" />. ToolbarControl keys its registration on the pair.
        /// </summary>
        public string ButtonId { get; }

        /// <summary>
        /// The name a player sees when configuring Blizzy's toolbar.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// The icon the button is created with. Later icon changes go through
        /// <see cref="IToolbarButtonHost.SetTexture" /> and do not change this.
        /// </summary>
        public string LargeTexturePath { get; }

        public string ModId { get; }

        /// <summary>
        /// Raised when the button is switched off.
        /// </summary>
        public Action OnFalse { get; set; }

        /// <summary>
        /// Raised when the button is switched on.
        /// </summary>
        public Action OnTrue { get; set; }

        public ApplicationLauncher.AppScenes Scenes { get; }

        public string SmallTexturePath { get; }

        public string Tooltip { get; }
    }
}
