namespace InterEx.Modules;

public class FileSystemModuleLoader : IModuleLoader
{
    protected Dictionary<string, Module> _cache = [];

    public bool TryLoadModule(ImportLib context, Module importer, string name, out Module module)
    {
        module = default;

        string targetPath;
        if (name.StartsWith("file://"))
        {
            targetPath = Path.Combine(name[6..]);
        }
        else if (importer.Name.StartsWith("file://") && name.StartsWith("./"))
        {
            targetPath = Path.Combine(Path.GetDirectoryName(importer.Name[6..]), name);
        }
        else
        {
            return false;
        }

        if (this._cache.TryGetValue(targetPath, out var existing))
        {
            module = existing;
            return true;
        }

        var content = File.ReadAllText(targetPath);
        var document = IEDocument.ParseCode(targetPath, content);

        module = new Module(targetPath);
        this._cache.Add(targetPath, module);
        module.Load(context, document);

        return true;
    }
}
