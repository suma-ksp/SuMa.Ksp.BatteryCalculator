# SuMa.Ksp.BatteryCalculator

A Kerbal Space Program 1.12 add-on that answers one question for the craft in the VAB/SPH, or the vessel in
flight: **does it have enough battery and enough production to get through the dark side of this orbit,
without anybody touching it?**

Requires [ClickThroughBlocker](https://github.com/linuxgurugamer/ClickThroughBlocker)
(`GameData\000_ClickThroughBlocker`), the one mod referenced directly. Everything else is optional:
[ToolbarControl](https://github.com/linuxgurugamer/ToolbarControl) adds Blizzy's toolbar, and every other mod
it supports is found by name at runtime and silently skipped when it is not installed. ModuleManager is not
needed. It replaces AmpYear for this purpose; see [Why not AmpYear](#why-not-ampyear).

## Using it

The toolbar button is a battery: **green** when the batteries and production are enough for the selected
body and orbit, **red** when they are not, grey while there is nothing to calculate. It works the same on the
stock application launcher and, with ToolbarControl installed, on Blizzy's toolbar.

The window puts the choice it is mostly used for on top:

- **Body** and **Altitude**, a circular orbit in kilometres, or **Landed** for a base, where the darkness is
  the night. A new craft starts at Kerbin, 120 km. In flight the vessel starts from where it is, or like a
  new craft while it is still on the launch pad. Picking another body, or unticking Landed, moves an altitude
  the body cannot be orbited at - inside its atmosphere, beyond its sphere of influence - to one it can; an
  impossible target shows a warning and a grey button instead of a verdict.
- For a moon, **Behind *planet***: the pass through the planet's shadow that comes once per orbit of the moon,
  on by default. The Mun spends about 37 minutes behind Kerbin, Laythe about an hour behind Jool. The eclipse
  joins up with the vessel's own dark side at either end, and one that outlasts the sunlit part of an orbit
  bridges two of them: in a 30 km orbit of the Mun that makes an hour of darkness.

Below that, the verdict: consumption, production, how fast the batteries drain in the dark, the battery
needed and aboard, what is to spare or short, and whether the sunlit part of the orbit puts back what the dark
took. **Producers** and **Consumers** open lists of everything found. Identical entries on identical parts are
one row with a count, which unfolds to the parts one by one; each row can be ticked into or out of the
calculation. The lists share one area that grows with them and only scrolls once the window reaches its
maximum height; the grip in the bottom right corner sets the width and that maximum, which never exceeds the
screen. **Reset** forgets the ticks changed by hand and the chosen target. The altitude field lets go of
the keyboard on Enter, Escape or a click elsewhere; while it has it, the game's own keys are held.

### What is counted by default

The default is what happens when nobody touches the vessel.

| Kind | Examples | Counted by default |
|---|---|---|
| Constant | probe cores, RTGs, radiators, cryogenic tank cooling, CommNet Antennas Consumptor's link draw, USI life support per kerbal | yes, unless the module says it is off |
| Continuous | converters, ISRU, drills, labs, MKS and USI modules such as habitats | in the editor; in flight while running |
| Switchable | SCANsat, lights, magnets | in flight while switched on; not in the editor |
| On demand | transmitters sending, reaction wheels, engines, RCS, wheels, robotics, winches | never |
| Fueled | fuel cells, reactors, fuel-burning generators | only if the fuel aboard lasts a year |
| Solar | solar panels | yes; in flight not while retracted or broken |

"A year" is `FueledProducerMinimumYears`, in the game's calendar, which is what separates a reactor from a fuel
cell without either being named. Every row's tooltip says which kind it is and what its rate assumes.

A module no handler knows is still listed from its configured RESOURCE rate, unticked, so nothing that
declares its power disappears silently.

## How it calculates

**Darkness.** Shadows are cylinders, as in KSP, which decides sunlight with a ray cast and has no penumbra.
For a circular orbit of radius r around a body of radius R the dark side lasts `period × asin(R / r) / π`. On
the surface the night is half a solar day, `1 / (1/rotation − 1/solar orbit)`: KSP has no axial tilt, so
every latitude but the poles gets exactly that.

A moon's pass behind its planet is worked out for the moon's actual orbit, on its longest pass, the one with
the apoapsis in the shadow: `2ab/h × (asin(R/b) + eR/b)`, which is the formula above for a circle. With it
included, the longest darkness is the eclipse plus the dark sides it can join, `max(E + D, n × period + D)`
with `n = floor((E + D) / period)`. On the surface the eclipse falls around local noon on the side facing the
planet, so it never lengthens a night and is left out.

**Solar output** follows stock: the panel's RESOURCE rate times `efficiencyMult` and the distance factor.
`flowMult` is left out, because stock only applies it to the number the part window shows. FlightIntegrator
measures the distance to the Sun from the Sun's surface and takes the flux from
`PhysicsGlobals.SolarLuminosity`, which is why a panel at Kerbin makes 1.04 times its rating; panels with a
power curve evaluate it at the distance from the centre. The distance used is the far end of the body's orbit
round the Sun, or its planet's for a moon, so an eccentric planet is judged at its dimmest. Panels are assumed
to face the Sun, which tracking panels do; cylindrical and spherical panels get stock's best case, 1/π and
1/4. On the surface the atmosphere's share comes from stock's own `CelestialBody.GetSolarAtmosphericEffects`,
at sea level, averaged over the day - for panels rated by flux only, since stock does not apply it to a power
curve.

**Rates** are the configured ones: a module's RESOURCE rate, a converter's recipe, a transmitter's
`packetResourceCost / packetInterval`. What flowed in the last physics tick is never read, because it is zero
while the batteries are empty and meaningless right after a vessel loads, when converters catch up on the
time it was away. That keeps the flight readout steady through vessel switches, docking and time warp. In
flight a converter's recipe is scaled by its efficiency bonus and its crew bonus, as stock does - an ISRU
without an engineer draws a twentieth - but not by its heat throttle, which sags while it warms up or catches
up; the editor plans at full rate. DynamicBatteryStorage enlarges one battery while warping at 100× and
above; the enlargement is taken back off using the size the mod saved.

**The verdict.** In the dark only non-solar producers help, so the battery has to cover
`(consumption − non-solar production) × longest darkness`. In sunlight the surplus has to refill what one
ordinary dark pass drained within the sunlit time of the same orbit or day; otherwise the batteries lose a
little more every revolution. The button is green only when both hold.

## Mods it understands

Handlers are chosen by type, not by module name, so a mod's subclass of a stock module is understood without
being listed: CommNet Antennas Consumptor's `ModuleGeneratorAntenna` is a generator, Near Future Exploration's
relay dishes are transmitters, every USI converter and harvester is a stock converter whose swap options fill
in the recipe, CactEye's gyros are reaction wheels. Where more is needed:

| Mod | Module | How |
|---|---|---|
| Near Future Solar | `ModuleCurvedSolarPanel` | its own rate and distance law, best orientation of its cells |
| Universal Storage 2 | `USSolarSwitch` | the selected variant's rate, best orientation of its sun catchers |
| SystemHeat (NFE and USI reactors) | `ModuleSystemHeatFissionReactor` | full output × core integrity, fuel duration from its inputs |
| CryoTanks | `ModuleCryoTank` | its own cooling cost, counted while cooling is on, and still listed when it is switched off in flight |
| DeepFreeze | `DeepFreezer` | charge per frozen kerbal, when the game's DeepFreeze settings ask for power |
| USI Life Support | vessel-wide | `ECAmount` per kerbal, tourists included |
| DynamicBatteryStorage | vessel-wide | the battery it enlarges for fast time warp is counted at its real size |
| SCANsat, DMagic, CactEye, KerbalActuators, AT_Utils, KAS, Konstruction, SystemHeat exchangers, NFE capacitors | various | configuration, below |

### Adding a mod without code

A `BATTERY_CALCULATOR_MODULE` node describes a module whose power is a field and whose state is another. It
only takes effect when the module's class is loaded, and a patch in any mod can add one:

```
BATTERY_CALCULATOR_MODULE
{
	name = WBILightController     // the module class; subclasses match too
	role = Consumer               // Consumer or Producer
	behaviour = Switchable        // Constant, Continuous, Switchable, OnDemand, Fueled or Solar
	rate = ecRequired             // a field or property, or resHandler for the RESOURCE nodes
	active = lightsOn             // a field or property saying the module is on; optional
	label = Lights
}
```

`rateScale`, `rateFactor` and `rateFactorScale` cover modules that store their rate differently, and
`activeValues` lists the values that mean "on" for a state that is not simply true or false. The shipped
`SuMa.Ksp.BatteryCalculator.cfg` has the full list and every setting.

## Settings

`BATTERY_CALCULATOR` in `GameData\zzz_suma\SuMa.Ksp.BatteryCalculator\SuMa.Ksp.BatteryCalculator.cfg`:

| Setting | Default | |
|---|---|---|
| `DefaultBody` | Kerbin | what a new craft is planned for; empty for the home planet |
| `DefaultAltitude` | 120 | kilometres |
| `IncludeParentEclipses` | True | where the moon eclipse checkbox starts |
| `FueledProducerMinimumYears` | 1 | how long fuel has to last for a fueled producer to count |
| `EditorRefreshInterval`, `FlightRefreshInterval` | 1 | seconds between recalculations while nothing changes |

## What it saves

Only what differs from the defaults, and only in the save: a `SCENARIO` node holding the ticks changed by
hand, keyed by the part's `persistentId`, and a chosen target keyed by the craft's or vessel's. Nothing is
added to any part. A launch keeps its parts' ids, so a choice made in the VAB is still there in flight.

A tick sticks: a converter ticked in the VAB, where converters start unticked, is counted in flight whether it
runs or not, and one unticked in flight while it runs is left out whether it runs or not. Ticking a row back
to its default removes the tick. Removing the mod leaves that one node behind, which KSP ignores.

## Why not AmpYear

AmpYear 1.5.9 was the last release, for KSP 1.11. Besides recalculating the whole vessel on every physics
tick and copying the crew manifest each time, which is what made the VAB crew tab crawl, it:

- adds the dark-side altitudes, asked for in kilometres, to the body radius in metres, so the transit it
  shows is for an orbit just above the surface: 20% too long at 100 km over Kerbin, 38% too short at
  keostationary, 88% too short for a 10 000 × 100 km orbit;
- matches modules by exact name, missing every subclass: CommNet Antennas Consumptor, Near Future relays,
  SystemHeat radiators and reactors, and more;
- divides a transmitter's draw by its packet size once more than stock does;
- counts every engine after the first electric one as electric;
- switches its whole USI integration off on any craft with a Kolonist, MedBay, Kerbitat or Sifter part.

Removing it is safe for saves: its parts only appear in the R&D unlock list, and the part modules and
`ReservePower` resource it added are dropped on load with a warning.

## Repository layout

```
src/SuMa.Ksp.BatteryCalculator/
  AssemblyInfo.cs                        KSPAssembly, and the dependency on ClickThroughBlocker

  Calculation/Darkness.cs                orbit period, shadow time and solar day
  Calculation/DarknessProfile.cs         the selected body and orbit as light and dark
  Calculation/SolarConditions.cs         flux, power curve distance and atmosphere, as stock computes them
  Calculation/EnergyEntry.cs             one producer or consumer: a row
  Calculation/EnergyBehaviour.cs         constant, switchable, on demand, fueled or solar
  Calculation/EnergyContext.cs           what a handler knows besides its module; fuel durations
  Calculation/EntrySink.cs               turns a handler's report into a row, with the default rules
  Calculation/VesselScanner.cs           walks the parts and modules, applies saved choices
  Calculation/EnergyBudget.cs            the verdict
  Calculation/Rates.cs                   reading rates from resource handlers and recipes

  Handlers/HandlerRegistry.cs            which handler a module type gets: the most derived wins
  Handlers/Stock/*.cs                    solar, generators, alternators, command, reaction wheels,
                                         transmitters, lights, radiators, converters, engines, RCS,
                                         wheels, robotics, sensors
  Handlers/Mods/*.cs                     the mods above that need code, all by reflection
  Handlers/Mods/ConfiguredModuleHandler.cs  a handler built from a BATTERY_CALCULATOR_MODULE node
  Handlers/ResourceHandlerFallback.cs    lists an unknown module from its RESOURCE rate, unticked

  Interface/BatteryCalculatorController.cs  the button, the window and when to recalculate
  Interface/EditorBatteryCalculator.cs   the VAB/SPH: craft, crew manifest, default target
  Interface/FlightBatteryCalculator.cs   flight: active vessel, current orbit, vessel events
  Interface/BatteryCalculatorWindow.cs   the IMGUI window
  Interface/*Toolbar*.cs, StockApplicationLauncherHost.cs  the toolbar abstraction, stock by default

  Persistence/BatteryCalculatorScenario.cs  the ticks and targets that differ from the defaults
  Settings/*.cs                          BATTERY_CALCULATOR and BATTERY_CALCULATOR_MODULE
  Support/*.cs                           logging, formatting, finding and reading other mods' types

  GameData/zzz_suma/...                  icons, settings and mod definitions shipped with the assembly

src/SuMa.Ksp.BatteryCalculator.Toolbar/  ToolbarControl support; KSP skips it when ToolbarControl is absent
```

## Building

`KspDir` in the project files points at the KSP install to compile against (`..\..\..\_KspDev`), which needs
ClickThroughBlocker in `GameData\000_ClickThroughBlocker` and, for the toolbar project, ToolbarControl in
`GameData\001_ToolbarControl`. `OutDir` points both projects at the same mod folder
(`..\..\..\_GameData\zzz_suma\SuMa.Ksp.BatteryCalculator`), so the assemblies, icons and config land there
directly, with no copy step.

```
dotnet build SuMa.Ksp.BatteryCalculator.sln
```

`KSP.log` shows which mods are supported, one line each:

```
[SuMa.BatteryCalculator] ToolbarControl is installed, so the button will also appear on Blizzy's toolbar.
[SuMa.BatteryCalculator] Supporting NearFutureSolar.ModuleCurvedSolarPanel.
[SuMa.BatteryCalculator] Supporting SystemHeat.ModuleSystemHeatFissionReactor.
[SuMa.BatteryCalculator] Supporting USI Life Support.
```

A module whose handler throws is reported once, by type, and left out of the totals; the rest of the
calculation carries on.
