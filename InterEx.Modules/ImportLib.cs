namespace InterEx.Modules;

public class ImportLib(IEEngine engine)
{
    public readonly IEEngine Engine = engine;
    public readonly List<IModuleLoader> Loaders = [];

    protected readonly Dictionary<string, Module> _cache = [];

    public bool TryResolveModule(string name, Module importer, out ModuleLoadInfo loadInfo)
    {
        foreach (var loader in this.Loaders)
        {
            if (loader.TryLoadModule(this, importer, name, out loadInfo))
            {
                return true;
            }
        }

        loadInfo = default;
        return false;
    }

    public ModuleLoadInfo ResolveModule(string name, Module importer)
    {
        if (this.TryResolveModule(name, importer, out var loadInfo)) return loadInfo;
        throw new ModuleLoadException($"Failed to find module '{name}'");
    }

    public Module LoadModule(string name, Module importer)
    {
        var loadInfo = this.ResolveModule(name, importer);
        if (this._cache.TryGetValue(loadInfo.Name, out var existing))
        {
            return existing;
        }

        var module = new Module(loadInfo.Name);
        this._cache.Add(loadInfo.Name, module);
        module.Load(this, loadInfo.Document);
        return module;
    }
}
