using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace InterEx.Integration
{
    public class ReflectionCache
    {
        public enum BindingType { Static, Instance }
        public BindingType Binding;

        public delegate object VariadicFunction(object[] arguments);

        public record class FunctionInfo
        {
            public readonly object Target;
            public readonly Type[] Parameters;

            public FunctionInfo(MethodInfo method)
            {
                this.Target = method;
                this.Parameters = method.GetParameters().Select(v => v.ParameterType).ToArray();
            }

            public FunctionInfo(ConstructorInfo method)
            {
                this.Target = method;
                this.Parameters = method.GetParameters().Select(v => v.ParameterType).ToArray();
            }

            public FunctionInfo(VariadicFunction target, Type[] parameters)
            {
                this.Target = target;
                this.Parameters = parameters;
            }

            public FunctionInfo(Delegate target, Type[] parameters)
            {
                this.Target = target;
                this.Parameters = parameters;
            }
        }

        public class ClassInfo
        {
            public readonly Dictionary<string, List<FunctionInfo>> Functions = new();

            public void AddFunction(string name, FunctionInfo method)
            {
                if (this.Functions.TryGetValue(name, out var list)) list.Add(method);
                else this.Functions.Add(name, new() { method });
            }

            public readonly Dictionary<string, MemberInfo> Properties = new();
        }

        public delegate void ClassPatcher(ReflectionCache owner, Type type, ClassInfo info);
        protected List<ClassPatcher> _patchers = new();
        public void AddPatcher(ClassPatcher patcher)
        {
            this._patchers.Add(patcher);
        }

        protected Dictionary<Type, ClassInfo> _classCache = new();
        public ClassInfo GetClassInfo(Type type)
        {
            if (this._classCache.TryGetValue(type, out var existing)) return existing;

            var info = new ClassInfo();

            var flags = BindingFlags.Public | BindingFlags.FlattenHierarchy | (this.Binding == BindingType.Static ? BindingFlags.Static : BindingFlags.Instance);

            foreach (var method in type.GetMethods(flags))
            {
                if (method.ContainsGenericParameters) continue;
                info.AddFunction(method.Name, new FunctionInfo(method));
            }

            if (this.Binding == BindingType.Static)
            {
                foreach (var constructor in type.GetConstructors())
                {
                    info.AddFunction("", new FunctionInfo(constructor));
                }
            }

            foreach (var property in type.GetProperties(flags))
            {
                var indexParameters = property.GetIndexParameters();
                if (indexParameters.Length == 1)
                {
                    var getterParams = indexParameters.Select(v => v.ParameterType).ToArray();
                    var setterParams = getterParams.Concat(new[] { property.PropertyType }).ToArray();

                    if (type.IsAssignableTo(typeof(IDictionary)))
                    {
                        info.AddFunction("at", new((IDictionary receiver, object key) =>
                        {
                            return receiver[key];
                        }, getterParams));

                        info.AddFunction("at", new((IDictionary receiver, object key, object value) =>
                        {
                            receiver[key] = value;
                            return receiver;
                        }, setterParams));
                    }
                    else if (type.IsAssignableTo(typeof(IList)))
                    {
                        info.AddFunction("at", new((IList receiver, int key) =>
                        {
                            return receiver[key];
                        }, getterParams));

                        info.AddFunction("at", new((IList receiver, int key, object value) =>
                        {
                            receiver[key] = value;
                            return receiver;
                        }, setterParams));
                    }
                    else
                    {
                        info.AddFunction("at", new((object receiver, object key) =>
                        {
                            return property.GetValue(receiver, new[] { key });
                        }, getterParams));

                        info.AddFunction("at", new((object receiver, object key, object value) =>
                        {
                            property.SetValue(receiver, value, new[] { key });
                            return receiver;
                        }, setterParams));
                    }

                    continue;
                }
                info.Properties.Add(property.Name, property);
            }

            foreach (var field in type.GetFields(flags))
            {
                info.Properties.Add(field.Name, field);
            }

            foreach (var patcher in this._patchers)
            {
                patcher(this, type, info);
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
