using System;
using System.Collections.Generic;
using KSP.UI.Screens;
using SuMa.Ksp.BatteryCalculator.Support;
using UnityEngine;

namespace SuMa.Ksp.BatteryCalculator.Interface
{
    /// <summary>
    /// The always available toolbar: KSP's own application launcher, the strip of icons down the side of the
    /// screen. No mod is needed for it, which is what makes it the default.
    ///
    /// The launcher is not necessarily awake when a scene addon starts, so a button asked for too early is
    /// parked until KSP says the launcher is ready. An icon change asked for while the button is still parked
    /// is kept and applied when it is created.
    /// </summary>
    internal class StockApplicationLauncherHost : IToolbarButtonHost
    {
        private readonly Dictionary<ToolbarButtonDefinition, ApplicationLauncherButton> _buttons = new Dictionary<ToolbarButtonDefinition, ApplicationLauncherButton>();
        private readonly Dictionary<ToolbarButtonDefinition, string> _currentTexturePaths = new Dictionary<ToolbarButtonDefinition, string>();
        private readonly List<ToolbarButtonDefinition> _pending = new List<ToolbarButtonDefinition>();

        private bool _isWaitingForLauncher;

        public void Add(ToolbarButtonDefinition definition)
        {
            if (definition == null || _buttons.ContainsKey(definition) || _pending.Contains(definition))
            {
                return;
            }

            if (ApplicationLauncher.Ready && ApplicationLauncher.Instance != null)
            {
                CreateButton(definition);
                return;
            }

            _pending.Add(definition);
            if (_isWaitingForLauncher)
            {
                return;
            }

            // An instance method, never a static one: GameEvents reads the delegate's Target to record the
            // subscriber, and a null Target throws out of the Add.
            GameEvents.onGUIApplicationLauncherReady.Add(OnApplicationLauncherReady);
            _isWaitingForLauncher = true;
        }

        public void Remove(ToolbarButtonDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            _pending.Remove(definition);
            _currentTexturePaths.Remove(definition);

            if (_buttons.TryGetValue(definition, out var button))
            {
                _buttons.Remove(definition);

                try
                {
                    if (button != null && ApplicationLauncher.Instance != null)
                    {
                        ApplicationLauncher.Instance.RemoveModApplication(button);
                    }
                }
                catch (Exception exception)
                {
                    Log.Error("Failed to remove the application launcher button: " + exception);
                }
            }

            if (_buttons.Count < 1 && _pending.Count < 1 && _isWaitingForLauncher)
            {
                GameEvents.onGUIApplicationLauncherReady.Remove(OnApplicationLauncherReady);
                _isWaitingForLauncher = false;
            }
        }

        public void SetState(ToolbarButtonDefinition definition, bool isOn)
        {
            if (definition == null || !_buttons.TryGetValue(definition, out var button) || button == null)
            {
                return;
            }

            // SetTrue/SetFalse with makeCall false changes the visual state without re-entering our own
            // callbacks, which would otherwise toggle the window straight back again.
            if (isOn)
            {
                button.SetTrue(false);
            }
            else
            {
                button.SetFalse(false);
            }
        }

        public void SetTexture(ToolbarButtonDefinition definition, string largeTexturePath, string smallTexturePath)
        {
            if (definition == null)
            {
                return;
            }

            if (_currentTexturePaths.TryGetValue(definition, out var currentPath) && currentPath == largeTexturePath)
            {
                return;
            }

            _currentTexturePaths[definition] = largeTexturePath;

            if (!_buttons.TryGetValue(definition, out var button) || button == null)
            {
                return;
            }

            var texture = LoadTexture(largeTexturePath);
            if (texture != null)
            {
                button.SetTexture(texture);
            }
        }

        private static Texture2D LoadTexture(string path)
        {
            var texture = GameDatabase.Instance == null ? null : GameDatabase.Instance.GetTexture(path, false);
            if (texture == null)
            {
                Log.Warning("No toolbar icon at '" + path + "'; the button will be blank.");
            }

            return texture;
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

        private void CreateButton(ToolbarButtonDefinition definition)
        {
            try
            {
                var texturePath = _currentTexturePaths.TryGetValue(definition, out var currentPath) ? currentPath : definition.LargeTexturePath;
                var button = ApplicationLauncher.Instance.AddModApplication(
                    () => Raise(definition.OnTrue),
                    () => Raise(definition.OnFalse),
                    null,
                    null,
                    null,
                    null,
                    definition.Scenes,
                    LoadTexture(texturePath));

                _buttons.Add(definition, button);
            }
            catch (Exception exception)
            {
                Log.Error("Failed to add the application launcher button: " + exception);
            }
        }

        private void OnApplicationLauncherReady()
        {
            if (ApplicationLauncher.Instance == null)
            {
                return;
            }

            var pending = _pending.ToArray();
            _pending.Clear();
            foreach (var definition in pending)
            {
                CreateButton(definition);
            }
        }
    }
}
