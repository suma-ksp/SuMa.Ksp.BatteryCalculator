using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClickThroughFix;
using SuMa.Ksp.BatteryCalculator.Calculation;
using SuMa.Ksp.BatteryCalculator.Persistence;
using SuMa.Ksp.BatteryCalculator.Settings;
using SuMa.Ksp.BatteryCalculator.Support;
using UnityEngine;

namespace SuMa.Ksp.BatteryCalculator.Interface
{
    /// <summary>
    /// The calculator's window: the body and orbit on top, because choosing them is what the window is mostly
    /// used for, then the verdict, then the producer and consumer lists folded away until wanted.
    ///
    /// Drawn in Unity's own IMGUI skin rather than HighLogic.Skin, so it reads as a tool window like MechJeb's
    /// and KerbalEngineer's. The skin is set and restored around the draw because it is global state another
    /// mod's OnGUI may have left pointing somewhere else. ClickThroughBlocker keeps a click on the window from
    /// also landing on the craft behind it.
    ///
    /// Identical entries on identical parts - eight of the same solar panel - are shown as one row with a count,
    /// which can be unfolded to tick or untick the parts one by one. The window never recalculates anything
    /// itself; it reports what the player changed and shows what it is given.
    ///
    /// Every change a click or a keystroke makes waits in <see cref="_afterDraw" /> until the window has been
    /// drawn. IMGUI draws each event against the layout it measured on the Layout event, so opening a list or
    /// showing a warning halfway through an event would ask for controls that layout does not have.
    ///
    /// The altitude field lets go of the keyboard on Enter or Escape and on a click anywhere else, because
    /// the game's own keys stay locked for as long as it has it.
    ///
    /// The producer and consumer lists share one scroll area, which is as tall as what it holds, measured
    /// rather than guessed because a long part name wraps onto a second line, and scrolls only once the
    /// window would grow past its maximum height. The grip in the corner sets the width and that maximum,
    /// which never lets the window get taller than the screen.
    /// </summary>
    internal sealed class BatteryCalculatorWindow
    {
        private const string AltitudeControlName = "SuMaBatteryCalculatorAltitude";
        private const float CountColumnWidth = 44f;
        private const float DefaultMaximumHeight = 860f;
        private const float DefaultWindowWidth = 600f;
        private const float GripSize = 16f;
        private const float LabelColumnWidth = 150f;
        private const float MinimumListHeight = 60f;
        private const float MinimumWindowHeight = 240f;
        private const float MinimumWindowWidth = 480f;
        private const float RateColumnWidth = 96f;
        private const float ResultLabelWidth = 150f;
        private const float ScreenMargin = 20f;

        /// <summary>
        /// Room for the scroll view's own border, so a list that fits exactly does not get a scrollbar.
        /// </summary>
        private const float ScrollSlack = 6f;

        private const float TooltipHeight = 34f;
        private const int WindowId = 0x5A6D0B01;

        private static readonly HashSet<string> ExpandedGroups = new HashSet<string>(StringComparer.Ordinal);

        private static bool _consumersOpen;
        private static float _maximumHeight = DefaultMaximumHeight;
        private static bool _producersOpen;
        private static Rect _windowRect = new Rect(320f, 100f, DefaultWindowWidth, 200f);

        private readonly List<Action> _afterDraw = new List<Action>();
        private readonly bool _isFlight;

        private Rect _altitudeFieldRect;
        private string _altitudeText = string.Empty;

        /// <summary>
        /// What the window holds below the list area, the tooltip line and the border, measured.
        /// </summary>
        private float _belowListHeight = 50f;

        private List<BodyRow> _bodyRows;
        private Vector2 _bodyScroll;
        private bool _bodySelectorOpen;
        private EnergyBudget _budget;
        private List<EntryGroup> _consumerGroups = new List<EntryGroup>();
        private GUIStyle _goodStyle;
        private GUIStyle _gripStyle;
        private GUIStyle _headingStyle;
        private bool _includeParentEclipses;
        private bool _isResizing;
        private float _listBottom;
        private float _listContentHeight;
        private Vector2 _listScroll;
        private float _listTop;
        private float _listViewportHeight;
        private List<EntryGroup> _producerGroups = new List<EntryGroup>();
        private GUIStyle _problemStyle;

