using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using InterEx.Integration;
using InterEx.InterfaceTypes;

namespace InterEx.CompilerInternals
{
    public class IntrinsicSource : IValueImporter, IValueExporter
    {
        public bool Export(IEIntegrationManager integration, Value value, Type type, out object data)
        {
            if (value.Content is double number)
            {
                if (type == typeof(int)) { data = (int)number; return true; };
                if (type == typeof(float)) { data = (float)number; return true; };
                if (type == typeof(short)) { data = (short)number; return true; };
                if (type == typeof(long)) { data = (long)number; return true; };
            }

            if (value.Content is IEnumerable enumerable)
            {
                if (type == typeof(IEnumerable<object>))
                {
                    var result = new List<object>();
                    foreach (var element in enumerable) result.Add(element);
                    data = result;
                    return true;
                }

                if (type == typeof(IEnumerable<string>))
                {
                    var result = new List<string>();
                    foreach (var element in enumerable) result.Add(element == null ? "null" : element.ToString());
                    data = result;
                    return true;
                }
            }

            if (type == typeof(string))
            {
                data = value.Content?.ToString() ?? "null";
                return true;
            }

            if (value.Content is IEFunction function && integration.Delegates.IsDelegate(type))
            {
                var adapter = integration.Delegates.GetAdapter(type);
                data = adapter.Adapt(function);
                return true;
            }

            if (type.IsEnum && value.Content is string enumName)
            {
                if (Enum.TryParse(type, enumName, out var enumValue))
                {
                    data = enumValue;
                    return true;
                }
            }

            data = default;
            return false;
        }

        public bool Import(IEIntegrationManager _, object data, out Value value)
        {
            if (data is int @int) { value = new Value((double)@int); return true; }
            if (data is short @short) { value = new Value((double)@short); return true; }
            if (data is float @float) { value = new Value((double)@float); return true; }
            if (data is long @long) { value = new Value((double)@long); return true; }

            value = default;
            return false;
        }

        protected IntrinsicSource() { }
        public static readonly IntrinsicSource Instance = new();

        public static readonly Type DictionaryType = typeof(IDictionary<string, string>).GetGenericTypeDefinition();
        public static readonly Type ListType = typeof(IList<string>).GetGenericTypeDefinition();
        public static readonly Type ListResultType = typeof(List<string>).GetGenericTypeDefinition();

        public static void InitializeIntegration(IEIntegrationManager integration)
        {
            integration.AddExporter(Instance);
            integration.AddImporter(Instance);

            integration.InstanceCache.AddPatcher((_, type, info) =>
            {
                if (type.IsAssignableTo(typeof(IDictionary)))
                {
                    var dictionaryInterface = type.GetInterfaces().First(v => v.GetGenericTypeDefinition() == IntrinsicSource.DictionaryType);
                    if (dictionaryInterface != null)
                    {
                        var genericParameters = dictionaryInterface.GetGenericArguments();
                        var keyType = genericParameters[0];
                        var valueType = genericParameters[1];
                        var keysResultType = IntrinsicSource.ListResultType.MakeGenericType(new[] { keyType });
                        var keysResultCtor = keysResultType.GetConstructor(Array.Empty<Type>());
                        var valuesResultType = IntrinsicSource.ListResultType.MakeGenericType(new[] { valueType });
                        var valuesResultCtor = valuesResultType.GetConstructor(Array.Empty<Type>());

                        info.AddFunction("keys", new((IDictionary receiver) =>
                        {
                            var keys = receiver.Keys;
                            var result = (IList)keysResultCtor.Invoke(Array.Empty<object>());
                            foreach (var key in keys) result.Add(key);
                            return result;
                        }, Array.Empty<Type>()));

                        info.AddFunction("values", new((IDictionary receiver) =>
                        {
                            var values = receiver.Values;
                            var result = (IList)valuesResultCtor.Invoke(Array.Empty<object>());
                            foreach (var value in values) result.Add(value);
                            return result;
                        }, Array.Empty<Type>()));

                        info.AddFunction("init", new((IDictionary receiver, Dictionary<string, Value> literal) =>
                        {
                            foreach (var (key, value) in literal)
                            {
                                receiver.Add(key, integration.ExportValue(value, valueType));
                            }

                            return receiver;
                        }, new[] { typeof(Dictionary<string, Value>) }));

                        return;
                    }
                }

                if (type.IsAssignableTo(typeof(IList)))
                {
                    var listInterface = type.GetInterfaces().First(v => v.GetGenericTypeDefinition() == IntrinsicSource.ListType);
                    if (listInterface != null)
                    {
                        var genericParameters = listInterface.GetGenericArguments();
                        var valueType = genericParameters[0];

                        info.AddFunction("init", new((ReflectionCache.VariadicFunction)((object[] arguments) =>
                        {
                            var receiver = (IList)arguments[0];

                            foreach (var element in arguments.Skip(1).Cast<Value>())
                            {
                                receiver.Add(integration.ExportValue(element, valueType));
                            }

                            return receiver;
                        }), null));

                        return;
                    }
                }

                info.AddFunction("init", new((object receiver, Dictionary<string, Value> literal, CallContext ctx) =>
                {
                    var engine = ctx.Engine;

                    foreach (var (key, value) in literal)
                    {
                        engine.SetProperty(new Value(receiver), key, value);
                    }

                    return receiver;
                }, new[] { typeof(Dictionary<string, Value>), typeof(CallContext) }));
            });
        }

        public static void InitializeIntrinsics(IEEngine engine)
        {
            engine.AddGlobal("true", true);
            engine.AddGlobal("false", false);
            engine.AddGlobal("null", null);
            engine.AddGlobal("GLOBAL", engine.GlobalScope);
            engine.AddGlobal("ENGINE", engine);

            engine.AddGlobal("k_Ref", (IEEngine engine, Statement value, Scope scope) =>
            {
                if (value is Statement.MemberAccess memberAccess)
                {
                    var receiver = engine.Evaluate(memberAccess.Receiver, scope);
                    engine.GetProperty(receiver, memberAccess.Member);
                    return (IEReference)new IEReference.ObjectProperty(engine, receiver, memberAccess.Member);
                }
                else if (value is Statement.VariableAccess variableAccess)
                {
                    var variable = engine.GetVariable(variableAccess.Name, variableAccess.Position, scope);
                    return (IEReference)new IEReference.VariableReference(engine, variable);
                }
                else throw new IERuntimeException("Can only get reference to a variable or object property");
            });


        }
    }
}
