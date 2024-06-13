using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace InterEx
{
    public partial class IEEngine
    {
        public readonly struct Value
        {
            public readonly object Content;

            public Value(object content)
            {
                this.Content = content;
            }
        }

        public interface IValueImporter
        {
            public bool Import(object data, out Value value);
        }

        public interface IValueExporter
        {
            public bool Export(Value value, Type type, out object data);
        }

        public interface IValueProvider
        {
            public bool Find(IEEngine engine, string name, out Value value);
        }

        public interface IValueAdapter
        {
            public bool Set(IEEngine engine, Value receiver, string name, Value value);
            public bool Get(IEEngine engine, Value receiver, string name, out Value value);
            public bool Invoke(IEEngine engine, Value receiver, string name, out Value result, Value[] arguments);
        }

        public sealed class Variable
        {
            public Value Content;

            public Variable(Value value)
            {
                this.Content = value;
            }
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
        protected readonly List<IValueAdapter> _adapters = new();
        public void AddAdapter(IValueAdapter adapter) => this._adapters.Add(adapter);

        public Value ImportValue(object data)
        {
            if (data is Value existing) return existing;

            foreach (var importer in this._importers)
            {
                if (importer.Import(data, out var value)) return value;
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
                if (exporter.Export(value, type, out var data)) return data;
            }

            if (type == typeof(object))
            {
                return value.Content;
            }

            foreach (var exporter in this._exportersFallback)
            {
                if (exporter.Export(value, type, out var data)) return data;
            }

            throw new IERuntimeException("Cannot convert value " + value.Content?.GetType().FullName + " into " + type.FullName);
        }

        public object[] ExportArguments(Value[] arguments, Type[] parameters)
        {
            if (arguments.Length != parameters.Length)
            {
                throw new IERuntimeException($"Argument count mismatch, got {arguments.Length}, but expected {parameters.Length} ({String.Join(", ", parameters.Select(v => v.FullName))})");
            }

            var result = new object[arguments.Length];

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
                    throw new IERuntimeException($"Argument type mismatch in ({String.Join(", ", parameters.Select(v => v.FullName))})[{i}]", error.Message);
                }
            }

            return result;
        }

        public object ExecuteMethodCall(MethodBase target, Value receiver, Value[] arguments)
        {
            var result = (object)null;

            if (target is MethodInfo method)
            {
                var exportedArguments = this.ExportArguments(arguments, method.GetParameters().Select(v => v.ParameterType).ToArray());
                result = method.Invoke(receiver.Content, exportedArguments);
            }
            else if (target is ConstructorInfo constructor)
            {
                var exportedArguments = this.ExportArguments(arguments, constructor.GetParameters().Select(v => v.ParameterType).ToArray());
                result = constructor.Invoke(exportedArguments);
            }

            return result;
        }

        public Value BridgeMethodCall(List<MethodBase> overloads, Statement.Invocation invocation, Value receiver, Value[] arguments)
        {
            if (invocation != null && invocation.CachedCall != null)
            {
                var result = (object)null;
                var call = invocation.CachedCall;

                if (arguments.Length != call.Parameters.Length) goto deoptimize;
                if (receiver.Content?.GetType() != call.ReceiverType) goto deoptimize;
                if (!Enumerable.SequenceEqual(arguments.Select(v => v.Content?.GetType()), call.Parameters)) goto deoptimize;

                try
                {
                    result = this.ExecuteMethodCall(call.Target, receiver, arguments);
                }
                catch (IERuntimeException)
                {
                    goto deoptimize;
                }

                return this.ImportValue(result);
            }

            goto next;
        deoptimize:
            invocation.CachedCall = null;
            invocation.DeoptimizeCounter++;
        next:
            var messages = new List<string>();

            foreach (var overload in overloads)
            {
                var resultObject = (object)null;

                try
                {
                    resultObject = this.ExecuteMethodCall(overload, receiver, arguments);
                }
                catch (IERuntimeException error)
                {
                    messages.Add(error.Message);
                    continue;
                }

                if (resultObject is Value value) return value;

                if (invocation != null && invocation.DeoptimizeCounter < 10) invocation.CachedCall = new(
                    ReceiverType: receiver.Content?.GetType(),
                    Parameters: arguments.Select(v => v.Content?.GetType()).ToArray(),
                    Target: overload
                );

                return this.ImportValue(resultObject);
            }

            throw new IERuntimeException(String.Join('\n', messages));
        }
    }
}