        /// <summary>
        /// Where in the grip the player took hold of it, as the distance to the window's bottom right corner.
        /// </summary>
        private Vector2 _resizeGrab;

        private GUIStyle _rightStyle;
        private string _shownTargetText;
        private GUIStyle _smallStyle;
        private PlanningTarget _target;
        private GUIStyle _warningStyle;

        public BatteryCalculatorWindow(bool isFlight)
        {
            _isFlight = isFlight;
        }

        /// <summary>
        /// Whether the altitude field has the keyboard, so the controller can keep the editor's and the flight
        /// scene's hotkeys from acting on the digits typed into it.
        /// </summary>
        public bool IsTyping { get; private set; }

        public Action OnClose { get; set; }

        /// <summary>
        /// The player ticked or unticked these entries, all to the same state.
        /// </summary>
        public Action<IList<EnergyEntry>, bool> OnEntriesToggled { get; set; }

        public Action<bool> OnIncludeParentEclipsesChanged { get; set; }

        public Action OnReset { get; set; }

        public Action<PlanningTarget> OnTargetChanged { get; set; }

        public Action OnUseCurrentOrbit { get; set; }

        public void Draw()
        {
            var current = Event.current;
            if (IsTyping && (IsFinishingKey(current) || (current.type == EventType.MouseDown && !_windowRect.Contains(current.mousePosition))))
            {
                ReleaseFocus();
            }

            if (_isResizing)
            {
                Resize(current);
            }

            var previousSkin = GUI.skin;
            GUI.skin = null;

            try
            {
                _windowRect = ClickThruBlocker.GUILayoutWindow(WindowId, _windowRect, DrawContents, "Battery Calculator", GUILayout.Width(_windowRect.width));
            }
            finally
            {
                GUI.skin = previousSkin;
            }

            KeepOnScreen();

            var pending = _afterDraw.ToArray();
            _afterDraw.Clear();
            foreach (var action in pending)
            {
                action();
            }
        }

        /// <summary>
        /// Takes the keyboard away from the altitude field, if it has it.
        /// </summary>
        public void ReleaseFocus()
        {
            if (IsTyping)
            {
                GUIUtility.keyboardControl = 0;
            }

            IsTyping = false;
        }

        public void Show(List<EnergyEntry> entries, EnergyBudget budget, PlanningTarget target, bool includeParentEclipses)
        {
            entries = entries ?? new List<EnergyEntry>();
            _producerGroups = Groups(entries, EnergyRole.Producer);
            _consumerGroups = Groups(entries, EnergyRole.Consumer);
            _budget = budget;
            _target = target;
            _includeParentEclipses = includeParentEclipses;

            // Keep the altitude field in step with a target that changed from outside - a reset, a vessel
            // switch - without fighting the player while they type.
            var targetText = target != null ? (target.Altitude / 1000.0).ToString("0.###", CultureInfo.InvariantCulture) : string.Empty;
            if (targetText != _shownTargetText)
            {
                _shownTargetText = targetText;
                if (!TryParseAltitude(_altitudeText, out var typed) || target == null || Math.Abs(typed * 1000.0 - target.Altitude) > 0.5)
                {
                    _altitudeText = targetText;
                }
            }
        }

        private static void AddBodyRows(List<BodyRow> rows, CelestialBody body, int depth)
        {
            rows.Add(new BodyRow(body, depth));
            var children = FlightGlobals.Bodies.Where(child => child != body && child.referenceBody == body && child.orbit != null).OrderBy(child => child.orbit.semiMajorAxis);
            foreach (var child in children)
            {
                AddBodyRows(rows, child, depth + 1);
            }
        }

        private static string BehaviourText(EnergyBehaviour behaviour)
        {
            switch (behaviour)
            {
                case EnergyBehaviour.Constant:
                    return "Runs all the time.";
                case EnergyBehaviour.Continuous:
                    return "Keeps running once started; counted in the editor, and in flight while it runs.";
                case EnergyBehaviour.Switchable:
                    return "Runs once switched on; counted in flight while it is on.";
                case EnergyBehaviour.OnDemand:
                    return "Only while in use; not counted unless ticked.";
                case EnergyBehaviour.Fueled:
                    return "Burns fuel; counted only if the fuel lasts.";
                default:
                    return "Needs sunlight; nothing in the dark.";
            }
        }

