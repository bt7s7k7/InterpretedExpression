using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using InterEx.CompilerInternals;
using InterEx.Integration;

namespace InterEx.InterfaceTypes
{
    public class IEIntegrationManager
    {
        public readonly TypeRegistry InstanceCache;
        public readonly TypeRegistry StaticCache;
        public readonly DelegateAdapterProvider Delegates;
        public readonly EntityProvider EntityProvider;

        public IEIntegrationManager()
        {
            this.InstanceCache = new TypeRegistry(TypeRegistry.BindingType.Instance);
            this.StaticCache = new TypeRegistry(TypeRegistry.BindingType.Static);
            this.Delegates = new DelegateAdapterProvider(this.InstanceCache);
            this.EntityProvider = new EntityProvider(this);

            IntrinsicSource.InitializeIntegration(this);

            this.AddProvider(this.EntityProvider);
        }

        public ReadOnlyCollection<IValueImporter> Importers => this._importers.AsReadOnly();
        protected readonly List<IValueImporter> _importers = [];
        public void AddImporter(IValueImporter importer) => this._importers.Add(importer);

        public ReadOnlyCollection<IValueExporter> Exporters => this._exporters.AsReadOnly();
        protected readonly List<IValueExporter> _exporters = [];
        public void AddExporter(IValueExporter exporter) => this._exporters.Add(exporter);
        public ReadOnlyCollection<IValueExporter> ExportersFallback => this._exportersFallback.AsReadOnly();
        protected readonly List<IValueExporter> _exportersFallback = [];
        public void AddExporterFallback(IValueExporter exporter) => this._exportersFallback.Add(exporter);

        public ReadOnlyCollection<IValueProvider> Providers => this._providers.AsReadOnly();
        protected readonly List<IValueProvider> _providers = [];
        public void AddProvider(IValueProvider provider) => this._providers.Add(provider);
        public ReadOnlyCollection<IValueProvider> ProvidersFallback => this._providersFallback.AsReadOnly();
        protected readonly List<IValueProvider> _providersFallback = [];
        public void AddProviderFallback(IValueProvider provider) => this._providersFallback.Add(provider);

        public bool FindValue(Scope scope, string name, out Value value)
        {
            foreach (var provider in this._providers)
            {
                if (provider.Find(this, scope, name, out value))
                {
                    return true;
                }
            }

            foreach (var provider in this._providersFallback)
            {
                if (provider.Find(this, scope, name, out value))
                {
                    return true;
                }
            }

            value = default;
            return false;
        }

        public Value ImportValue(object data)
        {
            if (data is Value existing) return existing;

            foreach (var importer in this._importers)
            {
                if (importer.Import(this, data, out var value)) return value;
            }

            return new Value(data);
        }

        public object ExportValue(Value value, Type type)
        {
            if (type == typeof(Value)) return value;
            if (type == typeof(void)) return null;

            if (value.Content != null && value.Content.GetType().IsAssignableTo(type))
            {
                return value.Content;
            }

            if (value.Content == null && (type.IsClass || type.IsInterface))
            {
                return null;
            }

            foreach (var exporter in this._exporters)
            {
                if (exporter.Export(this, value, type, out var data)) return data;
            }

            if (type == typeof(object))
            {
                return value.Content;
            }

            foreach (var exporter in this._exportersFallback)
            {
                if (exporter.Export(this, value, type, out var data)) return data;
            }

            throw new IERuntimeException("Cannot convert value " + (value.Content?.GetType().FullName ?? "null") + " into " + type.FullName);
        }

        public T ExportValue<T>(Value value)
        {
            return (T)this.ExportValue(value, typeof(T));
        }

        public object[] ExportArguments(Value[] arguments, Type[] parameters, CallContext context)
        {
            if (parameters == null) return arguments.Cast<object>().ToArray();

            var expectedParameterCount = parameters.Length;
            var resultArgumentsCount = arguments.Length;
            var addContext = false;
            if (parameters.Length > 0 && expectedParameterCount - 1 == resultArgumentsCount && parameters[^1] == typeof(CallContext))
            {
                expectedParameterCount--;
                resultArgumentsCount++;
                addContext = true;
            }

            if (arguments.Length != expectedParameterCount)
            {
                throw new IERuntimeException($"Argument count mismatch, got {arguments.Length}, but expected {expectedParameterCount} ({String.Join(", ", parameters.Select(v => v.FullName))})");
            }

            var result = new object[resultArgumentsCount];

            for (var i = 0; i < arguments.Length; i++)
            {
                var argument = arguments[i];
                var parameter = parameters[i];

                try
                {
                    result[i] = this.ExportValue(argument, parameter);
                }
                catch (IERuntimeException error)
                {
                    throw new IERuntimeException($"Argument type mismatch in ({String.Join(", ", parameters.Select(v => v.FullName))})[{i}]", error);
                }
            }

            if (addContext)
            {
                result[^1] = context;
            }

            return result;
        }
    }
}
