using System;
using System.Collections.Generic;
using System.Linq;
using InterEx.CompilerInternals;
using InterEx.Integration;

namespace InterEx.InterfaceTypes
{
    public class IEIntegrationManager
    {
        public readonly ReflectionCache InstanceCache;
        public readonly ReflectionCache StaticCache;
        public readonly DelegateAdapterProvider Delegates;

        public IEIntegrationManager()
        {
            this.InstanceCache = new ReflectionCache(ReflectionCache.BindingType.Instance);
            this.StaticCache = new ReflectionCache(ReflectionCache.BindingType.Static);
            this.Delegates = new DelegateAdapterProvider(this.InstanceCache);

            IntrinsicSource.InitializeIntegration(this);
        }

        protected readonly List<IValueImporter> _importers = new();
        public void AddImporter(IValueImporter importer) => this._importers.Add(importer);
        protected readonly List<IValueExporter> _exporters = new();
        public void AddExporter(IValueExporter exporter) => this._exporters.Add(exporter);
        protected readonly List<IValueExporter> _exportersFallback = new();
        public void AddExporterFallback(IValueExporter exporter) => this._exportersFallback.Add(exporter);
        protected readonly List<IValueProvider> _providers = new();
        public void AddProvider(IValueProvider provider) => this._providers.Add(provider);
        protected readonly List<IValueProvider> _providersFallback = new();
        public void AddProviderFallback(IValueProvider provider) => this._providersFallback.Add(provider);

        public bool FindValue(string name, out Value value)
        {
            foreach (var provider in this._providers)
            {
                if (provider.Find(this, name, out value))
                {
                    return true;
                }
            }

            foreach (var provider in this._providersFallback)
            {
                if (provider.Find(this, name, out value))
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

            if (value.Content != null && value.Content.GetType().IsAssignableTo(type))
            {
                return value.Content;
            }

            if (value.Content == null && type.IsClass)
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

            throw new IERuntimeException("Cannot convert value " + value.Content?.GetType().FullName + " into " + type.FullName);
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
