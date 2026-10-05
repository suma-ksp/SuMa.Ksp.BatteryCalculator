using System;
using System.Collections.Generic;
using SuMa.Ksp.BatteryCalculator.Handlers.Mods;
using SuMa.Ksp.BatteryCalculator.Handlers.Stock;
using SuMa.Ksp.BatteryCalculator.Settings;
using SuMa.Ksp.BatteryCalculator.Support;

namespace SuMa.Ksp.BatteryCalculator.Handlers
{
    /// <summary>
    /// Every handler the calculator has, and which one a given module type gets.
    ///
    /// Stock handlers are always there. A mod's handler is only registered when that mod's type can be found,
    /// so nothing here depends on another mod being installed. Handlers described in configuration come last
    /// and replace a coded handler for the same type, which lets a config file correct or extend support
    /// without a rebuild.
    ///
    /// The lookup walks from the module's own type towards PartModule and takes the first handler it meets,
    /// which is the one for the most derived type. The answer is cached per type, because the same few dozen
    /// types are asked about for every part on every refresh.
    /// </summary>
    internal sealed class HandlerRegistry
    {
        private readonly Dictionary<Type, IEnergyHandler> _handlerByModuleType = new Dictionary<Type, IEnergyHandler>();
        private readonly Dictionary<Type, IEnergyHandler> _resolved = new Dictionary<Type, IEnergyHandler>();
        private readonly List<IVesselEnergyHandler> _vesselHandlers = new List<IVesselEnergyHandler>();

        private HandlerRegistry()
        {
        }

        public IEnergyHandler Fallback { get; } = new ResourceHandlerFallback();

        public IList<IVesselEnergyHandler> VesselHandlers => _vesselHandlers;

        public static HandlerRegistry Create(BatteryCalculatorSettings settings)
        {
            var registry = new HandlerRegistry();

            registry.Register(new SolarPanelHandler());
            registry.Register(new GeneratorHandler());
            registry.Register(new AlternatorHandler());
            registry.Register(new CommandHandler());
            registry.Register(new ReactionWheelHandler());
            registry.Register(new DataTransmitterHandler());
            registry.Register(new LightHandler());
            registry.Register(new RadiatorHandler());
            registry.Register(new ConverterHandler());
            registry.Register(new EngineHandler());
            registry.Register(new RcsHandler());
            registry.Register(new WheelMotorHandler());
            registry.Register(new ServoHandler());
            registry.Register(new EnviroSensorHandler());

            registry.RegisterIfPresent(CurvedSolarPanelHandler.TryCreate());
            registry.RegisterIfPresent(UniversalStorageSolarHandler.TryCreate());
            registry.RegisterIfPresent(SystemHeatReactorHandler.TryCreate());
            registry.RegisterIfPresent(CryoTankHandler.TryCreate());
            registry.RegisterIfPresent(DeepFreezerHandler.TryCreate());

            foreach (var definition in settings.ModuleDefinitions)
            {
                registry.RegisterIfPresent(ConfiguredModuleHandler.TryCreate(definition));
            }

            var lifeSupport = UsiLifeSupportHandler.TryCreate();
            if (lifeSupport != null)
            {
                registry._vesselHandlers.Add(lifeSupport);
            }

            return registry;
        }

        /// <summary>
        /// The handler for a module of this type, or null when there is none and the module is left to
        /// <see cref="Fallback" />.
        /// </summary>
        public IEnergyHandler Find(Type moduleType)
        {
            if (moduleType == null)
            {
                return null;
            }

            if (_resolved.TryGetValue(moduleType, out var cached))
            {
                return cached;
            }

            IEnergyHandler handler = null;
            for (var type = moduleType; type != null && type != typeof(PartModule); type = type.BaseType)
            {
                if (_handlerByModuleType.TryGetValue(type, out handler))
                {
                    break;
                }
            }

            _resolved.Add(moduleType, handler);
            return handler;
        }

        private void Register(IEnergyHandler handler)
        {
            _handlerByModuleType[handler.ModuleType] = handler;
        }

        private void RegisterIfPresent(IEnergyHandler handler)
        {
            if (handler == null)
            {
                return;
            }

            Register(handler);
            Log.Info("Supporting " + handler.ModuleType.FullName + ".");
        }
    }
}
