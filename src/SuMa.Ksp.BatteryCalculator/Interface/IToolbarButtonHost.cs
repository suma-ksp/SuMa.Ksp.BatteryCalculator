namespace SuMa.Ksp.BatteryCalculator.Interface
{
    /// <summary>
    /// A toolbar our button can live on.
    ///
    /// The point of the indirection is that this assembly must not reference ToolbarControl: a missing
    /// ToolbarControl.dll would then stop the whole calculator loading. The stock launcher implementation here
    /// always works, and SuMa.Ksp.BatteryCalculator.Toolbar swaps in a ToolbarControl backed one when that mod
    /// is installed.
    /// </summary>
    public interface IToolbarButtonHost
    {
        void Add(ToolbarButtonDefinition definition);

        void Remove(ToolbarButtonDefinition definition);

        /// <summary>
        /// Pushes the button in or out without raising its callbacks, so a window closed by its own title bar
        /// can leave the toolbar showing the truth.
        /// </summary>
        void SetState(ToolbarButtonDefinition definition, bool isOn);

        /// <summary>
        /// Swaps the button's icon, for the red and green battery. Both paths are GameData relative and
        /// extensionless.
        /// </summary>
        void SetTexture(ToolbarButtonDefinition definition, string largeTexturePath, string smallTexturePath);
    }
}
