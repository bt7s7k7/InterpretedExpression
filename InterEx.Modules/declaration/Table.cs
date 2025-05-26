using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using InterEx.CompilerInternals;
using InterEx.Integration;
using InterEx.InterfaceTypes;

namespace InterEx.Declaration;

public class Table : ICustomValue
{
    public Table()
    {
        this.Properties = [];
    }

    public Table(IEnumerable<KeyValuePair<string, PropertyDescriptor>> properties)
    {
        this.Properties = new(properties);
    }

    public Table(IEnumerable<KeyValuePair<string, Value>> properties)
    {
        this.Properties = new(properties.Select(v => new KeyValuePair<string, PropertyDescriptor>(v.Key, new(v.Value))));
    }

    public readonly Dictionary<string, PropertyDescriptor> Properties;

    public void DeclareProperty(string name, Value value)
    {
        if (!this.Properties.TryAdd(name, new PropertyDescriptor(value))) throw new ArgumentException($"Duplicate declaration of property '{name}'");
    }

    public void DeclareProperty(string name, PropertyDescriptor value)
    {
        if (!this.Properties.TryAdd(name, value)) throw new ArgumentException($"Duplicate declaration of property '{name}'");
    }

    public void DeclareProperty(string name, Func<Value> getter, Action<Value> setter)
    {
        if (!this.Properties.TryAdd(name, new PropertyDescriptor
        {
            Getter = getter,
            Setter = setter,
        }))
        {
            throw new ArgumentException($"Duplicate declaration of property '{name}'");
        }
    }

    public void BindProperty(string name, IEReference reference)
    {
        var property = new PropertyDescriptor();
        property.Bind(reference);
        this.DeclareProperty(name, property);
    }

    public bool TryDeleteProperty(string name)
    {
        return this.Properties.Remove(name);
    }

    public void DeleteProperty(string name)
    {
        if (!this.TryDeleteProperty(name))
        {
            throw new KeyNotFoundException($"Property '{name}' does not exist");
        }
    }

    public bool TryGetProperty(string name, out Value value)
    {
        ref var property = ref CollectionsMarshal.GetValueRefOrNullRef(this.Properties, name);

        if (Unsafe.IsNullRef(ref property))
        {
            value = default;
            return false;
        }

        value = property.GetValue();
        return true;
    }

    public Value this[string name]
    {
        get
        {
            ref var property = ref CollectionsMarshal.GetValueRefOrNullRef(this.Properties, name);
            if (Unsafe.IsNullRef(ref property))
            {
                throw new KeyNotFoundException($"Property '{name}' does not exist");
            }

            return property.GetValue();
        }
        set
        {
            ref var property = ref CollectionsMarshal.GetValueRefOrNullRef(this.Properties, name);
            if (Unsafe.IsNullRef(ref property))
            {
                this.DeclareProperty(name, value);
                return;
            }

            property.SetValue(value);
        }
    }

    public bool Get(IEEngine engine, string name, out Value value)
    {
        ref var property = ref CollectionsMarshal.GetValueRefOrNullRef(this.Properties, name);
        if (Unsafe.IsNullRef(ref property))
        {
            value = default;
            return false;
        }

        value = property.GetValue();
        return true;
    }

    public bool Invoke(IEEngine engine, Statement.Invocation invocation, string name, out Value result, Value[] arguments)
    {
        if (name == "_decl")
        {
            var declareProperty = engine.Integration.InstanceCache.GetClassInfo(typeof(Table)).Functions["DeclareProperty"];
            result = engine.BridgeMethodCall(declareProperty, invocation, new Value(this), arguments);
            return true;
        }

        if (name == "_del")
        {
            var deleteProperty = engine.Integration.InstanceCache.GetClassInfo(typeof(Table)).Functions["DeleteProperty"];
            result = engine.BridgeMethodCall(deleteProperty, invocation, new Value(this), arguments);
            return true;
        }

        if (name == "_bind")
        {
            var bindProperty = engine.Integration.InstanceCache.GetClassInfo(typeof(Table)).Functions["BindProperty"];
            result = engine.BridgeMethodCall(bindProperty, invocation, new Value(this), arguments);
            return true;
        }

        if (name == "_props")
        {
            result = new Value(this.Properties);
            return true;
        }

        result = default;
        return false;
    }

    public bool Set(IEEngine engine, string name, Value value)
    {
        if (name.StartsWith('$'))
        {
            this.DeclareProperty(name[1..], value);
            return true;
        }

        ref var property = ref CollectionsMarshal.GetValueRefOrNullRef(this.Properties, name);
        if (Unsafe.IsNullRef(ref property))
        {
            return false;
        }

        property.SetValue(value);
        return true;
    }
}
