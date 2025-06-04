using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using InterEx.CompilerInternals;
using InterEx.InterfaceTypes;

namespace InterEx.Integration
{
    public class TypeRegistry(TypeRegistry.BindingType binding)
    {
        public enum BindingType { Static, Instance }
        public BindingType Binding = binding;

        public delegate object VariadicFunction(object[] arguments);

        public class MethodGroup(List<FunctionInfo> functions) : ICustomValue
        {
            public readonly List<FunctionInfo> Functions = functions;

            public bool Get(IEEngine engine, string name, out Value value)
            {
                if (name == "Functions")
                {
                    value = engine.Integration.ImportValue(this.Functions);
                    return true;
                }
                value = default;
                return false;
            }

            public bool Invoke(IEEngine engine, Statement.Invocation invocation, string name, out Value result, Value[] arguments)
            {
                if (name != "")
                {
                    result = default;
                    return false;
                }

                result = engine.BridgeMethodCall(this.Functions, invocation, default, arguments);
                return true;
            }

            public bool Set(IEEngine engine, string name, Value value)
            {
                return false;
            }
        }

        public class EntityInfo(EntityProvider owner, string name) : ICustomValue
        {
            public String Name = name;
            public EntityProvider Owner = owner;
            public readonly Dictionary<string, EntityInfo> Members = [];

            public Type Class = null;
            public List<FunctionInfo> Generics = null;

            public void AddGeneric(Type type)
            {
                var parameters = type.GetTypeInfo().GenericTypeParameters;
                var factoryParameters = Enumerable.Repeat(typeof(Type), parameters.Length).ToArray();
                var cache = new Dictionary<string, EntityInfo>();
                var name = this.Name;
                var owner = this.Owner;

                this.Generics ??= [];
                this.Generics.Add(new((VariadicFunction)((arguments) =>
                {
                    var typeArguments = arguments.Cast<Type>().ToArray();
                    var cacheKey = $"<{String.Join(", ", (IEnumerable<Type>)typeArguments)}>";
                    if (cache.TryGetValue(cacheKey, out var existing)) return existing;

                    var genericResult = type.MakeGenericType(typeArguments);
                    var genericName = name + cacheKey;
                    var entity = new EntityInfo(owner, genericName) { Class = genericResult };
                    cache.Add(cacheKey, entity);

                    return entity;
                }), factoryParameters));
            }

            public virtual bool Get(IEEngine engine, string name, out Value value)
            {
                if (name == "o_Members")
                {
                    value = engine.Integration.ImportValue(this.Members);
                    return true;
                }

                if (this.Members.TryGetValue(name, out var entity))
                {
                    value = new Value(entity);
                    return true;
                }

                if (this.Class != null)
                {
                    var info = this.Owner.Integration.StaticCache.GetClassInfo(this.Class);
                    if (info.Properties.TryGetValue(name, out var member))
                    {
                        if (member is PropertyInfo property)
                        {
                            value = engine.Integration.ImportValue(property.GetValue(null));
                            return true;
                        }
                        else if (member is FieldInfo field)
                        {
                            value = engine.Integration.ImportValue(field.GetValue(null));
                            return true;
                        }
                        else throw new();
                    }

                    if (info.Functions.TryGetValue(name, out var functions))
                    {
                        value = engine.Integration.ImportValue(new MethodGroup(functions));
                        return true;
                    }
                }

                value = default;
                return false;
            }

            public virtual bool Invoke(IEEngine engine, Statement.Invocation invocation, string name, out Value result, Value[] arguments)
            {
                if (this.Members.TryGetValue(name, out var constructor))
                {
                    return constructor.Invoke(engine, invocation, "", out result, arguments);
                }

                if (name.Length > 0)
                {
                    if (this.Class == null) { result = default; return false; }
                    var info = this.Owner.Integration.StaticCache.GetClassInfo(this.Class);
                    if (!info.Functions.TryGetValue(name, out var staticMethodOverloads)) { result = default; return false; }
                    result = engine.BridgeMethodCall(staticMethodOverloads, invocation, new Value(null), arguments);
                    return true;
                }

                List<FunctionInfo> overloads = null;

                if (this.Class != null)
                {
                    var info = this.Owner.Integration.StaticCache.GetClassInfo(this.Class);
                    info.Functions.TryGetValue("", out overloads);
                }

                if (this.Generics != null)
                {
                    if (overloads != null)
                    {
                        overloads = [.. overloads, .. this.Generics];
                    }
                    else
                    {
                        overloads = this.Generics;
                    }
                }

                if (overloads != null)
                {
                    result = engine.BridgeMethodCall(overloads, invocation, new Value(null), arguments);
                    return true;
                }

                throw new IERuntimeException($"Entity '{this}' is not constructible");
            }

            public bool Set(IEEngine engine, string name, Value value)
            {
                return false;
            }

            public EntityInfo GetMember(string name)
            {
                if (this.Members.TryGetValue(name, out var member)) return member;
                var newMember = new EntityInfo(this.Owner, this.Name == "" ? name : this.Name + "." + name);
                this.Members.Add(name, newMember);
                return newMember;
            }

            public override string ToString()
            {
                return "(" + (this switch
                {
                    { Class: not null, Generics: not null } => "generic+class",
                    { Class: not null } => "class",
                    { Generics: not null } => "generic",
                    _ => "namespace"
                }) + ")" + this.Name;
            }
        }

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
            public readonly Dictionary<string, List<FunctionInfo>> Functions = [];

            public void AddFunction(string name, FunctionInfo method)
            {
                if (this.Functions.TryGetValue(name, out var list)) list.Add(method);
                else this.Functions.Add(name, [method]);
            }

            public readonly Dictionary<string, MemberInfo> Properties = [];
        }

        public delegate void ClassPatcher(TypeRegistry owner, Type type, ClassInfo info);
        protected List<ClassPatcher> _patchers = [];
        public void AddPatcher(ClassPatcher patcher)
        {
            this._patchers.Add(patcher);
        }

        protected Dictionary<Type, ClassInfo> _classCache = [];
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
                if (type.IsInterface)
                {
                    info.AddFunction("", new FunctionInfo((object value) => value, [type]));
                }

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
                    var setterParams = getterParams.Concat([property.PropertyType]).ToArray();

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
                            return property.GetValue(receiver, [key]);
                        }, getterParams));

                        info.AddFunction("at", new((object receiver, object key, object value) =>
                        {
                            property.SetValue(receiver, value, [key]);
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
    }
}
