using InterEx.Declaration;

namespace InterEx.Modules;

public static class IEModulesInitializer
{
    public static void InitializeDeclarations(IEEngine engine)
    {
        var reflectionProvider = ReflectionValueProvider.CreateAndRegister(engine.Integration);
        var table = reflectionProvider.AddClass(typeof(Table));
        engine.AddGlobal("new", table);
    }

    public static void Initialize(IEEngine engine)
    {
        InitializeDeclarations(engine);
    }
}
