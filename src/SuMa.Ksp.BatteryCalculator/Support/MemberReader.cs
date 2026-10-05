using System;
using System.Globalization;
using System.Reflection;

namespace SuMa.Ksp.BatteryCalculator.Support
{
    /// <summary>
    /// Reads a field or property of another mod's type by name, public or not, looked up once. This is how the
    /// mod handlers read mod modules without referencing the mods.
    ///
    /// A member that turns out not to exist, or to throw, reads as "no value" rather than failing: a mod
    /// update that renames a field then costs its row, not the whole calculation.
    /// </summary>
    internal sealed class MemberReader
    {
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly FieldInfo _field;
        private readonly PropertyInfo _property;

        private MemberReader(FieldInfo field, PropertyInfo property)
        {
            _field = field;
            _property = property;
        }

        public string Name => _field != null ? _field.Name : _property.Name;

        /// <summary>
        /// The member of this name on the type or any of its base types, or null when there is none.
        /// </summary>
        public static MemberReader For(Type type, string name)
        {
            return Find(type, name, InstanceMembers);
        }

        public static MemberReader ForStatic(Type type, string name)
        {
            return Find(type, name, StaticMembers);
        }

        public object Read(object instance)
        {
            try
            {
                return _field != null ? _field.GetValue(instance) : _property.GetValue(instance, null);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public bool? ReadBoolean(object instance)
        {
            var value = Read(instance);
            if (value is bool boolean)
            {
                return boolean;
            }

            return value != null && bool.TryParse(value.ToString(), out var parsed) ? parsed : (bool?)null;
        }

        public double? ReadDouble(object instance)
        {
            var value = Read(instance);
            if (value == null)
            {
                return null;
            }

            try
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public string ReadString(object instance)
        {
            var value = Read(instance);
            return value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : null;
        }

        private static MemberReader Find(Type type, string name, BindingFlags flags)
        {
            if (type == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(name, flags | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return new MemberReader(field, null);
                }

                var property = current.GetProperty(name, flags | BindingFlags.DeclaredOnly);
                if (property != null && property.GetIndexParameters().Length == 0 && property.CanRead)
                {
                    return new MemberReader(null, property);
                }
            }

            return null;
        }
    }
}
