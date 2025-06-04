using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using InterEx.CompilerInternals;
using InterEx.Integration;
using InterEx.InterfaceTypes;

using static InterEx.Integration.TypeRegistry;

namespace InterEx
{
    public class EntityProvider : IValueProvider
    {
        protected EntityInfo GetEntity(string path)
        {
            var result = this.Global;
            var segments = path
                .Split(['.', '+'])
                .Select(v => v.Split('`')[0]);

            foreach (var segment in segments)
            {
                result = result.GetMember(segment);
            }

            return result;
        }

        public EntityInfo AddClass(Type type, string overridePath = null)
        {
            var path = overridePath ?? type.FullName;
            var entity = this.GetEntity(path);

            if (type.IsGenericTypeDefinition)
            {
                entity.AddGeneric(type);
            }
            else
            {
                entity.Class = type;
            }

            return entity;
        }

        public EntityProvider LoadAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                this.AddClass(type);
            }

            return this;
        }

        public EntityProvider LoadAllAssemblies()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                this.LoadAssembly(assembly);
            }
            return this;
        }

        public readonly EntityInfo Global;
        public readonly IEIntegrationManager Integration;

        public EntityProvider(IEIntegrationManager integration)
        {
            this.Global = new EntityInfo(this, "");
            this.Integration = integration;
        }

        public static void ImportFromNamespace(EntityInfo entity, Scope scope)
        {
            var usings = _GetUsingsInScope(scope, allowCreate: true);
            if (usings.Contains(entity)) return;
            usings.Add(entity);
        }

        private static void _UsingStatement(IEEngine engine, Statement target, Scope scope)
        {
            var targetValue = engine.Evaluate(target, scope);
            var entity = engine.Integration.ExportValue<EntityInfo>(targetValue);
            ImportFromNamespace(entity, scope);
        }

        private const string _USINGS_KEY = "<>__usings";

        private static List<EntityInfo> _defaultList = null;
        private static List<EntityInfo> _GetUsingsInScope(Scope scope, bool allowCreate)
        {
            if (scope.TryGetOwn(_USINGS_KEY, out var usingsVariable))
            {
                return (List<EntityInfo>)usingsVariable.Content.Content;
            }

            if (!allowCreate) return _defaultList ??= [];

            var usings = new List<EntityInfo>();
            scope.Declare(_USINGS_KEY).Content = new Value(usings);
            return usings;
        }

        bool IValueProvider.Find(IEIntegrationManager _, Scope scope, string name, out Value value)
        {
            var entity = (EntityInfo)null;

            if (scope.TryGetOwn(_USINGS_KEY, out var usingsVariable))
            {
                foreach (var usingNamespace in _GetUsingsInScope(scope, allowCreate: false))
                {
                    if (usingNamespace.Members.TryGetValue(name, out entity))
                    {
                        value = new Value(entity);
                        return true;
                    }
                }
            }

            if (scope.IsGlobal)
            {
                if (name == "k_Using")
                {
                    var action = _UsingStatement;
                    value = new Value(action);
                    return true;
                }

                if (this.Global.Members.TryGetValue(name, out entity))
                {

                    value = new Value(entity);
                    return true;
                }
            }

            value = default;
            return false;
        }
    }
}
