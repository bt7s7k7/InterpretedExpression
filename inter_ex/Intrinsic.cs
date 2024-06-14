using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace InterEx
{
    public partial class IEEngine
    {
        private class IntrinsicSource : IValueImporter, IValueExporter
        {
            public bool Export(Value value, Type type, out object data)
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

                data = default;
                return false;
            }

            public bool Import(object data, out Value value)
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
        }

        public IEEngine()
        {
            this.AddExporter(IntrinsicSource.Instance);
            this.AddImporter(IntrinsicSource.Instance);

            this.AddGlobal("true", true);
            this.AddGlobal("false", false);
            this.AddGlobal("null", null);

            this.InstanceCache.AddPatcher((_, type, info) =>
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
                    }
                }
            });
        }
    }
}
