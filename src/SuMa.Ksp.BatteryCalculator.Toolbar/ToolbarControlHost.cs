using System;
using System.Collections.Generic;
using SuMa.Ksp.BatteryCalculator.Interface;
using SuMa.Ksp.BatteryCalculator.Support;
using ToolbarControl_NS;
using UnityEngine;

namespace SuMa.Ksp.BatteryCalculator.Toolbar
{
    /// <summary>
    /// Puts our button on every toolbar the player has, through linuxgurugamer's ToolbarControl: the stock
    /// application launcher and Blizzy's toolbar, from one registration, with the choice between them left
    /// where it belongs, in ToolbarControl's own settings.
    ///
    /// A ToolbarControl is a MonoBehaviour, so the button gets a host GameObject of its own rather than
    /// borrowing one from a scene addon. That keeps the lifetime in one place: the object is created with the
    /// button and destroyed with it.
    /// </summary>
    internal class ToolbarControlHost : IToolbarButtonHost
    {
        private readonly Dictionary<ToolbarButtonDefinition, ToolbarControl> _controls = new Dictionary<ToolbarButtonDefinition, ToolbarControl>();
        private readonly Dictionary<ToolbarButtonDefinition, string> _currentTexturePaths = new Dictionary<ToolbarButtonDefinition, string>();
        private readonly HashSet<string> _registeredMods = new HashSet<string>(StringComparer.Ordinal);

        public void Add(ToolbarButtonDefinition definition)
        {
            if (definition == null || _controls.ContainsKey(definition))
            {
                return;
            }

            if (_registeredMods.Add(definition.ModId))
            {
                ToolbarControl.RegisterMod(definition.ModId, definition.DisplayName);
            }

            var hostObject = new GameObject("SuMa.BatteryCalculator.ToolbarControl." + definition.ButtonId);
            var control = hostObject.AddComponent<ToolbarControl>();
            control.AddToAllToolbars(
                () => Raise(definition.OnTrue),
                () => Raise(definition.OnFalse),
                definition.Scenes,
                definition.ModId,
                definition.ButtonId,
                definition.LargeTexturePath,
                definition.SmallTexturePath,
                definition.Tooltip);

            _controls.Add(definition, control);
        }

        public void Remove(ToolbarButtonDefinition definition)
        {
            if (definition == null || !_controls.TryGetValue(definition, out var control))
            {
                return;
            }

            _controls.Remove(definition);
            _currentTexturePaths.Remove(definition);

            if (control == null)
            {
                return;
            }

            // ToolbarControl wants its own teardown run before the component goes, or it leaves a dead button
            // behind on Blizzy's toolbar.
            control.OnDestroy();
            UnityEngine.Object.Destroy(control.gameObject);
        }

        public void SetState(ToolbarButtonDefinition definition, bool isOn)
        {
            if (definition == null || !_controls.TryGetValue(definition, out var control) || control == null)
            {
                return;
            }

            if (isOn)
            {
                control.SetTrue(false);
            }
            else
            {
                control.SetFalse(false);
            }
        }

        public void SetTexture(ToolbarButtonDefinition definition, string largeTexturePath, string smallTexturePath)
        {
            if (definition == null || !_controls.TryGetValue(definition, out var control) || control == null)
            {
                return;
            }

            // ToolbarControl reloads the texture on every call, and the icon is refreshed far more often than it
            // changes colour.
            if (_currentTexturePaths.TryGetValue(definition, out var currentPath) && currentPath == largeTexturePath)
            {
                return;
            }

            _currentTexturePaths[definition] = largeTexturePath;
            control.SetTexture(largeTexturePath, smallTexturePath);
        }

        private static void Raise(Action callback)
        {
            if (callback == null)
            {
                return;
            }

            try
            {
                callback();
            }
            catch (Exception exception)
            {
                Log.Error("A toolbar button callback threw: " + exception);
            }
        }
    }
}
