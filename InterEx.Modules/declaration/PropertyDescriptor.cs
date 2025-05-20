using System.Diagnostics.CodeAnalysis;
using InterEx.InterfaceTypes;

namespace InterEx.Declaration;

public struct PropertyDescriptor(Value value)
{
    public Value Value = value;
    public Func<Value> Getter;
    public Action<Value> Setter;

    public readonly Value GetValue()
    {
        return this.Getter?.Invoke() ?? this.Value;
    }

    public void SetValue(Value newValue)
    {
        if (this.Setter is { } setter)
        {
            setter.Invoke(newValue);
        }
        else
        {
            if (this.Getter != null)
            {
                ReadonlyPropertyHandler(newValue);
            }
            this.Value = newValue;
        }
    }

    public void Bind(IEReference reference)
    {
        this.Getter = reference.Get;
        this.Setter = reference.Set;
    }

    [DoesNotReturn]
    public static void ReadonlyPropertyHandler(Value _)
    {
        throw new IERuntimeException("Property is readonly");
    }
}
