using System;
using InterEx.InterfaceTypes;

namespace InterEx.Integration
{
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
}
