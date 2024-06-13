using System;
using System.Collections.Generic;
using System.Reflection;

namespace InterEx
{
    public class ReflectionValueProvider : IEEngine.IValueProvider
    {
        protected readonly ReflectionCache _reflectionCache = new(ReflectionCache.BindingType.Static);

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

            public virtual bool Get(IEEngine engine, string name, out IEEngine.Value value)
            {
                if (this.Members.TryGetValue(name, out var entity))
                {
                    value = new IEEngine.Value(entity);
                    return true;
                }

                if (this.Class != null)
                {
                    var info = this.Owner._reflectionCache.GetClassInfo(this.Class);
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

                if (this.Class == null)
                {
                    result = default;
                    return false;
                }

                var info = this.Owner._reflectionCache.GetClassInfo(this.Class);
                if (!info.Functions.TryGetValue(name, out var overloads)) { result = default; return false; };

                result = engine.BridgeMethodCall(overloads, invocation, new IEEngine.Value(null), arguments);
                return true;
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
                return $"({(this.Class == null ? "namespace" : "class")}){this.Name}";
            }
        }

        protected EntityInfo GetEntity(string path)
        {
            var result = this._global;
            var segments = path.Split(".");

            foreach (var segment in segments)
            {
                result = result.GetMember(segment);
            }

            return result;
        }

        public ReflectionValueProvider AddClass(Type type)
        {
            var path = type.FullName;
            this.GetEntity(path).Class = type;
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

        public ReflectionValueProvider()
        {
            this._global = new EntityInfo(this, "");
        }

        protected void Using(IEEngine engine, Statement target, IEEngine.Scope scope)
        {
            var targetValue = engine.Evaluate(target, scope);
            var entity = (EntityInfo)engine.ExportValue(targetValue, typeof(EntityInfo));
            if (this._usings.Contains(entity)) return;
            this._usings.Add(entity);
        }

        public bool Find(IEEngine engine, string name, out IEEngine.Value value)
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
    }
}
