using System;
using InterEx.InterfaceTypes;

namespace InterEx.Integration
{
    public interface IValueImporter
    {
        public bool Import(IEIntegrationManager integration, object data, out Value value);
    }

    public interface IValueExporter
    {
        public bool Export(IEIntegrationManager integration, Value value, Type type, out object data);
    }

    public interface IValueProvider
    {
        public bool Find(IEIntegrationManager integration, string name, out Value value);
    }
}
