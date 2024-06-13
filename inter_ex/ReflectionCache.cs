using System;
using System.Collections.Generic;
using System.Reflection;

namespace InterEx
{
    public class ReflectionCache
    {
        public enum BindingType { Static, Instance }
        public BindingType Binding;

        public class ClassInfo
        {
            public readonly Dictionary<string, List<MethodBase>> Methods = new();
            public readonly Dictionary<string, MemberInfo> Properties = new();
        }

        protected Dictionary<Type, ClassInfo> _classCache = new();
        public ClassInfo GetClassInfo(Type type)
        {
            if (this._classCache.TryGetValue(type, out var existing)) return existing;

            var info = new ClassInfo();

            var flags = BindingFlags.Public | BindingFlags.FlattenHierarchy | (this.Binding == BindingType.Static ? BindingFlags.Static : BindingFlags.Instance);

            foreach (var method in type.GetMethods(flags))
            {
                if (info.Methods.TryGetValue(method.Name, out var list)) list.Add(method);
                else info.Methods.Add(method.Name, new() { method });
            }

            if (this.Binding == BindingType.Static)
            {
                foreach (var constructor in type.GetConstructors())
                {
                    if (info.Methods.TryGetValue("", out var list)) list.Add(constructor);
                    else info.Methods.Add("", new() { constructor });
                }
            }

            foreach (var property in type.GetProperties(flags))
            {
                info.Properties.Add(property.Name, property);
            }

            foreach (var field in type.GetFields(flags))
            {
                info.Properties.Add(field.Name, field);
            }

            this._classCache.Add(type, info);
            return info;
        }

        public ReflectionCache(BindingType binding)
        {
            this.Binding = binding;
        }
    }
}