        private static List<BodyRow> BuildBodyRows()
        {
            var rows = new List<BodyRow>();
            var sun = DarknessProfile.Sun();
            if (sun == null)
            {
                return rows;
            }

            AddBodyRows(rows, sun, 0);
            return rows;
        }

        private static CelestialBody FindBody(string bodyName)
        {
            return FlightGlobals.Bodies != null ? FlightGlobals.Bodies.FirstOrDefault(body => body.bodyName == bodyName) : null;
        }

        private static List<EntryGroup> Groups(IEnumerable<EnergyEntry> entries, EnergyRole role)
        {
            return entries.Where(entry => entry.Role == role).GroupBy(entry => entry.GroupKey).Select(grouping => new EntryGroup(grouping.Key, grouping.ToList())).OrderByDescending(group => group.TotalRate).ToList();
        }

        private static bool IsFinishingKey(Event current)
        {
            return current.type == EventType.KeyDown && (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter || current.keyCode == KeyCode.Escape);
        }

        /// <summary>
        /// Moves the window back inside the screen when dragging, resizing or a taller list pushed part of it
        /// off.
        /// </summary>
        private static void KeepOnScreen()
        {
            _windowRect.x = Mathf.Max(0f, Mathf.Min(_windowRect.x, Screen.width - _windowRect.width));
            _windowRect.y = Mathf.Max(0f, Mathf.Min(_windowRect.y, Screen.height - _windowRect.height));
        }

        /// <summary>
        /// The tallest the window may get: the height the player chose with the grip, but never more than the
        /// screen has room for.
        /// </summary>
        private static float MaximumWindowHeight()
        {
            return Mathf.Max(MinimumWindowHeight, Mathf.Min(_maximumHeight, Screen.height - 2f * ScreenMargin));
        }

        private static string Name(CelestialBody body)
        {
            return body != null ? body.bodyDisplayName.LocalizeRemoveGender() : "?";
        }

        /// <summary>
        /// An orbit altitude that exists at the body, for a target that is not landed any more or has moved to
        /// another body: the one it had if the body can be orbited there, otherwise one it can, starting from
        /// the configured altitude when there was none.
        /// </summary>
        private static double OrbitAltitude(CelestialBody body, double altitude)
        {
            var preferred = altitude > 0.0 ? altitude : BatteryCalculatorSettings.Current.DefaultAltitudeKilometres * 1000.0;
            return DarknessProfile.SuggestedOrbitAltitude(body, preferred);
        }

        /// <summary>
        /// A layout window grows to fit its contents but never shrinks back on its own, so after a list folds
        /// away its height is let go and measured afresh.
        /// </summary>
        private static void Refit()
        {
            _windowRect.height = 0f;
        }

