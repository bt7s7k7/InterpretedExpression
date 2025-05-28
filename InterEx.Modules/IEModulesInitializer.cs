namespace InterEx.Modules;

public static class IEModulesInitializer
{
    public static ImportLib Initialize(IEEngine engine)
    {
        var importLib = new ImportLib(engine);
        engine.AddGlobal("IMPORTLIB", importLib);
        return importLib;
    }
}
