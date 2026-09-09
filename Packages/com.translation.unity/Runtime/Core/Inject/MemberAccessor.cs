using System;
using System.Collections.Generic;
using System.Reflection;

namespace Translation
{
    /// <summary> 필드와 프로퍼티를 같은 방식으로 읽고 쓴다. 타입별로 캐싱한다 </summary>
    public sealed class MemberAccessor
    {
        private const BindingFlags Flags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private static readonly Dictionary<string, MemberAccessor> Cache =
            new Dictionary<string, MemberAccessor>(StringComparer.Ordinal);

        private readonly FieldInfo _field;
        private readonly PropertyInfo _property;

        public string Name { get; }

        public Type ValueType { get; }

        public bool CanWrite { get; }

        private MemberAccessor(string name, FieldInfo field, PropertyInfo property)
        {
            Name = name;
            _field = field;
            _property = property;
            ValueType = field != null ? field.FieldType : property.PropertyType;
            CanWrite = field != null ? !field.IsInitOnly : property.CanWrite;
        }

        /// <summary> 이름으로 필드나 프로퍼티를 찾는다. 없으면 null. 대소문자를 무시하고 재시도한다 </summary>
        public static MemberAccessor Find(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name))
                return null;

            var cacheKey = type.FullName + "|" + name;
            lock (Cache)
            {
                if (Cache.TryGetValue(cacheKey, out var cached))
                    return cached;
            }

            var accessor = Resolve(type, name);
            lock (Cache)
            {
                Cache[cacheKey] = accessor;
            }

            return accessor;
        }

        private static MemberAccessor Resolve(Type type, string name)
        {
            var field = type.GetField(name, Flags);
            if (field != null)
                return new MemberAccessor(name, field, null);

            var property = type.GetProperty(name, Flags);
            if (property != null)
                return new MemberAccessor(name, null, property);

            foreach (var candidate in type.GetFields(Flags))
            {
                if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                    return new MemberAccessor(candidate.Name, candidate, null);
            }

            foreach (var candidate in type.GetProperties(Flags))
            {
                if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                    return new MemberAccessor(candidate.Name, null, candidate);
            }

            return null;
        }

        public object GetValue(object instance)
        {
            return _field != null ? _field.GetValue(instance) : _property.GetValue(instance, null);
        }

        public void SetValue(object instance, object value)
        {
            if (_field != null)
                _field.SetValue(instance, value);
            else
                _property.SetValue(instance, value, null);
        }

        public override string ToString()
        {
            return Name + " : " + ValueType.Name;
        }
    }
}