        private static bool TryParseAltitude(string text, out double kilometres)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out kilometres) && kilometres >= 0.0;
        }

        private void DrawBodySelector()
        {
            if (_bodyRows == null)
            {
                _bodyRows = BuildBodyRows();
            }

            _bodyScroll = GUILayout.BeginScrollView(_bodyScroll, GUILayout.Height(180f));
            foreach (var row in _bodyRows)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(row.Depth * 16f);
                var isSelected = _target != null && row.Body.bodyName == _target.BodyName;
                if (GUILayout.Toggle(isSelected, Name(row.Body), GUI.skin.button) && !isSelected)
                {
                    var landed = _target != null && _target.IsLanded && row.Body.hasSolidSurface;
                    var altitude = _target != null ? _target.Altitude : 0.0;
                    var target = new PlanningTarget(row.Body.bodyName, landed ? altitude : OrbitAltitude(row.Body, altitude), landed);
                    _afterDraw.Add(() =>
                    {
                        OnTargetChanged?.Invoke(target);
                        _bodySelectorOpen = false;
                        Refit();
                    });
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        private void DrawContents(int windowId)
        {
            EnsureStyles();

            if (IsTyping && Event.current.type == EventType.MouseDown && !_altitudeFieldRect.Contains(Event.current.mousePosition))
            {
                ReleaseFocus();
            }

            if (GUI.Button(new Rect(_windowRect.width - 20f, 2f, 18f, 16f), "x"))
            {
                _afterDraw.Add(() => OnClose?.Invoke());
            }

            if (_target == null)
            {
                IsTyping = false;
                GUILayout.Space(8f);
                GUILayout.Label(_isFlight ? "Nothing to calculate for the active vessel." : "Build a craft to see its power budget.");
                GUILayout.Space(8f);
                GUI.DragWindow();
                return;
            }

            DrawTarget();
            DrawDarkness();
            DrawResult();
            DrawLists();

            // A fixed height, so the window does not jump each time the mouse moves onto a row with a longer
            // tooltip.
            GUILayout.BeginHorizontal();
            GUILayout.Label(GUI.tooltip ?? string.Empty, _smallStyle, GUILayout.Height(TooltipHeight));
            DrawResizeGrip();
            GUILayout.EndHorizontal();

            if (Event.current.type == EventType.Repaint)
            {
                if (_producersOpen || _consumersOpen)
                {
                    _belowListHeight = GUILayoutUtility.GetLastRect().yMax - _listBottom + GUI.skin.window.padding.bottom;
                }

                IsTyping = GUI.GetNameOfFocusedControl() == AltitudeControlName;
            }

            GUI.DragWindow();
        }

        private void DrawDarkness()
        {
            var profile = _budget != null ? _budget.Profile : null;
            if (profile == null)
            {
                return;
            }

            GUILayout.Space(6f);
            if (!string.IsNullOrEmpty(profile.Warning))
            {
                GUILayout.Label(profile.Warning, _warningStyle);
            }

            if (profile.Body == DarknessProfile.Sun())
            {
                ResultRow("Darkness", "none: nothing shades a vessel orbiting the Sun");
                return;
            }

            if (profile.IsLanded)
            {
                ResultRow("Night", Format.Time(profile.DarkTime) + " of every " + Format.Time(profile.CycleTime) + " day");
            }
            else
            {
                ResultRow("Dark side", Format.Time(profile.DarkTime) + " of every " + Format.Time(profile.CycleTime) + " orbit");
            }

            if (profile.ParentBody != null && profile.ParentEclipseTime > 0.0 && !profile.IsLanded)
            {
                GUILayout.BeginHorizontal();
                var include = GUILayout.Toggle(_includeParentEclipses, new GUIContent(" Behind " + Name(profile.ParentBody), "Once every orbit of " + Name(profile.Body) + ", it passes through " + Name(profile.ParentBody) + "'s shadow, and so does the vessel. Right after its own dark side, that is the longest darkness it meets."), GUILayout.Width(ResultLabelWidth));
                GUILayout.Label(Format.Time(profile.ParentEclipseTime) + " once per " + Name(profile.Body) + " orbit");
                GUILayout.EndHorizontal();
                if (include != _includeParentEclipses)
                {
                    _afterDraw.Add(() => OnIncludeParentEclipsesChanged?.Invoke(include));
                }

                if (_includeParentEclipses)
                {
                    ResultRow("Longest darkness", Format.Time(profile.WorstDarkTime));
                }
            }
        }

        private void DrawEntryList(IList<EntryGroup> groups)
        {
            if (groups.Count == 0)
            {
                GUILayout.Label("None.");
                return;
            }

            foreach (var group in groups)
            {
                DrawGroup(group);
            }
        }

        private void DrawGroup(EntryGroup group)
        {
            var first = group.Entries[0];
            var included = group.IncludedCount;
            var tooltip = BehaviourText(first.Behaviour) + (string.IsNullOrEmpty(first.Detail) ? string.Empty : " " + first.Detail);

            GUILayout.BeginHorizontal();
            var wanted = GUILayout.Toggle(included == group.Entries.Count, new GUIContent(string.Empty, tooltip), GUILayout.Width(18f));
            if (wanted != (included == group.Entries.Count))
            {
                _afterDraw.Add(() => OnEntriesToggled?.Invoke(group.Entries, wanted));
            }

            var style = included > 0 ? GUI.skin.label : _smallStyle;
            GUILayout.Label(new GUIContent(first.Title, tooltip), style);
            GUILayout.Label(new GUIContent(first.Label, tooltip), style, GUILayout.Width(LabelColumnWidth));

            var countText = group.Entries.Count > 1 ? (included == group.Entries.Count || included == 0 ? "×" + group.Entries.Count : included + "/" + group.Entries.Count) : string.Empty;
            if (group.Entries.Count > 1)
            {
                if (GUILayout.Button(new GUIContent((IsExpanded(group) ? "▾ " : "▸ ") + countText, "Show each part on its own."), GUILayout.Width(CountColumnWidth)))
                {
                    _afterDraw.Add(() =>
                    {
                        if (!ExpandedGroups.Remove(group.Key))
                        {
                            ExpandedGroups.Add(group.Key);
                        }

                        Refit();
                    });
                }
            }
            else
            {
                GUILayout.Space(CountColumnWidth + 4f);
            }

            GUILayout.Label(new GUIContent(Format.Rate(group.TotalRate), tooltip), _rightStyle, GUILayout.Width(RateColumnWidth));
            GUILayout.EndHorizontal();

            if (!IsExpanded(group))
            {
                return;
            }

            foreach (var entry in group.Entries)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(22f);
                var entryWanted = GUILayout.Toggle(entry.IsIncluded, new GUIContent(" " + first.Title, tooltip));
                if (entryWanted != entry.IsIncluded)
                {
                    _afterDraw.Add(() => OnEntriesToggled?.Invoke(new[] { entry }, entryWanted));
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label(Format.Rate(entry.Rate), _rightStyle, GUILayout.Width(RateColumnWidth));
                GUILayout.EndHorizontal();
            }
        }

        private void DrawLists()
        {
            var producers = _producerGroups;
            var consumers = _consumerGroups;

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            var producersOpen = GUILayout.Toggle(_producersOpen, (_producersOpen ? "▾ " : "▸ ") + "Producers (" + producers.Count + ")", GUI.skin.button);
            var consumersOpen = GUILayout.Toggle(_consumersOpen, (_consumersOpen ? "▾ " : "▸ ") + "Consumers (" + consumers.Count + ")", GUI.skin.button);
            if (producersOpen != _producersOpen || consumersOpen != _consumersOpen)
            {
                _afterDraw.Add(() =>
                {
                    _producersOpen = producersOpen;
                    _consumersOpen = consumersOpen;
                    Refit();
                });
            }

            if (_isFlight && GUILayout.Button(new GUIContent("Current orbit", "Plan for where the vessel is now."), GUILayout.Width(100f)))
            {
                _afterDraw.Add(() => OnUseCurrentOrbit?.Invoke());
            }

            if (GUILayout.Button(new GUIContent("Reset", "Forget every tick changed by hand and the chosen body and orbit."), GUILayout.Width(60f)))
            {
                _afterDraw.Add(() => OnReset?.Invoke());
            }

            GUILayout.EndHorizontal();

            if (!_producersOpen && !_consumersOpen)
            {
                return;
            }

            // Worked out on the Layout event only, from what the last Repaint measured, so every event of a
            // frame lays the list out the same.
            if (Event.current.type == EventType.Layout)
            {
                var viewportHeight = ListViewportHeight();
                if (viewportHeight < _listViewportHeight - 0.5f)
                {
                    _afterDraw.Add(Refit);
                }

                _listViewportHeight = viewportHeight;
            }

            _listScroll = GUILayout.BeginScrollView(_listScroll, GUILayout.Height(_listViewportHeight));
            GUILayout.BeginVertical();
            if (_producersOpen)
            {
                GUILayout.Label("Producers", _headingStyle);
                DrawEntryList(producers);
            }

            if (_consumersOpen)
            {
                GUILayout.Label("Consumers", _headingStyle);
                DrawEntryList(consumers);
            }

            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint)
            {
                _listContentHeight = GUILayoutUtility.GetLastRect().height;
            }

            GUILayout.EndScrollView();
            if (Event.current.type == EventType.Repaint)
            {
                var viewport = GUILayoutUtility.GetLastRect();
                _listTop = viewport.y;
                _listBottom = viewport.yMax;
            }
        }

        /// <summary>
        /// The corner grip, at the end of the tooltip line so it is laid out like everything else and its place
        /// never depends on the window's height, which is let go of whenever the window is refitted. Pressing
        /// it only starts the resize; the size follows the mouse in <see cref="Resize" />.
        /// </summary>
        private void DrawResizeGrip()
        {
            GUILayout.Label(new GUIContent("◢", "Drag to resize the window."), _gripStyle, GUILayout.Width(GripSize), GUILayout.Height(TooltipHeight));
            var grip = GUILayoutUtility.GetLastRect();

            var current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0 && grip.Contains(current.mousePosition))
            {
                var padding = GUI.skin.window.padding;
                _isResizing = true;
                _resizeGrab = new Vector2(grip.xMax + padding.right - current.mousePosition.x, grip.yMax + padding.bottom - current.mousePosition.y);
                current.Use();
            }
        }

        private void DrawResult()
        {
            var budget = _budget;
            if (budget == null || !budget.IsCalculable)
            {
                return;
            }

            GUILayout.Space(6f);
            ResultRow("Consumption", Format.Rate(budget.Consumption));
            ResultRow("Production", Format.Rate(budget.TotalProduction) + (budget.SolarProduction > 0.0 ? " (solar " + Format.Rate(budget.SolarProduction) + ")" : string.Empty));
            ResultRow("Drain in the dark", budget.DarkDeficit > 0.0 ? Format.Rate(budget.DarkDeficit) : "none, covered without sunlight");
            ResultRow("Battery needed", Format.Energy(budget.RequiredCapacity));
            ResultRow("Battery aboard", Format.Energy(budget.Capacity) + (_isFlight ? " (" + Format.Energy(budget.Stored) + " charged)" : string.Empty));

            if (budget.HasEnoughCapacity)
            {
                ResultRow("Batteries", Format.Energy(budget.Margin) + " to spare", _goodStyle);
            }
            else
            {
                ResultRow("Batteries", Format.Energy(-budget.Margin) + " short", _problemStyle);
            }

            if (budget.DarkDeficit <= 0.0)
            {
                ResultRow("Recharging", "not needed", _goodStyle);
            }
            else if (budget.SunlitSurplus <= 0.0)
            {
                ResultRow("Recharging", "impossible: " + Format.Rate(-budget.SunlitSurplus) + " short even in sunlight", _problemStyle);
            }
            else if (!budget.Recharges)
            {
                var profile = budget.Profile;
                ResultRow("Recharging", "sunlight puts back " + Format.Energy(budget.SunlitSurplus * profile.SunlitTime) + " of the " + Format.Energy(budget.DarkDeficit * profile.DarkTime) + " each dark pass takes", _problemStyle);
            }
            else
            {
                ResultRow("Recharging", "full again after " + Format.Time(budget.RechargeTime) + " of sunlight", _goodStyle);
            }
        }

        private void DrawTarget()
        {
            var body = FindBody(_target.BodyName);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Body", GUILayout.Width(50f));
            var bodySelectorOpen = GUILayout.Toggle(_bodySelectorOpen, Name(body) + (_bodySelectorOpen ? "  ▴" : "  ▾"), GUI.skin.button, GUILayout.Width(140f));
            if (bodySelectorOpen != _bodySelectorOpen)
            {
                _afterDraw.Add(() =>
                {
                    _bodySelectorOpen = bodySelectorOpen;
                    Refit();
                });
            }

            GUILayout.Space(12f);

            var canLand = body != null && body.hasSolidSurface;
            GUI.enabled = !_target.IsLanded;
            GUILayout.Label("Altitude", GUILayout.Width(55f));
            GUI.SetNextControlName(AltitudeControlName);
            var typed = GUILayout.TextField(_altitudeText, GUILayout.Width(80f));
            if (Event.current.type == EventType.Repaint)
            {
                _altitudeFieldRect = GUILayoutUtility.GetLastRect();
            }

            GUILayout.Label("km", GUILayout.Width(24f));
            GUI.enabled = canLand;
            var landed = GUILayout.Toggle(_target.IsLanded && canLand, new GUIContent(" Landed", "Plan for a base on the surface: the darkness is the night."));
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            var target = _target;
            if (typed != _altitudeText)
            {
                _afterDraw.Add(() =>
                {
                    _altitudeText = typed;
                    if (TryParseAltitude(typed, out var kilometres) && Math.Abs(kilometres * 1000.0 - target.Altitude) > 0.5)
                    {
                        OnTargetChanged?.Invoke(new PlanningTarget(target.BodyName, kilometres * 1000.0, false));
                    }
                });
            }

            if (landed != _target.IsLanded)
            {
                var altitude = landed ? target.Altitude : OrbitAltitude(body, target.Altitude);
                _afterDraw.Add(() => OnTargetChanged?.Invoke(new PlanningTarget(target.BodyName, altitude, landed)));
            }

            if (!_target.IsLanded && !TryParseAltitude(_altitudeText, out _))
            {
                GUILayout.Label("The altitude has to be a number of kilometres.", _warningStyle);
            }

            if (_bodySelectorOpen)
            {
                DrawBodySelector();
            }
        }

        private void EnsureStyles()
        {
            if (_headingStyle != null)
            {
                return;
            }

            _headingStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _rightStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight };
            _goodStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _goodStyle.normal.textColor = new Color(0.4f, 0.9f, 0.4f);
            _problemStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = true };
            _problemStyle.normal.textColor = new Color(1f, 0.4f, 0.35f);
            _warningStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            _warningStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
            _smallStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = Math.Max(10, GUI.skin.label.fontSize - 1) };
            _smallStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            _gripStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, wordWrap = false };
            _gripStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
        }

        private bool IsExpanded(EntryGroup group)
        {
            return group.Entries.Count > 1 && ExpandedGroups.Contains(group.Key);
        }

        /// <summary>
        /// As tall as the lists are, up to what the maximum window height leaves below the verdict.
        /// </summary>
        private float ListViewportHeight()
        {
            var available = Mathf.Max(MinimumListHeight, MaximumWindowHeight() - _listTop - _belowListHeight);
            return Mathf.Min(_listContentHeight + ScrollSlack, available);
        }

        /// <summary>
        /// Follows the mouse while the grip is held: the width directly, and the maximum height, which the
        /// window grows to when its lists need the room. Read from Input rather than from IMGUI's events, which
        /// stop reaching the window once the mouse leaves it; applied on Layout only, so a frame is laid out
        /// and drawn at the same size.
        /// </summary>
        private void Resize(Event current)
        {
            if (!Input.GetMouseButton(0))
            {
                _isResizing = false;
                return;
            }

            if (current.type != EventType.Layout)
            {
                return;
            }

            var mouseX = Input.mousePosition.x;
            var mouseY = Screen.height - Input.mousePosition.y;
            _windowRect.width = Mathf.Max(MinimumWindowWidth, Mathf.Min(mouseX - _windowRect.x + _resizeGrab.x, Screen.width - _windowRect.x));
            _maximumHeight = Mathf.Max(MinimumWindowHeight, Mathf.Min(mouseY - _windowRect.y + _resizeGrab.y, Screen.height - _windowRect.y));
            Refit();
        }

        private void ResultRow(string label, string value, GUIStyle valueStyle = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(ResultLabelWidth));
            GUILayout.Label(value, valueStyle ?? GUI.skin.label);
            GUILayout.EndHorizontal();
        }

        private readonly struct BodyRow
        {
            public BodyRow(CelestialBody body, int depth)
            {
                Body = body;
                Depth = depth;
            }

            public CelestialBody Body { get; }

            public int Depth { get; }
        }

        /// <summary>
        /// One row of a list: the same entry on parts of the same kind, with what it adds up to worked out once
        /// per result rather than on every one of IMGUI's events.
        /// </summary>
        private sealed class EntryGroup
        {
            public EntryGroup(string key, List<EnergyEntry> entries)
            {
                Key = key;
                Entries = entries;
                IncludedCount = entries.Count(entry => entry.IsIncluded);
                TotalRate = entries.Sum(entry => entry.Rate);
            }

            public List<EnergyEntry> Entries { get; }

            public int IncludedCount { get; }

            public string Key { get; }

            public double TotalRate { get; }
        }
    }
}
