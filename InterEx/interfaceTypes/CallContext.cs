using InterEx.Integration;

namespace InterEx.InterfaceTypes
{
    public record struct CallContext(IEEngine Engine, ReflectionCache.FunctionInfo Function);
}
