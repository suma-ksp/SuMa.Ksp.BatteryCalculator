using System;
using System.Collections.Generic;
using KSP.UI.Screens;
using SuMa.Ksp.BatteryCalculator.Calculation;
using SuMa.Ksp.BatteryCalculator.Handlers;
using SuMa.Ksp.BatteryCalculator.Handlers.Mods;
using SuMa.Ksp.BatteryCalculator.Persistence;
using SuMa.Ksp.BatteryCalculator.Settings;
using SuMa.Ksp.BatteryCalculator.Support;
using UnityEngine;

namespace SuMa.Ksp.BatteryCalculator.Interface
{
    /// <summary>
    /// Owns the calculator in one scene: the toolbar button and its colour, the window, and when the numbers
    /// are worked out again. The editor and flight differ only in where the parts, the crew and the default
    /// target come from, which the two subclasses supply.
    ///
    /// The calculation runs in Update, never in OnGUI: IMGUI lays a window out on the Layout event and reads the
    /// layout back on Repaint, so rows that appeared between the two would throw. It runs when something
    /// changed and otherwise once per refresh interval, whether the window is open or not, because the button
    /// shows the verdict too.
    /// </summary>
    internal abstract class BatteryCalculatorController : MonoBehaviour
    {
        private const string ButtonId = "BatteryCalculator";
        private const string IconPath = "zzz_suma/SuMa.Ksp.BatteryCalculator/Icons/Battery";
        private const string ModId = "SuMa_BatteryCalculator";
        private const string TypingLockId = "SuMaBatteryCalculatorTyping";

        private static bool _includeParentEclipses = BatteryCalculatorSettings.Current.IncludeParentEclipses;
        private static HandlerRegistry _registry;

        private ToolbarButtonDefinition _button;
        private EnergyBudget _budget;
        private List<EnergyEntry> _entries = new List<EnergyEntry>();
        private bool _isDirty = true;
        private bool _isKeyboardLocked;
        private bool _isWindowOpen;
        private float _nextRefreshTime;
        private bool _refreshFailureReported;
        private PlanningTarget _target;
        private BatteryCalculatorWindow _window;

        protected abstract bool IsFlight { get; }

        protected abstract double RefreshInterval { get; }

        protected abstract ApplicationLauncher.AppScenes Scenes { get; }

        /// <summary>
        /// The configured body and altitude for a craft nobody has planned yet: Kerbin at 120 km unless the
        /// settings say otherwise, and the home body when the configured one does not exist. An altitude the
        /// body cannot be orbited at is moved to one it can.
        /// </summary>
        protected static PlanningTarget ConfiguredTarget()
        {
            var settings = BatteryCalculatorSettings.Current;
            var altitude = settings.DefaultAltitudeKilometres * 1000.0;
            var body = FindBody(settings.DefaultBodyName) ?? FlightGlobals.GetHomeBody() ?? DarknessProfile.Sun();
            if (body == null)
            {
                return new PlanningTarget(settings.DefaultBodyName, altitude, false);
            }

            return new PlanningTarget(body.bodyName, DarknessProfile.SuggestedOrbitAltitude(body, altitude), false);
        }

        protected abstract int CrewCount();

        /// <summary>
        /// The parts to plan for, or null when there is nothing to plan: no craft yet, a kerbal on EVA.
        /// </summary>
        protected abstract IList<Part> CurrentParts();

        protected abstract PlanningTarget DefaultTarget();

        /// <summary>
        /// The craft's or vessel's persistentId, which its target and vessel-wide choices are saved under.
        /// </summary>
        protected abstract uint VesselId();

        protected void MarkDirty()
        {
            _isDirty = true;
        }

        protected virtual void OnDestroy()
        {
            SetKeyboardLock(false);
            ToolbarButtonRegistry.Remove(_button);
        }

        protected void OnGUI()
        {
            if (_isWindowOpen && _window != null)
            {
                _window.Draw();
            }
        }

        protected virtual void Start()
        {
            if (_registry == null)
            {
                _registry = HandlerRegistry.Create(BatteryCalculatorSettings.Current);
            }

            _window = new BatteryCalculatorWindow(IsFlight)
            {
                OnClose = CloseWindow,
                OnEntriesToggled = ToggleEntries,
                OnIncludeParentEclipsesChanged = SetIncludeParentEclipses,
                OnReset = ResetChoices,
                OnTargetChanged = SetTarget,
                OnUseCurrentOrbit = UseCurrentOrbit
            };

            _button = new ToolbarButtonDefinition(ModId, ButtonId, "Battery Calculator", "Battery Calculator", IconPath + "Grey38", IconPath + "Grey24", Scenes);
            _button.OnTrue = OpenWindow;
            _button.OnFalse = CloseWindow;
            ToolbarButtonRegistry.Add(_button);
        }

        protected void Update()
        {
            SetKeyboardLock(_isWindowOpen && _window != null && _window.IsTyping);

            var now = Time.realtimeSinceStartup;
            if (!_isDirty && now < _nextRefreshTime)
            {
                return;
            }

            _isDirty = false;
            _nextRefreshTime = now + (float)Math.Max(0.1, RefreshInterval);

            try
            {
                Refresh();
            }
            catch (Exception exception)
            {
                // Once per scene: this runs every second, and a failure is unlikely to fix itself.
                if (!_refreshFailureReported)
                {
                    _refreshFailureReported = true;
                    Log.Error("Working out the vessel's power failed: " + exception);
                }
            }
        }

