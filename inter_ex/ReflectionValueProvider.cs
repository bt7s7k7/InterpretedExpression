using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace InterEx
{
    public class ReflectionValueProvider : IEEngine.IValueProvider, IEEngine.IValueExporter
    {
        public class EntityInfo : ICustomValue
        {
            public String Name;
            public ReflectionValueProvider Owner;

            public EntityInfo(ReflectionValueProvider owner, string name)
            {
                this.Name = name;
                this.Owner = owner;
            }

            public readonly Dictionary<string, EntityInfo> Members = new();

            public Type Class = null;
            public List<ReflectionCache.FunctionInfo> Generics = null;

            public void AddGeneric(Type type)
            {
                var parameters = type.GetTypeInfo().GenericTypeParameters;
                var factoryParameters = Enumerable.Repeat(typeof(Type), parameters.Length).ToArray();
                var cache = new Dictionary<string, EntityInfo>();
                var name = this.Name;
                var owner = this.Owner;

                this.Generics ??= new();
                this.Generics.Add(new((ReflectionCache.VariadicFunction)((arguments) =>
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

            public virtual bool Get(IEEngine engine, string name, out IEEngine.Value value)
            {
                if (name == "o_Members")
                {
                    value = engine.ImportValue(this.Members);
                    return true;
                }

                if (this.Members.TryGetValue(name, out var entity))
                {
                    value = new IEEngine.Value(entity);
                    return true;
                }

                if (this.Class != null)
                {
                    var info = this.Owner.Engine.StaticCache.GetClassInfo(this.Class);
                    if (info.Properties.TryGetValue(name, out var member))
                    {
                        if (member is PropertyInfo property)
                        {
                            value = engine.ImportValue(property.GetValue(null));
                            return true;
                        }
                        else if (member is FieldInfo field)
                        {
                            value = engine.ImportValue(field.GetValue(null));
                            return true;
                        }
                        else throw new();
                    }
                }

                value = default;
                return false;
            }

            public virtual bool Invoke(IEEngine engine, Statement.Invocation invocation, string name, out IEEngine.Value result, IEEngine.Value[] arguments)
            {
                if (this.Members.TryGetValue(name, out var constructor))
                {
                    return constructor.Invoke(engine, invocation, "", out result, arguments);
                }

                if (this.Class != null)
                {
                    var info = this.Owner.Engine.StaticCache.GetClassInfo(this.Class);
                    if (!info.Functions.TryGetValue(name, out var overloads)) { result = default; return false; };

                    result = engine.BridgeMethodCall(overloads, invocation, new IEEngine.Value(null), arguments);
                    return true;
                }

                if (this.Generics != null)
                {
                    result = engine.BridgeMethodCall(this.Generics, invocation, new IEEngine.Value(null), arguments);
                    return true;
                }

                result = default;
                return false;
            }

            public bool Set(IEEngine engine, string name, IEEngine.Value value)
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
                return $"({this switch
                {
                    { Class: not null } => "class",
                    { Generics: not null } => "generic",
                    _ => "namespace"
                }}){this.Name}";
            }
        }

        protected EntityInfo GetEntity(string path)
        {
            var result = this._global;
            var segments = path
                .Split(new[] { '.', '+' })
                .Select(v => v.Split('`')[0]);

            foreach (var segment in segments)
            {
                result = result.GetMember(segment);
            }

            return result;
        }

        public ReflectionValueProvider AddClass(Type type)
        {
            var path = type.FullName;
            var entity = this.GetEntity(path);

            if (type.IsGenericTypeDefinition)
            {
                entity.AddGeneric(type);
            }
            else
            {
                entity.Class = type;
            }

            return this;
        }

        public ReflectionValueProvider AddAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                this.AddClass(type);
            }

            return this;
        }

        public ReflectionValueProvider AddAllAssemblies()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                this.AddAssembly(assembly);
            }
            return this;
        }

        protected readonly EntityInfo _global;
        protected readonly List<EntityInfo> _usings = new();
        public readonly IEEngine Engine;

        public ReflectionValueProvider(IEEngine engine)
        {
            this._global = new EntityInfo(this, "");
            this.Engine = engine;

            this.Engine.AddExporter(this);
            this.Engine.AddProvider(this);
        }

        protected void Using(IEEngine engine, Statement target, IEEngine.Scope scope)
        {
            var targetValue = engine.Evaluate(target, scope);
            var entity = engine.ExportValue<EntityInfo>(targetValue);
            if (this._usings.Contains(entity)) return;
            this._usings.Add(entity);
        }

        bool IEEngine.IValueProvider.Find(IEEngine engine, string name, out IEEngine.Value value)
        {
            if (name == "k_Using")
            {
                var action = this.Using;
                value = new IEEngine.Value(action);
                return true;
            }

            var entity = (EntityInfo)null;

            foreach (var usingNamespace in this._usings)
            {
                if (usingNamespace.Members.TryGetValue(name, out entity)) { value = new IEEngine.Value(entity); return true; }
            }

            if (this._global.Members.TryGetValue(name, out entity)) { value = new IEEngine.Value(entity); return true; }

            value = default;
            return false;
        }

        bool IEEngine.IValueExporter.Export(IEEngine.Value value, Type type, out object data)
        {
            if (type == typeof(Type) && value.Content is EntityInfo entityInfo && entityInfo.Class != null)
            {
                data = entityInfo.Class;
                return true;
            }

            data = null;
            return false;
        }
    }
}
