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

            public override string ToString()
            {
                return this.Content == null ? "null" : this.Content.ToString();
            }

            public Value(object content)
            {
                this.Content = content;
            }
        }

        public interface IValueImporter
        {
            public bool Import(IEEngine engine, object data, out Value value);
        }

        public interface IValueExporter
        {
            public bool Export(IEEngine engine, Value value, Type type, out object data);
        }

        public interface IValueProvider
        {
            public bool Find(IEEngine engine, string name, out Value value);
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

        public object[] ExportArguments(Value[] arguments, Type[] parameters)
        {
            if (parameters == null) return arguments.Cast<object>().ToArray();

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
                    throw new IERuntimeException($"Argument type mismatch in ({String.Join(", ", parameters.Select(v => v.FullName))})[{i}]", error);
                }
            }

            return result;
        }

        public object ExecuteMethodCall(ReflectionCache.FunctionInfo function, Value receiver, Value[] arguments)
        {
            var result = (object)null;

            var target = function.Target;

            if (target is MethodInfo method)
            {
                var exportedArguments = this.ExportArguments(arguments, function.Parameters);
                result = method.Invoke(receiver.Content, exportedArguments);
            }
            else if (target is ConstructorInfo constructor)
            {
                var exportedArguments = this.ExportArguments(arguments, function.Parameters);
                result = constructor.Invoke(exportedArguments);
            }
            else if (target is ReflectionCache.VariadicFunction variadic)
            {
                var exportedArguments = this.ExportArguments(arguments, function.Parameters);
                if (receiver.Content != null) exportedArguments = new[] { receiver.Content }.Concat(exportedArguments).ToArray();
                result = variadic(exportedArguments);
            }
            else if (target is Delegate @delegate)
            {
                var exportedArguments = this.ExportArguments(arguments, function.Parameters);
                if (receiver.Content != null) exportedArguments = new[] { receiver.Content }.Concat(exportedArguments).ToArray();
                result = @delegate.DynamicInvoke(exportedArguments);
            }
            else throw new();

            return result;
        }

        public Value BridgeMethodCall(List<ReflectionCache.FunctionInfo> overloads, Statement.Invocation invocation, Value receiver, Value[] arguments)
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
