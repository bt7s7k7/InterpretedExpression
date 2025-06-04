using InterEx.Integration;

namespace InterEx.InterfaceTypes
{
    public record struct CallContext(IEEngine Engine, TypeRegistry.FunctionInfo Function);
}
