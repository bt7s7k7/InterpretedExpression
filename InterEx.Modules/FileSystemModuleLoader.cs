namespace InterEx.Modules;

public class FileSystemModuleLoader : IModuleLoader
{
    protected Dictionary<string, ModuleLoadInfo> _cache = [];

    public bool TryLoadModule(ImportLib context, Module importer, string name, out ModuleLoadInfo loadInfo)
    {
        if (!IModuleLoader.ResolveRelativeImportUsingProtocol(name, importer, "file", out var targetPath))
        {
            loadInfo = default;
            return false;
        }

        if (this._cache.TryGetValue(targetPath, out var existing))
        {
            loadInfo = existing;
            return true;
        }

        var content = File.ReadAllText(targetPath);
        var document = IEDocument.ParseCode(targetPath, content);

        loadInfo = new ModuleLoadInfo("file:/" + targetPath, document);
        this._cache.Add(targetPath, loadInfo);

        return true;
    }
}