        private static CelestialBody FindBody(string bodyName)
        {
            if (string.IsNullOrEmpty(bodyName) || FlightGlobals.Bodies == null)
            {
                return null;
            }

            foreach (var body in FlightGlobals.Bodies)
            {
                if (body.bodyName == bodyName)
                {
                    return body;
                }
            }

            return null;
        }

        private static PlanningTarget Validated(PlanningTarget target)
        {
            return target != null && FindBody(target.BodyName) != null ? target : null;
        }

        private void CloseWindow()
        {
            _isWindowOpen = false;
            _window.ReleaseFocus();

            // SetState does not raise the button's own callbacks, so closing from the window cannot bounce back
            // through OnFalse and round again.
            ToolbarButtonRegistry.SetState(_button, false);
        }

        private double MeasureCapacity(IList<Part> parts, out double stored)
        {
            VesselScanner.MeasureStorage(parts, out var capacity, out stored);
            if (IsFlight)
            {
                DynamicBatteryStorageCorrection.Apply(parts, ref capacity, ref stored);
            }

            return capacity;
        }

        private void OpenWindow()
        {
            _isWindowOpen = true;
            _isDirty = true;
        }

        private void Recalculate()
        {
            _budget = EnergyBudget.Calculate(_entries, _budget != null ? _budget.Capacity : 0.0, _budget != null ? _budget.Stored : 0.0, _budget != null ? _budget.Profile : null);
            ShowResult();
        }

        private void Refresh()
        {
            var parts = CurrentParts();
            if (parts == null || parts.Count == 0)
            {
                _entries = new List<EnergyEntry>();
                _budget = null;
                _target = null;
                ShowResult();
                return;
            }

            var vesselId = VesselId();
            _target = BatteryCalculatorScenario.TryGetTarget(vesselId, out var saved) && Validated(saved) != null ? saved : DefaultTarget();

            var profile = DarknessProfile.Calculate(FindBody(_target.BodyName), _target.IsLanded, _target.Altitude, _includeParentEclipses);
            var solar = SolarConditions.Calculate(profile);
            var minimumFuelDuration = BatteryCalculatorSettings.Current.FueledProducerMinimumYears * KSPUtil.dateTimeFormatter.Year;
            var context = new EnergyContext(parts, IsFlight, profile, solar, CrewCount(), vesselId, minimumFuelDuration);

            _entries = VesselScanner.Scan(context, _registry);
            var capacity = MeasureCapacity(parts, out var stored);
            _budget = EnergyBudget.Calculate(_entries, capacity, stored, profile);
            ShowResult();
        }

        private void ResetChoices()
        {
            var parts = CurrentParts();
            if (parts != null)
            {
                var ids = new List<uint> { VesselId() };
                foreach (var part in parts)
                {
                    ids.Add(part.persistentId);
                }

                BatteryCalculatorScenario.ClearChoices(ids);
            }

            BatteryCalculatorScenario.ClearTarget(VesselId());
            _isDirty = true;
        }

        private void SetIncludeParentEclipses(bool include)
        {
            _includeParentEclipses = include;
            _isDirty = true;
        }

        /// <summary>
        /// Holds the game's keyboard controls while the altitude field has focus, so typing "120" does not also
        /// switch the editor's gizmos. Released as soon as the field loses focus, the window closes or the scene
        /// ends, so it can never outlive the typing.
        /// </summary>
        private void SetKeyboardLock(bool locked)
        {
            if (locked == _isKeyboardLocked)
            {
                return;
            }

            _isKeyboardLocked = locked;
            if (locked)
            {
                InputLockManager.SetControlLock(ControlTypes.KEYBOARDINPUT, TypingLockId);
            }
            else
            {
                InputLockManager.RemoveControlLock(TypingLockId);
            }
        }

        private void SetTarget(PlanningTarget target)
        {
            if (target == null)
            {
                return;
            }

            BatteryCalculatorScenario.SetTarget(VesselId(), target);
            _isDirty = true;
        }

        private void ShowResult()
        {
            _window.Show(_entries, _budget, _target, _includeParentEclipses);

            string colour;
            if (_budget == null || !_budget.IsCalculable)
            {
                colour = "Grey";
            }
            else
            {
                colour = _budget.IsSufficient ? "Green" : "Red";
            }

            ToolbarButtonRegistry.SetTexture(_button, IconPath + colour + "38", IconPath + colour + "24");
        }

        private void ToggleEntries(IList<EnergyEntry> entries, bool included)
        {
            foreach (var entry in entries)
            {
                entry.IsIncluded = included;
                BatteryCalculatorScenario.SetChoice(entry.PersistenceId, entry.Key, included, entry.IsIncludedByDefault);
            }

            Recalculate();
        }

        private void UseCurrentOrbit()
        {
            BatteryCalculatorScenario.ClearTarget(VesselId());
            _isDirty = true;
        }
    }
}
