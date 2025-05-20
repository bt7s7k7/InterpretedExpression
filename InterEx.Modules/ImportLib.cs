namespace InterEx.Modules;

public class ImportLib(IEEngine engine)
{
    public readonly IEEngine Engine = engine;
    public readonly List<IModuleLoader> Loaders = [];

    public Module LoadModule(string name, Module importer)
    {
        foreach (var loader in this.Loaders)
        {
            if (loader.TryLoadModule(this, importer, name, out var module))
            {
                return module;
            }
        }

        throw new IERuntimeException($"Failed to find module '{name}'");
    }

}
